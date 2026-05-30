using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Black-box tests for the `translate` subcommand. Invokes
/// AccessHarvester.exe out-of-process and asserts on exit code,
/// --json-status envelope, and the kb tier directory shape.
///
/// The subcommand consumes the `raw/` output of a real Access extraction,
/// so this class shares a single `extract` run across its tests via
/// <see cref="ExtractFixture"/>. Fixture work happens lazily on first
/// use so the SkipIfNoAccess / fixture-file-present gates remain at the
/// test-method boundary — fixture-constructor Skip exceptions surface as
/// "fixture failed to initialize" rather than the clean Skipped verdict
/// xunit.skippablefact provides at the test level.
///
/// Does NOT duplicate the in-process Translator unit tests or the
/// canonical-golden regression gate; this class is strictly about the
/// CLI exe's public contract for the `translate` verb.
/// </summary>
[SupportedOSPlatform("windows")]
[Collection("HomeHealthCareCom")]
public sealed class CliTranslateTests : IClassFixture<CliTranslateTests.ExtractFixture>
{
    private readonly ExtractFixture _fixture;

    public CliTranslateTests(ExtractFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public void Translate_AgainstHHCRaw_EmitsKbTierDirectories()
    {
        AccessAvailability.SkipIfNoAccess();

        string db = Path.Combine(RepoPaths.Samples(), "HomeHealthCare.accdb");
        Skip.IfNot(File.Exists(db), $"Canonical sample DB not present at {db}.");

        string rawDir = _fixture.EnsureExtracted(db);

        string kbDir = Path.Combine(Path.GetTempPath(), $"ah-cli-translate-{Guid.NewGuid():N}");
        try
        {
            var result = CliInvoker.Run("translate", "--in", rawDir, "--out", kbDir, "--json-status");

            Assert.Equal(0, result.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(result.StdOut), "Expected JSON status on stdout.");

            using var doc = JsonDocument.Parse(result.StdOut);
            var envelope = doc.RootElement;

            Assert.Equal("translate", envelope.GetProperty("Command").GetString());
            Assert.False(string.IsNullOrEmpty(envelope.GetProperty("RawDir").GetString()));
            Assert.Equal(0, envelope.GetProperty("ExitCode").GetInt32());

            var translateSummary = envelope.GetProperty("Translate");
            Assert.True(
                translateSummary.GetProperty("Ok").GetInt32() > 0,
                "Expected at least one successful translation.");
            Assert.Equal(0, translateSummary.GetProperty("Failed").GetInt32());

            string reportedKbDir = envelope.GetProperty("KbDir").GetString()!;
            Assert.False(string.IsNullOrEmpty(reportedKbDir));
            Assert.True(
                Directory.Exists(reportedKbDir),
                $"CLI reported KbDir was not created: {reportedKbDir}. stderr={result.StdErr}");

            Assert.True(
                Directory.GetDirectories(reportedKbDir).Any(),
                $"Expected at least one category subdirectory under {reportedKbDir}.");
        }
        finally
        {
            if (Directory.Exists(kbDir))
                Directory.Delete(kbDir, recursive: true);
        }
    }

    public sealed class ExtractFixture : IDisposable
    {
        private readonly object _gate = new();
        private string? _extractRoot;
        private string? _rawDir;

        public string EnsureExtracted(string db)
        {
            lock (_gate)
            {
                if (_rawDir is not null) return _rawDir;

                string extractRoot = Path.Combine(
                    Path.GetTempPath(), $"ah-cli-extract-fixture-{Guid.NewGuid():N}");
                Directory.CreateDirectory(extractRoot);

                var result = CliInvoker.Run(
                    "extract", "--db", db, "--out", extractRoot, "--json-status");

                if (result.ExitCode != 0)
                {
                    try { Directory.Delete(extractRoot, recursive: true); } catch { /* best-effort */ }
                    throw new Xunit.Sdk.XunitException(
                        $"ExtractFixture: 'extract' exited with code {result.ExitCode}. " +
                        $"stdout={result.StdOut} stderr={result.StdErr}");
                }

                using var doc = JsonDocument.Parse(result.StdOut);
                string rawDir = doc.RootElement.GetProperty("RawDir").GetString()
                    ?? throw new Xunit.Sdk.XunitException(
                        "ExtractFixture: 'extract' JSON envelope missing RawDir.");

                _extractRoot = extractRoot;
                _rawDir = rawDir;
                return _rawDir;
            }
        }

        public void Dispose()
        {
            if (_extractRoot is not null && Directory.Exists(_extractRoot))
            {
                try { Directory.Delete(_extractRoot, recursive: true); } catch { /* best-effort */ }
            }
        }
    }
}
