using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Manifest;

// Per-category completeness invariant for the committed golden
// manifests at samples/GoldenOutput/<fixture>/manifest.json:
//
//   Summary.<X>Count == manifest.<X>.Length == count of *.ir.json
//                                              under ir/<DiskCategory>/
//
// The harvester pipeline silently dropped Tables and Relationships
// (call site in Pipeline.cs.EmitManifestArtifacts never passed them
// to DatabaseManifestAggregator.Build) and silently dropped Modules
// (the category string used to iterate raw/ and to filter parsed
// entries was "Modules" while the on-disk / exporter category is
// "ModulesAndClasses"). Both produced manifests whose Summary counts
// said zero while the per-category ir/ tier was non-empty — exactly
// the failure mode the first downstream consumer hit. This test pins
// the invariant for every category surfaced in the manifest so any
// future regression of the same shape fails here.
public sealed class ManifestCompletenessTests
{
    [Theory]
    [InlineData("HomeHealthCare")]
    [InlineData("NorthwindDevEdition")]
    public void Manifest_per_category_counts_match_summary_and_ir_files(string fixture)
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "samples", "GoldenOutput", fixture);
        string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
        string irRoot = Path.Combine(fixtureRoot, "ir");

        Assert.True(File.Exists(manifestPath),
            $"Committed golden manifest is missing: {manifestPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;
        var summary = root.GetProperty("Summary");

        AssertCategory(root, summary, irRoot, "Forms", "Forms", "FormCount");
        AssertCategory(root, summary, irRoot, "Reports", "Reports", "ReportCount");
        AssertCategory(root, summary, irRoot, "Queries", "Queries", "QueryCount");
        AssertCategory(root, summary, irRoot, "Tables", "Tables", "TableCount");

        // Modules are surfaced at manifest.Modules but the on-disk
        // category is "ModulesAndClasses" (the SaveAsText exporter,
        // translator, and VbaSignatureEnricher all use that name).
        AssertCategory(root, summary, irRoot, "Modules", "ModulesAndClasses", "ModuleCount");

        // Relationships are not summary-tracked (DatabaseSummary
        // intentionally omits a RelationshipCount field), so we only
        // assert array-length vs ir-count parity.
        AssertCategoryNoSummary(root, irRoot, "Relationships", "Relationships");
    }

    private static void AssertCategory(
        JsonElement root,
        JsonElement summary,
        string irRoot,
        string manifestKey,
        string irDirName,
        string summaryKey)
    {
        int manifestCount = root.GetProperty(manifestKey).GetArrayLength();
        int irCount = CountIrFiles(irRoot, irDirName);
        int summaryCount = summary.GetProperty(summaryKey).GetInt32();

        Assert.True(
            manifestCount == summaryCount && summaryCount == irCount,
            $"{manifestKey}: manifest array={manifestCount}, "
                + $"Summary.{summaryKey}={summaryCount}, "
                + $"ir/{irDirName}/*.ir.json={irCount} — all three must match.");
    }

    private static void AssertCategoryNoSummary(
        JsonElement root,
        string irRoot,
        string manifestKey,
        string irDirName)
    {
        int manifestCount = root.GetProperty(manifestKey).GetArrayLength();
        int irCount = CountIrFiles(irRoot, irDirName);

        Assert.True(
            manifestCount == irCount,
            $"{manifestKey}: manifest array={manifestCount}, "
                + $"ir/{irDirName}/*.ir.json={irCount} — must match.");
    }

    private static int CountIrFiles(string irRoot, string irDirName)
    {
        string dir = Path.Combine(irRoot, irDirName);
        if (!Directory.Exists(dir)) return 0;
        return Directory.GetFiles(dir, "*.ir.json").Length;
    }

    private static string FindRepoRoot()
    {
        string dir = System.AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "samples"))
                && Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Path.GetDirectoryName(dir)!;
        }
        return System.AppContext.BaseDirectory;
    }
}
