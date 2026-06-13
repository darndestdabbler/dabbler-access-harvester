using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>The data-load portion of a migration: the INSERT script plus any honest degradations.</summary>
public sealed record MigrationDataLoadResult(
    string Sql,
    IReadOnlyList<MigrationDegradation> Degradations);

/// <summary>
/// Generates deterministic SQL Server data-load (<c>INSERT</c>) statements from the
/// <c>sanitize</c> subcommand's CSV-per-table output, in FK-safe table order
/// (parents before children, supplied by <see cref="TableDependencyOrderer"/>).
/// <para>
/// The sanitized CSV is the source of truth: the masking / preserve / remap
/// decisions were already applied upstream by <c>sanitize</c> (an ID is preserved
/// or remapped <em>consistently across its PK and every FK</em> — plan § 5), so the
/// data-load loads those exact values verbatim and never re-masks. Because the CSV
/// always carries explicit ID values, loading them into an <c>IDENTITY</c> column
/// requires <c>SET IDENTITY_INSERT ON</c>; the generator wraps any table whose
/// identity column is being loaded from the CSV in <c>SET IDENTITY_INSERT
/// [T] ON … OFF</c> within a single batch.
/// </para>
/// <para>
/// Columns that survived into the schema but cannot be loaded from the text CSV
/// (binary / OLE), or that the CSV has no data for, are omitted from the
/// <c>INSERT</c> column list and flagged as
/// <see cref="MigrationDegradationKind.DataColumnOmitted"/> — never fabricated.
/// </para>
/// </summary>
public static class SqlServerDataLoadGenerator
{
    private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;
    private static string NL => SqlServerDdlEmitter.NL;
    private static string Quote(string id) => SqlServerDdlEmitter.Quote(id);

    /// <summary>
    /// Generates the data-load script. <paramref name="openTableCsv"/> returns a
    /// reader over a table's sanitized CSV (caller owns disposal via the returned
    /// reader), or <c>null</c> when no CSV exists for that table.
    /// </summary>
    public static MigrationDataLoadResult Generate(
        string database,
        IReadOnlyList<MigrationResolvedTable> tables,
        IReadOnlyList<string> orderedTableNames,
        Func<string, TextReader?> openTableCsv)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        if (orderedTableNames is null) throw new ArgumentNullException(nameof(orderedTableNames));
        if (openTableCsv is null) throw new ArgumentNullException(nameof(openTableCsv));

        var byName = new Dictionary<string, MigrationResolvedTable>(IdComparer);
        foreach (var t in tables) byName[t.Name] = t;

        var degradations = new List<MigrationDegradation>();
        var sb = new StringBuilder();
        Header(sb, database);

        foreach (var name in orderedTableNames)
        {
            if (!byName.TryGetValue(name, out var table)) continue; // not a migrated table
            EmitTable(sb, table, openTableCsv, degradations);
        }

