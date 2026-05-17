# Phase 4 Gap Audit — Mechanical Harvester Finish Line

> **Session set:** `remaining-work-planner` Session 1 of 3
> **Authored:** 2026-05-17, in-conversation by Claude Opus 4.7
> @ effort=high.
> **Authority for "Done":**
> [`docs/planning/strategy.md`](../../planning/strategy.md)
> Phase 4. **Authority for OUT OF SCOPE:** the same doc's
> "Frozen Unless Reauthorized" list + the 2026-05-17 strategic
> review at
> [`docs/strategic-reviews/2026-05-17-platform-tooling-fit/`](../../strategic-reviews/2026-05-17-platform-tooling-fit/).
>
> **Purpose:** for each Phase 4 "Done" criterion, classify the
> current state of the harvester as **COVERED**, **GAP**,
> **AMBIGUOUS**, or **CANDIDATE FOR UNFREEZING**. For each GAP,
> capture the working name, brief scope, dependency on existing
> planned work, and rough cost band.
>
> **Standards:** the audit is opinionated. Every classification
> has a recommendation; "tentatively in scope, awaiting human
> confirmation" is the minimum. Where the audit interprets
> strategy or guidance, it quotes the relevant text inline.

---

## Phase 4 "Done" criteria (from strategy.md, abbreviated for inline reference)

1. **Parser coverage finality** — every documented VBA / Jet SQL
   / property-sheet construct in the canonical fixtures either
   parses correctly or emits an explicit `UnresolvedItem` with
   provenance and reason.
2. **Schema finality** — `kb/` and `ir/` schemas and the VBA
   parser's `harvester-schema.v0.json` (now v1.0) are stable,
   documented, and validated against committed sample output.
3. **Sample-corpus validation** — golden output committed for
   each canonical fixture; CI/test suite verifies the harvester
   reproduces that golden output.
4. **CLI surface** — every harvester subcommand (`extract`,
   `translate`, `run`, `parse`, `generate-docs`) is documented,
   has end-to-end test coverage, and produces deterministic
   output.
5. **Unresolved-record reporting** — per-run report listing
   every `UnresolvedItem` with structured fields.
6. **Documentation** — user-facing `README.md` and `CLI.md` (or
   equivalent) describing the harvester for downstream consumers.
7. **Packaging** — installable by a downstream consumer without
   a from-source build.

---

## Current pipeline state (evidence base)

Established through inspection 2026-05-17:

**Production projects** (10): `AccessHarvester.Access`,
`AccessHarvester.Cli`, `AccessHarvester.Core`,
`AccessHarvester.Pipeline`, `AccessHarvester.Sql.Grammar` (g4),
`AccessHarvester.Sql.Parser`, `AccessHarvester.SymbolDb`,
`AccessHarvester.Vba.Grammar` (g4), `AccessHarvester.Vba.Parser`,
`AccessHarvester.Vba.Resolver`. File counts: Access=14,
Cli=1, Core=21, Pipeline=22, Sql.Parser=30, SymbolDb=34,
Vba.Parser=25, Vba.Resolver=43.

**Test projects** (10): `AccessHarvester.CharacterizationTests`,
`AccessHarvester.Integration`, `AccessHarvester.Pipeline.Tests`,
`AccessHarvester.Sql.Grammar.Tests`, `AccessHarvester.SymbolDb.Tests`,
`AccessHarvester.Tests`, `AccessHarvester.TranslationProbe.Tests`,
`AccessHarvester.Vba.Grammar.Tests`, `AccessHarvester.Vba.Parser.Tests`,
`AccessHarvester.Vba.Resolver.Tests`.

**Pipeline.cs surface** (`AccessHarvester.Core/Pipeline.cs`):
`Pipeline.Default()` factory, `WithCategories(...)`,
`Extract(dbPath, outputRoot)`, `Translate(rawDir, kbDir)`,
`Enrich(kbDir, irDir)`, `Run(dbPath, outputRoot)`, plus
`BaseDir / RawDir / KbDir / IrDir` path helpers and per-category
`ExtractResult / TranslateResult / EnrichResult / RunResult`
records.

