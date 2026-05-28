# Harvester Object Cleanup Identifier Spec

> **Purpose:** Add an `AccessHarvester identify-cleanup` subcommand that
> analyses a harvested Access database's KB/IR output and produces a
> JSON-based editable checklist of objects (tables, queries, forms,
> reports, modules) that are likely snapshotted or no longer used.
> The developer and user review the checklist together; the reviewed
> JSON becomes the exclusion list for downstream analysis (see
> `harvester-object-use-identifier`).
> **Created:** 2026-05-28
> **Session Set:** `docs/session-sets/harvester-object-cleanup-identifier/`
> **Prerequisite:** `harvester-schema-v1-adr-followups` (complete).
> `harvester-body-and-query-sql` (soft-prereq: query SQL in KB is
> used for cross-reference detection in Session 2; the subcommand
> degrades gracefully without it but produces fewer signals).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: CLI subcommand reads already-harvested KB/IR JSON files;
> no UI surface, no Access COM required for name-pattern and cross-
> reference detection. The date-signal path uses a lightweight COM
> pass guarded by `[SkippableFact]` when Access is absent. Unit and
> integration tests guard correctness throughout.

---

## Project Overview

After harvesting an Access database the engineer often discovers
objects — tables, queries, forms, reports, stand-alone modules — that
are clearly stale: named with `_OLD` or `_Copy` suffixes, contain a
year in the name, or appear to hold no recent data. Identifying and
excluding these objects before deeper analysis (usage classification,
data classification) saves time and avoids sending the AI engine on
wild-goose chases through objects the business has already abandoned.

This session set implements a detection pass that combines three signal
families:

1. **Name patterns** — object names that carry staleness vocabulary
   (`_OLD`, `_COPY`, `_BAK`, `_ARCHIVE`, `_TMP`, `_TEMP`, years,
   numeric duplicate suffixes).
2. **Date signals** — tables with DateTime columns whose maximum
   value is more than 90 days before the harvest date (requires
   a COM pass or existing DAO `LastUpdated` metadata).
3. **Cross-reference absence** — objects unreferenced by VBA, the
   navigation graph, any row-source binding, or any FK relationship.

Each signal fires at one of three confidence tiers — High, Medium,
or Low — and results are emitted as a `uat-checklist-editor`-
compatible JSON file that the developer and user review together.

### In scope

1. **New subcommand: `AccessHarvester identify-cleanup`.**
   - `identify-cleanup --in <harvested-output-root>` — reads KB/IR
     files from the output produced by `run`; emits two files:
     - `object-cleanup.json` — intermediate structured findings.
     - `object-cleanup-checklist.json` — `uat-checklist-editor`-
       compatible checklist for human review.
   - `identify-cleanup --manifest <path>` — targets a single
     manifest for pipelines that reorganise output.
   - `--json-status` — same structured envelope as other subcommands.
2. **Name-pattern detection (all object types, no COM required).**
   Patterns: `_OLD`, `_COPY`, `_BAK`, `_ARCHIVE`, `_TMP`, `_TEMP`,
   `_HOLD`, `_SAVE`, `_BACKUP`; words "Old", "Copy", "Archive",
   "Snapshot", "Temp", "Backup" (case-insensitive); 4-digit year or
   year-month pattern (e.g., `_2023`, `_Jan2024`, `_Q12025`);
   numeric-suffixed duplicate of a same-base-name object
   (e.g., `frmClients2` when `frmClients` also exists).
3. **Date-signal detection (tables only, COM or DAO metadata).**
   - Primary: sample the max value of every DateTime column via
     a lightweight COM pass (new `SampleDateMaxima` helper, guarded
     by `[SkippableFact]`).
   - Fallback: if Access COM is unavailable, read `LastUpdated`
     from the DAO table metadata already in the KB.
   - Threshold: 90 days before harvest date → "possibly stale".
   - Combined with any name pattern → "High" confidence.