        return new MigrationDataLoadResult(sb.ToString(), degradations);
    }

    private static void EmitTable(
        StringBuilder sb,
        MigrationResolvedTable table,
        Func<string, TextReader?> openTableCsv,
        List<MigrationDegradation> degradations)
    {
        // Resolve the loadable INSERT columns (schema order, for a deterministic
        // column list aligned with CREATE TABLE).
        TextReader? reader = openTableCsv(table.Name);
        if (reader is null)
        {
            degradations.Add(new MigrationDegradation(
                MigrationDegradationKind.MissingDataFile, table.Name, null,
                "No sanitized CSV data file was found for this table; CREATE TABLE stands but no rows were loaded."));
            Section(sb, table.Name, "no data file");
            return;
        }

        CsvTable csv;
        using (reader) csv = CsvTableReader.Read(reader);

        var headerIndex = new Dictionary<string, int>(IdComparer);
        for (int i = 0; i < csv.Header.Count; i++)
            headerIndex[csv.Header[i]] = i; // last-wins on dup header (degenerate input)

        var insertCols = new List<(MigrationResolvedColumn Col, int CsvIndex)>();
        foreach (var c in table.Columns)
        {
            if (SqlValueFormatter.IsBinary(c.TypeCode))
            {
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.DataColumnOmitted, table.Name, c.Name,
                    "Binary/OLE column data cannot round-trip through the text CSV; column kept in the schema but omitted from INSERT."));
                continue;
            }
            if (headerIndex.TryGetValue(c.Name, out int idx))
                insertCols.Add((c, idx));
            else
                degradations.Add(new MigrationDegradation(
                    MigrationDegradationKind.DataColumnOmitted, table.Name, c.Name,
                    "Surviving column has no matching column in the sanitized CSV; omitted from INSERT (SQL Server supplies its default/NULL)."));
        }

        Section(sb, table.Name, $"{csv.Rows.Count} row{(csv.Rows.Count == 1 ? "" : "s")}");

        if (insertCols.Count == 0 || csv.Rows.Count == 0)
        {
            if (csv.Rows.Count > 0)
                sb.Append("-- (no loadable columns; ").Append(csv.Rows.Count).Append(" row(s) skipped)").Append(NL);
            return;
        }

        // IDENTITY_INSERT only when the identity column is actually being loaded.
        var identity = table.IdentityColumn;
        bool identityInsert = identity is not null && insertCols.Any(ic => IdComparer.Equals(ic.Col.Name, identity.Name));

        if (identityInsert)
        {
            sb.Append("SET IDENTITY_INSERT ").Append(Quote(table.Name)).Append(" ON;").Append(NL);
        }

        string columnList = string.Join(", ", insertCols.Select(ic => Quote(ic.Col.Name)));
        // Track numeric/bit columns already flagged for an invalid token so the
        // degradation fires once per column, not once per offending row (F-2-7).
        var invalidTokenCols = new HashSet<string>(IdComparer);
        foreach (var row in csv.Rows)
        {
            var values = new List<string>(insertCols.Count);
            foreach (var ic in insertCols)
            {
                string? cell = ic.CsvIndex < row.Count ? row[ic.CsvIndex] : null;
                if (SqlValueFormatter.IsInvalidDataToken(ic.Col.TypeCode, cell) && invalidTokenCols.Add(ic.Col.Name))
                    degradations.Add(new MigrationDegradation(
                        MigrationDegradationKind.InvalidDataToken, table.Name, ic.Col.Name,
                        $"Sanitized CSV carried a token ('{cell}') that is not a valid numeric/bit literal for this column type; emitted NULL for the offending cell(s) instead of interpolating the raw token into the INSERT."));
                values.Add(SqlValueFormatter.Format(ic.Col.TypeCode, cell));
            }
            sb.Append("INSERT INTO ").Append(Quote(table.Name)).Append(" (").Append(columnList)
              .Append(") VALUES (").Append(string.Join(", ", values)).Append(");").Append(NL);
        }

        if (identityInsert)
        {
            sb.Append("SET IDENTITY_INSERT ").Append(Quote(table.Name)).Append(" OFF;").Append(NL);
        }

        sb.Append("GO").Append(NL);
    }

    private static void Header(StringBuilder sb, string database)
    {
        Line(sb, "-- =====================================================================");
        Line(sb, "-- SQL Server migration script (data) for database: " + database);
        Line(sb, "-- Generated by AccessHarvester generate-migration.");
        Line(sb, "-- Tables loaded parent-before-child for foreign-key safety; values are the sanitized rows verbatim.");
        Line(sb, "-- =====================================================================");
    }

    private static void Section(StringBuilder sb, string table, string note)
    {
        Line(sb, "");
        Line(sb, "-- ---------------------------------------------------------------------");
        Line(sb, $"-- {Quote(table)}  ({note})");
        Line(sb, "-- ---------------------------------------------------------------------");
    }

    private static void Line(StringBuilder sb, string text) => sb.Append(text).Append(NL);
}
