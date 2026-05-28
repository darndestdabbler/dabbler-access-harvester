using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

public class VbaReferenceCollectorTests
{
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
        Reports = [], Queries = [], Tables = [], Relationships = [], Macros = [],
        Modules = [],
    };

    [Fact]
    public void Collect_Returns_Empty_For_No_VBA()
    {
        var db = new IrDatabase
        {
            Name = "Test",
            Forms = [new IrFormOrReport
            {
                Name = "frmEmpty",
                ObjectType = "Form",
                VbaCode = null,
                RecordSource = null,
                Controls = [],
                MacroActions = [],
            }],
            Reports = [], Queries = [], Tables = [], Relationships = [], Macros = [], Modules = [],
        };
        var refs = VbaReferenceCollector.Collect(db);
        Assert.Empty(refs);
    }

    [Fact]
    public void Collect_DCount_Extracts_Table_Name()
    {
        var db = DbWithFormVba("frmClients",
            "If DCount(\"*\", \"tblClientBudgets\", \"ClientID=1\") > 0 Then");
        var refs = VbaReferenceCollector.Collect(db);
        Assert.Single(refs);
        Assert.Equal("frmClients", refs[0].SourceObject);
        Assert.Equal("tblClientBudgets", refs[0].ReferencedObject);
    }

    [Fact]
    public void Collect_DLookup_Extracts_Table_Name()
    {
        var db = DbWithFormVba("frmX",
            "x = DLookup(\"Field\", \"tblLookup\", \"ID=1\")");
        var refs = VbaReferenceCollector.Collect(db);
        Assert.Contains(refs, r => r.ReferencedObject == "tblLookup");
    }

    [Fact]
    public void Collect_OpenRecordset_Extracts_Table_Name()
    {
        var db = DbWithFormVba("frmData",
            "Set rs = CurrentDb.OpenRecordset(\"tblClients\", dbOpenSnapshot)");
        var refs = VbaReferenceCollector.Collect(db);
        Assert.Contains(refs, r => r.ReferencedObject == "tblClients");
    }

    [Fact]
    public void Collect_OpenRecordset_With_SQL_Excluded()
    {
        var db = DbWithFormVba("frmSql",
            "Set rs = CurrentDb.OpenRecordset(\"SELECT * FROM tblClients\")");
        var refs = VbaReferenceCollector.Collect(db);
        Assert.Empty(refs);
    }

    [Fact]
    public void Collect_Multiple_DFunction_Calls_All_Extracted()
    {
        var vba = "DCount(\"*\", \"tblA\", \"\") + DSum(\"F\", \"tblB\", \"\")";
        var db = DbWithFormVba("frmMulti", vba);
        var refs = VbaReferenceCollector.Collect(db);
        var names = refs.Select(r => r.ReferencedObject).ToList();
        Assert.Contains("tblA", names);
        Assert.Contains("tblB", names);
    }
}
