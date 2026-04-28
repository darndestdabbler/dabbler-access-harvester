using System.IO;
using System.Linq;
using AccessHarvester.Integration.TestSupport;
using AccessHarvester.Pipeline.Validation;
using Xunit;

namespace AccessHarvester.Integration.Pipeline;

// Hermetic schema-conformance gate. Validates EVERY committed example
// manifest under examples/ against harvester-schema.v1.0.json.
//
// Failure mode this guards against: schema and committed-example fixtures
// drift apart silently. The Pipeline.Tests project's SchemaValidatorTests
// exercises the validator's positive/negative behaviour with synthetic
// payloads — this class instead asserts the contract holds against the
// fixtures downstream consumers (SchemaDocGenerator, the AI consumer)
// actually read.
//
// Fixtures are discovered dynamically from the examples/ directory so
// newly committed manifests are picked up automatically rather than
// silently bypassing the gate.
public sealed class SchemaConformanceTests
{
    public static TheoryData<string> ExampleFixtures
    {
        get
        {
            var data = new TheoryData<string>();
            string examplesDir = RepoPaths.Examples();
            foreach (string path in Directory
                .EnumerateFiles(examplesDir, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase))
            {
                data.Add(Path.GetFileName(path));
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ExampleFixtures))]
    public void Example_ValidatesAgainstHarvesterSchemaV1(string fixtureFileName)
    {
        string fixturePath = Path.Combine(RepoPaths.Examples(), fixtureFileName);
        Assert.True(File.Exists(fixturePath), $"Example missing: {fixturePath}");
        string schemaPath = RepoPaths.SchemaFile();
        Assert.True(File.Exists(schemaPath), $"Schema missing: {schemaPath}");

        var validator = new SchemaValidator(schemaPath);
        var result = validator.Validate(File.ReadAllText(fixturePath));

        Assert.True(
            result.IsValid,
            $"{fixtureFileName} failed schema validation:\n  - " +
            string.Join("\n  - ", result.Errors));
    }
}
