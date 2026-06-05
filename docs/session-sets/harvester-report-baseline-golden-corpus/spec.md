# Harvester Report-Baseline Golden Corpus Spec

> **Purpose:** Commit the canonical `reports-baseline/` golden corpus and
> wire it into automated regression so downstream report-migration work
> can diff aggregate baselines the same way it already diffs `kb/`, `ir/`,
> and `usage` artifacts. The remaining scope is intentionally narrow:
> support the known unattended canonical-report set, not a generic Access
> report-expression interpreter.
> **Created:** 2026-05-31
> **Finalized:** 2026-05-31. The draft stub's lock-hang premise was
> disproved by a live probe on the shipped `report-baseline` path:
> `rptInvoice` captures cleanly and the remaining Northwind reports fail
> on `Undefined function 'reportParameterStartDate'`, not on COM/OLE DB
> lock contention. This set therefore narrows to deterministic support for
> the known `modReportParameters` helper pair plus corpus/regression work.
> **Session Set:** `docs/session-sets/harvester-report-baseline-golden-corpus/`
> **Prerequisite:** `harvester-report-baseline-capture` complete. That set
> shipped the `report-baseline` subcommand, aggregate-first runner,
> preloaded spec-read lifetime handling, PDF render path, manifest output,
> and the HomeHealthCare integration slice.
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification.
> Full tier; no UI, so no UAT/E2E.
> **Status:** Finalized.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites:
  - slug: harvester-report-baseline-capture
    condition: complete
