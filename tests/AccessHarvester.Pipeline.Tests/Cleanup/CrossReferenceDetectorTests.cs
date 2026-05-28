using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Cleanup;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Cleanup;

public class CrossReferenceDetectorTests
{
    private static IrDatabase Db(
        IrFormOrReport[]? forms = null,
        IrFormOrReport[]? reports = null,
        IrQuery[]? queries = null,
        IrTable[]? tables = null,
        IrRelationship[]? relationships = null,
        IrMacro[]? macros = null,
        IrModule[]? modules = null)
        => new()
        {
            Name = "Test",
            Forms = forms ?? [],
            Reports = reports ?? [],
            Queries = queries ?? [],
            Tables = tables ?? [],
            Relationships = relationships ?? [],
            Macros = macros ?? [],
            Modules = modules ?? [],
        };

    private static IrFormOrReport Form(string name, string? recordSource = null,
        string? vbaCode = null, IrControl[]? controls = null, IrMacroAction[]? macroActions = null,
        IReadOnlyDictionary<string, string>? properties = null)
        => new()
        {
            Name = name,
            ObjectType = "Form",
            RecordSource = recordSource,
            VbaCode = vbaCode,
            Controls = controls ?? [],
            MacroActions = macroActions ?? [],
            Properties = properties ?? new Dictionary<string, string>(),
        };

    private static IrTable Table(string name) => new() { Name = name, ColumnCount = 1 };
    private static IrQuery Query(string name, string? sql = null) =>
        new() { Name = name, QueryTypeCode = 0, Sql = sql };
    private static IrMacro Macro(string name, IrMacroAction[]? actions = null) =>
        new() { Name = name, Actions = actions ?? [] };
    private static IrModule Module(string name, string? vba = null) =>
        new() { Name = name, VbaCode = vba };

    private static bool IsUnreferenced(IrDatabase db, string type, string name)
        => CrossReferenceDetector.Detect(db).Any(r => r.ObjectType == type && r.ObjectName == name);

    [Fact]
    public void Table_Referenced_By_RecordSource_Is_Not_Unreferenced()
    {
        var db = Db(
            forms: [Form("frmClients", recordSource: "tblClients")],
            tables: [Table("tblClients")]);
        Assert.False(IsUnreferenced(db, "Table", "tblClients"));
    }

    [Fact]
    public void Table_Referenced_By_Inline_SQL_RecordSource_Is_Not_Unreferenced()
    {
        var db = Db(
            forms: [Form("frmClients", recordSource: "SELECT * FROM tblClients WHERE Active = True")],
            tables: [Table("tblClients")]);
        Assert.False(IsUnreferenced(db, "Table", "tblClients"));
    }

    [Fact]
    public void Table_Referenced_By_FK_Is_Not_Unreferenced()
    {
        var db = Db(
            tables: [Table("tblClients"), Table("tblOrders")],
            relationships: [new IrRelationship
            {
                Name = "rel_Client_Orders",
                ParentTable = "tblClients",
                ChildTable = "tblOrders",
            }]);
        Assert.False(IsUnreferenced(db, "Table", "tblClients"));
        Assert.False(IsUnreferenced(db, "Table", "tblOrders"));
    }

    [Fact]
    public void Table_With_Zero_Refs_Is_Unreferenced()
    {
        var db = Db(tables: [Table("tblOrphan")]);
        Assert.True(IsUnreferenced(db, "Table", "tblOrphan"));
    }

    [Fact]
    public void Query_Referenced_By_OpenQuery_Macro_Is_Not_Unreferenced()
    {
        var db = Db(
            queries: [Query("qryReportData")],
            macros: [Macro("mcrOpen", actions: [
                new IrMacroAction { Action = "OpenQuery", Arguments = ["qryReportData"] }
            ])]);
        Assert.False(IsUnreferenced(db, "Query", "qryReportData"));
    }

