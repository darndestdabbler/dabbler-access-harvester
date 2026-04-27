using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using AccessHarvester.Vba.Resolver.Types;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.Types;

// End-to-end tests of the type-inference pass against synthetic VBA
// modules. Each test parses a small inline source, builds the scope
// tree, runs the reference resolver, then runs TypeInferencePass and
// asserts on the resulting InferredTypeMap. Per
// docs/session-sets/resolver-default-members-and-inference/synthesis/
// session-2-type-inference.md §10.1.
public sealed class TypeInferencePassTests
{
    private static ResolvedModule Resolve(string source, string moduleName = "modDemo")
    {
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(
            project, ast, moduleName, ModuleKind.Standard, owningClass: null);
        return new ReferenceResolver().Resolve(module, ast);
    }

    private static Declaration FindLocal(ResolvedModule resolved, string name)
    {
        // Walks the scope tree finding the first Declaration with the
        // given name. Sufficient for the synthetic test sources below;
        // they each declare unique names for the candidates under test.
        return WalkDeclarations(resolved.ModuleScope)
            .First(d => Declaration.NameComparer.Equals(d.Name, name));
    }

    private static System.Collections.Generic.IEnumerable<Declaration> WalkDeclarations(Scope scope)
    {
        foreach (var decl in scope.Declarations) yield return decl;
        foreach (var child in scope.Children)
            foreach (var d in WalkDeclarations(child))
                yield return d;
    }

    // ── Single-evidence happy paths ───────────────────────────────────

    [Fact]
    public void Run_VariantWithSingleStringLiteralAssignment_InfersStringHigh()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = ""hello""
End Sub";
        var resolved = Resolve(src);
        var x = FindLocal(resolved, "x");

        var map = TypeInferencePass.Run(resolved);
        var hint = map.TryGet(x);
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Single(hint.Evidence);
        Assert.Equal(TypeEvidenceKind.Literal, hint.Evidence[0].Kind);
    }

    [Fact]
    public void Run_VariantWithIntegerLiteralAssignment_InfersLongHigh()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = 5
