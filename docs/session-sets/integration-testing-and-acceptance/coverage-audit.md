# Coverage Audit — Phase 8.1-8.2 Inventory

> Authored by the Session 1 orchestrator as the coverage map for the
> integration-testing-and-acceptance set. Cross-referenced through
> `route(task_type="schema-validation")` later in the same session for an
> independent coverage-vs-target correspondence check; reconciled below
> against the canonical tree.

## Method

Walked each child set #1-#17 in `session-set-tree.md` row order, opened
its `change-log.md` to confirm what the set landed, then mapped each
landed component to the test project that exercises it today. Where a
landed component has no exercising test, listed it as a gap.

The Phase 8.1-8.2 plan target is "comprehensive coverage": each grammar
production (positive + negative), each resolver scenario, each metadata
extraction case, each enrichment classification — plus
**end-to-end** integration tests under `tests/AccessHarvester.Integration/`
that run the full pipeline and assert structural properties of output.

**Producing-set numbering convention:** column 1 below uses the canonical
row numbers from
`docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`.
The tree's planned test-project paths and the repo's actual test-project
paths diverged during execution (e.g., the tree planned
`tests/AccessHarvester.Access.Tests/` for set #10's deliverables but the
work landed inside the existing `tests/AccessHarvester.Tests/` instead).
Column 3 below records the **actual** repo path; the tree's planned
path is noted inline where it differs.

## Component → Test-Project Map

