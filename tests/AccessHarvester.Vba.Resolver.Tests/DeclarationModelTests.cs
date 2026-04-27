using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using Xunit;
using EnumDeclaration = AccessHarvester.Vba.Resolver.Declarations.EnumDeclaration;
using EventDeclaration = AccessHarvester.Vba.Resolver.Declarations.EventDeclaration;

namespace AccessHarvester.Vba.Resolver.Tests;

// Hermetic tests that assert the shape of the declaration model: each
// kind is constructible, exposes its expected fields, threads
// references through the tracking list, and survives the
// containing-type/enum back-pointer wiring. These tests do NOT
// exercise a scope-construction visitor — that is Session 2. They
// exercise the data shape that #8 (reference resolution) will consume.
public sealed class DeclarationModelTests
{
    private static SourceRange Range(int line, int column)
        => new(new SourceLocation("", line, column), new SourceLocation("", line, column + 1));

    private static DeclarationProvenance SourceAt()
        => new SourceDeclarationProvenance(Range(0, 0));

    [Fact]
    public void SubDeclaration_ExposesNameAccessAndParameters()
    {
        var sub = new SubDeclaration(
            name: "DoThing",
            provenance: SourceAt(),
            access: AccessLevel.Public,
            isDefaultedAccess: false,
            isStatic: false,
            parameters: new List<ParameterDeclaration>());

        Assert.Equal("DoThing", sub.Name);
        Assert.Equal(AccessLevel.Public, sub.Access);
        Assert.False(sub.IsDefaultedAccess);
        Assert.False(sub.IsStatic);
        Assert.Empty(sub.Parameters);
        Assert.Null(sub.ReturnType);
        Assert.Empty(sub.References);
    }

    [Fact]
    public void FunctionDeclaration_CarriesReturnType()
    {
        var returnType = new TypeReference(
            new SourceProvenance(0, 0),
            QualifiedName: new[] { "Long" },
            IsAutoInstantiated: false,
            FixedStringSize: null,
            IsArray: false);

        var fn = new FunctionDeclaration(
            name: "Compute",
            provenance: SourceAt(),
            access: AccessLevel.Private,
            isDefaultedAccess: true,
            isStatic: false,
            parameters: new List<ParameterDeclaration>(),
            returnType: returnType);

        Assert.Same(returnType, fn.ReturnType);
        Assert.True(fn.IsDefaultedAccess);
    }

    [Fact]
    public void PropertyDeclaration_DistinguishesGetLetSet()
    {
        var get = new PropertyDeclaration(
            name: "Value", provenance: SourceAt(),
            access: AccessLevel.Public, isDefaultedAccess: false,
            isStatic: false, kind: PropertyKind.Get,
            parameters: new List<ParameterDeclaration>(),
            returnType: null);
        var let = new PropertyDeclaration(
            name: "Value", provenance: SourceAt(),
            access: AccessLevel.Public, isDefaultedAccess: false,
            isStatic: false, kind: PropertyKind.Let,
            parameters: new List<ParameterDeclaration>(),
            returnType: null);

        Assert.Equal(PropertyKind.Get, get.Kind);
        Assert.Equal(PropertyKind.Let, let.Kind);
        // Pairing is deferred to #9 — at this layer Get and Let are
        // independent declarations even when their names collide.
        Assert.NotSame(get, let);
    }

    [Fact]
    public void ParameterDeclaration_PreservesPassModeAndModifier()
    {
        var p = new ParameterDeclaration(
            name: "x",
            provenance: SourceAt(),
            passMode: ParameterPassMode.ByVal,
            modifier: ParameterModifier.Optional,
            type: null,
            typeSuffix: null,
            isArray: false,
            defaultValue: null);

        Assert.Equal(ParameterPassMode.ByVal, p.PassMode);
        Assert.Equal(ParameterModifier.Optional, p.Modifier);
    }

