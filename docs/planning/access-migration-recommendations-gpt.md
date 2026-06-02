# Access Migration Recommendations — Final Synthesized Plan

## Purpose

This document reconciles the earlier Gemini recommendation note and the later
Opus planning memo into one execution plan. It is intentionally biased toward
the highest reward-to-risk/effort ratio.

The key conclusion is:

- Keep the `dabbler-access-harvester` -> `dabbler-platform` line as the main
  path.
- Build the migration orchestrator in a new dedicated migration repo, not in
  the harvester repo.
- Script the deterministic mechanical work first.
- Use AI only where the source semantics are genuinely entangled with Access UI,
  runtime state, or business intent.
- Treat the AppDef ecosystem as a reference and parts donor, not as the new
  primary target.

## Evaluation Of The Two Source Docs

### What the Gemini doc got right

- It correctly separates deterministic foundation work from AI-required
  semantic translation.
- It correctly identifies CRUD REST and CRUD Blazor scaffolding as the strongest
  scriptable lane.
- It correctly treats VBA-to-C# as unsafe to fully automate with scripts alone.
- It correctly proposes a bridging stage that turns curated harvester output
  into `.dabbler-entity.json` inputs for platform scaffolding.

### What the Gemini doc missed or understated

- Its action item to place the integration script inside
  `dabbler-access-harvester` conflicts with the approved strategy in
  `docs/planning/strategy.md`, which explicitly defers Access-to-Blazor
  application work to a future migration repo.
- It understates how much useful review tooling already exists today:
  `identify-cleanup`, `identify-usage`, `classify`, `sanitize`, and the editors
  that already consume those JSON artifacts.
- It does not account for the platform's current reality: `Dabbler.CrudSlice`
  and `Dabbler.SchemaApplicator` generate a strong CRUD baseline, but the host
  app still intentionally owns `Program.cs`, `DbContext`, AutoMapper, and
  navigation edits.

### What the Opus memo got right

- It correctly fixes the repo boundary: the orchestrator belongs in a new
  migration repo.
- It correctly treats `generate-migration` as the strongest existing downstream
  mechanical success and as the discipline model for future stages.
- It correctly identifies the current limitation of the three shipped review
  artifacts: they steer the migration, but they do not by themselves contain
  enough UI-projection data to generate application pages.
- It correctly recommends using the AppDef line selectively rather than
  switching the whole migration target away from `dabbler-platform`.
- It correctly frames HomeHealthCare and Northwind Dev Edition as complementary
  proving grounds.

### What the Opus memo should be narrowed to keep ROI high

- It is a plan for a stronger document, not the final execution plan.
- Some of its medium-value ideas should be phased after the thin orchestrator
  proves value:
  - a fourth behavior-disposition review form
  - wiring the SQL parser into `ir/Queries`
  - mining AppDef generators for code or patterns
- Those items are reasonable follow-ons, but they are not prerequisites for the
  first high-value delivery.

## Final Decisions

### 1. Where the work lives

Create a new dedicated migration repo. Working name:
`dabbler-access-migration`.

That repo should:

- consume `dabbler-access-harvester` as a released binary and JSON/IR contract
- consume `dabbler-platform` as the target scaffolding and component platform
- keep both upstream repos mostly unchanged during the first phase

Do not build the orchestrator inside `dabbler-access-harvester`.

### 2. What the primary generation target is

Target `dabbler-platform`, not AppDefToBlazor.

Reasoning:

- It matches the current strategic direction.
- It preserves one platform, one component model, one security model, and one
  scaffolding path.
- It aligns with the user's steer to keep the harvester-to-platform line as the
  spine and to keep things simpler.

Use AppDefToBlazor only as a reference or parts donor when a specific gap blocks
progress, especially around:

- POCO/entity generation details
- report generation patterns
- edge-case type mapping

### 3. What the three review artifacts are for

The existing three review artifacts are the correct near-term review surface:

- `object-cleanup-checklist.json`
- `object-use-checklist.json`
- `classification.json`

Do not spend early effort rebuilding these as Dabbler application forms.

They should remain migration-governance artifacts opened in the existing tools:

- hosted `uat-checklist-editor` for the two checklist files
- local `classification-editor.html` for `classification.json`

### 4. What the default query target is

Do not invest first in a general-purpose Jet-to-T-SQL compiler or in emitting a
large catalog of standalone SQL Server views.

Default target behavior should be:

- tables and relationships -> SQL Server schema/data via `generate-migration`
- saved select queries -> service-layer methods, filter definitions, or report
  RDF data sources
- action queries -> service methods

SQL Server views or stored procedures are tactical fallbacks for specific cases,
not the default primary artifact.

### 5. What the AI boundary is

Scripts own mechanics.
AI owns semantic translation and design judgment.
Humans own intent, approval, and dispute resolution.

## High-ROI Detailed Plan

## Phase A — Thin Orchestrator, No Broad AI Dependence

This is the highest-value first delivery and should be the first milestone.

### Goals

