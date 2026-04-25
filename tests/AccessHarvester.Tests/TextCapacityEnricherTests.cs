using System.Text.Json.Nodes;
using AccessHarvester.Core.Enrichment;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="TextCapacityEnricher"/>. Each test composes
/// a small KB-shape form with the relevant control(s), runs <see cref="GeometryEnricher"/>
/// (so <c>GeometryIn</c> exists, matching real pipeline order) and then
/// <see cref="TextCapacityEnricher"/>, and asserts on the resulting block.
/// </summary>
public class TextCapacityEnricherTests
{
    // Aptos 11pt: avgCharWidthEm 0.50, lineHeightEm 1.20.
    //   avgCharW = 0.50 * 11 * 20 = 110 twips.
    //   lineH    = 1.20 * 11 * 20 = 264 twips.
    private const double AptosAvgCharW11 = 110.0;
    private const double AptosLineH11 = 264.0;

    [Fact]
    public void SimpleTextBox_NoPadding_ComputesAptosCapacity()
    {
        // 2880 / 110 = 26.18 → 26 chars; 360 / 264 = 1.36 → 1 line.
        var form = MakeForm(MakeControl("TextBox", "txt1", 0, 0, 2880, 360,
            fontName: "Aptos", fontSize: "11", noPadding: true));

        var ctrl = FindControl(EnrichAndRead(form), "txt1");
        var cap = ctrl["TextCapacity"]!.AsObject();

        Assert.Equal(26, cap["charsPerLine"]!.GetValue<int>());
        Assert.Equal(1, cap["visibleLines"]!.GetValue<int>());
        Assert.Equal("Aptos", cap["font"]!["name"]!.GetValue<string>());
        Assert.Equal(11, cap["font"]!["size"]!.GetValue<int>());
        Assert.Equal("explicit", cap["font"]!["resolution"]!.GetValue<string>());
        Assert.Equal("avgWidth", cap["metric"]!.GetValue<string>());
    }

    [Fact]
    public void MultiLineTextBox_VisibleLinesScalesWithHeight()
    {
        // 2160 / 264 = 8.18 → 8 lines (no padding subtracted).
        var form = MakeForm(MakeControl("TextBox", "memo", 0, 0, 2880, 2160,
            fontName: "Aptos", fontSize: "11", noPadding: true));

        var cap = FindControl(EnrichAndRead(form), "memo")["TextCapacity"]!.AsObject();
        Assert.Equal(8, cap["visibleLines"]!.GetValue<int>());
    }

    [Fact]
    public void DefaultPadding_IsSubtractedFromInnerArea()
    {
        // No LeftPadding/etc. in Properties → enricher applies 30-twip default
        // on each side. Width 2880 - 60 = 2820 inner; 2820/110 = 25.6 → 25.
        var form = MakeForm(MakeControl("TextBox", "txt", 0, 0, 2880, 360,
            fontName: "Aptos", fontSize: "11"));

        var cap = FindControl(EnrichAndRead(form), "txt")["TextCapacity"]!.AsObject();
        Assert.Equal(25, cap["charsPerLine"]!.GetValue<int>());
    }

    [Fact]
    public void CourierNew_EmitsExactMetric()
    {
        // Courier New marks fixedWidth in font-metrics.json — capacity is exact.
        var form = MakeForm(MakeControl("TextBox", "code", 0, 0, 1440, 360,
            fontName: "Courier New", fontSize: "12", noPadding: true));

        var cap = FindControl(EnrichAndRead(form), "code")["TextCapacity"]!.AsObject();
        Assert.Equal("exact", cap["metric"]!.GetValue<string>());
        // 1440 / (0.60 * 12 * 20) = 1440 / 144 = 10.
        Assert.Equal(10, cap["charsPerLine"]!.GetValue<int>());
    }

    [Fact]
    public void UnknownFont_FallsBackToFallbackMetricsAndResolution()
    {
        // "Wingdings" isn't in our table → fallback (avgCharWidthEm 0.50, lineHeightEm 1.20).
        var form = MakeForm(MakeControl("TextBox", "txt", 0, 0, 2200, 360,
            fontName: "Wingdings", fontSize: "10", noPadding: true));

        var cap = FindControl(EnrichAndRead(form), "txt")["TextCapacity"]!.AsObject();
        Assert.Equal("fallback", cap["font"]!["resolution"]!.GetValue<string>());
        Assert.Equal("Wingdings", cap["font"]!["name"]!.GetValue<string>());
        // 2200 / (0.50 * 10 * 20) = 2200 / 100 = 22.
        Assert.Equal(22, cap["charsPerLine"]!.GetValue<int>());
    }

