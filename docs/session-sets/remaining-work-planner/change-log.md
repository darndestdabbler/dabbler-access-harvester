# Change Log — remaining-work-planner

> Sessions 1–3 of 3. Completed 2026-05-17.
> Meta-planning set. No code changes; deliverables are planning artifacts.

---

## What this set produced

A named, sequenced execution plan for closing
[`docs/planning/strategy.md` Phase 4](../../planning/strategy.md#phase-4--mechanical-harvester-finish-line)
(the mechanical-harvester finish line). The plan resolves Phase 4 from
"we know the goal" to **5 named session sets, 13 sessions total,
~$1.00–$3.20 routed-cost rollup**, with prerequisites and parallelism
explicit at the per-feature level.

Four cumulative artifacts:

1. **[`gap-audit.md`](gap-audit.md)** (Session 1) — criterion-by-
   criterion classification of the harvester's current state against
   the seven Phase 4 "Done" criteria. Verdict: 1 COVERED, 5 small or
   fold-in GAPs, 1 central GAP. 5 frozen items confirmed frozen.
2. **Five stub specs at `docs/session-sets/harvester-finish-*/spec.md`**
   (Session 2) — F1 through F5, authored at acceptance-criteria level
   per the Session-1-vs-Session-2 boundary discipline.
3. **[`remaining-work-tree.md`](remaining-work-tree.md)** (Session 2)
   — row-per-set index with per-feature prerequisite table and the GAP
   #5 fold-in rationale. Authoritative for Phase 4 dependencies.
4. **[`docs/session-sets/ROADMAP.md`](../ROADMAP.md)** (Session 3) —
   top-level pointer document covering every session set in the
   project (completed, Phase 4 planned, deferred/cancelled/retired).
   The first artifact a fresh orchestrator reads.

`docs/planning/strategy.md` Phase 4 section was updated in Session 3
with the planned-sequence actuals.

---

## Session 1: Phase 4 Gap Audit

- **Read end-to-end:** strategy.md (Phase 4 + Frozen list), project-
  guidance.md, lessons-learned.md, session-set-authoring-guide.md,
  the strategic-reviews/ reports, and
  `VBA-Parser-Migration-Harvester-Plan.md` (893 lines). Most
  inherited from preceding strategy-reset conversation context.
- **Inspected current pipeline state:** 10 production projects + 10
  test projects + 1 IR codegen tool; CLI subcommands; enrichment
  pipeline surface; schema files; examples; samples.
- **Authored [`gap-audit.md`](gap-audit.md)** in-conversation by
  Claude Opus 4.7 @ effort=high per the orchestrator-matches-target
  convention. For each of the seven Phase 4 criteria, classified
  as COVERED / GAP (small or fold-in) / GAP (central) / AMBIGUOUS /
  CANDIDATE FOR UNFREEZING with working name, rough scope,
  dependency on existing planned work, and cost band.
- **Final classification:** 1 COVERED (schema finality, #2), 5
  small-or-fold-in GAPs (#1, #4, #5, #6, #7), 1 central GAP (#3
  canonical-golden-output). Three open questions surfaced for the
  human (MiniComplex inclusion; macros/property-sheet boundary;
  packaging shape) — all three resolved 2026-05-17, recorded in the
  audit's "Open questions" section and the set's
  [`disposition.json`](disposition.json) under
  `post_close_resolutions`. Five frozen items confirmed frozen.
- **Cross-provider verification (3 rounds, gpt-5-4, $0.337):**
  - R1 ISSUES_FOUND — 1 Major (criterion-6 classification
    inconsistency) + 2 Minor (over-deferred open questions;
    packaging implementation prescription).
  - R2 ISSUES_FOUND — 1 Minor stale-cross-reference introduced by
    the R1 fix.
  - R3 VERIFIED.

**Creates:** `gap-audit.md`, `ai-assignment.md`,
`session-reviews/session-001.md`, `issue-logs/session-001.json`,
`q2-q3-recommendations/{gemini-pro,gpt-5-4}.md` (cross-provider
recommendations the human used to adjudicate Open Questions 2 and 3),
`disposition.json` (per-session shape with `next_orchestrator` +
`blockers`).

---

## Session 2: Plan The Remaining Work

- **Inventoried existing assets** before stub-spec authoring per the
  "Inventory Existing Assets Before Designing New Infrastructure"
  lesson: confirmed `UnresolvedItemAggregator.Build(...)` exists and
  is well-tested but **not called from `Pipeline.Run()` or
  `Pipeline.Enrich()`**. This finding drove the GAP #5 fold-in
  decision (folded into F2 because both target the same
  insertion point).
- **Authored five stub specs** at acceptance-criteria level (per the
  Session-1-vs-Session-2 boundary discipline carried forward from
  Session 1's R1 Minor #3 calibration note):
  - F1 [`harvester-finish-canonical-parse-success-report`](../harvester-finish-canonical-parse-success-report/spec.md)
    — Criterion #1 gate; 2 sessions, small.
  - F2 [`harvester-finish-canonical-golden-output`](../harvester-finish-canonical-golden-output/spec.md)
    — Criteria #3 + #5 (folded); 4 sessions, medium. Central
    deliverable.
  - F3 [`harvester-finish-cli-e2e-test-audit`](../harvester-finish-cli-e2e-test-audit/spec.md)
    — Criterion #4 gate; 2 sessions, small.
  - F4 [`harvester-finish-operations-and-interpretation-docs`](../harvester-finish-operations-and-interpretation-docs/spec.md)
    — Criterion #6 (downstream-AI-consumer-facing portion); 3
    sessions, small-to-medium.
  - F5 [`harvester-finish-cli-packaging`](../harvester-finish-cli-packaging/spec.md)
    — Criterion #7 (self-contained Windows binary); 2 sessions,
    small. Final Phase 4 gate.
- **Authored [`remaining-work-tree.md`](remaining-work-tree.md)** with
  per-feature prerequisite table (one feature can depend on a
  different prereq than its sibling; the set-level prereq column is
  the union). No splits recommended; F3 has the deepest per-feature
  divergence but the cost of splitting outweighs the parallelism
  gain.
- **Skipped (intentional, recorded in tree's "Out-of-tree items"):**
  - `tree-amendments.md` — no proposals (all six in-scope gaps became
    either new stubs or a fold-in; no gap fits naturally into an
    existing pre-Phase-4 set).
  - `routing-playbook-delta.md` — no new task types needed (audit of
    F1–F5 confirmed every routed call uses an existing task type).
- **Cross-provider verification (3 rounds, gpt-5-4, $0.672):**
  - R1 ISSUES_FOUND — 3 Major (tree prereq inconsistency; missing
    skip-rationale for tree-amendments.md / routing-playbook-delta.md;
    missing freeze-boundary bullets across all 5 stubs).
  - R2 ISSUES_FOUND — 1 Major + 1 Minor introduced by R1 fixes /
    deeper sweep (F3 determinism gate didn't cover extract/translate;
    cost rollup mismatch).
  - R3 VERIFIED.
- **Next-orchestrator recommendation for Session 3 routed via
  gemini-pro** ($0.009): Opus @ effort=**medium** (down from high) +
  instruction to treat `remaining-work-tree.md` as authoritative
  input rather than re-interpreting it. Calibrated against Session
  3's narrower synthesis surface (single document, no
  cross-stub-consistency policing).

**Creates:** F1–F5 stub specs (5 files under
`docs/session-sets/harvester-finish-*/spec.md`),
`remaining-work-tree.md`, `session-reviews/session-002.md` (R1),
`session-reviews/session-002-round2.md` (R2),
`session-reviews/session-002-round3.md` (R3),
`session-3-next-orchestrator-recommendation.md`,
`session-3-next-orchestrator-recommendation-meta.json`.

---

## Session 3: Final Roadmap + Change Log

- **Verified Session 2 prerequisites:** `remaining-work-tree.md`
  exists; the five stub specs exist with `Status: STUB` headers;
  Session 2 committed cleanly (prior session verified per the
  "Verify The Prior Session Actually Committed And Pushed" convention).
- **Enumerated every session set** under `docs/session-sets/`: 32
  complete (with `change-log.md`), 1 in-progress (this set), 5
  Phase 4 stubs (F1–F5, status `not-started`), 1 deferred to future
  migration repo (`blazor-update-workflows`), 1 cancelled
  (`integration-testing-and-acceptance`).
- **Adjudicated four sets the human flagged for possible
  cancellation** (`jet-sql-resolution-and-dynamic`,
  `enrichment-functions-navigation-review`, `blazor-update-workflows`,
  `vba-symbol-resolution-and-enrichment`): three already in
  appropriate closed states (complete + retired + complete with
  buffer sessions unused); the fourth (`blazor-update-workflows`)
  remains `deferred` per the Phase 3a strategy-doc commitment to the
  future migration repo. Human accepted "leave all four as-is."
- **Authored [`ROADMAP.md`](../ROADMAP.md)** in-conversation @
  effort=medium per Session 2's routed recommendation. Faithful
  translation of `remaining-work-tree.md` for the Phase 4 rows; no
  creative re-sequencing. Status-icon legend, parallel-eligibility
  advisory, deferred/cancelled/retired section, document-maintenance
  rules.
- **Updated [`strategy.md` Phase 4 section](../../planning/strategy.md#phase-4--mechanical-harvester-finish-line)**
  with the planned-sequence actuals (F1 → F5 ordering, parallel
  advisory, ~$1.00–$3.20 cost rollup, ROADMAP link). Added a
  2026-05-17 (same-day) revision entry describing the planner's
  output.
- **Authored this change log and `disposition.json`** as the closeout
  artifacts.
- **Cross-provider verification:** see `session-reviews/session-003.md`.

**Creates:** `../ROADMAP.md`, this change log,
`disposition.json` (final-session shape with `next_session_set`),
`session-reviews/session-003.md`.

**Touches:** `docs/planning/strategy.md` (Phase 4 section +
Revisions entry).

---

## What ships with this set

| Artifact | Path | Role |
|---|---|---|
| Gap audit | [`gap-audit.md`](gap-audit.md) | Phase 4 criterion-by-criterion classification |
| Five Phase 4 stub specs | `docs/session-sets/harvester-finish-*/spec.md` | F1–F5 acceptance-criteria contracts |
| Remaining-work tree | [`remaining-work-tree.md`](remaining-work-tree.md) | Per-feature prereqs + execution order authority |
| ROADMAP | [`docs/session-sets/ROADMAP.md`](../ROADMAP.md) | Project-wide session-set index |
| Strategy update | [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4 + Revisions 2026-05-17 | Planned-sequence actuals landed |
| Q2/Q3 cross-provider recommendations | [`q2-q3-recommendations/`](q2-q3-recommendations/) | Audit trail for Open Questions 2 and 3 resolutions |

---

## Routed cost across the set

| Session | Routed cost | Notes |
|---|---|---|
| 1 | $0.337 | 3 verification rounds (R1 ISSUES + R2 ISSUES + R3 VERIFIED) |
| 2 | $0.681 | 3 verification rounds (same pattern) + 1 next-orc recommendation (gemini-pro) |
| 3 | (filled below in Session 3 actuals) | 1 verification round + Session 3 closeout |

The Session 1 and Session 2 spec-target overruns (70% and 240% above
the $0.05–$0.20 target) were driven by the same R1 → R2 → R3 pattern
in both sessions. This is a meta-planning-set characteristic
(synthesis-heavy stub authoring with cross-document consistency
surfaces) and the spec target was authored against a no-retry
assumption that did not hold. See `ai-assignment.md` Session 2
calibration note #1 for the promotion-to-Convention candidacy if
Session 3 also hit the pattern.

---

## What this set explicitly did NOT do

Per the planner spec's "What the planner explicitly DOES NOT do":

- **Authored no parser code, schema files, test fixtures, CLI
  subcommands, or production documentation** beyond the four planning
  artifacts. F1–F5 do that work.
- **Did not run `dotnet build` or `dotnet test`** (no code touched).
- **Did not edit existing specs.** The 5 stubs are new files; no
  amendments to in-flight or completed sets were authored
  (`tree-amendments.md` was the conditional file for that, and the
  audit produced zero proposals).
- **Did not edit `ai_router/router-config.yaml`.** The audit found no
  new task types needed (`routing-playbook-delta.md` was the
  conditional file for that, and the audit produced zero deltas).
- **Did not re-authorize any item on the strategy.md Frozen list.**
  The audit considered each and recommended keeping all five frozen.

---

## Next session set

**Start F1: [`harvester-finish-canonical-parse-success-report`](../harvester-finish-canonical-parse-success-report/spec.md).**
Rationale: F1 has no new prereqs (uses the existing parser +
`UnresolvedItemAggregator`) and establishes the parse-coverage gate
that F2's golden output diffs against. F3's first feature
(black-box CLI tests for `extract`/`translate`/`run`) is also
parallel-eligible from the start if window-budget burning justifies
a second branch — both have no F2 dependency. See the
[ROADMAP's Phase 4 parallel-eligibility advisory](../ROADMAP.md#phase-4--mechanical-harvester-finish-line).
