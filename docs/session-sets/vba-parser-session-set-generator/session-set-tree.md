# Session-Set Tree -- VBA Parser Migration Plan Decomposition

> **Authored:** 2026-04-25 by Session 2 of `vba-parser-session-set-generator`.
> Round 2 incorporates verifier findings F1-F5 from `session-reviews/session-002.md`.
> **Source plan:** `docs/VBA-Parser-Migration-Harvester-Plan.md` (893 lines, 9 phases).
> **Source retrospective:** `docs/session-sets/vba-parser-session-set-generator/harvester-import-retrospective.md`.
> **Synthesis posture:** Decomposition produced in-conversation by an Opus 4.7 orchestrator, per `project-guidance.md` Convention "When the orchestrator's model matches the routing target...". The `planning` task type pins to Opus in `router-config.yaml.task_type_overrides`.
> **Authority:** This document is the canonical source of truth for child session-set slugs (table column 2), plan-phase coverage (column 3), prerequisite child slugs (column 4), session counts (column 5), and output paths (column 7). Stub specs (Session 3) reference these strings verbatim; the routing playbook (Session 4) reads from the table. (Retrospective adopt-now `#2`.)

## Adopt-now rows applied

This decomposition explicitly applies the three Session 2 adopt-now rows from the retrospective:

- **`#1` -- Many small mechanical sessions over fewer large synthesis sessions.** Each child set below caps synthesis sessions at ≤2 of its 3-8. Phases the original Appendix E grouped into one Claude-Code session (e.g. "Resolver sessions: declarations + scope + reference + with-block + default + type + xref" as 4 sessions) are split across multiple child sets so each child has at most 2 design-heavy sessions.
- **`#2` -- The tree is the single source of truth.** Child slugs (column 2), plan-phase coverage (column 3), prerequisite slugs (column 4), and output paths (column 7) are the strings stub specs and the routing playbook must reference verbatim. No silent renames in later sessions. Output paths are committed (no `OR` alternatives left to later sessions).
- **`#3` -- Enumerated values, not free-text "depends on outcome."** Every dependency is a concrete child slug or `accessexportertemp1-import`. Every session count is a concrete range (e.g., `4-5`), not "to be determined."

## Test-case coverage table

The 12 hand-curated test cases in `docs/test-cases/` are mapped to plan phases below. Phases without a fixture entry require the responsible child set to author one before executing the work; that obligation is recorded both here and in the responsible child set's row.

