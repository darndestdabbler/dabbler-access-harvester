You are the cross-provider session-verifier for Session 2 of the session set
`harvester-finish-canonical-parse-success-report` in the dabbler-access-harvester
repo. This is Round 3 — the final retry under the workflow's max-2-retries cap.

Round 2 confirmed the R1 Majors resolved and surfaced one new Minor:
the global authority framing (opening "Authority for the reason set"
block, closing "Stability commitment" block) still claimed
`SuggestedNextStepsLookup` was the full source-of-truth even though the
narrative intentionally includes `member-not-found-at-segment-<N>` as a
documented dynamic-prefix exception. Both spots have been rewritten to
qualify the lookup's role to stable lookup-backed reasons only and to
name the `MemberNotFoundAtSegment(int)` factory as the basis for the
dynamic-prefix exception.

**Your response MUST begin with a detailed prose analysis covering each
concern below BEFORE the JSON block at the end.** Do not skip the prose
analysis.

Analyse the following concerns in order:

1. **R2 Minor — opening Authority block.** The opening block now
   describes `SuggestedNextStepsLookup` as authoritative for stable
   lookup-backed reasons and names `MemberNotFoundAtSegment(int)` as
   the basis for the dynamic-prefix exception. Is the new framing
   complete and internally consistent?
2. **R2 Minor — closing Stability commitment.** The closing section
   now distinguishes stable lookup-backed reasons (which land via the
   lookup) from dynamic-prefix families (which use
   `GenericSuggestedNextSteps`). Is the distinction explicit enough,
   and is the "new dynamic-prefix family appears" path described
   clearly enough that a future author would follow the pattern?
3. **Residual stale phrasing anywhere else.** Is there any other
   place in the document that still implies every reason in the
   document is a lookup key, or that the lookup is the singular
   reason-set source-of-truth?
4. **New drift.** Did the R2 fixes introduce any new typo,
   contradiction, or shape mismatch?

Reminder: the four in-scope code/test files were R1-VERIFIED and have
not been touched since. Do not flag them as missing.

---

## R2 verifier review (your prior findings)

```
1. **R1 Issue 1 — `member-not-found-at-segment-<N>` framing**

The revised subsection is **substantially fixed**. The new title, **“Dynamic-prefix family: `member-not-found-at-segment-<N>`”**, and especially the opening bold paragraph now make the key distinctions explicit:

- it is **not** a stable lookup-backed entry,
- `<N>` is emitted dynamically,
- consumers should match by **prefix**,
- and the listed next steps are **generic fallback**, not a verbatim `SuggestedNextStepsLookup` value.

Within that subsection itself, I do **not** see residual wording that still presents `<N>` as a literal stable reason code.

However, this fix exposed a remaining **document-wide framing mismatch** elsewhere: the opening “Authority for the reason set” text and the closing “Stability commitment” still speak as if the full documented reason set comes from `SuggestedNextStepsLookup`. That is no longer strictly true once this dynamic family is intentionally documented as an exception. So the subsection framing itself is adequate, but the global framing is not fully cleaned up.

2. **R1 Issue 2 — `unknown-expression` analyzer-support contradiction**

This is **resolved**.

The rewritten “Why the producer could not finish” paragraph now says the analyzer recognizes only a narrow set of primitives and that function calls — including `Replace`, `IIf`, `Format`, and `String$` — are treated as unknown holes, with a deferred-gap comment in `DynamicSqlAnalyzer.cs` cited as the basis. That is now consistent with the verbatim next steps telling the reader to consider adding recognizers for examples such as `Replace` and `IIf`.

I do not see any remaining internal contradiction in that section, and I do not see any other place in the document still claiming `Replace`/`IIf` are already recognized today.

3. **R1 Issue 3 — FunctionClassification per-pair coverage**

This is **resolved**.

The section now has five explicit `FunctionClassification` subentries:

- `not-in-scope`
- `me-in-standard-module`
- `default-member-not-yet-resolved`
- `with-block-flattening-deferred`
- `member-access-on-unresolved-target`

Each one is a distinct `(Source, Reason)` entry, each cross-references `Resolver` for the fuller explanation, and each pins the corresponding suggested-next-steps. That satisfies the spec intent behind “one paragraph per `(Source, Reason)` pair”: the pairs are no longer collapsed into a source-level summary.

4. **Post-fix sweep**

There **is** still some stale pre-fix language outside the repaired subsections.

The main remaining problem is that the document still overstates the authority of `SuggestedNextStepsLookup` in a couple of global places:

- **Opening authority block:**  
  “The aggregator's lookup is the source of truth for both the reason codes and the recommended next steps...”  
  That is too broad now that the doc explicitly includes one documented dynamic family whose emitted reasons are not lookup keys and whose steps come from generic fallback.

- **Stability commitment:**  
  “The reason set above is the closed set as of 2026-05-17 in the `UnresolvedItemAggregator.SuggestedNextStepsLookup` map.”  
  This is also too broad for the same reason. The documented content now includes six stable `SqlFormReference` reasons **plus** one dynamic-prefix family outside that map.

There is one softer spot in **Cross-cutting consumer guidance** (“Reason codes are stable strings... including the `member-not-found-at-segment-<N>` family by prefix”), but that sentence is at least immediately qualified by the prefix note, so I would treat it as optional tightening rather than a separate defect.

5. **New drift**

I do **not** see any new substantive contradiction introduced by the R1 fixes in the repaired sections themselves. The `unknown-expression` fix is coherent, and the `FunctionClassification` reshaping is clean.

The only remaining issue is the residual global lookup-authority wording described above. I would classify that as a **Minor** documentation inconsistency, not a new major drift.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md (opening authority block; Stability commitment)",
      "summary": "The document still globally describes `SuggestedNextStepsLookup` as the source of the full documented reason set even though the narrative now intentionally includes the dynamic `member-not-found-at-segment-<N>` family outside that lookup.",
      "fix": "Qualify the global framing to say the lookup is the source of truth for stable lookup-backed reasons and their verbatim steps, while `member-not-found-at-segment-<N>` is a documented dynamic-prefix exception that uses `GenericSuggestedNextSteps` fallback."
    }
  ]
}
```
```

---

## Revised `unresolved-reason-narrative.md` (post-R2)

```
# Unresolved-Reason Narrative