    [Fact]
    public void Query_Referenced_By_Another_Querys_FROM_Is_Not_Unreferenced()
    {
        var db = Db(
            queries: [
                Query("qryBase"),
                Query("qryOuter", sql: "SELECT * FROM qryBase"),
            ]);
        Assert.False(IsUnreferenced(db, "Query", "qryBase"));
    }

    [Fact]
    public void Form_Referenced_By_OpenForm_Macro_Is_Not_Unreferenced()
    {
        var db = Db(
            forms: [Form("frmAbout")],
            macros: [Macro("mcrOpenAbout", actions: [
                new IrMacroAction { Action = "OpenForm", Arguments = ["frmAbout"] }
            ])]);
        Assert.False(IsUnreferenced(db, "Form", "frmAbout"));
    }

    [Fact]
    public void Macro_Referenced_By_OnClick_Event_Property_Is_Not_Unreferenced()
    {
        var btnProps = new Dictionary<string, string>
        {
            ["Name"] = "btnRun",
            ["OnClick"] = "mcrRunStuff",
        };
        var control = new IrControl
        {
            Type = "CommandButton",
            Name = "btnRun",
            Properties = btnProps,
        };
        var db = Db(
            forms: [Form("frmHome", controls: [control])],
            macros: [Macro("mcrRunStuff")]);
        Assert.False(IsUnreferenced(db, "Macro", "mcrRunStuff"));
    }

    [Fact]
    public void Query_Self_Reference_In_Sql_Does_Not_Make_It_Referenced()
    {
        var db = Db(
            queries: [Query("qrySelf", sql: "SELECT * FROM qrySelf")]);
        Assert.True(IsUnreferenced(db, "Query", "qrySelf"));
    }

    [Fact]
    public void System_Objects_Are_Not_In_Unreferenced_Output()
    {
        var db = Db(tables: [Table("MSysObjects"), Table("USysAdmin")]);
        var output = CrossReferenceDetector.Detect(db);
        Assert.DoesNotContain(output, r => r.ObjectName.StartsWith("MSys"));
        Assert.DoesNotContain(output, r => r.ObjectName.StartsWith("USys"));
    }

    [Fact]
    public void CodeBehind_Modules_Are_Not_In_Unreferenced_Output()
    {
        var db = Db(modules: [Module("Form_frmClients"), Module("Report_rptOrders"), Module("modUtilities")]);
        var output = CrossReferenceDetector.Detect(db);
        Assert.DoesNotContain(output, r => r.ObjectName == "Form_frmClients");
        Assert.DoesNotContain(output, r => r.ObjectName == "Report_rptOrders");
        // modUtilities has no references and IS expected to appear
        Assert.Contains(output, r => r.ObjectName == "modUtilities");
    }

    [Fact]
    public void Signal_Reports_Checked_Reference_Types_For_Table()
    {
        var db = Db(tables: [Table("tblOrphan")]);
        var output = CrossReferenceDetector.Detect(db);
        var s = output.First(r => r.ObjectName == "tblOrphan").Signal;
        Assert.Contains("RecordSource", s.CheckedReferenceTypes);
        Assert.Contains("RowSource", s.CheckedReferenceTypes);
        Assert.Contains("VbaReference", s.CheckedReferenceTypes);
        Assert.Contains("QueryTableReference", s.CheckedReferenceTypes);
        Assert.Contains("FkRelationship", s.CheckedReferenceTypes);
    }

    // ── VBA DoCmd.Open* / RunMacro / DCount  (strategy §3.3 + Issue 2 fix) ─────

    [Fact]
    public void Form_Referenced_By_VBA_DoCmd_OpenForm_Is_Not_Unreferenced()
    {
        var db = Db(
            forms: [
                Form("frmHome", vbaCode: "Sub OpenIt()\r\n  DoCmd.OpenForm \"frmTarget\"\r\nEnd Sub"),
                Form("frmTarget"),
            ]);
        Assert.False(IsUnreferenced(db, "Form", "frmTarget"));
    }

