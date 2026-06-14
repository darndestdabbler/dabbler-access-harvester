using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.UsageIdentification;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

/// <summary>
/// Hermetic pins for <see cref="NavigationChainBuilder"/> (012 S2, R2-2 /
/// DW-10): the three mechanical chain-edge producers plus the
/// explicit-unresolved channel. Pure in-memory <see cref="IrDatabase"/>
/// inputs — no COM, no I/O.
/// </summary>
public class NavigationChainBuilderTests
{
    // ── Builders ───────────────────────────────────────────────────────

    private static IrDatabase Db(
        IrFormOrReport[]? forms = null,
        IrFormOrReport[]? reports = null,
        IrQuery[]? queries = null,
        IrTable[]? tables = null) =>
        new()
        {
            Name = "Test",
            Forms = forms ?? [],
            Reports = reports ?? [],
            Queries = queries ?? [],
            Tables = tables ?? [],
            Relationships = [],
            Macros = [],
            Modules = [],
        };

    private static IrFormOrReport Obj(
        string name, string objectType, string? recordSource = null, IrControl[]? controls = null) =>
        new()
        {
            Name = name,
            ObjectType = objectType,
            RecordSource = recordSource,
            VbaCode = null,
            Controls = controls ?? [],
            MacroActions = [],
        };

    private static IrControl Button(string name, string @event, string action, (string, string)[] args) =>
        new()
        {
            Type = "CommandButton",
            Name = name,
            EventMacros =
            [
                new IrControlEventMacro
                {
                    Event = @event,
                    Actions =
                    [
                        new IrEventMacroAction
                        {
                            Action = action,
                            Arguments = args.ToDictionary(a => a.Item1, a => a.Item2),
                        },
                    ],
                },
            ],
        };

    private static IrQuery Query(string name, string sql) =>
        new()
        {
            Name = name,
            QueryTypeCode = 0,
            Sql = sql,
            Parameters = IrLoader.ExtractQueryParameters(sql),
        };

    private static IrTable Table(string name) =>
        new() { Name = name, ColumnCount = 1 };

    // ── Producer 1: embedded-macro edges ───────────────────────────────

    [Fact]
    public void EmbeddedMacro_OpenReport_EmitsOpensReportEdge()
    {
        var menu = Obj("frmReportMenu", "Form", controls:
        [
            Button("btnClientProfile", "OnClick", "OpenReport", [("ReportName", "rptClientProfile")]),
        ]);
        var edges = NavigationChainBuilder.Build(Db(forms: [menu])).Edges;

        var edge = Assert.Single(edges);
        Assert.Equal(ChainEdgeKind.OpensReport, edge.Kind);
        Assert.Equal("embedded-macro", edge.Provenance);
        Assert.Equal(ChainNodeKind.Form, edge.From.Kind);
        Assert.Equal("frmReportMenu", edge.From.Name);
        Assert.Equal("btnClientProfile", edge.From.Member);
        Assert.Equal(ChainNodeKind.Report, edge.To.Kind);
        Assert.Equal("rptClientProfile", edge.To.Name);
    }

    [Fact]
    public void EmbeddedMacro_OpenForm_EmitsOpensFormEdge()
    {
        var search = Obj("frmClientSearch", "Form", controls:
        [
            Button("btnAddNew", "OnClick", "OpenForm", [("FormName", "frmClientDetail")]),
        ]);
        var edge = Assert.Single(NavigationChainBuilder.Build(Db(forms: [search])).Edges);
        Assert.Equal(ChainEdgeKind.OpensForm, edge.Kind);
        Assert.Equal("frmClientDetail", edge.To.Name);
        Assert.Equal(ChainNodeKind.Form, edge.To.Kind);
    }

    [Fact]
    public void EmbeddedMacro_NonOpenAction_IsIgnored()
    {
        // A report's OnClose CloseWindow macro is not a chain-open kind.
        var report = Obj("rptX", "Report", controls:
        [
            Button("Report", "OnClose", "CloseWindow", [("ObjectType", "2"), ("ObjectName", "frmDlg")]),
        ]);
        Assert.Empty(NavigationChainBuilder.Build(Db(reports: [report])).Edges);
    }

    // ── Producer 2: record-source edges ────────────────────────────────

    [Fact]
    public void RecordSource_NamedQuery_EmitsFeedsRecordSourceFromQuery()
    {
        var db = Db(
            reports: [Obj("rptProviderCosts", "Report", recordSource: "qryProviderCosts")],
            queries: [Query("qryProviderCosts", "SELECT 1;")]);
        var edge = Assert.Single(NavigationChainBuilder.Build(db).Edges);
        Assert.Equal(ChainEdgeKind.FeedsRecordSource, edge.Kind);
        Assert.Equal("record-source", edge.Provenance);
        Assert.Equal(ChainNodeKind.Query, edge.From.Kind);
        Assert.Equal("qryProviderCosts", edge.From.Name);
        Assert.Equal(ChainNodeKind.Report, edge.To.Kind);
        Assert.Equal("rptProviderCosts", edge.To.Name);
    }