> **Status:** Session 2 deliverable for
> `harvester-finish-canonical-parse-success-report`.
> **Authority for the reason set:** the closed
> `UnresolvedItemAggregator.SuggestedNextStepsLookup` in
> [`AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs`](../../../AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs)
> for the **stable lookup-backed reasons and their verbatim
> next-step lists**, plus the
> `UnresolvedReason.MemberNotFoundAtSegment(int)` factory in
> `AccessHarvester.Sql.Parser/FormReferences/UnresolvedReason.cs`
> for the one documented **dynamic-prefix exception**
> (`member-not-found-at-segment-<N>`), which has no lookup key and
> falls through to the aggregator's `GenericSuggestedNextSteps`
> list. This document explains the *construct* each reason flags
> and the *downstream-AI interpretation* a Blazor-migration agent
> should apply when it encounters one in a `UnresolvedItems[]`
> entry. For stable reasons, the "Verbatim suggested-next-steps"
> piece is pinned from the lookup; the dynamic-prefix family is
> called out explicitly where it appears.
> **Audience:** the downstream AI consumer of the harvester's
> `UnresolvedItems[]` output (and a human reviewer reading the same
> manual-review queue).

---

## How to read this document

Each section below covers one `UnresolvedItem.Source` enum value
(`Resolver`, `SqlFormReference`, `DynamicSqlSite`, `NavigationTarget`,
`FunctionClassification`). Within each section, one subheading per
stable `Reason` code from the aggregator's closed reason set. Each
entry has four pieces:

- **Construct** — what the producer saw in the source.
- **Why the producer could not finish** — the constraint that caused
  the construct to surface as an unresolved item rather than a fully
  resolved record.
- **Downstream-AI interpretation** — how a migration agent should
  understand the entry when it reads it from `UnresolvedItems[]`.
- **Verbatim suggested-next-steps** — pinned from
  `UnresolvedItemAggregator.SuggestedNextStepsLookup` so consumers can
  cross-reference the prose against the mechanical hint list.

One exception to "one subheading per stable reason code" is the
`SqlFormReference` source, which carries one dynamic-prefix family
(`member-not-found-at-segment-<N>`) alongside its six stable
reasons. The dynamic family has no lookup key — its emitted reason
varies per occurrence — and falls through to
`GenericSuggestedNextSteps` rather than a verbatim lookup entry. It
is documented in its own subsection within the `SqlFormReference`
section and the framing there makes the distinction explicit. The
"verbatim suggested-next-steps" piece does not apply to that
subsection.

