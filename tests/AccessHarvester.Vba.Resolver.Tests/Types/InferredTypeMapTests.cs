using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using AccessHarvester.Vba.Resolver.Types;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.Types;

// Map-shape regressions for InferredTypeMap. The map's contract is
// reference-equality keys + immutable backing storage; both must hold
// for downstream consumers per session-2-type-inference.md §3.5.
public sealed class InferredTypeMapTests
{
    private static ResolvedModule ResolveTrivial()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = ""hello""
End Sub";
        var parseResult = VbaParserPipeline.Parse(src);
        Module ast = parseResult.BuildAst("modDemo");
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(
            project, ast, "modDemo", ModuleKind.Standard, owningClass: null);
        return new ReferenceResolver().Resolve(module, ast);
    }

    [Fact]
    public void TryGet_NullDeclaration_Throws()
    {
        var map = TypeInferencePass.Run(ResolveTrivial());
        Assert.Throws<ArgumentNullException>(() => map.TryGet(null!));
    }

    [Fact]
    public void TryGet_UnknownDeclaration_ReturnsNull()
    {
        var map = TypeInferencePass.Run(ResolveTrivial());
        // A declaration we never registered — synthesised here.
        var stranger = new VariableDeclaration(
            name: "stranger",
            provenance: new SourceDeclarationProvenance(
                new SourceRange(
                    new SourceLocation("", 1, 1),
                    new SourceLocation("", 1, 9))),
            declarationScope: VariableScope.Local,
            access: AccessLevel.Private,
            isDefaultedAccess: true,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: null,
            typeSuffix: null,
            isArray: false,
            arrayBounds: Array.Empty<ArrayBound>());
        Assert.Null(map.TryGet(stranger));
    }

    [Fact]
    public void All_BackingMap_IsImmutable()
    {
        // Casting `All` back to a mutable Dictionary must not succeed —
        // the wrap in ReadOnlyDictionary is a defensive contract that
        // mirrors DefaultMemberRegistry's immutability test.
        var map = TypeInferencePass.Run(ResolveTrivial());
        Assert.IsNotType<Dictionary<Declaration, InferredType>>(map.All);
    }

    [Fact]
    public void Count_MatchesAllSize()
    {
        var map = TypeInferencePass.Run(ResolveTrivial());
        Assert.Equal(map.All.Count, map.Count);
    }
}
