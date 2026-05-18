using System.IO;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using AccessHarvester.Pipeline.Validation;
using Xunit;

namespace AccessHarvester.Integration.Pipeline;

// Hermetic schema-conformance gate. Validates every committed example
// MANIFEST under examples/ against harvester-schema.v1.0.json.
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
// silently bypassing the gate. examples/ also contains per-form IR
// records (verbatim copies of HHC/ir/Forms/*.ir.json) intended as
// documentation examples for the interpretation guide; those are not
// manifest-shaped and are excluded from this gate via the
// SchemaType == "DatabaseManifest" discriminator. The per-form IR
// records' typed contract is documented in
// docs/ir-schema-forms-reports.md.
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
                if (IsDatabaseManifest(path))
                    data.Add(Path.GetFileName(path));
            }
            return data;
        }
    }

    private static bool IsDatabaseManifest(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("SchemaType", out var st)
                && st.ValueKind == JsonValueKind.String
                && st.GetString() == "DatabaseManifest";
        }
        catch (JsonException)
        {
            // Non-JSON or malformed content is "not a manifest" — discriminate
            // silently. IOException, UnauthorizedAccessException, etc. propagate
            // so a corrupted or unreadable committed fixture surfaces as a real
            // test failure rather than a silent omission from the gate.
            return false;
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
