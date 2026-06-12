using System;
using System.IO;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Determinism gate — invokes each hermetic CLI subcommand twice against the
/// same input and asserts byte-identical output.
/// <para>
/// Coverage of `run` determinism is delegated to
/// <c>AccessHarvester.Integration.Pipeline.CanonicalGoldenRegressionTests</c>,
/// which exercises <c>Pipeline.Run()</c> against the committed canonical
/// golden corpus. That single-run-vs-frozen-reference check is a strictly
/// stronger property than two-runs-same-session and also subsumes
/// <c>extract</c> and <c>translate</c>, since <c>run = extract + translate
/// + enrich</c>.
/// </para>
/// </summary>
public sealed class CliDeterminismTests
{
    [Fact]
    public void Parse_TwoInvocations_ProduceIdenticalStdOut()
    {
        string fixture = Path.Combine(RepoPaths.TestCases(), "01-simple-validation.bas");
        Assert.True(File.Exists(fixture), $"Fixture missing: {fixture}");

        var first = CliInvoker.Run("parse", "--in", fixture);
        var second = CliInvoker.Run("parse", "--in", fixture);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(first.StdOut, second.StdOut);
    }

    [Fact]
    public void GenerateDocs_TwoInvocations_ProduceIdenticalSchemaSummary()
    {
        // homehealthcare-output.json is the manifest-shaped example
        // (other examples/ entries are per-form IR records, not valid
        // manifest inputs). See CliGenerateDocsTests for the same
        // fixture-choice rationale and examples/README.md for provenance.
        string manifest = Path.Combine(RepoPaths.Examples(), "homehealthcare-output.json");
        Assert.True(File.Exists(manifest), $"Example manifest missing: {manifest}");

        string outDir1 = Path.Combine(Path.GetTempPath(), $"ah-int-{Guid.NewGuid():N}");
        string outDir2 = Path.Combine(Path.GetTempPath(), $"ah-int-{Guid.NewGuid():N}");
        try
        {
            var first = CliInvoker.Run("generate-docs", "--in", manifest, "--out", outDir1);
            var second = CliInvoker.Run("generate-docs", "--in", manifest, "--out", outDir2);

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);

            string file1 = Path.Combine(outDir1, "homehealthcare", "schema-summary.md");
            string file2 = Path.Combine(outDir2, "homehealthcare", "schema-summary.md");
            Assert.True(File.Exists(file1), $"First-run schema summary missing at {file1}");
            Assert.True(File.Exists(file2), $"Second-run schema summary missing at {file2}");

            byte[] bytes1 = File.ReadAllBytes(file1);
            byte[] bytes2 = File.ReadAllBytes(file2);
            Assert.Equal(bytes1, bytes2);
        }
        finally
        {
            if (Directory.Exists(outDir1)) Directory.Delete(outDir1, recursive: true);
            if (Directory.Exists(outDir2)) Directory.Delete(outDir2, recursive: true);
        }
    }
}
