using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.DefaultMembers;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;
using AstModule = AccessHarvester.Vba.Parser.Ast.Module;

namespace AccessHarvester.Vba.Resolver.Tests.DefaultMembers;

// Hermetic tests for PropertyGroupBuilder. Constructs Property
// declarations through ScopeBuildingVisitor (mirroring
// ScopeBuildingVisitorTests' helper pattern) so the test fixtures
// share the public test surface used by the rest of the resolver.
//
// Pins Session 1 synthesis doc §8.2 contract.
public sealed class PropertyGroupBuilderTests
{
    // ── Helpers (mirror ScopeBuildingVisitorTests) ──────────────────

    private static SourceProvenance Prov(int line = 0, int column = 0)
    {
        var range = new SourceRange(
            new SourceLocation("", line, column),
            new SourceLocation("", line, column + 1));
        return new SourceProvenance(line, column, range);
    }

    private static PropertyProcedure Property(
        string name, PropertyKind kind, AccessModifier access = AccessModifier.Public)
    {
        return new PropertyProcedure(
            Provenance: Prov(),
            Name: name,
            Access: access,
            IsStatic: false,
            Kind: kind,
            Parameters: Array.Empty<Parameter>(),
            ReturnType: null,
            Body: Array.Empty<Statement>());
    }

    private static SubProcedure Sub(string name)
        => new(Provenance: Prov(),
               Name: name,
               Access: AccessModifier.Public,
               IsStatic: false,
               Parameters: Array.Empty<Parameter>(),
               Body: Array.Empty<Statement>());

    private static ModuleScope BuildModule(params ModuleItem[] items)
    {
        var module = new AstModule(Prov(), items);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        visitor.BuildModuleScope(project, module, "TestModule", ModuleKind.Standard, owningClass: null);
        return Assert.IsType<ModuleScope>(Assert.Single(project.Children));
    }

    // ── Pairing cases ───────────────────────────────────────────────

    [Fact]
    public void BuildForScope_PairsGetAndLet()
    {
        var scope = BuildModule(
            Property("Foo", PropertyKind.Get),
            Property("Foo", PropertyKind.Let));

        var groups = PropertyGroupBuilder.BuildForScope(scope);

        var group = Assert.Single(groups);
        Assert.Equal("Foo", group.Name);
        Assert.NotNull(group.Get);
        Assert.NotNull(group.Let);
        Assert.Null(group.Set);
        Assert.True(group.IsReadable);
        Assert.True(group.IsWritableValue);
        Assert.False(group.IsWritableObject);
    }

    [Fact]
    public void BuildForScope_PairsGetAndSet()
    {
        var scope = BuildModule(
            Property("Bar", PropertyKind.Get),
            Property("Bar", PropertyKind.Set));

        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.NotNull(group.Get);
        Assert.Null(group.Let);
        Assert.NotNull(group.Set);
        Assert.True(group.IsReadable);
        Assert.False(group.IsWritableValue);
        Assert.True(group.IsWritableObject);
    }

    [Fact]
    public void BuildForScope_GroupsCaseInsensitively()
    {
        var scope = BuildModule(
            Property("value", PropertyKind.Get),
            Property("VALUE", PropertyKind.Let));

        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.NotNull(group.Get);
        Assert.NotNull(group.Let);
        // First-seen spelling wins per the synthesis doc §5.2.
        Assert.Equal("value", group.Name);
    }

    [Fact]
    public void BuildForScope_SoloGetIsOwnGroup()
    {
        var scope = BuildModule(Property("ReadOnly_", PropertyKind.Get));
        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.NotNull(group.Get);
        Assert.Null(group.Let);
        Assert.Null(group.Set);
    }

    [Fact]
    public void BuildForScope_SoloLetIsOwnGroup()
    {
        var scope = BuildModule(Property("WriteOnlyValue", PropertyKind.Let));
        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.Null(group.Get);
        Assert.NotNull(group.Let);
        Assert.Null(group.Set);
        Assert.False(group.IsReadable);
        Assert.True(group.IsWritableValue);
    }

    [Fact]
    public void BuildForScope_SoloSetIsOwnGroup()
    {
        var scope = BuildModule(Property("WriteOnlyObject", PropertyKind.Set));
        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.Null(group.Get);
        Assert.Null(group.Let);
        Assert.NotNull(group.Set);
        Assert.False(group.IsWritableValue);
        Assert.True(group.IsWritableObject);
    }

    [Fact]
    public void BuildForScope_LetAndSetCoexist_StillOneGroup()
    {
        // Invalid VBA, but the parser may emit both — pairing must
        // record both rather than drop data, leaving diagnostic
        // emission to a later layer (synthesis doc §3.3).
        var scope = BuildModule(
            Property("X", PropertyKind.Let),
            Property("X", PropertyKind.Set));

        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.Null(group.Get);
        Assert.NotNull(group.Let);
        Assert.NotNull(group.Set);
    }

    [Fact]
    public void BuildForScope_IgnoresNonPropertyDeclarations()
    {
        var scope = BuildModule(
            Sub("Foo"),
            Property("Foo", PropertyKind.Get));

        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.NotNull(group.Get);
        // The Sub does not appear in any group.
        Assert.Null(group.Let);
        Assert.Null(group.Set);
    }

    [Fact]
    public void BuildForScope_EmptyScope_ReturnsEmpty()
    {
        var scope = BuildModule();
        Assert.Empty(PropertyGroupBuilder.BuildForScope(scope));
    }

    [Fact]
    public void BuildForScope_PreservesFirstSeenOrder()
    {
        var scope = BuildModule(
            Property("Zeta", PropertyKind.Get),
            Property("Alpha", PropertyKind.Get),
            Property("Zeta", PropertyKind.Let),
            Property("Mu", PropertyKind.Let));

        var groups = PropertyGroupBuilder.BuildForScope(scope);
        Assert.Equal(new[] { "Zeta", "Alpha", "Mu" },
            groups.Select(g => g.Name).ToArray());
    }

    [Fact]
    public void BuildForScope_GroupCarriesScopeBackPointer()
    {
        var scope = BuildModule(Property("X", PropertyKind.Get));
        var group = Assert.Single(PropertyGroupBuilder.BuildForScope(scope));
        Assert.Same(scope, group.Scope);
    }

    [Fact]
    public void BuildForScope_NullScope_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PropertyGroupBuilder.BuildForScope(null!));
    }
}
