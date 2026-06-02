using System;
using System.IO;
using System.Linq;
using System.Text;
using AccessHarvester.Pipeline.Migration;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// End-to-end golden-script test for the <c>generate-migration</c> feature: emits the
/// full migration for the committed <c>HomeHealthCare</c> golden IR (schema) plus a
/// small committed <c>sanitize</c> CSV slice (data) and byte-compares the result
/// against the committed golden <c>schema.sql</c> / <c>data.sql</c>. Because the
/// generator is deterministic (Session 2), drift in any emit path — type map, PK/FK,
/// FK-safe ordering, IDENTITY framing, value formatting, degradation comments —
/// surfaces here as a golden mismatch.
/// <para>
/// Hermetic and cross-platform: <c>generate-migration</c> reads only the harvested
/// JSON + CSV (no Access COM), so this runs anywhere the repo is checked out.
/// </para>
/// </summary>
public class MigrationGoldenScriptTests
{
    private static string LocateGoldenMigrationDir()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "samples", "GoldenOutput", "HomeHealthCare");
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate samples/GoldenOutput/HomeHealthCare walking up from '{AppContext.BaseDirectory}'.");
    }

    private static Func<string, TextReader?> OpenTableCsv(string dataDir) => tableName =>
    {
        string path = Path.Combine(dataDir, tableName + SanitizeCsvContract.FileExtension);
        return File.Exists(path) ? new StreamReader(path) : null;
    };

    private static MigrationScriptResult GenerateAgainstFixture()
    {
        string root = LocateGoldenMigrationDir();
        string dataDir = Path.Combine(root, "migration", "sample-data");
        var inputs = MigrationIrReader.Read(root, classification: null, sanitizedDataDir: dataDir);
        return SqlServerMigrationGenerator.Generate(inputs, OpenTableCsv(dataDir));
    }

    private static string Lf(string s) => s.Replace("\r\n", "\n");

    [Fact]
    public void Emitted_schema_matches_committed_golden()
    {
        var result = GenerateAgainstFixture();
        string golden = File.ReadAllText(Path.Combine(LocateGoldenMigrationDir(), "migration", "schema.sql"));
        AssertGoldenEqual(Lf(golden), Lf(result.DdlSql), "schema.sql");
    }

    [Fact]
    public void Emitted_data_matches_committed_golden()
    {
        var result = GenerateAgainstFixture();
        string golden = File.ReadAllText(Path.Combine(LocateGoldenMigrationDir(), "migration", "data.sql"));
        AssertGoldenEqual(Lf(golden), Lf(result.DataSql), "data.sql");
    }

    [Fact]
    public void Emitted_scripts_are_dialect_well_formed()
    {
        var result = GenerateAgainstFixture();

        // Schema: every non-system fixture table is created, no system table leaks,
        // identifiers are bracket-quoted, and the FK section is present.
        Assert.Contains("CREATE TABLE [tblClients] (", result.DdlSql);
        Assert.Contains("CREATE TABLE [tlkpStates] (", result.DdlSql);
        Assert.DoesNotContain("CREATE TABLE [MSys", result.DdlSql);
        Assert.Contains("PRIMARY KEY ([ClientID])", result.DdlSql);
        Assert.Contains("ADD CONSTRAINT [FK_tblClients_tlkpStates] FOREIGN KEY ([StateCode]) REFERENCES [tlkpStates] ([StateCode])", result.DdlSql);

        // Every CREATE TABLE / ALTER TABLE batch is terminated by a GO separator.
        int createTables = CountOccurrences(result.DdlSql, "CREATE TABLE [");
        int alterTables = CountOccurrences(result.DdlSql, "ALTER TABLE [");
        Assert.Equal(createTables + alterTables, CountGoBatches(result.DdlSql));

        // Data: IDENTITY_INSERT is balanced (every ON has a matching OFF) and present.
        int identityOn = CountOccurrences(result.DataSql, "SET IDENTITY_INSERT [tlkpPlanStatuses] ON;");
        int identityOff = CountOccurrences(result.DataSql, "SET IDENTITY_INSERT [tlkpPlanStatuses] OFF;");
        Assert.Equal(identityOn, identityOff);
        Assert.True(identityOn >= 1, "Expected at least one IDENTITY_INSERT framing in the data-load golden.");

        // Text values are nvarchar literals (N'...'); the load order put the FK parent
        // (tlkpStates) before its child (tblClients) section.
        Assert.Contains("INSERT INTO [tlkpStates] ([StateCode], [StateName]) VALUES (N'CA', N'California');", result.DataSql);
        Assert.True(
            result.DataSql.IndexOf("[tlkpStates]  (", StringComparison.Ordinal)
            < result.DataSql.IndexOf("[tblClients]", StringComparison.Ordinal),
            "FK parent tlkpStates must be ordered before its child tblClients in the data-load.");
    }

    [Fact]
    public void Generation_is_deterministic_across_runs()
    {
        var a = GenerateAgainstFixture();
        var b = GenerateAgainstFixture();
        Assert.Equal(a.DdlSql, b.DdlSql);
        Assert.Equal(a.DataSql, b.DataSql);
        Assert.Equal(a.TableLoadOrder, b.TableLoadOrder);
    }

    [Fact]
    public void Every_table_without_sample_data_is_flagged_missing_not_dropped()
    {
        var result = GenerateAgainstFixture();
        // Only tlkpStates + tlkpPlanStatuses have committed sample CSVs; the rest must
        // be honestly flagged MissingDataFile (never silently dropped from the schema).
        var missing = result.Degradations
            .Where(d => d.Kind == MigrationDegradationKind.MissingDataFile)
            .Select(d => d.Table)
            .ToList();
        Assert.Contains("tblClients", missing);
        Assert.DoesNotContain("tlkpStates", missing);
        Assert.DoesNotContain("tlkpPlanStatuses", missing);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    // Counts `GO` batch separators emitted on their own line (the generator's NL is \n).
    private static int CountGoBatches(string sql)
    {
        int count = 0;
        foreach (var line in Lf(sql).Split('\n'))
            if (line == "GO") count++;
        return count;
    }

    private static void AssertGoldenEqual(string expected, string actual, string name)
    {
        if (expected == actual) return;

        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        int diff = -1;
        int limit = Math.Min(expectedLines.Length, actualLines.Length);
        for (int i = 0; i < limit; i++)
            if (expectedLines[i] != actualLines[i]) { diff = i; break; }
        if (diff == -1) diff = limit;

        var msg = new StringBuilder();
        msg.AppendLine($"Migration golden drift in {name} at line {diff + 1}.");
        int from = Math.Max(0, diff - 2), to = diff + 3;
        msg.AppendLine("  expected (committed golden):");
        for (int i = from; i < Math.Min(to, expectedLines.Length); i++)
            msg.AppendLine($"    {(i == diff ? '>' : ' ')} {expectedLines[i]}");
        msg.AppendLine("  actual (generator output):");
        for (int i = from; i < Math.Min(to, actualLines.Length); i++)
            msg.AppendLine($"    {(i == diff ? '>' : ' ')} {actualLines[i]}");
        msg.AppendLine();
        msg.AppendLine("  If the change is intentional, refresh the golden per");
        msg.AppendLine("  samples/GoldenOutput/HomeHealthCare/migration/READ-ME-FIRST.md.");
        Assert.Fail(msg.ToString());
    }
}
