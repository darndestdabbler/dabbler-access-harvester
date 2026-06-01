# Change Log — Harvester Report-Baseline Golden Corpus

**Status:** COMPLETE (2026-06-01)
**Sessions:** 2 of 2

## Summary
This session set closed the deferred golden-corpus follow-up for `report-baseline`. It committed canonical `reports-baseline/` artifacts under both HomeHealthCare and Northwind, extended the canonical regression harness to diff those artifacts deterministically, updated both fixture guides so downstream agents can discover the new tier, and repaired the CLI-backed integration helper so the report-baseline validation path no longer stalls on natural process exit or lingering child-process trees during local runs.

## Per-session work

### Session 1: Deterministic Northwind date-helper support + live capture proof
- Preloaded saved-query SQL into the report-baseline input path and deterministically substituted the documented Northwind `reportParameterStartDate()` / `reportParameterEndDate()` helpers.
- Threaded the derived defaults through provenance so the canonical capture path stayed explicit and reproducible.
- Validated the helper support with focused report-baseline unit tests and a live Northwind unattended probe over the canonical report subset.

### Session 2: Commit corpus + regression gate + fixture docs + closeout
- Committed `reports-baseline/` golden artifacts for the canonical report subsets under `samples/GoldenOutput/HomeHealthCare/` and `samples/GoldenOutput/NorthwindDevEdition/`.
- Extended `CanonicalGoldenRegressionTests` and `GoldenNormalizer` so `baseline-manifest.json` and per-report `aggregates.json` files participate in the same deterministic golden gate as the existing canonical output tree.
- Repaired `CliInvoker` for the CLI-backed report-baseline path: the helper now avoids blocking on natural process-exit stream drains and explicitly cleans up the report-baseline process tree during validation.
- Restored the committed placeholder contract for report-baseline manifest normalization (`<NORMALIZED:ReportBaseline.Database>`), matching the checked-in corpus.
- Updated both fixture `READ-ME-FIRST.md` files so downstream agents know where the committed report baselines live and which reports are canonicalized.

## Files created or modified
- `samples/GoldenOutput/HomeHealthCare/READ-ME-FIRST.md`
- `samples/GoldenOutput/HomeHealthCare/reports-baseline/`
- `samples/GoldenOutput/NorthwindDevEdition/READ-ME-FIRST.md`
- `samples/GoldenOutput/NorthwindDevEdition/reports-baseline/`
- `tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs`
- `tests/AccessHarvester.Integration/Pipeline/GoldenNormalizer.cs`
- `tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs`
- `docs/session-sets/ROADMAP.md`
- `docs/session-sets/harvester-report-baseline-golden-corpus/ai-assignment.md`
- `docs/session-sets/harvester-report-baseline-golden-corpus/activity-log.json`
- `docs/session-sets/harvester-report-baseline-golden-corpus/change-log.md`
- `docs/session-sets/harvester-report-baseline-golden-corpus/disposition.json`
- `docs/session-sets/harvester-report-baseline-golden-corpus/session-events.jsonl`
- `docs/session-sets/harvester-report-baseline-golden-corpus/session-state.json`

## Verification
- Local TRX validation for `AccessHarvester.Integration.Pipeline.CanonicalGoldenRegressionTests.HomeHealthCare_ReproducesGoldenOutput`: passed.
- Local TRX validation for `AccessHarvester.Integration.Pipeline.CanonicalGoldenRegressionTests.NorthwindDevEdition_ReproducesGoldenOutput`: passed.
- Editor diagnostics for the final changed files: no errors in `tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs` and `tests/AccessHarvester.Integration/Pipeline/GoldenNormalizer.cs`.

## Notes
- During interactive Windows validation, the `dotnet test` wrapper could continue hanging after xUnit had already finished and after the TRX file had been written. The committed helper fix removes the report-baseline-specific natural-exit and child-process-tree stalls from `CliInvoker`; the remaining shell-wrapper behavior was worked around during closeout by treating the TRX artifacts as the authoritative pass/fail source.
- Step 9 reorganization review: no planning-doc changes recommended. `docs/planning/project-guidance.md` already covers the repeated parallel-COM / sample-DB contention patterns in this repo, and the fresh `BaseOutputPath` plus TRX-authoritative wrapper workaround from this set has only one confirmed context so far.