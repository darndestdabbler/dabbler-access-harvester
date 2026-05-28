using System.Text.Json.Nodes;
using AccessHarvester.Core.Enrichment;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="LabelResolver"/>. Each test constructs a
/// minimal hand-authored KB-shape JSON that exercises one round's decision path.
/// </summary>
public class LabelResolverTests
{
    // ----- Round 1: attached label -----

    [Fact]
    public void Round1_ControlWithAttachedLabelChild_UsesLabelCaption()
    {
        var form = BuildForm(
            section => section.AddControl("TextBox", "txtA", left: 0, top: 0, w: 1440, h: 360,
                attachedLabel: "First Name"));

        var outFile = EnrichAndWrite(form);
        var result = ReadForm(outFile);

        var ctrl = FindControl(result, "txtA");
        AssertLabel(ctrl, source: "Attached", text: "First Name");
    }

    [Fact]
    public void Round1_AttachedLabel_TakesPriority_OverNameAndBinding()
    {
        var form = BuildForm(
            section => section.AddControl("TextBox", "customerFirstName",
                left: 0, top: 0, w: 1440, h: 360,
                controlSource: "FirstName",
                attachedLabel: "Given Name"));

        var result = ReadForm(EnrichAndWrite(form));

        AssertLabel(FindControl(result, "customerFirstName"),
            source: "Attached", text: "Given Name");
    }

