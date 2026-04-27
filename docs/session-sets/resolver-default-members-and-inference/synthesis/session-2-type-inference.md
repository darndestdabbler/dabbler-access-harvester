# Session 2 Synthesis — Type Inference

> **Authored:** 2026-04-27 in-conversation by Claude Opus 4.7
> (orchestrator).
> **Routing decision:** Per `project-guidance.md` Convention "When the
> orchestrator's model matches the routing target...", Opus 4.7 authored
> this synthesis directly rather than calling
> `route(task_type="architecture")`, whose target is also Opus. The
> non-negotiable cross-provider gate (`session-verification`) still runs
> against gpt-5-4 at end of session.
> **Scope:** Phase 3.6 of `docs/VBA-Parser-Migration-Harvester-Plan.md`
> — *lightweight type-inference pass with confidence scoring on
> Variant-typed declarations*. Resolver wiring that consumes inferred
> types (Session 4 cross-cut), and the `GetReferencesTo` cross-reference
> index (Session 3), are explicitly out of scope here. This session
> lands the inference data the later sessions read.

## 1. Goal

Annotate `Variant`-typed (or untyped) declarations with an inferred
concrete type and a confidence score, **without mutating the original
declaration**. The plan's framing is unambiguous: "Inferred types
annotate the AST but never replace the original declaration." Phase 3.6
also caps ambition: "Don't overinvest here; AI-assisted translation
downstream can use these hints without depending on them being exact."

The deliverable is a side-table the resolver and downstream enrichment
consult when they want a hint about a Variant variable's likely runtime
type. A missing entry means "no inference made"; a present entry comes
with a confidence band the consumer is responsible for honoring.

## 2. Inputs and outputs

```
Input    ResolvedModule            // Session 3 wrapper from #8 — has
                                   // Ast, ModuleScope, References,
                                   // and the Expression -> Declaration
                                   // lookup the pass needs.
Output   InferredTypeMap           // Declaration -> InferredType for
                                   // every candidate that gathered
                                   // any evidence.
```

The pass is *pure* — it constructs the map from `ResolvedModule` data
and does not mutate the resolver graph. The candidate set and the
inference algorithm are deterministic over the input AST so two
runs against the same module produce the same map.

## 3. Data model

### 3.1 `InferredType`

```csharp
public sealed record InferredType(
    string TypeName,                       // canonical type spelling
    InferenceConfidence Confidence,
    IReadOnlyList<TypeEvidence> Evidence); // source-ordered evidence
```

`TypeName` is a leaf type name: `"String"`, `"Long"`, `"Double"`,
`"Date"`, `"Boolean"`, `"Currency"`, `"Recordset"`, `"Field"`,
`"Variant"`, etc. Matches the `Declaration.NameComparer` casing
convention (case-preserved string, compared case-insensitively).

The shape mirrors `DefaultMember` and `BuiltInFunction` from prior
sessions: a small immutable record. `Evidence` is exposed so consumers
that want to surface *why* the inference was made (debug views, AI
translation prompts) can reach the underlying assignments without
re-walking the AST.

### 3.2 `InferenceConfidence`

```csharp
public enum InferenceConfidence { High, Medium, Low }
```

Bands (see §6 for the algorithm that assigns them):

- **High** — every evidence row pointed at the same type, or a
  declaration's `TypeSuffix` definitively pinned it. Consumers may use
  the inferred type for translation without further checking.
- **Medium** — a strict majority of evidence agreed on one type but a
  minority dissented. Treat as a hint; surface to the human or to AI
  translation as "likely T but not unanimous."
- **Low** — no majority. The inference is best-effort; consumers
  should not depend on the result. The recorded `TypeName` is the
  most-frequently-seen, with first-seen breaking ties.

### 3.3 `TypeEvidence`

```csharp
public sealed record TypeEvidence(
    TypeEvidenceKind Kind,
    string TypeName,
    SourceRange Location);
```

One entry per piece of evidence the pass collected. `Location` is the
source range of the evidence site — a declaration's typed-suffix
position, or the right-hand side of an assignment. Consumers can pin
the evidence back to source without re-walking.

### 3.4 `TypeEvidenceKind`

```csharp
public enum TypeEvidenceKind
{
    TypeSuffix,                // declaration site carried `$`/`%`/etc.
    Literal,                   // RHS was a primitive literal
    NewExpression,             // RHS was `New T`
    BoundDeclarationType,      // RHS resolved to a typed declaration
    ProcedureReturnType,       // RHS resolved to a function call
    BuiltInReturnType,         // RHS resolved to a built-in call
    DefaultMemberReturnType,   // RHS was a parameterized default-member call
}
```