4. **Cross-reference absence detection (no COM required).**
   Reads existing IR: navigation graph (switchboard entries), VBA
   resolver output (DoCmd / Open calls), form/report `RecordSource`
   and control `RowSource` bindings, relationship graph (FK edges).
   Object is "unreferenced" when absent from all four. Confidence:
   "Low" (legitimate utility objects are also unreferenced).
5. **Confidence tiers:**
   - **High** — name-pattern + date-signal both fire.
   - **Medium** — name-pattern only, OR date-signal only.
   - **Low** — cross-reference absence only.
6. **Output: `object-cleanup.json`** (intermediate, machine-readable
   structured findings; not the checklist).
7. **Output: `object-cleanup-checklist.json`** — `uat-checklist-editor`
   format. Fields: `FunctionalArea` = object type, `Subarea` = object
   name, `ItemLabel` = one-line explanation of why flagged,
   `Steps` with one step "Review with user: is this object still
   needed?", `Passes: false`, `Feedback: ""`, `IsJudgmentItem: true`.
8. **JSON Schema for `object-cleanup.json`** committed alongside the
   code.
9. **Tests.** Unit tests for name-pattern matching and cross-reference
   absence against in-memory fixtures. `[SkippableFact]` integration
   tests for the date-signal COM path against `HomeHealthCare.accdb`
   and `NorthwindDevEdition.accdb`.
10. **Pilot calibration.** Session 3 runs the subcommand against the
    committed golden outputs (HomeHealthCare, NorthwindDevEdition),
    reviews results in `uat-checklist-editor`, tunes thresholds and
    description templates, and commits calibrated sample output.

### Out of scope

- Automatic deletion or archiving of flagged objects. This tool
  identifies; the human decides.
- Detection of unused VBA procedures or dead code within modules.
  That is a separate static-analysis concern.
- Cross-database staleness (e.g., linked table targets that no longer
  exist). Only the harvested database's internal references are
  checked.
- Migration generator or SQL Server output. Data classification and
  migration tooling live in `harvester-data-classifier`.
- Generating the uat-checklist-editor HTML tool itself. That tool
  already exists at `uat-checklist-editor`; this session set only
  emits JSON it can consume.

---

## Sessions

### Session 1 of 3: Schema + Detection Strategy

**Steps:**

1. Read `docs/planning/project-guidance.md` and
   `docs/planning/lessons-learned.md`.
2. Route `architecture` task: design the `object-cleanup.json`
   schema (findings per object, signals, confidence tier, fired
   heuristics) and its mapping to the `uat-checklist-editor` JSON
   format.
3. Confirm which signals require Access COM vs. derivable from
   existing KB/IR files; document in
   `docs/planning/object-cleanup-strategy.md`.
4. Commit `docs/planning/object-cleanup-strategy.md` and the JSON
   Schema for `object-cleanup.json`.
5. Cross-provider verification of the schema design.
6. Closeout: update `session-state.json`, append session actuals to
   `ai-assignment.md`.

**Creates:**
- `docs/planning/object-cleanup-strategy.md`
- `AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json`

**Ends with:** Committed schema and strategy document. No production
code yet. Verifier confirms schema covers all three signal families
and maps cleanly to the `uat-checklist-editor` format.

---

### Session 2 of 3: CLI Detection Code

**Steps:**

1. Read the strategy and schema from Session 1.
2. Implement `AccessHarvester.Cli/Commands/IdentifyCleanupCommand.cs`
   and supporting types under
   `AccessHarvester.Pipeline/Cleanup/`.
3. Name-pattern detector: regex/string matching against all object
   names read from the KB manifest.
4. Cross-reference absence detector: reads IR navigation graph,
   VBA resolver cross-reference output, form/report RecordSource
   and control RowSource bindings, relationship graph. Marks
   unreferenced objects at Low confidence.
5. Date-signal detector: lightweight COM pass via a new
   `DateMaximaSampler` helper (analogous to existing COM helpers);
   falls back to KB `LastUpdated` when COM is unavailable.
6. Output writer: emits `object-cleanup.json` and
   `object-cleanup-checklist.json` per the Session 1 schema.
7. Unit tests for name-pattern and cross-reference detectors.
   `[SkippableFact]` for the date-signal COM path.