| Producing Set | Component | Exercising Test Project | Gap? |
|---|---|---|---|
| #1 `parser-foundations` | `tools/parser-harness/`, schema reconciliation pass on `harvester-schema.v0.json`, `AccessHarvester.Vba.Grammar.csproj` skeleton + ANTLR build wiring | None directly — the parser harness is exercised transitively by the grammar/parser/resolver test projects below; the build wiring is exercised by the fact that those projects build green | None — infrastructure-only set; downstream test runs are the validation |
| #3 `vba-lexical-and-expression-grammar` | VBA lexical + expression grammar productions | `AccessHarvester.Vba.Grammar.Tests` (Lexical/, Expressions/) | None |
| #4 `vba-statement-and-module-grammar` | VBA statement + module grammar productions | `AccessHarvester.Vba.Grammar.Tests` (Statements/, Modules/) | None |
| #5 `vba-grammar-validation` | Grammar bug-fix commits, edge-case fixtures under `docs/test-cases/edge/` | `AccessHarvester.Vba.Grammar.Tests` (corpus regression) | None |
| #6 `parser-infrastructure` | `AccessHarvester.Vba.Parser/`, `SourceLocation`/`SourceRange`, AST visitor infrastructure, preprocessor pass-through | `AccessHarvester.Vba.Parser.Tests` (Ast/, VbaParserPipelineTests) | None |
| #7 `resolver-declarations-and-scopes` | DeclarationModel + ScopeBuildingVisitor | `AccessHarvester.Vba.Resolver.Tests` (DeclarationModelTests, ScopeBuildingVisitorTests, ScopeBuildingFixtureTests) | None |
| #8 `resolver-references-and-with-blocks` | ReferenceResolver + With-block resolution | `AccessHarvester.Vba.Resolver.Tests` (References/) | None |
| #9 `resolver-default-members-and-inference` | Default member + type inference | `AccessHarvester.Vba.Resolver.Tests` (DefaultMembers/, Types/) | None |
| #10 `access-metadata-extraction` | DAO/COM extractors (Tables, Relationships, Queries, Forms, Reports, Macros, Modules); property decoder, geometry enricher, label resolver, text capacity, VBA signature parser; ≥7 paired `.accdb` fixtures | `AccessHarvester.Tests` (TableMetadataExtractorTests, RelationshipMetadataExtractorTests, QueryMetadataExtractorTests, SyntheticMetadataExtractorIntegrationTests, MacroXmlParserTests, PropertyDecoderTests, GeometryEnricherTests, LabelResolverTests, TextCapacityEnricherTests, VbaSignatureParserTests, SyntheticDbIntegrationTests, SyntheticFixtureSnapshotTests). Tree's planned path was `tests/AccessHarvester.Access.Tests/`; the work landed inside the existing `AccessHarvester.Tests` project | None |
| #11a `access-type-library-registry` | Type-library symbol registry | `AccessHarvester.Tests` (TypeLibraryRegistryTests). Tree's planned path was `tests/AccessHarvester.Access.Tests/` | None |
| #11b `access-resolver-symbol-table-integration` | Resolver consumes registry | `AccessHarvester.Vba.Resolver.Tests` (AccessIntegration/, CrossReference/) | None |
| #12 `jet-sql-grammar-and-parser` | Jet SQL grammar + AST + parser infrastructure | `AccessHarvester.Sql.Grammar.Tests` (Ast/, JetSqlParserPipelineTests) | None |
| #13 `jet-sql-resolution-and-dynamic` | Form-reference detection, dynamic-SQL data-flow analyzer | `AccessHarvester.Sql.Grammar.Tests` (FormReferences/, DynamicSql/) | None |
| #14 `enrichment-queries-and-forms` | QueryAbstractionBuilder, FormComponentSpecBuilder | `AccessHarvester.Pipeline.Tests` (QueryAbstractionBuilderTests, FormComponentSpecBuilderTests) | None |
| #15 `enrichment-events-and-validation` | EventHandlerClassifier, ValidationRuleExtractor, RowSourceSpecBuilder | `AccessHarvester.Pipeline.Tests` (EventHandlerClassifierTests, ValidationRuleExtractorTests, RowSourceSpecBuilderTests) | None |
| #16 `enrichment-functions-navigation-review` | FunctionClassificationBuilder, NavigationGraphBuilder, UnresolvedItemAggregator | `AccessHarvester.Pipeline.Tests` (FunctionClassificationBuilderTests, NavigationGraphBuilderTests, UnresolvedItemAggregatorTests) + `EndToEndFixtureTests` (hermetic VBA-side end-to-end across the 5 anchor fixtures) | None |
| (Cross-cut from #14-#16) | `EnrichmentPipelineRunner` orchestration over the enrichment-set components above | `AccessHarvester.Pipeline.Tests` (EnrichmentPipelineRunnerTests) | None — runner is not a canonical tree-row output of any single set; it is the integration glue that coalesces #14/#15/#16's components |
| #17 `schema-finalization-and-docs` | `harvester-schema.v1.0.json`, `SchemaValidator`, `SchemaDocGenerator`, `generate-docs` CLI subcommand | `AccessHarvester.Pipeline.Tests` (Validation/SchemaValidatorTests, DocGen/SchemaDocGeneratorTests) | **Gap A** — no test that validates an actual pipeline-produced manifest against the v1.0 schema |
| CLI surface | `extract` / `translate` / `run` / `parse` / `generate-docs` subcommand dispatch + flag parsing | None directly prior to this session — covered transitively by Pipeline tests | **Gap B** — addressed in Session 1 by `tests/AccessHarvester.Integration/Cli/` (CliBootstrapTests, CliParseTests, CliGenerateDocsTests) |

## Identified Gaps

### Gap A — No pipeline → schema-validation E2E gate

The `harvester-schema.v1.0.json` defines a top-level `DatabaseManifest`
shape with `Database`, `Summary`, `Forms[]`, `Reports[]`, `Queries[]`,
`Modules[]`, `TableSchemas[]`, `Relationships[]`, etc. as arrays.
`SchemaValidatorTests` validates committed `examples/*.json` fixtures
against this schema, and `SchemaDocGeneratorTests` consumes a manifest
to produce markdown — but **no test today takes the actual output of
`Pipeline.Default().Run(db, outRoot)` and validates it against the
schema.**

The current pipeline emits per-category, per-object files (e.g.,
`ir/Forms/<formName>.ir.json`) rather than a single rolled-up manifest.
That means there is no manifest-emitter component for Session 1 to
exercise — the gap is genuine, not just untested.

**Disposition:** Session 1 lands a hermetic schema-conformance test
suite that validates each committed `examples/*.json` against
`harvester-schema.v1.0.json` (proving schema + fixtures are mutually
consistent). The "actual pipeline output validates against schema" gate
requires a manifest-aggregator component that does not yet exist —
flagged here for Session 2 or as an acceptance-criteria blocker for
Session 4.

### Gap B — No black-box CLI tests (now addressed in Session 1)

The CLI's argument parsing, subcommand dispatch, exit codes, and
`--json-status` envelopes were never exercised through their actual
entry point prior to this set. Every existing test called into
`Pipeline.Default()` or into individual extractor/enricher classes
directly.

**Disposition:** Session 1 lands black-box CLI tests in the new
`AccessHarvester.Integration` project. The CLI is invoked via
`Process.Start` against the build-output `AccessHarvester.exe` —
a true black-box boundary. Subcommands that need Access COM
(`extract`, `translate`, `run`) are gated `[SkippableFact]` per the
`Platform And Runtime` principle.

### Non-Gap notes

- The full extract → translate → enrich pipeline against synthetic and
  real `.accdb` files is **already** covered by
  `AccessHarvester.Tests.SyntheticDbIntegrationTests` and
  `SyntheticFixtureSnapshotTests`. The new Integration project does
  **not** duplicate that — it sits one layer up (CLI surface + schema
  contract).
- The hermetic VBA-side end-to-end (parse → scope → resolve → classify
  → navigate → unresolved aggregation) is already covered by
  `AccessHarvester.Pipeline.Tests.EndToEndFixtureTests` over the 5
  anchor fixtures (07, 08, 10, 11, 12). The new Integration project does
  **not** duplicate this either.
- Set #2 (`vba-spec-study`) is a docs-only set (`docs/spec-study/` and
  `docs/spec-coverage/`) — there is no testable code from it. Omitted
  from the table for that reason.

