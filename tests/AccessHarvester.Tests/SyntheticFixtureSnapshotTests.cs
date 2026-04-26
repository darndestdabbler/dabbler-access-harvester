using System.Text.Json.Nodes;
using AccessHarvester.Core;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Session 4 integration tests that validate the seven Session 3 synthetic DB
/// fixtures against their committed <c>*.expected.json</c> snapshots.
/// </summary>
public class SyntheticFixtureSnapshotTests
{
    private static readonly string ProjectRoot = ResolveProjectRoot();
    private static readonly string SyntheticDbDir = Path.Combine(ProjectRoot, "SyntheticDbs");

    [SkippableFact]
    public void SyntheticFixtures_MatchCommittedExpectedSnapshots()
    {
        SkipIfNoAccess();

        var snapshotFiles = Directory.GetFiles(SyntheticDbDir, "*.expected.json")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(snapshotFiles);

        var outRoot = Path.Combine(Path.GetTempPath(), $"ae-test-{Guid.NewGuid():N}");
        try
        {
            foreach (var expectedPath in snapshotFiles)
            {
                var dbPath = Path.ChangeExtension(expectedPath, ".accdb");
                Skip.IfNot(File.Exists(dbPath), $"Synthetic DB not present at {dbPath}.");

                ValidateFixturePair(dbPath, expectedPath, outRoot);
            }
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }

    private static void ValidateFixturePair(string dbPath, string expectedPath, string outRoot)
    {
        var expectedRoot = JsonNode.Parse(File.ReadAllText(expectedPath))!.AsObject();
        var expectedCategories = expectedRoot.Select(p => p.Key).ToArray();

        var pipeline = Pipeline.Default().WithCategories(expectedCategories);
        var result = pipeline.Run(dbPath, outRoot);

        Assert.All(result.Extract, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
        Assert.All(result.Translate, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
        Assert.All(result.Enrich, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

        foreach (var category in expectedCategories)
        {
            var expectedItems = ReadCategoryObjects(expectedRoot, category);
            var actualItems = ReadActualCategoryObjects(Path.Combine(result.IrDir, category));

            Assert.Equal(expectedItems.Count, actualItems.Count);

            Assert.Equal(
                expectedItems.Keys.Order(StringComparer.OrdinalIgnoreCase),
                actualItems.Keys.Order(StringComparer.OrdinalIgnoreCase));

            foreach (var name in expectedItems.Keys)
            {
                var expectedItem = expectedItems[name];
                var actualItem = actualItems[name];

                Assert.True(
                    JsonNode.DeepEquals(Normalize(expectedItem), Normalize(actualItem)),
                    $"Snapshot mismatch for {Path.GetFileName(expectedPath)} category '{category}' item '{name}'.");
            }
        }
    }

    private static Dictionary<string, JsonObject> ReadCategoryObjects(JsonObject snapshot, string category)
    {
        if (!snapshot.TryGetPropertyValue(category, out var node) || node is not JsonArray array)
        {
            return new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in array)
        {
            if (item is not JsonObject obj) continue;
            var name = obj["Name"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(name), $"Expected '{category}' snapshot item to have a Name.");
            result[name!] = obj;
        }

        return result;
    }

    private static Dictionary<string, JsonObject> ReadActualCategoryObjects(string categoryDir)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(categoryDir))
            return result;

        foreach (var file in Directory.GetFiles(categoryDir, "*.ir.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            var node = JsonNode.Parse(File.ReadAllText(file))?.AsObject();
            Assert.NotNull(node);
            var name = node!["Name"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(name), $"Expected '{Path.GetFileName(file)}' to have a Name.");
            result[name!] = node;
        }

        return result;
    }

    private static JsonNode Normalize(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var normalized = new JsonObject();
                foreach (var prop in obj.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                    normalized[prop.Key] = prop.Value is null ? null : Normalize(prop.Value);
                return normalized;
            }
            case JsonArray array:
            {
                var normalized = new JsonArray();
                foreach (var item in array)
                    normalized.Add(item is null ? null : Normalize(item));
                return normalized;
            }
            default:
                return node.DeepClone();
        }
    }

    private static void SkipIfNoAccess()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Access COM is Windows-only.");
        Skip.If(Type.GetTypeFromProgID("Access.Application") is null,
            "Microsoft Access is not installed (Access.Application ProgID not registered).");
    }

    private static string ResolveProjectRoot()
    {
        return AppContext.BaseDirectory;
    }
}
