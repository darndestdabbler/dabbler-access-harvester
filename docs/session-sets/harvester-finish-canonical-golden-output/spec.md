# Canonical Golden Output Spec — **STUB**

> **Status:** STUB authored 2026-05-17 by `remaining-work-planner` Session 2.
> This is **the central Phase 4 deliverable** — without it, Phase 4
> has no end-to-end gate.
> Acceptance criteria are pinned; implementation shape (manifest
> aggregator API, golden subset, normalization strategy, diff harness
> layout) is intentionally left for the set's own Session 1.
>
> **Purpose:** For each canonical fixture, commit a `DatabaseManifest`
> + per-object IR golden output and a regression test that re-runs
> the harvester and asserts byte-equality (with a documented
> normalization layer for the few legitimately noisy fields).
> Closes Phase 4 Criterion #3 ("golden output committed for each
> canonical fixture; CI/test suite verifies the harvester reproduces
> that golden output") AND Criterion #5 ("per-run report listing
> every `UnresolvedItem` with structured fields") by emitting a
> top-level `UnresolvedItems.json` from the production pipeline.
> **Created:** 2026-05-17
> **Session Set:** `docs/session-sets/harvester-finish-canonical-golden-output/`
> **Prerequisite:** `harvester-finish-canonical-parse-success-report`
> (parse counts agree with golden manifest counts).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. Deliverables are JSON files committed
> as fixture and a regression test that diffs them.

---

## Origin and authority

- **Authority for scope:** the gap audit at
  [`docs/session-sets/remaining-work-planner/gap-audit.md`](../remaining-work-planner/gap-audit.md)
  → GAP #3 (central) AND GAP #5 (folded — see "Why GAP #5 folds here"
  below).
- **Authority for "Done":**
  [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4
  Criteria #3 and #5.
- **Authority for the missing manifest aggregator:** the prior
  set's coverage audit at
  [`docs/session-sets/integration-testing-and-acceptance/coverage-audit.md`](../integration-testing-and-acceptance/coverage-audit.md)
  → Gap A explicitly states:
  > "The current pipeline emits per-category, per-object files (e.g.,
  > `ir/Forms/<formName>.ir.json`) rather than a single rolled-up
  > manifest. That means there is no manifest-emitter component for
  > Session 1 to exercise — the gap is genuine, not just untested."
- **Authority for canonical-fixture list:** the planner's resolved
  Open Question 1 — `NorthwindDevEdition.accdb` +
  `HomeHealthCare.accdb`. MiniComplex is OUT.

---

## Project Overview

The harvester runs `extract` → `translate` → `enrich` and emits
per-object IR JSON files. The v1.0 schema
(`docs/test-cases/harvester-schema.v1.0.json`) describes a top-level
`DatabaseManifest` envelope with `Database`, `Summary`, `Forms[]`,
`Reports[]`, `Queries[]`, `Modules[]`, `TableSchemas[]`,
`Relationships[]`, and (per GAP #5) an `UnresolvedItems[]` array.
**Today no component aggregates the per-object IR files into that
manifest, and no production code path writes the `UnresolvedItems`
array to disk** — the aggregator is invoked only from hermetic tests.

This set:

1. **Builds the manifest aggregator** in `AccessHarvester.Pipeline/`
   (or `Core/`, whichever fits — Session 1 decides). The aggregator
   reads `ir/<Category>/*.ir.json` plus the per-subsystem unresolved
   inputs (`ResolverDiagnostic`, `FormReferenceAnnotation`,
   `DynamicSqlAnalysisResult`, `NavigationGraph`,
   `FunctionClassification`) the existing extractors / enrichers
   already produce, and emits one top-level `manifest.json` plus one
   `unresolved-items.json` per run (or one combined file — Session 1
   picks the shape that the schema validates against cleanest).
2. **Wires `Pipeline.Run()` to emit the manifest + UnresolvedItems**
   at the run root.
3. **Generates and commits the golden output** for both canonical
   fixtures.
4. **Adds the regression test** that re-runs `Pipeline.Run()` and
   diffs against the committed golden, with a documented
   normalization layer.

### Why GAP #5 folds into this set

GAP #5 (per-run UnresolvedItems report) and GAP #3 (canonical golden
output) share a single insertion point: the production pipeline's
emission stage. GAP #5's `UnresolvedItems.json` is one of the files
GAP #3 needs to capture in the golden corpus. Implementing #5 first
and then #3 would force two passes over the same code; implementing
#3 without #5's emission would commit golden output that does not
exercise the unresolved-aggregation pathway at all. Folding them is
the cheaper, more coherent path. The decision is recorded here for
traceability; the planner's `remaining-work-tree.md` notes the
fold-in alongside its row for this set.

### In scope

1. A manifest-aggregator component (Session 1 picks the API
   surface — likely `IManifestAggregator` or a static
   `DatabaseManifestAggregator.Build(...)` mirroring
   `UnresolvedItemAggregator.Build(...)`).
