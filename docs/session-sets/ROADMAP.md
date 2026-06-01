# Session-Set ROADMAP — Dabbler Access Harvester

> **Purpose:** Top-level pointer document listing every session set in
> the project in recommended execution order. A new orchestrator (or
> fresh human picking the project up) reads this first to know what to
> do next.
> **Authored:** 2026-05-17 by `remaining-work-planner` Session 3 of 3.
> **Authority:**
> - [`docs/planning/strategy.md`](../planning/strategy.md) Phase 4
>   for the finish-line goal and Frozen list.
> - [`docs/session-sets/remaining-work-planner/remaining-work-tree.md`](remaining-work-planner/remaining-work-tree.md)
>   for Phase 4 prerequisites, execution order, and per-feature
>   depth — the authoritative input for the Phase 4 rows below.
> - [`docs/session-sets/remaining-work-planner/gap-audit.md`](remaining-work-planner/gap-audit.md)
>   for the per-criterion gap classification that drove the Phase 4
>   plan.
>
> This ROADMAP is a **pointer** document. Each row is a one-paragraph
> description plus links to the set's `spec.md` and (when present)
> `change-log.md`. Content is not duplicated from the specs.

---

## How to read this document

- **Status icons:**
  - `[x]` Complete (has `change-log.md`)
  - `[~]` In progress
  - `[ ]` Stub / not started
  - `[r]` Retired or superseded (the work was redirected; the set
    has its own change-log explaining why)
  - `[d]` Deferred to the future migration repo (spec preserved, not
    scheduled to execute here)
  - `[c]` Cancelled in-flight (set was started, then stopped; not a
    candidate to resume here)
- **What to start next:** the first row under `## Phase 4 — Mechanical
  Harvester Finish Line` that is not yet complete. F1.5
  (`vba-attribute-header-strip`) today — F1 closed 2026-05-17.
- **Session count ranges** are conservative; the per-stub specs are
  the authoritative budget when present.

---

## Recently closed / on deck

| Status | Slug | Why now |
|---|---|---|
| `[x]` | [`harvester-report-baseline-capture`](harvester-report-baseline-capture/change-log.md) | Closed 2026-05-31. Recovered Session 3 honestly after a bad prior closeout attempt: `report-baseline` now stays aggregates-first, optional PDF render runs only after aggregate success, and the shipped docs/help text match the runtime contract. The committed golden corpus + regression gate + `READ-ME-FIRST` propagation remain explicitly deferred to the named follow-up stub [`harvester-report-baseline-golden-corpus`](harvester-report-baseline-golden-corpus/spec.md). |
| `[x]` | [`harvester-report-baseline-golden-corpus`](harvester-report-baseline-golden-corpus/change-log.md) | Closed 2026-06-01. Committed the canonical `reports-baseline/` corpus for HomeHealthCare and Northwind, wired those artifacts into the canonical regression gate, repaired the report-baseline CLI teardown path in `CliInvoker`, and updated both fixture guides so downstream agents can discover the new artifact tier. |
| `[x]` | [`harvester-access-com-graceful-shutdown`](harvester-access-com-graceful-shutdown/change-log.md) | Closed 2026-06-01 (maintainer: claude-code). Root-cause fix for the on-deck `harvester-parallel-com-test-cleanup` candidate: hardened the single COM chokepoint `AccessComSession` to capture its Access PID up front (via `hWndAccessApp` -> `GetWindowThreadProcessId`) and force-kill only that exact PID if a graceful `Quit()` leaves a zombie `msaccess.exe`. Session 1 added the `IProcessController` seam + exact-PID reaper under 9 hermetic unit tests; Session 2 verified on a real Windows+Access host that the full COM/PDF integration tier (56/56) leaves **zero orphaned `msaccess.exe`/`testhost`** (process census 0/0), simplified the blunt `CliInvoker` `taskkill` teardown, fixed a pre-existing parallel-COM file-lock race (added three Pipeline classes to the serializing collection), and wired `docs/access-com-zombie-cleanup.md` into `project-guidance.md` + the three agent files. **Absorbs** the on-deck `harvester-parallel-com-test-cleanup` candidate. |
| `[x]` | [`harvester-consumer-workflow-instructions`](harvester-consumer-workflow-instructions/change-log.md) | Closed 2026-06-01 (maintainer: claude-code). Made the harvester output self-instructing for AI consumers: interpretation-guide §3.1 canonical workflow + §3.3 mandatory "explain the artifacts + link the editor, never hand over raw JSON" rule, a plain-language developer guide [`reviewing-the-feedback-artifacts.md`](../harvester/reviewing-the-feedback-artifacts.md), and editor URLs wired into `READ-ME-FIRST.md`. Resolved the editor-hosting open decision by cross-provider consensus (defer classification-editor hosting; document local-open). Fixed a real one-click-blocking bug: `object-use-checklist.json` was emitted camelCase and loaded empty in the PascalCase-only uat-checklist-editor — fixed `UsageChecklistWriter` + test + regenerated golden samples. Both sessions VERIFIED first round. |