**Enrichment pipeline** (`AccessHarvester.Pipeline/Enrichment/`):
`EventHandlerClassification`, `EventHandlerClassifier`,
`FormComponentSpec`, `FormComponentSpecBuilder`,
`FunctionClassification`, `FunctionClassificationBuilder`,
`NavigationGraph`, `NavigationGraphBuilder`, `QueryAbstraction`,
`QueryAbstractionBuilder`, `RowSourceSpec`, `RowSourceSpecBuilder`,
`UnresolvedItem`, `UnresolvedItemAggregator`, `ValidationRule`,
`ValidationRuleExtractor`. Plus `EnrichmentPipelineRunner` and
`EnrichmentResult`.

**CLI surface** (`AccessHarvester.Cli/Program.cs`): subcommands
`extract`, `translate`, `run`, `parse`, `generate-docs`. Plus
bare-file mode equivalent to `parse`. PrintUsage exists. Exit
codes documented.

**Schemas**: `docs/test-cases/harvester-schema.v0.json` and
`harvester-schema.v1.0.json`. Schema docs at
`docs/schema/v0-vs-tiers.md` and `v1-adr.md`.

**Examples**: `examples/minimal-form.json`,
`examples/complex-form.json`, `examples/northwind-output.json`.

**Samples**: 7 `.accdb` files committed under `samples/`
including the three canonical fixtures (`NorthwindDevEdition`,
`HomeHealthCare`, `MiniComplex`). YAML ground-truth mirrors at
`samples/DatabaseYaml/HomeHealthCare.accdb/` and
`samples/DatabaseYaml/NorthwindDevEdition.accdb/`, plus
`samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml`
and `FormAndReportDefaults.accdb.yaml`.

**Completed parser/resolver/enricher/CLI-shaping sets** (19):
`parser-foundations`, `parser-infrastructure`,
`vba-grammar-validation`, `vba-lexical-and-expression-grammar`,
`vba-statement-and-module-grammar`,
`resolver-declarations-and-scopes`,
`resolver-references-and-with-blocks`,
`resolver-default-members-and-inference`,
`access-resolver-symbol-table-integration`,
`access-type-library-registry`, `access-metadata-extraction`,
`jet-sql-grammar-and-parser`, `jet-sql-resolution-and-dynamic`,
`enrichment-events-and-validation`,
`enrichment-functions-navigation-review`,
`enrichment-queries-and-forms`,
`structured-form-report-extractor`,
`form-report-code-and-grouping`, `schema-finalization-and-docs`.
Plus the meta-planning sets (`vba-spec-study`,
`vba-parser-session-set-generator`), the symbol-DB arc
(`vba-symbol-db`, `vba-symbol-db-path-b-probe`,
`vba-symbol-resolution-and-enrichment`), the spike
(`access-object-extractor-spike`), the extraction coverage
(`table-extractor-coverage`), and the import bootstrap
(`accessexportertemp1-import`).

---

## Criterion-by-criterion classification

### 1. Parser coverage finality

> Phase 4 statement: every documented VBA / Jet SQL / property-sheet
> construct in the canonical fixtures either parses correctly OR
> emits an explicit `UnresolvedItem` with provenance and reason.

**Evidence covered:**
- VBA parser (`AccessHarvester.Vba.Parser/`, 25 source files +
  `Vba.g4`) shipped through Phases 1.4–1.6 of the migration plan
  via `vba-lexical-and-expression-grammar`,
  `vba-statement-and-module-grammar`, and `vba-grammar-validation`
  sets. `vba-grammar-validation` is the canonical proof: that
  set's change-log subhead is "#5" — i.e., it's the validation
  pass against the corpus that the plan's Phase 1.5 calls for.
- JET SQL parser (`AccessHarvester.Sql.Parser/`, 30 source files
  + `JetSql.g4`) shipped via `jet-sql-grammar-and-parser` (Phase
  5.1–5.2) and `jet-sql-resolution-and-dynamic` (Phase 5.3–5.4).
- Resolver (`AccessHarvester.Vba.Resolver/`, 43 source files)
  shipped via the four resolver sets and
  `access-resolver-symbol-table-integration` /
  `access-type-library-registry`.
