using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Pipeline;

// Mechanical regenerator for the committed reports-baseline golden corpus
// (samples/GoldenOutput/<fixture>/reports-baseline/). This is a DEV TOOL, not
// an assertion test: it is hard-guarded behind the AH_REGEN_GOLDEN env var and
// Skip.IfNot, so a normal `dotnet test` run never touches the repo. Run it only
// when an intentional, reviewed change to the report-baseline output shape
// lands (e.g. the DisplayTotals field + updated ContractNote added by the
// harvester-report-displayed-total-parity session set) and the committed corpus
// needs to be refreshed.
//
// Why this exists / why it lives here: the committed golden files carry the
// DEFAULT-encoder JSON escaping (<, ') produced by GoldenNormalizer's
// re-serialization, NOT the UnsafeRelaxedJsonEscaping the CLI writer emits. The
// only reliable way to produce byte-identical golden is to run the SAME
// GoldenNormalizer the regression gate runs. This helper captures via the exact
// same CLI path CanonicalGoldenRegressionTests uses, normalizes with the exact
// same dispatch, and writes the result into the golden dir — so a regen
// followed by the gate is guaranteed consistent.
//
// Usage (PowerShell, from the worktree root, Windows + Access required):
//   $env:AH_REGEN_GOLDEN = "1"
//   dotnet test tests/AccessHarvester.Integration `
//     --filter "FullyQualifiedName~GoldenCorpusRegenerator"
//   Remove-Item Env:\AH_REGEN_GOLDEN
//
// Serialized with the other HomeHealthCare COM tests (it drives the
// report-baseline CLI against the same fixtures).
[Collection("HomeHealthCareCom")]
[SupportedOSPlatform("windows")]
public sealed class GoldenCorpusRegenerator
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CanonicalReportBaselineReports =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["HomeHealthCare.accdb"] = new[]
            {
                "srptClientBudgets",
                "srptClientCurrentNeeds",
            },
            ["NorthwindDevEdition.accdb"] = new[]
            {
                "rptInvoice",
                "rptSalesByEmployee",
                "rptSalesByProduct",
            },
        };

    [SkippableFact]
    public void RegenerateCommittedReportBaselineCorpus()
    {
        Skip.IfNot(
            string.Equals(Environment.GetEnvironmentVariable("AH_REGEN_GOLDEN"), "1", StringComparison.Ordinal),
            "Golden-corpus regeneration is a guarded dev tool. Set AH_REGEN_GOLDEN=1 to run it.");
        AccessAvailability.SkipIfNoAccess();

        var regenerated = new List<string>();
        foreach (var (fixtureFileName, reportNames) in CanonicalReportBaselineReports)
        {
            string fixturePath = Path.Combine(RepoPaths.Samples(), fixtureFileName);
            if (!File.Exists(fixturePath))
            {
                // NorthwindDevEdition.accdb is gitignored; skip it on hosts
                // without a local copy rather than fail the regen.
                continue;
            }

            string fixtureStem = Path.GetFileNameWithoutExtension(fixtureFileName);
            string goldenReportsBaseline = Path.Combine(
                RepoPaths.Samples(), "GoldenOutput", fixtureStem, "reports-baseline");

            string stagingRoot = Path.Combine(
                Path.GetTempPath(), $"ah-golden-regen-{fixtureStem}-{Guid.NewGuid():N}");
            try
            {
                var cliResult = CliInvoker.RunWithTimeout(
                    600_000,
                    "report-baseline",
                    "--db", fixturePath,
                    "--out", stagingRoot,
                    "--reports", string.Join(",", reportNames),
                    "--json-status");

                if (cliResult.ExitCode != 0)
                {
                    Assert.Fail(
                        $"report-baseline capture failed for {fixtureFileName} (exit {cliResult.ExitCode}).\n" +
                        $"stderr:\n{cliResult.StdErr}\nstdout:\n{cliResult.StdOut}");
                }

                string capturedRoot = Path.Combine(
                    stagingRoot, "AccessMetaData", fixtureStem, "reports-baseline");

                NormalizeCapturedTreeIntoGolden(capturedRoot, goldenReportsBaseline);
                regenerated.Add(fixtureStem);
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                    Directory.Delete(stagingRoot, recursive: true);
            }
        }

        Assert.NotEmpty(regenerated);
    }

    // Rewrite the golden reports-baseline subtree from the freshly captured
    // staging tree, applying the SAME normalization the regression gate applies
    // to the regenerated output before comparison. The golden subtree is
    // cleared first so a report removed from the canonical subset cannot leave
    // an orphan behind.
    private static void NormalizeCapturedTreeIntoGolden(string capturedRoot, string goldenRoot)
    {
        if (!Directory.Exists(capturedRoot))
            throw new DirectoryNotFoundException($"Captured reports-baseline tree missing: {capturedRoot}.");

        if (Directory.Exists(goldenRoot))
            Directory.Delete(goldenRoot, recursive: true);
        Directory.CreateDirectory(goldenRoot);

        foreach (var capturedPath in Directory.EnumerateFiles(capturedRoot, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(capturedRoot, capturedPath).Replace('\\', '/');
            string normalized = NormalizeReportBaselineArtifact(rel, File.ReadAllText(capturedPath));

            string goldenPath = Path.Combine(goldenRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, normalized);
        }
    }

    // Mirror of CanonicalGoldenRegressionTests.NormalizeReportBaselineArtifact
    // so the committed golden is produced by the exact normalization the gate
    // applies to regenerated output.
    private static string NormalizeReportBaselineArtifact(string relPath, string json)
    {
        if (string.Equals(relPath, "baseline-manifest.json", StringComparison.Ordinal))
            return GoldenNormalizer.NormalizeReportBaselineManifest(json);

        if (relPath.EndsWith("/aggregates.json", StringComparison.Ordinal) ||
            string.Equals(relPath, "aggregates.json", StringComparison.Ordinal))
        {
            return GoldenNormalizer.NormalizeReportAggregates(json);
        }

        return json;
    }
}
