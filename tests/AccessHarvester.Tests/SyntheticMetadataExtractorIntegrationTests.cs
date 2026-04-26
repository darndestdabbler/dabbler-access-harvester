using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Access;
using AccessHarvester.Core;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Session 5 integration tests that run the query/table/relationship metadata
/// extractors against the committed database-level synthetic fixtures.
/// </summary>
public class SyntheticMetadataExtractorIntegrationTests
{
    private static readonly string SyntheticDbDir = Path.Combine(AppContext.BaseDirectory, "SyntheticDbs");

    [SkippableFact]
    public void SyntheticFixtures_QueryTableRelationshipMetadataMatchesExpectedContracts()
    {
        SkipIfNoAccess();

        var expectedFiles = Directory.GetFiles(SyntheticDbDir, "*.expected.json")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(expectedFiles);

        var outRoot = Path.Combine(Path.GetTempPath(), $"ah-metadata-test-{Guid.NewGuid():N}");
        try
        {
            var validatedFixtureCount = 0;
            foreach (var expectedPath in expectedFiles)
            {
                var expectedRoot = JsonNode.Parse(File.ReadAllText(expectedPath))!.AsObject();
                var categories = new[] { "Queries", "Tables", "Relationships" }
                    .Where(category => expectedRoot.TryGetPropertyValue(category, out var node)
                        && node is JsonArray array
                        && array.Count > 0)
                    .ToArray();

                if (categories.Length == 0) continue;

                var dbPath = Path.ChangeExtension(expectedPath, ".accdb");
                Skip.IfNot(File.Exists(dbPath), $"Synthetic DB not present at {dbPath}.");

                ValidateFixturePair(dbPath, expectedRoot, categories, outRoot);
                validatedFixtureCount++;
            }

            Assert.True(validatedFixtureCount > 0, "Expected at least one fixture with query/table/relationship metadata.");
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }

    private static void ValidateFixturePair(
        string dbPath,
        JsonObject expectedRoot,
        IReadOnlyCollection<string> categories,
        string outRoot)
    {
        var pipeline = Pipeline.Default().WithCategories(categories);
        var result = pipeline.Run(dbPath, outRoot);

        Assert.All(result.Extract, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
        Assert.All(result.Translate, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
        Assert.All(result.Enrich, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

        if (expectedRoot.TryGetPropertyValue("Queries", out var expectedQueries) && expectedQueries is JsonArray queries)
            AssertQueries(queries, ReadActualQueries(Path.Combine(result.IrDir, "Queries")));

        if (expectedRoot.TryGetPropertyValue("Tables", out var expectedTables) && expectedTables is JsonArray tables)
            AssertTables(tables, ReadActualTables(Path.Combine(result.IrDir, "Tables")));

        if (expectedRoot.TryGetPropertyValue("Relationships", out var expectedRelationships)
            && expectedRelationships is JsonArray relationships)
            AssertRelationships(relationships, ReadActualRelationships(Path.Combine(result.IrDir, "Relationships")));
    }

    private static void AssertQueries(JsonArray expectedQueries, IReadOnlyDictionary<string, QueryMetadata> actualQueries)
    {
        Assert.Equal(expectedQueries.Count, actualQueries.Count);

        foreach (var expectedNode in expectedQueries.OfType<JsonObject>())
        {
            var expectedName = ReadRequiredString(expectedNode, "Name");
            Assert.True(actualQueries.TryGetValue(expectedName, out var actual), $"Missing query '{expectedName}'.");

            Assert.Equal(ReadOptionalString(expectedNode, "QueryType"), actual!.QueryType);
            Assert.Equal(ReadOptionalString(expectedNode, "Sql"), actual.Sql);
        }
    }

    private static void AssertTables(JsonArray expectedTables, IReadOnlyDictionary<string, TableMetadata> actualTables)
    {
        Assert.Equal(expectedTables.Count, actualTables.Count);

        foreach (var expectedNode in expectedTables.OfType<JsonObject>())
        {
            var expectedName = ReadRequiredString(expectedNode, "Name");
            Assert.True(actualTables.TryGetValue(expectedName, out var actual), $"Missing table '{expectedName}'.");

            AssertExpectedFields(expectedName, ReadArray(expectedNode, "Fields"), actual!.Fields);
            AssertExpectedPrimaryKey(ReadArray(expectedNode, "PrimaryKey"), actual.PrimaryKey);
        }
    }

    private static void AssertRelationships(
        JsonArray expectedRelationships,
        IReadOnlyDictionary<string, RelationshipMetadata> actualRelationships)
    {
        Assert.Equal(expectedRelationships.Count, actualRelationships.Count);

        foreach (var expectedNode in expectedRelationships.OfType<JsonObject>())
        {
            var expectedName = ReadRequiredString(expectedNode, "Name");
            Assert.True(
                actualRelationships.TryGetValue(expectedName, out var actual),
                $"Missing relationship '{expectedName}'.");

            Assert.Equal(ReadOptionalString(expectedNode, "ParentTable"), actual!.ParentTable);
            Assert.Equal(ReadOptionalString(expectedNode, "ChildTable"), actual.ChildTable);
            AssertExpectedRelationshipFields(ReadArray(expectedNode, "Fields"), actual.Fields);
        }
    }

    private static void AssertExpectedFields(
        string tableName,
        JsonArray? expectedFields,
        IReadOnlyList<TableField>? actualFields)
    {
        if (expectedFields is null) return;

        Assert.NotNull(actualFields);
        Assert.Equal(expectedFields.Count, actualFields!.Count);

        var actualByName = actualFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var expectedField in expectedFields.OfType<JsonObject>())
        {
            var expectedFieldName = ReadRequiredString(expectedField, "Name");
            Assert.True(
                actualByName.TryGetValue(expectedFieldName, out var actualField),
                $"Missing field '{tableName}.{expectedFieldName}'.");

            Assert.Equal(ReadOptionalString(expectedField, "DataType"), actualField!.DataType);
            if (expectedField.TryGetPropertyValue("Size", out var expectedSize))
                Assert.Equal(expectedSize!.GetValue<int>(), actualField.Size);
        }
    }

    private static void AssertExpectedPrimaryKey(JsonArray? expectedPrimaryKey, IReadOnlyList<string>? actualPrimaryKey)
    {
        if (expectedPrimaryKey is null) return;

        Assert.NotNull(actualPrimaryKey);
        Assert.Equal(
            expectedPrimaryKey.Select(node => node!.GetValue<string>()).ToArray(),
            actualPrimaryKey!.ToArray());
    }

    private static void AssertExpectedRelationshipFields(
        JsonArray? expectedFields,
        IReadOnlyList<RelationshipFieldPair>? actualFields)
    {
        if (expectedFields is null) return;

        Assert.NotNull(actualFields);
        Assert.Equal(expectedFields.Count, actualFields!.Count);

        foreach (var expectedField in expectedFields.OfType<JsonObject>())
        {
            var expectedParent = ReadRequiredString(expectedField, "ParentField");
            var expectedChild = ReadRequiredString(expectedField, "ChildField");
            Assert.Contains(
                actualFields,
                actual => actual.ParentField.Equals(expectedParent, StringComparison.OrdinalIgnoreCase)
                    && actual.ChildField.Equals(expectedChild, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IReadOnlyDictionary<string, QueryMetadata> ReadActualQueries(string categoryDir)
        => ReadActualCategory(categoryDir, QueryMetadataExtractor.Extract, q => q.Name);

    private static IReadOnlyDictionary<string, TableMetadata> ReadActualTables(string categoryDir)
        => ReadActualCategory(categoryDir, TableMetadataExtractor.Extract, t => t.Name);

    private static IReadOnlyDictionary<string, RelationshipMetadata> ReadActualRelationships(string categoryDir)
        => ReadActualCategory(categoryDir, RelationshipMetadataExtractor.Extract, r => r.Name);

    private static IReadOnlyDictionary<string, T> ReadActualCategory<T>(
        string categoryDir,
        Func<JsonElement, T> extract,
        Func<T, string> getName)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(categoryDir))
            return result;

        foreach (var file in Directory.GetFiles(categoryDir, "*.ir.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var metadata = extract(doc.RootElement);
            result[getName(metadata)] = metadata;
        }

        return result;
    }

    private static JsonArray? ReadArray(JsonObject obj, string propertyName)
        => obj.TryGetPropertyValue(propertyName, out var node) && node is JsonArray array ? array : null;

    private static string ReadRequiredString(JsonObject obj, string propertyName)
        => ReadOptionalString(obj, propertyName)
            ?? throw new InvalidOperationException($"Expected property '{propertyName}' to be present.");

    private static string? ReadOptionalString(JsonObject obj, string propertyName)
        => obj.TryGetPropertyValue(propertyName, out var node) && node is not null
            ? node.GetValue<string>()
            : null;

    private static void SkipIfNoAccess()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Access COM is Windows-only.");
        Skip.If(Type.GetTypeFromProgID("Access.Application") is null,
            "Microsoft Access is not installed (Access.Application ProgID not registered).");
    }
}
