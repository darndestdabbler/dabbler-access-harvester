using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.CrossReference;

// Hermetic unit tests for the Phase 3.7 cross-reference index. The
// index inverts ResolvedModule's reference→declaration map so
// downstream call-graph and impact-analysis enrichment can ask
// "who references this declaration?" without re-walking the AST.
public sealed class CrossReferenceIndexTests
{
    private static (Module ast, ModuleScope scope) BuildFromSource(
        string source, string moduleName)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(
            project, ast, moduleName, ModuleKind.Standard, owningClass: null);
        return (ast, module);
    }

    private static ResolvedModule Resolve(string source, string moduleName)
    {
        var (ast, scope) = BuildFromSource(source, moduleName);
        return new ReferenceResolver().Resolve(scope, ast);
    }

    private static VariableDeclaration LocalVariable(ResolvedModule resolved, string name)
    {
        // Walk into the first procedure scope and pick the variable
        // declaration by name. Tests stay terse by relying on the
        // resolver's scope tree rather than re-deriving from AST.
        var proc = resolved.ModuleScope.Children.OfType<ProcedureScope>().First();
        return proc.Declarations
            .OfType<VariableDeclaration>()
            .First(v => Declaration.NameComparer.Equals(v.Name, name));
    }

    [Fact]
    public void Build_OnSingleModule_GroupsReferencesByDeclarationIdentity()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
    x = x + 2
