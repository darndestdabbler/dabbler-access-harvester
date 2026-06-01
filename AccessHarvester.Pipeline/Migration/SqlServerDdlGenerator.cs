using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>The DDL portion of a migration: the script text plus any honest degradations.</summary>
public sealed record MigrationDdlResult(
    string Sql,
    IReadOnlyList<MigrationDegradation> Degradations);

/// <summary>
/// Generates deterministic SQL Server schema DDL (<c>CREATE TABLE</c> + primary
/// keys, then <c>ALTER TABLE ... ADD FOREIGN KEY</c>) from harvested Access
/// metadata. Columns and tables that cannot migrate cleanly are flagged as
/// <see cref="MigrationDegradation"/>s and omitted — never silently dropped,
/// never fabricated.
/// <para>
/// Foreign keys are emitted as post-create <c>ALTER TABLE</c> statements (not
/// inline) so the <c>CREATE TABLE</c> order is independent of the FK graph; the
/// FK-safe <em>data-load</em> ordering is Session 2's concern. Output carries no
/// timestamps so it is byte-deterministic for golden comparison.
/// </para>
/// </summary>
public static class SqlServerDdlGenerator
{
    private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Fixed line terminator. The script is emitted with explicit <c>\n</c> (never
    /// <c>Environment.NewLine</c>) so the artifact is byte-identical regardless of
    /// the host OS — a prerequisite for the Session-3 golden-script comparison.
    /// </summary>
    private const string NL = "\n";

    public static MigrationDdlResult GenerateDdl(MigrationInputs inputs)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));

        var degradations = new List<MigrationDegradation>();

        // Reviewed-classification multi-value lookup: table -> set of multi-value column names.
        var multiValue = BuildMultiValueLookup(inputs.Classification);

        // Determine the migrated table set (exclude system + linked tables).
        var migratedTables = new List<MigrationTable>();
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
            migratedTables.Add(t);
        }

        var migratedNames = new HashSet<string>(migratedTables.Select(t => t.Name), IdComparer);
        // Per-table set of columns that actually survived into the CREATE TABLE.
        var survivingCols = new Dictionary<string, HashSet<string>>(IdComparer);

        var tableSql = new List<string>();
        foreach (var t in migratedTables)
        {
            var (createSql, survivors) = BuildCreateTable(t, multiValue, degradations);
            tableSql.Add(createSql);
            survivingCols[t.Name] = survivors;
        }

        var fkSql = BuildForeignKeys(inputs.Relationships, migratedNames, survivingCols, degradations);

        string sql = Assemble(inputs.Database, tableSql, fkSql);
        degradations.Sort(CompareDegradations);
        return new MigrationDdlResult(sql, degradations);
    }

    // ── CREATE TABLE ─────────────────────────────────────────────────────────

    private static (string Sql, HashSet<string> Survivors) BuildCreateTable(
        MigrationTable table,
        IReadOnlyDictionary<string, HashSet<string>> multiValue,
        List<MigrationDegradation> degradations)
    {
        var survivors = new HashSet<string>(IdComparer);

        var pk = table.PrimaryKey;
        var pkCols = pk is null
            ? new HashSet<string>(IdComparer)
            : new HashSet<string>(pk.Fields, IdComparer);

        multiValue.TryGetValue(table.Name, out var mvCols);

        var columnLines = new List<string>();
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

            survivors.Add(f.Name);
            columnLines.Add(BuildColumnLine(table, f, map, pkCols, degradations));
        }

        // Primary key — only if every PK column survived.
        string? pkLine = null;
        if (pk is not null && pk.Fields.Count > 0)
        {
            var missing = pk.Fields.Where(c => !survivors.Contains(c)).ToList();
            if (missing.Count == 0)
            {
                string cols = string.Join(", ", pk.Fields.Select(Quote));
                pkLine = $"CONSTRAINT {Quote("PK_" + table.Name)} PRIMARY KEY ({cols})";
            }
            else
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.UnmappableColumnType, table.Name, string.Join(", ", missing),
                    "Primary key omitted because one or more key columns could not be migrated."));
            }
        }

        var body = new List<string>(columnLines);
        if (pkLine is not null) body.Add(pkLine);

        var sb = new StringBuilder();
        sb.Append("CREATE TABLE ").Append(Quote(table.Name)).Append(" (").Append(NL);
        for (int i = 0; i < body.Count; i++)
        {
            sb.Append("    ").Append(body[i]);
            if (i < body.Count - 1) sb.Append(',');
            sb.Append(NL);
        }
        sb.Append(");");
        return (sb.ToString(), survivors);
    }

    private static string BuildColumnLine(
        MigrationTable table,
        MigrationField f,
        SqlTypeMapping map,
        HashSet<string> pkCols,
        List<MigrationDegradation> degradations)
    {
        var sb = new StringBuilder();
        sb.Append(Quote(f.Name)).Append(' ').Append(map.SqlType);

        if (map.IsIdentity)
            sb.Append(" IDENTITY(1,1)");

        // PK columns and identity columns must be NOT NULL.
        bool notNull = map.IsIdentity || f.Required || pkCols.Contains(f.Name);
        sb.Append(notNull ? " NOT NULL" : " NULL");

        // Defaults — never on an identity column.
        if (!map.IsIdentity && f.DefaultValue is not null)
        {
            if (DefaultValueTranslator.TryTranslate(f.DefaultValue, out var sqlDefault))
                sb.Append(" DEFAULT (").Append(sqlDefault).Append(')');
            else
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.UntranslatedDefault, table.Name, f.Name,
                    $"Access default expression {f.DefaultValue} was not translated; column emitted without a DEFAULT."));
        }

        return sb.ToString();
    }

    // ── FOREIGN KEY ────────────────────────────────────────────────────────

    private static List<string> BuildForeignKeys(
        IReadOnlyList<MigrationRelationship> relationships,
        HashSet<string> migratedNames,
        IReadOnlyDictionary<string, HashSet<string>> survivingCols,
        List<MigrationDegradation> degradations)
    {
        var fkSql = new List<string>();
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
            string childList = string.Join(", ", rel.FieldPairs.Select(p => Quote(p.ChildField)));
            string parentList = string.Join(", ", rel.FieldPairs.Select(p => Quote(p.ParentField)));

            var sb = new StringBuilder();
            sb.Append("ALTER TABLE ").Append(Quote(rel.ChildTable))
              .Append(" ADD CONSTRAINT ").Append(Quote(fkName))
              .Append(" FOREIGN KEY (").Append(childList).Append(')')
              .Append(" REFERENCES ").Append(Quote(rel.ParentTable))
              .Append(" (").Append(parentList).Append(')');
            if (rel.CascadeUpdate) sb.Append(" ON UPDATE CASCADE");
            if (rel.CascadeDelete) sb.Append(" ON DELETE CASCADE");
            sb.Append(';');
            fkSql.Add(sb.ToString());
        }

        return fkSql;
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

    // ── Assembly + helpers ───────────────────────────────────────────────────

    private static string Assemble(string database, List<string> tableSql, List<string> fkSql)
    {
        var sb = new StringBuilder();
        Line(sb, "-- =====================================================================");
        Line(sb, "-- SQL Server migration script (schema) for database: " + database);
        Line(sb, "-- Generated by AccessHarvester generate-migration.");
        Line(sb, "-- Deterministic: fixed LF newlines, no timestamps. Review flagged degradations in the manifest.");
        Line(sb, "-- =====================================================================");
        Line(sb, "");
        Line(sb, "-- ---------------------------------------------------------------------");
        Line(sb, "-- Tables");
        Line(sb, "-- ---------------------------------------------------------------------");
        foreach (var t in tableSql)
        {
            Line(sb, "");
            Line(sb, t);
            Line(sb, "GO");
        }

        if (fkSql.Count > 0)
        {
            Line(sb, "");
            Line(sb, "-- ---------------------------------------------------------------------");
            Line(sb, "-- Foreign keys");
            Line(sb, "-- ---------------------------------------------------------------------");
            foreach (var fk in fkSql)
            {
                Line(sb, "");
                Line(sb, fk);
                Line(sb, "GO");
            }
        }

        return sb.ToString();
    }

    /// <summary>Appends <paramref name="text"/> followed by the fixed LF terminator.</summary>
    private static void Line(StringBuilder sb, string text) => sb.Append(text).Append(NL);

    /// <summary>Bracket-quotes a SQL Server identifier, doubling any embedded <c>]</c>.</summary>
    private static string Quote(string identifier) =>
        "[" + identifier.Replace("]", "]]") + "]";

    private static int CompareDegradations(MigrationDegradation a, MigrationDegradation b)
    {
        int c = string.CompareOrdinal(a.Table, b.Table);
        if (c != 0) return c;
        c = string.CompareOrdinal(a.Column ?? "", b.Column ?? "");
        if (c != 0) return c;
        c = ((int)a.Kind).CompareTo((int)b.Kind);
        if (c != 0) return c;
        return string.CompareOrdinal(a.Reason, b.Reason); // total order: stable even for same (Table,Column,Kind)
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