End Sub";
        var resolved = Resolve(src);
        var x = FindLocal(resolved, "x");

        var hint = TypeInferencePass.Run(resolved).TryGet(x);
        Assert.NotNull(hint);
        Assert.Equal("Long", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
    }

    [Fact]
    public void Run_VariantWithFloatLiteralAssignment_InfersDoubleHigh()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = 3.14
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("Double", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
    }

    [Fact]
    public void Run_VariantWithBoolLiteralAssignment_InfersBooleanHigh()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = True
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("Boolean", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
    }

    [Fact]
    public void Run_VariantWithDateLiteralAssignment_InfersDateHigh()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = #1/1/2024#
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("Date", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
    }

    // ── New / typed-variable / built-in / procedure return ────────────

    [Fact]
    public void Run_VariantWithNewExpression_InfersClassNameHigh()
    {
        const string src = @"
Sub Demo()
    Dim c As Variant
    Set c = New Collection
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "c"));
        Assert.NotNull(hint);
        Assert.Equal("Collection", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Equal(TypeEvidenceKind.NewExpression, hint.Evidence[0].Kind);
    }

    [Fact]
    public void Run_VariantWithSetFromTypedVariable_InfersTargetTypeHigh()
    {
        const string src = @"
Sub Demo()
    Dim source As Recordset
    Dim target As Variant
    Set target = source
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "target"));
        Assert.NotNull(hint);
        Assert.Equal("Recordset", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Equal(TypeEvidenceKind.BoundDeclarationType, hint.Evidence[0].Kind);
    }

    [Fact]
    public void Run_VariantWithBuiltInReturnTyped_InfersReturnTypeHigh()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = Format(""y"", ""yyyy"")
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Equal(TypeEvidenceKind.BuiltInReturnType, hint.Evidence[0].Kind);
    }

    [Fact]
    public void Run_VariantWithBuiltInReturnPolymorphic_GathersNoEvidence()
    {
        // IIf / Nz return null in BuiltInFunctionRegistry — polymorphic
        // by VBA semantics. Pass should record no evidence and emit no
        // entry rather than recording "Variant".
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = IIf(True, 1, ""a"")
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.Null(hint);
    }

    [Fact]
    public void Run_VariantWithProcedureReturn_InfersFunctionReturnHigh()
    {
        const string src = @"
Function GiveMeALong() As Long
    GiveMeALong = 42
End Function

Sub Demo()
    Dim x As Variant
    x = GiveMeALong()
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("Long", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Equal(TypeEvidenceKind.ProcedureReturnType, hint.Evidence[0].Kind);
    }

    // ── Default-member chain ──────────────────────────────────────────

    [Fact]
    public void Run_VariantWithDefaultMemberCall_InfersFieldHigh()
    {
        // `rs("X")` where rs is Recordset. Recordset's default member
        // is Fields (parameterized) with ReturnType = "Field". The
        // inference pass uses one default-member hop.
        const string src = @"
Sub Demo()
    Dim rs As Recordset
    Dim v As Variant
    Set v = rs(""X"")
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "v"));
        Assert.NotNull(hint);
        Assert.Equal("Field", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Equal(TypeEvidenceKind.DefaultMemberReturnType, hint.Evidence[0].Kind);
    }

    // ── TypeSuffix dominates ──────────────────────────────────────────

    [Fact]
    public void Run_TypeSuffixOnDeclaration_InfersSuffixTypeHigh()
    {
        // `x$` is String at the language level. The inference pass
        // records this regardless of any later assignment evidence.
        const string src = @"
Sub Demo()
    Dim x$
    x = ""hello""
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Contains(hint.Evidence,
            e => e.Kind == TypeEvidenceKind.TypeSuffix);
    }

    // ── Confidence bands via assignment counts ────────────────────────

    [Fact]
    public void Run_TwoOfThreeStringLiterals_InfersStringMedium()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = ""a""
    x = ""b""
    x = 7
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
        Assert.Equal(InferenceConfidence.Medium, hint.Confidence);
    }

    [Fact]
    public void Run_TwoOfFourStringLiterals_InfersLow()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = ""a""
    x = ""b""
    x = 1
    x = 2.5
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        // 2 String / 1 Long / 1 Double — no strict majority.
        Assert.Equal(InferenceConfidence.Low, hint!.Confidence);
        // String was first-seen and ties at 2; tie-breaker is first-seen.
        Assert.Equal("String", hint.TypeName);
    }

    // ── Candidate-set membership ──────────────────────────────────────

    [Fact]
    public void Run_DeclaredAsString_NotACandidate()
    {
        const string src = @"
Sub Demo()
    Dim x As String
    x = ""hello""
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.Null(hint);
    }

    [Fact]
    public void Run_DeclaredAsVariantExplicitly_StillCandidate()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = ""hello""
