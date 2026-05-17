You are the cross-provider session-verifier for Session 2 of the session set
`harvester-finish-canonical-parse-success-report` in the dabbler-access-harvester
repo. Your job is to review the Session 2 deliverables against the spec's
Session 2 plan and surface any Critical / Major / Minor issues the orchestrator
should fix before commit.

**Your response MUST begin with a detailed prose analysis covering each
section below BEFORE the JSON block at the end.** Do not skip the prose
analysis — the JSON block alone is not sufficient.

Analyse the following concerns in order:

1. **Spec adherence.** Does each Session 2 step listed in the spec correspond
   to a concrete deliverable in the in-scope files? (Reconfirm diff gate;
   per-reason narrative; sentinel diff-message test; build+test.) Any
   missing step?
2. **Narrative correctness.** Does `unresolved-reason-narrative.md` cover
   every reason code emitted by `UnresolvedItemAggregator.SuggestedNextStepsLookup`?
   Are the verbatim suggested-next-steps lists accurate copies of the
   aggregator's lookup values? Is the construct-description per reason
   consistent with the producer's contract?
3. **Sentinel test integrity.** Does the sentinel test (`ParseSuccessReportDiffTests`)
   actually exercise the production diff-message builder? The Session 2 plan
   requires it to test the message a human would see at gate-fail time — not
   a parallel reimplementation. Is the helper refactor (`ParseSuccessReportDiff`)
   byte-identical to the inline message Session 1 shipped?
4. **Diff-gate continuity.** Are there any behaviours of the live gate
   (`ParseSuccessReportTests`) that the refactor changed? The Session 2 plan
   explicitly inherits the diff contract unchanged.
5. **Determinism / convention compliance.** Reason codes must be stable
   strings, the narrative must not invent new reasons, and any code change
   must hold the lean-by-default principle.

---

## Spec Session 2 plan (excerpt from `spec.md`)

```
### Session 2 of 2: Per-Reason Narrative + Sentinel Test

**Steps:**

1. Confirm the Session 1 diff-gate is still green against a fresh
   regeneration (the diff contract was already live from Session 1).
2. Author `unresolved-reason-narrative.md` (sibling doc) — one
   paragraph per `(Source, Reason)` pair that appears in either
   fixture's report, describing the construct and the recommended
   downstream-AI interpretation. Use the
   `UnresolvedItemAggregator.SuggestedNextStepsLookup` entries as
   ground truth for the recommended next steps. (Session 1's reports
   ship `unresolvedItems.status = "not-measured"`; the narrative
   covers the reason codes the aggregator currently emits, so it is
   reusable when the live wiring lands in the next set.)
3. Add a sentinel test that intentionally mutates one count and
   asserts the diff message is human-readable (confirms the failure
   mode the gate ships with).
4. Run `dotnet build && dotnet test` sequentially.
5. End-of-session cross-provider verification.
6. Closeout: `disposition.json` (per Convention), `change-log.md`
   via `task_type="summarization"` (or in-conversation if
   orchestrator is Opus/Sonnet),
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
7. Step 9 reorganization review of `project-guidance.md` and
   `lessons-learned.md` AFTER notify.

**Creates:**

- `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiffTests.cs`
  (the sentinel test).
- `docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/change-log.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/disposition.json`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/session-reviews/session-002.md`

**Touches:**

- The committed per-fixture golden reports (final pin if any
  Session 2 work shifts counts; otherwise unchanged from Session 1).

**Ends with:** Phase 4 Criterion #1 has a CI gate that fails on
parse-coverage regression; the manual-review queue has a per-reason
narrative the next downstream AI consumer can read.

**Progress keys:** `session-2/diff-gate-reconfirmed`,
`session-2/per-reason-narrative`, `session-2/sentinel-test`,
`session-2/build-test`, `session-2/verify`,
`session-2/closeout-artifacts`, `session-2/commit-push-notify`,
`session-2/step9-reorg`.

---

```

---

## In-scope files

### `docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`

```
# Unresolved-Reason Narrative