The `UnresolvedItem.Location` and `SurroundingText` slots point at the
construct's source; the consumer should re-read those before acting
on the recommendation here. Stable reason codes never get renamed —
future producer revisions append to the set — so a migration agent
may match on `Reason` exactly for stable codes, or by prefix for the
documented dynamic family above.

> **Two reasons covered here are not yet measured by Session 1's
> reports.** Both fixture reports ship `unresolvedItems.status =
> "not-measured"` for the structural reason that wiring the live
> aggregator requires the full enrichment pipeline (resolver /
> dynamic-SQL / navigation / function-classification), which lives
> in `harvester-finish-canonical-golden-output`. This narrative
> covers the reason set the aggregator currently emits so it is
> reusable as-is when the live wiring lands; no edits to this file
> are anticipated unless a producer introduces a *new* reason code.

---

## Source: `Resolver`

Resolver entries are Warning-severity outcomes from the VBA name
resolver (`AccessHarvester.Vba.Resolver.References.ResolverDiagnostic`).
Info-severity diagnostics are deferred-by-design (a later resolver pass
closes them) and the aggregator drops them from the manual-review
queue.

### `not-in-scope`

- **Construct:** a bare identifier reference (single name, no
  member-access dot) used in an expression position whose declaration
  the resolver could not locate in any visible scope.
- **Why the producer could not finish:** the resolver walked the
  procedure-local, module-local, and project-Public scopes and found
  no matching name. The most common causes are (a) the identifier
  refers to a Public symbol in a module the resolver did not load,
  (b) the identifier is dead code from a prior edit, or (c) the
  identifier is a built-in Access expression service (`Date()`,
  `Nz()`, `DLookup()`) that lives in the host application rather than
  the VBA project. The resolver does not assume (c) on its own —
  consumer code is expected to consult the built-in registry
  before treating the entry as a real gap.
- **Downstream-AI interpretation:** treat as a name-resolution gap.
  Before recommending a "remove dead code" fix, check whether the
  name matches a known Access-expression-service builtin. If it
  does, the entry is benign and the migration agent should map the
  call to the corresponding host service on the target platform
  (e.g., Blazor service injection for `Nz` / `DLookup` analogues).
  If the name does not match a builtin, the migration agent should
  surface the call site to the human for a "is this dead code?"
  judgment rather than silently dropping it.
- **Verbatim suggested-next-steps:**
  - Confirm whether the identifier is declared in another module's
    Public scope.
  - If the identifier is intentional dead code, mark it as such.

### `me-in-standard-module`

- **Construct:** a `Me.<member>` reference appearing inside a standard
  `.bas` module (no class / form / report context).
- **Why the producer could not finish:** `Me` has no meaning in a
  standard module — the binding requires a class instance, a form
  module, or a report module. The construct compiles but never
  evaluates at runtime; the resolver flags it rather than silently
  failing.
- **Downstream-AI interpretation:** treat as a latent runtime bug in
  the original VBA — the migration agent should rewrite the call to
  name the receiver explicitly (a specific form / report / class
  instance) on the target platform. If the migration agent cannot
  infer the intended receiver, surface to the human.
- **Verbatim suggested-next-steps:**
  - Move the `Me` reference into a class / form / report module, or
    rewrite to name the target explicitly.

### `default-member-not-yet-resolved`

- **Construct:** a member-access expression (`receiver.member`) whose
  resolution depends on the receiver's default-member or type-
  inference pass, neither of which had executed at the point of
  resolution.
- **Why the producer could not finish:** the resolver's name pass runs
  before the default-member and type-inference passes; a member access
  on a receiver whose type the early pass could not pin defers to a
  later pass. If the later pass succeeds, the unresolved entry is
  reissued as resolved (and does not surface here); if it does not,
  the entry survives to the manual-review queue.
- **Downstream-AI interpretation:** treat as a type-inference gap, not
  a name-resolution gap. The migration agent should attempt to infer
  the receiver's type from surrounding code (assignments, declarations
  the resolver may have missed because of late binding) before
  surfacing the entry. If the inference succeeds, the agent may
  proceed with the resolved member; if it fails, surface to the human
  for a type annotation.
- **Verbatim suggested-next-steps:**
  - Re-run after the default-member resolution pass has executed; if
    still unresolved, supply a type annotation for the receiver.

### `with-block-flattening-deferred`

- **Construct:** an implicit-with member reference (`.member` with no
  receiver) appearing outside any visible `With` block at the point
  the resolver examined it.
