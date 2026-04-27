# Session 2 Synthesis — With-Block Flattening Pass

> **Authored:** 2026-04-27 in-conversation by Claude Opus 4.7 (orchestrator).
> **Routing decision:** Per `project-guidance.md` Convention "When the
> orchestrator's model matches the routing target...", Opus 4.7 authored
> this synthesis directly rather than calling `route(architecture)`,
> whose target is also Opus. The non-negotiable cross-provider gate
> (`session-verification`) still runs against gpt-5-4 at end of session.
> **Scope:** With-block flattening pass per Phase 3.4 of
> `docs/VBA-Parser-Migration-Harvester-Plan.md`. Builds on the Session 1
> reference resolver — replaces the two
> `Unresolved("with-block-flattening-deferred")` branches with an
> annotation-based flattening that resolves `.Member` / `!Member` against
> the enclosing `With` target.

## 1. Goal

Inside `With expr ... End With`, statements like `.Member` and `!Member`
implicitly bind their leading dot/bang to `expr`. Session 1 emitted
`Unresolved("with-block-flattening-deferred")` for both
`ImplicitWithMemberExpression` and `ImplicitWithBangExpression` because
the flattening pass had not yet been written. Session 2 replaces those
branches: the resolver now synthesizes the full reference (`expr.Member`
or `expr!Member`) and resolves it against the enclosing scope chain,
recording the synthesized expression as an annotation so the original
implicit-with AST node is preserved.

Per the spec's Feature 2 → Standards: "Flattening is non-destructive:
the original `.`/`!` access node is preserved with an annotation
pointing at the synthesized full reference. The downstream consumer can
render either form."

## 2. Inputs and outputs

Inputs and outputs are unchanged from Session 1. The resolver still
consumes a `ModuleScope` plus the parser AST `Module` and emits an
`IReadOnlyList<ResolvedReference>`. What changes is the
`ResolutionOutcome` variant set: a new variant
`ResolvedViaWithFlattening` carries the synthesized full reference and
the inner outcome of resolving it.

## 3. ResolutionOutcome additions

A new sealed variant is added to the closed `ResolutionOutcome` union.
Per Session 1 §10's stability commitment ("Session 2 may add reasons;
existing variants and reasons must not be renamed or repurposed"), this
extension is additive and does not rename, repurpose, or reshape any
existing variant or reason.

| Variant | When |
|---|---|
| `ResolvedViaWithFlattening(Expression SynthesizedTarget, ResolutionOutcome InnerOutcome)` | Implicit-with member (`.X` or `!X`) inside a `With` block; `SynthesizedTarget` is the synthesized full-reference AST (`MemberAccessExpression(withTarget, X)` or `BangAccessExpression(withTarget, X)`); `InnerOutcome` is the result of resolving that synthesized reference's tail member against the enclosing scope chain. |

`InnerOutcome` itself is one of the existing five Session 1 variants
(`Resolved`, `ResolvedToObjectModel`, `ResolvedToBuiltIn`,
`ResolvedToMe`, `Unresolved`) — never another
`ResolvedViaWithFlattening`. That keeps switch-on-outcome exhaustive at
two levels at most.

The string constant `UnresolvedReasons.WithBlockFlatteningDeferred`
remains in the closed reason set as a defensive fallback (see §4.4) and
is not renamed or repurposed.

## 4. Algorithm

### 4.1 Locating the enclosing With target

The walker maintains the existing scope stack. When an
`ImplicitWithMemberExpression` or `ImplicitWithBangExpression` is
visited, the resolver walks up `CurrentScope` parent links to find the
nearest enclosing `WithBlockScope`. Because the walker pushes a
`WithBlockScope` for each `WithStatement` body it enters, the nearest
ancestor on the chain is by construction the innermost open `With`.
Per Phase 3.4: "Nested `With` blocks: the innermost one wins for `.`
references." The parent walk satisfies this without extra bookkeeping —
nested constructs that do not introduce scopes (`If`, `For`, `Do`, etc.)
are transparent to the chain, and constructs that do introduce scopes
(`ForEachScope`, nested `WithBlockScope`, nested `ProcedureScope`) are
walked through until the innermost `WithBlockScope` is found.

