# Remaining-Work Tree — Phase 4 Mechanical Harvester Finish Line

> **Session set:** `remaining-work-planner` Session 2 of 3
> **Authored:** 2026-05-17 by Claude Opus 4.7 @ effort=high.
> **Amended:** 2026-05-17 post-F1-close — inserted F1.5
> (`vba-attribute-header-strip`) between F1 and F2 after F1's
> reports quantified the VBA `Attribute VB_*` header gap and the
> human picked strip-at-loader rather than extend-the-grammar.
> Cross-engine input (gpt-5-4 + gemini-pro) pinned the spec shape
> at the stub authoring step. F2's prereq list expanded to include
> F1.5 because F1.5 regenerates F1's parse-success-report goldens
> that F2 reconciles against.
> **Authority:**
> [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4
> + the audit at
> [`gap-audit.md`](./gap-audit.md). Session 1's six in-scope gaps
> have been planned as **five session sets**: GAP #5 (UnresolvedItems
> wiring) folds into GAP #3 (canonical golden output) because they
> share the same insertion point in the production pipeline and the
> golden output is the test for whether the wiring is correct. The
> 2026-05-17 amendment added F1.5 as a sixth set — a tactical
> tighten of F1's parse-coverage signal, not a new criterion.
> **Purpose:** the row-per-set index that orders the remaining
> Phase 4 work for Session 3's `ROADMAP.md` and for any future
> orchestrator picking up a Phase 4 set.

This tree is **additive**: it does not replace
[`vba-parser-session-set-generator/session-set-tree.md`](../vba-parser-session-set-generator/session-set-tree.md),
which is the historical authority for the parser-arc rows #1–#17.
Phase 4 closure sits downstream of that tree's #17
(`schema-finalization-and-docs`).

---

## Tree

| # | Slug | Phase 4 criterion | Prereqs | Sessions | Cost band | Status |
|---|------|-------------------|---------|----------|-----------|--------|
| F1 | [`harvester-finish-canonical-parse-success-report`](../harvester-finish-canonical-parse-success-report/spec.md) | #1 Parser coverage finality | None new (uses existing parser + UnresolvedItemAggregator) | 2 | Small | **Complete** (2026-05-17) |
| F1.5 | [`vba-attribute-header-strip`](../vba-attribute-header-strip/spec.md) | #1 Parser coverage finality (tightens F1's signal — strips the canonical `Attribute VB_*` header before parsing, regenerates F1's goldens; HHC parseOk expected 3→~32, Northwind 14→~66) | F1 (regenerates its committed goldens) | 1–2 | Small | Stub |
| F2 | [`harvester-finish-canonical-golden-output`](../harvester-finish-canonical-golden-output/spec.md) | #3 Sample-corpus validation + **#5 Unresolved-record reporting (folded)** | F1 + F1.5 (post-strip parse counts are the gate-relevant ones that must agree with golden manifest counts) | 4 | Medium | Stub |
| F3 | [`harvester-finish-cli-e2e-test-audit`](../harvester-finish-cli-e2e-test-audit/spec.md) | #4 CLI surface | F2 (determinism gate reuses F2's normalization layer) | 2 | Small | Stub |
| F4 | [`harvester-finish-operations-and-interpretation-docs`](../harvester-finish-operations-and-interpretation-docs/spec.md) | #6 Documentation (downstream-AI-consumer-facing portion) | F1 + F1.5 + F2 (docs reference ground-truth output and per-reason narratives) | 3 | Small-to-medium | Stub |
| F5 | [`harvester-finish-cli-packaging`](../harvester-finish-cli-packaging/spec.md) | #7 Packaging | F3 (CLI is feature-stable with black-box coverage before cutting a release) | 2 | Small | Stub |

