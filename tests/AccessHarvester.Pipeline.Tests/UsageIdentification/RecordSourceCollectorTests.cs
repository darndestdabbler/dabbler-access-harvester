using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

public class RecordSourceCollectorTests
{
    private static IrDatabase Db(IrFormOrReport[] forms, IrFormOrReport[]? reports = null) =>
        new()
        {
            Name = "Test",
            Forms = forms,
            Reports = reports ?? [],
            Queries = [],
            Tables = [],
            Relationships = [],
            Macros = [],
            Modules = [],
        };

    private static IrFormOrReport Form(string name, string? recordSource) =>
        new()
        {
            Name = name,
            ObjectType = "Form",
            RecordSource = recordSource,
            VbaCode = null,
            Controls = [],
            MacroActions = [],
        };

    [Fact]
    public void Collect_Returns_Empty_When_No_Forms_Or_Reports()
    {
        var result = RecordSourceCollector.Collect(Db([]));
        Assert.Empty(result);
    }

    [Fact]
    public void Collect_Returns_Named_RecordSource_For_Form()
    {
        var db = Db([Form("frmClients", "tblClients")]);
        var result = RecordSourceCollector.Collect(db);
        Assert.True(result.TryGetValue("frmClients", out var rs));
        Assert.Equal("tblClients", rs);
    }

    [Fact]
    public void Collect_Excludes_Inline_SQL_RecordSource()
    {
        var db = Db([Form("frmDynamic", "SELECT * FROM tblClients")]);
        var result = RecordSourceCollector.Collect(db);
        Assert.False(result.ContainsKey("frmDynamic"));
    }

    [Fact]
    public void Collect_Excludes_Forms_With_Null_RecordSource()
    {
        var db = Db([Form("frmNoSource", null)]);
        var result = RecordSourceCollector.Collect(db);
        Assert.Empty(result);
    }

    [Fact]
    public void Collect_Excludes_Forms_With_Whitespace_RecordSource()
    {
        var db = Db([Form("frmBlank", "   ")]);
        var result = RecordSourceCollector.Collect(db);
        Assert.Empty(result);
    }

    [Fact]
    public void Collect_Includes_Query_As_RecordSource()
    {
        var db = Db([Form("frmReport", "qryClientList")]);
        var result = RecordSourceCollector.Collect(db);
        Assert.Equal("qryClientList", result["frmReport"]);
    }

    [Fact]
    public void Collect_Reports_RecordSource_Included()
    {
        var db = Db(
            [],
            [new IrFormOrReport
            {
                Name = "rptClients",
                ObjectType = "Report",
                RecordSource = "tblClients",
                VbaCode = null,
                Controls = [],
                MacroActions = [],
            }]);
        var result = RecordSourceCollector.Collect(db);
        Assert.Equal("tblClients", result["rptClients"]);
    }

    [Fact]
    public void Collect_Multiple_Forms_All_Included()
    {
        var db = Db([
            Form("frmA", "tblA"),
            Form("frmB", "qryB"),
            Form("frmC", null),
        ]);
        var result = RecordSourceCollector.Collect(db);
        Assert.Equal(2, result.Count);
        Assert.Equal("tblA", result["frmA"]);
        Assert.Equal("qryB", result["frmB"]);
    }
}
