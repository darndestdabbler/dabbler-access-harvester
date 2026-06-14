using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Pipeline.Enrichment;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

/// <summary>
/// Hermetic pins for <see cref="MacroChainBuilder"/> (012 R2-1 / DW-9):
/// macro-IR Begin nodes become MacroActions with documented argument names
/// for known actions, deterministic positional fallback for unknown ones,
/// scalar/array/absent argument handling, and RunCode → CallsProcedureName.
/// </summary>
public sealed class MacroChainBuilderTests
{
    private static JsonNode Doc(params JsonObject[] beginNodes)
    {
        var children = new JsonArray();
        foreach (var b in beginNodes) children.Add(b);
        return new JsonObject { ["Type"] = "DocumentRoot", ["Children"] = children };
    }

    private static JsonObject Begin(string action, JsonNode? argument, string? macroName = null)
    {
        var props = new JsonObject { ["Action"] = action };
        if (macroName is not null) props["MacroName"] = macroName;
        if (argument is not null) props["Argument"] = argument;
        return new JsonObject { ["Type"] = "Begin", ["Properties"] = props, ["Children"] = new JsonArray() };
    }

    [Fact]
    public void OpenForm_array_args_map_to_documented_names()
    {
        var doc = Doc(Begin("OpenForm",
            new JsonArray { "frmClientSearch", "0", "", "", "-1", "0" }, "OpenClients"));
        var chain = MacroChainBuilder.FromDocument("mcrSwitchboardActions", doc);

        Assert.Equal("mcrSwitchboardActions", chain.Name);
        var action = Assert.Single(chain.Actions);
        Assert.Equal("OpenForm", action.ActionName);
        Assert.Equal("frmClientSearch", action.Arguments["FormName"]);
        Assert.Equal("0", action.Arguments["View"]);
        Assert.Equal("-1", action.Arguments["DataMode"]);
        Assert.Equal("0", action.Arguments["WindowMode"]);
        Assert.Null(action.CallsProcedureName);
    }

    [Fact]
    public void RunCode_sets_CallsProcedureName_stripped_of_parens()
    {
        var doc = Doc(Begin("RunCode", new JsonArray { "Initialize()" }));
        var action = Assert.Single(MacroChainBuilder.FromDocument("AutoExec", doc).Actions);
        Assert.Equal("Initialize()", action.Arguments["FunctionName"]); // raw arg preserved
        Assert.Equal("Initialize", action.CallsProcedureName);          // parens stripped for the proc name

    }

    [Fact]
    public void Scalar_argument_is_accepted()
    {
        var doc = Doc(Begin("RunCommand", "97"));
        var action = Assert.Single(MacroChainBuilder.FromDocument("AutoKeys", doc).Actions);
        Assert.Equal("97", action.Arguments["Command"]);
    }

    [Fact]
    public void Unknown_action_falls_back_to_positional_keys()
    {
        var doc = Doc(Begin("FrobnicateWidget", new JsonArray { "a", "b" }));
        var action = Assert.Single(MacroChainBuilder.FromDocument("mcrX", doc).Actions);
        Assert.Equal("a", action.Arguments["Argument1"]);
        Assert.Equal("b", action.Arguments["Argument2"]);
    }

    [Fact]
    public void Action_without_argument_yields_empty_argument_map()
    {
        var doc = Doc(Begin("Requery", argument: null));
        var action = Assert.Single(MacroChainBuilder.FromDocument("m", doc).Actions);
        Assert.Equal("Requery", action.ActionName);
        Assert.Empty(action.Arguments);
    }

    [Fact]
    public void Comment_only_begin_nodes_are_skipped()
    {
        // _AXL comment chunks carry no Action and must not become actions.
        var commentNode = new JsonObject
        {
            ["Type"] = "Begin",
            ["Properties"] = new JsonObject { ["Comment"] = "_AXL:<UserInterfaceMacro>..." },
            ["Children"] = new JsonArray(),
        };
        var doc = Doc(Begin("OpenForm", new JsonArray { "frmA" }), commentNode);
        var chain = MacroChainBuilder.FromDocument("m", doc);
        Assert.Single(chain.Actions);
    }
}
