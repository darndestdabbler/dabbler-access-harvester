using System.Text.Json.Nodes;
using AccessHarvester.Core.Enrichment;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="PropertyDecoder"/>. Uses the embedded
/// defaults table loaded from Resources/property-defaults.json.
/// </summary>
public class PropertyDecoderTests
{
    [Fact]
    public void Decode_FormPropertyDefaultTrue_FlipsToFalse()
    {
        // RecordSelectors default is True on a Form → NotDefault means False.
        var form = NodeWithProperties("Form", ("RecordSelectors", "NotDefault"));
        var result = Enrich(form);

        Assert.Equal("False", result["Properties"]!["RecordSelectors"]!.GetValue<string>());
        Assert.Equal(new[] { "RecordSelectors" },
            result["NonDefaultProperties"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void Decode_FormPropertyDefaultFalse_FlipsToTrue()
    {
        // Modal default is False on a Form → NotDefault means True (modal dialog).
        var form = NodeWithProperties("Form", ("Modal", "NotDefault"));
        var result = Enrich(form);

        Assert.Equal("True", result["Properties"]!["Modal"]!.GetValue<string>());
    }

    [Fact]
    public void Decode_MultipleNotDefaults_AllFlippedAndAllListed()
    {
        var form = NodeWithProperties("Form",
            ("RecordSelectors", "NotDefault"),
            ("NavigationButtons", "NotDefault"),
            ("AllowAdditions", "NotDefault"),
            ("Modal", "NotDefault"),
            ("AutoCenter", "NotDefault"));

        var result = Enrich(form);
        var props = result["Properties"]!.AsObject();

        Assert.Equal("False", props["RecordSelectors"]!.GetValue<string>());
        Assert.Equal("False", props["NavigationButtons"]!.GetValue<string>());
        Assert.Equal("False", props["AllowAdditions"]!.GetValue<string>());
        Assert.Equal("True", props["Modal"]!.GetValue<string>());
        Assert.Equal("True", props["AutoCenter"]!.GetValue<string>());

        var nonDefault = result["NonDefaultProperties"]!.AsArray()
            .Select(n => n!.GetValue<string>()).ToHashSet();
        Assert.Equal(
            new HashSet<string> { "RecordSelectors", "NavigationButtons", "AllowAdditions", "Modal", "AutoCenter" },
            nonDefault);
    }

    [Fact]
    public void Decode_NonNotDefaultValues_LeftUntouched()
    {
        var form = NodeWithProperties("Form",
            ("Width", "10080"),
            ("Caption", "My Form"),
            ("RecordSelectors", "NotDefault"));

        var result = Enrich(form);
        var props = result["Properties"]!.AsObject();

        Assert.Equal("10080", props["Width"]!.GetValue<string>());
        Assert.Equal("My Form", props["Caption"]!.GetValue<string>());
        Assert.Equal("False", props["RecordSelectors"]!.GetValue<string>());
    }

    [Fact]
    public void Decode_UnknownPropertyOnKnownKind_LeavesNotDefaultInPlace()
    {
        // SomeFutureProperty isn't in our defaults table — we don't know what
        // default to invert, so the value should remain the literal string
        // "NotDefault". We still list it in NonDefaultProperties so downstream
        // tools can tell that a property was flagged.
        var form = NodeWithProperties("Form", ("SomeFutureProperty", "NotDefault"));
        var result = Enrich(form);

        Assert.Equal("NotDefault",
            result["Properties"]!["SomeFutureProperty"]!.GetValue<string>());
        Assert.Equal(new[] { "SomeFutureProperty" },
            result["NonDefaultProperties"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void Decode_UnknownKind_LeavesEverythingAlone()
    {
        // A node whose Type string we don't recognize at all — leave values
        // untouched but still record that they deviated.
        var node = NodeWithProperties("FutureControlType", ("SomeProp", "NotDefault"));
        var result = Enrich(node);

        Assert.Equal("NotDefault", result["Properties"]!["SomeProp"]!.GetValue<string>());
        Assert.Equal(new[] { "SomeProp" },
            result["NonDefaultProperties"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void Decode_ControlInsideForm_UsesControlTypeDefaults()
    {
        // TextBox.Locked default is False → NotDefault means True.
        var form = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "TextBox",
                    ["Properties"] = new JsonObject
                    {
                        ["Name"] = "txtCode",
                        ["Locked"] = "NotDefault",
                    },
                    ["Children"] = new JsonArray(),
                }
            }
        };

        var result = Enrich(form);
        var txt = result["Children"]!.AsArray()[0]!.AsObject();

        Assert.Equal("True", txt["Properties"]!["Locked"]!.GetValue<string>());
        Assert.Equal(new[] { "Locked" },
            txt["NonDefaultProperties"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void Decode_NodeWithNoProperties_Noop()
    {
        var node = new JsonObject
        {
            ["Type"] = "Form",
            ["Children"] = new JsonArray(),
        };

        var result = Enrich(node);
        Assert.False(result.ContainsKey("NonDefaultProperties"));
    }

    [Fact]
    public void ChainedWithGeometryAndLabelResolver_AllThreeContributionsPresent()
    {
        // Pipeline order is Geometry → LabelResolver → PropertyDecoder.
        // Verify all three effects survive on a single TextBox.
        var form = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject
            {
                ["Width"] = "10080",
                ["RecordSelectors"] = "NotDefault",
            },
            ["Children"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "Section",
                    ["Properties"] = new JsonObject { ["Height"] = "2160", ["Name"] = "Detail" },
                    ["Children"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["Type"] = "TextBox",
                            ["Properties"] = new JsonObject
                            {
                                ["Name"] = "CustomerFirstName",
                                ["Left"] = "360",
                                ["Top"] = "720",
                                ["Width"] = "1440",
                                ["Height"] = "360",
                                ["Locked"] = "NotDefault",
                            },
                            ["Children"] = new JsonArray(),
                        }
                    }
                }
            }
        };

        var kbFile = Path.GetTempFileName();
        var irFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(kbFile, form.ToJsonString());
            GeometryEnricher.Forms.EnrichFile(kbFile, irFile);
            LabelResolver.Forms.EnrichFile(irFile, irFile);
            PropertyDecoder.Forms.EnrichFile(irFile, irFile);

            var result = JsonNode.Parse(File.ReadAllText(irFile))!.AsObject();

            // Form-level: Geometry + decoded property + NonDefaultProperties
            Assert.Equal(7.0, result["GeometryIn"]!["W"]!.GetValue<double>());
            Assert.Equal("False", result["Properties"]!["RecordSelectors"]!.GetValue<string>());
            Assert.Contains("RecordSelectors",
                result["NonDefaultProperties"]!.AsArray().Select(n => n!.GetValue<string>()));

            // Control-level: Geometry + Label + decoded property
            var txt = result["Children"]!.AsArray()[0]!["Children"]!.AsArray()[0]!.AsObject();
            Assert.Equal(0.25, txt["GeometryIn"]!["X"]!.GetValue<double>());
            Assert.Equal("FromName", txt["Label"]!["source"]!.GetValue<string>());
            Assert.Equal("Customer First Name", txt["Label"]!["text"]!.GetValue<string>());
            Assert.Equal("True", txt["Properties"]!["Locked"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(kbFile);
            if (File.Exists(irFile)) File.Delete(irFile);
        }
    }

    // ----- helpers -----

    private static JsonObject NodeWithProperties(string type, params (string Key, string Value)[] properties)
    {
        var props = new JsonObject();
        foreach (var (k, v) in properties) props[k] = v;
        return new JsonObject
        {
            ["Type"] = type,
            ["Properties"] = props,
            ["Children"] = new JsonArray(),
        };
    }

    private static JsonObject Enrich(JsonObject input)
    {
        var inFile = Path.GetTempFileName();
        var outFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(inFile, input.ToJsonString());
            PropertyDecoder.Forms.EnrichFile(inFile, outFile);
            return JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
        }
        finally
        {
            File.Delete(inFile);
            if (File.Exists(outFile)) File.Delete(outFile);
        }
    }
}