> **Status:** Session 2 deliverable for
> `harvester-finish-canonical-parse-success-report`.
> **Authority for the reason set:** the closed
> `UnresolvedItemAggregator.SuggestedNextStepsLookup` in
> [`AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs`](../../../AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs).
> The aggregator's lookup is the source of truth for both the
> reason codes and the recommended next steps; this document explains
> the *construct* each reason flags and the *downstream-AI
> interpretation* a Blazor-migration agent should apply when it
> encounters one in a `UnresolvedItems[]` entry.
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

The `UnresolvedItem.Location` and `SurroundingText` slots point at the
construct's source; the consumer should re-read those before acting
on the recommendation here. Reason codes are stable strings — future
producer revisions append to the set rather than rename existing
codes — so a migration agent may match on `Reason` exactly.

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

### `member-not-found-at-segment-<N>` (dynamic family)

- **Construct:** a multi-segment form/report property chain
  (`[Forms]![frmFoo].Form.UnknownProperty.OtherStep`) that broke at a
  segment deeper than the form/control level. The `<N>` suffix is
  the zero-based segment index at which the chain failed.
- **Why the producer could not finish:** the registry knows the
  form/control vocabulary but does not enumerate the full property
  surface of every control type. A chain that reaches into a
  property the registry does not model returns the segment-specific
  reason so consumers know exactly where the chain broke.
