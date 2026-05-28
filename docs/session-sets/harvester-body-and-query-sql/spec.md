# Harvester Body And Query SQL Spec

> **Purpose:** Close v1-adr.md Deferred items D5, D6, and D14 by wiring
> the harvester's production `run` output to populate three currently-
> empty schema slots: `ProcedureItem.Body` (references, calls, SQL
> strings), `ProcedureItem.EventBinding`, and `QueryItem.Sql.Parsed` /
> `Sql.Parameters`. Once complete, the manifest that `AccessHarvester run`
> emits will contain the structured procedure and query data the downstream
> AI consumer needs to reason about form behavior and data access patterns
> without re-parsing raw VBA or SQL text.
> **Created:** 2026-05-28
> **Session Set:** `docs/session-sets/harvester-body-and-query-sql/`
> **Prerequisites:**
>   - `harvester-schema-v1-adr-followups` — complete (v1.0 schema operative,
>     fixture casing migrated, PredicateNode polymorphism fixed). Hard
>     prerequisite: the pipeline work here validates against v1.0.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: All output is JSON; correctness is verified by hermetic
> unit tests (projector logic) + schema-conformance tests (manifests
> produced by `run` against committed fixtures validate against
> harvester-schema.v1.0.json) + cross-provider AI verification.
> No human UAT layer exists for this repo.

---

## Project Overview

`AccessHarvester.Core/Pipeline.cs` `EmitManifestArtifacts()` currently
emits a minimal `DatabaseManifest` — a valid envelope with database
metadata but empty `Modules[]`, `Forms[]`, and `Queries[]` collections.
The comment in that method is candid: "Collection population grows in
follow-on sessions." This set is that follow-on.

Three schema slots from `v1-adr.md §Gaps Deferred to Post-v1.0` are
targeted:

| ADR item | Schema field | What it needs |
|---|---|---|
| **D5** | `ProcedureItem.Body.References`, `.Calls`, `.SqlStrings` | Resolver output projected into JSON per procedure |
| **D6** | `ProcedureItem.EventBinding.ControlName` + `.EventName` | Event handler name split (last underscore convention) |
| **D14** | `QueryItem.Sql.Parsed`, `QueryItem.Sql.Parameters` | Jet SQL parser output projected into `ParsedSql` shape |

### Why these three together

D5 and D6 are tightly coupled: both require running the VBA parser +
resolver chain on each module and projecting the result. D6 is a 10-line
extension of D5's projection pass. Separating them would be wasteful.

D14 requires the Jet SQL parser (not the VBA resolver), reads from a
different tier of the extraction output, and writes to a different
manifest collection. It is logically independent but fits naturally as
Session 2 of the same set since both sessions share the theme of
"wire the parser outputs the pipeline already computes into the manifest
format the schema defines."

### Current pipeline state (what exists)

- **VBA parsing + resolution:** `VbaParserPipeline.Parse()` →
  `ReferenceResolver.Resolve()` → `CrossReferenceIndex.Build()` →
  `FunctionClassificationBuilder.BuildAll()` all exist and are tested.
  They are NOT called from `EmitManifestArtifacts`.

- **Query SQL parsing:** `SqlParserPipeline.Parse()` exists. The
  `QueryAbstractionBuilder` walks the resulting AST and produces
  `QueryAbstraction` objects. The raw `ParsedSql` projection (for
  `QueryItem.Sql.Parsed`) is not yet built.

- **Manifest emission:** `DatabaseManifestAggregator.Build()` accepts
  `IEnumerable<ModuleExport>?`, `IEnumerable<FormExport>?`, and
  `IEnumerable<QueryAbstraction>?` among its optional parameters.
  The `Modules` and `Queries` parameters are currently passed as null.

### In scope

1. **ParserOutputProjector** (`AccessHarvester.Pipeline/Manifest/` or
   `AccessHarvester.Pipeline/Projection/`): a new class that takes a
   `ResolvedModule` + `CrossReferenceIndex` + `IReadOnlyList<FunctionClassification>`
   and produces the `JsonNode` that becomes `ModuleExport.ParserOutput`.
   Responsible for:
   - Walking each `ProcedureDeclaration` and projecting its resolved
     references into `Body.References` (JSON shape per the v1.0
     `ResolvedReference` schema def).
   - Extracting call sites (all `ResolvedReference` entries whose
     `Outcome` is `Resolved` and whose target is a procedure declaration)
     into `Body.Calls`.
   - Extracting SQL string sites from the unresolved-item aggregator's
     `DynamicSqlSite` entries into `Body.SqlStrings`.
   - Deriving `EventBinding` for event-handler procedures by splitting the
     procedure name on the last underscore
     (`Form_BeforeUpdate` → `{ControlName: "Form", EventName: "BeforeUpdate"}`).
   - Emitting `ProcedureKind`, `Visibility`, `IsStatic`, `Name`,
     `Signature.Parameters`, `Classification` (from `FunctionClassification`)
     for each procedure item.

