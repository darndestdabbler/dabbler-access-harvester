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
// the production pipeline into a temp staging dir, normalizes the
// two volatile artifacts (manifest.json + unresolved-items.json) via
// GoldenNormalizer, then diffs every produced artifact against the
// committed corpus. Drift surfaces the first differing file with the
// surrounding lines so the root cause is obvious without manually
// diffing multi-MB JSON.
//
// SkippableFact-gated for hosts without Microsoft Access. Northwind is
// gitignored per the spec — its test additionally Skips when the local
// .accdb is absent so a fresh clone runs the HomeHealthCare gate alone.
//
// Closes Phase 4 Criteria #3 ("golden output committed for each
// canonical fixture; CI/test suite verifies the harvester reproduces
// that golden output") and locks the work of Sessions 1–3 behind a
// repeatable regression check.
[SupportedOSPlatform("windows")]
public sealed class CanonicalGoldenRegressionTests
{
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
            $"NorthwindDevEdition.accdb is gitignored per spec — drop a local copy " +
            $"into samples/ to exercise this gate.");

        string fixtureStem = Path.GetFileNameWithoutExtension(fixtureFileName);
        string goldenDir = Path.Combine(RepoPaths.Samples(), "GoldenOutput", fixtureStem);
        if (!Directory.Exists(goldenDir))
            throw new DirectoryNotFoundException(
                $"Committed golden corpus missing at {goldenDir}. " +
                $"Re-run the Session-3 builder under tmp/build-golden-corpus to recreate it.");

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
            c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"extract/{c.Category}: {o.ErrorMessage}")));
        Assert.All(runResult.Translate,
            c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"translate/{c.Category}: {o.ErrorMessage}")));
        Assert.All(runResult.Enrich,
            c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"enrich/{c.Category}: {o.ErrorMessage}")));
    }

    private static void CompareNormalizedFile(
        string expected, string actual, Func<string, string> normalize, string relPath)
    {
        if (!File.Exists(expected))
            throw new FileNotFoundException(
                $"Committed golden artifact missing: {expected}.");
        if (!File.Exists(actual))
            Assert.Fail($"Pipeline did not emit expected artifact '{relPath}' " +
                        $"(searched {actual}).");

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

    private static void CompareTreeWithIrNormalization(
        string expectedRoot, string actualRoot, string tier)
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

    private static IEnumerable<string> EnumerateRelative(string root)
    {
        if (!Directory.Exists(root))
            return Array.Empty<string>();
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    private static void AssertFileSetParity(
        IReadOnlyCollection<string> expected,
        IReadOnlyCollection<string> actual,
        string tier)
    {
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);

        var missing = expectedSet.Except(actualSet)
            .OrderBy(p => p, StringComparer.Ordinal).ToList();
        var extra = actualSet.Except(expectedSet)
            .OrderBy(p => p, StringComparer.Ordinal).ToList();

        if (missing.Count == 0 && extra.Count == 0) return;

        var msg = new StringBuilder();
        msg.AppendLine($"Golden corpus file-set drift in {tier}/:");
        if (missing.Count > 0)
        {
            msg.AppendLine("  Missing from pipeline output (present in golden):");
            foreach (var m in missing) msg.AppendLine($"    - {tier}/{m}");
        }
        if (extra.Count > 0)
        {
            msg.AppendLine("  Extra in pipeline output (absent from golden):");
            foreach (var e in extra) msg.AppendLine($"    + {tier}/{e}");
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
        string expectedLF = expected.Replace("\r\n", "\n");
        string actualLF = actual.Replace("\r\n", "\n");
        if (expectedLF == actualLF) return;

        var expLines = expectedLF.Split('\n');
        var actLines = actualLF.Split('\n');
        int diffLine = -1;
        int limit = Math.Min(expLines.Length, actLines.Length);
        for (int i = 0; i < limit; i++)
        {
            if (expLines[i] != actLines[i]) { diffLine = i; break; }
        }
        if (diffLine == -1 && expLines.Length != actLines.Length)
            diffLine = limit;

        var msg = new StringBuilder();
        msg.AppendLine($"Golden drift in {relPath}");
        msg.AppendLine($"  First difference at line {diffLine + 1}.");
        int from = Math.Max(0, diffLine - 2);
        int to = diffLine + 3;
        msg.AppendLine("  expected (golden, file-on-disk):");
        for (int i = from; i < Math.Min(to, expLines.Length); i++)
            msg.AppendLine($"    {(i == diffLine ? '>' : ' ')} {expLines[i]}");
        msg.AppendLine("  actual (pipeline output, post-normalization):");
        for (int i = from; i < Math.Min(to, actLines.Length); i++)
            msg.AppendLine($"    {(i == diffLine ? '>' : ' ')} {actLines[i]}");
        msg.AppendLine();
        msg.AppendLine("  To regenerate the golden corpus (only after confirming the drift");
        msg.AppendLine("  is an intentional change), re-run the Session-3 builder under");
        msg.AppendLine("  tmp/build-golden-corpus/ and commit the result.");
        Assert.Fail(msg.ToString());
    }
}
