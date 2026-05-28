# Harvester Data Classifier Spec

> **Purpose:** Extend the harvester CLI with three new capabilities:
> (a) a `classify` subcommand that analyses table/column schema and
> emits a best-guess `classification.json` rating each column's
> data sensitivity; (b) a standalone zero-install HTML editor that
> lets a human reviewer confirm or override the guesses; and (c) a
> `sanitize` subcommand that reads the reviewed JSON and the source
> `.accdb` to produce sanitized test data with sensitive values
> replaced by mock data. The classification JSON is the keystone
> contract; both editor and sanitizer are driven by it.
> **Created:** 2026-05-28
> **Session Set:** `docs/session-sets/harvester-data-classifier/`
> **Authority for scope:** `docs/planning/data-classification-plan.md`
> (Phases 0–3). Phase 4 (SQL Server migration generator) is
> explicitly deferred to a future session set.
> **Prerequisite:** `harvester-schema-v1-adr-followups` (complete).
> `harvester-object-cleanup-identifier` (soft-prereq: stale tables
> identified there can be excluded from classification).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: The scanner (Session 2) and sanitizer (Session 4) use
> Access COM and are guarded by `[SkippableFact]` on non-Windows
> machines. The HTML editor (Session 3) is a standalone file with no
> build step. All logic is covered by unit tests (hermetic) and
> optional integration tests. No user-facing UI within this repo.

---

## Project Overview

Access databases used in healthcare, social services, and government
frequently hold PII and sensitive data. Before these databases can
be used for development testing or migrated to SQL Server, their
data must be classified and sanitized. Today that classification is
entirely manual.

This session set implements a lightweight, transparent pipeline that:

1. Reads the Access schema (via OLE DB and DAO interop) and applies
   structural heuristics to produce a best-guess data classification
   for every column in every (current) table.
2. Presents those guesses to a human reviewer in a purpose-built
   HTML editor that shows the fired heuristics and confidence and
   lets the reviewer confirm, override, or annotate each guess.
3. Uses the reviewed classification to produce sanitized test data —
   replacing sensitive values with mock data while preserving
   referential integrity.

The full algorithm, heuristics, category taxonomy, JSON schema, and
Access-specific gotchas are documented in
`docs/planning/data-classification-plan.md`, which is the authority
for this session set's scope. All decisions recorded there should be
re-read before each session begins.

### In scope

1. **`classification.json` schema** (Session 1 contract phase).
   Finalise the schema per `data-classification-plan.md` § 6.
   Validate against the test-data generator and HTML editor on paper
   before any scanner code is written. Commit a JSON Schema file and
   a sample fixture.
2. **`AccessHarvester classify` subcommand** (Session 2).
   - Reads schema via OLE DB (`GetOleDbSchemaTable` for TABLES,
     COLUMNS, PRIMARY_KEYS, FOREIGN_KEYS) and DAO interop
     (AutoNumber attribute, implicit "Lookup" FK from combo-box
     `RowSource`).
   - Heuristics: lookup-table detection (non-key non-system column
     count), PII name match (adapted from Presidio vocabularies),
     PII value-sample regex (no raw values stored — format patterns
     only), system/audit column detection (name + multi-table
     recurrence), PII veto rule (PII signal overrides lookup guess).
   - Cardinality query: `SELECT COUNT(*) FROM (SELECT DISTINCT col
     FROM tbl)` (no `COUNT(DISTINCT)` in Jet/ACE SQL).
   - Access gotchas from plan § 8: bitness-locked ACE OLE DB, DAO
     for AutoNumber, memo/OLE Object/multi-value flags, system-table
     exclusion (`MSys*`), linked-table flagging.
   - Emits `classification.json` conforming to the Session 1 schema.
   - Optional `--exclude-cleanup <path>`: skips tables whose
     `Passes: true` in `object-cleanup-checklist.json`.
3. **HTML classification editor** (Session 3).
   - **Open decision:** default to a new standalone repo
     (`dabbler-classification-editor` or similar, parallel to
     `uat-checklist-editor`). Override at session start if the
     human prefers a static file under `tools/` instead.
   - Zero-install single-file HTML; no build step; no server.
   - Loads `classification.json` from the local file system.
   - Hierarchical view: database → tables → columns.
   - Per-column: option buttons for the 10-category taxonomy (plan
     § 3.5), preserve-autonumber checkbox, masking-strategy selector.
   - Per-table: human override for lookup classification.
   - Displays fired heuristics, confidence, and informational
     referenced-by count alongside each AI guess.
   - Saves the modified JSON back to the file system.
   - Privacy constraint: the JSON never contains raw sampled values
     (enforced by the Session 1 schema); the editor must never
     display or expose raw values either.
