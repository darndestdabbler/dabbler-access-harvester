using System.Text.Json.Nodes;
using AccessHarvester.Core.Enrichment;
using AccessHarvester.Core.Enrichment.Macros;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic tests for <see cref="MacroXmlParser"/> (XML reassembly + parse)
/// and end-to-end <see cref="MacroActionsEnricher"/> behavior on hand-authored
/// kb-shaped JSON. Integration tests against real Access live in
/// <see cref="SyntheticDbIntegrationTests"/>.
/// </summary>
public class MacroXmlParserTests
{
    // ============================================================
    // MacroXmlParser.ParseToActions — core flatten tests
    // ============================================================

    [Fact]
    public void ParseToActions_SingleAction_NoCondition()
    {
        var xml =
            "<UserInterfaceMacro xmlns=\"http://schemas.microsoft.com/office/accessservices/2009/11/application\">" +
            "<Statements>" +
            "<Action Name=\"Beep\"/>" +
            "</Statements>" +
            "</UserInterfaceMacro>";

        var actions = MacroXmlParser.ParseToActions(xml);

        Assert.Single(actions);
        var a = actions[0]!.AsObject();
        Assert.Equal("Beep", a["Action"]!.GetValue<string>());
        Assert.Empty(a["Arguments"]!.AsObject());
        Assert.Null(a["Condition"]);
        Assert.Null(a["ConditionBranch"]);
        Assert.Null(a["InferredFunctionality"]);
    }

    [Fact]
    public void ParseToActions_NamedArguments_PopulatesArgumentsDict()
    {
        var xml =
            "<UserInterfaceMacro><Statements>" +
            "<Action Name=\"OpenForm\">" +
            "  <Argument Name=\"FormName\">frmStartup</Argument>" +
            "  <Argument Name=\"View\">0</Argument>" +
            "</Action>" +
            "</Statements></UserInterfaceMacro>";

        var actions = MacroXmlParser.ParseToActions(xml);

        var args = actions[0]!["Arguments"]!.AsObject();
        Assert.Equal("frmStartup", args["FormName"]!.GetValue<string>());
        Assert.Equal("0", args["View"]!.GetValue<string>());
    }

    [Fact]
    public void ParseToActions_ConditionalIfElseIfElse_PopulatesBranchAndCondition()
    {
        var xml =
            "<UserInterfaceMacro><Statements>" +
            "<ConditionalBlock>" +
            "  <If><Condition>X = 1</Condition><Statements>" +
            "    <Action Name=\"A\"/>" +
            "  </Statements></If>" +
            "  <ElseIf><Condition>X = 2</Condition><Statements>" +
            "    <Action Name=\"B\"/>" +
            "  </Statements></ElseIf>" +
            "  <Else><Statements>" +
            "    <Action Name=\"C\"/>" +
            "  </Statements></Else>" +
            "</ConditionalBlock>" +
            "</Statements></UserInterfaceMacro>";

        var actions = MacroXmlParser.ParseToActions(xml);

        Assert.Equal(3, actions.Count);

        Assert.Equal("A", actions[0]!["Action"]!.GetValue<string>());
        Assert.Equal("If", actions[0]!["ConditionBranch"]!.GetValue<string>());
        Assert.Equal("X = 1", actions[0]!["Condition"]!.GetValue<string>());

        Assert.Equal("B", actions[1]!["Action"]!.GetValue<string>());
        Assert.Equal("ElseIf", actions[1]!["ConditionBranch"]!.GetValue<string>());
        Assert.Equal("NOT (X = 1) AND X = 2", actions[1]!["Condition"]!.GetValue<string>());

        Assert.Equal("C", actions[2]!["Action"]!.GetValue<string>());
        Assert.Equal("Else", actions[2]!["ConditionBranch"]!.GetValue<string>());
        Assert.Equal("NOT (X = 1) AND NOT (X = 2)", actions[2]!["Condition"]!.GetValue<string>());
    }

    [Fact]
    public void ParseToActions_NestedStatementsInIf_AllInheritSameCondition()
    {
        var xml =
            "<UserInterfaceMacro><Statements>" +
            "<ConditionalBlock>" +
            "  <If><Condition>Y</Condition><Statements>" +
            "    <Action Name=\"A\"/>" +
            "    <Action Name=\"B\"/>" +
            "    <Action Name=\"C\"/>" +
            "  </Statements></If>" +
            "</ConditionalBlock>" +
            "</Statements></UserInterfaceMacro>";

        var actions = MacroXmlParser.ParseToActions(xml);

        Assert.Equal(3, actions.Count);
        Assert.All(actions, a =>
        {
            Assert.Equal("Y", a!["Condition"]!.GetValue<string>());
            Assert.Equal("If", a["ConditionBranch"]!.GetValue<string>());
        });
    }