**Net: 6 session sets, 14–15 sessions total** to close Phase 4.
(Original gap-audit estimate: ~6 sets, 10-14 sessions. The
fold-in of GAP #5 into F2 trimmed one set; the 2026-05-17 F1.5
insertion added one back. F1.5 is a tactical tighten of F1's
signal, not a new criterion.)

**Routed-cost rollup: ~$1.10–$3.60.** Sums the per-stub ranges
declared in each F-set's "Cost Expectations":

| Set | Per-stub routed-cost range |
|---|---|
| F1 | ~$0.10–$0.40 (actual: ~$0.92 across 2 sessions, verification-cost-driven) |
| F1.5 | ~$0.10–$0.40 |
| F2 | ~$0.40–$0.80 |
| F3 | ~$0.10–$0.40 |
| F4 | ~$0.30–$1.20 |
| F5 | ~$0.10–$0.40 |
| **Total** | **~$1.10–$3.60** |

This is higher than the gap-audit's earlier rough estimate of
~$1.00–$1.40 (which assumed a flat ~$0.10/session × 10–14 sessions
under the cross-provider-verification-only model). The
per-stub-derived sum reflects two stub-level decisions Session 2
made that the audit could not have priced:

1. F4's audience-check + downstream-AI probes add 2 extra routed
   calls per probe-bearing session beyond bare verification.
2. F2's architecture call for the manifest aggregator design plus
   any code-review calls on the wiring change push the per-session
   routed budget up modestly.

The per-stub ranges are the authoritative budget; the rollup is
their honest sum. Treat the upper bound as the planning cap.

---

## Per-feature prereq table

Per the `lessons-learned.md` → "Split Session Sets Whose Features
Span Independent Prereq Chains" convention: each feature's prereqs
are listed individually, not as a per-set union. The set-level
prereq column above is the union of every feature's prereqs in
that set; the per-feature view below shows which features actually
depend on which prereqs.

