# Harvester Object Use Identifier Spec

> **Purpose:** Add an `AccessHarvester identify-usage` subcommand
> that analyses a harvested Access database's KB/IR output and
> produces a `uat-checklist-editor`-compatible JSON checklist
> describing, at a high level, how each current (non-stale) form,
> report, and standalone query/table is used. Where intended
> functionality is unclear, the subcommand surfaces clarifying
> questions. The human-reviewed JSON becomes reference data for
> downstream AI work (documentation, migration planning, etc.).
> **Created:** 2026-05-28
> **Session Set:** `docs/session-sets/harvester-object-use-identifier/`
> **Prerequisite:** `harvester-object-cleanup-identifier` (soft-prereq:
> its output is consumed to exclude stale objects; the subcommand
> degrades gracefully without it). `harvester-body-and-query-sql`
> (soft-prereq: query SQL is needed for standalone-object detection
> via query-table references).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: CLI subcommand reads existing KB/IR JSON files only;
> no Access COM required. All signal computation is over committed
> JSON artefacts, making it fully hermetic. Cross-provider AI
> verification at Session 3 guards the quality of usage summaries
> and question templates.

---

## Project Overview

Once stale objects have been identified (see
`harvester-object-cleanup-identifier`), the engineer needs to
understand how the remaining objects are actually used. For a legacy
Access database this is rarely obvious: form names are terse,
queries are entangled, and switchboard navigation is implicit.

This session set analyses the signals already present in the KB/IR
— `RecordSource`, `RowSource`, navigation-graph edges, VBA event
handlers, query SQL, and FK relationships — to produce a per-object
usage summary and a structured checklist of clarifying questions
where the AI cannot determine intent with confidence.

The checklist is consumed by the developer and user together. The
reviewed JSON becomes a durable reference document that downstream
AI (documentation generators, migration planners) can read as
authoritative intent rather than having to re-infer it from raw
metadata.

### In scope

1. **New subcommand: `AccessHarvester identify-usage`.**
   - `identify-usage --in <harvested-output-root>` — reads KB/IR
     and optional cleanup checklist; emits:
     - `object-use.json` — intermediate structured findings.
     - `object-use-checklist.json` — `uat-checklist-editor`-
       compatible checklist.
   - `identify-usage --manifest <path>` — single-manifest mode.
   - `--exclude-cleanup <path>` — reads an
     `object-cleanup-checklist.json`; objects with `Passes: true`
     (confirmed stale) are skipped or annotated as excluded.
   - `--json-status` — standard structured envelope.
2. **Usage classification taxonomy** (defined in Session 1):
   - Main data entry — form backed by a transactional table.
   - Lookup / reference — form or table holding code lists.
   - Reporting — report or select-only query.
   - Navigation / shell — switchboard, menu, or tab-host form.
   - Standalone utility — query or table not referenced by any form
     or report but referenced by VBA or other queries.
   - Possibly unused — absent from all reference signals.
   - Unknown — insufficient signals to classify.
3. **Standalone object detection** — an object is "standalone" when
   it is not used as a row source for any form, report, or control;
   not listed in the navigation graph; has no VBA references; and
   has no FK relationships.
4. **Unclear-intent signals** — object is classified Unknown or
   confidence is below threshold; or expression / validation rule
   references columns with cryptic names; or VBA handler pattern is
   unrecognised.
5. **Clarifying question generation** — per-object question templates
   tuned to classification uncertainty. For example: "No navigation
   entry found — is this form launched from VBA code?", "Query
   references no tables — is it used as a parameter source?"
6. **Output: `object-use.json`** — intermediate machine-readable
   findings (classification, confidence, fired signals, questions).
7. **Output: `object-use-checklist.json`** — `uat-checklist-editor`
   format. Fields: `FunctionalArea` = object type, `Subarea` =
   object name, `ItemLabel` = AI-generated usage summary,
   `Steps` = clarifying questions (one step per question,
   `VerificationMethod: "ui"`), `Passes: false`, `Feedback: ""`,
   `IsJudgmentItem: true`.
8. **JSON Schema for `object-use.json`** committed alongside code.
9. **Tests.** Unit tests for each signal detector and question
   generator against in-memory fixtures derived from HomeHealthCare
   and NorthwindDevEdition KB data. No Access COM required.
10. **Pilot calibration.** Session 3 runs the subcommand against
    the committed golden outputs, reviews in `uat-checklist-editor`,
    tunes summaries and question templates, commits sample output.

### Out of scope

- Generating documentation prose from the reviewed JSON. That is a
  downstream step for future session sets.
- Ranking or prioritising objects for migration. This set classifies
  current usage; migration planning is a separate concern.
- VBA static analysis beyond what the resolver already provides.
  Deep dead-code analysis is out of scope.
- Access COM access. All signals come from already-harvested KB/IR
  files.
- Generating the `uat-checklist-editor` HTML tool. That tool exists;
  this set emits JSON it can consume.

---

## Sessions

### Session 1 of 3: Schema + Analysis Strategy

**Steps:**

1. Read `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`, and the output schema from
   `harvester-object-cleanup-identifier` (to understand exclusion
   format).
2. Route `architecture` task: design the usage classification
   taxonomy, the `object-use.json` schema (findings per object,
   signals, confidence, questions list), and its mapping to the
   `uat-checklist-editor` JSON format.