    [Fact]
    public void VariableDeclaration_LocalVsModuleKindIsCarried()
    {
        var local = new VariableDeclaration(
            name: "i", provenance: SourceAt(),
            declarationScope: VariableScope.Local,
            access: AccessLevel.Private, isDefaultedAccess: true,
            isStatic: false, isWithEvents: false, isAutoInstantiated: false,
            type: null, typeSuffix: null, isArray: false,
            arrayBounds: new List<ArrayBound>());

        Assert.Equal(VariableScope.Local, local.DeclarationScope);
    }

    [Fact]
    public void ConstantDeclaration_RequiresInitializer()
    {
        var lit = new LiteralExpression(new SourceProvenance(0, 0), LiteralKind.Integer, "42");
        var c = new ConstantDeclaration(
            name: "MAX",
            provenance: SourceAt(),
            declarationScope: VariableScope.Module,
            access: AccessLevel.Public, isDefaultedAccess: false,
            type: null, typeSuffix: null,
            value: lit);

        Assert.Same(lit, c.Value);
    }

    [Fact]
    public void ImplicitDeclaration_RecordsOriginatingUseInProvenance()
    {
        var range = Range(7, 3);
        var d = new ImplicitDeclaration(
            name: "undeclared",
            provenance: new ImplicitDeclarationProvenance(range),
            declarationScope: VariableScope.Local);

        var prov = Assert.IsType<ImplicitDeclarationProvenance>(d.Provenance);
        Assert.Equal(range, prov.OriginatingUse);
        Assert.Equal(range, d.Provenance.Range);
    }

    [Fact]
    public void UserDefinedType_StitchesContainingTypeOnMembers()
    {
        var member = new TypeMemberDeclaration(
            name: "Field1", provenance: SourceAt(),
            type: null, isArray: false,
            arrayBounds: new List<ArrayBound>());

        var udt = new UserDefinedTypeDeclaration(
            name: "Record",
            provenance: SourceAt(),
            access: AccessLevel.Public, isDefaultedAccess: false,
            members: new[] { member });

        Assert.Same(udt, member.ContainingType);
        Assert.Single(udt.Members);
    }

    [Fact]
    public void EnumDeclaration_StitchesContainingEnumOnMembers()
    {
        var m1 = new EnumMemberDeclaration(name: "A", provenance: SourceAt(), value: null);
        var m2 = new EnumMemberDeclaration(name: "B", provenance: SourceAt(), value: null);

        var e = new EnumDeclaration(
            name: "Color",
            provenance: SourceAt(),
            access: AccessLevel.Public, isDefaultedAccess: false,
            members: new[] { m1, m2 });

        Assert.Same(e, m1.ContainingEnum);
        Assert.Same(e, m2.ContainingEnum);
        Assert.Equal(2, e.Members.Count);
    }

    [Fact]
    public void DeclareDeclaration_CarriesLibraryAndAlias()
    {
        var d = new DeclareDeclaration(
            name: "Sleep",
            provenance: SourceAt(),
            access: AccessLevel.Public, isDefaultedAccess: false,
            isPtrSafe: true,
            kind: DeclareKind.Sub,
            libraryName: "kernel32",
            aliasName: null,
            parameters: new List<ParameterDeclaration>(),
            returnType: null);

        Assert.True(d.IsPtrSafe);
        Assert.Equal("kernel32", d.LibraryName);
        Assert.Equal(DeclareKind.Sub, d.Kind);
    }

    [Fact]
    public void EventDeclaration_HoldsParameters()
    {
        var p = new ParameterDeclaration(
            "value", SourceAt(),
            ParameterPassMode.ByVal, ParameterModifier.Required,
            type: null, typeSuffix: null, isArray: false, defaultValue: null);

        var ev = new EventDeclaration(
            name: "Changed",
            provenance: SourceAt(),
            access: AccessLevel.Public, isDefaultedAccess: false,
            parameters: new[] { p });

        Assert.Single(ev.Parameters);
        Assert.Same(p, ev.Parameters[0]);
    }