- **Why the producer could not finish:** the resolver's With-block
  flattening pass associates implicit-with members with their
  enclosing `With` target; references the pass could not match (e.g.,
  because the `With` block was not in scope at the AST position the
  resolver was processing) are deferred. The aggregator emits the
  unresolved record so the deferred case is visible rather than
  silent.
- **Downstream-AI interpretation:** treat as a probable
  parser-AST-shape issue rather than a true unresolved reference.
  The migration agent should re-examine the source around the
  reference for an enclosing `With` block the resolver may not have
  seen; if one is present in source but the resolver missed it,
  surface as a parser/resolver bug rather than a migration gap. If
  no `With` block is in scope, the reference is malformed VBA and
  should be surfaced to the human.
- **Verbatim suggested-next-steps:**
  - Confirm the implicit-with member is reachable from a containing
    With block; if not, rewrite to name the receiver explicitly.

### `member-access-on-unresolved-target`

- **Construct:** a `receiver.member` expression whose `receiver`
  itself failed to resolve. The construct cascades: the member
  access cannot be bound until the target is.
- **Why the producer could not finish:** the resolver emits one
  diagnostic per unresolved binding, so a chain like
  `unknownThing.Foo.Bar` produces one `not-in-scope` (or similar)
  for `unknownThing` plus one `member-access-on-unresolved-target`
  per dependent member access.
- **Downstream-AI interpretation:** noise — usually a consequence
  of an upstream unresolved entry. The migration agent should
  resolve the underlying target first; the dependent
  `member-access-on-unresolved-target` entry typically disappears
  once the upstream target is fixed. Only investigate
  member-access-on-unresolved-target entries on their own when no
  matching upstream entry exists in the same item batch (which
  would indicate a producer wiring gap).
- **Verbatim suggested-next-steps:**
  - Resolve the underlying target reference first; the member
    access cannot be bound until then.

---

## Source: `SqlFormReference`

SqlFormReference entries come from the SQL form-reference resolver
(`AccessHarvester.Sql.Parser.FormReferences.UnresolvedFormReference`).
They surface when a Jet-SQL query (or query-shaped property-sheet
value, once the live aggregator is wired) contains a
`[Forms]![frmFoo]!ctlBar` / `[Reports]![rptFoo]!ctlBar` chain that
the resolver cannot fully bind against the harvested metadata
snapshot.

### `bare-collection-reference`

- **Construct:** a `[Forms]` or `[Reports]` reference with no
  navigation target (no `!FormName` segment).
- **Why the producer could not finish:** the host collection itself
  is not a navigation target — the consumer needs a specific form or
  report to reach a usable binding. The resolver emits the bare
  reference rather than guessing at intent.
- **Downstream-AI interpretation:** almost always a partial edit or a
  built dynamically by string concatenation upstream. The migration
  agent should trace the call site for evidence of dynamic
  composition; if the construct is genuinely terminal (no concatenation
  upstream), surface to the human as a broken expression.
- **Verbatim suggested-next-steps:**
  - Replace the bare collection reference with a specific
    form/report navigation target.

### `form-not-in-metadata`

- **Construct:** a `[Forms]![<name>]!...` reference whose first
  segment did not match any form in the harvested metadata snapshot.
- **Why the producer could not finish:** the harvested metadata is
  the closed registry of known forms; a name not in it cannot be
  resolved against the snapshot. The form may be (a) loaded
  dynamically at runtime from an external source, (b) typo'd in the
  source code, or (c) genuinely missing from the metadata snapshot
  due to a harvest-time skip.
- **Downstream-AI interpretation:** check the migration project's
  manual form registry for a dynamic-form entry under the same name
  before treating as a typo. The most common real case is (a) — a
  form loaded by `OpenForm` from a runtime-supplied name; in that
  case the migration agent should register the form's expected
  schema explicitly in the target platform's component registry.
- **Verbatim suggested-next-steps:**
  - Confirm the form was harvested into the metadata snapshot; if
    it loads dynamically at runtime, register it manually.

### `report-not-in-metadata`

- **Construct:** a `[Reports]![<name>]!...` reference whose first
  segment did not match any report in the harvested metadata. Mirror
  of `form-not-in-metadata`.
- **Why the producer could not finish:** same constraint, reports
  side.
- **Downstream-AI interpretation:** apply the same logic as
  `form-not-in-metadata`. Reports are more often runtime-composed
  than forms (a generic report module that takes a record-source at
  open time), so the dynamic-form interpretation is more frequent
  here.
