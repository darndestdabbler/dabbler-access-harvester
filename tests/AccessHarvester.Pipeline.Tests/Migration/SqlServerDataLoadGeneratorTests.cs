using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.Pipeline.Migration;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// Hermetic tests for <see cref="SqlServerDataLoadGenerator"/>: IDENTITY_INSERT
/// framing, value typing/escaping, NULL handling, FK-safe table order honoring, and
/// the honest-degradation paths (binary omission, missing CSV column, missing file).
/// Inputs are in-memory resolved tables + in-memory CSV strings; no file I/O.
/// </summary>
public class SqlServerDataLoadGeneratorTests
{
    private static MigrationResolvedColumn Col(string name, int typeCode, bool identity = false) =>
        new(name, typeCode, identity ? "int" : "nvarchar(50)", identity, identity || typeCode == 4, null);

    private static MigrationResolvedTable Tbl(string name, params MigrationResolvedColumn[] cols) =>
        new(name, cols, null);

    private static Func<string, TextReader?> Files(Dictionary<string, string> files) =>
        name => files.TryGetValue(name, out var s) ? new StringReader(s) : null;

    private static MigrationDataLoadResult Run(
        MigrationResolvedTable table, string? csv, IReadOnlyList<string>? order = null)
    {
        var files = new Dictionary<string, string>();
        if (csv is not null) files[table.Name] = csv;
        return SqlServerDataLoadGenerator.Generate(
            "Db", new[] { table }, order ?? new[] { table.Name }, Files(files));
    }

    [Fact]
    public void Wraps_inserts_in_identity_insert_when_identity_column_is_loaded()
    {
        var table = Tbl("Users", Col("Id", 4, identity: true), Col("Name", 10));
        var result = Run(table, "Id,Name\r\n1,Alice\r\n2,Bob\r\n");

        Assert.Contains("SET IDENTITY_INSERT [Users] ON;", result.Sql);
        Assert.Contains("INSERT INTO [Users] ([Id], [Name]) VALUES (1, N'Alice');", result.Sql);
        Assert.Contains("INSERT INTO [Users] ([Id], [Name]) VALUES (2, N'Bob');", result.Sql);
        Assert.Contains("SET IDENTITY_INSERT [Users] OFF;", result.Sql);
        // ON precedes the inserts; OFF follows them. (Use full, unambiguous
        // markers — "INSERT" alone also matches inside "IDENTITY_INSERT".)
        int on = result.Sql.IndexOf("IDENTITY_INSERT [Users] ON;", StringComparison.Ordinal);
        int off = result.Sql.IndexOf("IDENTITY_INSERT [Users] OFF;", StringComparison.Ordinal);
        int firstInsert = result.Sql.IndexOf("INSERT INTO [Users]", StringComparison.Ordinal);
        int lastInsert = result.Sql.LastIndexOf("INSERT INTO [Users]", StringComparison.Ordinal);
        Assert.True(on < firstInsert);
        Assert.True(off > lastInsert);
        Assert.Empty(result.Degradations);
    }

    [Fact]
    public void Does_not_emit_identity_insert_for_a_table_without_an_identity_column()
    {
        var table = Tbl("Lookup", Col("Code", 10), Col("Label", 10));
        var result = Run(table, "Code,Label\r\nA,Alpha\r\n");

        Assert.DoesNotContain("IDENTITY_INSERT", result.Sql);
        Assert.Contains("INSERT INTO [Lookup] ([Code], [Label]) VALUES (N'A', N'Alpha');", result.Sql);
    }

    [Fact]
    public void Types_and_escapes_values_and_maps_null()
    {
        var table = Tbl("Mixed", Col("Id", 4, identity: true), Col("Name", 10), Col("Active", 1), Col("Score", 7));
        // Name on row 2 is an unquoted-empty field => NULL; an apostrophe must be doubled.
        var result = Run(table, "Id,Name,Active,Score\r\n1,O'Brien,true,1.5\r\n2,,false,\r\n");

        Assert.Contains("VALUES (1, N'O''Brien', 1, 1.5);", result.Sql);
        Assert.Contains("VALUES (2, NULL, 0, NULL);", result.Sql);
    }

