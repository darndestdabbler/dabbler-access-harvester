using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

// Disambiguate the AST-vs-resolver name collisions per the same alias
// pattern DeclarationModelTests.cs uses.
using ParserEnumDeclaration = AccessHarvester.Vba.Parser.Ast.EnumDeclaration;
using ParserEventDeclaration = AccessHarvester.Vba.Parser.Ast.EventDeclaration;
using ParserTypeDeclaration = AccessHarvester.Vba.Parser.Ast.TypeDeclaration;
using AstModule = AccessHarvester.Vba.Parser.Ast.Module;

namespace AccessHarvester.Vba.Resolver.Tests;

// Hermetic tests that drive ScopeBuildingVisitor with hand-built AST
// fixtures. End-to-end fixture replay over the 6 anchor inputs is
// Session 3's job (cross-cut + mechanical tightening). These tests
// pin Session 2's contract: scope-tree shape, declaration ownership,
// and access-level canonicalisation.
public sealed class ScopeBuildingVisitorTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static SourceProvenance Prov(int line = 0, int column = 0)
    {
        var range = new SourceRange(
            new SourceLocation("", line, column),
            new SourceLocation("", line, column + 1));
        return new SourceProvenance(line, column, range);
    }

    private static AstModule EmptyModule(IReadOnlyList<ModuleItem>? items = null)
        => new(Prov(), items ?? Array.Empty<ModuleItem>());

    private static VariableDeclarator VarDecl(
        string name,
        TypeReference? type = null,
        bool withEvents = false,
        bool isAutoInstantiated = false)
        => new(Prov(), name, TypeSuffix: null, withEvents, isAutoInstantiated, type,
               ArrayBounds: Array.Empty<ArrayBound>(), IsArray: false);

    private static ModuleVariableDeclaration ModuleVar(
        AccessModifier access,
        params VariableDeclarator[] declarators)
        => new(Prov(), access, declarators);

    private static ConstantDeclarator ConstDecl(string name, Expression value, TypeReference? type = null)
        => new(Prov(), name, TypeSuffix: null, type, value);

    private static ModuleConstantDeclaration ModuleConst(
        AccessModifier access, params ConstantDeclarator[] declarators)
        => new(Prov(), access, declarators);

    private static LiteralExpression IntLit(string text)
        => new(Prov(), LiteralKind.Integer, text);

    private static Parameter Param(string name, TypeReference? type = null,
        ParameterPassMode passMode = ParameterPassMode.Unspecified,
        ParameterModifier modifier = ParameterModifier.Required,
        Expression? defaultValue = null)
        => new(Prov(), name, TypeSuffix: null, passMode, modifier, type, IsArray: false, defaultValue);

    private static ProjectScope BuildSingleModule(
        AstModule module,
        string name = "TestModule",
        ModuleKind kind = ModuleKind.Standard,
        ClassDeclaration? owningClass = null)
    {
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        visitor.BuildModuleScope(project, module, name, kind, owningClass);
        return project;
    }

    private static ModuleScope SingleModuleScope(ProjectScope project)
        => Assert.IsType<ModuleScope>(Assert.Single(project.Children));

    // ── Module scope basics ──────────────────────────────────────────

    [Fact]
    public void EmptyModule_ProducesEmptyModuleScopeUnderProject()
    {
        var project = BuildSingleModule(EmptyModule());

        var module = SingleModuleScope(project);
        Assert.Same(project, module.Parent);
        Assert.Equal("TestModule", module.ModuleName);
        Assert.Equal(ModuleKind.Standard, module.Kind);
        Assert.Empty(module.Children);
        Assert.Empty(module.Declarations);
    }

    [Fact]
    public void OptionExplicit_IsCapturedOnModuleScope()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new OptionDirective(Prov(), OptionKind.Explicit, Argument: null),
        }));
        var module = SingleModuleScope(project);
        Assert.True(module.IsOptionExplicit);
    }

    [Fact]
    public void OptionPrivateModule_IsCapturedOnModuleScope()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new OptionDirective(Prov(), OptionKind.PrivateModule, Argument: "Module"),
        }));
        var module = SingleModuleScope(project);
        Assert.True(module.IsOptionPrivateModule);
    }

    [Theory]
    [InlineData("Text", OptionCompare.Text)]
    [InlineData("Database", OptionCompare.Database)]
    [InlineData("Binary", OptionCompare.Binary)]
    public void OptionCompare_IsCanonicalised(string source, OptionCompare expected)
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new OptionDirective(Prov(), OptionKind.Compare, source),
        }));
        var module = SingleModuleScope(project);
        Assert.Equal(expected, module.OptionCompare);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    public void OptionBase_IsParsed(string source, int expected)
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new OptionDirective(Prov(), OptionKind.Base, source),
        }));
        var module = SingleModuleScope(project);
        Assert.Equal(expected, module.OptionBase);
    }

    [Fact]
    public void OptionsDefaultWhenNotSpelled()
    {
        var project = BuildSingleModule(EmptyModule());
        var module = SingleModuleScope(project);
        Assert.False(module.IsOptionExplicit);
        Assert.False(module.IsOptionPrivateModule);
        Assert.Equal(0, module.OptionBase);
        Assert.Equal(OptionCompare.Binary, module.OptionCompare);
    }

    [Fact]
    public void ClassModuleKind_RecordsOwningClass()
    {
        var owner = new ClassDeclaration(
            "Customer", new SourceDeclarationProvenance(Prov().Range!),
            ClassDeclarationKind.ClassModule,
            implementedInterfaces: Array.Empty<TypeReference>());

        var project = BuildSingleModule(EmptyModule(),
            name: "Customer", kind: ModuleKind.Class, owningClass: owner);

        var module = SingleModuleScope(project);
        Assert.Equal(ModuleKind.Class, module.Kind);
        Assert.Same(owner, module.OwningClass);
    }

    // ── Module-level declarations ────────────────────────────────────

    [Theory]
    [InlineData(AccessModifier.Public,      AccessLevel.Public,  false)]
    [InlineData(AccessModifier.Private,     AccessLevel.Private, false)]
    [InlineData(AccessModifier.Friend,      AccessLevel.Friend,  false)]
    [InlineData(AccessModifier.Global,      AccessLevel.Public,  false)]
    [InlineData(AccessModifier.Dim,         AccessLevel.Private, false)]
    [InlineData(AccessModifier.Unspecified, AccessLevel.Private, true)]
    public void ModuleVariable_AccessIsCanonicalised(
        AccessModifier source, AccessLevel expected, bool expectedDefaulted)
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            ModuleVar(source, VarDecl("x")),
        }));
        var module = SingleModuleScope(project);
        var variable = Assert.IsType<VariableDeclaration>(Assert.Single(module.Declarations));
        Assert.Equal(expected, variable.Access);
        Assert.Equal(expectedDefaulted, variable.IsDefaultedAccess);
        Assert.Equal(VariableScope.Module, variable.DeclarationScope);
        Assert.Same(module, variable.Scope);
    }

    [Fact]
    public void ModuleVariable_MultipleDeclaratorsBecomeSeparateDeclarations()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            ModuleVar(AccessModifier.Private, VarDecl("a"), VarDecl("b"), VarDecl("c")),
        }));
        var module = SingleModuleScope(project);
        Assert.Equal(3, module.Declarations.Count);
        Assert.Equal(new[] { "a", "b", "c" },
            module.Declarations.OfType<VariableDeclaration>().Select(d => d.Name).ToArray());
    }

    [Fact]
    public void ModuleVariable_PreservesWithEventsAndAutoInstantiated()
    {
        var typeRef = new TypeReference(Prov(), new[] { "App" }, IsAutoInstantiated: true,
            FixedStringSize: null, IsArray: false);
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            ModuleVar(AccessModifier.Private,
                VarDecl("withEv", type: typeRef, withEvents: true),
                VarDecl("auto",   type: typeRef, isAutoInstantiated: true)),
        }));
        var module = SingleModuleScope(project);
        var withEv = (VariableDeclaration)module.Declarations[0];
        var auto = (VariableDeclaration)module.Declarations[1];
        Assert.True(withEv.IsWithEvents);
        Assert.False(auto.IsWithEvents);
        Assert.True(auto.IsAutoInstantiated);
    }

    [Fact]
    public void ModuleConstant_BecomesConstantDeclarationAtModuleScope()
    {
        var lit = IntLit("42");
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            ModuleConst(AccessModifier.Public, ConstDecl("MAX", lit)),
        }));
        var module = SingleModuleScope(project);
        var c = Assert.IsType<ConstantDeclaration>(Assert.Single(module.Declarations));
        Assert.Equal("MAX", c.Name);
        Assert.Equal(AccessLevel.Public, c.Access);
        Assert.Equal(VariableScope.Module, c.DeclarationScope);
        Assert.Same(lit, c.Value);
        // Const subsumes the VariableDeclaration shape per the spec
        // taxonomy and is not separately visible as a different
        // kind. ImplicitDeclaration is therefore unaffected.
        Assert.IsAssignableFrom<VariableDeclaration>(c);
    }

    [Fact]
    public void TypeDeclaration_StitchesContainingTypeOnMembers()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new ParserTypeDeclaration(Prov(), AccessModifier.Public, "Record",
                Members: new[]
                {
                    new TypeMember(Prov(), "Field1", Type: null, ArrayBounds: Array.Empty<ArrayBound>(), IsArray: false),
                    new TypeMember(Prov(), "Field2", Type: null, ArrayBounds: Array.Empty<ArrayBound>(), IsArray: false),
                }),
        }));
        var module = SingleModuleScope(project);
        var udt = Assert.IsType<UserDefinedTypeDeclaration>(Assert.Single(module.Declarations));
        Assert.Equal("Record", udt.Name);
        Assert.Equal(AccessLevel.Public, udt.Access);
        Assert.Equal(2, udt.Members.Count);
        Assert.Same(udt, udt.Members[0].ContainingType);
        Assert.Same(udt, udt.Members[1].ContainingType);
        // Members' Scope back-pointer is intentionally null at this layer.
        Assert.Null(udt.Members[0].Scope);
    }

    [Fact]
    public void EnumDeclaration_StitchesContainingEnumOnMembers()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new ParserEnumDeclaration(Prov(), AccessModifier.Public, "Color",
                Members: new[]
                {
                    new EnumMember(Prov(), "Red", Value: null),
                    new EnumMember(Prov(), "Green", Value: IntLit("1")),
                }),
        }));
        var module = SingleModuleScope(project);
        var enumDecl = Assert.IsType<Declarations.EnumDeclaration>(Assert.Single(module.Declarations));
        Assert.Equal(2, enumDecl.Members.Count);
        Assert.Same(enumDecl, enumDecl.Members[0].ContainingEnum);
        Assert.Same(enumDecl, enumDecl.Members[1].ContainingEnum);
    }

    [Fact]
    public void DeclareStatement_ProducesDeclareDeclarationWithoutProcedureScope()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new DeclareStatement(Prov(),
                Access: AccessModifier.Public,
                IsPtrSafe: true,
                Kind: DeclareKind.Sub,
                Name: "Sleep",
                LibraryName: "kernel32",
                AliasName: null,
                Parameters: new[] { Param("ms") },
                ReturnType: null),
        }));
        var module = SingleModuleScope(project);
        var declare = Assert.IsType<DeclareDeclaration>(Assert.Single(module.Declarations));
        Assert.True(declare.IsPtrSafe);
        Assert.Equal("kernel32", declare.LibraryName);
        Assert.Single(declare.Parameters);
        // Declare has no body — no ProcedureScope created.
        Assert.Empty(module.Children);
        // Declare parameters are NOT registered in the module's
        // Declarations list (that would let module-level TryFindLocal
        // bind a use-site to a Declare param). Their Scope back-pointer
        // is null to keep the bidirectional invariant Scope <-> Decl in
        // sync — they are accessed via decl.Parameters at call sites.
        Assert.Null(declare.Parameters[0].Scope);
        Assert.DoesNotContain(declare.Parameters[0], module.Declarations);
    }

    [Fact]
    public void EventDeclaration_DefaultsToPublicWhenAccessUnspecified()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new ParserEventDeclaration(Prov(),
                Access: AccessModifier.Unspecified,
                Name: "Changed",
                Parameters: new[] { Param("value") }),
        }));
        var module = SingleModuleScope(project);
        var ev = Assert.IsType<Declarations.EventDeclaration>(Assert.Single(module.Declarations));
        Assert.Equal(AccessLevel.Public, ev.Access);
        Assert.True(ev.IsDefaultedAccess);
        // Event has no body — no ProcedureScope created. Parameters
        // get null Scope per the same invariant as Declare (see
        // DeclareStatement_ProducesDeclareDeclarationWithoutProcedureScope).
        Assert.Empty(module.Children);
        Assert.Null(ev.Parameters[0].Scope);
        Assert.DoesNotContain(ev.Parameters[0], module.Declarations);
    }

    // ── Procedures ───────────────────────────────────────────────────

    [Fact]
    public void SubProcedure_ByRefAndParamArrayParameters_PreservePassModeAndModifier()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "Fill",
                Access: AccessModifier.Public,
                IsStatic: false,
                Parameters: new[]
                {
                    Param("arr",  passMode: ParameterPassMode.ByRef,  modifier: ParameterModifier.Required),
                    Param("vals", passMode: ParameterPassMode.ByRef,  modifier: ParameterModifier.ParamArray),
                },
                Body: Array.Empty<Statement>()),
        }));
        var module = SingleModuleScope(project);
        var procScope = Assert.IsType<ProcedureScope>(Assert.Single(module.Children));
        Assert.Equal(2, procScope.Declarations.Count);

        var byRef = Assert.IsType<ParameterDeclaration>(procScope.Declarations[0]);
        Assert.Equal(ParameterPassMode.ByRef, byRef.PassMode);
        Assert.Equal(ParameterModifier.Required, byRef.Modifier);

        var paramArray = Assert.IsType<ParameterDeclaration>(procScope.Declarations[1]);
        Assert.Equal(ParameterPassMode.ByRef, paramArray.PassMode);
        Assert.Equal(ParameterModifier.ParamArray, paramArray.Modifier);
    }

    [Fact]
    public void SubProcedure_ProducesProcedureScopeUnderModule()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "DoThing",
                Access: AccessModifier.Public,
                IsStatic: false,
                Parameters: Array.Empty<Parameter>(),
                Body: Array.Empty<Statement>()),
        }));
        var module = SingleModuleScope(project);
        var sub = Assert.IsType<SubDeclaration>(Assert.Single(module.Declarations));
        var procScope = Assert.IsType<ProcedureScope>(Assert.Single(module.Children));
        Assert.Same(module, procScope.Parent);
        Assert.Same(sub, procScope.Procedure);
    }

    [Fact]
    public void ProcedureAccess_DefaultsToPublicWhenUnspecified()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "Helper",
                Access: AccessModifier.Unspecified,
                IsStatic: false,
                Parameters: Array.Empty<Parameter>(),
                Body: Array.Empty<Statement>()),
        }));
        var module = SingleModuleScope(project);
        var sub = (SubDeclaration)module.Declarations[0];
        Assert.Equal(AccessLevel.Public, sub.Access);
        Assert.True(sub.IsDefaultedAccess);
    }

    [Fact]
    public void FunctionProcedure_PropagatesReturnType()
    {
        var rt = new TypeReference(Prov(), new[] { "Long" }, IsAutoInstantiated: false,
            FixedStringSize: null, IsArray: false);
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new FunctionProcedure(Prov(), "Compute",
                Access: AccessModifier.Public,
                IsStatic: false,
                Parameters: Array.Empty<Parameter>(),
                ReturnType: rt,
                Body: Array.Empty<Statement>()),
        }));
        var fn = Assert.IsType<FunctionDeclaration>(SingleModuleScope(project).Declarations[0]);
        Assert.Same(rt, fn.ReturnType);
    }

    [Fact]
    public void PropertyProcedure_KindFlowsThroughToPropertyDeclaration()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new PropertyProcedure(Prov(), "Value",
                Access: AccessModifier.Public, IsStatic: false,
                Kind: PropertyKind.Get,
                Parameters: Array.Empty<Parameter>(),
                ReturnType: null,
                Body: Array.Empty<Statement>()),
            new PropertyProcedure(Prov(), "Value",
                Access: AccessModifier.Public, IsStatic: false,
                Kind: PropertyKind.Let,
                Parameters: new[] { Param("rhs") },
                ReturnType: null,
                Body: Array.Empty<Statement>()),
        }));
        var module = SingleModuleScope(project);
        var properties = module.Declarations.OfType<PropertyDeclaration>().ToList();
        Assert.Equal(2, properties.Count);
        Assert.Equal(PropertyKind.Get, properties[0].Kind);
        Assert.Equal(PropertyKind.Let, properties[1].Kind);
        // Two distinct ProcedureScopes — pairing is deferred to #9.
        Assert.Equal(2, module.Children.Count);
    }

    [Fact]
    public void Procedure_ParametersBecomeProcedureScopeDeclarations()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                Access: AccessModifier.Public, IsStatic: false,
                Parameters: new[]
                {
                    Param("a", passMode: ParameterPassMode.ByVal),
                    Param("b", modifier: ParameterModifier.Optional, defaultValue: IntLit("0")),
                },
                Body: Array.Empty<Statement>()),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var paramDecls = procScope.Declarations.OfType<ParameterDeclaration>().ToList();
        Assert.Equal(2, paramDecls.Count);
        Assert.Equal(ParameterPassMode.ByVal, paramDecls[0].PassMode);
        Assert.Equal(ParameterModifier.Optional, paramDecls[1].Modifier);
        Assert.NotNull(paramDecls[1].DefaultValue);
        // Procedure-parameter Scope back-pointer points at the ProcedureScope.
        Assert.Same(procScope, paramDecls[0].Scope);
    }

    // ── Local declarations and body-level scopes ─────────────────────

    [Fact]
    public void LocalDim_BecomesProcedureScopeDeclaration()
    {
        var body = new Statement[]
        {
            new LocalDeclarationStatement(Prov(),
                LocalDeclarationKind.Dim,
                new[] { VarDecl("i"), VarDecl("j") }),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                Access: AccessModifier.Public, IsStatic: false,
                Parameters: Array.Empty<Parameter>(),
                Body: body),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var locals = procScope.Declarations.OfType<VariableDeclaration>().ToList();
        Assert.Equal(2, locals.Count);
        Assert.All(locals, v => Assert.Equal(VariableScope.Local, v.DeclarationScope));
        Assert.All(locals, v => Assert.False(v.IsStatic));
    }

    [Fact]
    public void StaticLocal_FlagsIsStatic()
    {
        var body = new Statement[]
        {
            new LocalDeclarationStatement(Prov(),
                LocalDeclarationKind.Static,
                new[] { VarDecl("counter") }),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(), body),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var local = procScope.Declarations.OfType<VariableDeclaration>().Single();
        Assert.True(local.IsStatic);
    }

    [Fact]
    public void LocalConst_BecomesProcedureScopeConstantDeclaration()
    {
        var lit = IntLit("3");
        var body = new Statement[]
        {
            new LocalConstantStatement(Prov(),
                new[] { ConstDecl("PI", lit) }),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(), body),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var constant = procScope.Declarations.OfType<ConstantDeclaration>().Single();
        Assert.Equal("PI", constant.Name);
        Assert.Equal(VariableScope.Local, constant.DeclarationScope);
        Assert.Same(lit, constant.Value);
    }

    [Fact]
    public void WithStatement_ProducesWithBlockScopeUnderProcedure()
    {
        var target = new IdentifierExpression(Prov(), "obj", TypeSuffix: null);
        var body = new Statement[]
        {
            new WithStatement(Prov(), target, Body: Array.Empty<Statement>()),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(), body),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var withScope = Assert.IsType<WithBlockScope>(Assert.Single(procScope.Children));
        Assert.Same(procScope, withScope.Parent);
        Assert.Same(target, withScope.Target);
    }

    [Fact]
    public void LocalsInsideWith_BelongToEnclosingProcedureScope()
    {
        var body = new Statement[]
        {
            new WithStatement(Prov(),
                new IdentifierExpression(Prov(), "obj", TypeSuffix: null),
                Body: new Statement[]
                {
                    new LocalDeclarationStatement(Prov(),
                        LocalDeclarationKind.Dim,
                        new[] { VarDecl("inner") }),
                }),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(), body),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var withScope = Assert.IsType<WithBlockScope>(procScope.Children[0]);
        // Local lives on the procedure, not on the With.
        Assert.Empty(withScope.Declarations);
        var local = Assert.IsType<VariableDeclaration>(Assert.Single(procScope.Declarations));
        Assert.Equal("inner", local.Name);
        Assert.Same(procScope, local.Scope);
    }

    [Fact]
    public void ForEachStatement_ProducesForEachScopeUnderProcedure()
    {
        var coll = new IdentifierExpression(Prov(), "items", TypeSuffix: null);
        var body = new Statement[]
        {
            new ForEachStatement(Prov(),
                LoopVariable: "item",
                LoopVariableTypeSuffix: null,
                Collection: coll,
                Body: Array.Empty<Statement>()),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(), body),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var forScope = Assert.IsType<ForEachScope>(Assert.Single(procScope.Children));
        Assert.Equal("item", forScope.LoopVariable);
    }

    [Fact]
    public void NestedWithInForEachInProcedure_BuildsThreeLevelsOfScope()
    {
        var inner = new WithStatement(Prov(),
            new IdentifierExpression(Prov(), "inner", TypeSuffix: null),
            Body: new Statement[]
            {
                new LocalDeclarationStatement(Prov(),
                    LocalDeclarationKind.Dim,
                    new[] { VarDecl("deep") }),
            });
        var middle = new ForEachStatement(Prov(),
            LoopVariable: "x",
            LoopVariableTypeSuffix: null,
            Collection: new IdentifierExpression(Prov(), "xs", TypeSuffix: null),
            Body: new Statement[] { inner });
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(),
                Body: new Statement[] { middle }),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        var forScope = Assert.IsType<ForEachScope>(procScope.Children[0]);
        var withScope = Assert.IsType<WithBlockScope>(forScope.Children[0]);
        Assert.Same(procScope, forScope.Parent);
        Assert.Same(forScope, withScope.Parent);
        // Deep local still belongs to the procedure.
        Assert.Single(procScope.Declarations.OfType<VariableDeclaration>().Where(v => v.Name == "deep"));
    }

    // ── ReDim / preprocessor pass-through ────────────────────────────

    [Fact]
    public void RedimStatement_DoesNotIntroduceDeclarations()
    {
        var body = new Statement[]
        {
            new RedimStatement(Prov(), Preserve: false,
                new[]
                {
                    new RedimDeclarator(Prov(), "arr", TypeSuffix: null,
                        ArrayBounds: Array.Empty<ArrayBound>(), Type: null),
                }),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(), body),
        }));
        var procScope = (ProcedureScope)SingleModuleScope(project).Children[0];
        Assert.Empty(procScope.Declarations.OfType<VariableDeclaration>());
    }

    [Fact]
    public void PreprocessorDirective_DoesNotIntroduceDeclarations()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new PreprocessorDirective(Prov(), PreprocessorKind.Const, Body: "DEBUG = 1"),
        }));
        var module = SingleModuleScope(project);
        Assert.Empty(module.Declarations);
    }

    // ── TryFindLocal ─────────────────────────────────────────────────

    [Fact]
    public void TryFindLocal_IsCaseInsensitive()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            ModuleVar(AccessModifier.Public, VarDecl("UserName")),
        }));
        var module = SingleModuleScope(project);
        Assert.NotNull(module.TryFindLocal("UserName"));
        Assert.NotNull(module.TryFindLocal("username"));
        Assert.NotNull(module.TryFindLocal("USERNAME"));
        Assert.Null(module.TryFindLocal("missing"));
    }

    [Fact]
    public void TryFindLocal_DoesNotWalkParentScopes()
    {
        var body = new Statement[]
        {
            new LocalDeclarationStatement(Prov(),
                LocalDeclarationKind.Dim,
                new[] { VarDecl("local") }),
        };
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            ModuleVar(AccessModifier.Public, VarDecl("moduleVar")),
            new SubProcedure(Prov(), "F",
                AccessModifier.Public, false,
                Array.Empty<Parameter>(), body),
        }));
        var module = SingleModuleScope(project);
        var procScope = (ProcedureScope)module.Children[0];
        // ModuleScope sees moduleVar but not local.
        Assert.NotNull(module.TryFindLocal("moduleVar"));
        Assert.Null(module.TryFindLocal("local"));
        // ProcedureScope sees local but not moduleVar — parent walking
        // is #8's job.
        Assert.NotNull(procScope.TryFindLocal("local"));
        Assert.Null(procScope.TryFindLocal("moduleVar"));
    }

    // ── Single-use guard ─────────────────────────────────────────────

    [Fact]
    public void Visitor_RejectsSecondBuildModuleScopeCall()
    {
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        visitor.BuildModuleScope(project, EmptyModule(), "First", ModuleKind.Standard);
        Assert.Throws<InvalidOperationException>(() =>
            visitor.BuildModuleScope(project, EmptyModule(), "Second", ModuleKind.Standard));
    }

    [Fact]
    public void TwoProceduresInModule_BecomeTwoChildScopes()
    {
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new SubProcedure(Prov(), "A",
                AccessModifier.Private, false,
                Array.Empty<Parameter>(), Array.Empty<Statement>()),
            new SubProcedure(Prov(), "B",
                AccessModifier.Private, false,
                Array.Empty<Parameter>(), Array.Empty<Statement>()),
        }));
        var module = SingleModuleScope(project);
        Assert.Equal(2, module.Children.Count);
        Assert.Equal(new[] { "A", "B" },
            module.Children.OfType<ProcedureScope>().Select(p => p.Procedure.Name).ToArray());
    }

    // ── Provenance round-trip ────────────────────────────────────────

    [Fact]
    public void NonRootScopes_HaveNonNullRangeEvenWhenAstProvenanceRangeIsNull()
    {
        // AST nodes whose Provenance.Range is null still must produce
        // scopes with a non-null Range (Feature 2 invariant — only
        // ProjectScope is the synthetic root exception).
        var bareProv = new SourceProvenance(7, 3, Range: null);
        var module = new AstModule(bareProv, new ModuleItem[]
        {
            new SubProcedure(bareProv, "F",
                AccessModifier.Public, IsStatic: false,
                Parameters: Array.Empty<Parameter>(),
                Body: new Statement[]
                {
                    new WithStatement(bareProv,
                        new IdentifierExpression(bareProv, "obj", TypeSuffix: null),
                        Body: new Statement[]
                        {
                            new ForEachStatement(bareProv,
                                LoopVariable: "x",
                                LoopVariableTypeSuffix: null,
                                Collection: new IdentifierExpression(bareProv, "xs", TypeSuffix: null),
                                Body: Array.Empty<Statement>()),
                        }),
                }),
        });
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var moduleScope = visitor.BuildModuleScope(project, module, "M", ModuleKind.Standard);

        Assert.NotNull(moduleScope.Range);
        var procScope = (ProcedureScope)moduleScope.Children[0];
        Assert.NotNull(procScope.Range);
        var withScope = (WithBlockScope)procScope.Children[0];
        Assert.NotNull(withScope.Range);
        var forScope = (ForEachScope)withScope.Children[0];
        Assert.NotNull(forScope.Range);
        // ProjectScope itself is the synthetic exception.
        Assert.Null(project.Range);
    }

    [Fact]
    public void DeclarationProvenance_PicksUpRangeFromAst()
    {
        var range = new SourceRange(new SourceLocation("M.bas", 5, 0),
                                    new SourceLocation("M.bas", 5, 10));
        var declarator = new VariableDeclarator(
            new SourceProvenance(5, 0, range),
            "rangedVar", TypeSuffix: null, WithEvents: false, IsAutoInstantiated: false,
            Type: null, ArrayBounds: Array.Empty<ArrayBound>(), IsArray: false);
        var project = BuildSingleModule(EmptyModule(new ModuleItem[]
        {
            new ModuleVariableDeclaration(Prov(), AccessModifier.Public, new[] { declarator }),
        }));
        var module = SingleModuleScope(project);
        var v = (VariableDeclaration)module.Declarations[0];
        var prov = Assert.IsType<SourceDeclarationProvenance>(v.Provenance);
        Assert.Equal(range, prov.Range);
    }
}