4. **`AccessHarvester sanitize` subcommand** (Session 4).
   - Reads reviewed `classification.json` and source `.accdb`.
   - Masks per category (plan § 3.5): replace PII with mock data,
     generalise quasi-identifiers, preserve lookup/FK/system values,
     remap surrogate PKs in lockstep everywhere they appear (PK +
     every referencing FK).
   - FK consistency invariant: never fake a FK column independently;
     always remap with its referencing PK.
   - Preserve-autonumber: if human-flagged as filing-significant,
     annotate for `SET IDENTITY_INSERT ON` (SQL Server context).
   - Memo/free-text: scrub strategy (configurable; default: replace
     with deterministic Lorem-style placeholder).
   - Output format — sanitized `.accdb` or CSV export — is a
     decision to be made at Session 4 start and documented in the
     actuals block.
   - Optional `--exclude-cleanup <path>`: skips tables flagged stale.
5. **Tests** throughout: unit tests for all heuristic logic;
   `[SkippableFact]` integration tests for COM-dependent paths.

### Out of scope

- **Phase 4: SQL Server migration generator** — deferred to a future
  session set.
- Running Presidio itself. Patterns and vocabularies are lifted as
  a seed; Presidio is not a runtime dependency.
- Schema authoring. The `classify` subcommand emits the schema;
  it does not alter it.
- Automatic remediation of classification errors. The editor
  surfaces guesses; humans decide.
- Validation against schema versions other than the one defined in
  Session 1.

---

## Sessions

### Session 1 of 4: JSON Contract

**Steps:**

1. Read `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`, and
   `docs/planning/data-classification-plan.md` in full.
2. Route `architecture` task: finalize the `classification.json`
   schema, validating it against the test-data generator (Session 4)
   and HTML editor (Session 3) on paper. Confirm the schema satisfies
   the privacy rule (§ 4 of the plan: no raw sampled values).
3. Commit the JSON Schema file and a realistic sample fixture.
4. Cross-provider verification of the schema.
5. Closeout: update `session-state.json`, append actuals.

**Creates:**
- `AccessHarvester.Pipeline/Classification/classification-schema.json`
- `samples/classification-fixture.json`

**Ends with:** Committed schema and fixture. Verifier confirms the
schema covers all 10 categories, the humanOverride block, the
privacy constraint, and FK/autonumber preserve flags. Both generator
(Session 4) and editor (Session 3) are validated on paper.

---

### Session 2 of 4: Scanner CLI

**Steps:**

1. Read the schema and fixture from Session 1 and the plan § 3, § 8.
2. Implement `AccessHarvester.Cli/Commands/ClassifyCommand.cs` and
   supporting types under `AccessHarvester.Pipeline/Classification/`.
3. OLE DB schema reader: TABLES, COLUMNS, PRIMARY_KEYS, FOREIGN_KEYS
   via `GetOleDbSchemaTable`.
4. DAO interop layer: AutoNumber attribute (`dbAutoIncrField`),
   implicit FK from combo-box `RowSource` property.
5. Heuristics engine: lookup detector, PII name matcher (Presidio-
   seeded vocabulary), value-sample regex (format patterns only),
   system/audit column detector, PII veto rule.
6. Cardinality sampler: subquery pattern for Jet/ACE.
7. Output writer: emits `classification.json` per Session 1 schema.
8. Optional exclusion reader: `--exclude-cleanup`.
9. Unit tests for all heuristic logic. `[SkippableFact]` integration
   tests against `HomeHealthCare.accdb`.
10. `dotnet build && dotnet test` sequentially.
11. Cross-provider verification.
12. Closeout: update `session-state.json`, append actuals.

**Creates:**
- `AccessHarvester.Cli/Commands/ClassifyCommand.cs`
- `AccessHarvester.Pipeline/Classification/ClassificationFinding.cs`
- `AccessHarvester.Pipeline/Classification/SchemaReader.cs`
- `AccessHarvester.Pipeline/Classification/HeuristicsEngine.cs`
- `AccessHarvester.Pipeline/Classification/PiiDictionary.cs`
- `AccessHarvester.Pipeline/Classification/ClassificationWriter.cs`
- Unit and integration test files

**Touches:**
- `AccessHarvester.Cli/Program.cs` (new subcommand)
- `AccessHarvester.Cli/AccessHarvester.Cli.csproj`

**Ends with:** `AccessHarvester classify --in <accdb>` emits
`classification.json`. Integration test on HomeHealthCare green.

---

### Session 3 of 4: HTML Classification Editor

**Steps:**

1. Confirm the open decision: new repo vs. in-repo file. Document
   the choice in the Session 3 actuals block.