| Plan phase / area | Test-case fixtures | Gap-resolution owner (and what they author) |
|---|---|---|
| 0.1 representative corpus (10-20 .accdb files) | `samples/NorthwindDevEdition.accdb` (committed by `accessexportertemp1-import`) + module-level fixtures `01-12` | **Out of scope for this tree per Open Question 1, recommendation (a).** Module-level fixtures + Northwind are sufficient for child sets #1-#9; database-level corpus expansion is folded into `#10 access-metadata-extraction` (which forces it) and `#18 integration-testing-and-acceptance` (regression suite expansion). No standalone "expand the .accdb corpus" child set is created. |
| 1.1-1.3 spec study, coverage matrix, hazard list | All 12 cases inform the hazard list; none are direct study fixtures | None (spec is the input; cases are evidence the hazard list crosschecks) |
| 1.4 lexical grammar | `01`, `07` (line numbers, type-decl chars) | None |
| 1.4 expression grammar | `02` (bang, member chains), `03` (`.`/`!`), `06` (Nz/IIf/Switch in non-VBA contexts) | None |
| 1.4 statement grammar | `01`, `05`, `07` (GoSub/Return), `09`, `11`, `12` | None |
| 1.4 module grammar | `10` (class, Implements, WithEvents, Property triplet, Initialize/Terminate) | None |
| 1.5-1.6 grammar validation + edge cases | All 12 + edge-case fixtures authored under `docs/test-cases/edge/` by #5 | `#5 vba-grammar-validation` authors edge-case fixtures driven by `#2`'s hazard list. The plan's "≥95% on representative corpus" target is reframed as "100% on the 12 module-level cases plus all hazard-list edge fixtures"; full .accdb corpus validation is deferred to `#18` per OQ 1(a). |
| 2.1-2.5 parser infrastructure | All 12 (regression-suitable) | `#6 parser-infrastructure` authors a dedicated preprocessor-directive fixture (`docs/test-cases/13-preprocessor.bas` + `13-preprocessor.expected.json`) before exercising 2.5. This is the only parser-infrastructure-level fixture the existing 12 cases do not cover. |
| 3.1-3.2 declarations + scope tree | `01`, `02`, `09`, `10`, `11`, `12` | None |
| 3.3-3.4 references + with-blocks | `02`, `03`, `09` | None |
| 3.5-3.7 default members + type inference + xref | `02`, `03`, `09`, `11` | None |
| 4.1-4.3 form/report/query/table metadata extraction | None directly -- module-level cases assume forms/controls exist but do not provide `.accdb` fixtures | `#10 access-metadata-extraction` authors database-level `.accdb` fixtures via the existing `test-builder/` Python tool (committed by `accessexportertemp1-import`); minimum coverage = one fixture per case 01/02/03/09/10/11/12 that has a form-or-report dependency. |
| 4.4-4.5 symbol-table integration + type library | `01`, `02`, `03` (require form metadata to resolve fully) | Inherits fixtures from `#10` |
| 5.1-5.2 JET SQL grammar + infrastructure | `04` (PARAMETERS, joins, IIf, date math), `05` (dynamic SQL fragments embedded in VBA strings) | None |
| 5.3 form-reference detection in SQL | `04` (`[Forms]![X]![Y]` patterns), `05` | None |
| 5.4 dynamic SQL analysis | `05` (canonical case, 32 possible forms enumerated as 5 templates) | None. (Removed `08` from this row -- `08` is `Eval()` / dynamic form name / `Application.Run` per the test-case README, which is graceful-failure / runtime-expression handling, NOT SQL string construction; `08` belongs to row 6.8 only.) |
| 6.1 query abstraction | `04` | None |
| 6.2 form component spec | `02`, `12`; full form-spec generation requires `.accdb` fixtures from `#10` | Inherits fixtures from `#10` |
| 6.3 event handler classification | `01` (validation), `02` (cascading), `09` (report events), `12` (switchboard navigation) | None |
| 6.4 validation rule extraction | `01`, `06` | None |
| 6.5 RowSource specification | `02` (combo box RowSource) | None |
| 6.6 VBA function classification | `07` (legacy mixed-return), `10` (audit-logger class) | None |
| 6.7 navigation/workflow extraction | `11` (AutoExec startup chain), `12` (switchboard) | None |
| 6.8 manual review routing | `07`, `08` | None |
| 7.1-7.4 schema finalization, examples, validation, doc generation | All 12 (each `.expected.json` already validates against `harvester-schema.v0.json`) | None |
| 8.1-8.5 unit/integration/regression/perf/acceptance | All 12 + DB-level fixtures from `#10` + edge fixtures from `#5` + preprocessor fixture from `#6` | `#18 integration-testing-and-acceptance` performs the OQ 1(a)-deferred .accdb corpus expansion: target 5-10 additional real-migration-scope .accdb files added under `samples/` for the regression suite. Below the plan's 10-20 target, but adequate for acceptance criteria; full target is post-v1 follow-up. |