2. **Pipeline wiring** (`AccessHarvester.Core/Pipeline.cs`):
   - For each VBA module found in the extraction output, run the parse →
     resolve → classify chain and invoke `ParserOutputProjector`.
   - Emit `ModuleExport` (for standalone modules) and populate
     `FormExport.ParserOutput` (for form/report code modules) with the
     projected output.
   - Pass the populated `modules` and updated `forms` collections to
     `DatabaseManifestAggregator.Build()`.

3. **QuerySqlProjector** (Session 2): a new class that takes a parsed
   `SqlStatement` + the query's declared parameters and produces the
   `JsonNode` for `QueryItem.Sql.Parsed` + `QueryItem.Sql.Parameters`,
   plus a `QueryItem` (ItemKind, Name, QueryType, Sql).

4. **Tests**:
   - Unit tests for `ParserOutputProjector`: given a synthetic
     `ResolvedModule`, assert the projected JSON has the expected
     `Body.References`, `Body.Calls`, and `EventBinding` shapes.
   - Unit tests for `QuerySqlProjector`: given a parsed SQL statement,
     assert `Sql.Parsed` has the expected Select / From / Where shape.
   - Schema-conformance integration tests: after Session 2 is complete,
     run `AccessHarvester run` against `samples/HomeHealthCare.accdb` and
     validate the emitted `manifest.json` against `harvester-schema.v1.0.json`.
     This extends `ManifestSchemaConformanceTests` (already in
     `AccessHarvester.Integration`).

### Out of scope

- `Body.Statements` (the formal statement list in the v1.0 schema). The
  v0 test fixtures show `Statements` as a structured list of `if`/`for`/
  `set` etc. nodes. The v1.0 schema's `Statement.Kind` enum is a
  high-level categorisation, but mapping the raw VBA AST to those
  categories requires a dedicated statement-classification pass not yet
  built. D5's `Statements` slot is deferred to a follow-on set; `Body`
  will have `References`, `Calls`, and `SqlStrings` populated but
  `Statements` will remain null.
- `QueryItem.Abstraction.ReturnShape` — column types require a resolved
  symbol table. The slot will exist (null) in the manifest; a future
  set populates it when symbol-table resolution is wired.
- `ExpressionItem` population (D15). Expression items for form control
  sources, validation rules, default values, etc. require reading the
  ir-tier form data and projecting expressions. A separate set.
- `ResolvedReference.ResolvedTarget` enrichment. The projector emits
  whatever the resolver already resolved; it does not attempt additional
  resolution of currently-unresolved targets.
- Changes to the VBA grammar, resolver, or SQL parser. The session
  produces a projection layer; the underlying parsers/resolvers are
  consumed as-is.

---

## Sessions

### Session 1 of 2: ParserOutputProjector + Module Wiring (D5 + D6)

**Steps:**

1. Read `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`, and this spec.
2. Read `AccessHarvester.Core/Pipeline.cs` `EmitManifestArtifacts()` to
   understand the current wiring seam. Read `ModuleExport.cs`,
   `FormExport.cs`, and `DatabaseManifestAggregator.cs` to understand
   the manifest assembly contract.
3. Route an architecture task: given the resolver output types
   (`ResolvedModule`, `CrossReferenceIndex`, `FunctionClassification`)
   and the v1.0 schema shape for `ProcedureItem.Body` + `EventBinding`,
   design the `ParserOutputProjector` interface and the wiring changes
   needed in `Pipeline.EmitManifestArtifacts`. Document the choice of
   module-discovery strategy (reading from `kb/` vs `raw/` tier vs
   re-running the extractor).
4. Implement `ParserOutputProjector`. Projection rules:
   - **Event binding (D6):** a procedure is an event handler if its
     `ProcedureKind` is `EventHandler` (per `FunctionClassification.Kind`)
     OR if its name matches `^[A-Za-z][A-Za-z0-9]*_[A-Za-z][A-Za-z0-9]*$`.
     Split on the last underscore: `Form_BeforeUpdate` →
     `{ControlName: "Form", EventName: "BeforeUpdate"}`.
   - **References (D5):** project each `ResolvedReference` in the
     resolved module (scoped to the current procedure by source range)
     into the `Body.References` JSON array per the v1.0
     `ResolvedReference` schema shape.
   - **Calls (D5):** filter references whose outcome is a procedure target
     and project into `Body.Calls` (CalleeText, Location, ResolvedCallee).
   - **SqlStrings (D5):** project `UnresolvedSource.DynamicSqlSite` items
     for this procedure from the `UnresolvedItemAggregator` into
     `Body.SqlStrings` (OriginalText, ConstructionMode, TemplateCandidates
     where available).
