using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Access;
using AccessHarvester.Core;
using Xunit;
// Disambiguate the Pipeline class from the AccessHarvester.Pipeline
// namespace now that Core transitively references the Pipeline assembly
// (added in Session 2 of harvester-finish-canonical-golden-output).
using CorePipeline = AccessHarvester.Core.Pipeline;

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
                var categories = new[] { "Queries", "Tables", "Relationships", "Forms", "Reports" }
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
        var pipeline = CorePipeline.Default().WithCategories(categories);
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

        if (expectedRoot.TryGetPropertyValue("Forms", out var expectedForms) && expectedForms is JsonArray forms)
            AssertForms(forms, ReadActualForms(Path.Combine(result.IrDir, "Forms")));

        if (expectedRoot.TryGetPropertyValue("Reports", out var expectedReports) && expectedReports is JsonArray reports)
            AssertReports(reports, ReadActualReports(Path.Combine(result.IrDir, "Reports")));
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

    private static void AssertForms(
        JsonArray expectedForms,
        IReadOnlyDictionary<string, FormMetadata> actualForms)
    {
        Assert.Equal(expectedForms.Count, actualForms.Count);

        foreach (var expectedNode in expectedForms.OfType<JsonObject>())
        {
            var expectedName = ReadRequiredString(expectedNode, "Name");
            Assert.True(actualForms.TryGetValue(expectedName, out var actual), $"Missing form '{expectedName}'.");

            AssertIfFixturePresent(expectedNode, "RecordSource", actual!.RecordSource);
            AssertIfFixturePresent(expectedNode, "Filter", actual.Filter);
            AssertIfFixturePresent(expectedNode, "OrderBy", actual.OrderBy);
            AssertIfFixturePresent(expectedNode, "DefaultView", actual.DefaultView);
            AssertControlsByName(
                expectedName,
                ReadArray(expectedNode, "Children"),
                FlattenNamedControls(actual.Children));
        }
    }

    private static void AssertReports(
        JsonArray expectedReports,
        IReadOnlyDictionary<string, ReportMetadata> actualReports)
    {
        Assert.Equal(expectedReports.Count, actualReports.Count);

        foreach (var expectedNode in expectedReports.OfType<JsonObject>())
        {
            var expectedName = ReadRequiredString(expectedNode, "Name");
            Assert.True(
                actualReports.TryGetValue(expectedName, out var actual),
                $"Missing report '{expectedName}'.");

            AssertIfFixturePresent(expectedNode, "RecordSource", actual!.RecordSource);
            AssertIfFixturePresent(expectedNode, "Filter", actual.Filter);
            AssertIfFixturePresent(expectedNode, "OrderBy", actual.OrderBy);
            AssertIfFixturePresent(expectedNode, "DefaultView", actual.DefaultView);

            AssertGroupLevels(expectedName, ReadArray(expectedNode, "GroupLevels"), actual.GroupLevels);
            AssertControlsByName(
                expectedName,
                ReadArray(expectedNode, "Children"),
                FlattenNamedControls(actual.Children));
        }
    }

    private static void AssertIfFixturePresent(JsonObject expectedNode, string key, string? actualValue)
    {
        if (!expectedNode.TryGetPropertyValue(key, out var node)) return;
        Assert.Equal(node?.GetValue<string>(), actualValue);
    }

    private static void AssertGroupLevels(
        string reportName,
        JsonArray? expectedGroupLevels,
        IReadOnlyList<GroupLevel>? actualGroupLevels)
    {
        if (expectedGroupLevels is null) return;

        Assert.NotNull(actualGroupLevels);
        var byLevel = actualGroupLevels!.ToDictionary(g => g.Level);

        foreach (var expected in expectedGroupLevels.OfType<JsonObject>())
        {
            if (!expected.TryGetPropertyValue("Level", out var levelNode) || levelNode is null)
                throw new InvalidOperationException(
                    $"Expected GroupLevel on '{reportName}' is missing the required 'Level' field.");
            int level = levelNode.GetValue<int>();
            Assert.True(byLevel.TryGetValue(level, out var actual), $"Report '{reportName}' missing group level {level}.");

            if (expected.TryGetPropertyValue("GroupOn", out var groupOnNode) && groupOnNode is not null)
                Assert.Equal(groupOnNode.GetValue<string>(), actual!.GroupOn);
            if (expected.TryGetPropertyValue("HasHeader", out var headerNode) && headerNode is not null)
                Assert.Equal(headerNode.GetValue<bool>(), actual!.HasHeader);
            if (expected.TryGetPropertyValue("HasFooter", out var footerNode) && footerNode is not null)
                Assert.Equal(footerNode.GetValue<bool>(), actual!.HasFooter);
            if (expected.TryGetPropertyValue("GroupingMode", out var groupingModeNode) && groupingModeNode is not null)
                Assert.Equal(groupingModeNode.GetValue<string>(), actual!.GroupingMode);
            if (expected.TryGetPropertyValue("SortOrder", out var sortOrderNode) && sortOrderNode is not null)
                Assert.Equal(sortOrderNode.GetValue<string>(), actual!.SortOrder);
            if (expected.TryGetPropertyValue("KeepTogether", out var keepTogetherNode) && keepTogetherNode is not null)
                Assert.Equal(keepTogetherNode.GetValue<string>(), actual!.KeepTogether);
            if (expected.TryGetPropertyValue("GroupInterval", out var groupIntervalNode) && groupIntervalNode is not null)
                Assert.Equal(groupIntervalNode.GetValue<int>(), actual!.GroupInterval);
        }
    }

    private static void AssertControlsByName(
        string ownerName,
        JsonArray? expectedChildren,
        IReadOnlyDictionary<string, AccessControl> actualByName)
    {
        if (expectedChildren is null) return;

        foreach (var expectedControl in EnumerateNamedControlsRecursive(expectedChildren))
        {
            var controlName = ReadRequiredString(expectedControl, "Name");
            Assert.True(
                actualByName.TryGetValue(controlName, out var actual),
                $"'{ownerName}' missing control '{controlName}'.");

            Assert.Equal(ReadRequiredString(expectedControl, "Type"), actual!.Type);

            if (expectedControl.TryGetPropertyValue("Label", out var labelNode) && labelNode is JsonObject expectedLabel)
            {
                Assert.NotNull(actual.Label);
                Assert.Equal(ReadOptionalString(expectedLabel, "Source"), actual.Label!.Source);
                Assert.Equal(ReadOptionalString(expectedLabel, "Text"), actual.Label.Text);
            }

            if (expectedControl.TryGetPropertyValue("Properties", out var propsNode) && propsNode is JsonObject expectedProps)
                AssertControlPropertiesMatch(ownerName, controlName, expectedProps, actual.Properties);
        }
    }

    private static IEnumerable<JsonObject> EnumerateNamedControlsRecursive(JsonArray? children)
    {
        if (children is null) yield break;
        foreach (var child in children.OfType<JsonObject>())
        {
            if (ReadOptionalString(child, "Name") is not null) yield return child;
            if (child.TryGetPropertyValue("Children", out var grand) && grand is JsonArray grandArr)
                foreach (var deep in EnumerateNamedControlsRecursive(grandArr))
                    yield return deep;
        }
    }

    private static IReadOnlyDictionary<string, AccessControl> FlattenNamedControls(IReadOnlyList<AccessControl>? controls)
    {
        var result = new Dictionary<string, AccessControl>(StringComparer.OrdinalIgnoreCase);
        Walk(controls);
        return result;

        void Walk(IReadOnlyList<AccessControl>? items)
        {
            if (items is null) return;
            foreach (var item in items)
            {
                if (item.Name is not null) result[item.Name] = item;
                Walk(item.Children);
            }
        }
    }

    private static void AssertControlPropertiesMatch(
        string ownerName,
        string controlName,
        JsonObject expectedProps,
        IReadOnlyDictionary<string, JsonElement>? actualProps)
    {
        Assert.NotNull(actualProps);
        foreach (var expected in expectedProps)
        {
            Assert.True(
                actualProps!.TryGetValue(expected.Key, out var actualValue),
                $"'{ownerName}.{controlName}' missing property '{expected.Key}'.");
            string expectedString = expected.Value!.ToJsonString();
            string actualString = actualValue.GetRawText();
            Assert.Equal(expectedString, actualString);
        }
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

    private static IReadOnlyDictionary<string, FormMetadata> ReadActualForms(string categoryDir)
        => ReadNamedActualCategory(categoryDir, FormMetadataExtractor.Extract, f => f.Name);

    private static IReadOnlyDictionary<string, ReportMetadata> ReadActualReports(string categoryDir)
        => ReadNamedActualCategory(categoryDir, ReportMetadataExtractor.Extract, r => r.Name);

    private static IReadOnlyDictionary<string, T> ReadNamedActualCategory<T>(
        string categoryDir,
        Func<string, JsonElement, T> extract,
        Func<T, string> getName)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(categoryDir))
            return result;

        foreach (var file in Directory.GetFiles(categoryDir, "*.ir.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.EndsWith(".ir", StringComparison.OrdinalIgnoreCase))
                name = name[..^3];
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var metadata = extract(name, doc.RootElement);
            result[getName(metadata)] = metadata;
        }

        return result;
    }

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