- `UnresolvedItem` type and `UnresolvedItemAggregator` exist in
  `AccessHarvester.Pipeline/Enrichment/`, shipped via
  `enrichment-functions-navigation-review` Session 3.

**GAP — Canonical-fixture parse-success report.**
- **Working name:** `harvester-finish/canonical-parse-success-report`
- **Scope:** Phase 4 says "every documented construct in the
  canonical fixtures." Today, the grammar-validation set ran
  against a corpus, but a *canonical-fixture-specific* parse-
  success report (one for `NorthwindDevEdition.accdb`, one for
  `HomeHealthCare.accdb`, one for `MiniComplex.accdb`) is not
  committed as a per-fixture artifact. The harness from the
  migration plan's Phase 0.2 exists implicitly in the test
  suites but does not produce a checked-in,
  diff-against-on-regression report per fixture. Without that
  artifact, "canonical-fixture parse coverage" is a claim, not
  a test gate.
- **Dependency:** none new — uses existing parser + canonical
  fixtures + existing test infrastructure.
- **Cost band:** **small** (1–2 sessions). Largely
  test/infrastructure plus a committed report shape.
- **Recommendation:** in scope. This is the gate that closes
  criterion 1 in a verifiable way.

**AMBIGUOUS — Macro/property-sheet coverage.**
- **Question:** the migration plan calls for "documented VBA /
  Jet SQL / property-sheet construct" coverage. The harvester
  has `MacroActionsEnricher` and `PropertyDecoder` shipped in
  Core, but Open Validation #7 in strategy.md is "partially
  resolved" — the four pilot workflows did not exercise macros,
  queries-as-first-class workflows, or report-bound logic. For
  Phase 4 specifically, do the canonical fixtures' macros and
  property-sheet entries get the same "parses or emits
  UnresolvedItem" guarantee, or is property-sheet behavior
  outside the canonical-fixture coverage gate?
- **Recommendation:** treat as in-scope by default; flag to the
  human at the gap-audit handoff. If the human decides
  macros/property-sheet are in scope, the
  `canonical-parse-success-report` GAP above should explicitly
  include them. If not, document the boundary in Phase 4 (i.e.,
  "documented constructs" means VBA + Jet SQL, not macros or
  property-sheet entries).

---

### 2. Schema finality

> Phase 4 statement: `kb/` and `ir/` schemas + the VBA parser's
> harvester-schema are stable, documented, and validated against
> committed sample output.

**Evidence covered:**
- `harvester-schema.v0.json` and `harvester-schema.v1.0.json`
  both exist at `docs/test-cases/`.
- Schema docs at `docs/schema/v0-vs-tiers.md` and
  `docs/schema/v1-adr.md`.
- `SchemaValidator` + `SchemaValidationResult` exist in
  `AccessHarvester.Pipeline/Validation/`.
- `SchemaDocGenerator` exists in
  `AccessHarvester.Pipeline/DocGen/`.
- `schema-finalization-and-docs` session set closed; its change-
  log title is "Phase 7.1-7.4" against the migration plan.

**COVERED.** Schema is finalized at v1.0; validator exists;
docs exist; the schema-finalization session set explicitly
covered Phase 7.1-7.4 of the plan. The v0→v1 migration is
documented in `v1-adr.md`.

**Minor — Schema docs subsection coverage check.**
- One light follow-up worth confirming in Session 2: are all
  major schema types documented (FormSpecification,
  ReportSpecification, ModuleSpecification, etc.)? The
  v1-adr.md plus auto-generated schema-doc output from
  `SchemaDocGenerator` should cover this, but a spot-check
  during Session 2 stub-spec authoring would close any
  remaining drift.
- **Recommendation:** no new session set. If the spot-check
  reveals coverage gaps, they belong in a tree-amendment to
  `schema-finalization-and-docs` (a follow-up commit, not a new
  set).

---

### 3. Sample-corpus validation

> Phase 4 statement: golden output committed for each canonical
> fixture; CI/test suite verifies the harvester reproduces that
> golden output.

**Evidence partially covered:**
- Canonical fixtures committed at `samples/{NorthwindDevEdition,
  HomeHealthCare, MiniComplex}.accdb`.