8. `dotnet build && dotnet test` sequentially.
9. Cross-provider verification of the implementation.
10. Closeout: update `session-state.json`, append actuals to
    `ai-assignment.md`.

**Creates:**
- `AccessHarvester.Cli/Commands/IdentifyCleanupCommand.cs`
- `AccessHarvester.Pipeline/Cleanup/CleanupFinding.cs`
- `AccessHarvester.Pipeline/Cleanup/NamePatternDetector.cs`
- `AccessHarvester.Pipeline/Cleanup/CrossReferenceDetector.cs`
- `AccessHarvester.Pipeline/Cleanup/DateMaximaSampler.cs`
- `AccessHarvester.Pipeline/Cleanup/CleanupChecklistWriter.cs`
- `tests/AccessHarvester.Pipeline.Tests/Cleanup/NamePatternDetectorTests.cs`
- `tests/AccessHarvester.Pipeline.Tests/Cleanup/CrossReferenceDetectorTests.cs`

**Touches:**
- `AccessHarvester.Cli/Program.cs` (dispatcher: new subcommand entry)
- `AccessHarvester.Cli/AccessHarvester.Cli.csproj`

**Ends with:** `AccessHarvester identify-cleanup --in <output>` emits
`object-cleanup-checklist.json`. Unit tests green; `[SkippableFact]`
integration tests green on a Windows machine with Access.

---

### Session 3 of 3: Pilot + Calibration

**Steps:**

1. Run `identify-cleanup` against the HomeHealthCare and
   NorthwindDevEdition golden outputs.
2. Open `object-cleanup-checklist.json` in `uat-checklist-editor`
   and review for clarity and accuracy of `ItemLabel` descriptions.
3. Tune name-pattern vocabulary, confidence thresholds, and
   description templates in `NamePatternDetector` and output writer.
4. Commit calibrated sample output files as golden fixtures under
   `samples/GoldenOutput/<db>/cleanup/`.
5. `dotnet build && dotnet test` sequentially.
6. Cross-provider verification of the full pilot output quality.
7. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, update `session-state.json`,
   append actuals to `ai-assignment.md`.

**Creates:**
- `samples/GoldenOutput/HomeHealthCare/cleanup/object-cleanup.json`
- `samples/GoldenOutput/HomeHealthCare/cleanup/object-cleanup-checklist.json`
- `samples/GoldenOutput/NorthwindDevEdition/cleanup/object-cleanup.json`
- `samples/GoldenOutput/NorthwindDevEdition/cleanup/object-cleanup-checklist.json`
- `docs/session-sets/harvester-object-cleanup-identifier/change-log.md`
- `docs/session-sets/harvester-object-cleanup-identifier/disposition.json`
- `docs/session-sets/harvester-object-cleanup-identifier/next-session-set-recommendation.md`

**Ends with:** Golden sample output committed. Verifier confirms
detection quality and checklist readability. `identify-cleanup` is
a first-class harvester subcommand with calibrated heuristics.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Date-signal COM path unworkable** | Access COM interop for datetime sampling produces unreliable max values or requires 32-bit-only process that conflicts with current hosting | Skip date-signal; emit all date-related findings at Medium (name-pattern only) and document the limitation. Do not block Sessions 1–2 on this. |
| **Cross-reference absence produces excessive false positives** | Pilot in Session 3 shows > 50% of flagged objects are clearly in active use | Raise Low-confidence threshold; consider suppressing cross-reference-absence signal entirely and relying only on name-pattern + date signals. |
| **`uat-checklist-editor` format is incompatible with the findings shape** | The checklist-editor's `Review` array cannot cleanly represent per-object findings with multi-signal explanations | Route `architecture` to design an adapted output format; do not force a bad fit. |

---

## Why now

`harvester-object-use-identifier` and `harvester-data-classifier`
both benefit from operating on only the *current* objects in the
database. Running cleanup identification first gives both downstream
sets a human-reviewed exclusion list, avoiding wasted analysis effort
on objects the business has already abandoned. This set is the
natural first step in the three-set analytical pipeline.