Phase 4 closed [2026-05-29](harvester-cli-distribution/change-log.md); the
most recent maintenance set
([`harvester-manifest-index-and-batch-readiness`](harvester-manifest-index-and-batch-readiness/change-log.md))
closed 2026-05-29 after answering the first real downstream consumer's six
findings, and surfaced two consumption-triggered follow-ons. The first,
[`harvester-report-baseline-capture`](harvester-report-baseline-capture/change-log.md),
closed 2026-05-31 after repairing the Session 3 closeout and locking the
shipped `report-baseline` contract. The follow-up set
[`harvester-report-baseline-golden-corpus`](harvester-report-baseline-golden-corpus/change-log.md)
closed 2026-06-01, landing the deferred golden corpus, regression gate,
fixture-guide propagation, and Windows-specific regression-harness cleanup
for the CLI-backed validation path. The sibling
[`harvester-consumer-workflow-instructions`](harvester-consumer-workflow-instructions/change-log.md)
closed 2026-06-01 (maintainer: claude-code) — making the harvester output
self-instructing so a consuming AI agent discovers the `identify-usage` /
`identify-cleanup` feedback workflow, giving developers a plain-language guide
to the feedback artifacts, and fixing a checklist-casing bug that made the
usage checklist load empty in the editor. The COM/zombie fragility it surfaced
mid-session was fixed by the now-closed
[`harvester-access-com-graceful-shutdown`](harvester-access-com-graceful-shutdown/change-log.md).