- Prove the end-to-end deterministic migration spine.
- Produce a compiling CRUD skeleton for retained table-backed workflows.
- Reuse existing harvester and platform tooling with minimal upstream changes.
- Validate on both HomeHealthCare and Northwind Dev Edition.

### Deliverables

- a new migration repo with an orchestrator CLI or script runner
- a deterministic runbook and automation wrapper
- a bridge that generates:
  - POCOs / EF entities
  - DTOs
  - `.dabbler-entity.json` files
  - optional registration manifests or copy-paste helper files
- generated CRUD slices via `Dabbler.CrudSlice`
- a buildable solution baseline
- a degradation manifest aggregating unresolved items

### The exact workflow

1. Harvest the Access database with `run`.
2. Generate cleanup recommendations with `identify-cleanup`.
3. Human reviews `object-cleanup-checklist.json`.
4. Generate usage classification with `identify-usage --exclude-cleanup`.
5. Human reviews `object-use-checklist.json`.
6. Generate data classification with `classify`.
7. Human reviews `classification.json`.
8. Generate sanitized data with `sanitize` when needed.
9. Generate SQL Server schema and data scripts with `generate-migration`.
10. Build bridge artifacts from:
    - reviewed cleanup/use/classification outputs
    - `ir/Tables`
    - `ir/Relationships`
    - `ir/Forms`
11. Generate POCOs, DTOs, and `.dabbler-entity.json` files.
12. Run `Dabbler.CrudSlice` for each retained CRUD-worthy entity.
13. Perform the deliberate host edits still required by platform conventions:
    - `DbContext` registration
    - `Program.cs` service wiring
    - AutoMapper or equivalent mapping registration
    - navigation/menu inclusion
    - `dotnet ef migrations` flow
14. Build and test the generated solution.
15. Emit a manifest of everything still requiring AI or manual design.

### PowerShell-shaped reference flow

```powershell
$dbPath = "C:\path\to\MyDb.accdb"
$outRoot = ".\tmp\migration-run"

AccessHarvester run --db $dbPath --out $outRoot

$dbSlugDir = Join-Path $outRoot "AccessMetaData\MyDb"

AccessHarvester identify-cleanup --in $dbSlugDir
AccessHarvester identify-usage --in $dbSlugDir --exclude-cleanup (Join-Path $dbSlugDir "object-cleanup-checklist.json")
AccessHarvester classify --in $dbPath --out $outRoot
AccessHarvester sanitize --classification (Join-Path $outRoot "mydb\classification.json") --in $dbPath --out $outRoot
AccessHarvester generate-migration --in $dbSlugDir --out $outRoot --classification (Join-Path $outRoot "mydb\classification.json") --data (Join-Path $outRoot "mydb")

# bridge step in the new migration repo:
# - read IR + reviewed JSON
# - emit entity/DTO/schema artifacts
# - invoke Dabbler.CrudSlice per entity
```

Important constraint:

- the `--in` path for `identify-cleanup`, `identify-usage`, and
  `generate-migration` must be the per-database directory that directly contains
  `ir/`, not the outer `--out` root

### Why Phase A is the best ROI

- It leans on tooling that already exists and is already tested.
- It immediately yields useful migration assets even before AI translation
  begins.
- It produces a concrete baseline the user can inspect and steer.
- It avoids early investment in speculative automation that may not survive the
  first real pilot.

## Phase B — Targeted AI Handoffs And Narrow Mechanical Enhancements

Start this only after Phase A works on both HomeHealthCare and Northwind.

### Goals

- Shrink the manual gap without turning the project into a speculative compiler.
- Package the hard semantic problems cleanly for AI.
- Add only the smallest new mechanical features that materially improve the AI
  handoff quality.

### Recommended work

1. Add prompt-pack generation for VBA, query, report, and validation-rule
   translation tasks.
2. Route VBA work by existing harvester metadata such as
   `FunctionClassification`.
3. Add characterization-test or parity harnesses around translated behavior.
4. Consider wiring `AccessHarvester.Sql.Parser` into `ir/Queries` if query
   translation quality is materially limited without the AST.
5. Consider a fourth behavior-disposition review artifact only if the first two
   pilots prove the existing review surfaces are insufficient.

### AI task packaging guidance

For each AI-routed task, the orchestrator should bundle:

- source object name and type
- original Access source or query text
- surrounding IR metadata
- caller/callee context when available
- relevant form/report/control context
- unresolved references or degradation notes
- the desired target shape in `dabbler-platform`
- the verification method to apply after generation

## Phase C — Richer Workflow Translation And Platform-Level Polishing

This is the right place for the harder migration work:

- master-detail and subform composition
- cascading combo logic
- calculated control parity
- report RDF completion beyond mechanical skeletons
- security and navigation refinement
- workflow-specific non-CRUD services and pages

This phase is intentionally later because it contains the largest semantic and
design risk.

## What Can Be Scripted vs What Needs AI