5. Wire `EmitManifestArtifacts` to:
   - Read module list from the extraction output.
   - Run parse → resolve → classify per module.
   - Invoke `ParserOutputProjector` per module.
   - Produce `ModuleExport` objects for standalone modules.
   - Produce `FormExport.ParserOutput` nodes for form/report modules
     (combine with the existing `FormComponentSpec` from `enrich.IrDir`
     if available, else emit `ParserOutput` only).
6. Unit tests for `ParserOutputProjector`:
   - Given a single-procedure `ResolvedModule` with one resolved
     reference: assert `Body.References[0]` shape is correct.
   - Given an event-handler procedure name `Form_BeforeUpdate`: assert
     `EventBinding` has `ControlName: "Form"`, `EventName: "BeforeUpdate"`.
   - Given an event-handler procedure name `btnSave_Click`: assert
     `EventBinding` has `ControlName: "btnSave"`, `EventName: "Click"`.
   - Given a non-event-handler procedure: assert `EventBinding` is null.
7. `dotnet build && dotnet test` sequentially.
8. End-of-session verification + commit + push + notify.

**Creates:**
- `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs`
  (or `Projection/ParserOutputProjector.cs`).
- Unit tests for `ParserOutputProjector`.
- `docs/session-sets/harvester-body-and-query-sql/session-reviews/session-001.md`

**Touches:**
- `AccessHarvester.Core/Pipeline.cs` (`EmitManifestArtifacts`).
- Possibly `AccessHarvester.Pipeline/Manifest/DatabaseManifestAggregator.cs`
  if the `Build()` parameter set needs extending.

**Ends with:** `AccessHarvester run` against `HomeHealthCare.accdb`
emits a `manifest.json` with `Modules[]` populated; each `ModuleExport`
has a `ParserOutput` whose `Body` contains non-null `References` and
`Calls`, and event-handler procedures have non-null `EventBinding`.

---

### Session 2 of 2: QuerySqlProjector + Query Wiring (D14)

**Steps:**

1. Read the Session 1 review and the D14 description in `v1-adr.md`.
2. Read `AccessHarvester.Core/Extraction/QueryExporter.cs` to understand
   what query data is available in the `raw/` or `kb/` tier.
3. Inspect `SqlParserPipeline.Parse()` and `QueryAbstractionBuilder.Build()`
   to understand what the Jet SQL parser produces and what is already
   projected into `QueryAbstraction`.
4. Route an architecture task: design `QuerySqlProjector` — the mapping
   from the parsed `SqlStatement` AST to the `ParsedSql` JSON shape
   (`Select[]`, `From[]`, `Joins[]`, `Where`, `GroupBy[]`, `Having`,
   `OrderBy[]`, `Raw`). Document the handling of queries whose SQL text
   fails to parse cleanly (emit `Raw` only, null structured fields).