2. Build the single-file HTML editor:
   - File-open / file-save for `classification.json`.
   - Hierarchical tree: database → tables → columns.
   - Per-column: 10-category radio group (plan § 3.5 taxonomy),
     preserve-autonumber checkbox, masking-strategy selector.
   - Per-table: lookup-override checkbox.
   - Display: fired heuristics, confidence score, referenced-by
     count (informational), current AI guess.
   - Save writes modified JSON (humanOverride fields updated).
   - Privacy: never display raw sampled values; the schema prevents
     them from being present.
3. Test the editor manually against `samples/classification-fixture.json`.
4. Commit the HTML file to its chosen location.
5. Cross-provider verification of the editor's UX and correctness.
6. Closeout: update `session-state.json`, append actuals.

**Creates (if new repo):**
- New repo `dabbler-classification-editor` with `index.html` (or
  equivalent); document the repo link in this spec's actuals.

**Creates (if in-repo):**
- `tools/classification-editor.html`

**Ends with:** A reviewer can load `samples/classification-fixture.json`,
override several category guesses, check the preserve-autonumber
box on a PK column, and save the result. The saved JSON contains
correct `humanOverride` entries and no raw values.

---

### Session 4 of 4: Test-Data Generator CLI

**Steps:**

1. Read the reviewed `classification.json` schema from Session 1
   and confirm the masking strategy mapping from plan § 3.5.
2. Implement `AccessHarvester.Cli/Commands/SanitizeCommand.cs` and
   supporting types under `AccessHarvester.Pipeline/Sanitization/`.
3. Classification reader: parses `classification.json`; resolves
   effective category (human override wins over AI guess).
4. FK graph builder: walks relationships to find all PK↔FK pairs
   that must be remapped in lockstep.
5. Masking engine: per-category handlers — PII mock replacement,
   quasi-identifier generalisation, free-text scrub, lookup/FK/
   system preservation, surrogate PK remap.
6. Output writer: decide at session start — sanitized `.accdb` or
   CSV; document the decision in the actuals block.
7. Unit tests for each masking handler and the FK graph builder.
   `[SkippableFact]` integration test against `HomeHealthCare.accdb`.
8. `dotnet build && dotnet test` sequentially.
9. Cross-provider verification.
10. Closeout: `disposition.json`, `change-log.md`,
    `next-session-set-recommendation.md`, update `session-state.json`,
    append actuals.

**Creates:**
- `AccessHarvester.Cli/Commands/SanitizeCommand.cs`
- `AccessHarvester.Pipeline/Sanitization/MaskingEngine.cs`
- `AccessHarvester.Pipeline/Sanitization/FkGraphBuilder.cs`
- `AccessHarvester.Pipeline/Sanitization/MaskingHandlers/*.cs`
- Unit and integration test files
- `docs/session-sets/harvester-data-classifier/change-log.md`
- `docs/session-sets/harvester-data-classifier/disposition.json`
- `docs/session-sets/harvester-data-classifier/next-session-set-recommendation.md`

**Touches:**
- `AccessHarvester.Cli/Program.cs` (new subcommand)
- `AccessHarvester.Cli/AccessHarvester.Cli.csproj`

**Ends with:** `AccessHarvester sanitize --classification <json> --in <accdb>`
produces sanitized output. FK consistency invariant verified by
integration test: no orphaned rows after remapping.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **ACE OLE DB bitness mismatch** | 64-bit host cannot load 32-bit ACE provider; schema reading fails | Switch to DAO-only schema reading or spawn a 32-bit helper process. Do not ship a classifier that silently reads no schema. |
| **PII veto produces excessive false positives** | Pilot review shows > 30% of legitimate lookup tables are incorrectly vetoed as PII-bearing | Tighten the PII name-match vocabulary; raise the value-sample match threshold from one match to two or more. |
| **FK remapping in Session 4 is too complex for `.accdb` output** | Remapping surrogate PKs in lockstep across an `.accdb` file requires DAO transaction management that proves fragile | Pivot output to CSV; document that the CSV approach requires the consumer to re-import with correct identity values. |
| **HTML editor open decision produces disagreement** | New-repo approach requires cross-repo coordination that stalls Session 3 | Default to in-repo `tools/classification-editor.html`; plan a repo-extraction follow-up if needed. |

---

## Why now

`HomeHealthCare.accdb` is the primary sample database for the
harvester, and it holds realistic healthcare data fields
(`ClientID`, `DateOfBirth`, `SSN`-pattern columns). Before the
harvester output from this database can be shared broadly for
development testing, its data must be classified and the sensitive
values sanitized. This session set is the toolchain that makes that
possible for any Access database in the migration pipeline.
