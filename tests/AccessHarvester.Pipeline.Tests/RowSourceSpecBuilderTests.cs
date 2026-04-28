using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic unit tests for RowSourceSpecBuilder. Inputs are
// AccessControl / FormMetadata records assembled in-memory plus the
// EventHandlerClassification shape returned by the Session 1 classifier;
// tests exercise the design-time row-source classification primitives
// independently of the cascading-driver annotation pass.
public class RowSourceSpecBuilderTests
{
    // ── Helpers ────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, JsonElement> Props(params (string Key, object Value)[] entries)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in entries) dict[k] = JsonSerializer.SerializeToElement(v);
        return dict;
    }

    private static AccessControl Combo(string name,
                                       IReadOnlyDictionary<string, JsonElement>? props = null)
        => new() { Type = "ComboBox", Name = name, Properties = props };

    private static AccessControl ListBox(string name,
                                         IReadOnlyDictionary<string, JsonElement>? props = null)
        => new() { Type = "ListBox", Name = name, Properties = props };

    private static FormMetadata Form(string name, params AccessControl[] children)
        => new() { Name = name, Children = children };

    // ── Eligibility ────────────────────────────────────────────────────

    [Fact]
    public void Build_Returns_Null_For_Non_Combo_Or_List_Control()
    {
        var textBox = new AccessControl { Type = "TextBox", Name = "txtFoo",
            Properties = Props(("RowSource", "ignored")) };

        Assert.Null(RowSourceSpecBuilder.Build(textBox, "frmFoo"));
    }

    [Fact]
    public void Build_Returns_Null_When_No_RowSource_Properties_Present()
    {
        var combo = Combo("cboFoo", Props(("Caption", "Bar")));

        Assert.Null(RowSourceSpecBuilder.Build(combo, "frmFoo"));
    }

    [Fact]
    public void Build_Emits_Empty_Kind_When_Only_BoundColumn_Specified()
    {
        var combo = Combo("cboFoo", Props(("BoundColumn", 1)));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo");

        Assert.NotNull(spec);
        Assert.Equal(RowSourceKind.Empty, spec!.Kind);
        Assert.Equal(1, spec.BoundColumn);
        Assert.Null(spec.RowSourceText);
    }

    // ── Table/Query — bareword reference ───────────────────────────────

    [Fact]
    public void Bareword_RowSource_Without_Type_Classifies_As_TableOrQueryNamed()
    {
        var combo = Combo("cboCaseworker", Props(("RowSource", "qryActiveCaseworkers")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmCases")!;

        Assert.Equal(RowSourceKind.TableOrQueryNamed, spec.Kind);
        Assert.Equal("qryActiveCaseworkers", spec.TargetName);
        Assert.Null(spec.InlineSql);
        Assert.Equal("qryActiveCaseworkers", spec.RowSourceText);
    }

    [Fact]
    public void Bareword_RowSource_With_TableQuery_Type_Classifies_As_TableOrQueryNamed()
    {
        var combo = Combo("cboFoo", Props(
            ("RowSource", "tblCases"), ("RowSourceType", "Table/Query")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.TableOrQueryNamed, spec.Kind);
        Assert.Equal("tblCases", spec.TargetName);
    }

    [Fact]
    public void Bracketed_Bareword_RowSource_Classifies_As_TableOrQueryNamed()
    {
        var combo = Combo("cboFoo", Props(("RowSource", "[Saved Query With Spaces]")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.TableOrQueryNamed, spec.Kind);
        Assert.Equal("[Saved Query With Spaces]", spec.TargetName);
    }

    // ── Table/Query — inline SQL ───────────────────────────────────────

    [Fact]
    public void Select_RowSource_Classifies_As_TableOrQueryInlineSql()
    {
        var sql = "SELECT CaseworkerID, CaseworkerName FROM tblCaseworkers ORDER BY CaseworkerName";
        var combo = Combo("cboCaseworker", Props(("RowSource", sql)));

        var spec = RowSourceSpecBuilder.Build(combo, "frmCases")!;

        Assert.Equal(RowSourceKind.TableOrQueryInlineSql, spec.Kind);
        Assert.Equal(sql, spec.InlineSql);
        Assert.Null(spec.TargetName);
    }

    [Fact]
    public void Parameters_RowSource_Classifies_As_TableOrQueryInlineSql()
    {
        var sql = "PARAMETERS [pCaseId] Long; SELECT * FROM tblCases WHERE Id = [pCaseId]";
        var combo = Combo("cboFoo", Props(("RowSource", sql)));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.TableOrQueryInlineSql, spec.Kind);
        Assert.Equal(sql, spec.InlineSql);
    }

    [Fact]
    public void Lowercase_Select_Classifies_As_InlineSql_Case_Insensitive()
    {
        var combo = Combo("cboFoo", Props(("RowSource", "select X from Y")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.TableOrQueryInlineSql, spec.Kind);
    }

    // ── Value list ─────────────────────────────────────────────────────

    [Fact]
    public void Value_List_RowSourceType_Splits_On_Semicolons()
    {
        var combo = Combo("cboStatus", Props(
            ("RowSource", "Open;Pending;Closed"),
            ("RowSourceType", "Value List")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.ValueList, spec.Kind);
        Assert.Equal(new[] { "Open", "Pending", "Closed" }, spec.ValueListItems);
        Assert.Null(spec.TargetName);
        Assert.Null(spec.InlineSql);
    }

    [Fact]
    public void Value_List_Strips_Surrounding_Quotes_And_Trailing_Empty()
    {
        var combo = Combo("cboFoo", Props(
            ("RowSource", "\"Open\";\"Pending Review\";\"Closed\";"),
            ("RowSourceType", "Value List")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(new[] { "Open", "Pending Review", "Closed" }, spec.ValueListItems);
    }

    [Fact]
    public void Value_List_With_Empty_RowSource_Emits_Null_Items()
    {
        var combo = Combo("cboFoo", Props(("RowSource", ""), ("RowSourceType", "Value List")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.ValueList, spec.Kind);
        Assert.Null(spec.ValueListItems);
    }

    // ── Field list ─────────────────────────────────────────────────────

    [Fact]
    public void Field_List_RowSourceType_Records_Source_Table()
    {
        var combo = Combo("cboField", Props(
            ("RowSource", "tblCases"), ("RowSourceType", "Field List")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.FieldList, spec.Kind);
        Assert.Equal("tblCases", spec.FieldListSourceTable);
        Assert.Null(spec.TargetName);
    }

    // ── Empty / unknown ────────────────────────────────────────────────

    [Fact]
    public void Empty_RowSource_String_With_LimitToList_Yields_Empty_Kind()
    {
        var combo = Combo("cboFoo", Props(("RowSource", ""), ("LimitToList", true)));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.Empty, spec.Kind);
        Assert.True(spec.LimitToList);
    }

    [Fact]
    public void Garbled_RowSource_That_Is_Neither_SQL_Nor_Bareword_Falls_Through_To_Unknown()
    {
        var combo = Combo("cboFoo", Props(("RowSource", "= 42 + ?? maybe")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(RowSourceKind.Unknown, spec.Kind);
        Assert.Equal("= 42 + ?? maybe", spec.RowSourceText);
    }

    // ── BoundColumn / ColumnCount / ColumnWidths ───────────────────────

    [Fact]
    public void Properties_Round_Trip_Onto_Spec()
    {
        var combo = Combo("cboCaseworker", Props(
            ("RowSource", "SELECT Id,Name FROM tblX"),
            ("BoundColumn", 1),
            ("ColumnCount", 4),
            ("ColumnWidths", "0;1440;1080;720"),
            ("LimitToList", true)));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(1, spec.BoundColumn);
        Assert.Equal(4, spec.ColumnCount);
        Assert.True(spec.LimitToList);
        Assert.NotNull(spec.Columns);
        Assert.Equal(4, spec.Columns!.Count);
    }

    [Fact]
    public void ColumnWidths_Decoded_To_Twips_And_Inches_With_Hidden_Flag()
    {
        var combo = Combo("cboFoo", Props(("ColumnWidths", "0;1440;720")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.NotNull(spec.Columns);
        Assert.Collection(spec.Columns!,
            c => { Assert.Equal(1, c.Index); Assert.Equal(0, c.WidthTwips); Assert.Equal(0.0, c.WidthInches); Assert.True(c.Hidden); },
            c => { Assert.Equal(2, c.Index); Assert.Equal(1440, c.WidthTwips); Assert.Equal(1.0, c.WidthInches); Assert.False(c.Hidden); },
            c => { Assert.Equal(3, c.Index); Assert.Equal(720, c.WidthTwips); Assert.Equal(0.5, c.WidthInches); Assert.False(c.Hidden); });
    }

    [Fact]
    public void ColumnWidths_With_Blank_Entries_Records_Null_Width_For_That_Column()
    {
        var combo = Combo("cboFoo", Props(("ColumnWidths", ";1440;")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(3, spec.Columns!.Count);
        Assert.Null(spec.Columns![0].WidthTwips);
        Assert.Equal(1440, spec.Columns![1].WidthTwips);
        Assert.Null(spec.Columns![2].WidthTwips);
    }

    [Fact]
    public void Numeric_Properties_Read_From_String_Encoded_Json()
    {
        var combo = Combo("cboFoo", Props(
            ("RowSource", "tblX"),
            ("BoundColumn", "1"),
            ("ColumnCount", "2")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.Equal(1, spec.BoundColumn);
        Assert.Equal(2, spec.ColumnCount);
    }

    [Fact]
    public void LimitToList_Reads_Yes_No_Strings()
    {
        var combo = Combo("cboFoo", Props(("RowSource", "tblX"), ("LimitToList", "Yes")));

        var spec = RowSourceSpecBuilder.Build(combo, "frmFoo")!;

        Assert.True(spec.LimitToList);
    }

    // ── Listbox eligibility ────────────────────────────────────────────

    [Fact]
    public void ListBox_Is_Eligible_For_RowSource_Spec()
    {
        var lst = ListBox("lstFoo", Props(("RowSource", "tblX")));

        var spec = RowSourceSpecBuilder.Build(lst, "frmFoo");

        Assert.NotNull(spec);
        Assert.Equal("ListBox", spec!.ControlType);
    }

    // ── BuildAll / tree walking ────────────────────────────────────────

    [Fact]
    public void BuildAll_Walks_Nested_Sections_And_Returns_All_Eligible_Controls()
    {
        var section = new AccessControl
        {
            Type = "Section",
            Name = "Detail",
            Children = new[]
            {
                Combo("cboA", Props(("RowSource", "tblA"))),
                ListBox("lstB", Props(("RowSource", "tblB"))),
                new AccessControl { Type = "TextBox", Name = "txtC" },
            },
        };
        var form = Form("frmFoo", section);

        var specs = RowSourceSpecBuilder.BuildAll(form);

        Assert.Equal(2, specs.Count);
        Assert.Equal(new[] { "cboA", "lstB" }, specs.Select(s => s.ControlName));
    }

    [Fact]
    public void BuildAll_Returns_Empty_When_Form_Has_No_Children()
    {
        var form = new FormMetadata { Name = "frmEmpty" };

        var specs = RowSourceSpecBuilder.BuildAll(form);

        Assert.Empty(specs);
    }

    // ── Cascading drivers ──────────────────────────────────────────────

    private static EventHandlerClassification Handler(string name,
        params EventHandlerClassificationSignal[] signals)
    {
        var categories = signals.Select(s => s.Category).Distinct().ToList();
        return new EventHandlerClassification
        {
            ProcedureName = name,
            Categories = categories,
            Mixed = categories.Count > 1,
            Signals = signals,
        };
    }

    private static EventHandlerClassificationSignal RowSourceAssign(string target, int line, int column)
        => new()
        {
            Category = EventHandlerClassificationCategory.CascadingUpdate,
            Kind = EventHandlerSignalKind.ControlAssignment,
            Line = line,
            Column = column,
            TargetExpression = target,
        };

    [Fact]
    public void Cascading_Driver_Detected_When_Bang_Access_Sets_RowSource()
    {
        var combo = Combo("cboClient", Props(("RowSource", "")));
        var form = Form("frmCases", combo);
        var handler = Handler("cboCaseworker_AfterUpdate",
            RowSourceAssign("Me!cboClient.RowSource", 12, 4));

        var specs = RowSourceSpecBuilder.BuildAll(form, new[] { handler });

        var spec = Assert.Single(specs);
        Assert.NotNull(spec.CascadingDrivers);
        var driver = Assert.Single(spec.CascadingDrivers!);
        Assert.Equal("cboCaseworker", driver.DriverControlName);
        Assert.Equal("cboCaseworker_AfterUpdate", driver.DriverProcedureName);
        Assert.Equal(12, driver.Line);
        Assert.Equal(4, driver.Column);
    }

    [Fact]
    public void Cascading_Driver_Detected_When_Dot_Access_Sets_RowSource()
    {
        var combo = Combo("cboClient", Props(("RowSource", "")));
        var form = Form("frmCases", combo);
        var handler = Handler("cboCaseworker_AfterUpdate",
            RowSourceAssign("Me.cboClient.RowSource", 5, 0));

        var specs = RowSourceSpecBuilder.BuildAll(form, new[] { handler });

        var driver = Assert.Single(specs[0].CascadingDrivers!);
        Assert.Equal("cboCaseworker", driver.DriverControlName);
    }

    [Fact]
    public void Cascading_Driver_With_Form_Level_Handler_Has_Null_Driver_Control()
    {
        var combo = Combo("cboClient", Props(("RowSource", "")));
        var form = Form("frmCases", combo);
        var handler = Handler("Form_Current",
            RowSourceAssign("Me!cboClient.RowSource", 1, 0));

        var specs = RowSourceSpecBuilder.BuildAll(form, new[] { handler });

        var driver = Assert.Single(specs[0].CascadingDrivers!);
        Assert.Null(driver.DriverControlName);
        Assert.Equal("Form_Current", driver.DriverProcedureName);
    }

    [Fact]
    public void Multiple_Drivers_To_Same_Combo_Are_Listed_In_Source_Order()
    {
        var combo = Combo("cboClient", Props(("RowSource", "")));
        var form = Form("frmCases", combo);
        var first = Handler("cboCaseworker_AfterUpdate",
            RowSourceAssign("Me!cboClient.RowSource", 12, 4));
        var second = Handler("btnReset_Click",
            RowSourceAssign("Me!cboClient.RowSource", 30, 4));

        var specs = RowSourceSpecBuilder.BuildAll(form, new[] { first, second });

        Assert.Equal(2, specs[0].CascadingDrivers!.Count);
        Assert.Equal("cboCaseworker", specs[0].CascadingDrivers![0].DriverControlName);
        Assert.Equal("btnReset", specs[0].CascadingDrivers![1].DriverControlName);
    }

    [Fact]
    public void NonControl_Assignment_Signals_Do_Not_Become_Cascading_Drivers()
    {
        var combo = Combo("cboClient", Props(("RowSource", "")));
        var form = Form("frmCases", combo);
        var handler = Handler("cboCaseworker_AfterUpdate",
            new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.FilterSearch,
                Kind = EventHandlerSignalKind.FilterAssignment,
                Line = 5,
                Column = 0,
                TargetExpression = "Me.Filter",
            });

        var specs = RowSourceSpecBuilder.BuildAll(form, new[] { handler });

        Assert.Null(specs[0].CascadingDrivers);
    }

    [Fact]
    public void Combo_Without_Driving_Handlers_Has_Null_CascadingDrivers()
    {
        var driven = Combo("cboClient", Props(("RowSource", "")));
        var unrelated = Combo("cboOther", Props(("RowSource", "tblX")));
        var form = Form("frmCases", driven, unrelated);
        var handler = Handler("cboCaseworker_AfterUpdate",
            RowSourceAssign("Me!cboClient.RowSource", 10, 0));

        var specs = RowSourceSpecBuilder.BuildAll(form, new[] { handler });

        var clientSpec = specs.Single(s => s.ControlName == "cboClient");
        var otherSpec = specs.Single(s => s.ControlName == "cboOther");
        Assert.NotNull(clientSpec.CascadingDrivers);
        Assert.Null(otherSpec.CascadingDrivers);
    }

    [Fact]
    public void AnnotateCascadingDrivers_Returns_Specs_Unmodified_When_Handlers_Empty()
    {
        var combo = Combo("cboClient", Props(("RowSource", "tblX")));
        var spec = RowSourceSpecBuilder.Build(combo, "frmCases")!;

        var annotated = RowSourceSpecBuilder.AnnotateCascadingDrivers(
            new[] { spec }, System.Array.Empty<EventHandlerClassification>());

        Assert.Same(spec, annotated[0]);
    }

    // ── Anchor fixture #02 — cascading combo + subform ────────────────

    [Fact]
    public void Fixture_02_Cascading_Combo_Pattern_Round_Trips()
    {
        var cboCaseworker = Combo("cboCaseworker", Props(
            ("RowSource",
             "SELECT CaseworkerID, CaseworkerName, Email, Phone FROM tblCaseworkers ORDER BY CaseworkerName"),
            ("RowSourceType", "Table/Query"),
            ("BoundColumn", 1),
            ("ColumnCount", 4),
            ("ColumnWidths", "0;1440;1440;1080")));
        var cboClient = Combo("cboClient", Props(
            ("RowSource", ""),
            ("RowSourceType", "Table/Query"),
            ("BoundColumn", 1),
            ("ColumnCount", 2)));

        var form = Form("frmCaseManagement", cboCaseworker, cboClient);

        var driverHandler = Handler("cboCaseworker_AfterUpdate",
            RowSourceAssign("Me!cboClient.RowSource", 12, 4));

        var specs = RowSourceSpecBuilder.BuildAll(form, new[] { driverHandler });

        Assert.Equal(2, specs.Count);

        var caseworker = specs.Single(s => s.ControlName == "cboCaseworker");
        Assert.Equal(RowSourceKind.TableOrQueryInlineSql, caseworker.Kind);
        Assert.Equal(4, caseworker.Columns!.Count);
        Assert.True(caseworker.Columns![0].Hidden);
        Assert.Null(caseworker.CascadingDrivers);

        var client = specs.Single(s => s.ControlName == "cboClient");
        Assert.Equal(RowSourceKind.Empty, client.Kind);
        var driver = Assert.Single(client.CascadingDrivers!);
        Assert.Equal("cboCaseworker", driver.DriverControlName);
    }
}
