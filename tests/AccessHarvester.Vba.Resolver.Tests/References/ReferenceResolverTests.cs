using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.References;

// Hermetic unit tests for the Session 1 reference resolver. Synthetic
// inline VBA modules cover each ResolutionOutcome variant and each
// closed-set Unresolved reason; fixture-driven smoke tests confirm
// the resolver runs to completion against the canonical Session 1
// anchors (02, 03, 09, 08-unresolvable.bas) without exception and
// without unrecognized reason strings.
public sealed class ReferenceResolverTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static string LocateTestCasesRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases");
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate docs/test-cases walking up from '{AppContext.BaseDirectory}'.");
    }

    private static (Module ast, ModuleScope scope) BuildFromSource(
        string source,
        string moduleName,
        ModuleKind kind = ModuleKind.Standard,
        ClassDeclaration? owningClass = null)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(project, ast, moduleName, kind, owningClass);
        return (ast, module);
    }

    private static (Module ast, ModuleScope scope) BuildFromFixture(
        string fileName,
        string moduleName,
        ModuleKind kind = ModuleKind.Standard,
        ClassDeclaration? owningClass = null)
    {
        string source = File.ReadAllText(Path.Combine(LocateTestCasesRoot(), fileName));
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(fileName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(project, ast, moduleName, kind, owningClass);
        return (ast, module);
    }

    private static IReadOnlyList<ResolvedReference> Resolve(
        Module ast,
        ModuleScope module,
        IAccessObjectModelRegistry? registry = null)
    {
        return new ReferenceResolver(registry).ResolveModule(module, ast);
    }

    // ── Identifier resolution: scope chain ────────────────────────────

    [Fact]
    public void Identifier_ResolvesToLocalDim()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var xUses = refs.Where(r =>
            r.Expression is IdentifierExpression id && id.Name == "x").ToList();
        Assert.NotEmpty(xUses);
        var first = Assert.IsType<ResolutionOutcome.Resolved>(xUses[0].Outcome);
        Assert.Equal("x", first.Declaration.Name);
        Assert.IsType<VariableDeclaration>(first.Declaration);
    }

    [Fact]
    public void Identifier_ResolvesToProcedureParameter()
    {
        const string src = @"
Sub Demo(value As Integer)
    Dim y As Integer
    y = value
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var valueUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "value");
        Assert.NotNull(valueUse);
        var resolved = Assert.IsType<ResolutionOutcome.Resolved>(valueUse!.Outcome);
        Assert.IsType<ParameterDeclaration>(resolved.Declaration);
        Assert.Equal("value", resolved.Declaration.Name);
    }

    [Fact]
    public void Identifier_ResolvesToModuleLevelConst()
    {
        const string src = @"
Public Const Pi As Double = 3.14159
Sub Demo()
    Dim r As Double
    r = Pi
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var piUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "Pi");
        Assert.NotNull(piUse);
        var resolved = Assert.IsType<ResolutionOutcome.Resolved>(piUse!.Outcome);
        Assert.IsType<ConstantDeclaration>(resolved.Declaration);
    }

    [Fact]
    public void Identifier_ResolvesToBuiltIn_WhenNotInScope()
    {
        const string src = @"
Sub Demo()
    Dim s As String
    s = Format(42, ""0"")
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var formatUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "Format");
        Assert.NotNull(formatUse);
        var bi = Assert.IsType<ResolutionOutcome.ResolvedToBuiltIn>(formatUse!.Outcome);
        Assert.Equal("Format", bi.Function.Name);
    }

    [Fact]
    public void Identifier_NotInScope_EmitsExplicitUnresolved()
    {
        // `xyzzy` is neither declared nor a built-in.
        const string src = @"
Sub Demo()
    xyzzy = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var xyzzyUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "xyzzy");
        Assert.NotNull(xyzzyUse);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(xyzzyUse!.Outcome);
        Assert.Equal(UnresolvedReasons.NotInScope, u.Reason);
    }

    [Fact]
    public void Identifier_LocalShadowsModuleLevel_LocalWins()
    {
        const string src = @"
Public x As Integer
Sub Demo()
    Dim x As String
    x = ""local""
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var xUseInside = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "x"
            && r.EnclosingScope is ProcedureScope);
        Assert.NotNull(xUseInside);
        var resolved = Assert.IsType<ResolutionOutcome.Resolved>(xUseInside!.Outcome);
        var v = Assert.IsType<VariableDeclaration>(resolved.Declaration);
        Assert.Equal(VariableScope.Local, v.DeclarationScope);
    }

    [Fact]
    public void DeclarationReceivesBackReference_WhenResolved()
    {
        const string src = @"
Sub Demo()
    Dim count As Integer
    count = 1
    count = count + 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        new ReferenceResolver().ResolveModule(scope, ast);

        var procScope = (ProcedureScope)scope.Children[0];
        var count = procScope.Declarations.OfType<VariableDeclaration>()
            .Single(v => v.Name == "count");
        // 3 use-sites in the source: lhs of first =, lhs of second =,
        // rhs of second =.
        Assert.Equal(3, count.References.Count);
        Assert.All(count.References, r => Assert.False(r.IsImplicit));
    }

    // ── Me ────────────────────────────────────────────────────────────

    [Fact]
    public void Me_InFormModule_ResolvesToOwningClass()
    {
        const string src = @"
Private Sub Form_Load()
    Dim m As Object
    Set m = Me
End Sub";
        var owning = new ClassDeclaration(
            name: "frmCustomers",
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            kind: ClassDeclarationKind.FormModule,
            implementedInterfaces: Array.Empty<TypeReference>());
        var (ast, scope) = BuildFromSource(src, "frmCustomers", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope);

        var meUse = refs.FirstOrDefault(r => r.Expression is MeExpression);
        Assert.NotNull(meUse);
        var me = Assert.IsType<ResolutionOutcome.ResolvedToMe>(meUse!.Outcome);
        Assert.Same(owning, me.OwningClass);
    }

    [Fact]
    public void Me_InStandardModule_EmitsMeInStandardModule()
    {
        const string src = @"
Sub Demo()
    Dim x As Object
    Set x = Me
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo", ModuleKind.Standard);
        var refs = Resolve(ast, scope);

        var meUse = refs.FirstOrDefault(r => r.Expression is MeExpression);
        Assert.NotNull(meUse);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(meUse!.Outcome);
        Assert.Equal(UnresolvedReasons.MeInStandardModule, u.Reason);
    }

    // ── Bang / member access ──────────────────────────────────────────

    [Fact]
    public void BangAccessOnMe_WithNullRegistry_DeferredToDefaultMember()
    {
        const string src = @"
Private Sub Form_Load()
    Me!ctlName = ""hi""
End Sub";
        var owning = new ClassDeclaration(
            "frmCustomers",
            new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            ClassDeclarationKind.FormModule,
            Array.Empty<TypeReference>());
        var (ast, scope) = BuildFromSource(src, "frmCustomers", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope);

        var bang = refs.FirstOrDefault(r => r.Expression is BangAccessExpression);
        Assert.NotNull(bang);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(bang!.Outcome);
        Assert.Equal(UnresolvedReasons.DefaultMemberNotYetResolved, u.Reason);
    }

    [Fact]
    public void BangAccessOnMe_WithStubRegistry_ResolvesToReturnedDeclaration()
    {
        const string src = @"
Private Sub Form_Load()
    Me!cboCity = ""Boston""
End Sub";
        var owning = new ClassDeclaration(
            "frmCustomers",
            new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            ClassDeclarationKind.FormModule,
            Array.Empty<TypeReference>());
        var fakeControl = new VariableDeclaration(
            name: "cboCity",
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            declarationScope: VariableScope.Module,
            access: AccessLevel.Public,
            isDefaultedAccess: false,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: null,
            typeSuffix: null,
            isArray: false,
            arrayBounds: Array.Empty<ArrayBound>());
        var registry = new StubRegistry(controlsByForm: new()
        {
            ["frmCustomers"] = new() { ["cboCity"] = fakeControl }
        });

        var (ast, scope) = BuildFromSource(src, "frmCustomers", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        var bang = refs.FirstOrDefault(r => r.Expression is BangAccessExpression);
        Assert.NotNull(bang);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(bang!.Outcome);
        Assert.Same(fakeControl, resolved.Declaration);
    }

    [Fact]
    public void BareIdentifier_FallsBackToTopLevelRegistry_WhenNotInScopeOrBuiltins()
    {
        // `MyHostThing` is not declared, not a built-in, and the
        // registry returns a synthetic declaration for it. The
        // resolver must pick the registry hit ahead of the
        // BuiltInFunctionRegistry / Unresolved fallback.
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = MyHostThing
End Sub";
        var fake = new VariableDeclaration(
            name: "MyHostThing",
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            declarationScope: VariableScope.Module,
            access: AccessLevel.Public,
            isDefaultedAccess: false,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: null,
            typeSuffix: null,
            isArray: false,
            arrayBounds: Array.Empty<ArrayBound>());
        var registry = new StubRegistry(
            controlsByForm: new(),
            topLevels: new() { ["MyHostThing"] = fake });

        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope, registry);

        var hit = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "MyHostThing");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        Assert.Same(fake, resolved.Declaration);
    }

    [Fact]
    public void TopLevelRegistry_TakesPrecedenceOverBuiltIns()
    {
        // When the registry returns a hit for a name that is ALSO a
        // curated built-in (e.g. "Format"), the documented algorithm
        // §4.3 ordering says registry wins over built-ins. Round-2
        // verifier finding (Minor) — guards the precedence rule.
        const string src = @"
Sub Demo()
    Dim s As Variant
    s = Format
End Sub";
        var fakeFormat = new VariableDeclaration(
            name: "Format",
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            declarationScope: VariableScope.Module,
            access: AccessLevel.Public,
            isDefaultedAccess: false,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: null,
            typeSuffix: null,
            isArray: false,
            arrayBounds: Array.Empty<ArrayBound>());
        var registry = new StubRegistry(
            controlsByForm: new(),
            topLevels: new() { ["Format"] = fakeFormat });

        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope, registry);

        var formatUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "Format");
        Assert.NotNull(formatUse);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(formatUse!.Outcome);
        Assert.Same(fakeFormat, resolved.Declaration);
    }

    [Fact]
    public void ParenthesizedTarget_UnwrapsForMemberDispatch()
    {
        // (Me)!ctl should still route through the registry as if
        // the parens were absent. Round-2 verifier finding (Major).
        const string src = @"
Private Sub Form_Load()
    (Me)!cboCity = ""Boston""
End Sub";
        var owning = new ClassDeclaration(
            "frmCustomers",
            new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            ClassDeclarationKind.FormModule,
            Array.Empty<TypeReference>());
        var fakeControl = new VariableDeclaration(
            name: "cboCity",
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            declarationScope: VariableScope.Module,
            access: AccessLevel.Public,
            isDefaultedAccess: false,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: null,
            typeSuffix: null,
            isArray: false,
            arrayBounds: Array.Empty<ArrayBound>());
        var registry = new StubRegistry(
            controlsByForm: new() { ["frmCustomers"] = new() { ["cboCity"] = fakeControl } });

        var (ast, scope) = BuildFromSource(src, "frmCustomers", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        var bang = refs.FirstOrDefault(r => r.Expression is BangAccessExpression);
        Assert.NotNull(bang);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(bang!.Outcome);
        Assert.Same(fakeControl, resolved.Declaration);
    }

    [Fact]
    public void ForEachCollection_IsResolvedAtEnclosingScope()
    {
        // The collection expression must be visited; otherwise its
        // identifier uses are silently dropped from the resolver's
        // output. Round-1 verifier finding (Major).
        const string src = @"
Sub Demo()
    Dim items As Collection
    Dim it As Variant
    For Each it In items
        Debug.Print it
    Next
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var itemsUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "items");
        Assert.NotNull(itemsUse);
        var resolved = Assert.IsType<ResolutionOutcome.Resolved>(itemsUse!.Outcome);
        Assert.Equal("items", resolved.Declaration.Name);
    }

    [Fact]
    public void MemberAccessOnUnresolvedTarget_CascadesUnresolved()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = unknownThing.Member
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var memberAccess = refs.FirstOrDefault(r => r.Expression is MemberAccessExpression);
        Assert.NotNull(memberAccess);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(memberAccess!.Outcome);
        Assert.Equal(UnresolvedReasons.MemberAccessOnUnresolvedTarget, u.Reason);
    }

    // ── Implicit-with members ─────────────────────────────────────────

    [Fact]
    public void ImplicitWithMember_EmitsWithBlockFlatteningDeferred()
    {
        const string src = @"
Private Sub Demo()
    Dim rs As Object
    With rs
        .Foo = 1
    End With
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var implicitWith = refs.FirstOrDefault(r =>
            r.Expression is ImplicitWithMemberExpression);
        Assert.NotNull(implicitWith);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(implicitWith!.Outcome);
        Assert.Equal(UnresolvedReasons.WithBlockFlatteningDeferred, u.Reason);
    }

    [Fact]
    public void ImplicitWithBang_EmitsWithBlockFlatteningDeferred()
    {
        const string src = @"
Private Sub Demo()
    Dim rs As Object
    With rs
        !Field1 = 1
    End With
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = Resolve(ast, scope);

        var implicitWithBang = refs.FirstOrDefault(r =>
            r.Expression is ImplicitWithBangExpression);
        Assert.NotNull(implicitWithBang);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(implicitWithBang!.Outcome);
        Assert.Equal(UnresolvedReasons.WithBlockFlatteningDeferred, u.Reason);
    }

    // ── Fixture-driven smoke tests ────────────────────────────────────
    // For each canonical Session 1 anchor, the resolver runs to
    // completion, every emitted entry's outcome variant is one of the
    // four documented variants, and every Unresolved.Reason is one of
    // the closed-set values declared in UnresolvedReasons.

    private static readonly string[] AllowedReasons = new[]
    {
        UnresolvedReasons.NotInScope,
        UnresolvedReasons.MeInStandardModule,
        UnresolvedReasons.DefaultMemberNotYetResolved,
        UnresolvedReasons.WithBlockFlatteningDeferred,
        UnresolvedReasons.MemberAccessOnUnresolvedTarget,
    };

    [Theory]
    [InlineData("02-cascading-combo-subform.bas", "frmCaseManagement", ModuleKind.Form)]
    [InlineData("03-with-block-shadowing.bas",     "frmRecordEditor",  ModuleKind.Form)]
    [InlineData("09-report-grouping-events.bas",   "rptCaseSummary",   ModuleKind.Report)]
    [InlineData("08-unresolvable.bas",             "modConfigDriven",  ModuleKind.Standard)]
    public void Fixture_RunsToCompletionWithKnownReasonCodes(
        string fileName, string moduleName, ModuleKind kind)
    {
        ClassDeclaration? owning = null;
        if (kind == ModuleKind.Form)
        {
            owning = new ClassDeclaration(moduleName,
                new SourceDeclarationProvenance(
                    new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
                ClassDeclarationKind.FormModule,
                Array.Empty<TypeReference>());
        }
        else if (kind == ModuleKind.Report)
        {
            owning = new ClassDeclaration(moduleName,
                new SourceDeclarationProvenance(
                    new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
                ClassDeclarationKind.ReportModule,
                Array.Empty<TypeReference>());
        }

        var (ast, scope) = BuildFromFixture(fileName, moduleName, kind, owning);
        var refs = Resolve(ast, scope);

        Assert.NotEmpty(refs);
        foreach (var r in refs)
        {
            switch (r.Outcome)
            {
                case ResolutionOutcome.Resolved:
                case ResolutionOutcome.ResolvedToObjectModel:
                case ResolutionOutcome.ResolvedToBuiltIn:
                case ResolutionOutcome.ResolvedToMe:
                    break;
                case ResolutionOutcome.Unresolved u:
                    Assert.Contains(u.Reason, AllowedReasons);
                    break;
                default:
                    Assert.Fail($"Unexpected ResolutionOutcome variant: {r.Outcome.GetType().Name}");
                    break;
            }
        }
    }

    // ── Stub registry for tests ───────────────────────────────────────

    private sealed class StubRegistry : IAccessObjectModelRegistry
    {
        private readonly Dictionary<string, Dictionary<string, Declaration>> _controlsByForm;
        private readonly Dictionary<string, Declaration> _topLevels;

        public StubRegistry(
            Dictionary<string, Dictionary<string, Declaration>> controlsByForm,
            Dictionary<string, Declaration>? topLevels = null)
        {
            _controlsByForm = controlsByForm;
            _topLevels = topLevels ?? new();
        }

        public Declaration? TryResolveTopLevelMember(string name)
            => _topLevels.TryGetValue(name, out var d) ? d : null;

        public Declaration? TryResolveControlOnForm(string formName, string controlName)
            => _controlsByForm.TryGetValue(formName, out var controls)
                && controls.TryGetValue(controlName, out var decl)
                ? decl : null;

        public Declaration? TryResolveControlOnReport(string reportName, string controlName)
            => null;
    }
}