    [Fact]
    public void ControlWithoutGeometry_EmitsUnavailable()
    {
        // No Width/Height in Properties → GeometryEnricher emits no GeometryIn,
        // and TextCapacity reports unavailable rather than guessing zero.
        var ctrl = new JsonObject
        {
            ["Type"] = "TextBox",
            ["Properties"] = new JsonObject
            {
                ["Name"] = "txt",
                ["FontName"] = "Aptos",
                ["FontSize"] = "11",
            },
            ["Children"] = new JsonArray(),
        };
        var form = MakeForm(ctrl);

        var cap = FindControl(EnrichAndRead(form), "txt")["TextCapacity"]!.AsObject();
        Assert.True(cap["unavailable"]!.GetValue<bool>());
        Assert.Equal("no geometry", cap["reason"]!.GetValue<string>());
        Assert.False(cap.ContainsKey("charsPerLine"));
    }

    [Fact]
    public void FormLevelFont_IsInheritedByControlsWithoutOwnFont()
    {
        // Form sets FontName=Calibri, FontSize=10. Control sets neither.
        var form = MakeForm(MakeControl("TextBox", "txt", 0, 0, 2400, 360,
            fontName: null, fontSize: null, noPadding: true));
        form["Properties"]!["FontName"] = "Calibri";
        form["Properties"]!["FontSize"] = "10";

        var cap = FindControl(EnrichAndRead(form), "txt")["TextCapacity"]!.AsObject();
        Assert.Equal("Calibri", cap["font"]!["name"]!.GetValue<string>());
        Assert.Equal(10, cap["font"]!["size"]!.GetValue<int>());
        Assert.Equal("inherited", cap["font"]!["resolution"]!.GetValue<string>());
        // Calibri avgCharWidthEm 0.48 * 10 * 20 = 96 twips. 2400 / 96 = 25.
        Assert.Equal(25, cap["charsPerLine"]!.GetValue<int>());
    }

    [Fact]
    public void ControlOwnFont_BeatsFormDefault()
    {
        var form = MakeForm(MakeControl("TextBox", "txt", 0, 0, 2400, 360,
            fontName: "Arial", fontSize: "12", noPadding: true));
        form["Properties"]!["FontName"] = "Calibri";
        form["Properties"]!["FontSize"] = "10";

        var cap = FindControl(EnrichAndRead(form), "txt")["TextCapacity"]!.AsObject();
        Assert.Equal("Arial", cap["font"]!["name"]!.GetValue<string>());
        Assert.Equal(12, cap["font"]!["size"]!.GetValue<int>());
        Assert.Equal("explicit", cap["font"]!["resolution"]!.GetValue<string>());
    }

    [Fact]
    public void LabelWithCanGrowTrue_GetsNoTextCapacity()
    {
        var label = new JsonObject
        {
            ["Type"] = "Label",
            ["Properties"] = new JsonObject
            {
                ["Name"] = "lbl",
                ["Caption"] = "wrap me",
                ["Left"] = "0", ["Top"] = "0", ["Width"] = "1440", ["Height"] = "360",
                ["CanGrow"] = "True",
            },
            ["Children"] = new JsonArray(),
        };
        var form = MakeForm(label);

        var node = FindByName(EnrichAndRead(form), "lbl");
        Assert.False(node.ContainsKey("TextCapacity"));
    }

    [Fact]
    public void LabelWithCanGrowNotDefault_GetsNoTextCapacity()
    {
        // PropertyDecoder doesn't decode Label.CanGrow (Label isn't in the boolean
        // defaults table). The "NotDefault" marker against a False default still
        // unambiguously means True, so we treat it as enabled.
        var label = new JsonObject
        {
            ["Type"] = "Label",
            ["Properties"] = new JsonObject
            {
                ["Name"] = "lbl",
                ["Caption"] = "x",
                ["Left"] = "0", ["Top"] = "0", ["Width"] = "1440", ["Height"] = "360",
                ["CanGrow"] = "NotDefault",
            },
            ["Children"] = new JsonArray(),
        };
        var form = MakeForm(label);

        var node = FindByName(EnrichAndRead(form), "lbl");
        Assert.False(node.ContainsKey("TextCapacity"));
    }