- **Verbatim suggested-next-steps:**
  - Confirm the report was harvested into the metadata snapshot;
    if it loads dynamically at runtime, register it manually.

### `control-not-on-form`

- **Construct:** a `[Forms]![frmFoo]![ctlBar]` reference where the
  form `frmFoo` resolved cleanly but the control `ctlBar` is not
  present on the resolved form.
- **Why the producer could not finish:** the control may be (a)
  created dynamically at runtime (e.g., by a wizard-style form-load
  handler), (b) renamed since the harvested snapshot, or (c)
  genuinely missing from the form (a real bug in the original
  source).
- **Downstream-AI interpretation:** the migration agent should
  check the form's `Form_Load` / `Form_Open` handler for dynamic
  control creation before treating as a missing-control bug. If the
  handler creates controls dynamically, the migration agent should
  surface the dynamic-control declaration explicitly in the target
  platform's component tree.
- **Verbatim suggested-next-steps:**
  - Confirm the control name; if it is created at runtime, surface
    the synthetic-control declaration explicitly.

### `control-not-on-report`

- **Construct:** a `[Reports]![rptFoo]![ctlBar]` reference where the
  report `rptFoo` resolved but the control `ctlBar` is not present
  on it.
- **Why the producer could not finish:** same constraint, reports
  side.
- **Downstream-AI interpretation:** apply the same logic as
  `control-not-on-form`. Reports more often expose dynamically-
  added controls via subreports than forms do, so check the
  subreport bindings before surfacing as a real bug.
- **Verbatim suggested-next-steps:**
  - Confirm the control name; if it is created at runtime, surface
    the synthetic-control declaration explicitly.

### `host-collection-not-registered`

- **Construct:** a `[Forms]!...` or `[Reports]!...` reference
  resolved against the null Access object-model registry
  (`NullAccessObjectModelRegistry.Instance`).
- **Why the producer could not finish:** the caller opted out of
  form-reference resolution by passing the null registry. This is
  the documented way to disable resolution rather than a missing
  metadata case; the entry surfaces so the consumer sees that
  resolution was bypassed by design.
- **Downstream-AI interpretation:** treat as a "resolution was
  intentionally disabled" signal, not a true unresolved reference.
  The migration agent should not surface to the human as a gap;
  instead it should re-run the harvester with a registered
  Access-object-model registry to get a real resolution result. If
  the migration agent cannot re-run, surface to the human as a
  setup issue rather than a code issue.
- **Verbatim suggested-next-steps:**
  - Re-run with a non-null `IAccessObjectModelRegistry` so the
    Forms/Reports collections are registered.

### Dynamic-prefix family: `member-not-found-at-segment-<N>`

**This is not a stable lookup-backed reason entry. It is a templated
family — the `<N>` suffix is filled in at emission time by
`UnresolvedReason.MemberNotFoundAtSegment(int)`, and the aggregator's
closed `SuggestedNextStepsLookup` does not have a key for it.**
Consumers match on the `member-not-found-at-segment-` prefix and
the family falls through to the aggregator's
`GenericSuggestedNextSteps` list. It is documented here because the
prefix is the documented signal for a real construct shape, even
though the listed steps below are the *generic fallback*, not a
verbatim lookup value.

- **Construct (templated):** a multi-segment form/report property
  chain (`[Forms]![frmFoo].Form.UnknownProperty.OtherStep`) that
  broke at a segment deeper than the form/control level. The `<N>`
  suffix on the emitted reason is the zero-based segment index at
  which the chain failed; consumers parse it back out of the reason
  string.
- **Why the producer could not finish:** the registry knows the
  form/control vocabulary but does not enumerate the full property
  surface of every control type. A chain that reaches into a
  property the registry does not model returns the segment-specific
  reason so consumers know exactly where the chain broke.