- **Downstream-AI interpretation:** dynamic reason — the migration
  agent should match on the `member-not-found-at-segment-` prefix
  rather than the full reason string. Treat as a property-surface
  gap: the chain is well-formed up to segment `<N>`, but the
  property at `<N>` is either misspelled, refers to a runtime-only
  property (e.g., a third-party control's extension), or refers to
  a registry coverage gap. The `<N>` index pinpoints which segment
  the agent needs to examine.
- **Verbatim suggested-next-steps:** falls through to the generic
  hint list when the exact dynamic reason is not in the closed
  lookup. The aggregator's `DescribeFormReferenceReason` switch
  recognises the prefix and produces a description, but the
  `SuggestedNextStepsLookup` covers only stable reason codes — so
  this family is the documented place to fall back on the generic
  text:
  - Review the construct at the recorded source location.
  - Decide whether to extend the producer's reason set with a more
    specific code (the aggregator's lookup is missing this reason).

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
  not recognise, a property access on a non-tracked receiver,
  etc.). The site is preserved for the manual-review queue but no
  template is synthesized.
- **Why the producer could not finish:** the analyzer recognises a
  closed set of dynamic-SQL builders (string concatenation, `&` /
  `+`, `Replace`, `IIf`); expressions outside that set are
  preserved as unanalyzable rather than synthesized blindly.
- **Downstream-AI interpretation:** inspect the call site's source
  to see what builder the construct uses; if it is a missing
  builder the analyzer should support (e.g., `Format`, `String$`,
  user-defined helpers used widely in the codebase), surface as an
  analyzer-extension request. If it is one-off code, surface to
  the human for a manual SQL extraction.
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
its walk.

**Reason codes:** identical to `Resolver` — `not-in-scope`,
`me-in-standard-module`, `default-member-not-yet-resolved`,
`with-block-flattening-deferred`, `member-access-on-unresolved-target`.
The construct definitions, the constraint explanations, and the
suggested-next-steps lists for each reason are identical to the
`Resolver` section above. The only difference is the producer
(per-procedure classifier vs. project-wide resolver diagnostics
pass) — which influences only where the consumer finds the entry,
not how to interpret it.

A migration agent that has already consumed a `Resolver` entry for
the same `(ModuleName, ProcedureName, Line, Column)` location may
treat the matching `FunctionClassification` entry as redundant
provenance and skip it. The aggregator does not deduplicate the
two views because the per-procedure attachment is the affordance
the classifier's consumers rely on; deduplication is a
consumer-side decision.

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

The reason set above is the closed set as of 2026-05-17 in the
`UnresolvedItemAggregator.SuggestedNextStepsLookup` map. New
producer reasons must be added to the lookup at the same time they
are added upstream (this is enforced by code review, not by a
build-time gate). When a new reason lands, this document gets a
new entry in the matching `Source` section, with the same four-
piece structure used above. The reason set is intentionally
narrow — over the prior session sets that built the producers,
each `Source` has converged on a small handful of codes — and
should stay narrow as the system evolves.

```

### `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiff.cs`

```
using System;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Diff-message builder shared by ParseSuccessReportTests (the live
// gate) and ParseSuccessReportDiffTests (Session 2's sentinel that
// verifies the failure message stays human-readable). Keeping the
// builder in one place means the sentinel is exercising the exact
// message the human would see at gate-fail time rather than a
// parallel implementation drifting from it.
internal static class ParseSuccessReportDiff
{
    internal const string UpdateEnvVar = "UPDATE_PARSE_SUCCESS_REPORTS";

    // Returns null when the two strings are byte-equal. Returns the
    // assembled diff-failure message otherwise.
    internal static string? Build(
        string committed,
        string generated,
        string fixtureName,
        string outPath)
    {
        if (string.Equals(committed, generated, StringComparison.Ordinal))
            return null;

        var divergence = FindFirstDivergence(committed, generated);
        return
            $"{fixtureName}: generated parse-success report does not match committed golden at {outPath}.\n"
            + $"First divergence at offset {divergence.Offset} (line {divergence.Line}): \n"
            + $"  committed: <<{Snippet(committed, divergence.Offset)}>>\n"
            + $"  generated: <<{Snippet(generated, divergence.Offset)}>>\n"
            + $"Re-run with `{UpdateEnvVar}=1 dotnet test --filter ParseSuccessReportTests` to regenerate, then commit the diff.";
    }

    internal readonly record struct Divergence(int Offset, int Line);

    internal static Divergence FindFirstDivergence(string a, string b)
    {
        int min = Math.Min(a.Length, b.Length);
        int line = 1;
        for (int i = 0; i < min; i++)
        {
            if (a[i] != b[i]) return new Divergence(i, line);
            if (a[i] == '\n') line++;
        }
        return new Divergence(min, line);
    }

    internal static string Snippet(string s, int offset)
    {
        int start = Math.Max(0, offset - 20);
        int end = Math.Min(s.Length, offset + 40);
        return s.Substring(start, end - start).Replace("\n", "\\n");
    }
}

```

### `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiffTests.cs`

```
using Xunit;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Session 2 of `harvester-finish-canonical-parse-success-report`:
// sentinel test for the diff-failure message. The live gate
// (ParseSuccessReportTests) only emits the failure message when the
// committed golden disagrees with a fresh generation — which means a
// maintainer encounters it once, under stress, while debugging an
// unexpected count shift. The message has to stay human-readable
// under that load. These tests construct a hand-built golden/generated
// pair with a known, single-character divergence and assert that the
// message ParseSuccessReportDiff.Build emits is the one the human
// would actually see: it names the fixture, points at the offset/line
// of the divergence, shows both surrounding snippets, and tells the
// maintainer how to regenerate.
//
// The diff helper is exercised here so it is not the parallel logic
// from a second copy — both this test and the live gate consume the
// same builder.
public sealed class ParseSuccessReportDiffTests
{
    [Fact]
    public void Build_returns_null_when_strings_are_byte_equal()
    {
        var same = "{\n  \"fixture\": \"HomeHealthCare.accdb\"\n}\n";
        var result = ParseSuccessReportDiff.Build(
            committed: same,
            generated: same,
            fixtureName: "HomeHealthCare.accdb",
            outPath: "samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json");

        Assert.Null(result);
    }

    [Fact]
    public void Build_emits_actionable_message_when_one_count_changes()
    {
        // Hand-built minimal report pair that diverges only at the
        // VBA totalModules count (32 vs. 31). The mutation is in
        // exactly one place so the divergence-offset / line / snippet
        // story is unambiguous.
        const string committed =
            "{\n"
            + "  \"fixture\": \"HomeHealthCare.accdb\",\n"
            + "  \"schemaVersion\": 1,\n"
            + "  \"summary\": {\n"
            + "    \"vbaModules\": {\n"
            + "      \"totalModules\": 32,\n"
            + "      \"parseOk\": 32\n"
            + "    }\n"
            + "  }\n"
            + "}\n";

        const string generated =
            "{\n"
            + "  \"fixture\": \"HomeHealthCare.accdb\",\n"
            + "  \"schemaVersion\": 1,\n"
            + "  \"summary\": {\n"
            + "    \"vbaModules\": {\n"
            + "      \"totalModules\": 31,\n"
            + "      \"parseOk\": 32\n"
            + "    }\n"
            + "  }\n"
            + "}\n";

        const string outPath =
            "samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json";

        var msg = ParseSuccessReportDiff.Build(
            committed: committed,
            generated: generated,
            fixtureName: "HomeHealthCare.accdb",
            outPath: outPath);

        Assert.NotNull(msg);

        // The message has to identify the fixture and the output path
        // — without them, a maintainer dropped into a CI log cannot
        // tell which golden is wrong or where it lives.
        Assert.Contains("HomeHealthCare.accdb", msg);
        Assert.Contains(outPath, msg);

        // The divergence sits on the totalModules line. With six lines
        // of preceding content, the first mismatch is on line 6 of
        // both strings (1-based) — that is what the human sees in the
        // "(line N)" portion of the message.
        Assert.Contains("(line 6)", msg);

        // Both the committed and generated values must be quoted, so
        // the maintainer can tell which side moved. The snippet window
        // around the offset captures the "totalModules" key and the
        // disagreeing digit.
        Assert.Contains("committed: <<", msg);
        Assert.Contains("generated: <<", msg);
        Assert.Contains("32", msg);
        Assert.Contains("31", msg);

        // Regeneration affordance — the message has to tell the
        // maintainer how to update the golden when the count shift is
        // intentional.
        Assert.Contains(ParseSuccessReportDiff.UpdateEnvVar, msg);
        Assert.Contains("dotnet test --filter ParseSuccessReportTests", msg);
    }

    [Fact]
    public void Snippet_collapses_embedded_newlines_for_single_line_display()
    {
        // The Snippet helper renders LF as the literal two-character
        // sequence "\n" so the diff message stays single-line per
        // committed/generated side. Without that, a divergence near a
        // line boundary would wrap mid-message and obscure the
        // surrounding context.
        var divergent = "abc\ndef\nghi";
        var snippet = ParseSuccessReportDiff.Snippet(divergent, offset: 4);

        Assert.DoesNotContain("\n", snippet);
        Assert.Contains("\\n", snippet);
    }
}

```

### `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportTests.cs`

```
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Session 1 of `harvester-finish-canonical-parse-success-report`:
// the per-fixture parse-success report is committed as the interim
// golden. Default test mode diffs against the committed file;
// `UPDATE_PARSE_SUCCESS_REPORTS=1` overwrites it. Session 2 carries
// forward the same gate without changing the test contract.
public sealed class ParseSuccessReportTests
{
    // The diff-message text is built by ParseSuccessReportDiff so the
    // Session 2 sentinel test can exercise the same builder.
    private const string UpdateEnvVar = ParseSuccessReportDiff.UpdateEnvVar;

    [Fact]
    public void HomeHealthCare_parse_success_report_matches_committed_golden()
    {
        AssertOrUpdate("HomeHealthCare.accdb");
    }

    [SkippableFact]
    public void NorthwindDevEdition_parse_success_report_matches_committed_golden()
    {
        // Northwind YAML mirror is gitignored — present on the
        // maintainer's Windows machine after a local COM-extract pass,
        // absent on CI / fresh clones. Skip cleanly when missing so
        // the suite stays green elsewhere.
        var repoRoot = RepoRoot();
        var mirror = Path.Combine(repoRoot, "samples", "DatabaseYaml", "NorthwindDevEdition.accdb");
        Skip.IfNot(
            Directory.Exists(mirror),
            $"NorthwindDevEdition YAML mirror not present at {mirror} — " +
            "regenerate via the local COM extractor or skip.");

        AssertOrUpdate("NorthwindDevEdition.accdb");
    }

    private static void AssertOrUpdate(string fixtureName)
    {
        var repoRoot = RepoRoot();
        var mirror = Path.Combine(repoRoot, "samples", "DatabaseYaml", fixtureName);
        var report = ParseSuccessReportGenerator.Generate(fixtureName, mirror);

        // Sanity invariants — a zero module/query count means the YAML
        // mirror is empty or the loader is broken. Fail loudly either
        // way before the diff path.
        Assert.True(report.Summary.VbaModules.TotalModules > 0,
            $"{fixtureName}: VBA module count is zero — empty mirror or loader regression.");
        Assert.True(report.Summary.JetSqlQueries.TotalQueries > 0,
            $"{fixtureName}: query count is zero — empty mirror or loader regression.");

        var generatedJson = JsonSerializer.Serialize(report, SerializerOptions) + "\n";

        var outDir = Path.Combine(repoRoot, "samples", "parse-success-reports");
        var outPath = Path.Combine(outDir, $"{fixtureName}.parse-success-report.json");

        bool update = string.Equals(
            Environment.GetEnvironmentVariable(UpdateEnvVar) ?? "", "1",
            StringComparison.Ordinal);

        if (update || !File.Exists(outPath))
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(outPath, generatedJson, new System.Text.UTF8Encoding(false));
            return;
        }

        var committed = File.ReadAllText(outPath, new System.Text.UTF8Encoding(false));
        var diff = ParseSuccessReportDiff.Build(committed, generatedJson, fixtureName, outPath);
        if (diff is not null)
            throw new Xunit.Sdk.XunitException(diff);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Prevent over-escaping of `<`, `>`, `&` in surrounding-text
        // samples; the report is consumed as a file, not embedded in
        // HTML. The serialiser writes ASCII by default; ensure that
        // text containing non-ASCII (e.g., a stray Caption glyph) is
        // encoded predictably.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Walk up from this test binary to the repo root. The root carries
    // a `.gitignore` or `samples/` directory; we anchor on the latter
    // since the repo's bare-worktree layout means `.gitignore` lives
    // inside the worktree (which may or may not be `main/`).
    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var samples = Path.Combine(here.FullName, "samples");
            var slnCandidate = Path.Combine(here.FullName, "AccessHarvester.sln");
            if (Directory.Exists(samples) && File.Exists(slnCandidate))
                return here.FullName;
            here = here.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate repo root above " + AppContext.BaseDirectory);
    }
}

```


---

### Files present in the repo but NOT touched this session — DO NOT flag as missing

These files were committed in Session 1 and remain unchanged. Their contents
are authoritative for the construct definitions the narrative references and
for the live diff-gate path the sentinel test exercises. If the verifier
needs to confirm anything about them, the relevant excerpts are quoted in
the in-scope content above:

- `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs` — defines
  `UnresolvedItem`, `UnresolvedSource` enum, and `UnresolvedLocation`.
- `AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs` — the
  closed `SuggestedNextStepsLookup` map the narrative pins as authoritative.
- `AccessHarvester.Vba.Resolver/References/ResolutionOutcome.cs` — `UnresolvedReasons`
  static class with the five resolver reason constants the narrative quotes.
- `AccessHarvester.Sql.Parser/FormReferences/UnresolvedReason.cs` — six stable
  SqlFormReference reason constants plus `MemberNotFoundAtSegment(int)` factory.
- `AccessHarvester.Sql.Parser/DynamicSql/UnanalyzableReason.cs` — three
  DynamicSqlSite reason constants.
- `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportModel.cs`
  + `ParseSuccessReportGenerator.cs` — Session 1 deliverables; the sentinel
  test does not exercise them.
- `samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json`
  + `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`
  — committed interim goldens; the live diff-gate (ParseSuccessReportTests)
  asserts byte-equality against these and was confirmed green in this
  session before the new files were added.

### What the verifier may flag normally

- The narrative's claim that `member-not-found-at-segment-<N>` falls through
  to the GenericSuggestedNextSteps list (the closed lookup keys on stable
  reason codes only; the segment-suffixed code is dynamic — see the
  `DescribeFormReferenceReason` switch in `UnresolvedItemAggregator.cs`
  for the prefix match).
- Anything else relating to the four in-scope files, the spec's Session 2
  plan, or the diff-gate contract.


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