    [Fact]
    public void LabelWithoutCanGrow_GetsTextCapacity()
    {
        var label = new JsonObject
        {
            ["Type"] = "Label",
            ["Properties"] = new JsonObject
            {
                ["Name"] = "lbl",
                ["Caption"] = "x",
                ["Left"] = "0", ["Top"] = "0", ["Width"] = "1440", ["Height"] = "360",
                ["FontName"] = "Aptos", ["FontSize"] = "11",
                ["LeftPadding"] = "0", ["RightPadding"] = "0",
                ["TopPadding"] = "0", ["BottomPadding"] = "0",
            },
            ["Children"] = new JsonArray(),
        };
        var form = MakeForm(label);

        var node = FindByName(EnrichAndRead(form), "lbl");
        Assert.True(node.ContainsKey("TextCapacity"));
    }

    [Fact]
    public void IneligibleType_GetsNoTextCapacity()
    {
        // CommandButton's Caption is its visible text — no capacity needed.
        var btn = MakeControl("CommandButton", "btn", 0, 0, 1440, 360,
            fontName: "Aptos", fontSize: "11");
        var form = MakeForm(btn);

        var node = FindControl(EnrichAndRead(form), "btn");
        Assert.False(node.ContainsKey("TextCapacity"));
    }

    [Fact]
    public void Sections_And_Forms_DoNotReceiveTextCapacity()
    {
        var form = MakeForm(MakeControl("TextBox", "txt", 0, 0, 1440, 360,
            fontName: "Aptos", fontSize: "11"));

        var ir = EnrichAndRead(form);
        Assert.False(ir.ContainsKey("TextCapacity"));
        var section = ir["Children"]!.AsArray()[0]!.AsObject();
        Assert.False(section.ContainsKey("TextCapacity"));
    }

    // ============== fixtures ==============

    private static JsonObject MakeForm(JsonObject control)
    {
        return new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject { ["Width"] = "10080" },
            ["Children"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "Section",
                    ["Properties"] = new JsonObject
                    {
                        ["Name"] = "Detail",
                        ["Height"] = "2160",
                    },
                    ["Children"] = new JsonArray { control },
                },
            },
        };
    }

    private static JsonObject MakeControl(
        string type, string name, int left, int top, int w, int h,
        string? fontName = null, string? fontSize = null,
        string? controlSource = null, bool noPadding = false)
    {
        var props = new JsonObject
        {
            ["Name"] = name,
            ["Left"] = left.ToString(),
            ["Top"] = top.ToString(),
            ["Width"] = w.ToString(),
            ["Height"] = h.ToString(),
        };
        if (fontName is not null) props["FontName"] = fontName;
        if (fontSize is not null) props["FontSize"] = fontSize;
        if (controlSource is not null) props["ControlSource"] = controlSource;
        if (noPadding)
        {
            props["LeftPadding"] = "0";
            props["RightPadding"] = "0";
            props["TopPadding"] = "0";
            props["BottomPadding"] = "0";
        }
        return new JsonObject
        {
            ["Type"] = type,
            ["Properties"] = props,
            ["Children"] = new JsonArray(),
        };
    }

    private static JsonObject EnrichAndRead(JsonObject form)
    {
        var inFile = Path.GetTempFileName();
        var outFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(inFile, form.ToJsonString());
            GeometryEnricher.Forms.EnrichFile(inFile, outFile);
            TextCapacityEnricher.Forms.EnrichFile(outFile, outFile);
            return JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
        }
        finally
        {
            if (File.Exists(inFile)) File.Delete(inFile);
            if (File.Exists(outFile)) File.Delete(outFile);
        }
    }

    private static JsonObject FindControl(JsonObject root, string name)
    {
        var found = FindByNameOrNull(root, name, excludeType: null);
        Assert.NotNull(found);
        return found!;
    }

    private static JsonObject FindByName(JsonObject root, string name)
    {
        var found = FindByNameOrNull(root, name, excludeType: null);
        Assert.NotNull(found);
        return found!;
    }

    private static JsonObject? FindByNameOrNull(JsonObject n, string name, string? excludeType)
    {
        if (n["Properties"] is JsonObject p
            && p["Name"] is JsonValue v
            && v.TryGetValue<string>(out var s)
            && s == name
            && (excludeType is null || n["Type"]?.GetValue<string>() != excludeType))
        {
            return n;
        }
        if (n["Children"] is JsonArray arr)
        {
            foreach (var c in arr)
            {
                if (c is JsonObject co)
                {
                    var hit = FindByNameOrNull(co, name, excludeType);
                    if (hit is not null) return hit;
                }
            }
        }
        return null;
    }
}
