# Canonical Parse-Success Report Spec

> **Status:** Elaborated 2026-05-17 by this set's Session 1 (Claude
> Opus 4.7 @ effort=high). The stub authored by
> `remaining-work-planner` Session 2 fixed the contract; this revision
> adds file paths, exact JSON shape, harness wiring, and the
> property-sheet-(d) verdict.
>
> **Purpose:** Produce a committed, diff-against-on-regression
> parse-success report per canonical fixture so Phase 4 Criterion #1
> ("every documented VBA / Jet SQL / property-sheet construct in the
> canonical fixtures parses correctly OR emits an explicit
> `UnresolvedItem` with provenance and reason") is verifiable rather
> than asserted.
> **Created:** 2026-05-17
> **Session Set:** `docs/session-sets/harvester-finish-canonical-parse-success-report/`
> **Prerequisite:** `remaining-work-planner` (the gap-audit + tree
> for Phase 4) and the parser/resolver session sets — all complete.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. Deliverables are parse-success report
> JSON files and a regression test that diffs them. Automated build
> / test plus cross-provider AI verification is the complete quality
> bar.

---

## Origin and authority

- **Authority for scope:** the gap audit at
  [`docs/session-sets/remaining-work-planner/gap-audit.md`](../remaining-work-planner/gap-audit.md)
  → GAP #1.
- **Authority for "Done":**
  [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4
  Criterion #1.
- **Authority for canonical-fixture list:** the planner's resolved
  Open Question 1 — **two fixtures: `NorthwindDevEdition.accdb` +
  `HomeHealthCare.accdb`.** MiniComplex is OUT.
- **Authority for construct scope:** the planner's resolved Open
  Question 2 — **HYBRID, inventory-driven.** This set's Session 1
  inventory pass (committed as
  [`property-sheet-inventory.md`](property-sheet-inventory.md))
  produced the verdict: (a) VBA + (b) Jet SQL + (c) macros are
  definitely in; (d) property-sheet expressions fold in **scoped
  to two dialects** (Access-expression for ValidationRule +
  DefaultValue; Jet-SQL fragment for Filter + OrderBy). Format /
  InputMask / RecordSource / RowSource / ControlSource are OUT of
  parser-coverage scope per the verdict's reasoning.

---

## Project Overview

The harvester has shipped a complete VBA parser
(`AccessHarvester.Vba.Parser` + `Vba.g4`), Jet SQL parser
(`AccessHarvester.Sql.Parser` + `JetSql.g4`), macro action enricher
(`MacroActionsEnricher`), and `UnresolvedItem` /
`UnresolvedItemAggregator` for explicit non-silent fallbacks. What is
missing is a per-fixture, committed artifact that says: *"for
fixture X, here is the count of constructs parsed and the count of
`UnresolvedItem` records emitted, broken down by source."* Without
that, "the parser handles the canonical fixtures" is a claim that
cannot regress visibly when a future grammar edit silently drops a
construct.

This set produces that artifact.

### In scope

1. A `parse-success-report.json` per canonical fixture, committed at
   `samples/parse-success-reports/<fixture>.parse-success-report.json`
   alongside the existing fixture / YAML mirror artifacts.
2. The report enumerates by source:
   - **VBA modules:** total modules / total procedures / parse OK /
     parse errors / `VBA0001` (`ERROR_TOKEN`) lexer-gap counts.
   - **Jet SQL queries:** total queries / parse OK / parse failed.
   - **Macros:** total macro objects / total macro entries /
     structural action-line count. (The macro action enricher runs
     as a live-pipeline enrichment step that requires the full
     extraction pass — wired into the report by
     `harvester-finish-canonical-golden-output`. Session 1 records
     only structural counts.)
   - **Property-sheet expressions** (scoped per the inventory verdict):
     - `validationRules` (table column ValidationRule values)
     - `defaultValues` (table column DefaultValue values; bare literals
       count as parsedOk-no-grammar-required so the bucket measures
       coverage rather than mere occurrence)
     - `formFilters` (form / report Filter values; Jet SQL fragment)
     - `formOrderBys` (form / report OrderBy values; Jet SQL fragment)
3. The report's `unresolvedItems` section carries
   `UnresolvedItem` records produced by `UnresolvedItemAggregator`
   grouped by `Source` and `Reason` with counts and up to 3
   exemplary locations per reason. **In the Session 1 interim, this
   section is populated with `"status": "not-measured"`** — wiring
   the live aggregator requires the full enrichment pipeline
   (resolver + dynamic-SQL + navigation + function-classification),
   which lives downstream in
   `harvester-finish-canonical-golden-output`. The shape stays
   stable when measurement lands; consumers MUST check the `status`
   field rather than treating an absent `totalCount` as zero.
4. A regression test (under `tests/AccessHarvester.Pipeline.Tests/`)
   re-generates the report and asserts byte-equality against the
   committed golden. Failure surfaces the JSON diff.
5. Each new `UnresolvedItem` reason that surfaces during report
   generation gets a one-paragraph note in
   `unresolved-reason-narrative.md` (sibling doc) describing the
   construct and the recommended downstream-AI interpretation.

### Out of scope

- Adding new parser features. If the inventory pass surfaces
  constructs not currently handled, the report records them as
  `UnresolvedItem` entries rather than triggering parser-extension
  work. Parser-extension scope is a separate strategy-level decision.
- Property-sheet inventory beyond the four buckets above. Format /
  InputMask / RecordSource / RowSource / ControlSource are explicitly
  out (per inventory verdict).
- Wiring the harvester's per-run `UnresolvedItems.json` output —
  that's `harvester-finish-canonical-golden-output`'s job. This set
  computes counts post-hoc against existing aggregator output rather
  than relying on a not-yet-wired CLI emission.
- **Strategy.md "Frozen Unless Reauthorized" items remain frozen.**
  Blazor application slices / workflows / pilots, production IR
  codegen, `dabbler-platform-discovery` refresh, additional Northwind
  workflows beyond the existing four pilot packages, CRUD-scaffolder
  expansion, and any item the 2026-05-17 strategic review froze. If
  this set's work surfaces evidence that one of those should be
  reauthorized, surface to the human.

---

## Architecture

### Where the report lives

```
samples/parse-success-reports/
  HomeHealthCare.accdb.parse-success-report.json        ← committed
  NorthwindDevEdition.accdb.parse-success-report.json   ← committed
```

Sibling to `samples/DatabaseYaml/<fixture>/` (the source-of-truth
YAML mirror the report measures coverage against).

### Where the harness lives

```
tests/AccessHarvester.Pipeline.Tests/
  ParseSuccessReport/
    ParseSuccessReportGenerator.cs   ← report-building harness
    ParseSuccessReportModel.cs       ← serialisation POCOs
    ParseSuccessReportTests.cs       ← [SkippableFact]/[Theory] gate
```

Hermetic — reads YAML mirrors from `samples/DatabaseYaml/`, runs the
existing parsers / aggregator, serialises the report. Does **not**
touch Access COM, so it runs on any platform where the YAML mirror
exists.

### Source-of-truth pairing

| Fixture                            | YAML mirror committed?         | Report regenerable in CI? |
|------------------------------------|--------------------------------|---------------------------|
| `HomeHealthCare.accdb`             | Yes (`samples/DatabaseYaml/`)  | Yes — `[Fact]`            |
| `NorthwindDevEdition.accdb`        | No — `.gitignore`'d           | Only locally — `[SkippableFact]` skipping when YAML mirror missing |

The committed Northwind report is the maintainer's snapshot from
their local YAML mirror; regression-test passes on the maintainer's
Windows machine, skips on CI. This matches the existing pattern for
Access-COM-dependent tests (`[SkippableFact]`).

### Report JSON shape (canonical)

```jsonc
{
  "fixture": "HomeHealthCare.accdb",
  "schemaVersion": 1,
  "generator": "AccessHarvester.Pipeline.Tests.ParseSuccessReport",
  "yamlMirror": "samples/DatabaseYaml/HomeHealthCare.accdb",

  "summary": {
    "vbaModules": {
      "totalModules": 32,
      "totalProcedures": 142,
      "parseOk": 32,
      "parseFailed": 0,
      "errorTokenCount": 0
    },
    "jetSqlQueries": {
      "totalQueries": 22,
      "parseOk": 21,
      "parseFailed": 1
    },
    "macros": {
      "totalMacroObjects": 4,
      "totalMacroEntries": 66,
      "actionLines": 39
    },
    "propertySheet": {
      "validationRules":  { "total": 19, "parsedOk": 13, "parseFailed": 6 },
      "defaultValues":    { "total": 12, "parsedOk": 12, "parseFailed": 0 },
      "formFilters":      { "total": 0,  "parsedOk": 0,  "parseFailed": 0 },
      "formOrderBys":     { "total": 0,  "parsedOk": 0,  "parseFailed": 0 }
    }
  },

  "unresolvedItems": {
    "status": "not-measured",
    "note": "<documenting note — see ParseSuccessReportGenerator>"

    // Future shape, when `harvester-finish-canonical-golden-output`
    // wires the live aggregator:
    //
    // "status": "measured",
    // "totalCount": 7,
    // "bySourceAndReason": [
    //   {
    //     "source": "Resolver",
    //     "reason": "not-in-scope",
    //     "count": 4,
    //     "exemplaryLocations": [
    //       { "moduleName": "basUtilities", ... }
    //     ]
    //   }
    // ]
  }
}
```

**Determinism rules:**

- Top-level keys appear in the order above (the serialiser writes
  them in declaration order; the POCO declares them in this order).
- `bySourceAndReason` is sorted by (`source` enum ordinal, `reason`
  ordinal-string). Within each entry, `exemplaryLocations` is capped
  at 3 and sorted by (moduleName, procedureName, line, column) —
  the same ordering the aggregator already uses.
- All `summary` counts are `int`. The `unresolvedItems` section
  uses a `status: "not-measured" | "measured"` discriminator;
  `totalCount` and `bySourceAndReason` are **omitted** while
  `status` is `"not-measured"` and rendered as `int` /
  `UnresolvedReasonGroup[]` once measurement lands. Consumers
  branch on `status`, never on field presence.
  so a future reader can tell "not measured" from "measured zero".
- Indentation: 2 spaces, trailing newline at EOF, UTF-8 with no BOM.
- `surroundingText` is truncated to 80 characters with `...` suffix
  when longer, so the report stays diff-friendly.

### Regeneration

The test class supports two modes:

```bash
# Diff mode (default) — fails on regression
dotnet test --filter "FullyQualifiedName~ParseSuccessReportTests"

# Update mode — overwrites the committed golden, used by the
# maintainer when an intentional grammar / aggregator change shifts
# counts.
UPDATE_PARSE_SUCCESS_REPORTS=1 dotnet test --filter "FullyQualifiedName~ParseSuccessReportTests"
```

The env var is plumbed in this set; downstream sets (notably
`harvester-finish-canonical-golden-output`) can reuse the same
mechanism for their own goldens.

---

## Sessions

### Session 1 of 2: Inventory + Report Shape + First-Pass Generation

**Steps (this session):**

1. Read the gap-audit GAP #1 entry + the stub end-to-end. *(Done.)*
2. Elaborate this stub into the elaborated spec above. *(Done.)*
3. Author `ai-assignment.md` via routed analysis (Rule #17).
4. **Property-sheet inventory pass.** Walk both YAML mirrors,
   enumerate expression-bearing property-sheet fields, classify by
   dialect, write the verdict to
   [`property-sheet-inventory.md`](property-sheet-inventory.md).
   *(Done — verdict pinned.)*
5. **Implement first-pass harness:**
   - `ParseSuccessReportModel.cs` — POCO record types matching the
     JSON shape above. The `UnresolvedItemsSection` carries the
     `status` discriminator with `totalCount` and `bySourceAndReason`
     ignored when null so Session 1 ships `"status":
     "not-measured"` with no false-zero counts.
   - `ParseSuccessReportGenerator.cs` — YAML-mirror loader, per-
     source counters, expression-bucket parsers, and a deterministic
     JSON serialiser. **Does NOT invoke `UnresolvedItemAggregator`
     directly** — the live aggregator requires the full enrichment
     pipeline (resolver / dynamic-SQL / navigation / function-
     classification) which is wired by
     `harvester-finish-canonical-golden-output`. Session 1 emits the
     `"not-measured"` status so the report shape is stable for the
     downstream set to fill in.
   - `ParseSuccessReportTests.cs` — `[Fact]` for HHC,
     `[SkippableFact]` for Northwind. Both run the generator and
     assert byte-equality with the committed golden by default;
     `UPDATE_PARSE_SUCCESS_REPORTS=1` regenerates and overwrites
     instead. The diff-gate contract is live from Session 1; Session
     2 inherits it unchanged.
6. Generate the per-fixture reports by running the harness with
   `UPDATE_PARSE_SUCCESS_REPORTS=1` once. Commit the JSON files as
   the **interim** golden.
7. **Flag surprises for human review BEFORE Session 2 starts.** If a
   fixture's report shows high `errorTokenCount`, surprisingly many
   `parseFailed` queries, or an unexpected `UnresolvedItem` reason
   family, surface them in the session-complete notification body
   so the human sees them before Session 2 layers per-reason
   narrative work on top.
8. `dotnet build && dotnet test` (sequential).
9. End-of-session cross-provider verification (`session-verification`).
10. Commit + push + notify per workflow Step 8.

**Creates:**

- `docs/session-sets/harvester-finish-canonical-parse-success-report/ai-assignment.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/property-sheet-inventory.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/session-reviews/session-001.md`
- `tools/parse_success_inventory.py`
- `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportModel.cs`
- `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs`
- `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportTests.cs`
- `samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json`
- `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`

**Touches:**

- `docs/session-sets/harvester-finish-canonical-parse-success-report/spec.md`
  (stub expansion → this elaborated spec).
- `.gitignore` (adds `tools/_inventory_out/`).

**Ends with:** interim per-fixture parse-success reports committed;
property-sheet (d) verdict recorded; any human-attention surprises
surfaced in the notification.

**Progress keys:** `session-1/spec-elaboration`,
`session-1/property-sheet-inventory`,
`session-1/harness-implementation`,
`session-1/first-pass-reports-committed`,
`session-1/build-test`, `session-1/verify`,
`session-1/commit-push-notify`.

---

### Session 2 of 2: Per-Reason Narrative + Sentinel Test

**Steps:**

1. Confirm the Session 1 diff-gate is still green against a fresh
   regeneration (the diff contract was already live from Session 1).
2. Author `unresolved-reason-narrative.md` (sibling doc) — one
   paragraph per `(Source, Reason)` pair that appears in either
   fixture's report, describing the construct and the recommended
   downstream-AI interpretation. Use the
   `UnresolvedItemAggregator.SuggestedNextStepsLookup` entries as
   ground truth for the recommended next steps. (Session 1's reports
   ship `unresolvedItems.status = "not-measured"`; the narrative
   covers the reason codes the aggregator currently emits, so it is
   reusable when the live wiring lands in the next set.)
3. Add a sentinel test that intentionally mutates one count and
   asserts the diff message is human-readable (confirms the failure
   mode the gate ships with).
4. Run `dotnet build && dotnet test` sequentially.
5. End-of-session cross-provider verification.
6. Closeout: `disposition.json` (per Convention), `change-log.md`
   via `task_type="summarization"` (or in-conversation if
   orchestrator is Opus/Sonnet),
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
7. Step 9 reorganization review of `project-guidance.md` and
   `lessons-learned.md` AFTER notify.

**Creates:**

- `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiffTests.cs`
  (the sentinel test).
- `docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/change-log.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/disposition.json`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/session-reviews/session-002.md`

**Touches:**

- The committed per-fixture golden reports (final pin if any
  Session 2 work shifts counts; otherwise unchanged from Session 1).

**Ends with:** Phase 4 Criterion #1 has a CI gate that fails on
parse-coverage regression; the manual-review queue has a per-reason
narrative the next downstream AI consumer can read.

**Progress keys:** `session-2/diff-gate-reconfirmed`,
`session-2/per-reason-narrative`, `session-2/sentinel-test`,
`session-2/build-test`, `session-2/verify`,
`session-2/closeout-artifacts`, `session-2/commit-push-notify`,
`session-2/step9-reorg`.

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.20 per session (cross-provider
  session-verification + the Session 1 `ai-assignment.md` analysis +
  the Session 2 closeout `summarization`). ~$0.10–$0.40 across the
  set.
- **In-conversation token spend:** Moderate. Inventory pass + report
  shape design are synthesis tasks; the harness wiring is largely
  mechanical.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Inventory reveals property-sheet expressions need a new grammar dialect** | Session 1's inventory uncovers a non-trivial sub-language (e.g., format-string mini-grammar, validation-rule dialect divergence) | Stop. Surface to the human. Either declare property-sheet expressions OUT (per Open Question 2's fallback path) or open a separate parser-extension session set; do not absorb into Phase 4. *(Session 1 verdict: did not trigger — both dialects route through existing grammars.)* |
| **`UnresolvedItem` count explodes** | Report shows hundreds of unresolved items in a single reason family, signalling a parser regression | Stop. Surface to the human. The fix lives in the affected parser/resolver project as a tree-amendment, not in this set. |
| **Two reports cannot agree on counts across runs** | Repeated regeneration produces different counts; signals non-determinism in the aggregator | Stop. Surface to the human. Determinism is the project-guidance.md → VBA Parser Strategy non-negotiable; non-determinism is a strategy-level defect, not a Phase-4-gate adjustment. |

---

## Notes for downstream sessions

- The committed JSON files are the **only** Phase 4 Criterion #1
  artifact. Future sessions that touch the parser, aggregator, or
  enricher chain should re-run
  `dotnet test --filter ParseSuccessReportTests` before committing.
  If counts shift intentionally, regenerate with
  `UPDATE_PARSE_SUCCESS_REPORTS=1 dotnet test ...`, commit the
  diff, and explain the shift in the commit message.
- `harvester-finish-canonical-golden-output` (the next set in the
  Phase 4 tree) will produce the full `raw/kb/ir` golden output and
  may extend this report's `unresolvedItems` section to include
  fully-piped aggregator output (resolver diagnostics, navigation
  graph targets, etc.). The shape above accommodates that — the
  `bySourceAndReason` list grows; the schema stays stable.