Kinds are exhaustive over what the pass produces today. Adding a kind
is an extension point a future session can use without reshaping the
record.

### 3.5 `InferredTypeMap`

```csharp
public sealed class InferredTypeMap
{
    public IReadOnlyDictionary<Declaration, InferredType> All { get; }
    public InferredType? TryGet(Declaration declaration);
    public int Count { get; }
}
```

A class (not a record) — equality is reference, like the rest of the
resolver graph. Backing storage is wrapped in `ReadOnlyDictionary` so
callers cannot cast `All` and mutate it (same defense pattern Session 1
applied to `DefaultMemberRegistry.All`). Map keys use
`ReferenceEqualityComparer.Instance` because two distinct `Declaration`
instances with the same name in different scopes must not coalesce.

## 4. The candidate set

A declaration is a *candidate* for inference when its declared type is
genuinely indeterminate at the source layer:

- `VariableDeclaration` whose `Type` is null (no `As` clause) **or**
  whose `Type.QualifiedName` last segment is `"Variant"`.
- `ImplicitDeclaration` (synthesised at first use in a non-Option-Explicit
  module). Always Variant-typed at this layer; always a candidate.

Excluded:

- Declarations with an explicit non-Variant `As` clause (`Dim x As String`).
- `ConstantDeclaration` — constants carry their declared or
  literal-derived type and are inferred elsewhere; this pass is about
  *value-bearing variables that may carry different types over time*.
- `ParameterDeclaration` — parameters are bound at call sites the pass
  cannot enumerate without whole-program analysis. Parameter inference
  belongs to a future cross-procedure pass; out of scope per "Don't
  overinvest."
- `ProcedureDeclaration`, `EnumDeclaration`, `TypeDeclaration`,
  `EventDeclaration`, `DeclareDeclaration`, `ClassDeclaration` — not
  value-bearing in the assignment sense.

A candidate that gathers zero evidence simply has no entry in the map.
That is the explicit signal "the pass had nothing to say." Consumers
should not equate map-absence with "Variant" — the original
declaration is still authoritative for that case.

## 5. Evidence sources

The pass walks the module AST gathering evidence for each candidate.

### 5.1 TypeSuffix

If a `VariableDeclaration` or `ImplicitDeclaration` carries a
`TypeSuffix` (`$`, `%`, `&`, `!`, `#`, `@`), the suffix maps directly
to a VBA primitive type. This is *definitive* — `Dim x$` is String at
the language level, not a hint.

| Suffix | Type    |
|--------|---------|
| `$`    | String  |
| `%`    | Integer |
| `&`    | Long    |
| `!`    | Single  |
| `#`    | Double  |
| `@`    | Currency |

Evidence emitted with `Kind = TypeSuffix`, `Location` pointing at the
declaration's provenance range.

### 5.2 Assignment RHS

For each `LetStatement` or `SetStatement` in the module body whose
target's bound declaration is a candidate, evaluate the RHS:

| RHS shape | TypeName | Kind |
|---|---|---|
| `LiteralExpression` (`String` literal kind) | `"String"` | `Literal` |
| `LiteralExpression` (`Integer`/`Hex`/`Octal`) | `"Long"` | `Literal` |
| `LiteralExpression` (`Float`) | `"Double"` | `Literal` |
| `LiteralExpression` (`Date`) | `"Date"` | `Literal` |
| `TrueExpression` / `FalseExpression` | `"Boolean"` | `Literal` |
| `NewExpression` | TypeReference leaf | `NewExpression` |
| Identifier/Member that resolves to a typed `VariableDeclaration` / `ParameterDeclaration` | declared type leaf | `BoundDeclarationType` |
| `IndexOrCallExpression` whose target resolves to a `FunctionDeclaration` / `PropertyDeclaration(Get)` with a `ReturnType` | return type leaf | `ProcedureReturnType` |
| `IndexOrCallExpression` whose target resolves to a `BuiltInFunction` with non-null `ReturnType` | built-in return type | `BuiltInReturnType` |
| `IndexOrCallExpression` whose target resolves to a typed declaration whose type has a `IsParameterized` default-member registry entry with non-null `ReturnType` | default-member return | `DefaultMemberReturnType` |

A `LetStatement` and `SetStatement` are both processed. VBA's
`Set`-vs-`Let` distinction matters at runtime (object reference vs.
value copy), but for *type inference of a Variant target* both
contribute evidence about what the variable is being assigned.

`NullExpression`, `NothingExpression`, `EmptyExpression`, polymorphic
built-in returns (`ReturnType: null` — e.g., `IIf`, `Nz`), and
unresolved RHS expressions yield no evidence. The pass is silent
rather than recording `Variant`, because "we don't know" is different
from "the value is explicitly Variant."