    [Fact]
    public void Round1_AttachedLabelWithEmptyCaption_FallsThroughToLaterRounds()
    {
        // Access auto-creates an attached label whose Caption is often empty until
        // the developer sets one. An empty attached label should not block other
        // rounds — Round 3 here picks up the non-generic Name.
        var form = BuildForm(section =>
            section.AddControl("TextBox", "EmployeeName", 0, 0, 1440, 360,
                attachedLabel: ""));

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "EmployeeName"),
            source: "FromName", text: "Employee Name");
    }

    // ----- Round 2: proximity -----

    [Fact]
    public void Round2_SingleLabelSingleControl_ResolvedByVacuousUniqueness()
    {
        // Label at (0, 0) 1440x360; TextBox at (1500, 0) 1440x360.
        // No other labels or controls — both 2× checks are vacuously true.
        var form = BuildForm(section =>
        {
            section.AddLabel("lblAmount", "Amount", left: 0, top: 0, w: 1440, h: 360);
            section.AddControl("TextBox", "Text1", left: 1500, top: 0, w: 1440, h: 360);
        });

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"),
            source: "Proximity", text: "Amount");
    }

    [Fact]
    public void Round2_AmbiguousTwoLabelsEquidistant_Unresolved()
    {
        // Two labels, one control midway between them — both gaps equal → label
        // uniqueness fails (second gap is not >= 2× best gap, it's equal).
        var form = BuildForm(section =>
        {
            section.AddLabel("lblA", "Alpha", left: 0, top: 0, w: 720, h: 360);
            section.AddLabel("lblB", "Beta", left: 3000, top: 0, w: 720, h: 360);
            section.AddControl("TextBox", "Text1", left: 1600, top: 0, w: 720, h: 360);
        });

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"), source: "Unresolved", text: null);
    }

    [Fact]
    public void Round2_AdversarialLabelEquidistantBetweenTwoControls_BothRejected()
    {
        // Label sits midway between two eligible controls — L's gap to C1 equals
        // its gap to C2, so control-uniqueness fails for both: neither wins.
        var form = BuildForm(section =>
        {
            section.AddLabel("lbl", "Shared", left: 1440, top: 0, w: 720, h: 360);  // x-range [1440, 2160]
            section.AddControl("TextBox", "Text1", left: 0, top: 0, w: 720, h: 360);    // gap 720
            section.AddControl("TextBox", "Text2", left: 2880, top: 0, w: 720, h: 360); // gap 720
        });

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"), source: "Unresolved", text: null);
        AssertLabel(FindControl(result, "Text2"), source: "Unresolved", text: null);
    }

    [Fact]
    public void Round2_OneControlDominates_LabelAssignedDespiteSecondControl()
    {
        // L is much closer to the first control than to the second — the closer
        // control wins, the 2× rule holds both ways. Using generic names keeps
        // Round 3 out of the way so the non-winner cleanly falls through to
        // "unresolved", proving the label didn't leak onto both controls.
        var form = BuildForm(section =>
        {
            section.AddLabel("lbl", "First Name", left: 0, top: 0, w: 1440, h: 360);
            section.AddControl("TextBox", "Text1", left: 1500, top: 0, w: 1440, h: 360);  // gap 60
            section.AddControl("TextBox", "Text2", left: 4500, top: 0, w: 1440, h: 360);  // gap 3060
        });

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"), source: "Proximity", text: "First Name");
        AssertLabel(FindControl(result, "Text2"), source: "Unresolved", text: null);
    }

    [Fact]
    public void Round2_PerSection_LabelsDoNotCrossSectionBoundaries()
    {
        // Label in Header, control in Detail — different coordinate systems;
        // proximity should NOT match across sections.
        var form = BuildFormMultiSection(
            header => header.AddLabel("lblHeader", "Header Label", 0, 0, 1440, 360),
            detail => detail.AddControl("TextBox", "Text1", 0, 0, 1440, 360));

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"), source: "Unresolved", text: null);
    }

    // ----- Round 3: non-generic Name -----

    [Fact]
    public void Round3_NonGenericName_SynthesizesFromCamelCase()
    {
        // No label, no attached, no proximity candidate — Name "CustomerFirstName"
        // is not a generic auto-name, so Round 3 fires.
        var form = BuildForm(section =>
            section.AddControl("TextBox", "CustomerFirstName", 0, 0, 1440, 360));

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "CustomerFirstName"),
            source: "FromName", text: "Customer First Name");
    }

    [Fact]
    public void Round3_GenericName_SkippedToRound4()
    {
        // Name "Text1" is a generic Access auto-name → Round 3 skips.
        // ControlSource supplied → Round 4 fires.
        var form = BuildForm(section =>
            section.AddControl("TextBox", "Text1", 0, 0, 1440, 360,
                controlSource: "OrderTotal"));

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"),
            source: "FromBinding", text: "Order Total");
    }

    // ----- Round 4: ControlSource -----

    [Fact]
    public void Round4_SimpleIdentifierControlSource_SynthesizesFromCamelCase()
    {
        var form = BuildForm(section =>
            section.AddControl("TextBox", "Text1", 0, 0, 1440, 360,
                controlSource: "firstName"));

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"),
            source: "FromBinding", text: "First Name");
    }

    [Fact]
    public void Round4_ExpressionControlSource_Skipped_FallsThroughToUnresolved()
    {
        // "=Sum([Amount])" is an expression, not a column — Round 4 should reject.
        var form = BuildForm(section =>
            section.AddControl("TextBox", "Text1", 0, 0, 1440, 360,
                controlSource: "=Sum([Amount])"));

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"), source: "Unresolved", text: null);
    }

    // ----- Unresolved -----

    [Fact]
    public void AllRoundsFail_EmitsUnresolvedWithNoText()
    {
        // Generic name, no binding, no label — every round skips.
        var form = BuildForm(section =>
            section.AddControl("TextBox", "Text1", 0, 0, 1440, 360));

        var result = ReadForm(EnrichAndWrite(form));
        var ctrl = FindControl(result, "Text1");
        AssertLabel(ctrl, source: "Unresolved", text: null);
    }

    // ----- Begin-wrapper transparency -----

    [Fact]
    public void BeginWrapperAroundControls_DoesNotHideThemFromSection()
    {
        // Access SaveAsText wraps the control list in a Begin node. LabelResolver
        // must descend through that wrapper to find its peers.
        var form = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject { ["Width"] = "10080" },
            ["Children"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "Section",
                    ["Properties"] = new JsonObject { ["Height"] = "2160", ["Name"] = "Detail" },
                    ["Children"] = new JsonArray
                    {
                        new JsonObject  // anonymous Begin wrapper
                        {
                            ["Type"] = "Begin",
                            ["Properties"] = new JsonObject(),
                            ["Children"] = new JsonArray
                            {
                                MakeLabel("lbl", "Amount", 0, 0, 1440, 360),
                                MakeControl("TextBox", "Text1", 1500, 0, 1440, 360),
                            }
                        }
                    }
                }
            }
        };

        var result = ReadForm(EnrichAndWrite(form));
        AssertLabel(FindControl(result, "Text1"),
            source: "Proximity", text: "Amount");
    }

    // ----- Ineligible types don't get a Label key -----

    [Fact]
    public void Sections_And_Labels_DoNotReceiveLabelKey()
    {
        var form = BuildForm(section =>
            section.AddLabel("lblA", "Something", 0, 0, 720, 360));

        var result = ReadForm(EnrichAndWrite(form));
        var section = result["Children"]!.AsArray()[0]!.AsObject();
        var label = section["Children"]!.AsArray()[0]!.AsObject();

        Assert.False(result.ContainsKey("Label"));
        Assert.False(section.ContainsKey("Label"));
        Assert.False(label.ContainsKey("Label"));
    }

    // ----- Chaining with GeometryEnricher (verifies the in==out contract) -----

    [Fact]
    public void ChainedWithGeometryEnricher_BothContributionsPresent()
    {
        // GeometryEnricher runs first (kb -> ir); LabelResolver rewrites ir in place.
        // Both GeometryIn and Label should end up on the control.
        var form = BuildForm(section => section.AddControl(
            "TextBox", "CustomerFirstName", left: 360, top: 720, w: 1440, h: 360));

        var kbFile = Path.GetTempFileName();
        var irFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(kbFile, form.ToJsonString());
            GeometryEnricher.Forms.EnrichFile(kbFile, irFile);
            LabelResolver.Forms.EnrichFile(irFile, irFile);   // in == out

            var result = JsonNode.Parse(File.ReadAllText(irFile))!.AsObject();
            var ctrl = FindControl(result, "CustomerFirstName");

            // Geometry survives.
            var geom = ctrl["GeometryIn"]!.AsObject();
            Assert.Equal(0.25, geom["X"]!.GetValue<double>());
            Assert.Equal(0.5, geom["Y"]!.GetValue<double>());

            // Label was added.
            AssertLabel(ctrl, source: "FromName", text: "Customer First Name");
        }
        finally
        {
            File.Delete(kbFile);
            if (File.Exists(irFile)) File.Delete(irFile);
        }
    }

    // ----- SplitCamelCase unit cases -----

    [Theory]
    [InlineData("CustomerFirstName", "Customer First Name")]
    [InlineData("customerFirstName", "Customer First Name")]
    [InlineData("firstName", "First Name")]
    [InlineData("Customer", "Customer")]
    [InlineData("ID", "ID")]
    [InlineData("custID", "Cust ID")]
    [InlineData("XMLParser", "XML Parser")]
    [InlineData("addr1Line", "Addr1 Line")]
    public void SplitCamelCase_KnownCases(string input, string expected)
    {
        Assert.Equal(expected, LabelResolver.SplitCamelCase(input));
    }

    // ================== test-fixture builders ==================

    private sealed class SectionBuilder
    {
        public JsonArray Children { get; } = new();

        public void AddLabel(string name, string caption, int left, int top, int w, int h)
            => Children.Add(MakeLabel(name, caption, left, top, w, h));

        public void AddControl(string type, string name, int left, int top, int w, int h,
            string? controlSource = null, string? attachedLabel = null)
            => Children.Add(MakeControl(type, name, left, top, w, h,
                controlSource: controlSource, attachedLabel: attachedLabel));
    }

    private static JsonObject BuildForm(Action<SectionBuilder> buildDetail)
    {
        var detail = new SectionBuilder();
        buildDetail(detail);

        return new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject { ["Width"] = "10080" },
            ["Children"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "Section",
                    ["Properties"] = new JsonObject { ["Height"] = "2160", ["Name"] = "Detail" },
                    ["Children"] = detail.Children,
                }
            }
        };
    }

    private static JsonObject BuildFormMultiSection(
        Action<SectionBuilder> buildHeader,
        Action<SectionBuilder> buildDetail)
    {
        var header = new SectionBuilder();
        buildHeader(header);
        var detail = new SectionBuilder();
        buildDetail(detail);

        return new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject { ["Width"] = "10080" },
            ["Children"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "Section",
                    ["Properties"] = new JsonObject { ["Height"] = "720", ["Name"] = "FormHeader" },
                    ["Children"] = header.Children,
                },
                new JsonObject
                {
                    ["Type"] = "Section",
                    ["Properties"] = new JsonObject { ["Height"] = "2160", ["Name"] = "Detail" },
                    ["Children"] = detail.Children,
                },
            }
        };
    }

    private static JsonObject MakeLabel(string name, string caption, int left, int top, int w, int h)
        => new()
        {
            ["Type"] = "Label",
            ["Properties"] = new JsonObject
            {
                ["Name"] = name,
                ["Caption"] = caption,
                ["Left"] = left.ToString(),
                ["Top"] = top.ToString(),
                ["Width"] = w.ToString(),
                ["Height"] = h.ToString(),
            },
            ["Children"] = new JsonArray(),
        };

    private static JsonObject MakeControl(string type, string name, int left, int top, int w, int h,
        string? controlSource = null, string? attachedLabel = null)
    {
        var props = new JsonObject
        {
            ["Name"] = name,
            ["Left"] = left.ToString(),
            ["Top"] = top.ToString(),
            ["Width"] = w.ToString(),
            ["Height"] = h.ToString(),
        };
        if (controlSource is not null) props["ControlSource"] = controlSource;

        var children = new JsonArray();
        if (attachedLabel is not null)
        {
            children.Add(MakeLabel(name + "_Label", attachedLabel, left, top, w, h));
        }

        return new JsonObject
        {
            ["Type"] = type,
            ["Properties"] = props,
            ["Children"] = children,
        };
    }

    // ================== pipeline plumbing ==================

    private static string EnrichAndWrite(JsonObject form)
    {
        var inFile = Path.GetTempFileName();
        var outFile = Path.GetTempFileName();
        File.WriteAllText(inFile, form.ToJsonString());
        LabelResolver.Forms.EnrichFile(inFile, outFile);
        File.Delete(inFile);
        return outFile;
    }

    private static JsonObject ReadForm(string file)
    {
        var obj = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        File.Delete(file);
        return obj;
    }

    private static JsonObject FindControl(JsonObject root, string name)
    {
        JsonObject? found = null;
        Walk(root);
        Assert.NotNull(found);
        return found!;

        void Walk(JsonObject n)
        {
            if (n["Properties"] is JsonObject p
                && p["Name"] is JsonValue v
                && v.TryGetValue<string>(out var s)
                && s == name
                && n["Type"]?.GetValue<string>() != "Label")
            {
                found = n;
                return;
            }
            if (n["Children"] is JsonArray arr)
            {
                foreach (var c in arr)
                {
                    if (c is JsonObject co && found is null) Walk(co);
                }
            }
        }
    }

    private static void AssertLabel(JsonObject control, string source, string? text)
    {
        Assert.True(control.ContainsKey("Label"), "Control has no Label key");
        var label = control["Label"]!.AsObject();
        Assert.Equal(source, label["source"]!.GetValue<string>());
        if (text is null)
        {
            Assert.False(label.ContainsKey("text"),
                $"Expected no text but found: {label["text"]}");
        }
        else
        {
            Assert.True(label.ContainsKey("text"), "Expected a text value but key was missing");
            Assert.Equal(text, label["text"]!.GetValue<string>());
        }
    }
}