- **Downstream-AI interpretation:** the migration agent should
  match on the `member-not-found-at-segment-` prefix rather than the
  full reason string. Treat as a property-surface gap: the chain is
  well-formed up to segment `<N>`, but the property at `<N>` is
  either misspelled, refers to a runtime-only property (e.g., a
  third-party control's extension), or refers to a registry coverage
  gap. The `<N>` index pinpoints which segment the agent needs to
  examine.
- **Suggested-next-steps (generic fallback, *not* a verbatim lookup
  entry):** because the reason has no stable key in
  `SuggestedNextStepsLookup`, the aggregator emits
  `GenericSuggestedNextSteps` verbatim:
  - Review the construct at the recorded source location.
  - Decide whether to extend the producer's reason set with a more
    specific code (the aggregator's lookup is missing this reason).
  The generic-text emission is itself the in-output signal that the
  producer reason is dynamic / unwired in the closed lookup. A
  consumer that wants source-specific guidance for this family
  should rely on the construct interpretation above rather than the
  generic hint list.

---

## Source: `DynamicSqlSite`

DynamicSqlSite entries come from the dynamic-SQL analyzer
(`AccessHarvester.Sql.Parser.DynamicSql`). They surface when a SQL
execution site (`DoCmd.RunSQL`, `OpenRecordset`, `Execute`) whose
SQL argument is composed dynamically from local variables and string
concatenation could not be flattened to a usable analytic template.

### `combinatorial-blowup`

- **Construct:** a dynamic-SQL execution site whose cross-product of
  branch alternatives at composition time exceeded
  `DynamicSqlAnalyzerOptions.MaxFullEnumeration`. The analyzer still
  emits a bounded representative sample (capped at
  `DynamicSqlAnalyzerOptions.MaxRepresentativeSamples`) so a consumer
  has *something* to look at, but the full enumeration is suppressed.
- **Why the producer could not finish:** the construct is analyzable
  in principle but exceeds the configured cap; raising the cap would
  let the analyzer continue but at exponential cost.
- **Downstream-AI interpretation:** read the
  `AdditionalContext["templateCount"]` and `["fullCardinality"]`
  slots to see how far the analyzer got and how much further it
  would need to go. The migration agent should treat the
  representative templates as the analytic baseline and surface to
  the human if a complete enumeration matters — typically it does
  not, because the representative sample covers the common shapes.
  If complete enumeration is needed (e.g., for a security review of
  injection paths), the agent should recommend rewriting the
  builder rather than raising the cap; the cap is calibrated to a
  defensible upper bound for the harvester's intended uses.
- **Verbatim suggested-next-steps:**
  - Inspect the representative templates the analyzer emitted and
    decide whether to raise the enumeration cap or rewrite the SQL
    builder to flatten the branch set.

### `unbound-variable`

- **Construct:** a dynamic-SQL execution site whose SQL argument was
  a reference to a string-typed local variable, and the analyzer
  never observed the variable being assigned along the path that
  reaches the execution site.
- **Why the producer could not finish:** the analyzer's dataflow
  pass walks assignments in source order; an execution site
  reached on a path the analyzer never observed an assignment for
  is "unbound" with respect to the analysis. This is distinct from
  `unknown-expression` because the unbound case carries dataflow
  semantics — there *should* be an assignment somewhere, but the
  analyzer did not see it.
- **Downstream-AI interpretation:** suspect dead code or an
  initialization-order bug in the original VBA. The migration
  agent should walk the procedure for assignments to the named
  variable; if none exist, the call is unreachable in any normal
  flow and the agent should surface to the human as probably-dead.
  If an assignment exists but on a path the analyzer missed
  (conditional branch, error handler, etc.), surface as an
  analyzer coverage gap.
- **Verbatim suggested-next-steps:**
  - Trace the SQL variable's assignment path; the analyzer never
    observed it being set before the execution site.

### `unknown-expression`

- **Construct:** a dynamic-SQL execution site whose SQL argument
  expression did not flatten to any literal content — every part of
  the chain was an unknown hole (a function call the analyzer does
  not peer inside, a property access on a non-tracked receiver,
  etc.). The site is preserved for the manual-review queue but no
  template is synthesized.
- **Why the producer could not finish:** the analyzer recognises a
  narrow set of SQL-builder primitives — string concatenation via
  `&` / `+`, string literals, and variable references whose
  assignment the analyzer observed on the path to the execution
  site. Function calls (including builtins like `Replace`, `IIf`,
  `Format`, `String$`), arithmetic, and `New` expressions are
  treated as unknown holes per the analyzer's own deferred-gap
  comment (`DynamicSqlAnalyzer.cs`: "*peering inside builtins like
  `Replace()` to extract their form-control argument*" is a
  documented future-work item).
- **Downstream-AI interpretation:** inspect the call site's source
  to see what builder the construct uses; if it is a builtin the
  analyzer ought to peer into (e.g., `Replace`, `IIf`, `Format`,
  `String$`, user-defined helpers used widely in the codebase),
  surface as an analyzer-extension request. If it is one-off code,
  surface to the human for a manual SQL extraction.
- **Verbatim suggested-next-steps:**
  - Inspect the SQL argument's surface text and decide whether to
    extend the analyzer with a builtin recogniser (e.g., Replace,
    IIf) or to rewrite the call site.

---

## Source: `NavigationTarget`

NavigationTarget entries come from the navigation-graph builder
(`AccessHarvester.Pipeline.Enrichment.NavigationGraphBuilder`). They
surface when a `DoCmd` navigation call (`OpenForm`, `OpenReport`,
`OpenQuery`, `Close`, etc.) could not be reduced to a literal target
name.

### `non-literal-target`

- **Construct:** a `DoCmd.OpenForm` (or sibling) dispatch whose
  target argument was a non-literal expression — a variable, a
  function call, a member access, etc.
- **Why the producer could not finish:** the navigation graph
  tracks edges between named forms/reports; a non-literal target
  cannot become an edge because the builder does not know which
  named node to point at. The construct is preserved with the
  surrounding expression text so a consumer can recover the
  literal at runtime if it has additional context.
- **Downstream-AI interpretation:** apply the same logic as
  SqlFormReference's `form-not-in-metadata` — the call most likely
  resolves to a dynamic form name supplied at runtime. The
  migration agent should walk the call site for evidence of the
  argument's likely values (a `Select Case` selector on a control
  value, a parameter passed in by the caller, etc.). If the agent
  can enumerate the likely targets, it should rewrite the dispatch
  with explicit branches; if not, surface to the human.
- **Verbatim suggested-next-steps:**
  - Trace the target expression to its source; if it resolves at
    runtime, replace with a literal name or register the dynamic
    target explicitly.

### `missing-target`

- **Construct:** a `DoCmd.OpenForm` (or sibling) dispatch with no
  positional arguments — the construct compiles but raises at
  runtime because the target is required.
- **Why the producer could not finish:** the builder cannot
  produce an edge with no target node; the dispatch is malformed
  from the navigation graph's perspective.
- **Downstream-AI interpretation:** almost always a partial edit
  in the original source — surface to the human as a real bug
  rather than a migration ambiguity. The fix is typically
  obvious from the surrounding context (a deleted line, an
  incomplete refactor).
- **Verbatim suggested-next-steps:**
  - Inspect the dispatch site — `DoCmd` calls without a target
    argument almost always indicate a partial edit and should be
    repaired before harvest.

---

## Source: `FunctionClassification`

FunctionClassification entries surface from per-procedure
classifier records (`AccessHarvester.Pipeline.Enrichment
.FunctionClassification.UnresolvedReferences`). They use the same
reason codes as `Resolver` entries — the underlying constraint is
the same — but the producer is the classifier's per-procedure walk,
so the entry rides alongside the procedure-level classification
record rather than appearing in the standalone resolver-diagnostics
list.

A migration agent that processes the per-procedure record set will
typically see these alongside the procedure's
`FunctionClassification.Classification` and
`.Confidence` slots; the unresolved-reference fact attaches
provenance for any name that the classifier could not bind during
its walk. Two views of the same construct — one
`(Source=Resolver, Reason=R)` entry and one
`(Source=FunctionClassification, Reason=R)` entry — can co-occur in
the same `UnresolvedItems[]` array when the resolver and the
classifier both observed the binding failure. The aggregator does
not deduplicate the two views because the per-procedure attachment
is the affordance the classifier's consumers rely on; deduplication
is a consumer-side decision (typically by
`(ModuleName, ProcedureName, Line, Column, Reason)`).

The five reason codes are reproduced below for per-pair coverage.
Each entry pins the verbatim suggested-next-steps from
`SuggestedNextStepsLookup` and points back to the `Resolver` section
for the full construct discussion — interpretation does not change
based on the producer.

### `not-in-scope`

- **Construct:** a bare identifier reference, surfaced from the
  classifier's per-procedure walk rather than the project-wide
  resolver-diagnostics pass. Per-pair affordance: the migration
  agent can read it alongside the procedure's classification record
  rather than cross-referencing the standalone diagnostics list.
- **Cross-reference for construct details, why-the-producer-could-not-finish,
  and downstream-AI interpretation:** see Resolver → `not-in-scope`
  above.
- **Verbatim suggested-next-steps:** identical to Resolver →
  `not-in-scope`.
  - Confirm whether the identifier is declared in another module's
    Public scope.
  - If the identifier is intentional dead code, mark it as such.

### `me-in-standard-module`

- **Construct:** a `Me.<member>` reference inside a standard module,
  observed during the classifier's per-procedure walk.
- **Cross-reference:** see Resolver → `me-in-standard-module`.
- **Verbatim suggested-next-steps:** identical to Resolver →
  `me-in-standard-module`.
  - Move the `Me` reference into a class / form / report module, or
    rewrite to name the target explicitly.

### `default-member-not-yet-resolved`

- **Construct:** a member-access expression awaiting a later
  default-member or type-inference pass, observed during the
  classifier's per-procedure walk.
- **Cross-reference:** see Resolver →
  `default-member-not-yet-resolved`.
- **Verbatim suggested-next-steps:** identical to Resolver →
  `default-member-not-yet-resolved`.
  - Re-run after the default-member resolution pass has executed;
    if still unresolved, supply a type annotation for the receiver.

### `with-block-flattening-deferred`

- **Construct:** an implicit-with member reference outside any
  visible `With` block at the point the classifier examined it.
- **Cross-reference:** see Resolver →
  `with-block-flattening-deferred`.
- **Verbatim suggested-next-steps:** identical to Resolver →
  `with-block-flattening-deferred`.
  - Confirm the implicit-with member is reachable from a containing
    With block; if not, rewrite to name the receiver explicitly.

### `member-access-on-unresolved-target`

- **Construct:** a `receiver.member` expression whose receiver
  itself failed to resolve, surfaced from the classifier's
  per-procedure walk.
- **Cross-reference:** see Resolver →
  `member-access-on-unresolved-target`.
- **Verbatim suggested-next-steps:** identical to Resolver →
  `member-access-on-unresolved-target`.
  - Resolve the underlying target reference first; the member
    access cannot be bound until then.

---

## Cross-cutting consumer guidance

A few patterns apply across all sources and reasons:

- **Reason codes are stable strings; future revisions append rather
  than rename.** A migration agent may match on `Reason` exactly,
  including the `member-not-found-at-segment-<N>` family by prefix.
  New producer reasons that appear in `UnresolvedItems[]` without a
  matching entry in `SuggestedNextStepsLookup` fall through to the
  generic hint list verbatim; the generic hint list is the in-output
  signal that the wiring needs an addition. When this narrative
  needs to grow, the trigger is a new reason code reaching the
  `SuggestedNextStepsLookup`, not a new occurrence in a specific
  fixture.
- **`UnresolvedItem.SuggestedNextSteps` is mechanical.** Two entries
  with the same reason carry the same hint list verbatim; this is by
  design so consumers can render the hint without sanitising. The
  *interpretation* in this document is what the consumer adds on top
  of the mechanical hint — the hint says "what to do next," the
  interpretation says "how to read it in context."
- **`Location` and `SurroundingText` are the primary affordances.**
  Before acting on a recommendation, the consumer should re-read the
  source at the recorded location. The narrative above describes the
  typical case for each reason; the actual fix depends on
  surrounding code.
- **Deferred-by-design entries are dropped before the queue.**
  Info-severity resolver diagnostics never reach the manual-review
  queue (the aggregator's `FromResolverDiagnostics` projector skips
  them). A consumer that sees fewer entries than the underlying
  producer emitted is not encountering a bug — the aggregator is
  filtering deferred cases by contract.

---

## Stability commitment

The stable lookup-backed reasons documented above are the closed
set in `UnresolvedItemAggregator.SuggestedNextStepsLookup` as of
2026-05-17. The one documented dynamic-prefix exception
(`member-not-found-at-segment-<N>`) is emitted by
`UnresolvedReason.MemberNotFoundAtSegment(int)` and is not a
lookup key; it falls through to `GenericSuggestedNextSteps` by
design. New producer reasons that join the lookup must be added at
the same time they are added upstream (this is enforced by code
review, not by a build-time gate). When a new stable reason lands,
this document gets a new entry in the matching `Source` section,
with the same four-piece structure used above. When a new
dynamic-prefix family appears, it gets a clearly-flagged
"dynamic-prefix family" subsection like the one already used for
`member-not-found-at-segment-<N>`. The reason set is intentionally
narrow — over the prior session sets that built the producers,
each `Source` has converged on a small handful of codes — and
should stay narrow as the system evolves.

```

---

## Required response format

After your detailed prose analysis, output a JSON block in this exact format:

```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }
  ]
}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