    [Fact]
    public void RecordSource_NamedTable_EmitsFeedsRecordSourceFromTable()
    {
        var db = Db(
            reports: [Obj("rptBudgets", "Report", recordSource: "tblClientBudgets")],
            tables: [Table("tblClientBudgets")]);
        var edge = Assert.Single(NavigationChainBuilder.Build(db).Edges);
        Assert.Equal(ChainNodeKind.Table, edge.From.Kind);
        Assert.Equal("tblClientBudgets", edge.From.Name);
    }

    [Fact]
    public void RecordSource_UnknownName_ClassifiesAsUnknown()
    {
        var db = Db(reports: [Obj("rptX", "Report", recordSource: "qryNotHarvested")]);
        var edge = Assert.Single(NavigationChainBuilder.Build(db).Edges);
        Assert.Equal(ChainNodeKind.Unknown, edge.From.Kind);
    }

    [Fact]
    public void RecordSource_InlineSql_EmitsFeedsRecordSourcePerFromAndJoinTarget()
    {
        var db = Db(
            reports: [Obj("rptX", "Report", recordSource:
                "SELECT n.*, s.ServiceName FROM tblNeeds AS n LEFT JOIN tblServices AS s ON n.ServiceID = s.ServiceID;")],
            tables: [Table("tblNeeds"), Table("tblServices")]);
        var edges = NavigationChainBuilder.Build(db).Edges;

        var froms = edges.Where(e => e.Kind == ChainEdgeKind.FeedsRecordSource)
            .Select(e => e.From.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "tblNeeds", "tblServices" }, froms);
        Assert.All(edges, e => Assert.Equal(ChainNodeKind.Table, e.From.Kind));
    }

    [Fact]
    public void RecordSource_InlineSql_WithTempVars_EmitsSuppliesParameterTempVarFlow()
    {
        var db = Db(
            reports: [Obj("srptShortfall", "Report", recordSource:
                "SELECT * FROM qryNeeds WHERE ClientID=TempVars!repCID And FiscalYear=TempVars!repFY;")],
            queries: [Query("qryNeeds", "SELECT 1;")]);
        var edges = NavigationChainBuilder.Build(db).Edges;

        var tempVars = edges
            .Where(e => e.Kind == ChainEdgeKind.SuppliesParameter && e.Provenance == "tempvar-flow")
            .Select(e => e.From.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "repCID", "repFY" }, tempVars);
        Assert.All(edges.Where(e => e.Provenance == "tempvar-flow"),
            e => Assert.Equal(ChainNodeKind.TempVar, e.From.Kind));
        // The FROM target still resolves to the named query.
        Assert.Contains(edges, e => e.Kind == ChainEdgeKind.FeedsRecordSource && e.From.Name == "qryNeeds");
    }

    [Fact]
    public void RecordSource_InlineSql_WithFormReference_EmitsSuppliesParameterFormReference()
    {
        // Synthetic: HHC has no [Forms]! reference in any record source.
        var db = Db(
            reports: [Obj("rptX", "Report", recordSource:
                "SELECT * FROM tblClients WHERE ClientID = [Forms]![frmParam]![txtClientId];")],
            tables: [Table("tblClients")]);
        var edges = NavigationChainBuilder.Build(db).Edges;

        var supplies = Assert.Single(edges.Where(e => e.Kind == ChainEdgeKind.SuppliesParameter));
        Assert.Equal("query-form-reference", supplies.Provenance);
        Assert.Equal(ChainNodeKind.Form, supplies.From.Kind);
        Assert.Equal("frmParam", supplies.From.Name);
        Assert.Equal("txtClientId", supplies.From.Member);
    }

    // ── Producer 3: declared-parameter edges ───────────────────────────

    [Fact]
    public void QueryParameter_PromptDeclared_EmitsUnresolvedChainTarget()
    {
        var db = Db(queries:
        [
            Query("qryParam", "PARAMETERS [Enter Provider ID:] Long; SELECT 1 FROM t WHERE x=[Enter Provider ID:];"),
        ]);
        var result = NavigationChainBuilder.Build(db);

        Assert.Empty(result.Edges.Where(e => e.Provenance == "query-declared-parameter"));
        var u = Assert.Single(result.UnresolvedTargets);
        Assert.Equal(ChainEdgeKind.SuppliesParameter, u.Kind);
        Assert.Equal("query-declared-parameter", u.Provenance);
        Assert.Equal("prompt-parameter", u.Reason);
        Assert.Equal(ChainNodeKind.Query, u.Consumer.Kind);
        Assert.Equal("qryParam", u.Consumer.Name);
        // QueryMetadataExtractor strips the brackets from a declared name.
        Assert.Equal("Enter Provider ID:", u.Detail);
    }

    [Fact]
    public void QueryParameter_FormReference_EmitsSuppliesParameterEdge()
    {
        // Synthetic: HHC has no [Forms]! reference in any query SQL.
        var db = Db(queries:
        [
            Query("qryByForm", "SELECT * FROM t WHERE id = [Forms]![frmFilter]![txtId];"),
        ]);
        var edge = Assert.Single(NavigationChainBuilder.Build(db).Edges);
        Assert.Equal(ChainEdgeKind.SuppliesParameter, edge.Kind);
        Assert.Equal("query-form-reference", edge.Provenance);
        Assert.Equal(ChainNodeKind.Form, edge.From.Kind);
        Assert.Equal("frmFilter", edge.From.Name);
        Assert.Equal("txtId", edge.From.Member);
        Assert.Equal(ChainNodeKind.Query, edge.To.Kind);
        Assert.Equal("qryByForm", edge.To.Name);
    }

    [Fact]
    public void QueryParameter_ReportReference_EmitsSuppliesParameterFromReport()
    {
        var db = Db(queries:
        [
            Query("qryByReport", "SELECT * FROM t WHERE id = [Reports]![rptX]![txtId];"),
        ]);
        var edge = Assert.Single(NavigationChainBuilder.Build(db).Edges);
        Assert.Equal(ChainNodeKind.Report, edge.From.Kind);
        Assert.Equal("rptX", edge.From.Name);
        Assert.Equal("txtId", edge.From.Member);
    }

    // ── Cross-cutting ──────────────────────────────────────────────────

    // ── R1 verification fixes ──────────────────────────────────────────

    [Fact]
    public void EmbeddedMacro_ObjectLevel_OnOpen_EmitsEdgeFromObject()
    {
        // A report whose OWN OnOpen embedded macro opens a dialog form
        // (object-level event macro, no control member). Golden-neutral on
        // HHC (no object-level Opens there); pins the general gap.
        var report = new IrFormOrReport
        {
            Name = "rptX", ObjectType = "Report", RecordSource = null, VbaCode = null,
            Controls = [], MacroActions = [],
            EventMacros =
            [
                new IrControlEventMacro
                {
                    Event = "OnOpen",
                    Actions = [ new IrEventMacroAction { Action = "OpenForm",
                        Arguments = new Dictionary<string, string> { ["FormName"] = "frmParam" } } ],
                },
            ],
        };
        var edge = Assert.Single(NavigationChainBuilder.Build(Db(reports: [report])).Edges);
        Assert.Equal(ChainEdgeKind.OpensForm, edge.Kind);
        Assert.Equal(ChainNodeKind.Report, edge.From.Kind);
        Assert.Equal("rptX", edge.From.Name);
        Assert.Null(edge.From.Member);   // object-level: no control member
        Assert.Equal("frmParam", edge.To.Name);
    }

    [Fact]
    public void EmbeddedMacro_RawPropertyWithoutStructuredMacro_IsSurfacedUnresolved()
    {
        // A button carries OnClickEmMacro the enricher did not structure into
        // EventMacros — surfaced explicitly, not silently dropped.
        var control = new IrControl
        {
            Type = "CommandButton", Name = "btnX",
            Properties = new Dictionary<string, string> { ["OnClickEmMacro"] = "Version =196611\nBegin\n..." },
            EventMacros = null,
        };
        var form = Obj("frmX", "Form", controls: [control]);
        var result = NavigationChainBuilder.Build(Db(forms: [form]));

        Assert.Empty(result.Edges);
        var u = Assert.Single(result.UnresolvedTargets);
        Assert.Equal("embedded-macro-unparsed", u.Reason);
        Assert.Equal("embedded-macro", u.Provenance);
        Assert.Equal("OnClick", u.Detail);
        Assert.Equal("btnX", u.Consumer.Member);
    }

    [Fact]
    public void RecordSource_TempVarsInsideStringLiteral_DoesNotEmitEdge()
    {
        // A string literal that contains "TempVars!x" must not fabricate a
        // tempvar-flow edge, and a substring ("xTempVars!") has no boundary.
        var db = Db(
            reports: [Obj("rptX", "Report", recordSource:
                "SELECT * FROM tblClients WHERE Note = 'see TempVars!fake' OR x = 1;")],
            tables: [Table("tblClients")]);
        var edges = NavigationChainBuilder.Build(db).Edges;
        Assert.DoesNotContain(edges, e => e.Provenance == "tempvar-flow");
        Assert.Contains(edges, e => e.Kind == ChainEdgeKind.FeedsRecordSource && e.From.Name == "tblClients");
    }

    [Fact]
    public void RecordSource_InlineSqlWithNoRecoverableSource_EmitsRecordSourceUnparsed()
    {
        var db = Db(reports: [Obj("rptX", "Report", recordSource: "SELECT @@@ garbage <<>> not sql ;")]);
        var result = NavigationChainBuilder.Build(db);
        var u = Assert.Single(result.UnresolvedTargets);
        Assert.Equal("record-source-unparsed", u.Reason);
        Assert.Equal(ChainEdgeKind.FeedsRecordSource, u.Kind);
        Assert.Equal("rptX", u.Consumer.Name);
    }

    [Fact]
    public void QueryParameter_MalformedFormReference_IsSurfacedUnresolvedNotFabricated()
    {
        // A form-reference parameter whose path names no object → explicit
        // unresolved, never a synthetic "?" endpoint.
        var q = new IrQuery
        {
            Name = "qryX", QueryTypeCode = 0, Sql = "SELECT 1;",
            Parameters = [ new IrQueryParameter { Name = "x", Source = "form-reference", Path = "[Forms]" } ],
        };
        var result = NavigationChainBuilder.Build(Db(queries: [q]));

        Assert.DoesNotContain(result.Edges, e => e.From.Name == "?");
        var u = Assert.Single(result.UnresolvedTargets);
        Assert.Equal("unresolvable-reference", u.Reason);
        Assert.Equal("query-form-reference", u.Provenance);
    }

    // ── R2 follow-on fixes ─────────────────────────────────────────────

    [Fact]
    public void RecordSource_TempVarsInsideBracketedIdentifier_DoesNotEmitEdge()
    {
        // A field literally named [TempVars!fake] must not fabricate a
        // tempvar-flow edge; a real (unbracketed) TempVars!x still does.
        var db = Db(
            reports: [Obj("rptX", "Report", recordSource:
                "SELECT [TempVars!fake] FROM tblClients WHERE c = TempVars!real;")],
            tables: [Table("tblClients")]);
        var edges = NavigationChainBuilder.Build(db).Edges;

        var tempVars = edges.Where(e => e.Provenance == "tempvar-flow").Select(e => e.From.Name).ToArray();
        Assert.Equal(new[] { "real" }, tempVars);  // 'fake' (bracketed identifier) excluded
    }

    [Fact]
    public void QueryParameter_FormReferenceWithEmptySegment_IsUnresolvedNotFabricated()
    {
        // "[Forms]!![txtId]" has an empty object slot -> explicit unresolved,
        // never a fabricated supplier named "txtId".
        var q = new IrQuery
        {
            Name = "qryX", QueryTypeCode = 0, Sql = "SELECT 1;",
            Parameters = [ new IrQueryParameter { Name = "txtId", Source = "form-reference", Path = "[Forms]!![txtId]" } ],
        };
        var result = NavigationChainBuilder.Build(Db(queries: [q]));

        Assert.Empty(result.Edges);
        Assert.DoesNotContain(result.Edges, e => e.From.Name == "txtId");
        var u = Assert.Single(result.UnresolvedTargets);
        Assert.Equal("unresolvable-reference", u.Reason);
    }

    // ── R3 follow-on fixes (token-aware scan + strict path shape) ──────

    [Fact]
    public void RecordSource_TempVarsInsideNonLeadingBracketSpan_DoesNotEmitEdge()
    {
        // TempVars appearing NOT as the first token of a bracketed identifier
        // (e.g. a field named [prompt TempVars!x]) must be skipped wholesale;
        // a real top-level TempVars![member] still resolves.
        var db = Db(
            reports: [Obj("rptX", "Report", recordSource:
                "SELECT [prompt TempVars!fake] FROM tblClients WHERE c = TempVars![real];")],
            tables: [Table("tblClients")]);
        var tempVars = NavigationChainBuilder.Build(db).Edges
            .Where(e => e.Provenance == "tempvar-flow").Select(e => e.From.Name).ToArray();
        Assert.Equal(new[] { "real" }, tempVars);
    }

    [Fact]
    public void RecordSource_TempVarsInDoubleQuotedLiteral_DoesNotEmitEdge()
    {
        var db = Db(
            reports: [Obj("rptX", "Report", recordSource:
                "SELECT * FROM tblClients WHERE c = \"x TempVars!fake y\" OR d = TempVars!real;")],
            tables: [Table("tblClients")]);
        var tempVars = NavigationChainBuilder.Build(db).Edges
            .Where(e => e.Provenance == "tempvar-flow").Select(e => e.From.Name).ToArray();
        Assert.Equal(new[] { "real" }, tempVars);
    }

    [Fact]
    public void QueryParameter_FormReferenceWithFourSegments_IsUnresolvedNotFabricated()
    {
        var q = new IrQuery
        {
            Name = "qryX", QueryTypeCode = 0, Sql = "SELECT 1;",
            Parameters = [ new IrQueryParameter { Name = "txtId", Source = "form-reference", Path = "[Forms]![frm]!![txtId]" } ],
        };
        var result = NavigationChainBuilder.Build(Db(queries: [q]));
        Assert.Empty(result.Edges);
        var u = Assert.Single(result.UnresolvedTargets);
        Assert.Equal("unresolvable-reference", u.Reason);
    }

    [Fact]
    public void Build_EmptyDatabase_ReturnsEmptyResult()
    {
        var result = NavigationChainBuilder.Build(Db());
        Assert.Empty(result.Edges);
        Assert.Empty(result.UnresolvedTargets);
    }

    [Fact]
    public void Build_IsDeterministic_AcrossInputOrdering()
    {
        var a = Obj("frmA", "Form", controls:
            [Button("b1", "OnClick", "OpenReport", [("ReportName", "rptZ")])]);
        var b = Obj("frmB", "Form", controls:
            [Button("b2", "OnClick", "OpenReport", [("ReportName", "rptA")])]);

        var first = NavigationChainBuilder.Build(Db(forms: [a, b])).Edges;
        var second = NavigationChainBuilder.Build(Db(forms: [b, a])).Edges;

        Assert.Equal(
            first.Select(e => (e.From.Name, e.To.Name)),
            second.Select(e => (e.From.Name, e.To.Name)));
    }

    // ── S3: code-behind producer (R2-2 part 2, DW-10) ──────────────────

    // Parses a VBA source string into a ResolvedModule named for the form/report
    // it is code-behind for (the module name IS the bare object name, matching
    // the production seam NavigationGraphBuilder uses). ModuleKind is irrelevant
    // to the chain producer (it keys off the name → form/report membership).
    private static ResolvedModule CodeBehind(
        string source, string moduleName, ModuleKind kind = ModuleKind.Report)
    {
        var ast = VbaParserPipeline.Parse(source).BuildAst(moduleName);
        var project = new ProjectScope();
        var moduleScope = new ScopeBuildingVisitor()
            .BuildModuleScope(project, ast, moduleName, kind);
        return new ReferenceResolver().Resolve(moduleScope, ast);
    }

    [Fact]
    public void CodeBehind_OpenForm_WithAcDialog_EmitsOpensDialog()
    {
        var db = Db(
            reports: [Obj("rptX", "Report")],
            forms: [Obj("frmParam", "Form")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    DoCmd.OpenForm ""frmParam"", , , , , acDialog
End Sub", "rptX");

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal(ChainEdgeKind.OpensDialog, edge.Kind);
        Assert.Equal("vba-call", edge.Provenance);
        Assert.Equal(ChainNodeKind.Report, edge.From.Kind);
        Assert.Equal("rptX", edge.From.Name);
        Assert.Null(edge.From.Member);
        Assert.Equal(ChainNodeKind.Form, edge.To.Kind);
        Assert.Equal("frmParam", edge.To.Name);
    }

    [Fact]
    public void CodeBehind_OpenForm_WithoutWindowMode_EmitsOpensForm()
    {
        var db = Db(forms: [Obj("frmSearch", "Form"), Obj("frmDetail", "Form")]);
        var module = CodeBehind(@"
Private Sub btnAddNew_Click()
    DoCmd.OpenForm ""frmDetail""
End Sub", "frmSearch", ModuleKind.Form);

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal(ChainEdgeKind.OpensForm, edge.Kind);
        Assert.Equal(ChainNodeKind.Form, edge.From.Kind);
        Assert.Equal("frmSearch", edge.From.Name);
        Assert.Equal("frmDetail", edge.To.Name);
    }

    [Fact]
    public void CodeBehind_OpenReport_EmitsOpensReport()
    {
        var db = Db(
            forms: [Obj("frmClientDetail", "Form")],
            reports: [Obj("rptClientProfile", "Report")]);
        var module = CodeBehind(@"
Private Sub btnPrint_Click()
    DoCmd.OpenReport ""rptClientProfile"", acViewPreview
End Sub", "frmClientDetail", ModuleKind.Form);

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal(ChainEdgeKind.OpensReport, edge.Kind);
        Assert.Equal(ChainNodeKind.Report, edge.To.Kind);
        Assert.Equal("rptClientProfile", edge.To.Name);
    }

    [Fact]
    public void CodeBehind_FormsControlRead_EmitsSuppliesParameterFromDialog()
    {
        var db = Db(
            reports: [Obj("rptX", "Report")],
            forms: [Obj("frmParam", "Form")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    m_ClientID = Forms!frmParam!cboClient
End Sub", "rptX");

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal(ChainEdgeKind.SuppliesParameter, edge.Kind);
        Assert.Equal("vba-call", edge.Provenance);
        Assert.Equal(ChainNodeKind.Form, edge.From.Kind);
        Assert.Equal("frmParam", edge.From.Name);
        Assert.Equal("cboClient", edge.From.Member);
        Assert.Equal(ChainNodeKind.Report, edge.To.Kind);
        Assert.Equal("rptX", edge.To.Name);
    }

    [Fact]
    public void CodeBehind_ReportsControlRead_EmitsSuppliesParameterFromReport()
    {
        var db = Db(reports: [Obj("rptX", "Report"), Obj("rptOther", "Report")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    x = Reports!rptOther!txtTotal
End Sub", "rptX");

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal(ChainNodeKind.Report, edge.From.Kind);
        Assert.Equal("rptOther", edge.From.Name);
        Assert.Equal("txtTotal", edge.From.Member);
    }

    [Fact]
    public void CodeBehind_FormsControlWrite_DoesNotEmitReadEdge()
    {
        // A WRITE into a dialog control (Forms!frm!ctl = x) is the owner pushing
        // INTO the dialog — the opposite direction of a parameter read; it must
        // not fabricate a (wrong-direction) SuppliesParameter edge.
        var db = Db(
            reports: [Obj("rptX", "Report")],
            forms: [Obj("frmParam", "Form")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    Forms!frmParam!cboClient = 42
End Sub", "rptX");

        Assert.Empty(NavigationChainBuilder.Build(db, [module]).Edges);
    }

    [Fact]
    public void CodeBehind_MemberWrappedFormsControlWrite_DoesNotEmitReadEdge()
    {
        // `Forms!frm!ctl.Value = 42` — a member-access wrapping the control bang
        // on the LHS is still a WRITE into the dialog; it must not fabricate a
        // (wrong-direction) read edge (routed-verifier R1 Major).
        var db = Db(
            reports: [Obj("rptX", "Report")],
            forms: [Obj("frmParam", "Form")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    Forms!frmParam!cboClient.Value = 42
End Sub", "rptX");

        Assert.Empty(NavigationChainBuilder.Build(db, [module]).Edges);
    }

    [Fact]
    public void CodeBehind_MemberWrappedFormsControlRead_EmitsReadEdge()
    {
        // …but `x = Forms!frm!ctl.Value` (member-access on the RHS) is a READ and
        // MUST still emit the SuppliesParameter edge (guards over-suppression).
        var db = Db(
            reports: [Obj("rptX", "Report")],
            forms: [Obj("frmParam", "Form")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    m_ClientID = Forms!frmParam!cboClient.Value
End Sub", "rptX");

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal(ChainEdgeKind.SuppliesParameter, edge.Kind);
        Assert.Equal("frmParam", edge.From.Name);
        Assert.Equal("cboClient", edge.From.Member);
    }

    [Fact]
    public void CodeBehind_TempVarsAssignment_EmitsSuppliesParameterTempVarFlow()
    {
        var db = Db(reports: [Obj("rptX", "Report")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    TempVars!repCID = m_ClientID
End Sub", "rptX");

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal(ChainEdgeKind.SuppliesParameter, edge.Kind);
        Assert.Equal("tempvar-flow", edge.Provenance);
        Assert.Equal(ChainNodeKind.Report, edge.From.Kind);
        Assert.Equal("rptX", edge.From.Name);
        Assert.Equal(ChainNodeKind.TempVar, edge.To.Kind);
        Assert.Equal("repCID", edge.To.Name);
    }

    [Fact]
    public void CodeBehind_NonLiteralOpenTarget_EmitsUnresolvedNonLiteralTarget()
    {
        // Golden-neutral on HHC (no dynamic open in form/report code-behind);
        // pins the explicit-unresolved channel for variable targets.
        var db = Db(reports: [Obj("rptMenu", "Report")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    DoCmd.OpenReport strReportName
End Sub", "rptMenu");

        var result = NavigationChainBuilder.Build(db, [module]);
        Assert.Empty(result.Edges);
        var u = Assert.Single(result.UnresolvedTargets);
        Assert.Equal(ChainEdgeKind.OpensReport, u.Kind);
        Assert.Equal("vba-call", u.Provenance);
        Assert.Equal("non-literal-target", u.Reason);
        Assert.Equal("rptMenu", u.Consumer.Name);
    }

    [Fact]
    public void CodeBehind_StandardModuleDispatch_IsNotPromotedToChainEdge()
    {
        // A DoCmd in a module that is NOT a form/report code-behind has no
        // chain owner — it stays in the VBA-edge layer, not the chain layer.
        var db = Db(forms: [Obj("frmDetail", "Form")]);
        var module = CodeBehind(@"
Sub OpenIt()
    DoCmd.OpenForm ""frmDetail""
End Sub", "basHelpers", ModuleKind.Standard);

        Assert.Empty(NavigationChainBuilder.Build(db, [module]).Edges);
    }

    [Fact]
    public void CodeBehind_DuplicateDispatch_CollapsesToOneEdge()
    {
        // Same structural open from two procedures → one chain edge.
        var db = Db(forms: [Obj("frmList", "Form"), Obj("frmDetail", "Form")]);
        var module = CodeBehind(@"
Private Sub btnAddNew_Click()
    DoCmd.OpenForm ""frmDetail""
End Sub
Private Sub Form_DblClick(Cancel As Integer)
    DoCmd.OpenForm ""frmDetail""
End Sub", "frmList", ModuleKind.Form);

        var edge = Assert.Single(NavigationChainBuilder.Build(db, [module]).Edges);
        Assert.Equal("frmDetail", edge.To.Name);
    }

    [Fact]
    public void TempVarSupply_ReferencedButNotSet_EmitsTempVarNoSupplier()
    {
        // A subreport record source references TempVars!repCID, but no harvested
        // code-behind sets it → explicit unresolved (broken supply chain).
        var db = Db(
            reports: [Obj("srptShortfall", "Report", recordSource:
                "SELECT * FROM qryNeeds WHERE ClientID=TempVars!repCID;")],
            queries: [Query("qryNeeds", "SELECT 1;")]);
        // A code-behind module that sets a DIFFERENT TempVar (repFY) — repCID
        // stays unsupplied.
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    TempVars!repFY = 2024
End Sub", "srptShortfall");

        var result = NavigationChainBuilder.Build(db, [module]);
        var u = Assert.Single(result.UnresolvedTargets, x => x.Reason == "tempvar-no-supplier");
        Assert.Equal("repCID", u.Detail);
        Assert.Equal("tempvar-flow", u.Provenance);
        Assert.Equal("srptShortfall", u.Consumer.Name);
    }

    [Fact]
    public void TempVarSupply_ReferencedAndSet_NoUnresolved()
    {
        // The canonical HHC shape: the parent report sets the TempVar a
        // subreport's record source consumes → matched, no unresolved.
        var db = Db(
            reports: [Obj("rptParent", "Report"),
                      Obj("srptShortfall", "Report", recordSource:
                          "SELECT * FROM qryNeeds WHERE ClientID=TempVars!repCID;")],
            queries: [Query("qryNeeds", "SELECT 1;")]);
        var module = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    TempVars!repCID = m_ClientID
End Sub", "rptParent");

        var result = NavigationChainBuilder.Build(db, [module]);
        Assert.DoesNotContain(result.UnresolvedTargets, x => x.Reason == "tempvar-no-supplier");
        // Both halves of the chain are present: setter (parent → TempVar) and
        // consumer (TempVar → subreport).
        Assert.Contains(result.Edges, e => e.Provenance == "tempvar-flow"
            && e.From.Name == "rptParent" && e.To.Kind == ChainNodeKind.TempVar);
        Assert.Contains(result.Edges, e => e.Provenance == "tempvar-flow"
            && e.From.Kind == ChainNodeKind.TempVar && e.To.Name == "srptShortfall");
    }

    [Fact]
    public void TempVarSupply_SetInStandardModule_SuppressesUnmatched()
    {
        // A TempVar set in a standard module (no form/report owner → no supplier
        // EDGE) still counts as supplied, so its record-source consumer is NOT
        // falsely flagged as a broken supply chain.
        var db = Db(
            reports: [Obj("srptShortfall", "Report", recordSource:
                "SELECT * FROM qryNeeds WHERE ClientID=TempVars!repCID;")],
            queries: [Query("qryNeeds", "SELECT 1;")]);
        var standardModule = CodeBehind(@"
Sub InitReportContext()
    TempVars!repCID = 1
End Sub", "basReportUtils", ModuleKind.Standard);

        var result = NavigationChainBuilder.Build(db, [standardModule]);
        Assert.DoesNotContain(result.UnresolvedTargets, x => x.Reason == "tempvar-no-supplier");
        // …and no supplier edge is fabricated from the owner-less standard module.
        Assert.DoesNotContain(result.Edges,
            e => e.Provenance == "tempvar-flow" && e.To.Kind == ChainNodeKind.TempVar);
    }

    [Fact]
    public void CodeBehind_FormReportNameCollision_AttributesEachModuleToItsOwnKind()
    {
        // Access permits a form and report to share a name; both code-behind
        // modules then arrive with ModuleName == the bare name. Owner kind must
        // come from each module's OWN resolved Kind, not a name lookup, so the
        // form's edges are NOT misattributed to the report (devil's-advocate C3).
        var db = Db(forms: [Obj("Dashboard", "Form")], reports: [Obj("Dashboard", "Report")]);
        var formModule = CodeBehind(@"
Private Sub cmdOpen_Click()
    DoCmd.OpenForm ""frmDetail""
End Sub", "Dashboard", ModuleKind.Form);
        var reportModule = CodeBehind(@"
Private Sub Report_Open(Cancel As Integer)
    DoCmd.OpenForm ""frmParam"", , , , , acDialog
End Sub", "Dashboard", ModuleKind.Report);

        var edges = NavigationChainBuilder.Build(db, [formModule, reportModule]).Edges;
        Assert.Contains(edges, e => e.Kind == ChainEdgeKind.OpensForm
            && e.From.Kind == ChainNodeKind.Form && e.From.Name == "Dashboard" && e.To.Name == "frmDetail");
        Assert.Contains(edges, e => e.Kind == ChainEdgeKind.OpensDialog
            && e.From.Kind == ChainNodeKind.Report && e.From.Name == "Dashboard" && e.To.Name == "frmParam");
    }

    [Theory]
    [InlineData("TempVars.Add \"repCID\", 1")]            // method-call setter form
    [InlineData("TempVars(\"repCID\") = 1")]                // indexer setter form
    [InlineData("TempVars.Item(\"repCID\") = 1")]           // explicit .Item indexer
    [InlineData("TempVars.Add Name:=\"repCID\", Value:=1")] // named-argument .Add
    public void TempVarSupply_NonBangSetterForm_SuppressesUnmatched(string setter)
    {
        // A TempVar set via a non-bang Access form (TempVars.Add / indexer) still
        // counts as supplied, so its record-source consumer is NOT false-flagged
        // (routed-verifier R1 Major).
        var db = Db(
            reports: [Obj("srptShortfall", "Report", recordSource:
                "SELECT * FROM qryNeeds WHERE ClientID=TempVars!repCID;")],
            queries: [Query("qryNeeds", "SELECT 1;")]);
        var module = CodeBehind($@"
Sub InitContext()
    {setter}
End Sub", "basReportUtils", ModuleKind.Standard);

        var result = NavigationChainBuilder.Build(db, [module]);
        Assert.DoesNotContain(result.UnresolvedTargets, x => x.Reason == "tempvar-no-supplier");
    }

    [Fact]
    public void TempVarSupply_AddWithDynamicNameSlot_DoesNotRecordValueLiteralAsName()
    {
        // `TempVars.Add dynamicName, "repCID"` — the NAME slot is dynamic, so no
        // supplied name is known; the VALUE literal "repCID" must NOT be mistaken
        // for the name and suppress a real consumer of TempVars!repCID
        // (routed-verifier R2 Major).
        var db = Db(
            reports: [Obj("srptShortfall", "Report", recordSource:
                "SELECT * FROM qryNeeds WHERE ClientID=TempVars!repCID;")],
            queries: [Query("qryNeeds", "SELECT 1;")]);
        var module = CodeBehind(@"
Sub InitContext(ByVal memberName As String)
    TempVars.Add memberName, ""repCID""
End Sub", "basReportUtils", ModuleKind.Standard);

        var result = NavigationChainBuilder.Build(db, [module]);
        Assert.Contains(result.UnresolvedTargets, x => x.Reason == "tempvar-no-supplier" && x.Detail == "repCID");
    }

    [Fact]
    public void Build_WithoutModules_DoesNotEmitTempVarNoSupplier()
    {
        // The S2-only path must not flag consumed TempVars as unsupplied — the
        // setters live in code-behind that was not supplied.
        var db = Db(
            reports: [Obj("srptShortfall", "Report", recordSource:
                "SELECT * FROM qryNeeds WHERE ClientID=TempVars!repCID;")],
            queries: [Query("qryNeeds", "SELECT 1;")]);

        var result = NavigationChainBuilder.Build(db);
        Assert.DoesNotContain(result.UnresolvedTargets, x => x.Reason == "tempvar-no-supplier");
    }
}
