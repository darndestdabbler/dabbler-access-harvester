using System;
using System.IO;
using System.Linq;
using AccessHarvester.Pipeline.Validation;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Standing regression guard: every docs/test-cases/*.expected.json fixture
// must validate against harvester-schema.v1.0.json after the v0->v1 fixture
// migration (tools/migrate_v0_to_v1.py).
//
// Failure here means either:
//   (a) a fixture was edited and drifted back to v0 camelCase keys, or
//   (b) a schema change broke a field the fixtures rely on.
// Either way it's a real defect — the downstream AI consumer's prompts
// depend on schema shape.
//
// Stop-rule exclusions (documented in docs/schema/v1-adr.md):
//   12-switchboard-navigation.expected.json: excluded because it contains
//   Source: "g_CurrentUserID" on a ParserQueryParameter -- a VBA variable name
//   used where the v1.0 schema requires a Source enum value.  This is a
//   semantic incompatibility that cannot be resolved by mechanical key/value
//   renaming alone; the fixture remains valid as a v0-schema reference.
public sealed class TestCaseFixtureConformanceTests
{
    private static readonly System.Collections.Generic.HashSet<string> StopRuleExclusions = new(
        System.StringComparer.OrdinalIgnoreCase)
    {
        "12-switchboard-navigation.expected.json",
    };
    private static string LocateTestCasesDir()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases");
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate docs/test-cases walking up from '{AppContext.BaseDirectory}'.");
    }

    private static string LocateSchemaPath()
    {
        string testCases = LocateTestCasesDir();
        string schema = Path.Combine(testCases, "harvester-schema.v1.0.json");
        if (!File.Exists(schema))
            throw new FileNotFoundException($"Schema not found: {schema}");
        return schema;
    }

    public static TheoryData<string> ExpectedJsonFixtures
    {
        get
        {
            var data = new TheoryData<string>();
            string dir = LocateTestCasesDir();
            foreach (string path in Directory
                .EnumerateFiles(dir, "*.expected.json", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(path);
                if (!StopRuleExclusions.Contains(fileName))
                    data.Add(fileName);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ExpectedJsonFixtures))]
    public void ExpectedJsonFixture_ValidatesAgainstHarvesterSchemaV1(string fixtureFileName)
    {
        string dir = LocateTestCasesDir();
        string fixturePath = Path.Combine(dir, fixtureFileName);
        Assert.True(File.Exists(fixturePath), $"Fixture missing: {fixturePath}");

        string json = File.ReadAllText(fixturePath, System.Text.Encoding.UTF8);
        string schemaPath = LocateSchemaPath();
        var validator = new SchemaValidator(schemaPath);
        var result = validator.Validate(json);

        Assert.True(
            result.IsValid,
            $"{fixtureFileName} failed v1.0 schema validation:\n  - " +
            string.Join("\n  - ", result.Errors));
    }
}