### 5.3 Default-member chain depth

The pass follows **at most one** default-member hop on the RHS — the
parameterized form (`rs("X")` where rs is `Recordset`, yielding
`Field`). The plan's example `Set rs = CurrentDb.OpenRecordset(...)`
requires a *method-on-type* registry that does not exist yet (that is
Phase 4 / type-library territory); this pass cannot synthesize that
chain today. Recording a single hop covers the canonical fixture
patterns and avoids cascading-inference bugs that would be hard to
audit. Future sessions extending the registry can lift this cap by
recursing on the result.

The choice to stop at one hop is deliberate: the plan's "Don't
overinvest" guidance applies, and the consumer's downstream AI can
chain hints itself when a multi-step inference is needed.

## 6. Confidence algorithm

After collecting evidence for a candidate:

1. **Definitive case — TypeSuffix.** If any evidence row has
   `Kind = TypeSuffix`, the inferred `TypeName` is the suffix-implied
   type and `Confidence = High`. Other evidence rows are still
   recorded in the `Evidence` list (a string-suffixed variable that
   gets a numeric assignment is *invalid VBA* but the source said what
   it said — preserving the evidence lets consumers see the conflict
   without losing data).

2. **Otherwise — vote on the type names.** Group evidence rows by
   `TypeName` using `Declaration.NameComparer` (ordinal, case-insensitive).

   - If **one** distinct type name was seen: `Confidence = High`,
     `TypeName` = that name.
   - If **one type name accounts for strictly more than half** the
     evidence rows: `Confidence = Medium`, `TypeName` = the dominant
     name. (Strict majority — exactly half is not enough.)
   - **Otherwise** (no majority — ties or three-way splits):
     `Confidence = Low`, `TypeName` = the most-frequent name with
     first-seen breaking ties.

3. **No-evidence case.** If the candidate gathered zero evidence rows,
   no entry is emitted in the map. `InferredTypeMap.TryGet(decl)`
   returns null.

The strict-majority threshold (>0.5) was chosen over a softer ratio
(e.g., ≥0.6) because the evidence counts are small in practice. A
2-of-3 split is a clear majority; a 2-of-4 tie is genuinely
indeterminate.

## 7. API surface

### 7.1 `TypeInferencePass`

```csharp
public static class TypeInferencePass
{
    public static InferredTypeMap Run(ResolvedModule resolved);
}
```

Static — the pass holds no configuration. The default-member registry
is consulted directly via `DefaultMemberRegistry`; introducing an
overridable lookup interface would be premature abstraction per
`project-guidance.md` → Lean By Default. If a future session needs to
swap the registry (custom-control hosts, third-party COM types), the
interface lands at that point.

The pass takes `ResolvedModule` rather than `(ModuleScope, Module)`
because the inference algorithm depends on `TryGetDeclaration` for
RHS evaluation, which the wrapper provides O(1).

### 7.2 Consumer pattern

```csharp
var resolved = resolver.Resolve(moduleScope, ast);
var inferred = TypeInferencePass.Run(resolved);

if (inferred.TryGet(varDecl) is { } hint
    && hint.Confidence != InferenceConfidence.Low)
{
    // use hint.TypeName
}
```

The `Low` band is *recorded* (it carries information) but consumers
that only want trustworthy hints can filter it out as shown.

## 8. Resolver integration: explicitly deferred

This session does **not** wire the inferred types back into the
resolver's reference resolution. The resolver continues to emit
`Unresolved("default-member-not-yet-resolved")` exactly as it does
today; downstream enrichment can read the inferred-type map alongside
the resolver result without further plumbing.

Wiring is Session 4 (cross-cut + mechanical tightening) work. The
reasons to split:

- The map is a useful artifact even without resolver integration —
  it is a debug surface and an AI-translation hint.
- The resolver's wiring needs the default-member registry, the
  inferred type map, and form/report metadata (Phase 4) for the full
  Forms!frmFoo!ctl chain. Wiring with only Session 1 + 2 partially
  resolves chains in a way Session 4 would have to undo.

## 9. Out of scope for this session

- **Resolver wiring.** Session 4. The resolver's `DecideMemberOutcome`
  continues to emit the existing `Unresolved` outcomes.
- **Cross-procedure inference.** Parameter type narrowing from call
  sites, return-type narrowing for procedures whose body assigns a
  consistent type — both require whole-program analysis the pass does
  not do today. Future-work flag.
