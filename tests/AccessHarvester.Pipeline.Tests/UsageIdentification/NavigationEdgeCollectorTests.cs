using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

public class NavigationEdgeCollectorTests
{
    private static IrDatabase EmptyDb() => new()
    {
        Name = "Test",
        Forms = [], Reports = [], Queries = [], Tables = [],
        Relationships = [], Macros = [], Modules = [],
    };

    private static IrDatabase DbWithMacro(string macroName, IrMacroAction[] actions) => new()
    {
        Name = "Test",
        Forms = [], Reports = [], Queries = [], Tables = [],
        Relationships = [],
        Macros = [new IrMacro { Name = macroName, Actions = actions }],
        Modules = [],
    };

    private static IrDatabase DbWithFormVba(string formName, string vbaCode) => new()
    {
        Name = "Test",
        Forms = [new IrFormOrReport
        {
            Name = formName,
            ObjectType = "Form",
            VbaCode = vbaCode,
            RecordSource = null,
            Controls = [],
            MacroActions = [],
        }],
        Reports = [], Queries = [], Tables = [], Relationships = [], Macros = [], Modules = [],
    };

    [Fact]
    public void Collect_Returns_Empty_For_Empty_Database()
    {
        var edges = NavigationEdgeCollector.Collect(EmptyDb());
        Assert.Empty(edges);
    }

    [Fact]
    public void Collect_OpenForm_Macro_Action_Produces_Edge()
    {
        var db = DbWithMacro("mcrSwitchboard", [
            new IrMacroAction { Action = "OpenForm", Arguments = ["frmClientSearch", "0"] }
        ]);
        var edges = NavigationEdgeCollector.Collect(db);
        Assert.Single(edges);
        Assert.Equal("mcrSwitchboard", edges[0].Source);
        Assert.Equal("frmClientSearch", edges[0].Target);
        Assert.Equal("OpenForm", edges[0].EdgeType);
    }

    [Fact]
    public void Collect_OpenReport_Macro_Action_Produces_Edge()
    {
        var db = DbWithMacro("mcrReports", [
            new IrMacroAction { Action = "OpenReport", Arguments = ["rptClients", "1"] }
        ]);
        var edges = NavigationEdgeCollector.Collect(db);
        Assert.Single(edges);
        Assert.Equal("OpenReport", edges[0].EdgeType);
        Assert.Equal("rptClients", edges[0].Target);
    }

    [Fact]
    public void Collect_NonNavigation_Macro_Actions_Are_Ignored()
    {
        var db = DbWithMacro("mcrValidation", [
            new IrMacroAction { Action = "SetValue", Arguments = ["[Field]", "Value"] }
        ]);
        var edges = NavigationEdgeCollector.Collect(db);
        Assert.Empty(edges);
    }

    [Fact]
    public void Collect_VBA_DoCmd_OpenForm_Produces_Edge()
    {
        var db = DbWithFormVba("frmSwitchboard",
            "DoCmd.OpenForm \"frmClientDetail\", acNormal");
        var edges = NavigationEdgeCollector.Collect(db);
        Assert.Single(edges);
        Assert.Equal("frmSwitchboard", edges[0].Source);
        Assert.Equal("frmClientDetail", edges[0].Target);
        Assert.Equal("OpenForm", edges[0].EdgeType);
    }

    [Fact]
    public void Collect_VBA_DoCmd_OpenReport_Produces_Edge()
    {
        var db = DbWithFormVba("frmClientDetail",
            "DoCmd.OpenReport \"rptClientProfile\", acViewPreview");
        var edges = NavigationEdgeCollector.Collect(db);
        Assert.Single(edges);
        Assert.Equal("OpenReport", edges[0].EdgeType);
        Assert.Equal("rptClientProfile", edges[0].Target);
    }

    [Fact]
    public void Collect_Macro_Action_With_Empty_Argument_Not_Added()
    {
        var db = DbWithMacro("mcrBad", [
            new IrMacroAction { Action = "OpenForm", Arguments = [] }
        ]);
        var edges = NavigationEdgeCollector.Collect(db);
        Assert.Empty(edges);
    }

    [Fact]
    public void Collect_Multiple_Actions_Produces_Multiple_Edges()
    {
        var db = DbWithMacro("mcrMenu", [
            new IrMacroAction { Action = "OpenForm",   Arguments = ["frmA"] },
            new IrMacroAction { Action = "OpenReport", Arguments = ["rptB"] },
        ]);
        var edges = NavigationEdgeCollector.Collect(db);
        Assert.Equal(2, edges.Count);
    }
}