3. Define the full set of KB/IR signals to be consumed:
   - Form/report `RecordSource` → backing table or query.
   - Control `RowSource` → lookup source.
   - Navigation graph edges → switchboard / button relationships.
   - VBA event handlers: DoCmd.OpenForm, DoCmd.OpenReport, etc.
   - Query SQL table references (from query KB).
   - Relationship graph: FK edges.
4. Define "standalone" and "unclear-intent" detection rules.
5. Define question-generation templates per uncertainty signal.
6. Commit `docs/planning/object-use-identification-strategy.md`
   and the JSON Schema for `object-use.json`.
7. Cross-provider verification of the schema and strategy.
8. Closeout: update `session-state.json`, append actuals to
   `ai-assignment.md`.

**Creates:**
- `docs/planning/object-use-identification-strategy.md`
- `AccessHarvester.Pipeline/UsageIdentification/object-use-schema.json`

**Ends with:** Committed strategy and schema. No production code.
Verifier confirms the taxonomy covers the HomeHealthCare object
inventory and that the uat-checklist-editor mapping is unambiguous.

---

### Session 2 of 3: CLI Analysis Code

**Steps:**

1. Read the strategy and schema from Session 1.
2. Implement `AccessHarvester.Cli/Commands/IdentifyUsageCommand.cs`
   and supporting types under
   `AccessHarvester.Pipeline/UsageIdentification/`.
3. Implement signal collectors: RecordSourceCollector,
   RowSourceCollector, NavigationGraphReader, VbaReferenceReader,
   QueryTableReferenceReader, RelationshipGraphReader.
4. Implement the classifier: combines signals into a usage category
   + confidence score for each object.
5. Implement the question generator: templates per uncertainty signal.
6. Implement optional exclusion reader: skips objects whose
   `Passes: true` in the supplied `object-cleanup-checklist.json`.
7. Implement output writers: `object-use.json` and
   `object-use-checklist.json`.
8. Unit tests for each signal collector and the classifier.
9. `dotnet build && dotnet test` sequentially.
10. Cross-provider verification.
11. Closeout: update `session-state.json`, append actuals.

**Creates:**
- `AccessHarvester.Cli/Commands/IdentifyUsageCommand.cs`
- `AccessHarvester.Pipeline/UsageIdentification/UsageFinding.cs`
- `AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs`
- `AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs`
- `AccessHarvester.Pipeline/UsageIdentification/UsageChecklistWriter.cs`
- Signal collector types (one per signal family, see step 3)
- Unit test files under `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/`

**Touches:**
- `AccessHarvester.Cli/Program.cs` (new subcommand entry)
- `AccessHarvester.Cli/AccessHarvester.Cli.csproj`

**Ends with:** `AccessHarvester identify-usage --in <output>` emits
`object-use-checklist.json`. Unit tests green.

---

### Session 3 of 3: Pilot + Calibration

**Steps:**

1. Run `identify-usage` against HomeHealthCare golden output;
   optionally supply the cleanup checklist from
   `harvester-object-cleanup-identifier` via `--exclude-cleanup`.
2. Open `object-use-checklist.json` in `uat-checklist-editor`.
   Review usage summaries and clarifying questions for accuracy.
3. Run `identify-usage` against NorthwindDevEdition golden output.
4. Tune classifier confidence thresholds, usage-category boundaries,
   and question templates based on pilot review.
5. Commit calibrated sample outputs as golden fixtures under
   `samples/GoldenOutput/<db>/usage/`.
6. `dotnet build && dotnet test` sequentially.
7. Cross-provider verification of output quality.
8. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, update `session-state.json`,
   append actuals to `ai-assignment.md`.

**Creates:**
- `samples/GoldenOutput/HomeHealthCare/usage/object-use.json`
- `samples/GoldenOutput/HomeHealthCare/usage/object-use-checklist.json`
- `samples/GoldenOutput/NorthwindDevEdition/usage/object-use.json`
- `samples/GoldenOutput/NorthwindDevEdition/usage/object-use-checklist.json`
- `docs/session-sets/harvester-object-use-identifier/change-log.md`
- `docs/session-sets/harvester-object-use-identifier/disposition.json`
- `docs/session-sets/harvester-object-use-identifier/next-session-set-recommendation.md`

**Ends with:** Golden fixtures committed. Verifier confirms usage
summaries are accurate and clarifying questions are relevant and
well-phrased. `identify-usage` is a first-class harvester subcommand.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Signal coverage too thin on NorthwindDevEdition** | Pilot shows > 60% of objects classified "Unknown" because the NorthwindDevEdition IR lacks navigation-graph or VBA data | Lower confidence threshold for classification; emit "Standalone utility" as the default for unreferenced objects rather than "Unknown". Document the limitation in the checklist `ItemLabel`. |
| **Question templates produce irrelevant questions** | Pilot review shows questions are generic or inapplicable to the actual object type | Route `architecture` to redesign question templates per object-type category rather than per uncertainty signal. |
| **`uat-checklist-editor` layout is unreadable for large databases** | A database with 200+ objects produces a checklist that is unwieldy in the editor | Add a `--top-n` flag to limit output to the N objects with highest uncertainty; document the flag. Do not change the editor. |

---

## Why now

Once `harvester-object-cleanup-identifier` has identified stale
objects, the next question is: "What do the remaining objects
actually do?" This set answers that question in a structured,
human-reviewable way. The reviewed output becomes authoritative
intent data that every downstream AI step — documentation,
migration planning, data classification — can consume rather than
re-inferring from raw metadata.