## Session 1 Deliverable

`tests/AccessHarvester.Integration/` xUnit project with:

- **Hermetic CLI tests** (`Cli/`):
  - `CliBootstrapTests.cs` — `--help`, no-args, unknown command exit codes.
  - `CliParseTests.cs` — `parse <docs/test-cases/01-simple-validation.bas>`
    asserts JSON `status=parsed` shape; bare-file mode and file-not-found.
  - `CliGenerateDocsTests.cs` — `generate-docs --in examples/minimal-form.json
    --out <tmp>` asserts markdown file emitted.
- **Hermetic schema-conformance tests** (`Pipeline/`):
  - `SchemaConformanceTests.cs` — validates each `examples/*.json` against
    `harvester-schema.v1.0.json` via `SchemaValidator`.
- **`[SkippableFact]` integration test** (`Pipeline/`):
  - `EndToEndPipelineTests.cs` — drives `Pipeline.Default().Run()` against
    `samples/MiniComplex.accdb`; asserts per-category IR files emitted (the
    full schema-rollup gate is deferred until the manifest-aggregator
    exists).
- **Test support** (`TestSupport/`):
  - `CliInvoker.cs` — black-box `Process.Start` invocation of the
    AccessHarvester CLI exe.
  - `RepoPaths.cs` — walks up from `AppContext.BaseDirectory` to locate
    `docs/`, `examples/`, `samples/` (mirrors the pattern in
    `EndToEndFixtureTests.LocateTestCasesRoot`).
  - `AccessAvailability.cs` — Skip helper for the COM-required path.

## Routed Validation

The next step in this session passed this audit through
`route(task_type="schema-validation")` for an independent
coverage-vs-target correspondence check. The verifier's findings
(saved at `coverage-audit-routed.md`) flagged producing-set numbering
errors in the original draft (a Critical) plus omission of set #1 and
spurious labelling of an enrichment-pipeline-runner row (Majors). Those
findings have been applied to the table above. The verifier's classification
of Gap A and Gap B was confirmed as accurate.
