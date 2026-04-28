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
using ParserProcedure = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic unit tests for FunctionClassificationBuilder. Inputs are
// VBA source strings parsed via VbaParserPipeline.Parse -> BuildAst,
// scope-built via ScopeBuildingVisitor, resolved via ReferenceResolver,
// then handed to FunctionClassificationBuilder. The tests exercise both
// the classification visitor and the cross-reference dependency edges.
public sealed class FunctionClassificationBuilderTests
{
    private static (ResolvedModule resolved, Module ast) Resolve(
        string source, string moduleName, ModuleKind kind = ModuleKind.Standard)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(project, ast, moduleName, kind);
        var resolved = new ReferenceResolver().Resolve(module, ast);
        return (resolved, ast);
    }

    private static ParserProcedure Procedure(Module ast, string name)
    {
        foreach (var item in ast.Items)
        {
            if (item is ParserProcedure p
                && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        throw new InvalidOperationException(
            $"No procedure '{name}' in module — fixture defect.");
    }

    private static FunctionClassification ClassifyOne(
        string source, string procedureName,
        string moduleName = "modDemo", ModuleKind kind = ModuleKind.Standard)
    {
        var (resolved, ast) = Resolve(source, moduleName, kind);
        var procAst = Procedure(ast, procedureName);
        var index = CrossReferenceIndex.Build(resolved);
        return FunctionClassificationBuilder.Build(resolved, procAst, index);
    }

    // ── Identity facets ─────────────────────────────────────────────

    [Fact]
    public void Build_OnPlainSub_ReportsKindNameModuleAndSignatureFacets()
    {
        const string src = @"
Public Sub Greet(ByVal who As String)
    Dim msg As String
    msg = ""hi "" & who
End Sub";
        var c = ClassifyOne(src, "Greet");

        Assert.Equal("modDemo", c.ModuleName);
        Assert.Equal("Greet", c.ProcedureName);
        Assert.Equal(ProcedureKind.Sub, c.Kind);
        Assert.True(c.IsPublic);
        Assert.False(c.IsStatic);
        Assert.Equal(1, c.ParameterCount);
        Assert.False(c.HasReturnValue);
    }

    [Fact]
    public void Build_OnFunction_ReportsHasReturnValueTrue()
    {
        const string src = @"
Function Sum(a As Long, b As Long) As Long
    Sum = a + b
End Function";
        var c = ClassifyOne(src, "Sum");
        Assert.Equal(ProcedureKind.Function, c.Kind);
        Assert.True(c.HasReturnValue);
        Assert.Equal(2, c.ParameterCount);
    }

    [Fact]
    public void Build_OnPropertyGetLetSet_DiscriminatesEachKind()
    {
        const string src = @"
Public Property Get Name() As String
    Name = """"
End Property
Public Property Let Name(ByVal v As String)
End Property
Public Property Set Target(ByVal v As Object)
End Property";
        var (resolved, ast) = Resolve(src, "modProps");
        var index = CrossReferenceIndex.Build(resolved);
        var get = FunctionClassificationBuilder.Build(resolved, Procedure(ast, "Name"), index);
        var setObj = FunctionClassificationBuilder.Build(resolved, Procedure(ast, "Target"), index);

        // Two procedures named "Name" exist (Get and Let). Procedure() returns the first
        // matching by source order — the Get. The Let / Set discrimination is exercised by
        // walking the AST directly so all three kinds are covered.
        Assert.Equal(ProcedureKind.PropertyGet, get.Kind);
        Assert.True(get.HasReturnValue);

        Assert.Equal(ProcedureKind.PropertySet, setObj.Kind);
        Assert.False(setObj.HasReturnValue);

        // The Let accessor: pick it explicitly via AST index.
        var letAst = ast.Items.OfType<PropertyProcedure>().First(p => p.Kind == PropertyKind.Let);
        var let = FunctionClassificationBuilder.Build(resolved, letAst, index);
        Assert.Equal(ProcedureKind.PropertyLet, let.Kind);
        Assert.False(let.HasReturnValue);
    }

    // ── Pure classification ─────────────────────────────────────────

    [Fact]
    public void Build_OnPureBody_ReportsPureCategoryAndIsMixedFalse()
    {
        const string src = @"
Function Compute(x As Long, y As Long) As Long
    Dim r As Long
    r = x * 2 + y
    Compute = r
End Function";
        var c = ClassifyOne(src, "Compute");
        Assert.Single(c.Categories);
        Assert.Equal(ClassificationCategory.Pure, c.Categories[0]);
        Assert.Equal(ClassificationCategory.Pure, c.PrimaryCategory);
        Assert.False(c.IsMixed);
    }

    [Fact]
    public void Build_OnBuiltInStringMath_StillClassifiesAsPure()
    {
        // String/Math/DateTime/Information/Conversion built-ins carry no
        // side-effect signal — using Format/Len/Abs should not flip the
        // body off Pure.
        const string src = @"
Function Title(s As String) As String
    Title = UCase(Left(s, 1)) & LCase(Mid(s, 2))
End Function";
        var c = ClassifyOne(src, "Title");
        Assert.Equal(ClassificationCategory.Pure, c.PrimaryCategory);
        Assert.False(c.IsMixed);
    }

    // ── DataAccess classification ───────────────────────────────────

    [Fact]
    public void Build_WithDLookup_ClassifiesAsDataAccess()
    {
        const string src = @"
Function GetClientName(id As Long) As Variant
    GetClientName = DLookup(""Name"", ""tblClients"", ""ID="" & id)
End Function";
        var c = ClassifyOne(src, "GetClientName");
        Assert.Contains(ClassificationCategory.DataAccess, c.Categories);
        Assert.Equal(ClassificationCategory.DataAccess, c.PrimaryCategory);
        Assert.False(c.IsMixed);
    }

    [Fact]
    public void Build_WithCurrentDb_ClassifiesAsDataAccess()
    {
        const string src = @"
Sub Refresh()
    Dim db As Object
    Set db = CurrentDb
End Sub";
        var c = ClassifyOne(src, "Refresh");
        // CurrentDb is the data signal; the late-bound `db` later is a
        // separate concern (no member-access here, just assignment).
        Assert.Contains(ClassificationCategory.DataAccess, c.Categories);
    }

    // ── UI classification ───────────────────────────────────────────

    [Fact]
    public void Build_WithMsgBox_ClassifiesAsUIBound()
    {
        const string src = @"
Sub Notify(msg As String)
    MsgBox msg
End Sub";
        var c = ClassifyOne(src, "Notify");
        Assert.Contains(ClassificationCategory.UIBound, c.Categories);
        Assert.Equal(ClassificationCategory.UIBound, c.PrimaryCategory);
        Assert.False(c.IsMixed);
    }

    [Fact]
    public void Build_WithDoCmd_ClassifiesAsUIBound()
    {
        const string src = @"
Sub OpenIt()
    DoCmd.OpenForm ""frmClients""
End Sub";
        var c = ClassifyOne(src, "OpenIt");
        Assert.Contains(ClassificationCategory.UIBound, c.Categories);
    }

    // ── ExternalIO classification ───────────────────────────────────

    [Fact]
    public void Build_WithFreeFile_ClassifiesAsExternalIO()
    {
        const string src = @"
Sub WriteLog(line As String)
    Dim handle As Integer
    handle = FreeFile
End Sub";
        var c = ClassifyOne(src, "WriteLog");
        Assert.Contains(ClassificationCategory.ExternalIO, c.Categories);
    }

    [Fact]
    public void Build_WithShell_ClassifiesAsExternalIONotUI()
    {
        // Shell launches a process — must be ExternalIO, not the UI bucket
        // its parent BuiltInCategory.Interaction would suggest.
        const string src = @"
Sub Launch()
    Shell ""notepad.exe""
End Sub";
        var c = ClassifyOne(src, "Launch");
        Assert.Contains(ClassificationCategory.ExternalIO, c.Categories);
        Assert.DoesNotContain(ClassificationCategory.UIBound, c.Categories);
    }

    // ── Mixed classification ────────────────────────────────────────

    [Fact]
    public void Build_OnMixedBody_FlagsIsMixedAndPicksPrimaryByPrecedence()
    {
        // Body has Data signal (DLookup) AND UI signal (MsgBox).
        // Precedence puts UI ahead of Data for PrimaryCategory.
        const string src = @"
Sub WarnIfMissing(id As Long)
    If IsNull(DLookup(""Name"", ""tblClients"", ""ID="" & id)) Then
        MsgBox ""Missing.""
    End If
End Sub";
        var c = ClassifyOne(src, "WarnIfMissing");
        Assert.Contains(ClassificationCategory.DataAccess, c.Categories);
        Assert.Contains(ClassificationCategory.UIBound, c.Categories);
        Assert.True(c.IsMixed);
        Assert.Equal(ClassificationCategory.UIBound, c.PrimaryCategory);
    }

    // ── LateBound classification + UnresolvedReferences ────────────

    [Fact]
    public void Build_WithLateBoundMemberAccess_ReportsLateBoundAndUnresolvedFacts()
    {
        // `db.OpenRecordset` — db is declared `As Object`, so the member
        // call cannot be resolved statically. The classifier must record
        // a LateBound signal AND surface the unresolved fact.
        const string src = @"
Sub LoadAll()
    Dim db As Object
    Set db = CurrentDb
    db.OpenRecordset ""tblClients""
End Sub";
        var c = ClassifyOne(src, "LoadAll");
        Assert.Contains(ClassificationCategory.LateBound, c.Categories);
        Assert.Contains(ClassificationCategory.DataAccess, c.Categories);  // CurrentDb fired
        Assert.True(c.IsMixed);
        Assert.NotNull(c.UnresolvedReferences);
        Assert.Contains(
            c.UnresolvedReferences!,
            f => f.MemberName is not null
                 && string.Equals(f.MemberName, "OpenRecordset", StringComparison.OrdinalIgnoreCase));
    }

    // ── Cyclomatic complexity ───────────────────────────────────────

    [Fact]
    public void Build_OnStraightLineBody_ReportsCyclomaticOne()
    {
        const string src = @"
Sub OneLine()
    Dim x As Long
    x = 1
End Sub";
        var c = ClassifyOne(src, "OneLine");
        Assert.Equal(1, c.Metrics.CyclomaticComplexity);
    }

    [Fact]
    public void Build_OnIfElseIfElse_AddsOnePerDecision()
    {
        // 1 (base) + 1 (If) + 2 (two ElseIfs) = 4. Else is fall-through,
        // not a separate decision.
        const string src = @"
Sub Pick(x As Long)
    If x = 1 Then
    ElseIf x = 2 Then
    ElseIf x = 3 Then
    Else
    End If
End Sub";
        var c = ClassifyOne(src, "Pick");
        Assert.Equal(4, c.Metrics.CyclomaticComplexity);
    }

    [Fact]
    public void Build_OnSelectCase_CountsEachCaseClause()
    {
        // 1 (base) + 3 (three Cases) = 4. Case Else is fall-through.
        const string src = @"
Sub Dispatch(s As String)
    Select Case s
        Case ""a""
        Case ""b""
        Case ""c""
        Case Else
    End Select
End Sub";
        var c = ClassifyOne(src, "Dispatch");
        Assert.Equal(4, c.Metrics.CyclomaticComplexity);
    }

    [Fact]
    public void Build_OnLoop_AddsOnePerLoop()
    {
        // 1 (base) + 1 (For) + 1 (Do While) = 3.
        const string src = @"
Sub LoopIt(n As Long)
    Dim i As Long
    For i = 1 To n
    Next i
    Do While i > 0
        i = i - 1
    Loop
End Sub";
        var c = ClassifyOne(src, "LoopIt");
        Assert.Equal(3, c.Metrics.CyclomaticComplexity);
    }

    [Fact]
    public void Build_OnAndOrInCondition_CountsLogicalsAsAdditionalDecisions()
    {
        // 1 (base) + 1 (If) + 2 (And, Or in condition) = 4.
        const string src = @"
Sub Guard(x As Long, y As Long, z As Long)
    If x > 0 And y > 0 Or z = 0 Then
    End If
End Sub";
        var c = ClassifyOne(src, "Guard");
        Assert.Equal(4, c.Metrics.CyclomaticComplexity);
    }

    // ── LinesOfCode and StatementCount ──────────────────────────────

    [Fact]
    public void Build_OnMultilineBody_CountsDistinctSourceLines()
    {
        // Body spans three lines: Dim, assignment, return-via-name.
        const string src = @"
Function F(x As Long) As Long
    Dim r As Long
    r = x + 1
    F = r
End Function";
        var c = ClassifyOne(src, "F");
        Assert.Equal(3, c.Metrics.LinesOfCode);
        Assert.Equal(3, c.Metrics.StatementCount);
    }

    // ── DependencyEdges and Callers ─────────────────────────────────

    [Fact]
    public void Build_WithUserCallTarget_RecordsDirectDependencyEdge()
    {
        const string src = @"
Sub Outer()
    Inner
End Sub
Sub Inner()
End Sub";
        var (resolved, ast) = Resolve(src, "modCalls");
        var index = CrossReferenceIndex.Build(resolved);
        var outer = FunctionClassificationBuilder.Build(resolved, Procedure(ast, "Outer"), index);

        var inner = Assert.Single(outer.DirectDependencies);
        Assert.Equal("Inner", inner.ProcedureName);
        Assert.Equal("modCalls", inner.ModuleName);
        Assert.Equal(ProcedureKind.Sub, inner.Kind);
    }

    [Fact]
    public void Build_DirectDependencies_AreDeduplicatedAcrossCallSites()
    {
        const string src = @"
Sub Outer()
    Inner
    Inner
    Inner
End Sub
Sub Inner()
End Sub";
        var (resolved, ast) = Resolve(src, "modDup");
        var index = CrossReferenceIndex.Build(resolved);
        var outer = FunctionClassificationBuilder.Build(resolved, Procedure(ast, "Outer"), index);
        Assert.Single(outer.DirectDependencies);
    }

    [Fact]
    public void Build_BuiltInCallTargets_AreNotRecordedAsDependencies()
    {
        const string src = @"
Function Title(s As String) As String
    Title = UCase(s)
End Function";
        var c = ClassifyOne(src, "Title");
        Assert.Empty(c.DirectDependencies);
    }

    [Fact]
    public void Build_ReportsCallersFromCrossReferenceIndex()
    {
        const string src = @"
Sub A()
    Helper
End Sub
Sub B()
    Helper
End Sub
Sub Helper()
End Sub";
        var (resolved, ast) = Resolve(src, "modCallers");
        var index = CrossReferenceIndex.Build(resolved);
        var helper = FunctionClassificationBuilder.Build(
            resolved, Procedure(ast, "Helper"), index);

        Assert.Equal(2, helper.Callers.Count);
        Assert.Contains(helper.Callers, e => e.ProcedureName == "A");
        Assert.Contains(helper.Callers, e => e.ProcedureName == "B");
    }

    [Fact]
    public void Build_TransitiveClosure_IncludesIndirectCallees()
    {
        // Outer calls Mid, Mid calls Leaf. Outer's transitive closure
        // must contain Mid AND Leaf (Outer itself excluded — the seed
        // is not a dependency of itself).
        const string src = @"
Sub Outer()
    Mid_
End Sub
Sub Mid_()
    Leaf
End Sub
Sub Leaf()
End Sub";
        var (resolved, ast) = Resolve(src, "modChain");
        var index = CrossReferenceIndex.Build(resolved);
        var outer = FunctionClassificationBuilder.Build(
            resolved, Procedure(ast, "Outer"), index);

        Assert.Equal(2, outer.TransitiveDependencies.Count);
        Assert.Contains(outer.TransitiveDependencies, e => e.ProcedureName == "Mid_");
        Assert.Contains(outer.TransitiveDependencies, e => e.ProcedureName == "Leaf");
        Assert.DoesNotContain(outer.TransitiveDependencies, e => e.ProcedureName == "Outer");
    }

    [Fact]
    public void Build_RecursiveProcedure_ListsSelfAtMostOnceInTransitive()
    {
        // A recursive call would re-enqueue the seed; the BFS must
        // terminate and the seed must NOT appear in the transitive set.
        const string src = @"
Function Fact(n As Long) As Long
    If n <= 1 Then
        Fact = 1
    Else
        Fact = n * Fact(n - 1)
    End If
End Function";
        var c = ClassifyOne(src, "Fact");
        Assert.DoesNotContain(c.TransitiveDependencies, e => e.ProcedureName == "Fact");
    }

    // ── BuildAll convenience ────────────────────────────────────────

    [Fact]
    public void BuildAll_ProducesOneEntryPerProcedureAcrossModules()
    {
        var (rmA, _) = Resolve(@"
Sub A1()
End Sub
Sub A2()
End Sub", "modA");
        var (rmB, _) = Resolve(@"
Function B1() As Long
    B1 = 0
End Function", "modB");
        var index = new CrossReferenceIndex(new[] { rmA, rmB });
        var all = FunctionClassificationBuilder.BuildAll(new[] { rmA, rmB }, index);

        Assert.Equal(3, all.Count);
        Assert.Contains(all, c => c.ModuleName == "modA" && c.ProcedureName == "A1");
        Assert.Contains(all, c => c.ModuleName == "modA" && c.ProcedureName == "A2");
        Assert.Contains(all, c => c.ModuleName == "modB" && c.ProcedureName == "B1");
    }

    // ── Argument validation ─────────────────────────────────────────

    [Fact]
    public void Build_WithProcedureFromAnotherModule_Throws()
    {
        var (rmA, astA) = Resolve("Sub A()\nEnd Sub", "modA");
        var (rmB, _) = Resolve("Sub B()\nEnd Sub", "modB");
        var index = CrossReferenceIndex.Build(rmA, rmB);

        Assert.Throws<ArgumentException>(() =>
            FunctionClassificationBuilder.Build(rmB, Procedure(astA, "A"), index));
    }

    [Fact]
    public void Build_NullArguments_Throw()
    {
        var (rm, ast) = Resolve("Sub A()\nEnd Sub", "modA");
        var index = CrossReferenceIndex.Build(rm);
        var proc = Procedure(ast, "A");

        Assert.Throws<ArgumentNullException>(() =>
            FunctionClassificationBuilder.Build(null!, proc, index));
        Assert.Throws<ArgumentNullException>(() =>
            FunctionClassificationBuilder.Build(rm, null!, index));
        Assert.Throws<ArgumentNullException>(() =>
            FunctionClassificationBuilder.Build(rm, proc, null!));
    }

    // ── Type-library owner categorization ───────────────────────────

    // Direct coverage of the namespace-prefix dispatch used when the
    // resolver returns ResolvedToObjectModel(SyntheticTypeLibraryMember).
    // Wiring up a full AccessObjectModelRegistry + ITypeLibraryRegistry
    // for an end-to-end source-driven test would be a lot of setup for
    // one decision table; CategorizeTypeLibraryOwner is internal and
    // exposed via [InternalsVisibleTo("AccessHarvester.Pipeline.Tests")]
    // for this purpose.
    [Theory]
    [InlineData("Access.Form", ClassificationCategory.UIBound)]
    [InlineData("Access.TextBox", ClassificationCategory.UIBound)]
    [InlineData("Access.Control", ClassificationCategory.UIBound)]
    [InlineData("access.report", ClassificationCategory.UIBound)]   // case-insensitive
    [InlineData("DAO.Recordset", ClassificationCategory.DataAccess)]
    [InlineData("DAO.Database", ClassificationCategory.DataAccess)]
    [InlineData("ADODB.Connection", ClassificationCategory.DataAccess)]
    [InlineData("Scripting.Dictionary", ClassificationCategory.ExternalIO)]
    [InlineData("Scripting.FileSystemObject", ClassificationCategory.ExternalIO)]
    [InlineData("Excel.Application", ClassificationCategory.ExternalIO)]
    [InlineData("Word.Document", ClassificationCategory.ExternalIO)]
    [InlineData("Outlook.MailItem", ClassificationCategory.ExternalIO)]
    [InlineData("PowerPoint.Presentation", ClassificationCategory.ExternalIO)]
    [InlineData("VBA.Collection", ClassificationCategory.LateBound)]
    [InlineData("Custom.LibType", ClassificationCategory.LateBound)]
    [InlineData("", ClassificationCategory.LateBound)]
    public void CategorizeTypeLibraryOwner_DispatchesByNamespacePrefix(
        string ownerTypeName, ClassificationCategory expected)
    {
        var actual = FunctionClassificationBuilder.CategorizeTypeLibraryOwner(ownerTypeName);
        Assert.Equal(expected, actual);
    }

    // ── Determinism ────────────────────────────────────────────────

    // Re-running the builder on the same inputs must produce
    // structurally equal output (same edge counts, same edge order).
    // Guards against the HashSet-iteration-order non-determinism that
    // round-1 verification flagged.
    [Fact]
    public void Build_OnSameInputs_ProducesIdenticallyOrderedDependencyEdges()
    {
        const string src = @"
Sub Hub()
    Beta
    Alpha
    Gamma
End Sub
Sub Alpha()
End Sub
Sub Beta()
End Sub
Sub Gamma()
End Sub";
        var (resolved, ast) = Resolve(src, "modHub");
        var index = CrossReferenceIndex.Build(resolved);
        var hub = Procedure(ast, "Hub");

        var c1 = FunctionClassificationBuilder.Build(resolved, hub, index);
        var c2 = FunctionClassificationBuilder.Build(resolved, hub, index);

        Assert.Equal(
            c1.DirectDependencies.Select(e => e.ProcedureName).ToArray(),
            c2.DirectDependencies.Select(e => e.ProcedureName).ToArray());
        // Also verify the order is sorted (Alpha, Beta, Gamma) rather
        // than source-order (Beta, Alpha, Gamma) — the explicit sort
        // is what makes the output reproducible.
        Assert.Equal(
            new[] { "Alpha", "Beta", "Gamma" },
            c1.DirectDependencies.Select(e => e.ProcedureName).ToArray());
    }

    [Fact]
    public void Build_TransitiveDependencies_AreSortedAndStableAcrossRuns()
    {
        const string src = @"
Sub Outer()
    M2
    M1
End Sub
Sub M1()
    Leaf2
End Sub
Sub M2()
    Leaf1
End Sub
Sub Leaf1()
End Sub
Sub Leaf2()
End Sub";
        var (resolved, ast) = Resolve(src, "modT");
        var index = CrossReferenceIndex.Build(resolved);
        var outer = Procedure(ast, "Outer");

        var c1 = FunctionClassificationBuilder.Build(resolved, outer, index);
        var c2 = FunctionClassificationBuilder.Build(resolved, outer, index);

        Assert.Equal(
            c1.TransitiveDependencies.Select(e => e.ProcedureName).ToArray(),
            c2.TransitiveDependencies.Select(e => e.ProcedureName).ToArray());
        Assert.Equal(
            new[] { "Leaf1", "Leaf2", "M1", "M2" },
            c1.TransitiveDependencies.Select(e => e.ProcedureName).ToArray());
    }

    [Fact]
    public void Build_Callers_AreSortedAndStableAcrossRuns()
    {
        const string src = @"
Sub Z_Callsite()
    Helper
End Sub
Sub A_Callsite()
    Helper
End Sub
Sub Helper()
End Sub";
        var (resolved, ast) = Resolve(src, "modCallerOrder");
        var index = CrossReferenceIndex.Build(resolved);
        var helper = Procedure(ast, "Helper");

        var c = FunctionClassificationBuilder.Build(resolved, helper, index);
        Assert.Equal(
            new[] { "A_Callsite", "Z_Callsite" },
            c.Callers.Select(e => e.ProcedureName).ToArray());
    }
}
