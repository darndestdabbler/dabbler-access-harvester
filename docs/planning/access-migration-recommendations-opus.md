# Access Migration Recommendations - Superseded Pointer

This file is retained only as a breadcrumb from the earlier planning pass.

The current source of truth for this work now lives in the dedicated
`dabbler-access-migration-orchestrator` repo:

- `docs/planning/access-migration-recommendations-gpt.md`
- `docs/planning/project-plan.md`
- `docs/planning/phase-a-bridge-contract.md`

The long Opus planning memo was an intermediate artifact. Use the orchestrator
repo's planning docs for the active migration strategy and execution plan.
- **Recommend: target dabbler-platform (CrudSlice + component-library mappings)**
  per the user's steer and to keep ONE platform/component/security model
  ("simpler, easier to use"); use AppDefToBlazor as reference + parts donor for the
  EF-entity and report-RDF generators.

### §4 The soup-to-nuts runbook (command-by-command, anchored to HHC + Northwind)
A numbered pipeline with per-step automation level, reusing shipped tooling:
`run` → `identify-cleanup` → `identify-usage` → `classify`/`sanitize` →
**HUMAN REVIEW GATE** → `generate-migration` → POCO/DTO synth → `.dabbler-entity.json`
synth → CrudSlice loop → (`dotnet ef migrations`) → AI handoff passes (VBA/queries/
reports/validation) → build/test → degradation rollup. Two worked examples
(HomeHealthCare, Northwind Dev Edition) using their committed golden output so the
runbook is concrete and dry-runnable. Note the EF impedance mismatch explicitly:
**POCOs-first → `dotnet ef migrations`**, with `generate-migration`'s `schema.sql`
used as a **parity oracle**, `data.sql` as the data-load path.

### §5 The AI-vs-script boundary (the centerpiece — 3-column table)
- **SCRIPT (mechanics):** CLI orchestration; form generation; review-gate
  enforcement; schema.sql/data.sql; type-mapped POCO+DTO; `.dabbler-entity.json`
  synthesis (ir/Forms+ir/Tables+object-use); CrudSlice loop; wiring Sql.Parser into
  ir/; bundling VBA/SQL AST into AI prompt packets + routing by classification;
  per-phase degradation-manifest rollup.
- **AI (reasoning over entangled source):** VBA intent → service/handler
  (state/UI decoupling); UI-coupled/idiomatic Jet SQL → parameterized service
  methods/LINQ; reports beyond the mechanical band subset; validation expressions →
  C# validators; 3NF/denormalization analysis; master-detail/cascading UI.
- **HUMAN (intent + sign-off):** the 3 review forms (authoritative); the proposed
  4th behavior-disposition form; platform's deliberately-non-generated hand-edits
  (DbContext DbSet, Program.cs registration, AutoMapper, nav menu, `dotnet ef`);
  adjudicating the 18 open domain questions + AI-translation disagreements
  (Step-7 verifier-disagreement procedure).
Each row cites the grounding evidence (verdict %, file, or migration-map mapping).

### §6 Per-target detail (answers Q2 fully, with the migration map as authority)
- **Tables/relationships → SQL Server:** DONE (`generate-migration`); ~70%
  mechanical; degradations honest. The reference discipline to copy everywhere.
- **Queries/report sources:** parse = mechanical (wire Sql.Parser); translate =
  AI into C# services / filter config / RDF dataSources ("saved query disappears").
  Reports → `report.rdf.json` for `DabblerReportPage`; structure mechanical,
  complex expressions AI; **parity-gate against report-baseline aggregates**.
- **VBA → C#:** AI-required; ~40–50% mechanical; script bundles per-procedure
  AST + `FunctionClassification` + callers + unresolved refs and routes
  (Mechanical→auto+verify, Structured/ManualReview→AI). Gate with **characterization
  tests (Option-D pattern)**. Honest answer to "is there enough metadata?": yes for
  routing/bundling + the mechanical subset; no for safe full automation.
- **REST + Blazor:** CrudSlice = pure script for CRUD once entities + schema exist;
  master-detail/subforms/FK-combos/calculated controls/geometry → AI + the
  migration-map composition patterns.

