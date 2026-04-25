# AccessExporterTemp1 Import Spec

> **Purpose:** Discover what code in the sibling `../AccessExporterTemp1`
> repo is relevant to this harvester, plan an import that respects this
> repo's principles (lean, no UI, deterministic output, no
> AI-interpretation), and execute the import session-by-session so each
> imported component is verified independently before the next lands.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/accessexportertemp1-import/`
> **Prerequisite:** None. This is the first session set in the repo.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Project Overview

`../AccessExporterTemp1` is a sibling repo (not under git) containing a
working C# .NET 10 toolchain that already extracts Access metadata
through three output tiers (`raw/` → `kb/` → `ir/`). Its `BATON.md`
documents the architecture in detail. This session set imports the
parts of that codebase that fit the harvester's mission, leaving behind
anything that does not earn its keep here.

The import is **not** a wholesale copy. The sibling repo carries
artifacts (UAT-emission helpers tied to a specific human-review loop,
preliminary Gemini-era code kept "for reference / history",
sample-output dumps from prior runs) that either don't apply to this
project's no-UAT discipline or are non-essential debt. The first
session in this set decides what's in scope and what's not — without
that gate, the orchestrator would otherwise hoard work and import
everything just to "be safe."

The import lands in this repo's intended structure: `AccessHarvester.Cli`,
`AccessHarvester.Core`, `tests/AccessHarvester.Tests`, plus supporting
`tools/`, `test-builder/`, and `samples/` directories. Namespaces and
solution name follow the repo's project-guidance conventions. Code
imported from the sibling does **not** retain its original namespaces;
it is renamed during import.

The session-set close is reached when:

- A C# solution builds clean (`dotnet build`).
- The hermetic unit-test suite passes (`dotnet test`, including parser
  tests if any landed).
- At least one `[SkippableFact]` integration test runs against a
  committed synthetic `.accdb` fixture and passes when Access is
  available.
- The pipeline produces `raw/` → `kb/` → `ir/` output for at least one
  category (Forms or Reports) on a committed sample DB.
- A short `README.md` at the repo root names the entry points and
  describes how to run the CLI.

---

## Feature 1: Discovery And Import Plan (Session 1)

### Scope

Read `../AccessExporterTemp1/BATON.md` end-to-end. List every project,
folder, and resource. Tag each one as one of:

- **import-as-is** — copy with namespace rename, no behavioral change.
- **import-with-edits** — copy, then make specific edits (e.g., remove
  UAT-emission helper coupling).
- **rebuild** — concept stays, code is rewritten in this repo because
  the sibling's implementation conflicts with this repo's principles
  (e.g., AI-interpretation slots, defensive fallbacks not earning
  their keep, UAT-loop coupling).
- **out-of-scope** — leave behind. Includes the
  `AccessExporterTemp1/AccessExporterTemp1/` original-Gemini-code
  folder (per the BATON it's kept "for reference/history"); the
  sibling's `sample-output/` dumps from prior runs (we'll regenerate
  on demand); and any UAT-emission machinery whose downstream consumer
  is the human-UAT checklist editor (not used in this repo).

The deliverable of Session 1 is `import-plan.md` inside this session
set's folder, with one row per sibling-folder + a short rationale.
Open questions for the human are flagged inline under "**Question for
human:**" so Session 2 can begin only after the human has answered.

### Standards

- Treat the BATON.md as informative but not authoritative — verify
  against the actual file tree, since the BATON drifts. If a
  discrepancy is found, write the truth to the import plan and note
  the discrepancy.
- The plan is opinionated. "Copy everything because it might be
  useful" is not a valid disposition.
- Use `route(task_type="planning")` to draft the plan after
  enumerating the file tree directly.

---

## Feature 2: Core Import (Sessions 2–4)

### Scope

Import the items the human approved in Session 1, one logical layer
per session:

- **Session 2 — Solution skeleton + Core abstractions + COM session.**
  Create `AccessHarvester.sln` (or `.slnx`), `AccessHarvester.Core`
  csproj, and copy/rename the assembly-level
  `[SupportedOSPlatform("windows")]` declaration, the
  `Abstractions/Abstractions.cs` file, the `Com/AccessComSession.cs`
  file, and the `Model/AccessNode.cs` DTO. Build clean.
- **Session 3 — Extraction phase.** Import the four extractors
  (`SaveAsTextExporter`, `QueryExporter`, `TableExporter`,
  `RelationshipExporter`) plus `Pipeline.cs` (Extract phase only).
  Wire up `AccessHarvester.Cli` with the `extract` subcommand.
  Build + run extract against a committed sample `.accdb`; verify
  `raw/` output appears.
- **Session 4 — Translation + Enrichment phases.** Import
  `AccessTextParser`, `SaveAsTextTranslator`,
  `JsonPassthroughTranslator`, and the enrichers the import plan
  approved (`GeometryEnricher`, `LabelResolver`, `PropertyDecoder`,
  `TextCapacityEnricher`, `VbaSignatureEnricher` + its `Vba/`
  subfolder, `MacroActionsEnricher` + its `Macros/` subfolder). Wire
  up the `translate` and `run` CLI subcommands. Build + run full
  pipeline against the same sample DB; verify `kb/` and `ir/` output.

### Standards

- Each session includes an end-to-end `dotnet test` run plus a
  schema-validation step against any committed sample output.
- Namespaces follow this repo's convention
  (`AccessHarvester.Core.*`, `AccessHarvester.Cli`).
- Embedded resources (`property-defaults.json`, `font-metrics.json`)
  travel with the enrichers that consume them.
- The session that imports `PropertyDecoder` also imports the
  `tools/generate_property_defaults.py` script, since the resource
  it generates is the contract.
- The orchestrator is encouraged to mark imported types `[Obsolete]`
  rather than physically delete sibling-source artifacts that a
  later session is scheduled to rewrite — see project-guidance →
  Workflow Expectations.

---

## Feature 3: Tests + Synthetic DB Builder (Session 5)

### Scope

Import the test project (`tests/AccessExporter.Tests/` →
`tests/AccessHarvester.Tests/`) keeping:

- Hermetic unit tests (`GeometryEnricherTests`, `LabelResolverTests`).
- Synthetic-DB integration tests (`SyntheticDbIntegrationTests`)
  with the committed `.accdb` fixtures under `SyntheticDbs/`.
- The `test-builder/` Python project (specs JSON, builder helpers,
  `build_synthetic.py`).

Drop:

- `UatChecklist.cs`, `UatChecklistTests.cs`, `UatEmitted/`,
  `UatGroundTruth/` — the UAT-emission loop is not used here. (Open
  question for Session 1: does the human want a different
  label-quality review mechanism in its place, or is "verifier-driven
  review" sufficient? The Session 1 import plan should ask.)

If Session 1 decides to keep some form of label-quality review, that
work lands in a *future* session set, not here. This session imports
only the tests that already exist for non-label-review concerns.

### Standards

- All hermetic unit tests must pass.
- Integration tests use `[SkippableFact]` so non-Windows runs skip
  cleanly.
- The Python `test-builder/` is included but not run as part of
  `dotnet test` — it is a developer utility.

---

## Feature 4: Samples + Tools + README (Session 6)

### Scope

Import committed sample inputs (`samples/HomeHealthCare.accdb`,
`MiniComplex.accdb`, `NorthwindDevEdition.accdb`, `ProjectBoard.accdb`,
`FormAndReportDefaults.accdb`, plus the YAML extractor utilities the
sibling repo carries for ground-truth generation). Import any `tools/`
scripts the prior sessions referenced and didn't already land.

Write `README.md` at repo root: project overview, build + test
commands, CLI usage (extract / translate / run subcommands), known
constraints (Windows + Access required for COM extractor; hermetic
tests run anywhere).

### Standards

- README must reference `docs/planning/project-status.md`,
  `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`,
  and `docs/ai-led-session-workflow.md` so a fresh orchestrator finds
  them on landing.
- README is short. Detail belongs in the linked docs.
- Use `route(task_type="documentation")` for the README — it *is*
  code documentation, so the documentation template is correct here.
  (If the README acquires a "what we built and why" narrative
  section, that section is generated via
  `task_type="summarization"` per project-guidance.)

---

## Session Plan

### Session 1 of 6: Discovery + Import Plan

**Steps:**
1. Enumerate `../AccessExporterTemp1/` file tree directly via
   `Glob`/`ls`. Compare against the BATON.md inventory; note any
   drift.
2. Read `../AccessExporterTemp1/BATON.md` end-to-end.
3. For each top-level folder and significant file, classify as
   import-as-is / import-with-edits / rebuild / out-of-scope.
4. Route the draft to `route(task_type="planning")` for an
   independent review of the disposition list — verifier should
   flag anything misclassified.
5. Save `docs/session-sets/accessexportertemp1-import/import-plan.md`
   with the final dispositions and any open questions for the human.
6. Build + test: not applicable (no code yet).
7. End-of-session cross-provider verification of the import plan.

**Creates:**
- `docs/session-sets/accessexportertemp1-import/import-plan.md`

**Touches:** none

**Ends with:** `import-plan.md` committed; pending human approval of
any flagged questions before Session 2 starts.

**Progress keys:** `session-1/enumerate-tree`, `session-1/read-baton`,
`session-1/classify-dispositions`, `session-1/route-plan-review`,
`session-1/write-plan`, `session-1/verify`

---

### Session 2 of 6: Solution Skeleton + Core Abstractions + COM Session

**Steps:**
1. Verify human approved import-plan.md questions (or no questions
   remain).
2. Create `AccessHarvester.sln` (or `.slnx`), `AccessHarvester.Core`
   csproj targeting `net10.0`, `AccessHarvester.Cli` csproj.
3. Copy `Abstractions/Abstractions.cs`, `Com/AccessComSession.cs`,
   `Model/AccessNode.cs` from the sibling, renaming namespaces to
   `AccessHarvester.Core.*`.
4. Place the assembly-level `[SupportedOSPlatform("windows")]` on
   `AccessHarvester.Core`.
5. Wire a stub `Program.cs` in `AccessHarvester.Cli` so the solution
   builds.
6. `dotnet build` — must pass.
7. End-of-session cross-provider verification.

**Creates:**
- `AccessHarvester.sln`
- `AccessHarvester.Core/AccessHarvester.Core.csproj`
- `AccessHarvester.Core/Abstractions/Abstractions.cs`
- `AccessHarvester.Core/Com/AccessComSession.cs`
- `AccessHarvester.Core/Model/AccessNode.cs`
- `AccessHarvester.Cli/AccessHarvester.Cli.csproj`
- `AccessHarvester.Cli/Program.cs` (stub)

**Touches:** none

**Ends with:** clean `dotnet build`.

**Progress keys:** `session-2/create-solution`,
`session-2/import-abstractions`, `session-2/import-com`,
`session-2/import-model`, `session-2/build`, `session-2/verify`

---

### Session 3 of 6: Extraction Phase

**Steps:**
1. Verify Session 2 prerequisites exist.
2. Import `Extraction/SaveAsTextExporter.cs`,
   `Extraction/QueryExporter.cs`, `Extraction/TableExporter.cs`,
   `Extraction/RelationshipExporter.cs`. Rename namespaces.
3. Import `Pipeline.cs` (extract phase only — translate/enrich
   methods stubbed for now).
4. Wire `extract` subcommand in `AccessHarvester.Cli/Program.cs`
   (hand-rolled arg parser, matching the sibling).
5. Copy `samples/MiniComplex.accdb` (or whichever sample the import
   plan named as the smallest reasonable smoke-test input) into
   `samples/`.
6. `dotnet build` then run `AccessHarvester.exe extract
   --db samples/<sample>.accdb --out tmp/raw-output/` and verify
   `raw/` directory tree appears.
7. `dotnet test` (still empty / hermetic-only at this point).
8. End-of-session cross-provider verification — include a sample
   `raw/` output file in the verification prompt so the verifier can
   sanity-check the format.

**Creates:**
- `AccessHarvester.Core/Extraction/SaveAsTextExporter.cs`
- `AccessHarvester.Core/Extraction/QueryExporter.cs`
- `AccessHarvester.Core/Extraction/TableExporter.cs`
- `AccessHarvester.Core/Extraction/RelationshipExporter.cs`
- `AccessHarvester.Core/Pipeline.cs`
- `samples/<sample>.accdb`

**Touches:**
- `AccessHarvester.Cli/Program.cs`

**Ends with:** `extract` subcommand produces `raw/` output for a
sample DB.

**Progress keys:** `session-3/import-extractors`,
`session-3/import-pipeline`, `session-3/wire-cli`,
`session-3/copy-sample`, `session-3/run-extract`, `session-3/verify`

---

### Session 4 of 6: Translation + Enrichment Phases

**Steps:**
1. Verify Session 3 prerequisites exist.
2. Import `Translation/AccessTextParser.cs`,
   `Translation/SaveAsTextTranslator.cs`,
   `Translation/JsonPassthroughTranslator.cs`.
3. Import enrichers from the import plan:
   `Enrichment/GeometryEnricher.cs`,
   `Enrichment/LabelResolver.cs`,
   `Enrichment/PropertyDecoder.cs`,
   `Enrichment/TextCapacityEnricher.cs`,
   `Enrichment/VbaSignatureEnricher.cs` + `Enrichment/Vba/`
   subfolder, `Enrichment/MacroActionsEnricher.cs` +
   `Enrichment/Macros/` subfolder.
4. Import embedded resources (`Resources/property-defaults.json`,
   `Resources/font-metrics.json`) and wire up as embedded resources
   in the csproj.
5. Fill in the translate + enrich phases of `Pipeline.cs`. Wire the
   `translate` and `run` subcommands in the CLI.
6. `dotnet build` clean.
7. Run `AccessHarvester.exe run --db samples/<sample>.accdb
   --out tmp/run-output/` and verify `raw/` + `kb/` + `ir/` all
   appear with expected content.
8. End-of-session cross-provider verification — include a sample
   `ir/` output file plus the schema (or a representative subset) so
   the verifier can sanity-check the enrichment.

**Creates:** ~10 new files under `AccessHarvester.Core/Translation/`
and `Enrichment/`, plus embedded resources.

**Touches:**
- `AccessHarvester.Core/Pipeline.cs`
- `AccessHarvester.Cli/Program.cs`
- `AccessHarvester.Core/AccessHarvester.Core.csproj` (embedded resources)

**Ends with:** full pipeline produces `raw/` → `kb/` → `ir/` for the
sample DB.

**Progress keys:** `session-4/import-translation`,
`session-4/import-enrichers`, `session-4/import-resources`,
`session-4/wire-pipeline`, `session-4/wire-cli`, `session-4/run-full`,
`session-4/verify`

---

### Session 5 of 6: Tests + Synthetic DB Builder

**Steps:**
1. Verify Session 4 prerequisites exist.
2. Create `tests/AccessHarvester.Tests/AccessHarvester.Tests.csproj`.
3. Import hermetic unit tests: `GeometryEnricherTests.cs`,
   `LabelResolverTests.cs`. Rename namespaces.
4. Import `SyntheticDbIntegrationTests.cs` and the committed
   `SyntheticDbs/*.accdb` fixtures.
5. Import the Python `test-builder/` directory (builder helpers,
   `create_from_spec.py`, `build_synthetic.py`, `specs/*.json`,
   `README.md`).
6. **Skip:** `UatChecklist.cs`, `UatChecklistTests.cs`,
   `UatEmitted/`, `UatGroundTruth/`. Per Session 1's import plan.
7. `dotnet test` — hermetic unit tests must pass; integration tests
   should pass on Windows / skip elsewhere.
8. End-of-session cross-provider verification.

**Creates:**
- `tests/AccessHarvester.Tests/AccessHarvester.Tests.csproj`
- `tests/AccessHarvester.Tests/GeometryEnricherTests.cs`
- `tests/AccessHarvester.Tests/LabelResolverTests.cs`
- `tests/AccessHarvester.Tests/SyntheticDbIntegrationTests.cs`
- `tests/AccessHarvester.Tests/SyntheticDbs/*.accdb`
- `test-builder/` (Python utilities)

**Touches:**
- `AccessHarvester.sln` (add test project)

**Ends with:** `dotnet test` passes (or skips integration on
non-Windows). Synthetic DB builder importable.

**Progress keys:** `session-5/create-test-proj`,
`session-5/import-unit-tests`, `session-5/import-integration-tests`,
`session-5/import-fixtures`, `session-5/import-test-builder`,
`session-5/run-tests`, `session-5/verify`

---

### Session 6 of 6: Samples + Tools + README + Change Log

**Steps:**
1. Verify Session 5 prerequisites exist.
2. Import remaining `samples/` artifacts the import plan approved
   (additional `.accdb` files, the `MasterDbPropertyExtractor.accdb`
   VBA YAML tool, `FormsAndReportsYaml/` ground-truth dumps if the
   plan kept them).
3. Import any `tools/*.py` scripts that did not land earlier
   (`generate_property_defaults.py`, `validate_against_yaml.py`).
4. Route a `README.md` draft via `route(task_type="documentation")`
   covering: project overview, build + test commands, CLI usage,
   constraints, pointers to planning docs.
5. `dotnet build` + `dotnet test` final pass.
6. End-of-session cross-provider verification.
7. **Last session — generate change-log.md** via
   `route(task_type="summarization")` and commit it in the same
   commit as Session 6's other changes.
8. Cost report, commit + push, notification.
9. Step 9 reorganization review (post-notify): scan
   `project-guidance.md` and `lessons-learned.md` for any
   promotion candidates the import session experience surfaced. If
   none, output the canonical "no changes recommended" line.

**Creates:**
- `samples/*.accdb` (remaining)
- `tools/*.py`
- `README.md`
- `docs/session-sets/accessexportertemp1-import/change-log.md`

**Touches:** none beyond what the new files imply.

**Ends with:** full clean build + test, README at root, change-log
committed, session set marked complete.

**Progress keys:** `session-6/import-samples`,
`session-6/import-tools`, `session-6/route-readme`,
`session-6/build-test`, `session-6/verify`,
`session-6/route-changelog`, `session-6/commit-push`,
`session-6/notify`, `session-6/reorganization-review`

---

## Open questions parked for human

These are the questions Session 1 should ask the human in
`import-plan.md`. Listing them here so the orchestrator does not
forget to surface them:

1. **Label-quality review.** The sibling's UAT-emission machinery
   (`UatChecklist.cs`, `UatEmitted/`, `UatGroundTruth/`) supports a
   human-graded review of label-resolution quality. This repo does
   not use a UAT layer. Replace with verifier-driven review of a
   sampled output, drop the concept entirely, or defer to a future
   session set?
2. **`MasterDbPropertyExtractor.accdb` + `FormsAndReportsYaml/`
   ground-truth dumps.** These are dev tools tied to regenerating
   `property-defaults.json`. Keep so we can re-run as Access
   versions evolve, or drop and rebuild on demand?
3. **Original Gemini-era code** (`AccessExporterTemp1/AccessExporterTemp1/`
   per the BATON). Confirmed out-of-scope — drop without import?
4. **`sample-output/` dumps.** Confirmed out-of-scope — drop without
   import; we'll regenerate on demand?