The on-deck `harvester-parallel-com-test-cleanup` candidate is **absorbed** by
the closed `harvester-access-com-graceful-shutdown` (root-cause fix at the
`AccessComSession` chokepoint). **Next up (routed recommendation, 2026-06-01):**
the **`sanitize`-against-HomeHealthCare timeout investigative spike** documented
in [`lessons-learned.md`](../planning/lessons-learned.md) — reliability before
features, the ordering this arc just validated. *Re-measure first:* the COM
graceful-shutdown hardening may have resolved the timeout as a side effect, in
which case the spike closes with a documentation note; no spec required. The
feature capstone `harvester-sql-migration-generator`
([`harvester-cli-distribution`'s recommendation](harvester-cli-distribution/next-session-set-recommendation.md))
follows the spike — its "until CI is reliable" deferral is now satisfied; it
needs its own spec plus a belongs-here-vs-future-migration-repo check.

---

## Phase 4 — Mechanical Harvester Finish Line

These six sets close out [`strategy.md` Phase 4](../planning/strategy.md#phase-4--mechanical-harvester-finish-line).
Total **6 session sets, 14–15 sessions**, routed-cost rollup
**~$1.10–$3.60**. The recommended execution order tracks
[`remaining-work-tree.md`](remaining-work-planner/remaining-work-tree.md#recommended-execution-order)
faithfully — the per-feature prereq table in that file is the
authority on what can run in parallel.

F1.5 was inserted on 2026-05-17 (post-F1-close) after F1's reports
quantified the VBA `Attribute VB_*` header gap (HHC 91% / Northwind
79% parseFailed driven entirely by code-behind headers the grammar
does not recognise). The human picked **strip-at-loader rather than
extend-the-grammar**, scoped as its own session set per the lean +
"strategy-level changes are session-set work" Conventions; F2's
prereq list expanded to include F1.5 because F1.5 regenerates the
parse-success-report goldens F2 reconciles against.

| Order | Status | Slug | Phase 4 criterion | Prereqs | Sessions |
|---|---|---|---|---|---|
| F1 | `[x]` | [`harvester-finish-canonical-parse-success-report`](harvester-finish-canonical-parse-success-report/change-log.md) | #1 Parser coverage finality | None new (uses existing parser + `UnresolvedItemAggregator`) | 2 |
| F1.5 | `[x]` | [`vba-attribute-header-strip`](vba-attribute-header-strip/change-log.md) | #1 Parser coverage finality (tightens F1's signal — strips the canonical `Attribute VB_*` header before parsing, regenerates F1's goldens) | F1 (this set regenerates its goldens) | 1–2 |
| F2 | `[x]` | [`harvester-finish-canonical-golden-output`](harvester-finish-canonical-golden-output/change-log.md) | #3 Sample-corpus validation **+ #5 Unresolved-record reporting (folded in)** | F1 + F1.5 (parse counts must agree with golden manifest counts, and post-strip counts are the gate-relevant ones) | 4 |
| F3 | `[x]` | [`harvester-finish-cli-e2e-test-audit`](harvester-finish-cli-e2e-test-audit/change-log.md) | #4 CLI surface | F2 (determinism gate reuses F2's normalization layer) | 2 |
| F4 | `[x]` | [`harvester-finish-operations-and-interpretation-docs`](harvester-finish-operations-and-interpretation-docs/change-log.md) | #6 Documentation (downstream-AI-consumer-facing portion) | F1 + F1.5 + F2 (docs reference ground-truth output and per-reason narratives) | 3 |
| F5 | `[x]` | [`harvester-finish-cli-packaging`](harvester-finish-cli-packaging/change-log.md) | #7 Packaging | F3 (CLI is feature-stable with black-box coverage before cutting a release) | 2 |

**Phase 4 closed 2026-05-29.** All six F-sets (F1–F5 plus the inserted
F1.5) shipped. Two post-Phase-4 extensions followed:

- [`harvester-data-classifier`](harvester-data-classifier/change-log.md)
  added the `classify` + `sanitize` subcommands and the
  `classification.json` v1.0 schema; Phase 4 of the
  data-classification-plan (SQL Server migration generator) is
  deferred to the recommended next set.
- [`harvester-cli-distribution`](harvester-cli-distribution/change-log.md)
  shipped the GitHub Actions release workflow, rewrote
  [`install.md`](../harvester/install.md) for the GitHub Releases
  channel, and cut + published the first real release
  ([v1.0.0](https://github.com/darndestdabbler/dabbler-access-harvester/releases/tag/v1.0.0)).
  A downstream consumer can now install the harvester binary without
  contacting the maintainer.
- [`harvester-manifest-index-and-batch-readiness`](harvester-manifest-index-and-batch-readiness/change-log.md)
  answered the first real downstream consumer's six findings from the
  2026-05-29 harvest of `NorthwindDevEdition.accdb`. Closed:
  manifest completeness (Tables / Relationships / Modules now
  populated); manifest restructured as a self-describing index
  (per-category `FileMap`, `Documentation` pointer with public HTTPS
  URL, static `READ-ME-FIRST.md`); additive schema bump v1.0 → v1.1
  with per-document dispatch in `validate`; `--no-clean` opt-out for
  the new default-clean re-harvest semantics; `--json-status`
  provenance fields (`HarvesterVersion`, `StartedAt`, `DurationMs`,
  `Clean`); cross-DB roll-up index at
  `<out>/AccessMetaData/index.json`; per-field-type semantics
  documented in `interpretation-guide.md` §5.5; new
  [`batch-operations.md`](../harvester/batch-operations.md) covering
  many-DB layout, sequential-only operation, MSACCESS hygiene, and
  the status-envelope provenance contract. The set surfaced the
  xUnit parallel-COM Integration-suite contention pattern as the
  recommended follow-up.

**Parallel-eligibility (advisory, not a schedule):**

- **F1.5 and F3's "black-box CLI tests" feature** can start in parallel
  immediately — F1.5 only needs F1 (closed), and F3's first feature has
  no F-set prereq. F3's "determinism gate" feature defers until F2 lands.
  Per [`project-guidance.md` → Token-Window Budget Overrides](../planning/project-guidance.md)
  and the per-set git-worktree convention, parallel runs go on
  `session-set/<slug>` branches.
- **F2 strictly waits** on both F1 and F1.5 — F2 reconciles per-object
  manifest counts against the post-strip parse-success-report goldens.
- **F3 and F4 can run in parallel after F2 closes** — no mutual
  dependency; both consume F2.
- **F5 strictly waits** on F3.

**Phase 4 Criterion #2 (Schema finality)** is **COVERED** by the
already-shipped
[`schema-finalization-and-docs`](schema-finalization-and-docs/change-log.md);
no F-set is needed. The
[`gap-audit.md`](remaining-work-planner/gap-audit.md#2-schema-finality)
records a light schema-doc spot-check Session 1 of F2 may surface; if
drift appears, it lands as a tree-amendment to
`schema-finalization-and-docs`, not a new set.

When F5 closes, **Phase 4 is done** — the harvester ships with golden
output for two canonical fixtures (`NorthwindDevEdition.accdb` +
`HomeHealthCare.accdb`), determinism gates on every CLI subcommand, a
downstream-AI-consumer-facing interpretation guide, and a
self-contained Windows binary installable without a from-source build.

---

## Completed sets

Grouped by the structural arc they belong to. Within each group, rows
are roughly chronological. Each row links the set's `change-log.md` for
detailed outcomes; the spec is one hop away from there.

### Bootstrap and meta-planning

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`accessexportertemp1-import`](accessexportertemp1-import/change-log.md) | Imported the bootstrap codebase from the sibling `../AccessExporterTemp1` repo into this repo's project layout — the starting point everything else builds on. |
| `[x]` | [`vba-spec-study`](vba-spec-study/change-log.md) | Studied the VBA language spec corners (lexical, expression-precedence, error-flow) before grammar authoring, so the parser arc could start grounded. |
| `[x]` | [`vba-parser-session-set-generator`](vba-parser-session-set-generator/change-log.md) | Generated the parser-arc session-set tree (rows #1–#17 in [`session-set-tree.md`](vba-parser-session-set-generator/session-set-tree.md)) that scoped the parser, resolver, and enrichment work. |

### VBA parser foundations (migration plan Phases 1.x)

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`parser-foundations`](parser-foundations/change-log.md) | Stood up the verification harness, reconciled parser test-case schema with the harvester's tier outputs, and wired ANTLR4 build-time generation into `AccessHarvester.Vba.Grammar`. |
| `[x]` | [`parser-infrastructure`](parser-infrastructure/change-log.md) | Built the runtime layer over the validated grammar — parser pipeline, error recovery, AST shape, position tracking, preprocessor pass-through. |
| `[x]` | [`vba-lexical-and-expression-grammar`](vba-lexical-and-expression-grammar/change-log.md) | Landed the lexical rules and expression-grammar branch of `Vba.g4`. |
| `[x]` | [`vba-statement-and-module-grammar`](vba-statement-and-module-grammar/change-log.md) | Landed the statement and module-level grammar rules — control flow, declarations, module structure. |
| `[x]` | [`vba-grammar-validation`](vba-grammar-validation/change-log.md) | Ran the full grammar against the 12 module-level fixtures plus hazard-driven edge fixtures; classified and fixed every failure; produced the parse-failure log downstream resolver sets consumed. |

### VBA resolver and Access integration (migration plan Phases 2–4)

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`resolver-declarations-and-scopes`](resolver-declarations-and-scopes/change-log.md) | Landed the declaration model and scope-tree visitor every later resolver child set builds on. |
| `[x]` | [`resolver-references-and-with-blocks`](resolver-references-and-with-blocks/change-log.md) | Landed the reference-resolution algorithm and the With-block flattening pass that resolves `.Member` accesses. |
| `[x]` | [`resolver-default-members-and-inference`](resolver-default-members-and-inference/change-log.md) | Landed the default-member registry that resolves implicit `.Value` (and analogues) plus the lightweight type-inference pass that annotates `Variant` declarations. |
| `[x]` | [`access-resolver-symbol-table-integration`](access-resolver-symbol-table-integration/change-log.md) | Injected Access metadata (forms, reports, controls) into the resolver as synthetic declarations so VBA references to UI objects resolve through the same scope/lookup machinery. |
| `[x]` | [`access-type-library-registry`](access-type-library-registry/change-log.md) | Landed the hand-curated DAO / ADO / Office / Access object-model registry and the `ITypeLibraryRegistry` contract the resolver consumes. |

### Access metadata extraction

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`access-object-extractor-spike`](access-object-extractor-spike/change-log.md) | Investigated `samples/AccessObjectExtractor.accdb`'s metadata-extraction approach and produced a gap-analysis + integration-strategy doc. |
| `[x]` | [`access-metadata-extraction`](access-metadata-extraction/change-log.md) | Extracted form, report, query, and table metadata via DAO/ACE and authored the database-level fixtures the migration plan's Phase 4 required. |
| `[x]` | [`table-extractor-coverage`](table-extractor-coverage/change-log.md) | Closed the field-coverage gap between `TableExporter` / `RelationshipExporter` and the Access-side ground-truth. |

### JET SQL parser and resolution (migration plan Phase 5)

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`jet-sql-grammar-and-parser`](jet-sql-grammar-and-parser/change-log.md) | Authored the ANTLR4 grammar for the JET/ACE SQL dialect and the parser infrastructure (error recovery, AST, position tracking). |
| `[x]` | [`jet-sql-resolution-and-dynamic`](jet-sql-resolution-and-dynamic/change-log.md) | Resolved `[Forms]![X]![Y]` references inside SQL ASTs and performed intra-procedural data-flow analysis of VBA-constructed dynamic SQL. |

### Enrichment pipeline (migration plan Phase 6)

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`enrichment-events-and-validation`](enrichment-events-and-validation/change-log.md) | Classified every event handler by purpose (validation / cascading / filter / navigation / side-effect / calculation / formatting / mixed); extracted validation rules into structured form. |
| `[x]` | [`enrichment-functions-navigation-review`](enrichment-functions-navigation-review/change-log.md) | Classified every user-defined VBA procedure by translation strategy; reconstructed the navigation graph; landed `UnresolvedItem` / `UnresolvedItemAggregator`. |
| `[x]` | [`enrichment-queries-and-forms`](enrichment-queries-and-forms/change-log.md) | Produced structured query-abstraction and form-component specifications — converted parsed SQL ASTs and Access form metadata into method-shaped query specs and component descriptors. |
| `[x]` | [`structured-form-report-extractor`](structured-form-report-extractor/change-log.md) | Audited the `ir/` output for Forms and Reports against the YAML ground-truth and promoted `RecordSource` / `Filter` / `OrderBy` to typed fields. |
| `[x]` | [`form-report-code-and-grouping`](form-report-code-and-grouping/change-log.md) | Populated the two remaining deferred fields on the typed Form/Report records. |

### Schema (migration plan Phase 7)

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`schema-finalization-and-docs`](schema-finalization-and-docs/change-log.md) | Promoted `harvester-schema.v0.json` to v1.0 with backward-compatibility plan; landed example fixtures for every major construct; wired output-validation that fails the harvester on schema drift. **Phase 4 Criterion #2 COVERED here.** |

### Symbol-DB exploration arc (calibration → pivot)

| Status | Slug | One-line summary |
|---|---|---|
| `[r]` | [`vba-symbol-resolution-and-enrichment`](vba-symbol-resolution-and-enrichment/change-log.md) | **Retired / superseded by `vba-symbol-db`.** A 2026-04-28 PoC that shipped without verification, returned FAIL on backfill, and whose substrate-design was found redundant against the already-built C# parser/resolver/enrichers. See its change-log for the full retirement explanation. |
| `[x]` | [`vba-symbol-db`](vba-symbol-db/change-log.md) | Built the queryable SQLite symbol DB substrate. The set ran a 7-session calibration arc and concluded with a PIVOT verdict — routed cross-provider rigor on AI-interpretation columns was paying high cost for near-zero correlation. See [`vba-symbol-db/verdict.md`](vba-symbol-db/verdict.md). |
| `[x]` | [`vba-symbol-db-path-b-probe`](vba-symbol-db-path-b-probe/change-log.md) | Three-session probe that tested **translation + characterization tests** (Path B) against AI-interpretation (Path A). Verdict: **Hybrid at the procedure level**. See [`vba-symbol-db-path-b-probe/decision.md`](vba-symbol-db-path-b-probe/decision.md). |

### Downstream-consumer pilots (strategy.md Phases 1–2 + 3c, 3e auxiliary)

| Status | Slug | One-line summary |
|---|---|---|
| `[x]` | [`workflow-package-pilot`](workflow-package-pilot/change-log.md) | **Phase 1 of strategy.md.** Produced four workflow packages (Add an Order, Update an Order, Update a Product, Update an Employee) plus 107 characterization tests. Proved the workflow-package shape. |
| `[x]` | [`blazor-add-an-order`](blazor-add-an-order/change-log.md) | **Phase 2 of strategy.md.** Implemented Add an Order end-to-end as a Blazor Server + MudBlazor UI calling a Web API service layer, real EF Core + SQLite persistence. All 111 characterization tests passed unmodified (Option D / invariant-correspondence resolution). |
| `[x]` | [`generalization-validation-on-non-northwind`](generalization-validation-on-non-northwind/change-log.md) | **Phase 3c of strategy.md.** Shipped the HHC `Add a Client` workflow package + falsification tests. Verdict: `generalizes-cleanly`. Resolved Open Validation #2. |
| `[x]` | [`lowdefy-ir-foundation`](lowdefy-ir-foundation/change-log.md) | Probe of Lowdefy-as-IR shape. Produced a `ModalDetailForm` JSON Schema, a hand-authored Northwind sample, and the standalone [`tools/AccessHarvester.IrCodegen/`](../../tools/AccessHarvester.IrCodegen/) slice generator. **Probe verdict only — production generator not authorized** ([`strategy.md` Frozen list](../planning/strategy.md#frozen-unless-reauthorized-2026-05-17)). |
| `[x]` | [`dabbler-platform-discovery`](dabbler-platform-discovery/change-log.md) | Three-session inventory pass producing four authoritative cross-repo docs at [`docs/cross-repo/`](../cross-repo/), pinned to `dabbler-platform` commit `aa77020e1ca0` (2026-05-16). Dated snapshot; re-check before relying on specific component names. |

---

## Deferred / cancelled / retired

| Status | Slug | Why it's here |
|---|---|---|
| `[d]` | [`blazor-update-workflows`](blazor-update-workflows/spec.md) | **Deferred to the future migration repo (planned, not yet created).** Session 1 started 2026-05-17 and was paused mid-step when the orchestrator noticed `../dabbler-platform/crud-scaffolding` had shipped two CRUD templates a day prior. Cross-provider strategic review concluded further hand-authored Blazor application work in this repo would dilute the harvester's scope. Spec preserved as input for the future migration repo. See the set's [`session-state.json`](blazor-update-workflows/session-state.json) for the full deferral context and [`strategy.md` Phase 3a](../planning/strategy.md#phase-3--next-set-selection-resolved-2026-05-17). |
| `[c]` | [`integration-testing-and-acceptance`](integration-testing-and-acceptance/spec.md) | Cancelled mid-set. See [`session-state.json`](integration-testing-and-acceptance/session-state.json) for status. |

The frozen work-streams in
[`strategy.md` Frozen list](../planning/strategy.md#frozen-unless-reauthorized-2026-05-17)
have no folder here — they are not session sets, just policy
commitments that would each have to be scoped from scratch if
reauthorized. The current list (five items) covers: production IR
generator beyond the probe; further `dabbler-platform-discovery`
refreshes; CRUD-scaffolder expansion to cover workflow-driven flows;
additional Northwind workflows in this repo beyond the existing four
packages; and old stale enrichment sessions. The gap audit
re-examined each and recommended keeping all five frozen.

---

## What happens after Phase 4 closes

Per [`strategy.md`](../planning/strategy.md):

- The Access-to-Blazor *application* migration moves to the future
  migration repo, not yet created. That repo inherits:
  - The workflow-package shape from `workflow-package-pilot`'s
    [`_template.md`](workflow-package-pilot/_template.md).
  - The Option D / invariant-correspondence pattern from
    [`blazor-add-an-order`](blazor-add-an-order/change-log.md).
  - The Phase 4 golden output as ground truth for the migration's
    extraction layer.
  - The recommendations in
    [`strategy.md` → Recommendations for the future migration repo](../planning/strategy.md#recommendations-for-the-future-migration-repo).
- The harvester repo itself moves to maintenance — bug fixes against
  Phase 4's golden output, schema-tier adjustments triggered by the
  future repo's consumption, no new feature surface.

---

## Document maintenance

Update this ROADMAP when:

1. **A row's status changes** — a stub starts, an in-progress set
   completes, a planned set is deferred. Same edit as the row's
   `session-state.json` flip.
2. **A new session set is authored** — add the row in the appropriate
   group; if it's a Phase 4 set, also update
   [`remaining-work-tree.md`](remaining-work-planner/remaining-work-tree.md).
3. **A Phase 4 set surfaces a tree-amendment** that adds a new set —
   per the planner's anti-coupling discipline, the amendment lands as
   a follow-up commit on the affected spec, and this ROADMAP gets a
   new row.

This document is **not** updated for tactical changes within a set
(those go in the set's own change-log) or for session-by-session
progress (that lives in each set's `activity-log.json`).
