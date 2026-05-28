using System;
using System.IO;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// End-to-end black-box tests for the <c>classify</c> subcommand.
/// Skipped on hosts without Microsoft Access (including non-Windows
/// agents and CI runners without the ACE redistributable installed).
/// </summary>
public class ClassifyCommandTests
{
    [SkippableFact]
    public void HomeHealthCare_classify_emits_schema_conformant_json()
    {
        AccessAvailability.SkipIfNoAccess();

        string sampleDb = Path.Combine(RepoPaths.Samples(), "HomeHealthCare.accdb");
        Skip.If(!File.Exists(sampleDb), $"Sample database not present at {sampleDb}.");

        string outDir = Path.Combine(Path.GetTempPath(),
            "classify-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            // 4-minute budget: value-sampling on HomeHealthCare touches every
            // non-key column across hundreds of tables. The smoke run on a
            // dev box takes ~90s; tripling the budget keeps the test from
            // tripping over a slower CI host.
            var result = CliInvoker.RunWithTimeout(240_000,
                "classify", "--in", sampleDb, "--out", outDir, "--json-status");
            // Exit 0 (clean) or 3 (degradations recorded) are both acceptable
            // outcomes for HomeHealthCare — the database may legitimately
            // surface composite-FK or implicit-FK degradations on different
            // hosts. What matters is that the run completed and produced a
            // schema-conformant JSON file plus a structured envelope.
            Assert.True(result.ExitCode == 0 || result.ExitCode == 3,
                $"Unexpected exit code {result.ExitCode}. stderr: {result.StdErr}");

            string expectedFile = Path.Combine(outDir, "homehealthcare", "classification.json");
            Assert.True(File.Exists(expectedFile),
                $"Expected output file not produced: {expectedFile}. stderr: {result.StdErr}");

            // Parse the output and assert the high-level invariants without
            // pinning specific table counts (which are sensitive to the
            // sample database's exact contents).
            using var doc = JsonDocument.Parse(File.ReadAllText(expectedFile));
            var root = doc.RootElement;
            Assert.Equal("1.0",                 root.GetProperty("version").GetString());
            Assert.Equal("HomeHealthCare.accdb", root.GetProperty("database").GetString());
            Assert.True(root.GetProperty("tables").GetArrayLength() > 0,
                "Expected at least one classified table.");

            // Privacy invariant: no scanner-emitted "notes" field carries
            // anything other than the empty string the schema mandates.
            foreach (var table in root.GetProperty("tables").EnumerateArray())
            {
                Assert.Equal(string.Empty,
                    table.GetProperty("humanOverride").GetProperty("notes").GetString());
                foreach (var col in table.GetProperty("columns").EnumerateArray())
                {
                    Assert.Equal(string.Empty,
                        col.GetProperty("humanOverride").GetProperty("notes").GetString());
                }
            }

            // --json-status envelope on stdout.
            Assert.False(string.IsNullOrWhiteSpace(result.StdOut),
                "Expected --json-status envelope on stdout.");
            using var statusDoc = JsonDocument.Parse(result.StdOut);
            Assert.Equal("classify", statusDoc.RootElement.GetProperty("Command").GetString());
            int statusExit = statusDoc.RootElement.GetProperty("ExitCode").GetInt32();
            Assert.True(statusExit == 0 || statusExit == 3,
                $"Status envelope ExitCode = {statusExit}; expected 0 or 3.");
        }
        finally
        {
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void Classify_with_missing_input_reports_usage_error()
    {
        var result = CliInvoker.Run("classify");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--in", result.StdErr);
    }

    [Fact]
    public void Classify_with_nonexistent_db_reports_file_not_found()
    {
        var result = CliInvoker.Run(
            "classify",
            "--in", "C:/nonexistent-db-for-testing.accdb",
            "--out", Path.GetTempPath());
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }
}