    [Fact]
    public void ParseToActions_MalformedXml_EmitsParseErrorSentinel()
    {
        var actions = MacroXmlParser.ParseToActions("<UserInterfaceMacro><Stat");

        Assert.Single(actions);
        var a = actions[0]!.AsObject();
        Assert.Equal("ParseError", a["Action"]!.GetValue<string>());
        Assert.NotNull(a["Arguments"]!["message"]);
    }

    [Fact]
    public void ParseToActions_FreeFormCommentInsideStatements_PreservedAsCommentEntry()
    {
        var xml =
            "<UserInterfaceMacro><Statements>" +
            "<Comment>note about A</Comment>" +
            "<Action Name=\"A\"/>" +
            "</Statements></UserInterfaceMacro>";

        var actions = MacroXmlParser.ParseToActions(xml);

        Assert.Equal(2, actions.Count);
        Assert.Equal("Comment", actions[0]!["Action"]!.GetValue<string>());
        Assert.Equal("note about A", actions[0]!["Arguments"]!["Text"]!.GetValue<string>());
        Assert.Equal("A", actions[1]!["Action"]!.GetValue<string>());
    }

    // ============================================================
    // MacroXmlParser.AssembleAxlChunks — chunking
    // ============================================================

    [Fact]
    public void AssembleAxlChunks_ConcatenatesInDocumentOrder_StripsPrefix()
    {
        var root = new JsonObject
        {
            ["Children"] = new JsonArray
            {
                BeginNodeWithComment("_AXL:<UserInterfaceMacro><State"),
                BeginNodeWithComment("_AXL:ments><Action Name=\"X\"/></St"),
                BeginNodeWithComment("_AXL:atements></UserInterfaceMacro>"),
            },
        };

        string? assembled = MacroXmlParser.AssembleAxlChunks(root);

        Assert.Equal(
            "<UserInterfaceMacro><Statements><Action Name=\"X\"/></Statements></UserInterfaceMacro>",
            assembled);
    }

    [Fact]
    public void AssembleAxlChunks_NoChunks_ReturnsNull()
    {
        var root = new JsonObject { ["Children"] = new JsonArray() };
        Assert.Null(MacroXmlParser.AssembleAxlChunks(root));
    }

    // ============================================================
    // MacroActionsEnricher — standalone (Macros category)
    // ============================================================