End Sub";
        var resolved = Resolve(src, "modDemo");
        var index = CrossReferenceIndex.Build(resolved);

        var x = LocalVariable(resolved, "x");
        var refs = index.GetReferencesTo(x);

        // Three use sites of `x` — LHS of `x = 1`, LHS of `x = x + 2`,
        // and the RHS `x` in `x + 2`. The Dim is a declaration, not a
        // reference, so it does not appear here.
        Assert.Equal(3, refs.Count);
        foreach (var r in refs)
        {
            var decl = ((ResolutionOutcome.Resolved)r.Outcome).Declaration;
            Assert.Same(x, decl);
        }
    }

    [Fact]
    public void GetReferencesTo_WithNoMatch_ReturnsEmptyList()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var resolved = Resolve(src, "modDemo");
        var index = CrossReferenceIndex.Build(resolved);

        // A declaration from an unrelated, un-indexed module is not
        // present in this index. The index must return an empty list,
        // not null.
        var unrelated = Resolve(@"
Sub Other()
    Dim y As Integer
End Sub", "modOther");
        var y = LocalVariable(unrelated, "y");
        var refs = index.GetReferencesTo(y);
        Assert.NotNull(refs);
        Assert.Empty(refs);
    }

    [Fact]
    public void Build_AcrossMultipleModules_PreservesPerModuleReferenceIdentity()
    {
        // Two independent modules; each has its own `x`. The index
        // must not collapse them — Declaration identity differs.
        const string srcA = @"
Sub DemoA()
    Dim x As Integer
    x = 1
End Sub";
        const string srcB = @"
Sub DemoB()
    Dim x As Integer
    x = 2
    x = 3
End Sub";
        var rmA = Resolve(srcA, "modA");
        var rmB = Resolve(srcB, "modB");
        var index = new CrossReferenceIndex(new[] { rmA, rmB });

        var xA = LocalVariable(rmA, "x");
        var xB = LocalVariable(rmB, "x");

        Assert.Single(index.GetReferencesTo(xA));
        Assert.Equal(2, index.GetReferencesTo(xB).Count);
        Assert.Equal(2, index.Modules.Count);
        Assert.Contains(xA, index.Declarations);
        Assert.Contains(xB, index.Declarations);
    }

    [Fact]
    public void Build_DoesNotIndex_BuiltInOrMeOrUnresolvedOutcomes()
    {
        // `Len(s)` resolves to a built-in; `xyzzy` is unresolved; both
        // outcome variants must be skipped by the index. The local
        // `s` resolves normally and is the only declaration that
        // should appear in Declarations.
        const string src = @"
Sub Demo()
    Dim s As String
    s = ""hi""
    Debug.Print Len(s)
    xyzzy = 1
End Sub";
        var resolved = Resolve(src, "modDemo");
        var index = CrossReferenceIndex.Build(resolved);

        var s = LocalVariable(resolved, "s");
        Assert.NotEmpty(index.GetReferencesTo(s));

        // No declaration in the index should be a synthetic / null:
        // every key must come from a real Resolved/ResolvedToObjectModel
        // outcome on at least one reference.
        foreach (var decl in index.Declarations)
        {
            var refs = index.GetReferencesTo(decl);
            Assert.NotEmpty(refs);
            foreach (var r in refs)
            {
                Assert.True(
                    r.Outcome is ResolutionOutcome.Resolved
                        or ResolutionOutcome.ResolvedToObjectModel
                        or ResolutionOutcome.ResolvedViaWithFlattening,
                    $"Unexpected outcome variant indexed: {r.Outcome.GetType().Name}");
            }
        }
    }

    [Fact]
    public void Build_WithFlattening_IndexesUnderInnerDeclaration()
    {
        // The Session-2 with-flattening pass wraps implicit-with member
        // sites in ResolvedViaWithFlattening. The index must peek
        // through the wrapper and key the reference under the inner
        // outcome's Declaration, mirroring ResolvedModule.TryGetDeclaration.
        //
        // Hand-build a ResolvedReference whose Outcome is
        // ResolvedViaWithFlattening over a real Declaration so the
        // unwrap path in CrossReferenceIndex is genuinely exercised.
        // (Source-driven flattening through `With rs / .Foo` lands as
        // DefaultMemberNotYetResolved at the inner outcome until the
        // Phase-3.5 registry wires the lookup, which would NOT exercise
        // the unwrap branch — see ReferenceResolverTests
        // ImplicitWithMember_FlattensToMemberAccessOnWithTarget.)
        const string src = @"
Sub Demo()
    Dim target As Object
    target = Nothing
End Sub";
        var (ast, scope) = BuildFromSource(src, "modDemo");
        var resolvedFlat = new ReferenceResolver().ResolveModule(scope, ast);

        // Pick the parser AST node and the Declaration we want to use
        // as the synthesized inner reference target.
        var resolved = new ReferenceResolver().Resolve(scope, ast);
        var targetDecl = resolved.ModuleScope.Children
            .OfType<ProcedureScope>().First()
            .Declarations.OfType<VariableDeclaration>()
            .First(v => Declaration.NameComparer.Equals(v.Name, "target"));
        var anyExpr = resolvedFlat.First().Expression;

        // Construct a synthetic ResolvedViaWithFlattening outcome whose
        // InnerOutcome resolves to the local `target` Declaration.
        var inner = new ResolutionOutcome.Resolved(targetDecl);
        var flattened = new ResolutionOutcome.ResolvedViaWithFlattening(
            SynthesizedTarget: anyExpr,
            InnerOutcome: inner);
        var syntheticReference = new ResolvedReference(
            Expression: anyExpr,
            Location: resolvedFlat[0].Location,
            EnclosingScope: resolvedFlat[0].EnclosingScope,
            Outcome: flattened);

        var rm = new ResolvedModule(ast, scope, new[] { syntheticReference });
        var index = CrossReferenceIndex.Build(rm);

        var refs = index.GetReferencesTo(targetDecl);
        Assert.Single(refs);
        Assert.Same(syntheticReference, refs[0]);
        Assert.IsType<ResolutionOutcome.ResolvedViaWithFlattening>(refs[0].Outcome);
    }

    [Fact]
    public void GetReferencesTo_ReturnedList_IsImmutable()
    {
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var resolved = Resolve(src, "modDemo");
        var index = CrossReferenceIndex.Build(resolved);
        var x = LocalVariable(resolved, "x");

        var refs = index.GetReferencesTo(x);
        Assert.IsNotType<List<ResolvedReference>>(refs);
    }

    [Fact]
    public void Constructor_NullModulesEnumerable_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CrossReferenceIndex(modules: null!));
    }

    [Fact]
    public void Constructor_NullModuleEntry_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CrossReferenceIndex(new ResolvedModule[] { null! }));
    }

    [Fact]
    public void GetReferencesTo_NullDeclaration_Throws()
    {
        const string src = @"Sub Demo()
End Sub";
        var resolved = Resolve(src, "modDemo");
        var index = CrossReferenceIndex.Build(resolved);
        Assert.Throws<ArgumentNullException>(
            () => index.GetReferencesTo(declaration: null!));
    }

    [Fact]
    public void Build_IsSinglePass_ReBuildingDoesNotAffectExistingIndex()
    {
        // Spec Feature 3 Standards: "Index construction is a single
        // pass; downstream callers query via the API, never reconstruct."
        // The index must take a snapshot of the modules' reference lists
        // at construction; nothing in the resolver mutates those lists
        // after Resolve returns, but a second index over the same
        // modules must produce equivalent results without disturbing
        // the first.
        const string src = @"
Sub Demo()
    Dim x As Integer
    x = 1
End Sub";
        var resolved = Resolve(src, "modDemo");
        var first = CrossReferenceIndex.Build(resolved);
        var second = CrossReferenceIndex.Build(resolved);

        var x = LocalVariable(resolved, "x");
        Assert.Equal(first.GetReferencesTo(x).Count, second.GetReferencesTo(x).Count);
    }
}