| Area | Default owner | Notes |
|---|---|---|
| Harvesting, IR emission, review artifact generation | Script | Already implemented in the harvester. |
| SQL Server schema and data migration | Script | `generate-migration` is the current strongest mechanical lane. |
| POCO/entity/DTO generation | Script | Should be generated from `ir/Tables`, relationships, and reviewed classifications. |
| `.dabbler-entity.json` generation | Script | Must join reviewed JSON with `ir/Forms` and `ir/Tables`; the review JSON alone is insufficient. |
| CRUD service and CRUD page scaffolding | Script | Use `Dabbler.CrudSlice`, then perform the remaining required host edits. |
| Review gate enforcement | Script + Human | Script generates and blocks; human reviews and approves. |
| Simple query translation | Script first, AI review if needed | Only for the subset with straightforward semantics. |
| Query translation with `Forms!`, VBA functions, `TRANSFORM`, or runtime coupling | AI | Too semantically entangled for safe pure scripting. |
| VBA behavior translation | AI | Script should route and package, not pretend to fully translate. |
| Report skeleton generation | Script | Mechanical layout, fields, grouping, and data-source scaffolds are fair game. |
| Rich report expression translation | AI | Especially where expressions depend on Access functions or UI state. |
| Final page composition for non-CRUD workflows | AI + Human | Platform guidance favors explicit page composition. |
| Normalization and business-rule interpretation | AI + Human | These are judgment-heavy decisions. |

## The Three Review Artifacts As Generation Inputs

Yes, the reviewed JSON should drive downstream generation, but only as part of a
join.

### What they are sufficient for

- deciding what to keep or drop
- identifying which objects are likely CRUD-worthy
- identifying data sensitivity and masking behavior
- blocking unsafe downstream automation until a human has reviewed the inputs

### What they are not sufficient for by themselves

- page type selection for all forms
- full grid/form/filter projection
- report rendering details
- event-handling behavior
- workflow composition

### The bridge rule

The migration bridge should synthesize downstream generation inputs by joining:

- `object-cleanup-checklist.json`
- `object-use-checklist.json`
- `classification.json`
- `ir/Tables`
- `ir/Relationships`
- `ir/Forms`

That bridge should derive fields such as:

- `isInGrid`
- `isInForm`
- `isFilterable`
- `isSortable`
- default read-only behavior
- likely page type or CRUD candidacy

## Generation Target Assessment

### Recommended default: `dabbler-platform`

Use `dabbler-platform` as the target because it offers the best strategic fit
and the lowest long-term maintenance risk.

### Specific strengths

- canonical current platform direction
- `Dabbler.CrudSlice` already wraps the service/page templates and schema
  applicator
- direct path to the platform's security, reports, and component model
- simpler mental model for future migration work

### Limits to accept up front

- CRUD-first, not full-workflow-first
- explicit page composition, not one giant metadata-driven UI generator
- some host edits remain intentional

### Role of AppDefToBlazor

Keep it in reserve as:

- proof that broader deterministic generation is possible
- a source of ideas for entity/report generation
- a benchmark against which to judge missing capabilities

Do not switch the main plan to AppDefToBlazor unless the platform path is proven
insufficient on the pilots.

## HomeHealthCare And Northwind Pilot Strategy

Both pilots should be used because they stress different things.

### HomeHealthCare

Use it to validate:

- less tidy naming and real-world legacy conventions
- more ambiguous workflow surfaces
- higher semantic pressure on forms, queries, and validation rules

### Northwind Dev Edition

Use it to validate:

- more canonical table/query/report patterns
- richer bound-form and subform cases
- cleaner parity checks for report and CRUD scaffolding

### Pilot success criteria

Phase A is successful if both databases produce:

- deterministic harvester outputs
- reviewed migration inputs
- SQL migration artifacts
- generated CRUD scaffolds for the retained CRUD-worthy slice
- a compiling application baseline
- a bounded, explicit list of remaining AI tasks

## High-Risk Work To Avoid Early

To keep the reward-to-risk/effort ratio high, do not start with these:

- a full VBA-to-C# translator
- a full Jet-to-T-SQL compiler
- rebuilding the current review tooling as application pages
- importing the whole AppDef line into the main path
- a fully automatic final UI generator for non-CRUD workflows

These are later optimizations, not first value.

## Immediate Next Actions

1. Create the new migration repo and keep both upstream repos black-box where
   possible.
2. Implement the thin orchestrator around the existing harvester commands and
   the existing review gate.
3. Implement the bridge from reviewed harvester output to POCOs, DTOs, and
   `.dabbler-entity.json`.
4. Run `Dabbler.CrudSlice` over the bridged entities and prove a compiling CRUD
   baseline on HomeHealthCare and Northwind.
5. Only after that baseline is real, add targeted AI packaging for VBA, query,
   report, and validation-rule translation.

## Bottom Line

The final reconciled plan is:

- keep the harvester-to-platform line as the main strategy
- move the orchestration into a new migration repo
- maximize reuse of what is already shipped
- let reviewed JSON gate and steer generation, but not pretend it is the whole
  application model
- use scripts for mechanics and AI for semantics
- prove the thin CRUD-plus-SQL spine first on HomeHealthCare and Northwind
- treat everything beyond that as a phased follow-on, not as a prerequisite

That path has the best current reward-to-risk/effort ratio.