- YAML ground-truth mirrors at
  `samples/DatabaseYaml/HomeHealthCare.accdb/` and
  `NorthwindDevEdition.accdb/` (per-object structured YAML, NOT
  full harvester `ir/` output).
- `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml`
  and `FormAndReportDefaults.accdb.yaml` exist as form/report-
  specific ground truth.
- `examples/` contains `minimal-form.json`, `complex-form.json`,
  `northwind-output.json` — these are example-illustrating
  fixtures, NOT full-database golden output.

**GAP — Full-database golden output for each canonical fixture.**
- **Working name:** `harvester-finish/canonical-golden-output`
- **Scope:** Run the harvester's `run` subcommand against each
  of `NorthwindDevEdition.accdb`, `HomeHealthCare.accdb`, and
  `MiniComplex.accdb`. Commit the resulting `raw/`, `kb/`, `ir/`
  artifacts (or a stable subset — see Recommendation) as golden
  output. Add an integration test under
  `tests/AccessHarvester.Integration/` that re-runs the
  harvester against the canonical fixtures on each CI run and
  diffs the output against the committed golden. Drift fails
  the build.
- **Dependency:** GAP #1 (canonical-parse-success report)
  naturally pairs here — failed parses become `UnresolvedItem`
  records in the golden output.