```csharp
private static WithBlockScope? FindEnclosingWith(Scope from)
{
    for (Scope? s = from; s is not null; s = s.Parent)
        if (s is WithBlockScope w) return w;
    return null;
}
```

The walk halts at any non-WithBlockScope null parent (`ProjectScope`'s
parent is null), which is the defensive fallback case (see §4.4).

### 4.2 Synthesizing the full reference

Given the enclosing `WithBlockScope` and the implicit-with site's
member name, the resolver constructs a synthetic AST node. Before
synthesis, the `With` target is **fully expanded**: when the recorded
outcome at the With target's AST node is itself
`ResolvedViaWithFlattening`, the resolver substitutes that outcome's
`SynthesizedTarget`. This handles `With .Foo ... .Bar` inside an outer
`With outerRs`: without expansion, the inner `.Bar`'s synthesised
target would still contain the `.Foo` ImplicitWith node; with
expansion, the synthesised target is `outerRs.Foo`.

```csharp
Expression effectiveTarget = ExpandFlattenedTarget(withScope.Target);
Expression synthesized = isBang
    ? new BangAccessExpression(site.Provenance, effectiveTarget, memberName)
    : new MemberAccessExpression(site.Provenance, effectiveTarget, memberName);
```

`ExpandFlattenedTarget` peels through `ParenthesizedExpression` (via
`UnwrapTransparent`) and through `ResolvedViaWithFlattening` outcomes
recursively, terminating when no recorded flattening outcome remains.
By the source-order construction of the walker, each
`SynthesizedTarget` was already fully expanded when emitted, so
recursion typically halts after one substitution; the recursive shape
defends against future ordering changes.

The synthesized node shares the implicit-with site's `Provenance` so a
downstream consumer asking "where in the source did this reference
occur?" gets the user-visible site, not the `With` header line.

The synthesized node is **not** added to `_outcomesByExpression` and
**not** emitted as a separate `ResolvedReference`. It exists only as
the `SynthesizedTarget` annotation on the original implicit-with site's
outcome. The original AST node and the original `ResolvedReference`
entry are the canonical record; the synthesized form is the
flattening's annotation.

### 4.3 Resolving the synthesized reference

The synthesized reference resolves the same way the original
`MemberAccessExpression` / `BangAccessExpression` dispatch path
resolves: the existing `DecideMemberOutcome(target, memberName, isBang)`
helper is called with `target = withScope.Target`. That helper consults
`_outcomesByExpression` to look up the With target's recorded
outcome — which exists because `Visit(n.Target)` ran during the parent
`WithStatement`'s walk at the OUTER scope (before the WithBlockScope was
pushed). The dispatch then uses the existing decision table from
Session 1 §4.4 to produce an `InnerOutcome`:

| With-target outcome | Inner outcome of `.X` / `!X` |
|---|---|
| `ResolvedToMe(class)` for a Form/Report | Optimistic registry control lookup → `ResolvedToObjectModel` if hit, else `Unresolved("default-member-not-yet-resolved")`. |
| `ResolvedToObjectModel(decl)` | `Unresolved("default-member-not-yet-resolved")` (registry sub-child lookup is #11's job; Session 2 stub returns null). |
| `Resolved(decl)` | Same as above — until Phase 3.5/3.6 lands type info, the inner outcome is `default-member-not-yet-resolved`. |
| `Unresolved(...)` | `Unresolved("member-access-on-unresolved-target")`. |
| Other (no recorded outcome / `ResolvedToBuiltIn`) | `Unresolved("default-member-not-yet-resolved")`. |

This deliberately reuses the existing decision table — flattening does
not change *how* a member access resolves once the target is known; it
only supplies the target. Phase 3.5 / #11 will progressively replace
`default-member-not-yet-resolved` branches and the wins will flow
through both ordinary and flattened sites uniformly.

### 4.4 Defensive fallback

If `FindEnclosingWith` returns null (no enclosing `WithBlockScope` is
on the chain), the resolver falls back to Session 1's behavior and
emits `Unresolved("with-block-flattening-deferred")`. The grammar does
not produce `ImplicitWithMemberExpression` outside a `With` body, so
this branch is structurally unreachable from valid input — but
defending it costs nothing and keeps the closed reason-string set
populated for any future grammar change or scope-construction bug.

### 4.5 Chained implicit-with: `.Foo.Bar`

A chained access whose root is an implicit-with member (e.g.,
`.Foo.Bar.Baz`) parses as:

```
MemberAccessExpression(
    target: MemberAccessExpression(
        target: ImplicitWithMemberExpression("Foo"),
        member: "Bar"),
    member: "Baz")
```

The walker visits the inner `ImplicitWithMemberExpression` first and
records its outcome as
`ResolvedViaWithFlattening(synthesizedFoo, innerFooOutcome)`. When the
outer `MemberAccessExpression` then asks
`_outcomesByExpression[implicitWithFooSite]` for its target's outcome,
`DecideMemberOutcome` must peek through the `ResolvedViaWithFlattening`
wrapper to dispatch on the inner outcome:

```csharp
while (targetOutcome is ResolutionOutcome.ResolvedViaWithFlattening f)
    targetOutcome = f.InnerOutcome;
```

The peek is a `while` loop for symmetry, but in practice never iterates
more than once: `InnerOutcome` is one of the five Session 1 variants
and never another `ResolvedViaWithFlattening` (see §3 invariant).

### 4.6 Cross-reference back-population

The Session 1 `Emit` populates `Declaration.References` when the
outcome is `Resolved` or `ResolvedToObjectModel`. Session 2 extends
this to peek through `ResolvedViaWithFlattening` and back-populate the
inner outcome's declaration when applicable:

```csharp
case ResolutionOutcome.ResolvedViaWithFlattening f:
    switch (f.InnerOutcome)
    {
        case ResolutionOutcome.Resolved ir:
            ir.Declaration.AddReference(...);
            break;
        case ResolutionOutcome.ResolvedToObjectModel iom:
            iom.Declaration.AddReference(...);
            break;
    }
    break;
```

The flattening therefore preserves Phase 3.7's reverse-lookup
seeding — a flattened `.cboCity` that resolves through a registry
control declaration still appears in that declaration's `References`
list, so cross-reference queries do not need a separate "include
flattened sites" pass.

## 5. File layout

```
AccessHarvester.Vba.Resolver/References/
  ResolutionOutcome.cs        ← MODIFY: add ResolvedViaWithFlattening variant
  ResolvedReference.cs        ← MODIFY: docstring updates only
  ReferenceResolver.cs        ← MODIFY:
                                  - VisitImplicitWithMemberExpression
                                  - VisitImplicitWithBangExpression
                                  - DecideMemberOutcome (peek-through)
                                  - Emit (back-population peek-through)
                                  - FindEnclosingWith helper
AccessHarvester.Vba.Resolver/Scopes/
  WithBlockScope.cs           ← unchanged (provided by #7; the comment's
                                forward reference to "#8 will flatten"
                                stays accurate post-Session 2 — its
                                contract is still "carry the target
                                expression for #8 to flatten").
```

Tests:

```
tests/AccessHarvester.Vba.Resolver.Tests/References/
  ReferenceResolverTests.cs   ← MODIFY:
                                  - rewrite ImplicitWithMember/Bang tests
                                    to assert ResolvedViaWithFlattening
                                  - add nested-With innermost-wins test
                                  - add chained-implicit-with test
                                  - add back-population-through-flattening test
                                  - add Me-form-flattening test (Me.ctl
                                    via With Me)
                                  - add fixture-driven assertion that
                                    ResolvedViaWithFlattening is now an
                                    accepted outcome variant
```

## 6. What this session deliberately does NOT do

- **Replace `default-member-not-yet-resolved`** — the inner outcome of a
  flattening is whatever the existing decision table returns. Phase 3.5
  / #9 covers default-member resolution and will progressively replace
  those inner outcomes.
- **Rewrite the AST.** The synthesized expression is annotation-only;
  it does not appear in the parser AST tree, is not emitted as a
  separate `ResolvedReference`, and is not stored in
  `_outcomesByExpression`.
- **Resolve cross-module Public visibility for the With target.** If
  `expr` in `With expr` itself fails to resolve (e.g., it names a
  Public sub from another module not yet wired), the inner outcome is
  `Unresolved("member-access-on-unresolved-target")` — the same
  cross-module gap that Phase 3.4 / #9 covers for ordinary references.
- **Refactor `WithBlockScope.cs`.** Its current shape (parent, target,
  range) is exactly what flattening needs. The only artifact is the
  comment forward-referencing #8: it remains accurate as documentation
  of the scope's purpose, even now that #8's flattening pass is the
  consumer.

## 7. Test plan

Hermetic synthetic-source tests:

1. `ImplicitWithMember_InsideWith_FlattensToMemberAccess` — `.Foo`
   inside `With rs` where `rs` is a local `Object` Dim → outcome is
   `ResolvedViaWithFlattening` whose `SynthesizedTarget` is a
   `MemberAccessExpression(rs, "Foo")` and whose `InnerOutcome` is
   `Unresolved("default-member-not-yet-resolved")` (rs resolves; sub-
   child is null until #11 fills the registry).
2. `ImplicitWithBang_InsideWith_FlattensToBangAccess` — `!Foo` inside
   `With rs` → mirror of (1) using `BangAccessExpression`.
3. `ImplicitWith_InsideNestedWith_UsesInnermostTarget` — outer
   `With rs`, inner `With Me.sfrmInspections.Form`, `.RecordsetClone`
   inside the inner With → `SynthesizedTarget.Target` is the inner
   `Me.sfrmInspections.Form` expression, NOT `rs`.
4. `ChainedImplicitWith_DispatchesThroughFlattening` —
   `.cboCity.Column(2)` inside `With Me` (Form module) where the
   registry returns a fake `cboCity` → outer `MemberAccessExpression`
   peeks through the inner `ResolvedViaWithFlattening` and dispatches
   on its inner outcome (`ResolvedToObjectModel`).
5. `ImplicitWith_OnFormMe_RoutesThroughControlRegistry` — `.cboCity`
   inside `With Me` in a Form module with a stub registry that returns
   a fake control → `InnerOutcome` is `ResolvedToObjectModel`
   pointing at the fake control.
6. `Flattening_BackPopulatesDeclarationReferences` — `.X` inside
   `With Me` whose flattening produces `Resolved`/`ResolvedToObjectModel`
   inner outcome → that declaration's `References` list gains an entry.
7. `SynthesizedTarget_SharesImplicitWithSiteProvenance` — the
   synthesized AST node's `Provenance` equals the implicit-with site's
   `Provenance` (so downstream "where in source" queries return the
   user-visible location).

Fixture extension: the existing
`Fixture_RunsToCompletionWithKnownReasonCodes` test must accept
`ResolvedViaWithFlattening` as a recognised outcome variant. Fixture
03's nested-With scenarios become live regression coverage for
flattening behaviour automatically, without per-fixture expected
counts (those land in Session 4).

## 8. Stability commitments

Once this session lands, the following surfaces are stable for
Sessions 3–4:

- The `ResolutionOutcome` variant set now includes
  `ResolvedViaWithFlattening`. Its two-property shape
  (`SynthesizedTarget`, `InnerOutcome`) is part of the public contract.
- `InnerOutcome` is never another `ResolvedViaWithFlattening` (no
  nesting). Consumers' switch handlers can rely on a single peek-through.
- The `UnresolvedReasons.WithBlockFlatteningDeferred` constant remains
  in the closed reason set as the defensive fallback for the
  no-enclosing-With path — its meaning is preserved (the flattening
  pass could not run), it is not repurposed.
- `ResolvedReference`'s four-property shape is unchanged; flattening is
  expressed entirely through the new `ResolutionOutcome` variant.

Session 3's `ResolvedModule` wrapper still wraps the unchanged
`IReadOnlyList<ResolvedReference>` output; its public surface is
additive.