**Net additional fixture work owned across the tree:**
1. `#5 vba-grammar-validation` -- edge-case fixtures driven by hazard list (count determined by `#2`'s output).
2. `#6 parser-infrastructure` -- preprocessor-directive fixture (one paired source/expected file).
3. `#10 access-metadata-extraction` -- database-level `.accdb` fixtures per `test-builder/` (≥7 fixtures covering form/report-bearing cases).
4. `#18 integration-testing-and-acceptance` -- 5-10 real-migration-scope `.accdb` files for the regression suite (deferred Phase 0.1 corpus expansion).

## Open questions for the human

These are genuine forks the spec does not resolve. Per retrospective adopt-now `#5`, kept to forks that actually block progress.

1. **Phase 0.1 corpus shape.** The plan calls for 10-20 *database-level* fixtures from real migration-scope databases. We have 12 *module-level* hand-curated cases plus Northwind committed by `accessexportertemp1-import`. Two viable paths:
   - **(a)** Treat module-level fixtures as primary for Phases 1-3; build database-level fixtures only for Phase 4 (`#10`, where they're forced by metadata extraction); and expand the .accdb corpus in `#18` for the regression suite. **Default applied.**
   - **(b)** Author a database-level corpus up front in a dedicated `parser-corpus-expansion` child set so Phase 1.5 grammar validation can hit ≥95% on a real corpus, not just on 12 cases.
   - **Recommendation:** **(a).** Defers cost into Phase 4 where it's unavoidable anyway, and the 12 module-level cases are sufficient for grammar/resolver validation. The tree above implements (a); selecting (b) requires inserting a new `parser-corpus-expansion` child set as a peer of `#1` and `#2`.
2. **Project namespace reconciliation.** The plan's project layout uses `Harvester.*` (e.g. `Harvester.Vba.Grammar/`); `accessexportertemp1-import` established `AccessHarvester.*` (e.g. `AccessHarvester.Cli`, `AccessHarvester.Core`). **Default applied:** extend the existing `AccessHarvester.*` namespace per `project-guidance.md` Conventions → Code Style. All output paths in the table below use `AccessHarvester.Vba.*`, `AccessHarvester.Sql.*`, `AccessHarvester.Access` accordingly. Selecting the plan's original naming requires a search-and-replace across Session 3's stub specs.

## Child session-set tree

| # | Slug | Plan-phase coverage | Prereqs | Sessions (rough) | Synthesis count | Outputs |
|---|---|---|---|---|---|---|
| 1 | `parser-foundations` | Phase 0.2-0.4 (verification harness, schema reconciliation, ANTLR build wiring; project skeleton already done in `accessexportertemp1-import`; Phase 0.1 deferred per OQ 1(a)) | `accessexportertemp1-import` committed | 3-4 | 1 (verification harness design) | `tools/parser-harness/`, schema reconciliation pass on `docs/test-cases/harvester-schema.v0.json`, new `AccessHarvester.Vba.Grammar/AccessHarvester.Vba.Grammar.csproj` with ANTLR4 build-task wired |
| 2 | `vba-spec-study` | Phase 1.1-1.3 (MS-VBAL summary, coverage matrix, hazard list) | none -- runs in parallel with #1 | 3-4 | 2 (MS-VBAL summary, hazard list synthesis) | `docs/spec-study/ms-vbal-summary.md`, `docs/spec-coverage/ms-vbal-coverage.csv`, `docs/spec-coverage/hazard-list.md` |
| 3 | `vba-lexical-and-expression-grammar` | Phase 1.4 lexical + expression rules | #1, #2 | 4 | 2 (lexical grammar, expression grammar) | `AccessHarvester.Vba.Grammar/Vba.g4` (lexical + expression sections), unit tests under `tests/AccessHarvester.Vba.Grammar.Tests/` |
| 4 | `vba-statement-and-module-grammar` | Phase 1.4 statement + module rules | #3 | 4 | 2 (statement grammar, module grammar) | `AccessHarvester.Vba.Grammar/Vba.g4` (statement + module sections, edits in place), unit tests under `tests/AccessHarvester.Vba.Grammar.Tests/` |
| 5 | `vba-grammar-validation` | Phase 1.5-1.6 corpus validation + edge case coverage; authors edge-case fixtures | #4 | 3-4 | 1 (edge-case-driven grammar fix loop) | Grammar bug-fix commits in `AccessHarvester.Vba.Grammar/Vba.g4`, `docs/spec-coverage/parse-failure-log.md`, edge-case fixtures under `docs/test-cases/edge/` |
| 6 | `parser-infrastructure` | Phase 2.1-2.5 (pipeline, error recovery, AST, position tracking, preprocessor); authors preprocessor fixture | #5 | 4-5 | 2 (error recovery strategy, AST shape) | `AccessHarvester.Vba.Parser/`, `SourceLocation`/`SourceRange` types, AST visitor infrastructure, preprocessor pass-through, paired fixture `docs/test-cases/13-preprocessor.bas` + `13-preprocessor.expected.json` |
| 7 | `resolver-declarations-and-scopes` | Phase 3.1-3.2 (declaration model + scope tree) | #6 | 3-4 | 2 (declaration model, scope tree visitor) | `AccessHarvester.Vba.Resolver/Declarations/`, `AccessHarvester.Vba.Resolver/Scopes/` |
| 8 | `resolver-references-and-with-blocks` | Phase 3.3-3.4 (reference resolution + with-block flattening) | #7 | 4-5 | 2 (resolution algorithm, with-block flattening) | `AccessHarvester.Vba.Resolver/References/`, `AccessHarvester.Vba.Resolver/Scopes/WithBlockScope.cs`, resolved-AST annotations |
| 9 | `resolver-default-members-and-inference` | Phase 3.5-3.7 (default member resolution + type inference + cross-reference index) | #8 | 4-5 | 2 (default-member registry design, type inference) | `AccessHarvester.Vba.Resolver/DefaultMembers/`, `AccessHarvester.Vba.Resolver/Types/` (inference pass), `GetReferencesTo()` API |
| 10 | `access-metadata-extraction` | Phase 4.1-4.3 (form, report, query, table metadata); authors `.accdb` fixtures | #1 (independent of resolver work above; runs in parallel with #2-#9) | 5-6 | 2 (metadata schema for forms/reports, query+table extractor) | `AccessHarvester.Access/FormMetadata.cs`, `ReportMetadata.cs`, `QueryMetadata.cs`, `TableMetadata.cs`; ≥7 paired `.accdb` fixtures under `tests/AccessHarvester.Access.Tests/SyntheticDbs/` built by `test-builder/` |
| 11a | `access-type-library-registry` | Phase 4.5 (type-library registry, pragmatic curated approach) | #10 | 2 | 1 (type-library registry curation + consuming contract design) | `AccessHarvester.Access/ITypeLibraryRegistry.cs`, `AccessHarvester.Access/TypeLibraryRegistry.cs` |
| 11b | `access-resolver-symbol-table-integration` | Phase 4.4 (symbol-table integration: synthetic declarations from Access metadata) | #9, #10, #11a | 2-3 | 1 (synthetic-declaration injection design) | Resolver extensions to consume Access metadata under `AccessHarvester.Vba.Resolver/AccessIntegration/`; consumes `ITypeLibraryRegistry` from #11a |
| 12 | `jet-sql-grammar-and-parser` | Phase 5.1-5.2 (JET SQL grammar + parser infrastructure) | #6 | 4-5 | 2 (JET SQL grammar, error recovery + AST for SQL) | `AccessHarvester.Sql.Grammar/AccessHarvester.Sql.Grammar.csproj`, `AccessHarvester.Sql.Grammar/JetSql.g4`, `AccessHarvester.Sql.Parser/` (AST + error recovery), tests under `tests/AccessHarvester.Sql.Grammar.Tests/` |
| 13 | `jet-sql-resolution-and-dynamic` | Phase 5.3-5.4 (form-reference detection in SQL + dynamic SQL analysis) | #11b, #12 | 3-4 | 2 (form-reference detection design, dynamic SQL data-flow analyzer) | `AccessHarvester.Sql.Parser/FormReferences/`, `AccessHarvester.Sql.Parser/DynamicSql/` data-flow analyzer |
| 14 | `enrichment-queries-and-forms` | Phase 6.1-6.2 (query abstraction + form component spec) | #13 | 4 | 2 (query-abstraction structure, form-spec structure) | `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs`, `AccessHarvester.Pipeline/Enrichment/FormComponentSpec.cs` |
| 15 | `enrichment-events-and-validation` | Phase 6.3-6.5 (event handler classification, validation rule extraction, RowSource spec) | #14 | 4-5 | 2 (event-handler taxonomy, validation-rule extraction) | `AccessHarvester.Pipeline/Enrichment/EventHandlerClassification.cs`, `ValidationRule.cs`, `RowSourceSpec.cs` |
| 16 | `enrichment-functions-navigation-review` | Phase 6.6-6.8 (VBA function classification, navigation extraction, manual review routing) | #14 (parallel with #15) | 4-5 | 2 (function classification, navigation graph reconstruction) | `AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs`, `NavigationGraph.cs`, `UnresolvedItem.cs` |
| 17 | `schema-finalization-and-docs` | Phase 7.1-7.4 (JSON schema v1 finalization, examples, output validation, documentation generation) | #15, #16 | 3-4 | 1 (schema v1 review pass; mostly mechanical against existing v0) | `docs/test-cases/harvester-schema.v1.0.json`, `examples/*.json`, output-validation pass under `AccessHarvester.Pipeline/Validation/`, generated markdown docs under `docs/schema/` |
| 18 | `integration-testing-and-acceptance` | Phase 8.1-8.5 (unit consolidation, integration tests, regression suite, performance, acceptance criteria); expands `.accdb` corpus | #17 | 4-5 | 1 (acceptance-criteria validation) | Regression-suite expansion under `tests/AccessHarvester.Integration/`, perf benchmarks, 5-10 additional `.accdb` files under `samples/` for the regression suite, `docs/acceptance-criteria.md` checked off |

**Total:** 19 child session sets (#11 was split into #11a + #11b on 2026-04-26 to unblock the type-library half of the work, which depends only on #10, from the resolver-integration half, which is gated on #9). Sum of session ranges (column 5) = **67-82 sessions** across the tree. The original Appendix E suggested ~32 Claude Code sessions; this decomposition produces roughly 2.3× as many because (a) the retrospective `#1` discipline of ≤2 synthesis sessions per child enforces splitting and (b) every child carries its own `session-verification` overhead. The cost trade is favorable (mechanical sessions are 3-10× cheaper than synthesis sessions per the import-set actuals -- retrospective `#16`).

## Dependency DAG

The DAG below is generated mechanically from column 4 ("Prereqs") of the child table above. Every edge in the list also appears in some row's column 4, and every row's column 4 entry appears as an edge. Format: `prereq -> dependent`.

```
External:
  accessexportertemp1-import -> #1
  (no external prereq for #2; it has no prereqs at all)

Internal edges:
  #1  -> #3
  #1  -> #10
  #2  -> #3
  #3  -> #4
  #4  -> #5
  #5  -> #6
  #6  -> #7
  #6  -> #12
  #7  -> #8
  #8  -> #9
  #9  -> #11
  #10 -> #11
  #11 -> #13
  #12 -> #13
  #13 -> #14
  #14 -> #15
  #14 -> #16
  #15 -> #17
  #16 -> #17
  #17 -> #18
```

Roots (no internal prereq): `#1` (external prereq: `accessexportertemp1-import`), `#2` (no prereq -- runs in parallel from the start).
Leaf: `#18`.

**Parallelizable branches:**
- `#1 parser-foundations` and `#2 vba-spec-study` can run in parallel (no shared files).
- `#10 access-metadata-extraction` runs in parallel with the entire `#3-#9` resolver branch (depends only on `#1`).
- `#12 jet-sql-grammar-and-parser` runs in parallel with `#7-#11` (depends only on `#6`).
- `#15 enrichment-events-and-validation` and `#16 enrichment-functions-navigation-review` are independent siblings under `#14`.

**Critical path:** `#1 → #3 → #4 → #5 → #6 → #7 → #8 → #9 → #11 → #13 → #14 → (#15 ∪ #16) → #17 → #18`. Critical path = 14 child sets; sum of their session ranges = **50-61 sessions** on the long branch. (Sibling `#15`/`#16` count as one node since `#17` waits for both -- using the larger of the two ranges, 4-5.)

## Notes for Sessions 3 and 4

- **Session 3 (stubs):** Each row in the child table becomes one stub at `docs/session-sets/<slug>/spec.md`. Stubs cite their plan-phase coverage column verbatim and their dependency list verbatim. Stubs are flagged "stub -- Session 1 of this set must elaborate the step lists before running them" per spec Standards. Per retrospective adopt-now `#4`, `#5`, `#6`, stubs avoid human-blocking gates for decisions already implied by the spec or `project-guidance.md`; the only Open Questions worth carrying forward are the two enumerated above.
- **Session 4 (playbook):** The kinds of work this tree actually exercises (per the synthesis-count column above):
  - **Grammar authoring** -- 4 instances (rows #3, #4) → Opus xhigh per retrospective `#13`, calibrated `architecture` task type proposal in playbook
  - **AST / declaration model design** -- 4 instances (rows #6, #7) → `architecture` task type
  - **Resolver algorithm design** -- 6 instances (rows #8, #9, #11) → `architecture` task type
  - **JET SQL grammar + dynamic SQL analyzer** -- 4 instances (rows #12, #13) → `architecture`
  - **Enrichment-pipeline classification** -- 6 instances (rows #14, #15, #16) → `analysis` or `architecture`, depends on novelty
  - **Mechanical-correspondence checks** (schema validation, parse-tree shape diff, AST-vs-fixture diff) -- recurring across every row that has a test fixture → `schema-validation` (proposed new task type, Sonnet-pinned per retrospective `#10`)
  - **Test-fixture authoring** -- 4 instances forced (row #5 edge fixtures, row #6 preprocessor fixture, row #10 DB-level fixtures, row #18 corpus expansion); routine in others → `test-generation` task type
  - **Change logs / READMEs** -- 18 instances (one per child set's last session) → `summarization` per retrospective `#9`, with `max_tier=1` fallback documented
- Per retrospective adopt-now `#13`, the playbook must label `architecture` as **uncalibrated against this repo's actuals**; first child to invoke it logs actuals carefully.
