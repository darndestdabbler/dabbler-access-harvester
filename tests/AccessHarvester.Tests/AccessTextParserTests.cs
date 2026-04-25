using AccessHarvester.Core.Translation;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic tests for the SaveAsText parser bug-fixes that landed alongside
/// <see cref="AccessHarvester.Core.Enrichment.MacroActionsEnricher"/>: repeated-
/// key accumulation, quoted-string continuation, and the "return root" mode
/// the Macros translator uses to keep all top-level Begin children.
/// </summary>
public class AccessTextParserTests
{
    [Fact]
    public void RepeatedKey_Accumulates_IntoListInDocumentOrder()
    {
        // SaveAsText writes one Argument line per positional argument; the
        // pre-fix parser overwrote each with the next, leaving only the last
        // value. Now repeated keys accumulate.
        var lines = new[]
        {
            "Begin",
            "    Action =\"OpenForm\"",
            "    Argument =\"frmStartup\"",
            "    Argument =\"0\"",
            "    Argument =\"\"",
            "    Argument =\"-1\"",
            "End",
        };

        var root = AccessTextParser.ParseBlockFormat(lines, returnRoot: true);

        Assert.Single(root.Children);
        var begin = root.Children[0];
        Assert.Equal("OpenForm", Assert.IsType<string>(begin.Properties["Action"]));

        var args = Assert.IsType<List<object>>(begin.Properties["Argument"]);
        Assert.Equal(new object[] { "frmStartup", "0", "", "-1" }, args);
    }

    [Fact]
    public void QuotedStringContinuation_AppendsToPreviousValue()
    {
        // Long string property values are split across lines, each line a
        // complete quoted string. They concatenate with no separator.
        var lines = new[]
        {
            "Begin",
            "    Comment =\"_AXL:<UserI\"",
            "        \"nterfaceMacro xmln\"",
            "        \"s=\\\"http://x\\\">\"",
            "End",
        };

        var root = AccessTextParser.ParseBlockFormat(lines, returnRoot: true);

        var begin = root.Children[0];
        var comment = Assert.IsType<string>(begin.Properties["Comment"]);
        // Embedded \" sequences survive — we strip the outermost quotes only.
        Assert.Equal("_AXL:<UserInterfaceMacro xmlns=\\\"http://x\\\">", comment);
    }

    [Fact]
    public void ReturnRoot_KeepsAllTopLevelBeginBlocks()
    {
        // Standalone macros are a flat sequence of root-level Begin blocks;
        // the default mode (which Forms/Reports use) returns only the first
        // child via FirstOrDefault. Returning root preserves them all.
        var lines = new[]
        {
            "Version =196611",
            "Begin",
            "    Action =\"OpenForm\"",
            "End",
            "Begin",
            "    Action =\"RunCode\"",
            "End",
            "Begin",
            "    Comment =\"_AXL:<xml/>\"",
            "End",
        };

        var root = AccessTextParser.ParseBlockFormat(lines, returnRoot: true);

        Assert.Equal("DocumentRoot", root.Type);
        Assert.Equal("196611", root.Properties["Version"]);
        Assert.Equal(3, root.Children.Count);
        Assert.Equal("OpenForm", root.Children[0].Properties["Action"]);
        Assert.Equal("RunCode", root.Children[1].Properties["Action"]);
        Assert.Equal("_AXL:<xml/>", root.Children[2].Properties["Comment"]);
    }

    [Fact]
    public void DefaultMode_ReturnsFirstChild_BackwardsCompatibleWithFormsAndReports()
    {
        // Sanity check that we didn't regress the existing Form/Report
        // behavior — they still expect FirstOrDefault.
        var lines = new[]
        {
            "Version =21",
            "Begin Form",
            "    Width =\"10080\"",
            "End",
        };

        var node = AccessTextParser.ParseBlockFormat(lines);

        Assert.Equal("Form", node.Type);
        Assert.Equal("10080", node.Properties["Width"]);
    }

    [Fact]
    public void MultilineValueRepeated_StillAccumulates()
    {
        // PropName = Begin … End multi-line values also accumulate when
        // repeated. Rare but we apply the same rule for consistency.
        var lines = new[]
        {
            "Begin",
            "    NameMap =Begin",
            "    deadbeef",
            "    End",
            "    NameMap =Begin",
            "    cafef00d",
            "    End",
            "End",
        };

        var root = AccessTextParser.ParseBlockFormat(lines, returnRoot: true);
        var begin = root.Children[0];

        var values = Assert.IsType<List<object>>(begin.Properties["NameMap"]);
        Assert.Equal(2, values.Count);
        Assert.Equal("deadbeef", values[0]);
        Assert.Equal("cafef00d", values[1]);
    }
}