- **Cost band:** **medium** (3–4 sessions). Sources of cost:
  (a) deciding what subset of the three-tier output is golden-
  worthy vs. timestamp-noisy (samples' .accdb mutate timestamps
  on open per `lessons-learned.md` → "Run Parallel Session Sets
  In Per-Set Git Worktrees"); (b) authoring the diff harness
  that normalizes acceptable noise; (c) committing the actual
  output (large JSON files — likely 5-10MB per database).
- **Recommendation:** in scope; the most important Phase 4 gap.
  Without this, Phase 4 has no end-to-end gate.

**AMBIGUOUS — Excluded fixtures (`ProjectBoard`, `MiniComplex`).**
- `project-guidance.md` says: "`samples/MiniComplex.accdb` and
  `samples/ProjectBoard.accdb` are excluded from automated test
  coverage pending an error-audit pass."
- The audit needs the human to confirm: for Phase 4's
  "canonical fixtures" criterion, is MiniComplex in or out?
  Strategy.md lists `NorthwindDevEdition`, `HomeHealthCare`,
  `MiniComplex` as the three canonicals; project-guidance.md
  excludes MiniComplex from automated test coverage.
- **Recommendation:** ask the human. If MiniComplex is out for
  Phase 4 too, narrow the canonical-fixture list to two
  (`NorthwindDevEdition` + `HomeHealthCare`). If in, the
  error-audit pass MiniComplex is waiting on becomes a
  prerequisite gap (`harvester-finish/minicomplex-error-audit`,
  small set, blocks #3 GAP above for MiniComplex specifically).

---

### 4. CLI surface

> Phase 4 statement: every harvester subcommand is documented,
> has end-to-end test coverage, and produces deterministic
> output.

**Evidence covered:**
- CLI surface at `AccessHarvester.Cli/Program.cs`. Subcommands:
  `extract`, `translate`, `run`, `parse`, `generate-docs`. Plus
  bare-file mode.
- PrintUsage method documents all subcommands with options +
  exit codes.
- `tests/AccessHarvester.Tests/` (and `Integration/`) exist —
  these likely contain per-subcommand tests.

**GAP — End-to-end CLI test coverage audit.**
- **Working name:** `harvester-finish/cli-e2e-test-audit`
- **Scope:** Spot-check whether every subcommand
  (`extract`, `translate`, `run`, `parse`, `generate-docs`) has
  at least one end-to-end test that invokes the binary and
  asserts on its output. If gaps, author the missing tests. Add
  determinism checks (run twice, diff output, ensure stable).
  `generate-docs` is treated as in scope by default — it's on
  the shipped CLI surface and the criterion says "every
  harvester subcommand," so the audit closes the question
  rather than deferring it. If the human later narrows the CLI
  surface, the stub spec for this gap can drop it; that's a
  Session 2 decision, not a Session 1 blocker.
- **Dependency:** GAP #3 (canonical-golden-output) covers the
  `run` subcommand's end-to-end test by construction; the audit
  for `extract`, `translate`, `parse`, `generate-docs`
  separately is what this gap covers.
- **Cost band:** **small** (1–2 sessions). Mostly test
  authoring with a thin layer of determinism harness.
- **Recommendation:** in scope. May fold into #3's session set
  if the per-subcommand coverage turns out to be thin (~2
  missing tests vs. ~10).

---

### 5. Unresolved-record reporting

> Phase 4 statement: per-run report listing every
> `UnresolvedItem` with structured fields (source location,
> construct kind, reason, suggested follow-up).

**Evidence partially covered:**
- `UnresolvedItem.cs` and `UnresolvedItemAggregator.cs` exist
  in `AccessHarvester.Pipeline/Enrichment/`.
- Per-construct usage in `FunctionClassification.cs` and
  `NavigationGraph.cs` (grep confirmed).
- Migration plan's Phase 6.8 (Manual Review Routing) explicitly
  calls for `UnresolvedItems` array on every output, populated
  with structured records.

**GAP — Top-level per-run UnresolvedItems output artifact.**
- **Working name:** `harvester-finish/unresolved-items-report`
- **Scope:** Confirm that the harvester emits a top-level
  `UnresolvedItems.json` (or similar) per run, aggregating all
  `UnresolvedItem` records produced by the enrichment pipeline.
  If not, add the aggregation point + output emission. Schema
  finality (criterion #2) implies this is part of the schema
  itself; ensure the v1.0 schema accommodates it. Audit needs
  to verify whether this is wired through to the CLI's
  `run`/`extract` outputs.
- **Dependency:** depends on #2 (schema finality) being
  complete — should be. Pairs with #3 (canonical-golden-output)
  to verify per-fixture: each fixture's golden output includes
  its full `UnresolvedItems` array.
- **Cost band:** **small** (1 session). If the wiring is
  already there, this collapses into a verification + doc step
  inside #3's set rather than its own set.
- **Recommendation:** verify during Session 2 stub-spec
  authoring whether the wiring is in place. If yes, fold into
  the audit checklist for #3. If no, separate small set.

---

### 6. Documentation

> Phase 4 statement: user-facing `README.md` and `CLI.md` (or
> equivalent) describing the harvester for downstream consumers.

**Status: GAP** (partial coverage; the downstream-AI-consumer-
facing interpretation guide is what's missing).

**Evidence of partial coverage:**
- `README.md` exists and is substantive — describes the
  three-tier output model, repository layout, build/test
  instructions, and CLI surface in detail. The CLI section walks
  through `extract` / `translate` / `run` with examples and
  category filtering. **This part is COVERED**: the developer-
  onboarding documentation needs nothing new.
- `docs/schema/v0-vs-tiers.md` and `docs/schema/v1-adr.md`
  document the schema evolution. **Also COVERED** for the
  schema audience.
- The schema-finalization-and-docs set's change-log mentions
  Phase 7.4 (Documentation Generation) was in scope, but its
  scope was schema-doc generation, not consumer-interpretation
  prose.

**What's missing (the GAP portion):**

**GAP — Operations manual / interpretation guide for the
downstream consumer.**
- **Working name:** `harvester-finish/operations-and-interpretation-docs`
- **Scope:** The migration plan's Phase 7.4 calls for
  per-database markdown summaries, dependency diagrams,
  documentation generation, and (per Appendix D)
  an Operations Manual. The README covers "how to invoke";
  what's missing is "how to interpret the output." A downstream
  AI consumer reading the harvester's ir/ JSON needs guidance
  on: what each field means, what its provenance is, how
  `UnresolvedItem` records should be interpreted, and what
  known limitations (per Open Validation #7) it should expect.
- **Dependency:** GAP #2 (schema finality, already COVERED)
  produces the auto-generated schema docs; this gap is the
  narrative layer on top.
- **Cost band:** **small-to-medium** (2–3 sessions). Most of
  the prose can be assembled from existing
  `docs/schema/v1-adr.md`, `docs/homehealthcare-northwind-comparison.md`,
  and the migration plan itself.
- **Recommendation:** in scope. The
  state-government documentation rigor mentioned in the
  migration plan's Appendix D and the
  `project-guidance.md` principle "the downstream consumer is
  an AI agent, not a human reviewer" both pull toward investing
  in this. Don't go overboard — the audience is the next AI
  agent, not a human reader.

**Minor — Doc directory tidying (non-blocking).**
- One light follow-up: `docs/` has 7 top-level `.md` files
  including the strategy doc, the workflow doc, the migration
  plan, the homehealthcare-vs-northwind comparison, the
  lowdefy evaluation, the IR schema doc, the portability
  feedback, and the bootstrap doc. The harvester finish-line
  documentation may want to land under `docs/harvester/` or
  similar for clarity (separating "how to use the harvester"
  from "how the harvester project is run"). **This is a
  tidying call for Session 2 to decide while authoring the
  operations-docs stub spec — not a Phase 4 gate, not a Session
  1 human-blocker.** Session 2 picks the path; if uncertain,
  default to keeping the new harvester-finish docs flat in
  `docs/` and revisit if `docs/` becomes cluttered later.

---

### 7. Packaging

> Phase 4 statement: installable by a downstream consumer
> without a from-source build (dotnet tool, binary release, or
> equivalent distribution shape).

**Evidence — none.**
- No `dotnet tool` configuration visible
  (`<PackAsTool>true</PackAsTool>` not present in the Cli
  csproj).
- No `dotnet pack` workflow in the repo.
- No published binary release.
- The CLI today is "clone the repo, `dotnet run`".

**GAP — Packaging the CLI for distribution.**
- **Working name:** `harvester-finish/cli-packaging`
- **Scope (acceptance-criteria level):**
  - A downstream consumer can install the harvester CLI
    without cloning the repo or building from source.
  - The installed CLI exposes the same subcommand surface
    (`extract`, `translate`, `run`, `parse`, `generate-docs`)
    and PrintUsage text as the source build.
  - A `pack` step (or release-build step, whichever fits the
    chosen shape) produces the distributable artifact
    reproducibly.
  - Install steps are documented in the README and (when GAP #6
    closes) the operations doc.
- **Distribution-shape decision is deferred to Session 2** —
  `dotnet tool` global install, self-contained binary release,
  and embedded-NuGet are all viable shapes. Session 2's stub
  spec picks one with human input (see Open Question 3) and
  defines the exact mechanics there. Session 1 only commits to
  "Phase 4 needs an installable distribution shape."
- **Dependency:** depends on the Cli being feature-stable —
  which it functionally is, but GAP #4's e2e test coverage
  should land first to reduce ship-with-bugs risk.
- **Cost band:** **small** (1–2 sessions). Most distribution
  shapes are well-trodden in .NET tooling; the bulk of the
  work is choosing the shape and validating reproducibility.
- **Recommendation:** in scope. Final gate for Phase 4 (the
  "done when installable by a downstream consumer" test).

---

## Candidate-for-unfreezing items

> The 2026-05-17 strategic review froze five categories of work
> in strategy.md. The audit considered each against Phase 4
> needs.

### CANDIDATE — `tools/AccessHarvester.IrCodegen/` production generator

- **Frozen reason:** probe verdict only; runtime usefulness not
  yet demonstrated.
- **Audit's read:** Phase 4 doesn't require the IR codegen.
  Phase 4 is about the *harvester* (parser, extractor, schemas,
  CLI); the IR codegen is downstream tooling that turns
  harvester output into Blazor components. **Keep frozen.**
- **Recommendation:** no unfreeze. Confirmed Phase 4
  out-of-scope per the existing strategy reset.

### CANDIDATE — `dabbler-platform-discovery` refresh

- **Frozen reason:** dated snapshot at platform commit
  `aa77020e1ca0`; refresh only when the future migration repo
  actively consumes the platform.
- **Audit's read:** Phase 4 doesn't depend on the platform.
  **Keep frozen.**
- **Recommendation:** no unfreeze.

### CANDIDATE — Additional Northwind workflows beyond the
existing four packages

- **Frozen reason:** the four-workflow pilot proved package
  shape; more is incremental scale proof, not new ground.
- **Audit's read:** Phase 4 doesn't need more workflows; the
  existing four packages are not part of the harvester's
  shippable surface (they're a separate downstream consumer
  artifact). **Keep frozen.**

### CANDIDATE — Old stale enrichment sessions

- **Frozen reason:** none currently active after the 2026-05-17
  housekeeping pass.
- **Audit's read:** if a Phase 4 gap (specifically the
  canonical-parse-success report in GAP #1) surfaces an enricher
  defect — e.g., the macro-actions enricher fails on
  Northwind's specific macro shapes — the fix lands in the
  relevant enricher's existing project (`AccessHarvester.Core`
  or `AccessHarvester.Pipeline`) as a tree-amendment to the
  already-completed enrichment session set, not as a reopen of
  a frozen session. **Keep frozen.**

### CANDIDATE — CRUD-scaffolder expansion

- **Frozen reason:** the platform tool is correctly scoped to
  single-entity admin slices; reshaping around Access migration
  was explicitly recommended against.
- **Audit's read:** entirely out-of-scope for Phase 4. **Keep
  frozen.**

---

## Gap summary

| # | Working name | Phase 4 criterion | Cost band | Disposition |
|---|---|---|---|---|
| 1 | `harvester-finish/canonical-parse-success-report` | #1 Parser coverage finality | Small (1–2 sessions) | In scope; first gap to close |
| 2 | (none — schema is COVERED) | #2 Schema finality | — | Spot-check during Session 2; potential tree-amendment to `schema-finalization-and-docs` if drift surfaces |
| 3 | `harvester-finish/canonical-golden-output` | #3 Sample-corpus validation | Medium (3–4 sessions) | In scope; most important gap |
| 4 | `harvester-finish/cli-e2e-test-audit` | #4 CLI surface | Small (1–2 sessions) | In scope; may fold into #3 |
| 5 | `harvester-finish/unresolved-items-report` | #5 Unresolved-record reporting | Small (1 session, or fold into #3) | In scope, pending wiring audit during Session 2 |
| 6 | `harvester-finish/operations-and-interpretation-docs` | #6 Documentation | Small-to-medium (2–3 sessions) | In scope; downstream-AI-consumer-facing docs |
| 7 | `harvester-finish/cli-packaging` | #7 Packaging | Small (1–2 sessions) | In scope; final gate |

**Net: ~6 new session sets, totaling ~10–14 sessions** to
close Phase 4. At ~$0.10 routed per session, that's roughly
$1.00–$1.40 total routed cost. Most work is in-conversation
under subscription windows per the outsource-last pattern.

### Recommended execution order (advisory, surfaced to Session 2)

1. `canonical-parse-success-report` (#1) — establishes the
   gate that every other gap diffs against.
2. `unresolved-items-report` (#5) wiring audit — likely folds
   into #3 by the time Session 2 plans it.
3. `canonical-golden-output` (#3) — the central deliverable.
4. `cli-e2e-test-audit` (#4) — runs in parallel with #3 if
   resources allow; otherwise after #3.
5. `operations-and-interpretation-docs` (#6) — depends on #1,
   #3, #5 being landed so the doc reflects ground truth.
6. `cli-packaging` (#7) — final gate; depends on #4 being
   green.

---

## Open questions for the human (resolve before Session 2)

Three genuine scope/boundary questions only the human can
adjudicate. Two earlier candidate questions (originally listed
as #3 `generate-docs` purpose and #5 documentation-directory
home) have been resolved in-audit on the cross-provider
verifier's recommendation and demoted: `generate-docs` is
in scope for criterion #4 by default (see GAP #4 wording);
documentation home is a Session 2 tidying call (see Criterion
6's "Doc directory tidying" note).

1. **Is `MiniComplex.accdb` in scope for Phase 4 canonical-
   fixture coverage?** Strategy.md lists it as canonical;
   `project-guidance.md` excludes it from automated test
   coverage pending an error-audit pass. If excluded, narrow
   the canonical list to two. If included, the
   `minicomplex-error-audit` becomes a prerequisite for
   the canonical-golden-output gap.

2. **Are macros and property-sheet entries in scope for the
   canonical-parse-success report (GAP #1)?** Open Validation
   #7 in strategy.md is "partially resolved" with macros not
   exercised. For Phase 4's "documented constructs in canonical
   fixtures" criterion, define the boundary explicitly.

3. **Packaging shape — `dotnet tool`, self-contained binary
   release, or embedded-NuGet?** The audit defers the choice
   to Session 2, but the human's preference (if any) shapes
   Session 2's stub spec for `cli-packaging`. If the human has
   no preference, Session 2 picks based on downstream-consumer
   convenience (likely `dotnet tool` given the consumer
   already needs the .NET runtime, but Session 2 owns the
   decision).

Once the human resolves these, Session 2 (Plan The Remaining
Work) can author the stub specs at
`docs/session-sets/<harvester-finish/...>/spec.md` and the
`remaining-work-tree.md` index.

---

## Out-of-scope items (audit considered and rejected)

For traceability, items the audit considered and explicitly
classified as not gating Phase 4:

- **Refining the type-library registry beyond DAO/ADO/Office.**
  Per migration plan Phase 4.5, the registry is hand-curated
  and degrades gracefully for unknown types. Already shipped
  by `access-type-library-registry`. Further investment is
  not gating.
- **Performance optimization to migration-plan Phase 8.4
  thresholds** ("median database processes in under 10
  minutes"). Phase 4 is about correctness and shippability, not
  scale. Performance is a follow-up consideration once a real
  consumer surfaces speed needs.
- **Ribbons / Switchboards / Cross-database library references
  beyond the canonical fixtures' surface area.** Strategy.md
  Frozen list explicitly defers these beyond "what's needed for
  the canonical fixtures' golden output." If the canonical-
  golden-output set surfaces any of these as gaps, they get
  amendments to the existing enrichment specs (tree-amendment
  pattern), not new "Ribbons" or "Switchboards" session sets.
- **AI-interpretation columns (`Purpose`, `Justification`,
  `Confidence`) in the symbol DB.** Per project-guidance.md
  Principles → Output Architecture, "interpretive slots remain
  null at the `ir/` tier." Out of scope for the harvester
  itself; downstream AI consumer fills these.
- **Ribbons XML parsing** if the canonical fixtures don't have
  ribbon XML. Northwind has `USysRibbons`; HHC and MiniComplex
  may not. Audit recommends: if Northwind's USysRibbons parses
  cleanly today, leave it. If not, it surfaces during GAP #3
  and gets a tree-amendment.

---

## Session 2 inputs (handoff)

When Session 2 starts, the human will have either confirmed or
amended the audit; the orchestrator will have:

1. This `gap-audit.md` (canonical input).
2. The human's resolutions to the 3 open questions above (two
   earlier questions were demoted/resolved in-audit during the
   Round-2 verification follow-up)
   (recorded inline in this file as the human commits them, or
   in a follow-up note).
3. Authority to author the stub specs at
   `docs/session-sets/harvester-finish-*/spec.md` for each in-
   scope gap.

Session 2 must NOT extend Phase 4 to absorb out-of-scope items
unless the human explicitly reopens them via a strategy.md
revision.

---

## Closing

The harvester pipeline is structurally complete. The 30+
completed session sets shipped the parser, resolver, enricher,
schema, and CLI in well-defined increments. What remains is
**closure work** — committing the golden output, packaging the
CLI, and writing the downstream-consumer-facing documentation —
not new feature surface.

The seven Phase 4 criteria fall into three buckets:

- **COVERED (1):** schema finality (Criterion #2). README +
  schema docs are also substantively in place, but the
  downstream-AI-consumer-facing interpretation guide is
  missing — that piece is tracked in the documentation GAP.
- **GAP, small or fold-in (5):** canonical parse-success report
  (#1), CLI e2e test audit (#4), unresolved-items wiring audit
  (#5), operations & interpretation docs (#6), packaging (#7).
- **GAP, central (1):** canonical golden output (#3) — the
  most important single piece of remaining work.

If the human authorizes the gaps as scoped, Phase 4 closes in
~6 follow-on session sets over ~10–14 sessions at ~$1.00–$1.40
total routed cost. That is the executable plan Session 2 stubs
and Session 3 sequences into the final ROADMAP.
