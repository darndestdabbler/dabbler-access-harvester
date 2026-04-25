using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Core.Enrichment;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="GeometryEnricher"/>. Uses hand-authored KB
/// JSON as input, so no Access / COM / Python dependency is required to run.
/// Integration-level tests that drive the pipeline against a real .accdb live
/// in <see cref="SyntheticDbIntegrationTests"/>.
/// </summary>
public class GeometryEnricherTests
{
    private const string KbForm_TwoTextBoxes = """
    {
      "Type": "Form",
      "Properties": { "Width": "10080" },
      "Children": [
        {
          "Type": "Section",
          "Properties": { "Height": "2160", "Name": "Detail" },
          "Children": [
            {
              "Type": "TextBox",
              "Properties": {
                "Name": "txtA",
                "Left": "360",
                "Top": "720",
                "Width": "1440",
                "Height": "360"
              },
              "Children": [],
              "VbaCode": null
            },
            {
              "Type": "TextBox",
              "Properties": {
                "Name": "txtB",
                "Left": "1500",
                "Top": "720",
                "Width": "1500",
                "Height": "360"
              },
              "Children": [],
              "VbaCode": null
            }
          ],
          "VbaCode": null
        }
      ],
      "VbaCode": null
    }
    """;

    [Fact]
    public void EnrichFile_AddsGeometryIn_WithCorrectInchValues()
    {
        var inFile = WriteTempFile(KbForm_TwoTextBoxes);
        var outFile = Path.GetTempFileName();
        try
        {
            GeometryEnricher.Forms.EnrichFile(inFile, outFile);

            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();

            // Form-level: Width=10080 twips → 7 inches, no Left/Top/Height.
            var formGeom = doc["GeometryIn"]!.AsObject();
            Assert.Equal(7.0, formGeom["W"]!.GetValue<double>());
            Assert.Null(formGeom["X"]);
            Assert.Null(formGeom["Y"]);
            Assert.Null(formGeom["H"]);

            // Section: Height=2160 twips → 1.5 inches.
            var section = doc["Children"]!.AsArray()[0]!.AsObject();
            var sectionGeom = section["GeometryIn"]!.AsObject();
            Assert.Equal(1.5, sectionGeom["H"]!.GetValue<double>());

            // txtA: whole-inch values.
            var txtA = section["Children"]!.AsArray()[0]!.AsObject();
            var a = txtA["GeometryIn"]!.AsObject();
            Assert.Equal(0.25, a["X"]!.GetValue<double>());
            Assert.Equal(0.5, a["Y"]!.GetValue<double>());
            Assert.Equal(1.0, a["W"]!.GetValue<double>());
            Assert.Equal(0.25, a["H"]!.GetValue<double>());

            // txtB: 1500/1440 = 1.041666... → rounded to 3 dp = 1.042.
            var txtB = section["Children"]!.AsArray()[1]!.AsObject();
            var b = txtB["GeometryIn"]!.AsObject();
            Assert.Equal(1.042, b["X"]!.GetValue<double>());
            Assert.Equal(1.042, b["W"]!.GetValue<double>());
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    [Fact]
    public void EnrichFile_MissingDimension_OmitsThatFieldFromGeometryIn()
    {
        // Control with only Height — should emit GeometryIn with just H.
        var kb = """
        {
          "Type": "Section",
          "Properties": { "Height": "720" },
          "Children": [],
          "VbaCode": null
        }
        """;
        var inFile = WriteTempFile(kb);
        var outFile = Path.GetTempFileName();
        try
        {
            GeometryEnricher.Forms.EnrichFile(inFile, outFile);

            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            var geom = doc["GeometryIn"]!.AsObject();

            Assert.Equal(1, geom.Count);
            Assert.Equal(0.5, geom["H"]!.GetValue<double>());
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    [Fact]
    public void EnrichFile_NonIntegerPropertyValue_SkipsThatField()
    {
        // "NotDefault" is a common non-integer value Access writes — the
        // enricher should ignore it, not throw.
        var kb = """
        {
          "Type": "TextBox",
          "Properties": {
            "Left": "NotDefault",
            "Top": "720",
            "Width": "1440",
            "Height": "360"
          },
          "Children": [],
          "VbaCode": null
        }
        """;
        var inFile = WriteTempFile(kb);
        var outFile = Path.GetTempFileName();
        try
        {
            GeometryEnricher.Forms.EnrichFile(inFile, outFile);

            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            var geom = doc["GeometryIn"]!.AsObject();

            Assert.Null(geom["X"]);                           // skipped — not a number
            Assert.Equal(0.5, geom["Y"]!.GetValue<double>());
            Assert.Equal(1.0, geom["W"]!.GetValue<double>());
            Assert.Equal(0.25, geom["H"]!.GetValue<double>());
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    [Fact]
    public void EnrichFile_NoGeometryProperties_OmitsGeometryIn()
    {
        var kb = """
        {
          "Type": "Form",
          "Properties": { "Name": "frmX" },
          "Children": [],
          "VbaCode": null
        }
        """;
        var inFile = WriteTempFile(kb);
        var outFile = Path.GetTempFileName();
        try
        {
            GeometryEnricher.Forms.EnrichFile(inFile, outFile);

            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            Assert.False(doc.ContainsKey("GeometryIn"));
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    private static string WriteTempFile(string contents)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents);
        return path;
    }
}
