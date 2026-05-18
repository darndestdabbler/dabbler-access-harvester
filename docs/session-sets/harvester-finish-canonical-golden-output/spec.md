# Canonical Golden Output Spec

> **Status:** ELABORATED 2026-05-18 by Session 1 (was STUB authored
> 2026-05-17 by `remaining-work-planner` Session 2). Session 1's
> elaboration locks the aggregator API, golden subset, normalization
> layer design, and per-session step lists. This is **the central
> Phase 4 deliverable** — without it, Phase 4 has no end-to-end gate.
>
> **Purpose:** For each canonical fixture, commit a `DatabaseManifest`
> + per-object IR golden output and a regression test that re-runs
> the harvester and asserts byte-equality (with a documented
> normalization layer for the few legitimately noisy fields).
> Closes Phase 4 Criterion #3 ("golden output committed for each
> canonical fixture; CI/test suite verifies the harvester reproduces
> that golden output") AND Criterion #5 ("per-run report listing
> every `UnresolvedItem` with structured fields") by emitting a
> top-level `unresolved-items.json` from the production pipeline.
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
`Reports[]`, `Queries[]`, `Modules[]`, `Tables[]`,
`Relationships[]`, and (per GAP #5) an `UnresolvedItems[]` array.
**Today no component aggregates the per-object IR files into that
manifest, and no production code path writes the `UnresolvedItems`
array to disk** — the aggregator is invoked only from hermetic tests.

This set:

1. **Builds the manifest aggregator** in
   `AccessHarvester.Pipeline/Manifest/` as a pure in-memory
   assembly function (`DatabaseManifestAggregator.Build(...)`).
   It does **not** read IR files or perform any I/O; callers supply
   per-category in-memory inputs (extractors' metadata, enrichers'
   spec/abstraction records, the existing `UnresolvedItemAggregator`
   output) and the aggregator returns one immutable
   `DatabaseManifest` record. See *Design decisions (Session 1,
   2026-05-18)* below for the locked API.
2. **Wires `Pipeline.Run()` to emit the manifest + UnresolvedItems**
   — Session 2's job. Run assembles the aggregator's inputs and
   serialises the returned manifest plus a sibling
   `unresolved-items.json` to the run root.
3. **Generates and commits the golden output** for both canonical
   fixtures.
4. **Adds the regression test** that re-runs `Pipeline.Run()` and
   diffs against the committed golden, with a documented
   normalization layer.

### Design decisions (Session 1, 2026-05-18)

Session 1 settled the four decisions the stub deferred. Each was
chosen to preserve determinism and minimize schema-vs-implementation
divergence; rationale recorded inline so subsequent sessions (and
future readers) do not relitigate.

**Decision 1 — Aggregator API.** `DatabaseManifestAggregator.Build(...)`
is a static method that mirrors `UnresolvedItemAggregator.Build(...)`
verbatim in shape and intent: it takes a flat set of optional
per-category in-memory inputs and returns one immutable
`DatabaseManifest` record. The aggregator is **pure**: no I/O, no
COM, no Access; callers compose their own input set. This keeps the
aggregator unit-testable against synthetic inputs and keeps the
Session 2 wiring (in `Pipeline.Run()`) thin — Run assembles the
parts and calls Build.

The full signature is captured in the *Appendix A — Aggregator API*
section below.

**Decision 2 — Manifest record types.** Six new manifest-envelope
record types land in `AccessHarvester.Pipeline/Manifest/`:

- `DatabaseManifest` — root document with `SchemaType:
  "DatabaseManifest"` and `SchemaVersion: "1.0"` constants enforced
  at construction time.
- `DatabaseInfo` — `Name`, `FilePath`, `ExportedAt` (ISO-8601).
- `DatabaseSummary` — counts derived from the populated arrays.
- `FormExport` — wraps an existing `FormComponentSpec` with optional
  `ParserOutput`.
- `ModuleExport` — `ModuleName`, `ModuleKind`, optional
  `ParserOutput`.
- `HarvesterDiagnostic` — `Severity`, `Message`, optional `Code`.

The per-category collections (`Tables[]`, `Relationships[]`,
`Queries[]`, `FunctionClassifications[]`, etc.) carry the
**existing in-memory record types** from
`AccessHarvester.Access` / `AccessHarvester.Pipeline.Enrichment` /
`AccessHarvester.Vba.Parser` — they are not re-modelled. JSON
property-name conformance against `harvester-schema.v1.0.json` is
a Session 2 concern, gated by the schema-validation test the
spec calls out for that session. If any in-memory type produces
JSON whose shape diverges from the schema, the fix is a tree
amendment to `schema-finalization-and-docs` (extend the schema or
adjust the type), not a new projection layer.

**Decision 3 — Golden subset.** The Session 3 commit-corpus is
**`kb/` + `ir/` + `manifest.json` + `unresolved-items.json`** for
each canonical fixture. The `raw/` tier is **excluded** from the
golden corpus because SaveAsText output carries Access-mutated
timestamps that fail byte-equality without aggressive
normalization. The `kb/` tier captures the deterministic parse of
the raw output; the `ir/` tier captures per-object enrichment; the
manifest captures the rolled-up shape that downstream consumers
ingest; the separate `unresolved-items.json` gives manual-review
tooling a single file to pull without re-parsing the manifest.

Size budget: ~5-10MB per fixture (Northwind has more objects than
HomeHealthCare); ~15-20MB total across both fixtures. Well under
the 100MB stop-rule trigger.

Golden path: **`samples/GoldenOutput/<fixture-name>/`** at the
repo root, sibling to the existing `samples/DatabaseYaml/` mirror.
Placing it under `samples/` (not `tests/`) keeps the data near the
fixtures it derives from and makes the golden corpus discoverable
to humans inspecting the harvester's output without needing to dig
into test-project conventions.

**Decision 4 — Normalization layer.** Session 3 lands a
`GoldenNormalizer` static class in
`tests/AccessHarvester.Integration/Pipeline/GoldenNormalizer.cs`
so the same normalization that produces the committed corpus is
the normalization the Session 4 regression test runs against
re-generated output. The static class has three entry points:

- `NormalizeManifest(string json) -> string` — replaces volatile
  fields in the manifest envelope (`Database.ExportedAt`,
  `Database.FilePath`, `HarvesterVersion`) with stable
  placeholders and re-emits the JSON with
  `JsonSerializerOptions { WriteIndented = true }` for
  deterministic whitespace.
- `NormalizeUnresolvedItems(string json) -> string` — operates on
  the top-level `unresolved-items.json` (a bare
  `UnresolvedItem[]` array, NOT a manifest envelope). No volatile
  fields are known today, so the V1 body is a re-serialise
  pass-through (parse → re-emit with the same whitespace options)
  that preserves the array contract. The method is the named
  contract Session 4 calls; the body extends if Session 3's
  golden surfaces any drift here.
- `NormalizePerObjectIr(string json) -> string` — placeholder-pass
  today; extends in-place if Session 3's golden surfaces any
  per-object timestamp or absolute-path leakage. Default is no-op
  pending evidence.

The normalizer is intentionally narrow: the **only** volatile
fields known today are the three on the manifest envelope. Any
field that needs normalization beyond those three is a
determinism defect in the producing component (per the
project-guidance Principle: "the parser must produce structured
output that is deterministic given the same input") and triggers
Stop Rule "Normalization layer grows unbounded."

### Why GAP #5 folds into this set

GAP #5 (per-run UnresolvedItems report) and GAP #3 (canonical golden
output) share a single insertion point: the production pipeline's
emission stage. GAP #5's `unresolved-items.json` is one of the files
GAP #3 needs to capture in the golden corpus. Implementing #5 first
and then #3 would force two passes over the same code; implementing
#3 without #5's emission would commit golden output that does not
exercise the unresolved-aggregation pathway at all. Folding them is
the cheaper, more coherent path. The decision is recorded here for
traceability; the planner's `remaining-work-tree.md` notes the
fold-in alongside its row for this set.

### In scope

1. **The manifest-aggregator component.** A pure static method
   `DatabaseManifestAggregator.Build(...)` in
   `AccessHarvester.Pipeline/Manifest/`, mirroring
   `UnresolvedItemAggregator.Build(...)` (Session 1 lock — see
   *Design decisions (Session 1, 2026-05-18)* and *Appendix A*
   for the locked signature and contract).
2. **`Pipeline.Run()` wiring** (Session 2). Run assembles the
   aggregator's inputs after the enrich phase, calls Build, and
   serialises the returned manifest to
   `<runRoot>/AccessMetaData/<dbName>/manifest.json` plus a
   companion `<runRoot>/AccessMetaData/<dbName>/unresolved-items.json`
   that carries the same `UnresolvedItems[]` independently — two
   files (not combined), serialised from one shared in-memory
   list. The shape rationale is in Decision 1.
3. **Committed golden output** for `NorthwindDevEdition.accdb`
   and `HomeHealthCare.accdb`. Session 1 lock: **`kb/` + `ir/` +
   `manifest.json` + `unresolved-items.json`** (`raw/` excluded —
   timestamp noise). See Decision 3 above for the rationale and
   path layout (`samples/GoldenOutput/<fixture-name>/`).
4. **A normalization layer** for legitimately noisy fields
   (timestamps, absolute paths). Session 1 lock: a
   `GoldenNormalizer` static class lands in Session 3 (so the
   same normalization that produces the committed corpus is the
   one the Session 4 regression test runs against re-generated
   output). See Decision 4 above for the API and the
   deferred-until-evidence policy for extending it.
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
   normalization layer's API). See *Design decisions (Session 1,
   2026-05-18)* above for the locked-in choices.