End Sub";
        var resolved = Resolve(src);
        Assert.NotNull(TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x")));
    }

    [Fact]
    public void Run_NoExplicitType_IsCandidate()
    {
        const string src = @"
Sub Demo()
    Dim x
    x = ""hello""
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
    }

    [Fact]
    public void Run_ConstantDeclaration_NotACandidate()
    {
        const string src = @"
Sub Demo()
    Const k As Variant = 7
End Sub";
        var resolved = Resolve(src);
        // `k` is a ConstantDeclaration even with `As Variant`. The pass
        // skips constants entirely — no entry should appear, period.
        Assert.Null(TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "k")));
    }

    [Fact]
    public void Run_ParameterDeclaration_NotACandidate()
    {
        const string src = @"
Sub Demo(x As Variant)
    x = ""hello""
End Sub";
        var resolved = Resolve(src);
        // `x` is a ParameterDeclaration (Variant-typed). The pass skips
        // parameters because their bindings cross procedure boundaries —
        // out of scope for this lightweight pass.
        Assert.Null(TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x")));
    }

    [Fact]
    public void Run_ModuleLevelVariant_IsCandidate()
    {
        const string src = @"
Public g As Variant

Sub Demo()
    g = ""hello""
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "g"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
    }

    [Fact]
    public void Run_VariantWithNoAssignment_HasNoEntry()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
End Sub";
        var resolved = Resolve(src);
        Assert.Null(TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x")));
    }

    [Fact]
    public void Run_VariantWithNothingAssignment_GathersNoEvidence()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    Set x = Nothing
End Sub";
        var resolved = Resolve(src);
        // Nothing yields no evidence — an entry would be misleading.
        Assert.Null(TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x")));
    }

    [Fact]
    public void Run_VariantWithUnresolvedRhs_GathersNoEvidence()
    {
        const string src = @"
Option Explicit
Sub Demo()
    Dim x As Variant
    x = SomeUnresolvedName
End Sub";
        var resolved = Resolve(src);
        Assert.Null(TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x")));
    }

    [Fact]
    public void Run_NullArgument_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => TypeInferencePass.Run(null!));
    }

    // ── Round-1 verifier regressions ──────────────────────────────────

    [Fact]
    public void Run_VariantSetFromSuffixTypedLocal_InfersStringHigh()
    {
        // The source variable `s` carries no As clause but has the
        // `$` suffix — VBA treats `Dim s$` as String. Inference of the
        // Variant target must follow the suffix-implied type.
        const string src = @"
Sub Demo()
    Dim s$
    s = ""hi""
    Dim x As Variant
    x = s
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Contains(hint.Evidence,
            e => e.Kind == TypeEvidenceKind.BoundDeclarationType && e.TypeName == "String");
    }

    // The parameter / module-level analogue of this test is not
    // included end-to-end because the parser currently retains the
    // type-decl suffix in the declaration's Name on those forms
    // (e.g., `Sub Foo(p$)` produces a ParameterDeclaration whose
    // Name is `p$`), so a use-site of bare `p` does not resolve to
    // it. The fallback fix in TypeOfBoundDeclaration is still
    // correct defensive code: when a future parser change normalizes
    // suffix-bearing names, the fallback already covers the typed
    // bound-declaration case for parameters and module-level
    // variables. This local-Dim test exercises the same code path
    // in the case the parser's name-stripping does support today.

    [Fact]
    public void Run_BuiltInCallInsideWithBlock_StillInfersReturnHigh()
    {
        // Sanity check: an unqualified built-in call (`Format(...)`)
        // appearing inside a `With` block still produces the right
        // evidence. The call's target identifier doesn't go through
        // With-flattening (it isn't a `.Format` reference), so this
        // test exercises ordinary built-in resolution under nested
        // scopes — the Types/EvaluateReferenceOutcome unwrap of
        // ResolvedViaWithFlattening is exercised directly by
        // Run_VariantWithImplicitWithFlattenedReference_... below.
        const string src = @"
Sub Demo()
    Dim host As Object
    Dim x As Variant
    With host
        x = Format(""y"", ""yyyy"")
    End With
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
    }

    [Fact]
    public void Run_VariantWithImplicitWithFlattenedReference_UnwrapsAndInfersHigh()
    {
        // Genuinely exercises the With-flattening peek inside the
        // inference pass. `.cboName` inside `With Me` of a Form module
        // synthesizes `Me.cboName`, which the resolver routes through
        // the StubRegistry control lookup, returning a typed
        // VariableDeclaration. The inference pass must unwrap the
        // ResolvedViaWithFlattening wrapper and surface the bound
        // declaration's type. Round-3 verifier finding: the original
        // round-1 With test went through the unqualified-call path
        // and did not actually trigger the flattening unwrap.
        const string src = @"
Private Sub Form_Load()
    Dim x As Variant
    With Me
        x = .cboName
    End With
End Sub";
        var owning = new ClassDeclaration(
            "frmTest",
            new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            ClassDeclarationKind.FormModule,
            Array.Empty<TypeReference>());
        var stringType = new TypeReference(
            new SourceProvenance(0, 0),
            new List<string> { "String" },
            IsAutoInstantiated: false,
            FixedStringSize: null,
            IsArray: false);
        var fakeControl = new VariableDeclaration(
            name: "cboName",
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            declarationScope: VariableScope.Module,
            access: AccessLevel.Public,
            isDefaultedAccess: false,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: stringType,
            typeSuffix: null,
            isArray: false,
            arrayBounds: Array.Empty<ArrayBound>());
        var registry = new TypesTestStubRegistry(controlsByForm: new()
        {
            ["frmTest"] = new() { ["cboName"] = fakeControl }
        });

        var pr = VbaParserPipeline.Parse(src);
        Module ast = pr.BuildAst("frmTest");
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(
            project, ast, "frmTest", ModuleKind.Form, owning);
        var resolved = new ReferenceResolver(registry).Resolve(module, ast);

        // Sanity-check: the implicit-with reference does in fact go
        // through ResolvedViaWithFlattening (round-3 verifier point —
        // make sure the test exercises the path under review).
        var implicitWith = resolved.References.First(r =>
            r.Expression is ImplicitWithMemberExpression);
        Assert.IsType<ResolutionOutcome.ResolvedViaWithFlattening>(implicitWith.Outcome);

        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("String", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Equal(TypeEvidenceKind.BoundDeclarationType, hint.Evidence[0].Kind);
    }

    private sealed class TypesTestStubRegistry : IAccessObjectModelRegistry
    {
        private readonly Dictionary<string, Dictionary<string, Declaration>> _controlsByForm;

        public TypesTestStubRegistry(
            Dictionary<string, Dictionary<string, Declaration>> controlsByForm)
        {
            _controlsByForm = controlsByForm;
        }

        public Declaration? TryResolveTopLevelMember(string name) => null;

        public Declaration? TryResolveControlOnForm(string formName, string controlName)
            => _controlsByForm.TryGetValue(formName, out var controls)
                && controls.TryGetValue(controlName, out var decl)
                ? decl : null;

        public Declaration? TryResolveControlOnReport(string reportName, string controlName)
            => null;
    }

    // ── Round-2 verifier regressions ──────────────────────────────────

    [Fact]
    public void Run_VariantSetFromVariantSource_GathersNoEvidence()
    {
        // The source `y` is itself Variant. Recording "Variant" as
        // evidence for the target would conflate "we know nothing more
        // than Variant" with "we know it's Variant" and dilute the
        // confidence vote. Round-2 verifier finding: such evidence is
        // filtered out at the helper level via ConcreteName.
        const string src = @"
Sub Demo()
    Dim y As Variant
    Dim x As Variant
    Set x = y
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.Null(hint);
    }

    [Fact]
    public void Run_VariantWithVariantReturningFunction_GathersNoEvidence()
    {
        // A user-defined function declared `As Variant` is no more
        // informative than the candidate's existing Variant typing —
        // filter the evidence.
        const string src = @"
Function GiveMeAVariant() As Variant
    GiveMeAVariant = 1
End Function

Sub Demo()
    Dim x As Variant
    x = GiveMeAVariant()
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.Null(hint);
    }

    [Fact]
    public void Run_VariantWithBareFunctionReference_InfersReturnHigh()
    {
        // `x = MyFunction` (no parens) is a valid VBA call to a
        // zero-arg function. Round-2 verifier finding: previously
        // EvaluateNameReference fell straight to TypeOfBoundDeclaration
        // and missed procedure return types. The fix routes through
        // EvaluateDeclarationOutcome which checks ReturnTypeOf first.
        const string src = @"
Function GiveMeALong() As Long
    GiveMeALong = 7
End Function

Sub Demo()
    Dim x As Variant
    x = GiveMeALong
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("Long", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Equal(TypeEvidenceKind.ProcedureReturnType, hint.Evidence[0].Kind);
    }

    [Fact]
    public void Run_VariantSetFromSuffixTypedLocal_InfersViaSuffixFallback()
    {
        // `Dim s#` inside a procedure: parser strips the suffix from
        // the declaration's Name (so the use-site `s` resolves
        // correctly) but leaves Type null and TypeSuffix "#". The
        // inference pass's TypeSuffix fallback in TypeOfBoundDeclaration
        // produces "Double" evidence on x.
        const string src = @"
Sub Demo()
    Dim s#
    s = 1.5
    Dim x As Variant
    x = s
End Sub";
        var resolved = Resolve(src);
        var hint = TypeInferencePass.Run(resolved).TryGet(FindLocal(resolved, "x"));
        Assert.NotNull(hint);
        Assert.Equal("Double", hint!.TypeName);
        Assert.Equal(InferenceConfidence.High, hint.Confidence);
        Assert.Contains(hint.Evidence,
            e => e.Kind == TypeEvidenceKind.BoundDeclarationType
                 && Declaration.NameComparer.Equals(e.TypeName, "Double"));
    }
}
