using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// End-to-end black-box tests for the <c>generate-migration</c> subcommand.
///
/// <para>
/// Unlike the other CLI command tests, these are plain <see cref="FactAttribute"/>s
/// (not <c>SkippableFact</c>) and need no <c>[Collection("HomeHealthCareCom")]</c>:
/// <c>generate-migration</c> is COM-free — it reads the committed golden IR, the
/// optional classification, and the committed <c>sanitize</c> CSV slice, so it never
/// opens the <c>.accdb</c> and cannot race the ACE OLE DB file lock.
/// </para>
/// </summary>
public class GenerateMigrationCommandTests
{
    private static string GoldenFixtureRoot()
        => Path.Combine(RepoPaths.Samples(), "GoldenOutput", "HomeHealthCare");

    [Fact]
    public void GenerateMigration_against_golden_ir_with_sample_data_emits_scripts_and_manifest()
    {
        string fixtureRoot = GoldenFixtureRoot();
        Skip.IfNot(Directory.Exists(fixtureRoot), $"Golden fixture missing at {fixtureRoot}.");

        string dataDir = Path.Combine(fixtureRoot, "migration", "sample-data");
        string workDir = Path.Combine(Path.GetTempPath(),
            "genmig-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(workDir);

            var result = CliInvoker.Run(
                "generate-migration",
                "--in", fixtureRoot,
                "--data", dataDir,
                "--out", workDir,
                "--json-status");

            // 11 of the 13 tables have no sample CSV -> degradations -> exit 3.
            Assert.Equal(3, result.ExitCode);

            string outSubdir    = Path.Combine(workDir, "homehealthcare");
            string schemaPath    = Path.Combine(outSubdir, "schema.sql");
            string dataPath       = Path.Combine(outSubdir, "data.sql");
            string manifestPath   = Path.Combine(outSubdir, "migration-manifest.json");

            Assert.True(File.Exists(schemaPath),   $"schema.sql missing: {schemaPath}");
            Assert.True(File.Exists(dataPath),     $"data.sql missing: {dataPath}");
            Assert.True(File.Exists(manifestPath), $"manifest missing: {manifestPath}");

            // Schema: CREATE TABLE + FK present; no system table leaks.
            string schema = File.ReadAllText(schemaPath);
            Assert.Contains("CREATE TABLE [tblClients] (", schema);
            Assert.Contains("ADD CONSTRAINT [FK_tblClients_tlkpStates]", schema);
            Assert.DoesNotContain("CREATE TABLE [MSys", schema);

            // Data: real INSERTs for the sample tables, IDENTITY framing where needed.
            string data = File.ReadAllText(dataPath);
            Assert.Contains("INSERT INTO [tlkpStates] ([StateCode], [StateName]) VALUES (N'CA', N'California');", data);
            Assert.Contains("SET IDENTITY_INSERT [tlkpPlanStatuses] ON;", data);

            // Manifest shape.
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            Assert.Equal("MigrationManifest", root.GetProperty("SchemaType").GetString());
            Assert.Equal("HomeHealthCare", root.GetProperty("Database").GetString());
            Assert.Equal("schema.sql", root.GetProperty("GeneratedFiles").GetProperty("Schema").GetString());
            Assert.Equal(13, root.GetProperty("TableLoadOrder").GetArrayLength());
            Assert.True(root.GetProperty("DegradationCount").GetInt32() >= 11);
            Assert.Contains(
                root.GetProperty("Degradations").EnumerateArray(),
                d => d.GetProperty("Kind").GetString() == "MissingDataFile"
                  && d.GetProperty("Table").GetString() == "tblClients");

            // --json-status envelope.
            using var status = JsonDocument.Parse(result.StdOut);
            Assert.Equal("generate-migration", status.RootElement.GetProperty("Command").GetString());
            Assert.Equal(13, status.RootElement.GetProperty("TablesOrdered").GetInt32());
            Assert.Equal(3, status.RootElement.GetProperty("ExitCode").GetInt32());
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void GenerateMigration_with_missing_args_reports_usage_error()
    {
        var result = CliInvoker.Run("generate-migration");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--in", result.StdErr);
    }

    [Fact]
    public void GenerateMigration_with_unknown_option_reports_usage_error()
    {
        // A mistyped switch (e.g. --datta for --data) must surface as a usage error,
        // never silently degrade the migration to a missing-data "success".
        var result = CliInvoker.Run(
            "generate-migration",
            "--in", GoldenFixtureRoot(),
            "--out", Path.GetTempPath(),
            "--datta", "C:/whatever");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown option", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateMigration_with_nonexistent_input_reports_error()
    {
        var result = CliInvoker.Run(
            "generate-migration",
            "--in", "C:/nonexistent-harvested-root",
            "--out", Path.GetTempPath());
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("input directory not found", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }
}