2. `Pipeline.Run()` modifications to call the aggregator and write
   `<runRoot>/AccessMetaData/<dbName>/manifest.json` and the
   `unresolved-items.json` companion (or combined file).
3. Committed golden output for `NorthwindDevEdition.accdb` and
   `HomeHealthCare.accdb`. The exact golden subset is Session 1's
   call — the candidates are:
   - **Full three-tier** (`raw/` + `kb/` + `ir/`): largest, but
     gives complete regression visibility. Risk: `raw/` SaveAsText
     output may include Access-mutated timestamps (HomeHealthCare's
     binary `.accdb` is committed; Northwind's is gitignored because
     Access mutates internal timestamps on every open).
   - **`kb/` + `ir/` only**, with `raw/` regenerated locally per
     run: smaller, more deterministic. Likely correct trade.
   - **Manifest + UnresolvedItems only**, with per-object IR files
     covered by sampling: smallest, but loses per-object regression
     visibility.
   Session 1 picks; document the rationale.
4. A normalization layer for legitimately noisy fields (timestamps,
   absolute paths, machine-specific GUIDs). Documented inline in the
   harness so a future engineer can extend.
5. The regression test (likely under
   `tests/AccessHarvester.Integration/Pipeline/`,
   `[SkippableFact]`-gated for hosts without Microsoft Access) that:
   - For each canonical fixture, runs `Pipeline.Default().Run(...)`.
   - Applies the normalization layer to the output.
   - Diffs against the committed golden.
   - Fails with a clear "expected vs. actual" message on drift.
6. Schema-conformance gate: the produced manifest validates against
   `harvester-schema.v1.0.json` via `SchemaValidator`. This closes
   the prior set's open Gap A.

### Out of scope

- Northwind's `.accdb` re-commit. Northwind stays gitignored per the
  existing `.gitignore` entry; the test consumes the local copy and
  skips gracefully when absent.
- Adding new schema fields. If the manifest aggregation surfaces a
  field the v1.0 schema does not accommodate, the fix is a
  tree-amendment to `schema-finalization-and-docs`, not a scope
  expansion here.
- Performance optimization. Phase 4 is about correctness, not the
  migration-plan-Phase-8.4 "median database processes in under 10
  minutes" target.
- Fixing parser/extractor bugs the golden surfaces. If the audit
  uncovers a real defect (e.g., a form emits malformed IR), the fix
  lands in the affected enricher's existing project as a
  tree-amendment to the originating session set, and the golden
  pins post-fix. Do not patch by editing the committed golden.
- **Strategy.md "Frozen Unless Reauthorized" items remain frozen.**
  Specifically: Blazor application slices / workflows / pilots,
  production IR codegen, `dabbler-platform-discovery` refresh,
  additional Northwind workflows beyond the existing four pilot
  packages, CRUD-scaffolder expansion, and any item the 2026-05-17
  strategic review froze. The manifest aggregator is harvester-side
  output assembly only; it does NOT feed any downstream Blazor /
  IR-codegen / platform integration this set re-opens.

---

## Sessions

### Session 1 of 4: Manifest Aggregator Design + Build

**Steps:**

