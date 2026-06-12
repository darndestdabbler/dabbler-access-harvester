using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Smoke tests that re-run a subset of the existing CLI black-box checks
/// (bootstrap / parse / generate-docs) against the self-contained
/// published artifact at <c>publish/&lt;rid&gt;/AccessHarvester.exe</c>
/// rather than the per-test-bin copy. Verifies the packaged binary a
/// downstream consumer would install behaves identically to the
/// source-build CLI exercised by <see cref="CliBootstrapTests"/>,
/// <see cref="CliParseTests"/>, and <see cref="CliGenerateDocsTests"/>.
///
/// The tests are <c>[SkippableFact]</c>-gated: when no published
/// artifact is present (developer skipped <c>tools/publish/publish.ps1</c>,
/// CI did not produce one), the tests skip cleanly rather than fail.
/// Run <c>pwsh tools/publish/publish.ps1</c> from the repo root to make
/// these tests execute.
/// </summary>
public sealed class PackagedBinarySmokeTests
{
    [SkippableFact]
    public void PackagedBinary_NoArgs_PrintsUsage_AndReturnsExitOne()
    {
        string? exe = PublishedArtifact.TryLocate();
        Skip.If(exe is null, "Published artifact absent; run tools/publish/publish.ps1.");

        var result = RunPackaged(exe!);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("AccessHarvester", result.StdErr);
        Assert.Contains("Usage:", result.StdErr);
    }

    [SkippableFact]
    public void PackagedBinary_HelpFlag_PrintsUsage_AndReturnsExitZero()
    {
        string? exe = PublishedArtifact.TryLocate();
        Skip.If(exe is null, "Published artifact absent; run tools/publish/publish.ps1.");

        var result = RunPackaged(exe!, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdErr);
    }

    [SkippableFact]
    public void PackagedBinary_Parse_AgainstCommittedFixture_Succeeds()
    {
        string? exe = PublishedArtifact.TryLocate();
        Skip.If(exe is null, "Published artifact absent; run tools/publish/publish.ps1.");

        string fixture = Path.Combine(RepoPaths.TestCases(), "01-simple-validation.bas");
        Assert.True(File.Exists(fixture), $"Fixture missing: {fixture}");

        var result = RunPackaged(exe!, "parse", "--in", fixture);

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal("parsed", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.GetProperty("tree_stats").GetProperty("node_count").GetInt32() > 0);
    }

    [SkippableFact]
    public void PackagedBinary_GenerateDocs_AgainstCommittedManifest_WritesSchemaSummary()
    {
        string? exe = PublishedArtifact.TryLocate();
        Skip.If(exe is null, "Published artifact absent; run tools/publish/publish.ps1.");

        string manifest = Path.Combine(RepoPaths.Examples(), "homehealthcare-output.json");
        Assert.True(File.Exists(manifest), $"Example manifest missing: {manifest}");

        string outDir = Path.Combine(Path.GetTempPath(), $"ah-pkg-{Guid.NewGuid():N}");
        try
        {
            var result = RunPackaged(exe!, "generate-docs", "--in", manifest, "--out", outDir);
            Assert.Equal(0, result.ExitCode);

            string expected = Path.Combine(outDir, "homehealthcare", "schema-summary.md");
            Assert.True(File.Exists(expected),
                $"Expected schema summary not written. stderr={result.StdErr}");
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }

    [SkippableFact]
    public void PackagedBinary_Validate_ConformantManifest_ReturnsZero()
    {
        string? exe = PublishedArtifact.TryLocate();
        Skip.If(exe is null, "Published artifact absent; run tools/publish/publish.ps1.");

        string manifest = Path.Combine(RepoPaths.Examples(), "homehealthcare-output.json");
        Assert.True(File.Exists(manifest), $"Example manifest missing: {manifest}");

        var result = RunPackaged(exe!, "validate", "--manifest", manifest);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Validation passed", result.StdErr);
    }

    private static CliResult RunPackaged(string exePath, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start packaged CLI at {exePath}");

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        // Self-extracting single-file bootstrap can take a few seconds on first launch
        // while native libs are unpacked to %TEMP%; give it 90s to cover the cold-start
        // case without masking real hangs.
        if (!proc.WaitForExit(90_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException($"Packaged CLI did not exit within 90s. args=[{string.Join(' ', args)}]");
        }
        return new CliResult(proc.ExitCode, stdoutTask.Result, stderrTask.Result);
    }
}