    [Fact]
    public void ClassDeclaration_DistinguishesKindAndImplements()
    {
        var iface = new TypeReference(
            new SourceProvenance(0, 0),
            QualifiedName: new[] { "ISample" },
            IsAutoInstantiated: false,
            FixedStringSize: null,
            IsArray: false);

        var c = new ClassDeclaration(
            name: "Customer",
            provenance: SourceAt(),
            kind: ClassDeclarationKind.ClassModule,
            implementedInterfaces: new[] { iface });

        Assert.Equal(ClassDeclarationKind.ClassModule, c.Kind);
        Assert.Single(c.ImplementedInterfaces);
        Assert.Same(iface, c.ImplementedInterfaces[0]);
    }

    [Fact]
    public void Declaration_ReferencesListIsEmptyByDefault()
    {
        var sub = new SubDeclaration(
            "X", SourceAt(),
            AccessLevel.Public, isDefaultedAccess: false,
            isStatic: false, parameters: new List<ParameterDeclaration>());

        Assert.Empty(sub.References);
    }

    [Fact]
    public void ConstantDeclaration_IsAVariableDeclarationPerSpecTaxonomy()
    {
        var lit = new LiteralExpression(new SourceProvenance(0, 0), LiteralKind.Integer, "1");
        var c = new ConstantDeclaration(
            "PI", SourceAt(),
            VariableScope.Module, AccessLevel.Public, isDefaultedAccess: false,
            type: null, typeSuffix: null, value: lit);

        Assert.IsAssignableFrom<VariableDeclaration>(c);
        Assert.Same(lit, c.Value);
        Assert.False(c.IsStatic);
        Assert.False(c.IsArray);
    }

    [Fact]
    public void ImplicitDeclaration_HasTypeSurfaceForSession9Inference()
    {
        var d = new ImplicitDeclaration(
            "x", new ImplicitDeclarationProvenance(Range(0, 0)),
            VariableScope.Local);

        // null = Variant per the parser-AST convention.
        Assert.Null(d.Type);
        Assert.Null(d.TypeSuffix);
    }

    [Fact]
    public void Declaration_NameComparerIsOrdinalIgnoreCase()
    {
        Assert.Same(System.StringComparer.OrdinalIgnoreCase, Declaration.NameComparer);
        Assert.True(Declaration.NameComparer.Equals("DoThing", "dothing"));
    }

    [Fact]
    public void Declaration_ScopeBackPointerIsNullUntilSession2WiresIt()
    {
        var sub = new SubDeclaration(
            "X", SourceAt(),
            AccessLevel.Public, isDefaultedAccess: false,
            isStatic: false, parameters: new List<ParameterDeclaration>());

        Assert.Null(sub.Scope);
    }

    [Fact]
    public void ParameterDeclaration_ByRef_PreservesPassModeAndModifier()
    {
        var p = new ParameterDeclaration(
            name: "data",
            provenance: SourceAt(),
            passMode: ParameterPassMode.ByRef,
            modifier: ParameterModifier.Required,
            type: null,
            typeSuffix: null,
            isArray: false,
            defaultValue: null);

        Assert.Equal(ParameterPassMode.ByRef, p.PassMode);
        Assert.Equal(ParameterModifier.Required, p.Modifier);
    }

    [Fact]
    public void ParameterDeclaration_ParamArray_PreservesModifier()
    {
        var p = new ParameterDeclaration(
            name: "args",
            provenance: SourceAt(),
            passMode: ParameterPassMode.ByRef,
            modifier: ParameterModifier.ParamArray,
            type: null,
            typeSuffix: null,
            isArray: true,
            defaultValue: null);

        Assert.Equal(ParameterModifier.ParamArray, p.Modifier);
        Assert.True(p.IsArray);
    }
}
