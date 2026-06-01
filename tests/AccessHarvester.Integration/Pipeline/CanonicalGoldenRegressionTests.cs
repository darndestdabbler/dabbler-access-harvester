using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using AccessHarvester.Core;
using AccessHarvester.Integration.TestSupport;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Integration.Pipeline;

// Regression gate for the committed canonical golden corpus
// (samples/GoldenOutput/<fixture>/). For each canonical fixture, runs
// the production pipeline into a temp staging dir, captures the
// canonical reports-baseline slice via the public CLI, normalizes the
// known volatile artifacts via GoldenNormalizer, then diffs every
// produced artifact against the committed corpus. Drift surfaces the
// first differing file with the surrounding lines so the root cause is
// obvious without manually diffing multi-MB JSON.
//
// SkippableFact-gated for hosts without Microsoft Access. Northwind is
// gitignored per the spec: its test additionally skips when the local
// .accdb is absent so a fresh clone runs the HomeHealthCare gate alone.
//
// Closes Phase 4 Criteria #3 ("golden output committed for each
// canonical fixture; CI/test suite verifies the harvester reproduces
// that golden output") and extends the same regression discipline to the
// committed reports-baseline corpus.
// Serialized with the other HomeHealthCare COM tests: this class drives
// COM against samples/HomeHealthCare.accdb both in-process (CorePipeline)
// and via the report-baseline CLI. Without the shared collection it races
// the Cli command tests on ACE OLE DB's file lock and starves them into
// per-test timeouts (project-guidance.md -> Testing Conventions).
[Collection("HomeHealthCareCom")]
[SupportedOSPlatform("windows")]
public sealed class CanonicalGoldenRegressionTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CanonicalReportBaselineReports = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
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
    public void HomeHealthCare_ReproducesGoldenOutput()
    {
        AssertGoldenCorpusReproduced("HomeHealthCare.accdb");
    }

    [SkippableFact]
    public void NorthwindDevEdition_ReproducesGoldenOutput()
    {
        AssertGoldenCorpusReproduced("NorthwindDevEdition.accdb");
    }

    private static void AssertGoldenCorpusReproduced(string fixtureFileName)
    {
        AccessAvailability.SkipIfNoAccess();

        string fixturePath = Path.Combine(RepoPaths.Samples(), fixtureFileName);
        Skip.IfNot(
            File.Exists(fixturePath),
            $"Canonical fixture not present at {fixturePath}. " +
            $"NorthwindDevEdition.accdb is gitignored per spec: drop a local copy " +
            $"into samples/ to exercise this gate.");

        string fixtureStem = Path.GetFileNameWithoutExtension(fixtureFileName);
        string goldenDir = Path.Combine(RepoPaths.Samples(), "GoldenOutput", fixtureStem);
        if (!Directory.Exists(goldenDir))
        {
            throw new DirectoryNotFoundException(
                $"Committed golden corpus missing at {goldenDir}. " +
                "Recreate the canonical fixture corpus and commit it.");
        }

        string stagingRoot = Path.Combine(
            Path.GetTempPath(), $"ah-golden-regress-{fixtureStem}-{Guid.NewGuid():N}");
        try
        {
            var runResult = CorePipeline.Default().Run(fixturePath, stagingRoot);

            AssertAllOutcomesSucceeded(runResult);

            CompareNormalizedFile(
                expected: Path.Combine(goldenDir, "manifest.json"),
                actual: Path.Combine(runResult.BaseDir, "manifest.json"),
                normalize: GoldenNormalizer.NormalizeManifest,
                relPath: "manifest.json");

            CompareNormalizedFile(
                expected: Path.Combine(goldenDir, "unresolved-items.json"),
                actual: Path.Combine(runResult.BaseDir, "unresolved-items.json"),
                normalize: GoldenNormalizer.NormalizeUnresolvedItems,
                relPath: "unresolved-items.json");

            CompareTreeVerbatim(
                expectedRoot: Path.Combine(goldenDir, "kb"),
                actualRoot: runResult.KbDir,
                tier: "kb");

            CompareTreeWithIrNormalization(
                expectedRoot: Path.Combine(goldenDir, "ir"),
                actualRoot: runResult.IrDir,
                tier: "ir");

            CompareReportBaseline(
                fixtureFileName: fixtureFileName,
                fixturePath: fixturePath,
                goldenDir: goldenDir,
                stagingRoot: stagingRoot);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private static void AssertAllOutcomesSucceeded(RunResult runResult)
    {
        Assert.All(runResult.Extract,
            category => Assert.All(category.Outcomes,
                outcome => Assert.True(outcome.Success, $"extract/{category.Category}: {outcome.ErrorMessage}")));
        Assert.All(runResult.Translate,
            category => Assert.All(category.Outcomes,
                outcome => Assert.True(outcome.Success, $"translate/{category.Category}: {outcome.ErrorMessage}")));
        Assert.All(runResult.Enrich,
            category => Assert.All(category.Outcomes,
                outcome => Assert.True(outcome.Success, $"enrich/{category.Category}: {outcome.ErrorMessage}")));
    }

    private static void CompareNormalizedFile(
        string expected,
        string actual,
        Func<string, string> normalize,
        string relPath)
    {
        if (!File.Exists(expected))
            throw new FileNotFoundException($"Committed golden artifact missing: {expected}.");
        if (!File.Exists(actual))
            Assert.Fail($"Pipeline did not emit expected artifact '{relPath}' (searched {actual}).");

        string expectedJson = File.ReadAllText(expected);
        string actualNormalized = normalize(File.ReadAllText(actual));

        AssertEqualWithContext(expectedJson, actualNormalized, relPath);
    }

    private static void CompareTreeVerbatim(string expectedRoot, string actualRoot, string tier)
    {
        var expectedFiles = EnumerateRelative(expectedRoot).ToList();
        var actualFiles = EnumerateRelative(actualRoot).ToList();

        AssertFileSetParity(expectedFiles, actualFiles, tier);

        foreach (var rel in expectedFiles)
        {
            string expectedText = File.ReadAllText(Path.Combine(expectedRoot, rel));
            string actualText = File.ReadAllText(Path.Combine(actualRoot, rel));
            AssertEqualWithContext(expectedText, actualText, $"{tier}/{rel}");
        }
    }

    private static void CompareTreeWithIrNormalization(string expectedRoot, string actualRoot, string tier)
    {
        var expectedFiles = EnumerateRelative(expectedRoot).ToList();
        var actualFiles = EnumerateRelative(actualRoot).ToList();

        AssertFileSetParity(expectedFiles, actualFiles, tier);

        foreach (var rel in expectedFiles)
        {
            string expectedText = File.ReadAllText(Path.Combine(expectedRoot, rel));
            string actualText = GoldenNormalizer.NormalizePerObjectIr(
                File.ReadAllText(Path.Combine(actualRoot, rel)));
            AssertEqualWithContext(expectedText, actualText, $"{tier}/{rel}");
        }
    }

    private static void CompareReportBaseline(
        string fixtureFileName,
        string fixturePath,
        string goldenDir,
        string stagingRoot)
    {
        if (!CanonicalReportBaselineReports.TryGetValue(fixtureFileName, out var reportNames))
        {
            throw new InvalidOperationException(
                $"No canonical report-baseline subset declared for fixture '{fixtureFileName}'.");
        }

        var cliResult = CliInvoker.RunWithTimeout(
            600_000,
            "report-baseline",
            "--db", fixturePath,
            "--out", stagingRoot,
            "--reports", string.Join(",", reportNames),
            "--json-status");

        if (cliResult.ExitCode != 0)
        {
            var msg = new StringBuilder();
            msg.AppendLine($"report-baseline failed for {fixtureFileName}.");
            msg.AppendLine($"  exit: {cliResult.ExitCode}");
            if (!string.IsNullOrWhiteSpace(cliResult.StdErr))
            {
                msg.AppendLine("  stderr:");
                msg.AppendLine(cliResult.StdErr);
            }
            if (!string.IsNullOrWhiteSpace(cliResult.StdOut))
            {
                msg.AppendLine("  stdout:");
                msg.AppendLine(cliResult.StdOut);
            }

            Assert.Fail(msg.ToString());
        }

        string fixtureStem = Path.GetFileNameWithoutExtension(fixtureFileName);
        CompareReportBaselineTree(
            expectedRoot: Path.Combine(goldenDir, "reports-baseline"),
            actualRoot: Path.Combine(stagingRoot, "AccessMetaData", fixtureStem, "reports-baseline"));
    }

    private static void CompareReportBaselineTree(string expectedRoot, string actualRoot)
    {
        var expectedFiles = EnumerateRelative(expectedRoot).ToList();
        var actualFiles = EnumerateRelative(actualRoot).ToList();

        AssertFileSetParity(expectedFiles, actualFiles, "reports-baseline");

        foreach (var rel in expectedFiles)
        {
            string expectedText = File.ReadAllText(Path.Combine(expectedRoot, rel));
            string actualText = NormalizeReportBaselineArtifact(
                rel,
                File.ReadAllText(Path.Combine(actualRoot, rel)));
            AssertEqualWithContext(expectedText, actualText, $"reports-baseline/{rel}");
        }
    }

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

    private static IEnumerable<string> EnumerateRelative(string root)
    {
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static void AssertFileSetParity(
        IReadOnlyCollection<string> expected,
        IReadOnlyCollection<string> actual,
        string tier)
    {
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);

        var missing = expectedSet.Except(actualSet)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var extra = actualSet.Except(expectedSet)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (missing.Count == 0 && extra.Count == 0)
            return;

        var msg = new StringBuilder();
        msg.AppendLine($"Golden corpus file-set drift in {tier}/:");
        if (missing.Count > 0)
        {
            msg.AppendLine("  Missing from pipeline output (present in golden):");
            foreach (var path in missing)
                msg.AppendLine($"    - {tier}/{path}");
        }
        if (extra.Count > 0)
        {
            msg.AppendLine("  Extra in pipeline output (absent from golden):");
            foreach (var path in extra)
                msg.AppendLine($"    + {tier}/{path}");
        }

        Assert.Fail(msg.ToString());
    }

    // Normalize CRLF/LF before comparison: git's autocrlf setting can flip
    // line endings on checkout, but the pipeline writes them with the
    // current OS's convention. Comparing on logical lines makes the gate
    // robust against that purely-storage detail without weakening the
    // field-level byte-equality contract.
    private static void AssertEqualWithContext(string expected, string actual, string relPath)
    {
        string expectedLf = expected.Replace("\r\n", "\n");
        string actualLf = actual.Replace("\r\n", "\n");
        if (expectedLf == actualLf)
            return;

        var expectedLines = expectedLf.Split('\n');
        var actualLines = actualLf.Split('\n');
        int diffLine = -1;
        int limit = Math.Min(expectedLines.Length, actualLines.Length);
        for (int i = 0; i < limit; i++)
        {
            if (expectedLines[i] != actualLines[i])
            {
                diffLine = i;
                break;
            }
        }

        if (diffLine == -1 && expectedLines.Length != actualLines.Length)
            diffLine = limit;

        var msg = new StringBuilder();
        msg.AppendLine($"Golden drift in {relPath}");
        msg.AppendLine($"  First difference at line {diffLine + 1}.");
        int from = Math.Max(0, diffLine - 2);
        int to = diffLine + 3;
        msg.AppendLine("  expected (golden, file-on-disk):");
        for (int i = from; i < Math.Min(to, expectedLines.Length); i++)
            msg.AppendLine($"    {(i == diffLine ? '>' : ' ')} {expectedLines[i]}");
        msg.AppendLine("  actual (pipeline output, post-normalization):");
        for (int i = from; i < Math.Min(to, actualLines.Length); i++)
            msg.AppendLine($"    {(i == diffLine ? '>' : ' ')} {actualLines[i]}");
        msg.AppendLine();
        msg.AppendLine("  To refresh the golden corpus (only after confirming the drift");
        msg.AppendLine("  is intentional), regenerate the canonical fixture output and");
        msg.AppendLine("  commit the updated files.");
        Assert.Fail(msg.ToString());
    }
}
