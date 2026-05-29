using System.IO;
using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

public class ClassificationReaderTests
{
    [Fact]
    public void Reads_fixture_round_trip()
    {
        // Use the project's sample fixture as a known-good v1.0 document.
        string fixturePath = Path.Combine(RepoRoot(), "samples", "classification-fixture.json");
        Skip.IfNot(File.Exists(fixturePath), $"Fixture not present at {fixturePath}.");

        var doc = ClassificationReader.ReadFile(fixturePath);

        Assert.Equal("1.0", doc.Version);
        Assert.Equal("HomeHealthCare.accdb", doc.Database);
        Assert.NotEmpty(doc.Tables);
        var person = doc.Tables[0];
        Assert.Equal("Person", person.Name);
        var personId = person.Columns[0];
        Assert.Equal("PersonID", personId.Name);
        Assert.Equal(ColumnCategory.SurrogateAutonumberPk, personId.GuessedCategory);
        // Fixture sets preserveAutonumber=true on PersonID.
        Assert.True(personId.HumanOverride.PreserveAutonumber);
    }

    [Fact]
    public void Wrong_version_rejected()
    {
        string json = """
            {
              "version": "2.0",
              "database": "T.accdb",
              "scannedUtc": "2026-05-28T12:00:00Z",
              "config": { "systemColumnPatterns": [], "piiNamePatterns": [],
                          "piiValuePatterns": [], "valueSampleSize": 0 },
              "tables": []
            }
            """;
        Assert.Throws<InvalidDataException>(() => ClassificationReader.Parse(json));
    }

    [Fact]
    public void Missing_required_field_rejected()
    {
        // Missing 'database'.
        string json = """
            {
              "version": "1.0",
              "scannedUtc": "2026-05-28T12:00:00Z",
              "config": { "systemColumnPatterns": [], "piiNamePatterns": [],
                          "piiValuePatterns": [], "valueSampleSize": 0 },
              "tables": []
            }
            """;
        Assert.Throws<InvalidDataException>(() => ClassificationReader.Parse(json));
    }

    [Fact]
    public void Human_override_masking_strategy_parses()
    {
        string json = """
            {
              "version": "1.0",
              "database": "T.accdb",
              "scannedUtc": "2026-05-28T12:00:00Z",
              "config": { "systemColumnPatterns": [], "piiNamePatterns": [],
                          "piiValuePatterns": [], "valueSampleSize": 0 },
              "tables": [
                {
                  "name": "T", "isLinkedTable": false, "rowCount": 0,
                  "lookupGuess": false, "nonKeyNonSystemColumnCount": 1,
                  "referencedByFkCount": 0, "tableHint": "unclassified",
                  "confidence": 1.0, "firedHeuristics": [],
                  "humanOverride": { "isLookup": null, "reviewedBy": null, "notes": "" },
                  "columns": [
                    {
                      "name": "c", "ordinalPosition": 0, "dataType": "Text",
                      "isAutoNumber": false, "isPrimaryKey": false, "isForeignKey": false,
                      "fkTarget": null, "isMemo": false, "isOleObject": false,
                      "isMultiValue": false, "isRequired": false,
                      "rowCount": 0, "nullCount": 0, "cardinality": 0,
                      "uniquenessRatio": 0, "relativeCardinality": 0,
                      "piiNameMatch": false, "piiValueMatch": false,
                      "valueFormatPattern": null,
                      "guessedCategory": "free-text-memo",
                      "confidence": 1.0, "firedHeuristics": [],
                      "humanOverride": {
                        "category": null,
                        "maskingStrategy": "redact",
                        "preserveAutonumber": false,
                        "reviewedBy": "tester",
                        "notes": ""
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var doc = ClassificationReader.Parse(json);
        Assert.Equal(MaskingStrategy.Redact, doc.Tables[0].Columns[0].HumanOverride.MaskingStrategy);
    }

    private static string RepoRoot()
    {
        // Tests run from tests/AccessHarvester.Pipeline.Tests/bin/Debug/net10.0.
        // Walk up to find the directory that contains 'samples'.
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;
        return dir?.FullName ?? string.Empty;
    }
}