    [Fact]
    public void Omits_binary_column_data_and_flags_it()
    {
        var table = Tbl("Docs", Col("Id", 4, identity: true), Col("Blob", 11)); // 11 = OLE / long binary
        var result = Run(table, "Id,Blob\r\n1,garbage\r\n");

        Assert.Contains("INSERT INTO [Docs] ([Id]) VALUES (1);", result.Sql);
        Assert.DoesNotContain("[Blob]", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.DataColumnOmitted && d.Table == "Docs" && d.Column == "Blob");
    }

    [Fact]
    public void Invalid_numeric_token_degrades_to_NULL_and_is_flagged_once()  // 006 F-2-7
    {
        // A numeric column whose CSV carries a non-numeric token (stale CSV / hand-edit
        // / type drift) must NOT interpolate the raw token into the INSERT — it emits
        // NULL and records a single InvalidDataToken degradation for the column.
        var table = Tbl("Scores", Col("Id", 4, identity: true), Col("Value", 7)); // 7 = Double
        var result = Run(table, "Id,Value\r\n1,1.5\r\n2,not-a-number\r\n3,oops\r\n");

        Assert.Contains("VALUES (1, 1.5);", result.Sql);
        Assert.Contains("VALUES (2, NULL);", result.Sql);
        Assert.Contains("VALUES (3, NULL);", result.Sql);
        // The raw bad tokens are never emitted into the script.
        Assert.DoesNotContain("not-a-number", result.Sql);
        Assert.DoesNotContain("oops", result.Sql);
        // Flagged once for the column, not once per offending row.
        Assert.Single(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.InvalidDataToken && d.Table == "Scores" && d.Column == "Value");
    }

    [Fact]
    public void Flags_a_missing_data_file()
    {
        var table = Tbl("Orphan", Col("Id", 4, identity: true));
        var result = Run(table, csv: null);

        Assert.DoesNotContain("INSERT INTO", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.MissingDataFile && d.Table == "Orphan");
    }

    [Fact]
    public void Flags_a_surviving_column_absent_from_the_csv()
    {
        var table = Tbl("Partial", Col("Id", 4, identity: true), Col("Extra", 10));
        var result = Run(table, "Id\r\n7\r\n"); // CSV has no Extra column

        Assert.Contains("INSERT INTO [Partial] ([Id]) VALUES (7);", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.DataColumnOmitted && d.Table == "Partial" && d.Column == "Extra");
    }

    [Fact]
    public void Does_not_set_identity_insert_when_identity_column_is_absent_from_csv()
    {
        var table = Tbl("T", Col("Id", 4, identity: true), Col("Name", 10));
        var result = Run(table, "Name\r\nAlice\r\n"); // Id (the identity col) absent

        Assert.DoesNotContain("IDENTITY_INSERT", result.Sql);
        Assert.Contains("INSERT INTO [T] ([Name]) VALUES (N'Alice');", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.DataColumnOmitted && d.Column == "Id");
    }

    [Fact]
    public void Empty_table_emits_no_inserts()
    {
        var table = Tbl("Empty", Col("Id", 4, identity: true));
        var result = Run(table, "Id\r\n"); // header only

        Assert.DoesNotContain("INSERT INTO", result.Sql);
        Assert.DoesNotContain("IDENTITY_INSERT", result.Sql);
        Assert.Empty(result.Degradations);
    }

    [Fact]
    public void Loads_tables_in_the_supplied_order()
    {
        var parent = Tbl("Parent", Col("Id", 4, identity: true));
        var child = Tbl("Child", Col("Id", 4, identity: true), Col("ParentId", 4));
        var files = new Dictionary<string, string>
        {
            ["Parent"] = "Id\r\n1\r\n",
            ["Child"] = "Id,ParentId\r\n10,1\r\n",
        };

        var result = SqlServerDataLoadGenerator.Generate(
            "Db", new[] { child, parent }, new[] { "Parent", "Child" }, Files(files));

        Assert.True(result.Sql.IndexOf("INSERT INTO [Parent]", StringComparison.Ordinal)
                  < result.Sql.IndexOf("INSERT INTO [Child]", StringComparison.Ordinal));
    }

    [Fact]
    public void Output_uses_fixed_lf_newlines()
    {
        var table = Tbl("T", Col("Id", 4, identity: true), Col("Name", 10));
        var result = Run(table, "Id,Name\r\n1,A\r\n");

        Assert.DoesNotContain("\r\n", result.Sql);
        Assert.Contains("\n", result.Sql);
    }
}