| Set | Feature | Prereqs (per-feature) |
|-----|---------|----------------------|
| F1 | Parse-success report generation | (existing parser + aggregator only) |
| F1 | Property-sheet inventory pass | (existing parser only) |
| F1 | Regression gate | F1's own report-shape decision |
| F1.5 | `VbaModuleBodyLoader` + `ModuleHeaderMetadata` | (existing parser AST only) |
| F1.5 | Production call-site audit + routing | F1.5's loader |
| F1.5 | Regenerated parse-success-report goldens | F1's goldens + F1.5's audit complete |
| F2 | Manifest aggregator design + build | (existing per-object IR files only) |
| F2 | Pipeline wiring + UnresolvedItems emission | F2's manifest aggregator |
| F2 | Golden output generation + commit | F2's wiring landed **+ F1.5's regenerated parse-success counts** (the cross-check at commit-time confirms manifest counts agree with parse counts, and the post-strip counts are the gate-relevant ones) |
| F2 | Regression test | F2's golden committed |
| F3 | Black-box CLI tests for `extract` / `translate` / `run` | (CLI surface only — already feature-stable; **no F2 dependency**) |
| F3 | Determinism gate | **F2's normalization layer** (without it, "run twice and diff" has no normalization conventions to reuse) |
| F4 | Outline + audience-check probe | (existing docs + F1's per-reason narratives) |
| F4 | Body authoring | F2's manifest shape |
| F4 | Pointer page + downstream-AI probe | F4's body |
| F5 | Publish wiring | F3's CLI feature-stable baseline |
| F5 | Packaged-binary smoke tests + install docs | F5's publish |

**Per-feature depth check.** Within each set, the longest-prereq
feature pins the set-level prereq column:

- F1: longest is "Regression gate" → only F1's own work → set
  prereq is "None new." ✓
- F1.5: longest is "Regenerated parse-success-report goldens" →
  F1's goldens + F1.5's audit → set prereq is "F1." ✓
- F2: longest is "Golden output generation + commit" → F2's own
  wiring + F1 + F1.5 → set prereq is "F1 + F1.5." ✓
- F3: longest is "Determinism gate" → F2 → set prereq is "F2." ✓
  Note: F3's black-box CLI tests feature does NOT need F2, but the
  determinism-gate feature does. F3 stays as a single set because
  the per-feature prereq spread is one set (F2 or nothing) — same
  scale, no split warranted.
- F4: longest is "Body authoring" → F2 → set prereq is "F1 + F1.5 + F2." ✓
- F5: longest is "Publish wiring" → F3 → set prereq is "F3." ✓

**No splits recommended.** F3 has the deepest per-feature divergence
(one F2-dep feature, one independent feature) but the two features
share a small, coherent scope (CLI test surface) and the second
feature is a one-session add-on; the cost of splitting (one extra
change-log + verification step per split set per the
lessons-learned guidance) outweighs the parallelism gain. The
fold-in of GAP #5 into F2 was the only structural decision the
audit surfaced.

---

## Recommended execution order

The advisory order surfaced in the gap-audit holds, modulo the
GAP #5 fold-in and the 2026-05-17 F1.5 insertion:

1. **F1** — `harvester-finish-canonical-parse-success-report` —
   establishes the parse-coverage gate that every other gap
   references. **Closed 2026-05-17.**
2. **F1.5** — `vba-attribute-header-strip` — strips the canonical
   `Attribute VB_*` header before parsing and regenerates F1's
   goldens (HHC parseOk 3→~32, Northwind 14→~66). Tactical
   tighten of F1's signal so F2 reconciles against post-strip
   counts.
3. **F2** — `harvester-finish-canonical-golden-output` (with the
   GAP #5 UnresolvedItems wiring folded in) — central deliverable;
   produces the manifest shape every downstream artifact diffs
   against.
4. **F3** — `harvester-finish-cli-e2e-test-audit` — runs after F2
   because the determinism gate reuses F2's normalization layer.
5. **F4** — `harvester-finish-operations-and-interpretation-docs` —
   depends on F1 + F1.5 (per-reason narratives, post-strip metadata)
   and F2 (manifest shape) so the docs reflect ground truth.
6. **F5** — `harvester-finish-cli-packaging` — final gate; depends
   on F3 so the binary distribution ships against the tested CLI
   surface.

F3 and F4 are **eligible to run in parallel** with each other after
F2 closes (no mutual dependency; both depend on F2 — F3 for the
determinism gate's normalization-layer reuse, F4 for the manifest
shape). If parallelism is desirable for window-budget reasons
(per the `project-guidance.md` → Token-Window Budget Overrides and
the parallel-worktree convention), they may be cut as
`session-set/<slug>` branches simultaneously after F2 closes.

**F1.5 and F3's black-box CLI work could start in parallel from
here** (F1.5 only needs F1, which is closed; F3's first feature has
no F-set prereq and the determinism feature can defer until F2
lands). If a window-burning parallel push is desired, F1.5 + F3
(first feature only, deferring the determinism gate to a follow-up
commit inside F3) are the eligible starters; F2 then starts when
F1.5 closes. F4 and F5 strictly wait for their predecessors.

---

## Note on the GAP #5 fold-in

GAP #5 (top-level per-run UnresolvedItems report) was originally
audited as either its own small set or a fold-in to GAP #3,
contingent on a Session-2 wiring audit. Session 2's audit found:

- `UnresolvedItemAggregator.Build(...)` exists and is well-tested
  (`tests/AccessHarvester.Pipeline.Tests/UnresolvedItemAggregatorTests.cs`,
  `EndToEndFixtureTests.cs`).
- It is **not called from `Pipeline.Run()` or `Pipeline.Enrich()`**.
  Production-pipeline emission of an `UnresolvedItems.json` artifact
  does not exist; the aggregator is invoked only from hermetic tests.

The fold-in into F2 is appropriate because:

1. Both GAP #3 (manifest aggregator + golden) and GAP #5 (UnresolvedItems
   emission) require modifying the same `Pipeline.Run()` emission stage.
2. The manifest aggregator's design naturally includes UnresolvedItems
   as a top-level array per the v1.0 schema's `UnresolvedItems[]`
   field.
3. F2's golden corpus is the regression gate for whether the
   UnresolvedItems wiring is correct.

The fold-in trims one set from the audit's net count and avoids two
separate passes over the same insertion point. The decision is
documented inline in F2's stub spec ("Why GAP #5 folds into this
set" section) so a future orchestrator reading F2 understands the
combined scope.

---

## Out-of-tree items

The following are tracked elsewhere; they are NOT new sets:

- **Schema spot-check (gap-audit's Criterion #2 Minor).** If F2's
  manifest aggregation surfaces schema-doc drift, the fix is a
  conditional future tree-amendment to `schema-finalization-and-docs`
  authored by F2's orchestrator at the time the drift is observed.
  See "Tree-amendments not authored" below for why no
  `tree-amendments.md` artifact exists in this set.
- **MiniComplex.accdb error-audit pass.** Out of Phase 4 per Open
  Question 1's resolution. If reauthorized post-Phase-4, the audit
  would author a separate set then. Not on this tree.
- **Reauthorizing any item on the strategy.md Frozen list.** Out
  of scope per the planner's spec; recorded in `gap-audit.md` →
  "Candidate-for-unfreezing items" with audit recommendations.
  Reauthorization is a human/strategy decision, not a planner
  output.

### Tree-amendments not authored (intentional)

The planner's spec.md → Session 2 "Creates" list names
`tree-amendments.md` as conditional ("only if any amendments are
proposed"). **None were proposed by this session, so no
`tree-amendments.md` was authored.** All six in-scope gaps from
gap-audit.md became either:

- A new stub spec (GAPs #1, #3, #4, #6, #7 → F1–F5 above), or
- A fold-in to a sibling new stub (GAP #5 → folded into F2).

No gap fits naturally into an existing pre-Phase-4 session set in
a way that would warrant amending that set's already-shipped spec.
The conditional future tree-amendments F1–F5's stop rules reference
(schema-finalization-and-docs for schema drift; enrichment sets for
Ribbons / XML gaps surfaced during golden-output generation) are
authored at the time of observation by the F-set's orchestrator,
NOT pre-authored here. Pre-authoring contingent amendments would
be speculation, not planning.

### Routing-playbook-delta not authored (intentional)

The planner's spec.md → Session 2 "Creates" list also names
`routing-playbook-delta.md` as conditional ("only if any new task
categories are needed"). **None are needed.** Audit of the planned
F1–F5 work confirms every routed call uses an existing task type:

- `analysis` — used in every set for ai-assignment.md authoring +
  cross-set decision support.
- `architecture` — used in F2 (manifest aggregator design) per
  the existing "above ~3K tokens use Opus orchestrator
  in-conversation" Convention.
- `code-review` — used in F2 and F5 for routed pre-verification
  on the C# wiring changes.
- `test-generation` — used in F1, F3, F5 for routed test
  authoring (subject to the truncation-watch lesson).
- `session-verification` — used in every set (mandatory cross-
  provider Rule #2).
- `summarization` — used in every set for change-log + session-
  summary prose per the established Convention.

The F4 audience-check and downstream-AI probes use `analysis` with
novel prompts but do not require a new task type. The router's
existing tier-and-effort assignments cover the audited work; no
config change is needed.

---

## How Session 3 uses this tree

Session 3 of this planner reads `remaining-work-tree.md` plus every
existing `change-log.md` in `docs/session-sets/` to build
`docs/session-sets/ROADMAP.md` — a top-level pointer document with
already-complete sets (with change-log links), the five planned
sets here (with stub-spec links), and deferred sets clearly marked.
The recommended execution order above feeds into the ROADMAP's
"what to start next" guidance.