- **Method-on-type registry.** A registry of methods-and-their-return-types
  on Access object-model types (e.g., `Database.OpenRecordset() -> Recordset`).
  That is Phase 4 work; the inference pass lifts naturally once it
  exists.
- **Inferred-type back-write to the original declaration.** The plan
  forbids it: "Inferred types annotate the AST but never replace the
  original declaration." `ImplicitDeclaration.Type` is intentionally
  not mutated even though its setter is internal.
- **Consumer-extension protocol.** No `ITypeInferenceProvider` interface
  is introduced — the pass is internal infrastructure that the resolver
  consumes. Premature per "Lean By Default" until a second consumer
  exists.

## 10. Test plan

### 10.1 Inference-pass tests
(`tests/AccessHarvester.Vba.Resolver.Tests/Types/TypeInferencePassTests.cs`)

Pinpoint hand-built scenarios. Each test constructs a small VBA
source via the existing parser-and-scope test fixtures, runs the
resolver, then runs `TypeInferencePass.Run` and asserts on the
resulting `InferredTypeMap`.

- `Run_VariantWithSingleStringLiteralAssignment_InfersStringHigh`
- `Run_VariantWithMultipleConsistentLiterals_InfersHigh`
- `Run_VariantWithMixedLiterals_InfersDominantTypeMediumOrLow`
  (theory: `2-of-3 string` → Medium; `2-of-4 string` → Low; `1-of-1` → High)
- `Run_VariantWithNewExpression_InfersClassNameHigh`
- `Run_VariantWithSetFromTypedVariable_InfersTargetTypeHigh`
- `Run_VariantWithBuiltInReturnTyped_InfersReturnTypeHigh`
  (e.g., `x = Format(...)` → "String")
- `Run_VariantWithBuiltInReturnPolymorphic_GathersNoEvidence`
  (e.g., `x = IIf(...)` → no entry)
- `Run_VariantWithDefaultMemberCall_InfersFieldHigh`
  (e.g., `Dim v As Variant : v = rs("X")` where rs is Recordset → "Field"
   via the parameterized default-member chain)
- `Run_VariantWithTypeSuffix_InfersSuffixTypeHighRegardlessOfAssignments`
- `Run_DeclaredAsVariantExplicitly_StillCandidate`
- `Run_DeclaredAsString_NotACandidate`
- `Run_ConstantDeclaration_NotACandidate`
- `Run_ParameterDeclaration_NotACandidate`
- `Run_ImplicitDeclaration_IsCandidate`
- `Run_VariantWithNoAssignment_HasNoEntry`
- `Run_VariantWithBoolLiteralAssignment_InfersBooleanHigh`
- `Run_VariantWithDateLiteralAssignment_InfersDateHigh`
- `Run_VariantWithNothingAssignment_GathersNoEvidence`
- `Run_VariantWithUnresolvedRhs_GathersNoEvidence`

### 10.2 Map-shape tests

- `InferredTypeMap_TryGet_ReturnsNullForUnknownDeclaration`
- `InferredTypeMap_AllIsImmutable` — regression for the
  `ReadOnlyDictionary` wrapping (mirrors the `DefaultMemberRegistry`
  test from Session 1).
- `InferredTypeMap_KeyEqualityIsReferenceIdentity` — two distinct
  declarations with the same name register independently.

### 10.3 Confidence-rule tests

Direct unit tests of the algorithm (smaller than full inference
fixtures) — pure-logic per `project-guidance.md` → Testing
Conventions. The tests assemble synthetic `TypeEvidence` lists and
assert on the resulting `(TypeName, Confidence)`.

- `Confidence_TypeSuffixDominatesEverything`
- `Confidence_AllSameType_IsHigh`
- `Confidence_StrictMajority_IsMedium`
- `Confidence_NoMajority_IsLowWithMostFrequent`
- `Confidence_TwoOfThree_IsMedium`
- `Confidence_TwoOfFour_IsLow`
- `Confidence_ThreeWayTie_IsLowFirstSeenWins`

## 11. Stable-contract notes

The §3 record/enum shapes form a public contract. Future sessions may
*add* `TypeEvidenceKind` values, *add* fields to `InferredType` (e.g.,
a chain depth), and *raise* the default-member chain depth past 1.
Sessions should not *rename* or *reshape* the existing fields without
coordinating with consumers — Session 4's resolver wiring will read
every field and a downstream enrichment session is expected to read
`InferredType` for translation hints.

The map's key being `Declaration` (reference equality) means a
consumer that re-runs the resolver gets a different `Declaration`
instance and must re-run the pass — there is no cross-run identity.
That's intentional; type inference is inexpensive enough to
re-compute, and a stable identity scheme for declarations is a larger
architectural commitment than this session warrants.