### §7 The 3-forms-as-input correction + the proposed 4th form (answers Q3)
- The 3 shipped JSONs lack UI-projection fields + explicit page-type → join with
  `ir/Forms` (ControlSource→isInForm; continuous/list membership→isInGrid; index→
  isSortable) and `ir/Tables`. Recommend deriving projection in the bridge
  synthesizer rather than bloating the shipped schemas.
- **Recommend a 4th "behavior-disposition" review form** (uat-checklist-editor
  shape): one item per VBA proc / query / report / validation rule with disposition
  (port/rewrite/drop/defer) + a slot to ratify the AI translation. This is the
  single biggest *human-decision* gap today.

### §8 Cross-cutting discipline & risks (make it robust + simple)
- Copy `generate-migration`'s discipline everywhere: db-slug-addressed artifacts,
  never-silent degradations, exit-3, per-phase manifest, idempotent re-runs
  (partial classes + SchemaApplicator BEGIN/END markers + CrudSlice `--skip-*`).
- **Pin to actual contracts, not docs:** bind to the platform's real
  `SchemaApplicator`/`SchemaModels.cs` (`entity`, nullable `isSortable`); the live
  `supplier.dabbler-entity.json` and prose docs disagree (`entity` vs `entityName`).
  Add a CI round-trip conformance test.
- EF impedance mismatch handled (§4). Gate-bypass, unsupported-types, and
  over-automation risks each get a one-line mitigation.

### §9 Phasing (keep it incremental + simple)
- **Phase A (no AI):** new repo + orchestrator + review-gate + generate-migration +
  POCO/DTO + `.dabbler-entity.json` synth + CrudSlice loop + build → running CRUD
  skeleton for keep+MainDataEntry objects; prove on HHC + Northwind.
- **Phase B (tiered AI):** wire Sql.Parser into ir/; add 4th form; add AI
  bundlers/routers for VBA/queries/reports/validation, each behind its verification
  gate (characterization tests / report-baseline parity / golden SQL).
- **Phase C (polish):** master-detail/subform composition, security mapping, nav
  menu, the 18 open questions.

---

## Files the doc will reference (read-only inputs; no edits to these)
- Harvester: `docs/planning/strategy.md`, `docs/cross-repo/access-to-platform-migration-map.md`,
  `docs/harvester/interpretation-guide.md`, `docs/planning/access-migration-recommendations-gemini.md`,
  `AccessHarvester.Pipeline/Migration/*`, `AccessHarvester.Sql.Parser/*`, `AccessHarvester.Sql.Grammar/JetSql.g4`,
  the 3 `*-schema.json`, `samples/GoldenOutput/{HomeHealthCare,NorthwindDevEdition}/`,
  `output-tests/blazor-add-an-order/` (Option-D reference).
- Platform: `tools/Dabbler.CrudSlice/*`, `tools/Dabbler.SchemaApplicator/SchemaModels.cs`,
  `supplier.dabbler-entity.json`, `docs/scaffolding-templates/*`.
- AppDef line (reference/parts-donor only): `AppDefToBlazor/src/Dabbler.BlazorGenerator/Generators/*`,
  `AppDefEcosystem/docs/Generation_Complete_IR_Strategy.md`, `AccessBlazorPlanning/80-session-plan.md`.

## The only artifact written on approval
- `docs/planning/access-migration-recommendations-opus.md` (the deliverable).
- 2–3 memory files (durable cross-repo facts: AppDef-ecosystem existence+relationship;
  the prior gemini doc + the strategy.md "future migration repo" decision; the
  adversarial-verdict downgrades) + MEMORY.md pointers.

## Verification (how we'll know the doc is right)
- Self-consistency: every recommendation traces to a cited file or a verdict %;
  no claim contradicts `strategy.md`.
- Dry-run check: the §4 runbook commands resolve against the committed golden
  output for HHC + Northwind (paths exist; CLIs/flags match the real subcommands).
- Contract check: the `.dabbler-entity.json` shape the doc specifies matches the
  platform's actual `SchemaModels.cs` (key `entity`, nullable `isSortable`).
- Optional: route the finished doc for one cross-provider consensus pass (per the
  user's standing preference) before they compare it to the Gemini version.
