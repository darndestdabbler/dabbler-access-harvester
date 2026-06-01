using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// Resolves harvested Access metadata (<see cref="MigrationInputs"/>) into a
/// <see cref="MigrationSchema"/>: which tables and columns survive, their SQL
/// Server types, identity/nullability/default decisions, the real (enforced) FK
/// edges, and every honest degradation. This is pure logic with no text emission —
/// the DDL emitter, the table orderer, and the data-load generator all consume the
/// resolved model so none of them re-derives "what survived" independently.
/// </summary>
public static class MigrationSchemaResolver
{
    private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

    public static MigrationSchema Resolve(MigrationInputs inputs)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));

        var degradations = new List<MigrationDegradation>();
        var multiValue = BuildMultiValueLookup(inputs.Classification);

        // Determine the migrated table set (exclude system + linked tables).
        var migratedSource = new List<MigrationTable>();
        foreach (var t in inputs.Tables.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (t.IsSystem) continue; // out of scope, silently
            if (t.IsLinked)
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.LinkedTableSkipped, t.Name, null,
                    "Linked/attached table; its schema and data live in an external source and are not migrated."));
                continue;
            }
            migratedSource.Add(t);
        }

        var resolvedTables = new List<MigrationResolvedTable>();
        // Per-table set of columns that actually survived into the schema.
        var survivingCols = new Dictionary<string, HashSet<string>>(IdComparer);
        foreach (var t in migratedSource)
        {
            var resolved = ResolveTable(t, multiValue, degradations);
            resolvedTables.Add(resolved);
            survivingCols[t.Name] = new HashSet<string>(resolved.Columns.Select(c => c.Name), IdComparer);
        }

        var migratedNames = new HashSet<string>(resolvedTables.Select(t => t.Name), IdComparer);
        var foreignKeys = ResolveForeignKeys(inputs.Relationships, migratedNames, survivingCols, degradations);

        degradations.Sort(CompareDegradations);
        return new MigrationSchema(inputs.Database, resolvedTables, foreignKeys, degradations);
    }

    // ── Tables / columns ──────────────────────────────────────────────────────

    private static MigrationResolvedTable ResolveTable(
        MigrationTable table,
        IReadOnlyDictionary<string, HashSet<string>> multiValue,
        List<MigrationDegradation> degradations)
    {
        var pk = table.PrimaryKey;
        var pkCols = pk is null
            ? new HashSet<string>(IdComparer)
            : new HashSet<string>(pk.Fields, IdComparer);

        multiValue.TryGetValue(table.Name, out var mvCols);

        var columns = new List<MigrationResolvedColumn>();
        var survivors = new HashSet<string>(IdComparer);
        foreach (var f in table.Fields.OrderBy(f => f.OrdinalPosition).ThenBy(f => f.Name, StringComparer.Ordinal))
        {
            if (mvCols is not null && mvCols.Contains(f.Name))
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.MultiValueColumn, table.Name, f.Name,
                    "Reviewed classification marks this as a multi-value column (a hidden child table in Access); omitted from CREATE TABLE."));
                continue;
            }

            var map = SqlServerTypeMapper.Map(f);
            if (!map.Mappable)
            {
                degradations.Add(new MigrationDegradation(
                    map.DegradationKind ?? MigrationDegradationKind.UnmappableColumnType,
                    table.Name, f.Name, map.DegradationReason ?? "Unmappable column type."));
                continue;
            }

            if (map.DegradationKind == MigrationDegradationKind.AssumedTypePrecision)
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.AssumedTypePrecision, table.Name, f.Name,
                    map.DegradationReason ?? "Assumed type precision."));

            bool notNull = map.IsIdentity || f.Required || pkCols.Contains(f.Name);
            string? sqlDefault = ResolveDefault(table, f, map, degradations);

            survivors.Add(f.Name);
            columns.Add(new MigrationResolvedColumn(f.Name, f.TypeCode, map.SqlType, map.IsIdentity, notNull, sqlDefault));
        }

        // Primary key — only if every PK column survived.
        IReadOnlyList<string>? pkColumns = null;
        if (pk is not null && pk.Fields.Count > 0)
        {
            var missing = pk.Fields.Where(c => !survivors.Contains(c)).ToList();
            if (missing.Count == 0)
                pkColumns = pk.Fields;
            else
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.UnmappableColumnType, table.Name, string.Join(", ", missing),
                    "Primary key omitted because one or more key columns could not be migrated."));
        }

        return new MigrationResolvedTable(table.Name, columns, pkColumns);
    }

    private static string? ResolveDefault(
        MigrationTable table, MigrationField f, SqlTypeMapping map, List<MigrationDegradation> degradations)
    {
        // Identity columns never carry a DEFAULT, even if Access recorded one.
        if (map.IsIdentity || f.DefaultValue is null) return null;

        if (DefaultValueTranslator.TryTranslate(f.DefaultValue, out var sqlDefault))
            return sqlDefault;

        degradations.Add(new MigrationDegradation(
            MigrationDegradationKind.UntranslatedDefault, table.Name, f.Name,
            $"Access default expression {f.DefaultValue} was not translated; column emitted without a DEFAULT."));
        return null;
    }

    // ── Foreign keys ──────────────────────────────────────────────────────────

    private static List<MigrationResolvedForeignKey> ResolveForeignKeys(
        IReadOnlyList<MigrationRelationship> relationships,
        HashSet<string> migratedNames,
        IReadOnlyDictionary<string, HashSet<string>> survivingCols,
        List<MigrationDegradation> degradations)
    {
        var result = new List<MigrationResolvedForeignKey>();
        var usedNames = new HashSet<string>(IdComparer);

        foreach (var rel in relationships.OrderBy(r => r.Name, StringComparer.Ordinal))
        {
            // System-table relationships are out of scope, silently.
            if (MigrationIrReader.IsSystemTableName(rel.ParentTable) ||
                MigrationIrReader.IsSystemTableName(rel.ChildTable))
                continue;

            if (rel.FieldPairs.Count == 0)
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.ForeignKeyDropped, rel.ChildTable, null,
                    $"Relationship '{rel.Name}' carries no column pairs in the IR; foreign key dropped."));
                continue;
            }

            // Non-enforced relationships are informational in Access; emitting them as a
            // real FK would impose a constraint the source never enforced.
            if (!rel.EnforceReferentialIntegrity)
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.ForeignKeyDropped, rel.ChildTable, null,
                    $"Relationship '{rel.Name}' does not enforce referential integrity in Access; not emitted as a SQL Server foreign key."));
                continue;
            }

            if (!migratedNames.Contains(rel.ParentTable) || !migratedNames.Contains(rel.ChildTable))
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.ForeignKeyDropped, rel.ChildTable, null,
                    $"Relationship '{rel.Name}' references a table not in the migrated set ([{rel.ParentTable}] -> [{rel.ChildTable}]); foreign key dropped."));
                continue;
            }

            var parentCols = survivingCols.GetValueOrDefault(rel.ParentTable) ?? new HashSet<string>(IdComparer);
            var childCols = survivingCols.GetValueOrDefault(rel.ChildTable) ?? new HashSet<string>(IdComparer);

            var missing = rel.FieldPairs
                .Where(p => !parentCols.Contains(p.ParentField) || !childCols.Contains(p.ChildField))
                .ToList();
            if (missing.Count > 0)
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.ForeignKeyDropped, rel.ChildTable, null,
                    $"Relationship '{rel.Name}' references a column that was not migrated; foreign key dropped."));
                continue;
            }

            string fkName = UniqueFkName(rel.ChildTable, rel.ParentTable, usedNames);
            result.Add(new MigrationResolvedForeignKey(
                fkName, rel.ParentTable, rel.ChildTable, rel.FieldPairs, rel.CascadeUpdate, rel.CascadeDelete));
        }

        return result;
    }

    private static string UniqueFkName(string child, string parent, HashSet<string> used)
    {
        string baseName = $"FK_{child}_{parent}";
        string name = baseName;
        int n = 2;
        while (!used.Add(name))
            name = $"{baseName}_{n++}";
        return name;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Total order over degradations so the manifest is byte-stable even when two
    /// degradations share <c>(Table, Column, Kind)</c>.
    /// </summary>
    internal static int CompareDegradations(MigrationDegradation a, MigrationDegradation b)
    {
        int c = string.CompareOrdinal(a.Table, b.Table);
        if (c != 0) return c;
        c = string.CompareOrdinal(a.Column ?? "", b.Column ?? "");
        if (c != 0) return c;
        c = ((int)a.Kind).CompareTo((int)b.Kind);
        if (c != 0) return c;
        return string.CompareOrdinal(a.Reason, b.Reason);
    }

    private static Dictionary<string, HashSet<string>> BuildMultiValueLookup(ClassificationDocument? classification)
    {
        var lookup = new Dictionary<string, HashSet<string>>(IdComparer);
        if (classification is null) return lookup;
        foreach (var t in classification.Tables)
        {
            var cols = t.Columns.Where(c => c.IsMultiValue).Select(c => c.Name).ToList();
            if (cols.Count > 0)
                lookup[t.Name] = new HashSet<string>(cols, IdComparer);
        }
        return lookup;
    }
}