5. Implement `QuerySqlProjector`. Projection rules:
   - `ParsedSql.Raw` is always the original SQL text (fallback for
     consumers that don't read the structured fields).
   - `ParsedSql.Select` — project the SELECT list items as
     `{Name, Expression, IsAggregate}` objects.
   - `ParsedSql.From` — project the primary table source(s).
   - `ParsedSql.Joins` — project JOIN descriptors (Kind, RightTable, OnExpression).
   - `ParsedSql.Where` — the WHERE clause serialised as a `PredicateNode`
     tree (reuse `QueryAbstractionBuilder`'s predicate visitor; the
     `[JsonPolymorphic]` fix from `harvester-schema-v1-adr-followups`
     makes this round-trip safely).
   - `QueryItem.Sql.Parameters` — project from the `PARAMETERS` clause
     (declared parameters) and any implicit form/report references found
     in the body, per the `ParserQueryParameter` schema shape.
   - `QueryItem.Abstraction` — emit `SuggestedMethodName` and
     `SuggestedParameters` from `QueryAbstractionBuilder`. Leave
     `ReturnShape` null (requires resolved symbol table).
6. Wire `EmitManifestArtifacts` to:
   - Read saved queries from the extraction output.
   - Parse each query's SQL with `SqlParserPipeline`.
   - Invoke `QuerySqlProjector` to produce the `QueryItem` JSON.
   - Pass the `queries` collection to `DatabaseManifestAggregator.Build()`.
7. Unit tests for `QuerySqlProjector`:
   - A simple `SELECT a FROM b WHERE c = 1` produces the expected
     `Select[]`, `From[]`, and `Where` predicate tree.
   - A query with `PARAMETERS [Enter ID] Long;` produces a
     `Sql.Parameters[0]` with `Name: "enterId"`, `Source: "NamedParameter"`.
   - A query whose SQL fails to parse produces `{Raw: "<text>"}` and
     null structured fields (no exception thrown).
8. Schema-conformance integration test: run `AccessHarvester run` against
   `HomeHealthCare.accdb`, load the emitted `manifest.json`, validate
   against `harvester-schema.v1.0.json` via `SchemaValidator`. This test
   lives in `ManifestSchemaConformanceTests` (extend the existing
   `PipelineRunProducedManifest_ValidatesAgainstSchema` test or add a
   peer `HomeHealthCare` variant).
9. `dotnet build && dotnet test` sequentially.
10. End-of-session verification.
11. Closeout: `disposition.json`, `change-log.md`,
    `next-session-set-recommendation.md`, commit + push +
    `mark_session_complete()` + notify.

**Creates:**
- `AccessHarvester.Pipeline/Manifest/QuerySqlProjector.cs`
  (or `Projection/QuerySqlProjector.cs`).
- Unit tests for `QuerySqlProjector`.
- Schema-conformance integration test for full `run` manifest.
- `docs/session-sets/harvester-body-and-query-sql/session-reviews/session-002.md`
- `docs/session-sets/harvester-body-and-query-sql/change-log.md`
- `docs/session-sets/harvester-body-and-query-sql/disposition.json`

**Touches:**
- `AccessHarvester.Core/Pipeline.cs` (`EmitManifestArtifacts` — add
  query wiring).
- `tests/AccessHarvester.Integration/Pipeline/ManifestSchemaConformanceTests.cs`.

**Ends with:** `AccessHarvester run` against `HomeHealthCare.accdb`
emits a `manifest.json` with `Queries[]` populated; each `QueryItem`
has `Sql.Parsed` containing at minimum a `Raw` fallback text and
structured fields for any query the Jet SQL parser handles cleanly.
The full manifest (Modules + Queries now populated) validates against
harvester-schema.v1.0.json.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **VBA module list not discoverable from extraction output** | The `raw/` or `kb/` tier does not have a reliable, enumerable list of VBA module paths that can be resolved to `.bas`/`.cls` source text | Stop. Surface to human. Pivot: add a module-index file to the extractor's output before attempting projection. |
| **Resolver produces more than N unresolved references per module where N makes output noisy** | A sample `run` shows that modules have O(100+) unresolved references, flooding `Body.References` with low-value entries | Route a consensus decision: should the projector filter out `Unresolved` references from `Body.References` (emitting only resolved ones) or include them all? Default: include all (mirrors what the test fixtures show). |
| **SQL parse failure rate is too high** | More than ~30% of saved queries in a real-world sample (`HomeHealthCare.accdb`, `NorthwindDevEdition.accdb`) produce only `Raw` with no structured fields | Stop. Investigate whether the Jet SQL parser needs grammar fixes before the projection layer makes sense. Do not ship projection logic that silently produces mostly-empty output. |
| **Wiring `EmitManifestArtifacts` requires Access COM** | The VBA module text is only available via COM extraction (not committed in `raw/`) | The VBA source text IS committed by `SaveAsTextExporter` into `raw/`. If this assumption is wrong for a specific database, stop and surface to human before attempting to run the VBA parser inline. |

---

## Why now

The test fixtures (`docs/test-cases/*.expected.json`) already document
what populated `Body.References`, `Body.Calls`, and `EventBinding` look
like — they were authored by hand as the reference contract. The
downstream consumer spec (`Dabbler.NorthwindToBlazor`) depends on
reading procedure body information from the manifest to understand form
event handling and data access patterns. Shipping a manifest that has
only the envelope (no module or query content) means the downstream AI
is operating blind on the most information-dense part of the Access
application. This set closes that gap.

The `Body.Statements` field (raw AST statement list) is intentionally
left out of scope for the reason stated in "Out of scope": converting
the VBA AST to the `Statement.Kind` enum requires a classification pass
that is its own project. The consumer can access the verbatim VBA source
via `ProcedureItem` once the body fields are wired; `Body.References`
and `Body.Calls` carry the resolved semantic graph.
