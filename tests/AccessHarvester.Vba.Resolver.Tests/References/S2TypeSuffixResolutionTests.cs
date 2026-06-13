using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.References;

// Set 008 Session 2 (F-3-3): type-declaration suffixes (`$ % & ! # @`) are
// stripped into TypeSuffix at every declaration site, so a suffixed module
// variable / constant / parameter declares under the same Name its references
// resolve against. Before the fix these declared as e.g. "g_sTitle$" while the
// reference stripped to "g_sTitle", so every such reference resolved
// Unresolved (not-in-scope) — silent-wrong. These pins run the real parse →
// scope-build → resolve chain (the 006 F-3-3 probe shape).
public sealed class S2TypeSuffixResolutionTests
{
    private static IReadOnlyList<ResolvedReference> ResolveSource(string src, ModuleKind kind = ModuleKind.Standard)
    {
        var parseResult = VbaParserPipeline.Parse(src);
        Module ast = parseResult.BuildAst("modSuffix");
        var project = new ProjectScope();
        var scope = new ScopeBuildingVisitor().BuildModuleScope(project, ast, "modSuffix", kind);
        return new ReferenceResolver().ResolveModule(scope, ast);
    }

    private static void AssertResolvesTo<TDecl>(string src, string name) where TDecl : Declaration
    {
        var refs = ResolveSource(src);
        var use = refs.FirstOrDefault(r => r.Expression is IdentifierExpression id && id.Name == name);
        Assert.NotNull(use);
        var resolved = Assert.IsType<ResolutionOutcome.Resolved>(use!.Outcome);
        Assert.IsType<TDecl>(resolved.Declaration);
        Assert.Equal(name, resolved.Declaration.Name);
    }

    [Fact]
    public void SuffixedModuleVariable_ResolvesFromBody()
    {
        AssertResolvesTo<VariableDeclaration>("""
            Public g_sTitle$
            Sub Use()
                g_sTitle = "x"
            End Sub
            """, "g_sTitle");
    }

    [Fact]
    public void SuffixedParameter_ResolvesFromBody()
    {
        AssertResolvesTo<ParameterDeclaration>("""
            Sub Foo(p$)
                Debug.Print p
            End Sub
            """, "p");
    }

    [Fact]
    public void SuffixedModuleConstant_ResolvesFromBody()
    {
        AssertResolvesTo<ConstantDeclaration>("""
            Private Const MSG$ = "hi"
            Sub Use()
                Debug.Print MSG
            End Sub
            """, "MSG");
    }

    [Fact]
    public void SuffixedUseSite_MatchesSuffixedDeclaration()
    {
        // Even when the use site carries the suffix, it strips to the same Name
        // and matches the suffixed declaration.
        AssertResolvesTo<VariableDeclaration>("""
            Public g_sTitle$
            Sub Use()
                g_sTitle$ = "x"
            End Sub
            """, "g_sTitle");
    }

    [Fact]
    public void ControlCase_SuffixedLocal_ResolvesFromBody()
    {
        AssertResolvesTo<VariableDeclaration>("""
            Sub Use()
                Dim s$
                s = "x"
            End Sub
            """, "s");
    }
}
