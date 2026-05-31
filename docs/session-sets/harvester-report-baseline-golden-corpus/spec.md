# Harvester Report-Baseline Golden Corpus (lock-free aggregates) — Spec

> **Status:** DRAFT STUB — authored 2026-05-31 as the deferral target of
> `harvester-report-baseline-capture` Session 3. **Not finalized.** Run
> the spec through the canonical
> [`session-set-authoring-guide.md`](https://raw.githubusercontent.com/darndestdabbler/dabbler-ai-orchestration/master/docs/planning/session-set-authoring-guide.md)
> (slug/sizing/Configuration block) before starting.
> **Created:** 2026-05-31
> **Prerequisite:** `harvester-report-baseline-capture` (subcommand,
> aggregates engine, PDF render, parameter handling all shipped there).

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites: [harvester-report-baseline-capture]
totalSessions: 2   # provisional — confirm at finalization
```

---

## Why this set

`harvester-report-baseline-capture` shipped the `report-baseline`
subcommand but **deferred its decision #7** — a committed golden
`reports-baseline/` corpus wired into the byte-comparison regression
gate. The deferral was forced by a real, reproducible blocker:

**The blocker (root cause, confirmed twice).**
`AccessReportSpecReader.ReadSpec` opens each report in hidden **design
view** (the only view where `GroupLevel` is readable). Opening a report
in design view escalates the whole database to an **exclusive lock for
the Access session's lifetime**, which collides with the concurrent
**ACE OLEDB** aggregate query (`OleDbReportQueryExecutor`) running while
the `AccessComSession` is still alive. The prior set reproduced this in
a fresh `Access.Application`: shared-open + ACE OLEDB concurrently → OK;
insert a report design-view open/close → ACE OLEDB then fails on the
lock. In `harvester-report-baseline-capture` S3 (2026-05-30) the same
contention manifested as an indefinite **hang** during capture against
`samples/HomeHealthCare.accdb`.

## Scope

1. **Decouple the COM design-view read from the OLEDB query so capture
   runs lock-free.** Two candidate designs — evaluate and pick:
   - **(a) Phase-decoupling:** open `AccessComSession`, read ALL report
     specs (and render PDFs if `--include-pdf`), **dispose** the COM
     session to release the exclusive lock, THEN open ACE OLEDB and run
     ALL aggregate queries. Minimal behavioral change.
   - **(b) No-COM aggregates (preferred to evaluate first):** compute
     aggregates purely via standalone ACE OLEDB against the
     `RecordSource` (no COM, no design-view read at all). Treat
     `GroupLevels` as **optional** — derive grouping from the query's
     `GROUP BY` / `ORDER BY` where present, or omit group subtotals when
     not derivable. This dissolves the lock rather than sequencing
     around it, and makes aggregate capture hermetic (no live Access).
2. **Capture the golden corpus** for the canonical fixtures
   (`NorthwindDevEdition` — `rptInvoice`, `rptSalesByEmployee`,
   `rptSalesByProduct`; `HomeHealthCare` — its cleanly-capturing
   reports), scoped via `--reports` to reports that capture cleanly
   aggregates-only (per the prior set's decision #7).
3. **Wire the regression gate:** commit `reports-baseline/` artifacts
   under `samples/GoldenOutput/<fixture>/`, add a regression test
   mirroring `CanonicalGoldenRegressionTests` + a `GoldenNormalizer`
   method normalizing the volatile `Provenance.HarvesterVersion` (in
   `aggregates.json`) and `HarvesterVersion` / `Database` path (in
   `baseline-manifest.json`).
4. **READ-ME-FIRST:** add the `reports-baseline/` artifact to
   `samples/GoldenOutput/<fixture>/READ-ME-FIRST.md` for both fixtures
   (the prior set's deferred Step 3.4).

## Out of scope (inherited deferrals)

- `acSaveNo` in-memory `RecordSource` substitution for unattended
  required-parameter render/aggregate.
- A report-parameter clarifying-question type in `QuestionGenerator`.

## Provenance

Deferred from `harvester-report-baseline-capture` Session 3 (2026-05-31).
See that set's `change-log.md` / `disposition.json` and decision #7 in
its `spec.md`.