    [Fact]
    public void StandaloneMacro_AssembledFromChunks_EmitsActionsAtRoot()
    {
        var root = new JsonObject
        {
            ["Type"] = "DocumentRoot",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray
            {
                BeginNodeWithComment("_AXL:<UserInterfaceMacro><Statemen"),
                BeginNodeWithComment("_AXL:ts><Action Name=\"OpenForm\"><Argument Name=\"FormName\">frmS"),
                BeginNodeWithComment("_AXL:tartup</Argument></Action></Statements></UserInterfaceMacro>"),
            },
        };

        var (inFile, outFile) = WriteAndRunStandalone(root);
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            var actions = doc["Actions"]!.AsArray();
            Assert.Single(actions);
            Assert.Equal("OpenForm", actions[0]!["Action"]!.GetValue<string>());
            Assert.Equal("frmStartup", actions[0]!["Arguments"]!["FormName"]!.GetValue<string>());
            Assert.Null(actions[0]!["InferredFunctionality"]);
            Assert.True(doc.ContainsKey("InferredFunctionality"));
            Assert.Null(doc["InferredFunctionality"]);
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    [Fact]
    public void StandaloneMacro_PrefersXml_OverLegacyArgumentList()
    {
        // The kb tree contains both: a Begin block with the legacy
        // (Argument: ["A","B","C"]) shape plus _AXL: chunks with the
        // canonical XML representation. The enricher should pull from XML.
        var legacyBlock = new JsonObject
        {
            ["Type"] = "Begin",
            ["Properties"] = new JsonObject
            {
                ["Action"] = "OpenForm",
                ["Argument"] = new JsonArray("A", "B", "C"),
            },
            ["Children"] = new JsonArray(),
        };
        var root = new JsonObject
        {
            ["Type"] = "DocumentRoot",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray
            {
                legacyBlock,
                BeginNodeWithComment(
                    "_AXL:<UserInterfaceMacro><Statements><Action Name=\"OpenForm\">" +
                    "<Argument Name=\"FormName\">frmCanonical</Argument>" +
                    "</Action></Statements></UserInterfaceMacro>"),
            },
        };

        var (inFile, outFile) = WriteAndRunStandalone(root);
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            var actions = doc["Actions"]!.AsArray();
            Assert.Single(actions);
            // Named, canonical FormName from the XML — not the positional list.
            Assert.Equal("frmCanonical",
                actions[0]!["Arguments"]!["FormName"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    [Fact]
    public void StandaloneMacro_NoChunks_EmitsEmptyActionsArray()
    {
        var root = new JsonObject
        {
            ["Type"] = "DocumentRoot",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray(),
        };

        var (inFile, outFile) = WriteAndRunStandalone(root);
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            Assert.Empty(doc["Actions"]!.AsArray());
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    // ============================================================
    // MacroActionsEnricher — embedded (Forms / Reports)
    // ============================================================

    [Fact]
    public void EmbeddedMacro_OnControlEvent_PopulatesEventMacrosBlock()
    {
        // CommandButton with OnClick set + child _AXL: chunks describing the
        // OpenForm action.
        var control = new JsonObject
        {
            ["Type"] = "CommandButton",
            ["Properties"] = new JsonObject
            {
                ["Name"] = "cmdGo",
                ["OnClick"] = "[Embedded Macro]",
                ["OnClickEmMacro"] = "Version =1\nBegin\nAction =\"CloseWindow\"\nEnd",
            },
            ["Children"] = new JsonArray
            {
                BeginNodeWithComment(
                    "_AXL:<UserInterfaceMacro For=\"cmdGo\"><Statements>" +
                    "<Action Name=\"CloseWindow\">" +
                    "<Argument Name=\"ObjectType\">acForm</Argument>" +
                    "<Argument Name=\"ObjectName\">frmCustomers</Argument>" +
                    "</Action></Statements></UserInterfaceMacro>"),
            },
        };
        var form = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray(control),
        };

        var (inFile, outFile) = WriteAndRunEmbedded(form);
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            var ctrl = doc["Children"]!.AsArray()[0]!.AsObject();
            var em = ctrl["EventMacros"]!.AsObject();
            Assert.True(em.ContainsKey("OnClick"));

            var onClick = em["OnClick"]!.AsObject();
            var actions = onClick["Actions"]!.AsArray();
            Assert.Single(actions);
            Assert.Equal("CloseWindow", actions[0]!["Action"]!.GetValue<string>());
            Assert.Equal("acForm",
                actions[0]!["Arguments"]!["ObjectType"]!.GetValue<string>());
            Assert.Null(actions[0]!["InferredFunctionality"]);
            Assert.Null(onClick["InferredFunctionality"]);
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    [Fact]
    public void EmbeddedMacro_MultipleEvents_OneBlockPerEvent()
    {
        // Two events on one control → two UserInterfaceMacro elements in the
        // assembled XML (matched positionally to event names in property order).
        var control = new JsonObject
        {
            ["Type"] = "TextBox",
            ["Properties"] = new JsonObject
            {
                ["OnClickEmMacro"] = "Version =1\n",
                ["AfterUpdateEmMacro"] = "Version =1\n",
            },
            ["Children"] = new JsonArray
            {
                BeginNodeWithComment(
                    "_AXL:<UserInterfaceMacro><Statements>" +
                    "<Action Name=\"Beep\"/>" +
                    "</Statements></UserInterfaceMacro>" +
                    "<UserInterfaceMacro><Statements>" +
                    "<Action Name=\"RunMacro\"/>" +
                    "</Statements></UserInterfaceMacro>"),
            },
        };
        var form = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray(control),
        };

        var (inFile, outFile) = WriteAndRunEmbedded(form);
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            var ctrl = doc["Children"]!.AsArray()[0]!.AsObject();
            var em = ctrl["EventMacros"]!.AsObject();
            Assert.Equal(2, em.Count);
            Assert.True(em.ContainsKey("OnClick"));
            Assert.True(em.ContainsKey("AfterUpdate"));
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    [Fact]
    public void NoEmMacroProperties_NoEventMacrosBlock()
    {
        // Control with normal data binding, no events — absence is the signal,
        // we don't emit an explicit "no events" marker.
        var control = new JsonObject
        {
            ["Type"] = "TextBox",
            ["Properties"] = new JsonObject
            {
                ["Name"] = "txtName",
                ["ControlSource"] = "FirstName",
            },
            ["Children"] = new JsonArray(),
        };
        var form = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray(control),
        };

        var (inFile, outFile) = WriteAndRunEmbedded(form);
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
            var ctrl = doc["Children"]!.AsArray()[0]!.AsObject();
            Assert.False(ctrl.ContainsKey("EventMacros"));
        }
        finally
        {
            File.Delete(inFile);
            File.Delete(outFile);
        }
    }

    // ============================================================
    // helpers
    // ============================================================

    private static JsonObject BeginNodeWithComment(string comment) => new()
    {
        ["Type"] = "Begin",
        ["Properties"] = new JsonObject { ["Comment"] = comment },
        ["Children"] = new JsonArray(),
    };

    private static (string inFile, string outFile) WriteAndRunStandalone(JsonObject root)
    {
        var inFile = Path.GetTempFileName();
        var outFile = Path.GetTempFileName();
        File.WriteAllText(inFile, root.ToJsonString());
        MacroActionsEnricher.Macros.EnrichFile(inFile, outFile);
        return (inFile, outFile);
    }

    private static (string inFile, string outFile) WriteAndRunEmbedded(JsonObject form)
    {
        var inFile = Path.GetTempFileName();
        var outFile = Path.GetTempFileName();
        File.WriteAllText(inFile, form.ToJsonString());
        MacroActionsEnricher.Forms.EnrichFile(inFile, outFile);
        return (inFile, outFile);
    }
}