3. Author `ai-assignment.md` via routed analysis.
4. Land the six manifest-envelope record types under
   `AccessHarvester.Pipeline/Manifest/`:
   `DatabaseManifest.cs`, `DatabaseInfo.cs`, `DatabaseSummary.cs`,
   `FormExport.cs`, `ModuleExport.cs`, `HarvesterDiagnostic.cs`.
5. Land `AccessHarvester.Pipeline/Manifest/DatabaseManifestAggregator.cs`
   with the static `Build(...)` API (signature in *Appendix A*).
6. Land unit tests at
   `tests/AccessHarvester.Pipeline.Tests/Manifest/DatabaseManifestAggregatorTests.cs`
   covering: empty inputs produce a minimal valid manifest;
   counts in `DatabaseSummary` reflect populated arrays;
   per-category arrays passthrough unchanged; `SchemaType` /
   `SchemaVersion` constants are enforced; deterministic ordering
   is preserved across re-runs.
7. `dotnet build && dotnet test` sequentially.
8. End-of-session verification + commit + push + notify.

**Creates:**
- `AccessHarvester.Pipeline/Manifest/DatabaseManifest.cs`
- `AccessHarvester.Pipeline/Manifest/DatabaseInfo.cs`
- `AccessHarvester.Pipeline/Manifest/DatabaseSummary.cs`
- `AccessHarvester.Pipeline/Manifest/FormExport.cs`
- `AccessHarvester.Pipeline/Manifest/ModuleExport.cs`
- `AccessHarvester.Pipeline/Manifest/HarvesterDiagnostic.cs`
- `AccessHarvester.Pipeline/Manifest/DatabaseManifestAggregator.cs`
- `tests/AccessHarvester.Pipeline.Tests/Manifest/DatabaseManifestAggregatorTests.cs`
- `docs/session-sets/harvester-finish-canonical-golden-output/ai-assignment.md`
- `docs/session-sets/harvester-finish-canonical-golden-output/session-reviews/session-001.md`

