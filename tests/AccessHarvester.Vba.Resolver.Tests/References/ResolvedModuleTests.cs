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

// Hermetic unit tests for the Session 3 ResolvedModule wrapper. The
// wrapper is purely additive over Session 1's flat list output — these
// tests confirm the wrapper preserves the underlying references and
// adds correct lookups, unresolved-subset materialisation, and
// diagnostics with the §4.1 reason→severity mapping.
public sealed class ResolvedModuleTests
{
    // ── Helpers (mirroring ReferenceResolverTests; intentionally
    // duplicated rather than shared, since they're tiny and the tests
    // remain readable in isolation) ────────────────────────────────────

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

    private static ResolvedModule ResolveAsModule(
        Module ast, ModuleScope scope, IAccessObjectModelRegistry? registry = null)
        => new ReferenceResolver(registry).Resolve(scope, ast);

    // ── Wrapper shape ────────────────────────────────────────────────

    [Fact]
    public void Resolve_ReturnsResolvedModule_PropagatingAstAndScopeByIdentity()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        Assert.Same(ast, resolved.Ast);
        Assert.Same(scope, resolved.ModuleScope);
        Assert.NotNull(resolved.References);
        Assert.NotEmpty(resolved.References);
    }

    [Fact]
    public void Resolve_References_EquivalentToFlatResolveModule_ExactPerEntry()
    {
        // Construct a ResolvedModule from the same reference list
        // ResolveModule produced and assert per-entry reference equality
        // (i.e., the wrapper doesn't filter, reorder, or rebuild
        // ResolvedReference instances). Using one resolver call avoids
        // the back-population side effect that would double a Declaration's
        // References list on a second pass.
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
    xyzzy = 2
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var flat = new ReferenceResolver().ResolveModule(scope, ast);
        var rm = new ResolvedModule(ast, scope, flat);
        Assert.Equal(flat.Count, rm.References.Count);
        for (int i = 0; i < flat.Count; i++)
            Assert.Same(flat[i], rm.References[i]);
    }

    [Fact]
    public void ExposedCollections_AreImmutableViews()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        // Read-only views should not be down-castable to mutable List<T>.
        Assert.IsNotType<List<ResolvedReference>>(resolved.References);
        Assert.IsNotType<List<ResolvedReference>>(resolved.UnresolvedReferences);
        Assert.IsNotType<List<ResolverDiagnostic>>(resolved.Diagnostics);
    }

    [Fact]
    public void DefensiveCopy_MutatingInputListAfterConstruction_DoesNotAffectModule()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = new List<ResolvedReference>(
            new ReferenceResolver().ResolveModule(scope, ast));
        var rm = new ResolvedModule(ast, scope, refs);
        int before = rm.References.Count;
        refs.Clear();
        Assert.Equal(before, rm.References.Count);
    }

    // ── Unresolved subset ────────────────────────────────────────────

    [Fact]
    public void UnresolvedReferences_ContainsExactlyUnresolvedOutcomes()
    {
        const string src = @"
Sub Demo()
    xyzzy = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var fromReferences = resolved.References
            .Where(r => r.Outcome is ResolutionOutcome.Unresolved)
            .ToList();
        Assert.NotEmpty(fromReferences);
        Assert.Equal(fromReferences.Count, resolved.UnresolvedReferences.Count);
        for (int i = 0; i < fromReferences.Count; i++)
            Assert.Same(fromReferences[i], resolved.UnresolvedReferences[i]);
    }

    // ── TryGetReference / TryGetDeclaration ──────────────────────────

    [Fact]
    public void TryGetReference_ReturnsEntryForEmittedNode()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var xUse = resolved.References.First(r =>
            r.Expression is IdentifierExpression id && id.Name == "x");
        var lookup = resolved.TryGetReference(xUse.Expression);
        Assert.Same(xUse, lookup);
    }

    [Fact]
    public void TryGetReference_ReturnsNullForNodeWithNoEmittedOutcome()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        // The Module node itself is not a reference site; the walker
        // never records an outcome for it.
        Assert.Null(resolved.TryGetReference(
            new IdentifierExpression(
                new SourceProvenance(0, 0, null), "never_visited", null)));
    }

    [Fact]
    public void TryGetReference_ReturnsNull_ForRealNonEmittingAstNode()
    {
        // IndexOrCallExpression is a real node the walker explicitly
        // does NOT emit an outcome for (per ReferenceResolver.cs §
        // VisitIndexOrCallExpression). Pull a real such node out of the
        // AST so the test exercises the lookup miss path on an actual
        // walker-produced node, not a synthetic standalone one.
        const string src = @"
Sub Demo()
    Dim s As String
    s = Format(42, ""0"")
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var indexOrCall = FindFirstDescendant<IndexOrCallExpression>(ast);
        Assert.NotNull(indexOrCall);
        Assert.Null(resolved.TryGetReference(indexOrCall!));
    }

    // Walks the AST via VbaSyntaxWalker to find the first node of the
    // requested type. Used to fish a real walker-visited node out of a
    // parsed module for lookup-miss assertions.
    private static T? FindFirstDescendant<T>(VbaSyntaxNode root) where T : VbaSyntaxNode
    {
        var finder = new TypeFinder<T>();
        finder.Visit(root);
        return finder.Found;
    }

    private sealed class TypeFinder<T> : VbaSyntaxWalker where T : VbaSyntaxNode
    {
        public T? Found;
        public override void DefaultVisit(VbaSyntaxNode node)
        {
            if (Found is not null) return;
            if (node is T t) { Found = t; return; }
            base.DefaultVisit(node);
        }
    }

    [Fact]
    public void TryGetDeclaration_ReturnsBoundDeclaration_OnResolvedOutcome()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var xUse = resolved.References.First(r =>
            r.Expression is IdentifierExpression id && id.Name == "x");
        var decl = resolved.TryGetDeclaration(xUse.Expression);
        Assert.NotNull(decl);
        Assert.Equal("x", decl!.Name);
        Assert.IsType<VariableDeclaration>(decl);
    }

    [Fact]
    public void TryGetDeclaration_ReturnsNull_ForBuiltInOutcome()
    {
        const string src = @"
Sub Demo()
    Dim s As String
    s = Format(42, ""0"")
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var formatUse = resolved.References.First(r =>
            r.Expression is IdentifierExpression id && id.Name == "Format");
        Assert.IsType<ResolutionOutcome.ResolvedToBuiltIn>(formatUse.Outcome);
        Assert.Null(resolved.TryGetDeclaration(formatUse.Expression));
    }

    [Fact]
    public void TryGetDeclaration_ReturnsNull_ForResolvedToMeOutcome()
    {
        const string src = @"
Private Sub Form_Load()
    Dim m As Object
    Set m = Me
End Sub";
        var owning = new ClassDeclaration(
            "frmCustomers",
            new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            ClassDeclarationKind.FormModule,
            Array.Empty<TypeReference>());
        var (ast, scope) = BuildFromSource(src, "frmCustomers", ModuleKind.Form, owning);
        var resolved = ResolveAsModule(ast, scope);

        var meUse = resolved.References.First(r => r.Expression is MeExpression);
        Assert.IsType<ResolutionOutcome.ResolvedToMe>(meUse.Outcome);
        Assert.Null(resolved.TryGetDeclaration(meUse.Expression));
    }

    [Fact]
    public void TryGetDeclaration_ReturnsBoundDeclaration_OnResolvedToObjectModel()
    {
        // Stub registry returns a synthetic declaration for a top-level
        // name; the resolver emits ResolvedToObjectModel and
        // TryGetDeclaration must surface the synthetic declaration.
        const string src = @"
Sub Demo()
    Dim s As Variant
    s = MyHostThing
End Sub";
        var fake = MakeFakeVariableDecl("MyHostThing");
        var registry = new StubRegistry(
            controlsByForm: new(),
            topLevels: new() { ["MyHostThing"] = fake });

        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope, registry);

        var hit = resolved.References.First(r =>
            r.Expression is IdentifierExpression id && id.Name == "MyHostThing");
        Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit.Outcome);
        Assert.Same(fake, resolved.TryGetDeclaration(hit.Expression));
    }

    [Fact]
    public void TryGetDeclaration_PeeksThroughResolvedViaWithFlattening_ToInnerBinding()
    {
        // Construct a ResolvedModule directly with a ResolvedViaWithFlattening
        // whose InnerOutcome is Resolved, exercising the peek-through.
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var refs = new ReferenceResolver().ResolveModule(scope, ast);
        var existing = refs.First(r =>
            r.Expression is IdentifierExpression id && id.Name == "x");
        var inner = (ResolutionOutcome.Resolved)existing.Outcome;

        // Synthesize an implicit-with site and a flattened outcome whose
        // inner-bound declaration is the same as the existing 'x' decl.
        var implicitWith = new ImplicitWithMemberExpression(
            new SourceProvenance(99, 0, null), "x");
        var synthesizedTarget = new IdentifierExpression(
            new SourceProvenance(99, 0, null), "withTarget", null);
        var flattened = new ResolvedReference(
            implicitWith,
            existing.Location,
            scope,
            new ResolutionOutcome.ResolvedViaWithFlattening(synthesizedTarget, inner));

        var rm = new ResolvedModule(ast, scope, new[] { existing, flattened });
        Assert.Same(inner.Declaration, rm.TryGetDeclaration(implicitWith));
    }

    [Fact]
    public void TryGetDeclaration_ReturnsNull_ForUnresolved()
    {
        const string src = @"
Sub Demo()
    xyzzy = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var unresolvedUse = resolved.UnresolvedReferences.First(r =>
            r.Expression is IdentifierExpression id && id.Name == "xyzzy");
        Assert.Null(resolved.TryGetDeclaration(unresolvedUse.Expression));
    }

    // ── Diagnostics ──────────────────────────────────────────────────

    [Fact]
    public void Diagnostics_OneEntryPerUnresolvedReference()
    {
        const string src = @"
Sub Demo()
    xyzzy = 1
    foo = 2
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        Assert.Equal(resolved.UnresolvedReferences.Count, resolved.Diagnostics.Count);
        // Every diagnostic's Code is a known UnresolvedReasons constant.
        Assert.All(resolved.Diagnostics, d =>
        {
            Assert.Contains(d.Code, new[]
            {
                UnresolvedReasons.NotInScope,
                UnresolvedReasons.MeInStandardModule,
                UnresolvedReasons.DefaultMemberNotYetResolved,
                UnresolvedReasons.WithBlockFlatteningDeferred,
                UnresolvedReasons.MemberAccessOnUnresolvedTarget,
            });
        });
    }

    [Fact]
    public void Diagnostics_NotInScope_IsWarning_WithLocationFromReference()
    {
        const string src = @"
Sub Demo()
    xyzzy = 1
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var xyzzyRef = resolved.UnresolvedReferences.First(r =>
            r.Expression is IdentifierExpression id && id.Name == "xyzzy");
        var diag = resolved.Diagnostics.First(d =>
            d.Code == UnresolvedReasons.NotInScope
            && d.Location.Start.Line == xyzzyRef.Location.Start.Line
            && d.Location.Start.Column == xyzzyRef.Location.Start.Column);
        Assert.Equal(ResolverDiagnosticSeverity.Warning, diag.Severity);
        Assert.Contains("xyzzy", diag.Message);
    }

    [Fact]
    public void Diagnostics_MeInStandardModule_IsWarning()
    {
        const string src = @"
Sub Demo()
    Dim x As Object
    Set x = Me
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var diag = resolved.Diagnostics.First(d =>
            d.Code == UnresolvedReasons.MeInStandardModule);
        Assert.Equal(ResolverDiagnosticSeverity.Warning, diag.Severity);
    }

    [Fact]
    public void Diagnostics_MemberAccessOnUnresolvedTarget_IsInfo()
    {
        // `xyzzy.Foo` — target is unresolved, so the tail member is
        // emitted with MemberAccessOnUnresolvedTarget.
        const string src = @"
Sub Demo()
    Dim s As Variant
    s = xyzzy.Foo
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolved = ResolveAsModule(ast, scope);

        var diag = resolved.Diagnostics.FirstOrDefault(d =>
            d.Code == UnresolvedReasons.MemberAccessOnUnresolvedTarget);
        Assert.NotNull(diag);
        Assert.Equal(ResolverDiagnosticSeverity.Info, diag!.Severity);
    }

    [Fact]
    public void Diagnostics_WithBlockFlatteningDeferred_IsInfo_WhenSynthesised()
    {
        // The walker no longer emits this reason for valid input
        // (Session 2 wired the flattening pass), but the severity
        // mapping must still be correct for any future defensive case.
        // Construct a ResolvedModule directly with a synthetic Unresolved
        // entry carrying the deferred-with reason.
        const string src = @"
Sub Demo()
    Dim x As Integer
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var implicitWith = new ImplicitWithMemberExpression(
            new SourceProvenance(99, 0, null), "ctlName");
        var synthetic = new ResolvedReference(
            implicitWith,
            new SourceRange(new SourceLocation("", 99, 0), new SourceLocation("", 99, 1)),
            scope,
            new ResolutionOutcome.Unresolved(UnresolvedReasons.WithBlockFlatteningDeferred));

        var rm = new ResolvedModule(ast, scope, new[] { synthetic });
        var diag = rm.Diagnostics.Single();
        Assert.Equal(UnresolvedReasons.WithBlockFlatteningDeferred, diag.Code);
        Assert.Equal(ResolverDiagnosticSeverity.Info, diag.Severity);
    }

    [Fact]
    public void Diagnostics_DefaultMemberNotYetResolved_IsInfo()
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
        var resolved = ResolveAsModule(ast, scope);

        var diag = resolved.Diagnostics.FirstOrDefault(d =>
            d.Code == UnresolvedReasons.DefaultMemberNotYetResolved);
        Assert.NotNull(diag);
        Assert.Equal(ResolverDiagnosticSeverity.Info, diag!.Severity);
    }

    // ── Fixture-driven smoke ─────────────────────────────────────────

    [Theory]
    [InlineData("02-cascading-combo-subform.bas")]
    [InlineData("03-with-block-shadowing.bas")]
    [InlineData("09-report-grouping-events.bas")]
    [InlineData("08-unresolvable.bas")]
    public void Resolve_OnFixture_ProducesNonNullModule_DiagnosticsMatchUnresolvedCount(string fileName)
    {
        string source = File.ReadAllText(Path.Combine(LocateTestCasesRoot(), fileName));
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(fileName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var scope = visitor.BuildModuleScope(project, ast, fileName, ModuleKind.Standard, null);

        var resolved = new ReferenceResolver().Resolve(scope, ast);

        Assert.NotNull(resolved);
        Assert.Same(ast, resolved.Ast);
        Assert.Equal(resolved.UnresolvedReferences.Count, resolved.Diagnostics.Count);
    }

    // ── Test helpers ─────────────────────────────────────────────────

    private static VariableDeclaration MakeFakeVariableDecl(string name)
        => new VariableDeclaration(
            name: name,
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

    // Mirror of the ReferenceResolverTests' StubRegistry — duplicated
    // here intentionally so this file is self-contained.
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

        public Declaration? TryResolveMemberOf(Declaration parent, string memberName)
            => null;
    }
}