totalSessions: 2
```

> Rationale: this is a CLI/data-artifact follow-up set. The quality bar is
> focused code/tests plus mandatory cross-provider verification; no human
> UAT or browser E2E applies.

---

## Why This Set, Why Now

`harvester-report-baseline-capture` deliberately stopped short of
committing a golden corpus and regression gate. The follow-up stub was
authored under the belief that hidden design-view spec reads still held
the database lock long enough to collide with ACE OLE DB aggregate
capture. The current shipped code and a live 2026-05-31 probe narrow the
remaining problem:

- `ReportBaselineCommand` already preloads report specs inside a short-
  lived `AccessComSession`, disposes that session, then runs aggregate
  capture through `OleDbReportQueryExecutor`.
- A live Northwind probe against the intended canonical set no longer
  hangs. `rptInvoice` captures cleanly.
- The two remaining target reports (`rptTotalsByOwner`,
  `rptTotalsByCategory`) fail because their `RecordSource` SQL calls
  `reportParameterStartDate()` / `reportParameterEndDate()`, which ACE
  OLE DB cannot evaluate directly.
- The helper module's *form-closed* branch is deterministic and already
  documented in the committed fixture output: `start = DateAdd("m", -3,
  DateValue(DMax("OrderDate", "Orders")))`; `end = DateValue(DMax
  ("OrderDate", "Orders"))`.

That makes the remaining code work small and testable. This set should
land the narrow substitution layer for those helper calls, prove the
canonical unattended report set captures cleanly, then commit the
`reports-baseline/` artifacts and regression gate.

## Scope

1. **Support the known canonical Northwind date-helper pair in the
   aggregate path.** Implement the minimum deterministic rewrite needed
   for `reportParameterStartDate()` / `reportParameterEndDate()` in
   report `RecordSource` SQL, using the module's documented default
   behavior when `frmReports` is not open. Do not widen this into a
   generic VBA-function executor.
2. **Capture the committed report-baseline corpus** for the unattended
   canonical set:
   - `NorthwindDevEdition`: `rptInvoice`, `rptTotalsByOwner`,
     `rptTotalsByCategory`
   - `HomeHealthCare`: the clean aggregates-only subset already proven in
     the shipped integration slice (`srptClientBudgets`,
     `srptClientCurrentNeeds`, plus any additional report proven cleanly
     unattended during this set)
3. **Wire the regression gate** under the existing canonical-golden test
   harness. Normalize only the genuinely volatile report-baseline fields:
   `baseline-manifest.json` (`HarvesterVersion`, database path) and
   `aggregates.json` provenance (`HarvesterVersion`). Keep every stable
   aggregate value byte-compared.
4. **Update the fixture READ-ME-FIRST pages** so downstream agents can
   discover the new `reports-baseline/` artifact alongside the existing
   `kb/`, `ir/`, and `usage` outputs.

## Out of Scope

- Generic evaluation of arbitrary Access VBA functions inside OLE DB SQL.
- Generic simulation of `Forms!`, `Reports!`, `TempVars`, `OpenArgs`, or
  other UI/runtime context beyond the explicitly supported canonical
  helper pair above.
- `acSaveNo` in-memory `RecordSource` substitution for unattended
  required-parameter render/aggregate.
- A report-parameter clarifying-question type in `QuestionGenerator`.

---

## Sessions

### Session 1 of 2: Deterministic Northwind Date-Helper Support + Capture Proof

**Steps:**
1. Re-run the shipped `report-baseline` path against the canonical
   Northwind and HomeHealthCare targets and record the actual per-report
   failure modes so the implementation targets only real blockers.
2. Implement the narrow SQL-preparation layer needed for
   `reportParameterStartDate()` / `reportParameterEndDate()` using the
   documented default branch from `modReportParameters` when
   `frmReports` is closed.
3. Add focused hermetic tests around the rewrite/default-date logic and
   a narrow unattended validation slice on the canonical reports.
4. Prove the full intended canonical subset captures without hanging and
   without manual parameter prompts; record the exact report list and any
   explicitly excluded reports in the session artifacts.

**Creates:**
`docs/session-sets/harvester-report-baseline-golden-corpus/ai-assignment.md`

**Touches:**
`AccessHarvester.Core/ReportBaseline/RecordSourceResolver.cs`
`AccessHarvester.Core/ReportBaseline/ReportBaselineService.cs`
`AccessHarvester.Cli/Commands/ReportBaselineCommand.cs`
`tests/AccessHarvester.Tests/ReportBaseline/RecordSourceResolverTests.cs`
`tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineRunnerTests.cs`
`tests/AccessHarvester.Tests/ReportBaselineCommandIntegrationTests.cs`

**Ends with:** The canonical unattended report set captures cleanly on the
shipped CLI path, or any remaining exclusion is explicitly named and
grounded in a non-goal from this spec rather than a stale lock-hang
theory.

**Progress keys:** `session-1/probe-current-path`,
`session-1/support-northwind-date-helpers`,
`session-1/validate-canonical-report-set`

---

### Session 2 of 2: Commit Corpus + Regression Gate + READ-ME-FIRST + Closeout

**Steps:**
1. Capture and commit `reports-baseline/` under
   `samples/GoldenOutput/<fixture>/` for the canonical report set proven
   in Session 1.
2. Extend the canonical golden regression harness and normalizer to diff
   the new report-baseline artifacts deterministically.
3. Update both fixture `READ-ME-FIRST.md` files so downstream agents know
   where the report baselines live and what they represent.
4. Run focused validation, perform closeout, update ROADMAP/change-log,
   and run Step 9 reorganization review.

**Creates:**
`samples/GoldenOutput/HomeHealthCare/reports-baseline/`
`samples/GoldenOutput/NorthwindDevEdition/reports-baseline/`

**Touches:**
`tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs`
`tests/AccessHarvester.Integration/Pipeline/GoldenNormalizer.cs`
`samples/GoldenOutput/HomeHealthCare/READ-ME-FIRST.md`
`samples/GoldenOutput/NorthwindDevEdition/READ-ME-FIRST.md`
`docs/session-sets/ROADMAP.md`

**Ends with:** The committed report-baseline corpus is part of the same
golden-regression discipline as the rest of the canonical fixtures, and
the set closes with the artifact discoverability docs updated.

**Progress keys:** `session-2/commit-report-baseline-goldens`,
`session-2/wire-regression-gate`,
`session-2/update-read-me-first`,
`session-2/closeout`

---

## Deliverables

- A deterministic unattended capture path for the canonical Northwind
  date-range reports.
- Committed `reports-baseline/` golden artifacts under
  `samples/GoldenOutput/HomeHealthCare/` and
  `samples/GoldenOutput/NorthwindDevEdition/`.
- Regression coverage that fails loud on report-baseline drift.
- Updated `READ-ME-FIRST.md` fixture guides that mention the new
  artifact tier.

## Provenance

Deferred from `harvester-report-baseline-capture` Session 3
(2026-05-31). This spec supersedes that set's draft follow-up stub by
incorporating the live probe against the shipped command path and the
current code's preloaded spec-read lifetime behavior.