1. Read GAP #3 + GAP #5 + Gap A from the prior coverage audit.
2. Elaborate this stub into a full spec (Session-1-vs-Session-2
   step lists, file paths, the chosen golden subset, the
   normalization layer's API).
3. Author `ai-assignment.md` via routed analysis.
4. **Pick the golden subset.** Per "In scope" item 3 above — full
   three-tier vs. `kb/ir` only vs. manifest-only. Decision criteria:
   determinism, repo-size budget (golden corpus of ~5-10MB per
   database is reasonable; ~100MB is not), regression visibility.
5. Design the manifest aggregator API.
6. Implement the aggregator + unit tests under
   `tests/AccessHarvester.Pipeline.Tests/`.
7. `dotnet build && dotnet test` sequentially.
8. End-of-session verification + commit + push + notify.

**Creates:**
- Manifest aggregator source files in `AccessHarvester.Pipeline/`
  (or `Core/`).
- Unit tests in `tests/AccessHarvester.Pipeline.Tests/`.
- `docs/session-sets/harvester-finish-canonical-golden-output/ai-assignment.md`
- `docs/session-sets/harvester-finish-canonical-golden-output/session-reviews/session-001.md`

**Touches:**
- `docs/session-sets/harvester-finish-canonical-golden-output/spec.md`
  (stub expansion).

**Ends with:** manifest aggregator class shipping with green tests;
not yet wired into `Pipeline.Run()`.

---

### Session 2 of 4: Pipeline Wiring + UnresolvedItems Emission

**Steps:**

1. Wire `Pipeline.Run()` to invoke the manifest aggregator and write
   `manifest.json` + `unresolved-items.json` (or combined) at the
   run root.
2. Add a `--manifest-only` or similar flag if the design benefits
   from one (Session 1's spec expansion decides).
3. Update `tests/AccessHarvester.Integration/Pipeline/EndToEndPipelineTests.cs`
   so the "ran pipeline produced X" assertions cover the manifest +
   UnresolvedItems files.
4. Schema-validation test: produced manifest validates against
   `harvester-schema.v1.0.json` via existing `SchemaValidator`
   (closes the prior set's Gap A).
5. `dotnet build && dotnet test` sequentially.
6. End-of-session verification + commit + push + notify.

**Creates:**
- The schema-conformance test under
  `tests/AccessHarvester.Integration/Pipeline/`.
- `docs/session-sets/harvester-finish-canonical-golden-output/session-reviews/session-002.md`

**Touches:**
- `AccessHarvester.Core/Pipeline.cs` (or `AccessHarvester.Cli/`
  if the wiring lives in the CLI layer — Session 1 decides).
- `AccessHarvester.Cli/Program.cs` for any new flag and
  `--json-status` envelope changes.

**Ends with:** the production pipeline writes manifest +
UnresolvedItems on every run; schema validation gate green.

---

### Session 3 of 4: Generate and Commit Golden Output

**Steps:**

1. Run `Pipeline.Default().Run(<fixture>, <staging-dir>)` for both
   `NorthwindDevEdition.accdb` and `HomeHealthCare.accdb`.
2. Apply the normalization layer (timestamps, absolute paths,
   machine-specific GUIDs).
3. Commit the normalized golden corpus at the chosen path
   (Session 1 spec-expansion picks: candidates
   `tests/.../GoldenOutput/<fixture>/` or
   `samples/GoldenOutput/<fixture>/`).
4. **Inspect for surprises.** If the golden surfaces clearly-bugged
   output (malformed IR, missing fields, wrong shape), STOP and
   surface to the human (Stop Rule "Golden surfaces real defect"
   below). Do NOT commit known-bad output as golden.
5. End-of-session verification + commit + push + notify.

**Creates:**
- The committed golden corpus at the chosen path.
- `docs/session-sets/harvester-finish-canonical-golden-output/session-reviews/session-003.md`

**Touches:**
- Nothing in source code unless a Session-2-introduced defect needs
  patching pre-commit.

**Ends with:** golden output committed for both canonical fixtures.

---

### Session 4 of 4: Regression Gate + Closeout

**Steps:**

1. Author the regression test that re-runs `Pipeline.Run()`,
   normalizes, and diffs against the committed golden. Lands in
   `tests/AccessHarvester.Integration/Pipeline/`, `[SkippableFact]`
   for hosts without Access.
2. Confirm CI loop: green on the canonical fixtures; clear failure
   message on drift.
3. `dotnet build && dotnet test` sequentially.
4. End-of-session verification.
5. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
6. Step 9 reorganization review AFTER notify.

**Creates:**
- The regression test class.
- `docs/session-sets/harvester-finish-canonical-golden-output/change-log.md`
- `docs/session-sets/harvester-finish-canonical-golden-output/disposition.json`
- `docs/session-sets/harvester-finish-canonical-golden-output/session-reviews/session-004.md`

**Ends with:** Phase 4 Criteria #3 and #5 closed with a CI gate;
golden output regression-locked; the manifest is the harvester's
shippable contract artifact.

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.30 per session (verification
  + occasional analysis on tricky normalization questions).
  ~$0.40–$0.80 across the set.
- **In-conversation token spend:** Substantial. Manifest aggregator
  design and normalization layer are synthesis-heavy.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Golden surfaces real defect** | Generated golden output shows malformed IR, missing fields, or shape inconsistent with the v1.0 schema | Stop. Surface to the human. Fix lands in the affected enricher's project as a tree-amendment to the originating session set; pin golden post-fix. Do NOT commit known-bad output as golden. |
| **Repo-size budget exceeded** | Chosen golden subset materializes to >100MB total for both fixtures | Stop. Surface to the human. Either narrow the subset (drop `raw/`, sample IR per category) or accept the size — but the call is the human's, not the orchestrator's. |
| **Normalization layer grows unbounded** | More than ~10 distinct fields require normalization (signals non-determinism in the pipeline itself) | Stop. Surface to the human. Non-determinism is a project-guidance Principle violation; the fix lives in the producing component, not in the harness. |
| **Schema-validation fails on legitimate output** | Produced manifest fails `SchemaValidator` against `harvester-schema.v1.0.json` and inspection confirms the manifest is correct | Stop. Tree-amendment to `schema-finalization-and-docs` to extend or relax the schema. Do not loosen the validator's strictness in this set. |

---

## Why this is a stub

The stub fixes the contract: manifest aggregator + production
wiring + golden corpus + regression gate + UnresolvedItems
emission. It deliberately leaves the API shape, golden-subset
decision, normalization layer design, and file paths for the
set's own Session 1.