    [Fact]
    public void Report_Referenced_By_VBA_DoCmd_OpenReport_Is_Not_Unreferenced()
    {
        var report = new IrFormOrReport
        {
            Name = "rptTarget",
            ObjectType = "Report",
            Controls = [],
            MacroActions = [],
        };
        var db = Db(
            forms: [Form("frmHome", vbaCode: "DoCmd.OpenReport \"rptTarget\", acViewPreview")],
            reports: [report]);
        Assert.False(IsUnreferenced(db, "Report", "rptTarget"));
    }

    [Fact]
    public void Macro_Referenced_By_VBA_DoCmd_RunMacro_Is_Not_Unreferenced()
    {
        var db = Db(
            forms: [Form("frmHome", vbaCode: "DoCmd.RunMacro \"mcrTarget\"")],
            macros: [Macro("mcrTarget")]);
        Assert.False(IsUnreferenced(db, "Macro", "mcrTarget"));
    }

    [Fact]
    public void Query_Referenced_By_VBA_DoCmd_OpenQuery_Is_Not_Unreferenced()
    {
        var db = Db(
            forms: [Form("frmHome", vbaCode: "DoCmd.OpenQuery \"qryTarget\"")],
            queries: [Query("qryTarget")]);
        Assert.False(IsUnreferenced(db, "Query", "qryTarget"));
    }

    [Fact]
    public void Table_Referenced_By_VBA_DCount_Is_Not_Unreferenced()
    {
        var db = Db(
            modules: [Module("modBiz", vba: "x = DCount(\"*\", \"tblWidgets\")")],
            tables: [Table("tblWidgets")]);
        Assert.False(IsUnreferenced(db, "Table", "tblWidgets"));
    }

    [Fact]
    public void Form_Referenced_By_Bare_Token_In_Another_Module_Is_Not_Unreferenced()
    {
        var db = Db(
            forms: [Form("frmTarget")],
            modules: [Module("modUtilities", vba: "Sub Go()\r\n  Forms!frmTarget.Visible = True\r\nEnd Sub")]);
        Assert.False(IsUnreferenced(db, "Form", "frmTarget"));
    }

    [Fact]
    public void Forms_Own_VBA_Does_Not_Count_As_Self_Reference()
    {
        // The form's own code-behind names the form itself in a string and as a token,
        // but per strategy §3.3 "Self-reference exclusion" this is not a reference.
        var db = Db(
            forms: [Form("frmLone", vbaCode: "Sub Form_Load()\r\n  Me.Caption = \"frmLone\"\r\n  Debug.Print frmLone\r\nEnd Sub")]);
        Assert.True(IsUnreferenced(db, "Form", "frmLone"));
    }

    // ── Event-property binding narrow scope (strategy §3.3.2 + Issue 3 fix) ───

    [Fact]
    public void Event_Property_Bare_Identifier_Matching_Query_Is_Not_NavigationEdge()
    {
        // A bare-identifier event-property value that matches a query name (but not a
        // macro name) does NOT count as a reference per strategy §3.3.2.
        var btnProps = new Dictionary<string, string>
        {
            ["Name"] = "btnRun",
            ["OnClick"] = "qryReports",
        };
        var control = new IrControl
        {
            Type = "CommandButton",
            Name = "btnRun",
            Properties = btnProps,
        };
        var db = Db(
            forms: [Form("frmHome", controls: [control])],
            queries: [Query("qryReports")]);
        // qryReports should be considered unreferenced — the event-property value
        // is not a recognised binding (Access stores [Event Procedure] for VBA
        // handlers, and only macro names count as bare-identifier bindings).
        Assert.True(IsUnreferenced(db, "Query", "qryReports"));
    }

