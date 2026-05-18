using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Black-box integration tests for the `extract` subcommand of the AccessHarvester CLI.
/// These tests invoke the compiled AccessHarvester.exe as a separate process to verify
/// its public contract, including argument parsing, exit codes, and --json-status output.
/// Assertions focus on the successful invocation against a clean fixture (HomeHealthCare.accdb),
/// the shape of the JSON status envelope, and the creation of the expected `raw/` directory
/// structure. These tests do not duplicate the per-object extraction-correctness tests
/// found in the core pipeline and extractor-specific test suites.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CliExtractTests
{
    [SkippableFact]
    public void Extract_WithHomeHealthCareDb_SucceedsAndCreatesRawDirectory()
    {
        AccessAvailability.SkipIfNoAccess();

        string db = Path.Combine(RepoPaths.Samples(), "HomeHealthCare.accdb");
        Skip.IfNot(File.Exists(db), $"Canonical sample DB not present at {db}.");

        string outRoot = Path.Combine(Path.GetTempPath(), $"ah-cli-extract-{Guid.NewGuid():N}");
        try
        {
            var result = CliInvoker.Run("extract", "--db", db, "--out", outRoot, "--json-status");

            // HomeHealthCare.accdb is the clean canonical fixture. Any per-object failures
            // (exit code 3) indicate a regression and should fail this test.
            Assert.Equal(0, result.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(result.StdOut), "Expected JSON status on stdout");

            using var doc = JsonDocument.Parse(result.StdOut);
            var envelope = doc.RootElement;

            Assert.Equal("extract", envelope.GetProperty("Command").GetString());
            Assert.Equal(0, envelope.GetProperty("ExitCode").GetInt32());

            string databasePath = envelope.GetProperty("Database").GetString()!;
            string outputRootPath = envelope.GetProperty("OutputRoot").GetString()!;
            string basePath = envelope.GetProperty("BaseDir").GetString()!;
            string rawPath = envelope.GetProperty("RawDir").GetString()!;

            Assert.False(string.IsNullOrEmpty(databasePath));
            Assert.False(string.IsNullOrEmpty(outputRootPath));
            Assert.False(string.IsNullOrEmpty(basePath));
            Assert.False(string.IsNullOrEmpty(rawPath));

            var extractSummary = envelope.GetProperty("Extract");
            Assert.True(extractSummary.GetProperty("Ok").GetInt32() > 0, "Expected at least one successful extraction");
            Assert.Equal(0, extractSummary.GetProperty("Failed").GetInt32());

            Assert.True(Directory.Exists(rawPath), $"Expected raw output directory to exist at '{rawPath}'. stderr={result.StdErr}");

            var categoryDirs = Directory.GetDirectories(rawPath);
            Assert.True(categoryDirs.Any(), $"Expected at least one category subdirectory (e.g., Forms, Reports) under '{rawPath}'.");
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }
}
