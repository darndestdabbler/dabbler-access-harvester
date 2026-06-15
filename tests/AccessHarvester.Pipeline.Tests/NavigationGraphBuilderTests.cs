using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic tests for NavigationGraphBuilder. Inputs are VBA source
// strings parsed via VbaParserPipeline.Parse → BuildAst, scope-built
// via ScopeBuildingVisitor, and resolved via ReferenceResolver. The
// builder itself does not currently consult the resolver outcomes for
// dispatch detection (it walks the AST directly), but resolved modules
// are still produced so the API surface matches FunctionClassification
// and so the cross-reference index is wirable.
public sealed class NavigationGraphBuilderTests
{
    private static ResolvedModule Resolve(
        string source, string moduleName, ModuleKind kind = ModuleKind.Standard)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(project, ast, moduleName, kind);
        return new ReferenceResolver().Resolve(module, ast);
    }

    private static NavigationGraph BuildOne(
        string source, string moduleName = "modNav", ModuleKind kind = ModuleKind.Standard,
        NavigationGraphInputs? inputs = null)
    {
        var resolved = Resolve(source, moduleName, kind);
        var index = CrossReferenceIndex.Build(resolved);
        return NavigationGraphBuilder.Build(new[] { resolved }, index, inputs);
    }

    // ── Argument validation ─────────────────────────────────────────

    [Fact]
    public void Build_NullModules_Throws()
    {
        var index = new CrossReferenceIndex(Array.Empty<ResolvedModule>());
        Assert.Throws<ArgumentNullException>(() =>
            NavigationGraphBuilder.Build(null!, index));
    }

    [Fact]
    public void Build_NullCrossReferences_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NavigationGraphBuilder.Build(Array.Empty<ResolvedModule>(), null!));
    }

    [Fact]
    public void Build_ModulesContainingNull_Throws()
    {
        var index = new CrossReferenceIndex(Array.Empty<ResolvedModule>());
        Assert.Throws<ArgumentNullException>(() =>
            NavigationGraphBuilder.Build(new ResolvedModule?[] { null }!, index));
    }

    // ── Empty / no-dispatch bodies ─────────────────────────────────

    [Fact]
    public void Build_OnEmptyModule_ProducesEmptyGraph()
    {
        var graph = BuildOne(@"
Sub Noop()
End Sub");
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.UnresolvedTargets);
        Assert.Empty(graph.EntryPoints);
        Assert.Empty(graph.Macros);
        Assert.Null(graph.Ribbon);
        Assert.Null(graph.Switchboard);
    }

    [Fact]
    public void Build_OnPureBody_ProducesNoEdges()
    {
        var graph = BuildOne(@"
Function Add(a As Long, b As Long) As Long
    Add = a + b
End Function");
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.UnresolvedTargets);
    }

    // ── OpenForm dispatches ─────────────────────────────────────────

    [Fact]
    public void Build_DoCmdOpenForm_WithLiteral_EmitsEdge()
    {
        var graph = BuildOne(@"
Sub OpenIt()
    DoCmd.OpenForm ""frmCustomers""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal("modNav", edge.SourceModule);
        Assert.Equal("OpenIt", edge.SourceProcedure);
        Assert.Equal(ProcedureKind.Sub, edge.SourceProcedureKind);
        Assert.Equal(NavigationAction.OpenForm, edge.Action);
        Assert.Equal(NavigationTargetKind.Form, edge.TargetKind);
        Assert.Equal("frmCustomers", edge.TargetName);
        Assert.Null(edge.WhereCondition);
        Assert.Null(edge.OpenArgs);
    }

    [Fact]
    public void Build_DoCmdOpenForm_ParenthesisedCallForm_EmitsEdge()
    {
        // The grammar puts parenthesised args on an IndexOrCallExpression
        // wrapping the member access; the builder must unwrap that and
        // produce the same edge as the bare-call form.
        var graph = BuildOne(@"
Sub OpenIt()
    DoCmd.OpenForm(""frmCustomers"")
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal("frmCustomers", edge.TargetName);
        Assert.Equal(NavigationAction.OpenForm, edge.Action);
    }

    [Fact]
    public void Build_DoCmdOpenForm_CaseInsensitiveDispatchName_EmitsEdge()
    {
        var graph = BuildOne(@"
Sub openit()
    docmd.openform ""frmFoo""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal("frmFoo", edge.TargetName);
    }

    [Fact]
    public void Build_DoCmdOpenForm_WithEscapedQuotesInLiteral_UnescapesCorrectly()
    {
        var graph = BuildOne(@"
Sub OpenIt()
    DoCmd.OpenForm ""frm""""Special""""""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal("frm\"Special\"", edge.TargetName);
    }

    [Fact]
    public void Build_DoCmdOpenForm_WithWhereCondition_PreservesText()
    {
        var graph = BuildOne(@"
Sub OpenIt()
    DoCmd.OpenForm ""frmList"", , , ""Status = 'Open'""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal("frmList", edge.TargetName);
        Assert.NotNull(edge.WhereCondition);
        Assert.Contains("Status", edge.WhereCondition!);
    }

    [Fact]
    public void Build_DoCmdOpenForm_WithOpenArgs_PreservesText()
    {
        var graph = BuildOne(@"
Sub OpenIt()
    DoCmd.OpenForm ""frmDetail"", , , , , , ""ctx=42""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal("frmDetail", edge.TargetName);
        Assert.NotNull(edge.OpenArgs);
        Assert.Contains("ctx=42", edge.OpenArgs!);
    }

    [Fact]
    public void Build_DoCmdOpenForm_WithDynamicTarget_ProducesUnresolvedEntry()
    {
        var graph = BuildOne(@"
Sub OpenIt(name As String)
    DoCmd.OpenForm name
End Sub");
        Assert.Empty(graph.Edges);
        var u = Assert.Single(graph.UnresolvedTargets);
        Assert.Equal(NavigationAction.OpenForm, u.Action);
        Assert.Equal("non-literal-target", u.Reason);
        Assert.Equal("name", u.TargetExpressionText);
    }

    [Fact]
    public void Build_DoCmdOpenForm_WithConcatenatedTarget_RecordsConcatenationText()
    {
        var graph = BuildOne(@"
Sub OpenIt(prefix As String)
    DoCmd.OpenForm prefix & ""Detail""
End Sub");
        Assert.Empty(graph.Edges);
        var u = Assert.Single(graph.UnresolvedTargets);
        Assert.Equal("prefix & \"Detail\"", u.TargetExpressionText);
    }

    [Fact]
    public void Build_DoCmdOpenForm_WithFunctionCallTarget_RecordsCallText()
    {
        var graph = BuildOne(@"
Sub OpenIt()
    DoCmd.OpenForm GetTarget()
End Sub
Function GetTarget() As String
    GetTarget = ""frmFoo""
End Function");
        // The dispatch site cannot be statically resolved even though
        // GetTarget happens to return a literal — we deliberately do not
        // do constant-folding here per Lean By Default.
        Assert.Empty(graph.Edges);
        var u = Assert.Single(graph.UnresolvedTargets);
        Assert.Equal("GetTarget()", u.TargetExpressionText);
    }

    // ── OpenReport dispatches ───────────────────────────────────────

    [Fact]
    public void Build_DoCmdOpenReport_TargetKindIsReport()
    {
        var graph = BuildOne(@"
Sub Run()
    DoCmd.OpenReport ""rptCaseload"", acViewPreview
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(NavigationAction.OpenReport, edge.Action);
        Assert.Equal(NavigationTargetKind.Report, edge.TargetKind);
        Assert.Equal("rptCaseload", edge.TargetName);
    }

    [Fact]
    public void Build_DoCmdOpenReport_WithWhereConditionAtFourthSlot_PreservesText()
    {
        // OpenReport: ReportName, View, FilterName, WhereCondition, ...
        // Index 3 is WhereCondition (FilterName at index 2 is skipped).
        var graph = BuildOne(@"
Sub Run()
    DoCmd.OpenReport ""rptList"", acViewPreview, , ""Status = 'Open'""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.NotNull(edge.WhereCondition);
        Assert.Contains("Status", edge.WhereCondition!);
    }

    // ── Close dispatches ────────────────────────────────────────────

    [Fact]
    public void Build_DoCmdClose_WithExplicitFormAndName_EmitsEdge()
    {
        var graph = BuildOne(@"
Sub Done()
    DoCmd.Close acForm, ""frmDetail""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(NavigationAction.Close, edge.Action);
        Assert.Equal(NavigationTargetKind.Form, edge.TargetKind);
        Assert.Equal("frmDetail", edge.TargetName);
    }

    [Fact]
    public void Build_DoCmdClose_WithExplicitReportAndName_EmitsReportEdge()
    {
        var graph = BuildOne(@"
Sub Done()
    DoCmd.Close acReport, ""rptList""
End Sub");
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(NavigationTargetKind.Report, edge.TargetKind);
        Assert.Equal("rptList", edge.TargetName);
    }

    [Fact]
    public void Build_DoCmdClose_WithNoArgs_ProducesNoEdgeOrUnresolved()
    {
        // `DoCmd.Close` (no args) closes the active object — there is
        // no target to record. The dispatch is silently skipped: not
        // an error, not an unresolved target, just absence.
        var graph = BuildOne(@"
Sub Done()
    DoCmd.Close
End Sub");
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.UnresolvedTargets);
    }

    [Fact]
    public void Build_DoCmdClose_WithDynamicName_ProducesUnresolved()
    {
        var graph = BuildOne(@"
Sub Done(name As String)
    DoCmd.Close acForm, name
End Sub");
        Assert.Empty(graph.Edges);
        var u = Assert.Single(graph.UnresolvedTargets);
        Assert.Equal(NavigationAction.Close, u.Action);
    }

    // ── BrowseTo dispatches ─────────────────────────────────────────

    [Fact]
    public void Build_DoCmdBrowseTo_TargetKindIsForm()
    {
        var graph = BuildOne(@"
Sub Nav()
    DoCmd.BrowseTo acBrowseToForm, ""frmFoo""
End Sub");
        // BrowseTo's first positional argument is the object kind
        // (acBrowseToForm), the second is the form name. Currently the
        // builder treats the first positional as the target name —
        // this test pins that current behaviour and surfaces the gap
        // for future refinement when the BrowseTo overload set is
        // worth modelling. The test asserts on the raw outcome.
        // (BrowseTo enum constant text 'acBrowseToForm' is not a
        // string literal, so the dispatch goes to UnresolvedTargets.)
        Assert.Empty(graph.Edges);
        var u = Assert.Single(graph.UnresolvedTargets);
        Assert.Equal(NavigationAction.BrowseTo, u.Action);
    }

    // ── Switchboard fixture (mirrors test case 12) ─────────────────

    [Fact]
    public void Build_OnSwitchboardFormFixture_ProducesEdgePerClickHandler()
    {
        // Mirrors fixture 12-switchboard-navigation.bas: each click
        // handler opens a different form/report. The builder should
        // emit one edge per dispatch with the correct source procedure.
        const string src = @"
Private Sub btnNewCase_Click()
    DoCmd.OpenForm ""frmCaseDetail""
End Sub
Private Sub btnSearchCases_Click()
    DoCmd.OpenForm ""frmCaseSearch""
End Sub
Private Sub btnReports_Click()
    DoCmd.OpenForm ""frmReportMenu""
End Sub
Private Sub btnQuickCaseload_Click()
    DoCmd.OpenReport ""rptCaseLoadByDistrict"", acViewPreview
End Sub
Private Sub btnAuditLog_Click()
    DoCmd.OpenForm ""frmAuditLogViewer""
End Sub";
        var graph = BuildOne(src, moduleName: "Form_frmMainSwitchboard", kind: ModuleKind.Form);
        Assert.Equal(5, graph.Edges.Count);
        // Each edge wired to its specific handler:
        Assert.Contains(graph.Edges, e =>
            e.SourceProcedure == "btnNewCase_Click" && e.TargetName == "frmCaseDetail");
        Assert.Contains(graph.Edges, e =>
            e.SourceProcedure == "btnSearchCases_Click" && e.TargetName == "frmCaseSearch");
        Assert.Contains(graph.Edges, e =>
            e.SourceProcedure == "btnReports_Click" && e.TargetName == "frmReportMenu");
        Assert.Contains(graph.Edges, e =>
            e.SourceProcedure == "btnQuickCaseload_Click"
            && e.TargetName == "rptCaseLoadByDistrict"
            && e.Action == NavigationAction.OpenReport);
        Assert.Contains(graph.Edges, e =>
            e.SourceProcedure == "btnAuditLog_Click" && e.TargetName == "frmAuditLogViewer");
    }

    [Fact]
    public void Build_OnRoleBasedDispatch_ProducesOneEdgePerCaseArm()
    {
        // Mirrors the Initialize-from-fixture-11 pattern: a Select Case
        // on g_UserRole opens five different forms. The builder emits
        // one OpenForm edge per arm, all under the same source
        // procedure.
        const string src = @"
Public Function Initialize() As Boolean
    Select Case g_UserRole
        Case ""Administrator""
            DoCmd.OpenForm ""frmAdminSwitchboard""
        Case ""Caseworker""
            DoCmd.OpenForm ""frmCaseworkerHome""
        Case ""Supervisor""
            DoCmd.OpenForm ""frmSupervisorDashboard""
        Case ""ReadOnly""
            DoCmd.OpenForm ""frmReadOnlyView""
        Case Else
            DoCmd.OpenForm ""frmMainSwitchboard""
    End Select
    Initialize = True
End Function";
        var graph = BuildOne(src, moduleName: "mdlStartup");
        Assert.Equal(5, graph.Edges.Count);
        Assert.All(graph.Edges, e =>
        {
            Assert.Equal("Initialize", e.SourceProcedure);
            Assert.Equal(NavigationAction.OpenForm, e.Action);
        });
        var names = graph.Edges.Select(e => e.TargetName).ToList();
        Assert.Contains("frmAdminSwitchboard", names);
        Assert.Contains("frmCaseworkerHome", names);
        Assert.Contains("frmSupervisorDashboard", names);
        Assert.Contains("frmReadOnlyView", names);
        Assert.Contains("frmMainSwitchboard", names);
    }

    // ── Source procedure kind dispatch ────────────────────────────

    [Theory]
    [InlineData(@"Sub S()
    DoCmd.OpenForm ""f""
End Sub", "S", ProcedureKind.Sub)]
    [InlineData(@"Function F() As Long
    DoCmd.OpenForm ""f""
End Function", "F", ProcedureKind.Function)]
    [InlineData(@"Property Get P() As String
    DoCmd.OpenForm ""f""
End Property", "P", ProcedureKind.PropertyGet)]
    [InlineData(@"Property Let P(v As String)
    DoCmd.OpenForm ""f""
End Property", "P", ProcedureKind.PropertyLet)]
    [InlineData(@"Property Set P(v As Object)
    DoCmd.OpenForm ""f""
End Property", "P", ProcedureKind.PropertySet)]
    public void Build_RecordsSourceProcedureKindForEachKind(
        string source, string expectedProcedure, ProcedureKind expectedKind)
    {
        var graph = BuildOne(source);
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(expectedProcedure, edge.SourceProcedure);
        Assert.Equal(expectedKind, edge.SourceProcedureKind);
    }

    // ── Cross-module aggregation ──────────────────────────────────

    [Fact]
    public void Build_AcrossMultipleModules_AggregatesEdgesAndSortsByModuleThenProc()
    {
        var rmZ = Resolve(@"
Sub OpenZ()
    DoCmd.OpenForm ""frmZ""
End Sub", "modZ");
        var rmA = Resolve(@"
Sub OpenA1()
    DoCmd.OpenForm ""frmA1""
End Sub
Sub OpenA2()
    DoCmd.OpenForm ""frmA2""
End Sub", "modA");
        var index = new CrossReferenceIndex(new[] { rmA, rmZ });
        var graph = NavigationGraphBuilder.Build(new[] { rmZ, rmA }, index);

        Assert.Equal(3, graph.Edges.Count);
        // Sorted by (module, proc, line, column) — modA < modZ;
        // OpenA1 < OpenA2 within modA.
        Assert.Equal("modA", graph.Edges[0].SourceModule);
        Assert.Equal("OpenA1", graph.Edges[0].SourceProcedure);
        Assert.Equal("modA", graph.Edges[1].SourceModule);
        Assert.Equal("OpenA2", graph.Edges[1].SourceProcedure);
        Assert.Equal("modZ", graph.Edges[2].SourceModule);
    }

    [Fact]
    public void Build_IsDeterministic_SameInputProducesSameEdgeSequence()
    {
        const string src = @"
Sub A()
    DoCmd.OpenForm ""frm1""
    DoCmd.OpenForm ""frm2""
End Sub";
        var g1 = BuildOne(src);
        var g2 = BuildOne(src);
        Assert.Equal(g1.Edges.Count, g2.Edges.Count);
        for (int i = 0; i < g1.Edges.Count; i++)
        {
            Assert.Equal(g1.Edges[i].SourceLine, g2.Edges[i].SourceLine);
            Assert.Equal(g1.Edges[i].TargetName, g2.Edges[i].TargetName);
        }
    }

    // ── Non-DoCmd calls are ignored ────────────────────────────────

    [Fact]
    public void Build_NonDoCmdMemberCall_DoesNotEmitEdge()
    {
        var graph = BuildOne(@"
Sub Use()
    Forms!frmFoo.Requery
End Sub");
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.UnresolvedTargets);
    }

    [Fact]
    public void Build_BareIdentifierCall_DoesNotEmitEdge()
    {
        var graph = BuildOne(@"
Sub Use()
    OpenForm ""frmFoo""
End Sub");
        // OpenForm without DoCmd qualifier is just an unresolved bare
        // call (or a user-defined sub of the same name). Either way
        // not a DoCmd dispatch.
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.UnresolvedTargets);
    }

    [Fact]
    public void Build_DoCmdNonNavigationVerb_IsIgnored()
    {
        var graph = BuildOne(@"
Sub Setup()
    DoCmd.SetWarnings False
End Sub");
        // SetWarnings is a DoCmd verb but not a navigation one;
        // the builder ignores it cleanly.
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.UnresolvedTargets);
    }

    // ── Inputs: macros / switchboard / default form ────────────────

    [Fact]
    public void Build_WithAutoExecMacroChain_ProducesAutoExecEntryPoint()
    {
        var inputs = new NavigationGraphInputs
        {
            Macros = new[]
            {
                new MacroChain
                {
                    Name = "AutoExec",
                    Actions = new[]
                    {
                        new MacroAction
                        {
                            ActionName = "RunCode",
                            Arguments = new Dictionary<string, string>
                            {
                                ["FunctionName"] = "Initialize()",
                            },
                            CallsProcedureName = "Initialize",
                        },
                    },
                },
            },
        };
        var graph = BuildOne(@"
Sub Noop()
End Sub", inputs: inputs);
        var ep = Assert.Single(graph.EntryPoints);
        Assert.Equal(EntryPointKind.AutoExec, ep.Kind);
        Assert.Equal("AutoExec", ep.Name);
        Assert.NotNull(ep.CallsProcedure);
        Assert.Equal("Initialize", ep.CallsProcedure!.ProcedureName);
        // Macro is also exposed in the macros list:
        var macro = Assert.Single(graph.Macros);
        Assert.Equal("AutoExec", macro.Name);
    }

    [Fact]
    public void Build_WithDefaultFormAndSwitchboard_OrdersEntryPointsCorrectly()
    {
        var inputs = new NavigationGraphInputs
        {
            DefaultFormName = "frmStartup",
            Switchboard = new SwitchboardStructure
            {
                Pages = new[]
                {
                    new SwitchboardPage
                    {
                        SwitchboardId = 1,
                        Table = "Switchboard Items",
                        Name = "Main Menu",
                        Items = Array.Empty<SwitchboardItem>(),
                    },
                },
            },
        };
        var graph = BuildOne(@"
Sub Noop()
End Sub", inputs: inputs);
        // Default form first, then switchboard root. AutoExec absent.
        Assert.Equal(2, graph.EntryPoints.Count);
        Assert.Equal(EntryPointKind.DefaultForm, graph.EntryPoints[0].Kind);
        Assert.Equal("frmStartup", graph.EntryPoints[0].Name);
        Assert.Equal(EntryPointKind.SwitchboardRoot, graph.EntryPoints[1].Kind);
        Assert.Equal("Main Menu", graph.EntryPoints[1].Name);
    }

    [Fact]
    public void Build_WithAutoExecAndDefaultFormAndSwitchboard_OrdersAllThreeKinds()
    {
        var inputs = new NavigationGraphInputs
        {
            DefaultFormName = "frmStartup",
            Macros = new[]
            {
                new MacroChain
                {
                    Name = "AutoExec",
                    Actions = new[]
                    {
                        new MacroAction
                        {
                            ActionName = "OpenForm",
                            Arguments = new Dictionary<string, string>
                            {
                                ["FormName"] = "frmAutoOpen",
                            },
                            CallsProcedureName = null,
                        },
                    },
                },
            },
            Switchboard = new SwitchboardStructure
            {
                Pages = new[]
                {
                    new SwitchboardPage
                    {
                        SwitchboardId = 1, Table = "Switchboard Items", Name = "Main", Items = Array.Empty<SwitchboardItem>(),
                    },
                },
            },
        };
        var graph = BuildOne(@"
Sub Noop()
End Sub", inputs: inputs);
        Assert.Equal(3, graph.EntryPoints.Count);
        Assert.Equal(EntryPointKind.AutoExec, graph.EntryPoints[0].Kind);
        Assert.Equal("frmAutoOpen", graph.EntryPoints[0].OpensFormName);
        Assert.Equal(EntryPointKind.DefaultForm, graph.EntryPoints[1].Kind);
        Assert.Equal(EntryPointKind.SwitchboardRoot, graph.EntryPoints[2].Kind);
    }

    [Fact]
    public void Build_WithRibbonInput_PassesRibbonThrough()
    {
        var ribbon = new RibbonStructure
        {
            Tabs = new[]
            {
                new RibbonTab
                {
                    Id = "tabHome", Label = "Home",
                    Groups = new[]
                    {
                        new RibbonGroup
                        {
                            Id = "grpNav", Label = "Navigation",
                            Controls = new[]
                            {
                                new RibbonControl
                                {
                                    ControlType = "button",
                                    Id = "btnHome", Label = "Home",
                                    OnAction = "OpenHome",
                                },
                            },
                        },
                    },
                },
            },
        };
        var inputs = new NavigationGraphInputs { Ribbon = ribbon };
        var graph = BuildOne(@"
Sub Noop()
End Sub", inputs: inputs);
        Assert.NotNull(graph.Ribbon);
        Assert.Equal("tabHome", graph.Ribbon!.Tabs[0].Id);
    }

    [Fact]
    public void Build_WithSwitchboardInput_PassesSwitchboardThrough()
    {
        var sb = new SwitchboardStructure
        {
            Pages = new[]
            {
                new SwitchboardPage
                {
                    SwitchboardId = 1, Table = "Switchboard Items", Name = "Main",
                    Items = new[]
                    {
                        new SwitchboardItem
                        {
                            Text = "Open Cases",
                            Command = 3,
                            Argument = "frmCases",
                        },
                    },
                },
            },
        };
        var inputs = new NavigationGraphInputs { Switchboard = sb };
        var graph = BuildOne(@"
Sub Noop()
End Sub", inputs: inputs);
        Assert.NotNull(graph.Switchboard);
        Assert.Equal("frmCases", graph.Switchboard!.Pages[0].Items[0].Argument);
    }

    [Fact]
    public void Build_WithoutInputs_LeavesTopLevelSurfacesNull()
    {
        var graph = BuildOne(@"
Sub OpenIt()
    DoCmd.OpenForm ""frmFoo""
End Sub");
        Assert.Null(graph.Ribbon);
        Assert.Null(graph.Switchboard);
        Assert.Empty(graph.Macros);
        Assert.Empty(graph.EntryPoints);
        // VBA-side edges still produced:
        Assert.Single(graph.Edges);
    }

    // ── Ribbon entry points (016 S2 / DW-13) ──────────────────────────

    // A single control whose onAction names the given callback, inside one
    // tab/group, so the ribbon-callback resolution path is exercised directly.
    private static RibbonStructure RibbonWithButton(string onAction,
        string controlId = "btnFoo", string label = "Foo", string controlType = "button")
        => new()
        {
            Tabs = new[]
            {
                new RibbonTab
                {
                    Id = "tHome", Label = "Home",
                    Groups = new[]
                    {
                        new RibbonGroup
                        {
                            Id = "gNav", Label = "Nav",
                            Controls = new[]
                            {
                                new RibbonControl
                                {
                                    ControlType = controlType, Id = controlId,
                                    Label = label, OnAction = onAction,
                                },
                            },
                        },
                    },
                },
            },
        };

    private static NavigationGraph BuildWithModules(
        NavigationGraphInputs? inputs, params (string Source, string Name)[] modules)
    {
        var resolved = modules.Select(m => Resolve(m.Source, m.Name)).ToArray();
        var index = new CrossReferenceIndex(resolved);
        return NavigationGraphBuilder.Build(resolved, index, inputs);
    }

    [Fact]
    public void Build_NoRibbon_RibbonEntryPointsNull()
    {
        // No ribbon -> the facet is omitted entirely (like Ribbon), so a
        // non-ribbon corpus (HHC) stays byte-neutral.
        var graph = BuildOne(@"
Sub Whatever()
End Sub");
        Assert.Null(graph.Ribbon);
        Assert.Null(graph.RibbonEntryPoints);
    }

    [Fact]
    public void Build_RibbonOnAction_MatchingProcedure_Resolves()
    {
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("cmdAbout_OnAction") };
        var graph = BuildWithModules(inputs,
            (@"
Sub cmdAbout_OnAction(control As IRibbonControl)
End Sub", "modRibbonCallback"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal(RibbonCallbackKind.OnAction, ep.Kind);
        Assert.Equal("cmdAbout_OnAction", ep.CallbackName);
        Assert.Equal("button", ep.ControlType);
        Assert.Equal("btnFoo", ep.ControlId);
        Assert.Equal("Foo", ep.ControlLabel);
        Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
        Assert.NotNull(ep.ResolvedProcedure);
        Assert.Equal("modRibbonCallback", ep.ResolvedProcedure!.ModuleName);
        Assert.Equal("cmdAbout_OnAction", ep.ResolvedProcedure.ProcedureName);
    }

    [Fact]
    public void Build_RibbonOnAction_NoMatchingProcedure_IsExplicitUnresolved()
    {
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("cmdGhost_OnAction") };
        var graph = BuildWithModules(inputs,
            (@"
Sub SomethingElse()
End Sub", "modOther"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal("cmdGhost_OnAction", ep.CallbackName); // preserved verbatim
        Assert.Equal(RibbonCallbackResolution.Unresolved, ep.Resolution);
        Assert.Null(ep.ResolvedProcedure);
    }

    [Fact]
    public void Build_RibbonOnAction_NameInMultipleModules_IsAmbiguousNoGuess()
    {
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("Shared_OnAction") };
        var graph = BuildWithModules(inputs,
            (@"
Sub Shared_OnAction(control As IRibbonControl)
End Sub", "modA"),
            (@"
Sub Shared_OnAction(control As IRibbonControl)
End Sub", "modB"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal(RibbonCallbackResolution.Ambiguous, ep.Resolution);
        Assert.Null(ep.ResolvedProcedure); // never picks a module
    }

    [Fact]
    public void Build_RibbonOnAction_CaseInsensitiveMatch_PreservesDeclaredCasing()
    {
        // VBA is case-insensitive: a lower-case onAction resolves to the
        // mixed-case declaration, and ResolvedProcedure carries the DECLARED
        // casing while CallbackName keeps the authored casing.
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("openhome") };
        var graph = BuildWithModules(inputs,
            (@"
Sub OpenHome()
End Sub", "modNav"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
        Assert.Equal("openhome", ep.CallbackName);
        Assert.Equal("OpenHome", ep.ResolvedProcedure!.ProcedureName);
    }

    [Fact]
    public void Build_RibbonOnLoad_IsFirstAndResolves()
    {
        var ribbon = RibbonWithButton("cmdFoo_OnAction") with { OnLoad = "OnRibbonLoad" };
        var inputs = new NavigationGraphInputs { Ribbon = ribbon };
        var graph = BuildWithModules(inputs,
            (@"
Sub OnRibbonLoad(ribbon As IRibbonUI)
End Sub
Sub cmdFoo_OnAction(control As IRibbonControl)
End Sub", "modRibbonCallback"));

        Assert.Equal(2, graph.RibbonEntryPoints!.Count);
        // onLoad is emitted first (deterministic source order).
        var onLoad = graph.RibbonEntryPoints![0];
        Assert.Equal(RibbonCallbackKind.OnLoad, onLoad.Kind);
        Assert.Equal("OnRibbonLoad", onLoad.CallbackName);
        Assert.Null(onLoad.ControlType); // root callback belongs to no control
        Assert.Null(onLoad.ControlId);
        Assert.Equal(RibbonCallbackResolution.Resolved, onLoad.Resolution);
        Assert.Equal("modRibbonCallback", onLoad.ResolvedProcedure!.ModuleName);

        Assert.Equal(RibbonCallbackKind.OnAction, graph.RibbonEntryPoints![1].Kind);
    }

    [Fact]
    public void Build_RibbonNestedChildOnAction_IsSurfacedInDepthFirstOrder()
    {
        // splitButton with its own onAction and a nested menu button onAction:
        // the parent surfaces before the child (DFS, source order).
        var ribbon = new RibbonStructure
        {
            Tabs = new[]
            {
                new RibbonTab
                {
                    Id = "tHome",
                    Groups = new[]
                    {
                        new RibbonGroup
                        {
                            Id = "gNav",
                            Controls = new[]
                            {
                                new RibbonControl
                                {
                                    ControlType = "splitButton", Id = "sbParent",
                                    OnAction = "Parent_OnAction",
                                    Children = new[]
                                    {
                                        new RibbonControl
                                        {
                                            ControlType = "button", Id = "bChild",
                                            OnAction = "Child_OnAction",
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
        var inputs = new NavigationGraphInputs { Ribbon = ribbon };
        var graph = BuildWithModules(inputs,
            (@"
Sub Parent_OnAction(control As IRibbonControl)
End Sub
Sub Child_OnAction(control As IRibbonControl)
End Sub", "modCb"));

        Assert.Equal(2, graph.RibbonEntryPoints!.Count);
        Assert.Equal("Parent_OnAction", graph.RibbonEntryPoints![0].CallbackName);
        Assert.Equal("Child_OnAction", graph.RibbonEntryPoints![1].CallbackName);
    }

    [Fact]
    public void Build_RibbonContextualTabControlOnAction_IsSurfaced()
    {
        var ribbon = new RibbonStructure
        {
            ContextualTabSets = new[]
            {
                new RibbonContextualTabSet
                {
                    Id = "ctxSet",
                    Tabs = new[]
                    {
                        new RibbonTab
                        {
                            Id = "ctxTab",
                            Groups = new[]
                            {
                                new RibbonGroup
                                {
                                    Id = "ctxGrp",
                                    Controls = new[]
                                    {
                                        new RibbonControl
                                        {
                                            ControlType = "button", Id = "ctxBtn",
                                            OnAction = "Ctx_OnAction",
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
        var inputs = new NavigationGraphInputs { Ribbon = ribbon };
        var graph = BuildWithModules(inputs,
            (@"
Sub Ctx_OnAction(control As IRibbonControl)
End Sub", "modCb"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal("Ctx_OnAction", ep.CallbackName);
        Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
    }

    [Fact]
    public void Build_RibbonPresentButNoCallbacks_EmptyNotNull()
    {
        // A ribbon with no onAction/onLoad anywhere -> an empty (but non-null)
        // list: the ribbon exists, it just contributes no callbacks. (Null is
        // reserved for "no ribbon at all".)
        var ribbon = new RibbonStructure
        {
            Tabs = new[]
            {
                new RibbonTab { IdMso = "TabHome" }, // built-in tab, no controls
            },
        };
        var inputs = new NavigationGraphInputs { Ribbon = ribbon };
        var graph = BuildWithModules(inputs, (@"
Sub Noop()
End Sub", "modNav"));

        Assert.NotNull(graph.RibbonEntryPoints);
        Assert.Empty(graph.RibbonEntryPoints!);
    }

    // ── Bindability gate (016 S2 set-wide review, F1/A1) ──────────────

    [Fact]
    public void Build_RibbonOnAction_NameAlsoInClassModule_ResolvesToStandardOnly()
    {
        // A ribbon callback binds only to a standard-module procedure. A
        // same-named sub in a form/report class module is NOT a binding
        // candidate, so it must not turn the unique standard-module match into
        // a false Ambiguous.
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("cmdAbout_OnAction") };
        var resolvedStd = Resolve(@"
Sub cmdAbout_OnAction(control As IRibbonControl)
End Sub", "modRibbonCallback", ModuleKind.Standard);
        var resolvedClass = Resolve(@"
Public Sub cmdAbout_OnAction()
End Sub", "Form_frmAbout", ModuleKind.Form);
        var index = new CrossReferenceIndex(new[] { resolvedStd, resolvedClass });
        var graph = NavigationGraphBuilder.Build(new[] { resolvedStd, resolvedClass }, index, inputs);

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
        Assert.Equal("modRibbonCallback", ep.ResolvedProcedure!.ModuleName);
    }

    [Fact]
    public void Build_RibbonOnAction_NameAlsoPrivateInOtherStandardModule_ResolvesToPublic()
    {
        // A Private procedure cannot be a ribbon-callback target, so a unique
        // Public match elsewhere resolves cleanly rather than being masked as
        // Ambiguous.
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("Shared_OnAction") };
        var graph = BuildWithModules(inputs,
            (@"
Public Sub Shared_OnAction(control As IRibbonControl)
End Sub", "modPublic"),
            (@"
Private Sub Shared_OnAction(control As IRibbonControl)
End Sub", "modPrivate"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
        Assert.Equal("modPublic", ep.ResolvedProcedure!.ModuleName);
    }

    [Fact]
    public void Build_RibbonOnAction_OnlyPrivateMatch_IsUnresolved()
    {
        // If the ONLY same-named procedure is Private (non-bindable), the
        // callback is Unresolved — Access could not dispatch to it either —
        // and the verbatim name is still retained.
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("cmdHidden_OnAction") };
        var graph = BuildWithModules(inputs,
            (@"
Private Sub cmdHidden_OnAction(control As IRibbonControl)
End Sub", "modStd"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal(RibbonCallbackResolution.Unresolved, ep.Resolution);
        Assert.Equal("cmdHidden_OnAction", ep.CallbackName);
        Assert.Null(ep.ResolvedProcedure);
    }

    [Fact]
    public void Build_RibbonOnAction_ModifierlessSubIsBindable()
    {
        // A modifier-less Sub in a standard module is Public by default, so it
        // is a bindable target (the most common authored shape).
        var inputs = new NavigationGraphInputs { Ribbon = RibbonWithButton("cmdGo_OnAction") };
        var graph = BuildWithModules(inputs,
            (@"
Sub cmdGo_OnAction(control As IRibbonControl)
End Sub", "modStd"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
        Assert.Equal("modStd", ep.ResolvedProcedure!.ModuleName);
    }

    [Fact]
    public void Build_RibbonBuiltInControlOnAction_SurfacesIdMso()
    {
        // A built-in idMso control can carry an onAction override; the metadata
        // surfaces IdMso (and a null custom Id).
        var ribbon = new RibbonStructure
        {
            Tabs = new[]
            {
                new RibbonTab
                {
                    Id = "tHome",
                    Groups = new[]
                    {
                        new RibbonGroup
                        {
                            Id = "gNav",
                            Controls = new[]
                            {
                                new RibbonControl
                                {
                                    ControlType = "button", IdMso = "FileSave",
                                    OnAction = "Save_OnAction",
                                },
                            },
                        },
                    },
                },
            },
        };
        var inputs = new NavigationGraphInputs { Ribbon = ribbon };
        var graph = BuildWithModules(inputs, (@"
Sub Save_OnAction(control As IRibbonControl)
End Sub", "modCb"));

        var ep = Assert.Single(graph.RibbonEntryPoints!);
        Assert.Null(ep.ControlId);
        Assert.Equal("FileSave", ep.ControlIdMso);
        Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
    }
}