    [Fact]
    public void Event_Property_Bare_Identifier_Matching_Form_Is_Not_NavigationEdge()
    {
        var btnProps = new Dictionary<string, string>
        {
            ["Name"] = "btnOpen",
            ["OnClick"] = "frmHelp",
        };
        var control = new IrControl
        {
            Type = "CommandButton",
            Name = "btnOpen",
            Properties = btnProps,
        };
        var db = Db(
            forms: [Form("frmHome", controls: [control]), Form("frmHelp")]);
        Assert.True(IsUnreferenced(db, "Form", "frmHelp"));
    }

    // ── RowSource on controls (strategy §3.3 / §3.3.1 + R2 Issue 3 coverage) ──

    [Fact]
    public void Table_Referenced_By_Control_RowSource_Literal_Is_Not_Unreferenced()
    {
        // Strategy §3.3: RowSource may be a literal table or query name.
        var comboProps = new Dictionary<string, string>
        {
            ["Name"] = "cboPick",
            ["RowSource"] = "tblTarget",
        };
        var control = new IrControl
        {
            Type = "ComboBox",
            Name = "cboPick",
            RowSource = "tblTarget",
            Properties = comboProps,
        };
        var db = Db(
            forms: [Form("frmHome", controls: [control])],
            tables: [Table("tblTarget")]);
        Assert.False(IsUnreferenced(db, "Table", "tblTarget"));
    }

    [Fact]
    public void Table_Referenced_By_Control_RowSource_Inline_SQL_Is_Not_Unreferenced()
    {
        // Strategy §3.3.1: inline-SQL RowSource — FROM-clause table contributes
        // a RowSource reference.
        string sql = "SELECT col FROM tblTarget";
        var comboProps = new Dictionary<string, string>
        {
            ["Name"] = "cboPick",
            ["RowSource"] = sql,
        };
        var control = new IrControl
        {
            Type = "ComboBox",
            Name = "cboPick",
            RowSource = sql,
            Properties = comboProps,
        };
        var db = Db(
            forms: [Form("frmHome", controls: [control])],
            tables: [Table("tblTarget")]);
        Assert.False(IsUnreferenced(db, "Table", "tblTarget"));
    }

    // ── Module/class context-narrowed scanning (strategy §3.3 + R2 Issue 4 fix) ──

    [Fact]
    public void Module_Referenced_By_Call_Statement_Is_Not_Unreferenced()
    {
        var db = Db(
            modules: [
                Module("modCallee"),
                Module("modCaller", vba: "Sub Go()\r\n  Call modCallee\r\nEnd Sub"),
            ]);
        Assert.False(IsUnreferenced(db, "Module", "modCallee"));
    }

    [Fact]
    public void Module_Referenced_By_Set_New_Constructor_Is_Not_Unreferenced()
    {
        var db = Db(
            modules: [
                Module("clsThing"),
                Module("modCaller",
                    vba: "Sub Make()\r\n  Set x = New clsThing\r\nEnd Sub"),
            ]);
        Assert.False(IsUnreferenced(db, "Module", "clsThing"));
    }

    [Fact]
    public void Module_Referenced_By_Qualifier_Dot_Is_Not_Unreferenced()
    {
        var db = Db(
            modules: [
                Module("modUtilities"),
                Module("modCaller",
                    vba: "Sub Go()\r\n  modUtilities.DoStuff\r\nEnd Sub"),
            ]);
        Assert.False(IsUnreferenced(db, "Module", "modUtilities"));
    }

    [Fact]
    public void Module_Bare_Token_In_Comment_Or_String_Is_Not_A_Reference()
    {
        // R2 Issue 4: bare-token occurrences of a module/class name in comments,
        // string literals, or unrelated code do NOT count as references. Only
        // Call / Set...New / qualifier contexts qualify.
        var db = Db(
            modules: [
                Module("modCallee"),
                Module("modCaller",
                    vba: "' modCallee is the helper module\r\nDebug.Print \"modCallee\"\r\n"),
            ]);
        Assert.True(IsUnreferenced(db, "Module", "modCallee"));
    }
}