**Touches:**
- `docs/session-sets/harvester-finish-canonical-golden-output/spec.md`
  (stub expansion).

**Ends with:** manifest aggregator class + record types shipping
with green tests; not yet wired into `Pipeline.Run()`.

---

### Session 2 of 4: Pipeline Wiring + UnresolvedItems Emission

**Steps:**

1. Wire `AccessHarvester.Core/Pipeline.Run()` to invoke
   `DatabaseManifestAggregator.Build(...)` after the enrich phase
   and write `manifest.json` and `unresolved-items.json` separately
   at `<runRoot>/AccessMetaData/<dbName>/`. Two files (not
   combined): the manifest carries the same `UnresolvedItems[]`
   inline, but downstream tooling that just wants the manual-review
   queue should not have to deserialise the whole manifest. The
   shared array is built once and serialised twice.
2. No new CLI flag is required for V1 — the manifest is always
   emitted when `run` completes. If a `--manifest-only` mode
   surfaces value during Session 2, defer to a follow-up set.
3. Update `tests/AccessHarvester.Integration/Pipeline/EndToEndPipelineTests.cs`
   so the "ran pipeline produced X" assertions cover the manifest +
   UnresolvedItems files.
4. Schema-validation test: produced manifest validates against
   `harvester-schema.v1.0.json` via existing `SchemaValidator`
   (closes the prior set's Gap A). Any in-memory-type-vs-schema
   shape mismatch surfaces here — the fix is a tree amendment to
   `schema-finalization-and-docs`, not a projection layer here.
5. `dotnet build && dotnet test` sequentially.
6. End-of-session verification + commit + push + notify.

**Creates:**
- `tests/AccessHarvester.Integration/Pipeline/ManifestSchemaConformanceTests.cs`
- `docs/session-sets/harvester-finish-canonical-golden-output/session-reviews/session-002.md`

**Touches:**
- `AccessHarvester.Core/Pipeline.cs` (wires the aggregator in
  after `Enrich(...)`).
- `tests/AccessHarvester.Integration/Pipeline/EndToEndPipelineTests.cs`
  (adds manifest + unresolved-items.json assertions).
- The Core project may need a `ProjectReference` to
  `AccessHarvester.Pipeline` if it does not already have one
  (Session 2 verifies and adds if missing).

**Ends with:** the production pipeline writes
`manifest.json` + `unresolved-items.json` on every run; schema
validation gate green; Gap A closed.

---

### Session 3 of 4: Generate and Commit Golden Output

**Steps:**

1. Run `Pipeline.Default().Run(<fixture>, <staging-dir>)` for both
   `NorthwindDevEdition.accdb` and `HomeHealthCare.accdb`.
2. Land `GoldenNormalizer` at
   `tests/AccessHarvester.Integration/Pipeline/GoldenNormalizer.cs`
   per Decision 4 (three entry points: `NormalizeManifest`,
   `NormalizeUnresolvedItems`, `NormalizePerObjectIr`). The
   normalizer must exist before Session 3 can apply it; landing
   it here keeps the corpus-producing and corpus-regenerating
   sides identical.
3. Apply the normalization layer: `NormalizeManifest` on
   `manifest.json`, `NormalizeUnresolvedItems` on
   `unresolved-items.json`, per-object IR files copied verbatim
   from staging (default `NormalizePerObjectIr` is no-op) unless
   evidence of drift appears, in which case the relevant entry
   point is extended.
4. Commit the normalized golden corpus at
   `samples/GoldenOutput/NorthwindDevEdition/` and
   `samples/GoldenOutput/HomeHealthCare/`. Per Decision 3, the
   corpus contains `kb/`, `ir/`, `manifest.json`, and
   `unresolved-items.json`. `raw/` is excluded.
5. **Inspect for surprises.** If the golden surfaces clearly-bugged
   output (malformed IR, missing fields, wrong shape), STOP and
   surface to the human (Stop Rule "Golden surfaces real defect"
   below). Do NOT commit known-bad output as golden.
6. End-of-session verification + commit + push + notify.

**Creates:**
- `tests/AccessHarvester.Integration/Pipeline/GoldenNormalizer.cs`
- `samples/GoldenOutput/NorthwindDevEdition/{kb,ir}/...` plus
  top-level `manifest.json` and `unresolved-items.json`.
- `samples/GoldenOutput/HomeHealthCare/{kb,ir}/...` plus
  top-level `manifest.json` and `unresolved-items.json`.
- `docs/session-sets/harvester-finish-canonical-golden-output/session-reviews/session-003.md`

**Touches:**
- Nothing in source code unless a Session-2-introduced defect needs
  patching pre-commit.

**Ends with:** `GoldenNormalizer` landed; golden output committed
for both canonical fixtures.

---

### Session 4 of 4: Regression Gate + Closeout

**Steps:**

1. Author the regression test
   `tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs`
   that, for each canonical fixture: runs
   `Pipeline.Default().Run(...)` against a temp staging dir,
   normalizes the produced manifest + unresolved-items.json via
   the Session-3 `GoldenNormalizer`, diffs each normalized output
   against the committed `samples/GoldenOutput/<fixture>/`
   corpus, and fails with a clear "expected vs. actual" message
   on drift. `[SkippableFact]` gates on Microsoft Access
   availability per the `Platform And Runtime` principle.
2. Confirm CI loop: green on the canonical fixtures; clear failure
   message on drift.
3. `dotnet build && dotnet test` sequentially.
4. End-of-session verification.
5. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
6. Run the workflow doc's Step 9 reorganization review AFTER
   notify (review `docs/planning/project-guidance.md` and
   `docs/planning/lessons-learned.md`; propose moves; apply
   accepted proposals in a separate follow-up commit).

**Creates:**
- `tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs`
- `docs/session-sets/harvester-finish-canonical-golden-output/change-log.md`
- `docs/session-sets/harvester-finish-canonical-golden-output/disposition.json`
- `docs/session-sets/harvester-finish-canonical-golden-output/next-session-set-recommendation.md`
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

## Appendix A — Aggregator API (Session 1 lock)

`AccessHarvester.Pipeline.Manifest.DatabaseManifestAggregator` is
a static class with one public entry point. Every input is
optional (default `null`); the aggregator skips missing inputs
and produces a minimal valid manifest from whatever is supplied.
This matches the existing `UnresolvedItemAggregator.Build(...)`
contract.

```csharp
public static DatabaseManifest Build(
    DatabaseInfo database,
    string harvesterVersion,
    IEnumerable<FormExport>?               forms                       = null,
    IEnumerable<FormExport>?               reports                     = null,
    IEnumerable<QueryAbstraction>?         queries                     = null,
    IEnumerable<ModuleExport>?             modules                     = null,
    IEnumerable<TableMetadata>?            tables                      = null,
    IEnumerable<RelationshipMetadata>?     relationships               = null,
    IEnumerable<ValidationRule>?           validationRules             = null,
    IEnumerable<RowSourceSpec>?            rowSourceSpecs              = null,
    IEnumerable<FunctionClassification>?   functionClassifications     = null,
    IEnumerable<EventHandlerClassification>? eventHandlerClassifications = null,
    NavigationGraph?                       navigationGraph             = null,
    IEnumerable<UnresolvedItem>?           unresolvedItems             = null,
    IEnumerable<HarvesterDiagnostic>?      diagnostics                 = null);
```

Contract:

- **Required inputs:** `database` and `harvesterVersion`. The
  schema's `DatabaseManifest.required` is
  `["SchemaType", "SchemaVersion", "HarvesterVersion", "Database"]`;
  `SchemaType` and `SchemaVersion` are constants the aggregator
  emits ("DatabaseManifest" and "1.0" respectively). The other
  two come from the caller.
- **All collection inputs:** treated as null-or-empty equivalent.
  An empty collection produces an empty array in the manifest
  (the schema permits zero-length arrays); a null collection
  produces an empty array too — the manifest never has a
  collection slot set to JSON `null`.
- **Determinism:** the aggregator does **not** re-sort the inputs.
  Callers pass already-deterministic ordering; the aggregator
  preserves it verbatim. (`UnresolvedItemAggregator` does its own
  multi-key sort because it merges from multiple sources;
  `DatabaseManifestAggregator` merges nothing — each input is a
  single source — so the responsibility is the caller's.)
- **Summary:** `DatabaseSummary` is computed from the populated
  collections — `FormCount = forms?.Count() ?? 0`, etc. — so the
  manifest is internally consistent.
- **No I/O:** the aggregator does not read or write files. Callers
  serialize the returned `DatabaseManifest` themselves.

## Why this was a stub

The stub fixed the contract: manifest aggregator + production
wiring + golden corpus + regression gate + UnresolvedItems
emission. Session 1 (2026-05-18) elaborated the API shape,
golden-subset decision, normalization layer design, and file
paths — captured in *Design decisions (Session 1, 2026-05-18)*
and *Appendix A* above. The remaining decisions are
implementation work and surface in Sessions 2-4.
