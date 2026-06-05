# Change Log — Harvester Report Displayed-Total Parity

**Status:** COMPLETE (2026-06-01)
**Sessions:** 2 of 2
**Maintainer:** claude-code (Claude Opus 4.8 @ effort=high)
**Target release:** v1.2.0

## Summary
This set made report parity assert the *displayed* total a user actually
reads in the report footer — not just raw column sums — without depending
on a rendered PDF. Session 1 added a focused, dependency-free closed-set
(`Sum`-only) footer-expression evaluator and a footer-control COM reader,
emitting a `DisplayTotals` field on each `aggregates.json` and routing every
non-derivable footer control to `Skipped` with a grounded reason. Session 2
regenerated the committed `reports-baseline/` golden corpus so it carries
those `DisplayTotals` (byte-gated by `CanonicalGoldenRegressionTests`), and
moved the opt-in `--include-pdf` visual spot-check into a separate, non-gated
`reports-baseline-spot-check/` sibling so a non-deterministic PDF can never
contaminate the determinism gate.

## Per-session work

### Session 1: Footer-Expression Display Totals (Core + capture proof)
- Surveyed the 5 canonical reports and fixed the supported closed set to
  exactly `Sum`. Corrected the spec premise that an existing Access-expression
  parser could be reused (none exists); a routed cross-provider analysis chose
  a focused recursive-descent `SumAggregateEvaluator` (tokenizer + parser +
  decimal evaluator) scoped to `=Sum(<arith>)` over numeric result-set columns.
- `ReportAggregateComputer` emits a `DisplayTotals` collection for derivable
  report-footer aggregates and routes everything else (group footers,
  `RunningSum`, unsupported functions like `IIf`, control references,
  unresolved/non-numeric columns, parse errors, overflow) to `Skipped` with
  grounded reasons. `AccessReportSpecReader` captures footer-section text boxes
  via COM (report footer + group footers, excluding page footers) through a
  hermetically-tested `ReportFooterSection` helper. `AggregateContract` stays
  `RecordSourceResultSet` (DisplayTotals is additive; `ContractNote` updated).
- Verification (gpt-5-4): R1 ISSUES_FOUND (two Major — a numeric-literal
  exception path and a silent COM-enumeration swallow; one Minor — coverage);
  all fixed, R2 VERIFIED.

### Session 2: Corpus Regen + Regression + Optional PDF Spot-Check + Closeout
- Regenerated the committed `reports-baseline/` golden corpus for both fixtures
  via a new **guarded `GoldenCorpusRegenerator`** dev tool (`AH_REGEN_GOLDEN`
  env + `Skip.IfNot`) that reuses the exact `GoldenNormalizer` the gate uses, so
  the committed golden is produced by the same normalization the gate compares
  against. Each `aggregates.json` now carries `DisplayTotals` + the updated
  `ContractNote`: `rptInvoice` computes
  `txtSubTotal=Sum((1-[Discount])*[Quantity]*[UnitPrice])=50313.622` with
  `txtShipping`/`txtTotal` (DisplayTotalNonAggregate) and `txtTax`
  (DisplayTotalUnsupportedFunction, `IIf`) recorded in `Skipped`;
  `rptTotalsByOwner`/`rptTotalsByCategory` compute `GrandTotalSum=Sum([OrderTotal])`
  with their group-footer controls Skipped; the two HHC sub-reports get empty
  `DisplayTotals`. `CanonicalGoldenRegressionTests` passes 2/2 byte-for-byte.
- Moved the opt-in `--include-pdf` visual spot-check into a SEPARATE, non-gated
  `reports-baseline-spot-check/` sibling (**Option A**, chosen by unanimous
  cross-provider consensus of gpt-5-4 + gemini-pro). The gated
  `baseline-manifest.json` stays PDF-free; a new `SpotCheckManifest` /
  `SpotCheckManifestWriter` indexes the PDFs; the gated corpus is byte-identical
  with or without `--include-pdf`; a PDF render failure is non-blocking
  (recorded only on the spot-check manifest, never changing the report's gated
  status). `ReportBaselineRunner.Run` gained a `spotCheckRoot` parameter plus a
  `PathsOverlap` disjointness guard so a direct caller cannot place PDFs inside
  the gated tree.
- Propagated the contract to both fixture `READ-ME-FIRST.md`,
  interpretation-guide §5.2, and the ROADMAP.
- Verification (gpt-5-4): R1 ISSUES_FOUND (two Minor — unhardened
  spotCheckRoot/reportsBaselineRoot disjointness at the API boundary; a missing
  `includePdf`+`ReadSpecFailed` spot-check test); both fixed (PathsOverlap guard
  + 3 new tests), R2 VERIFIED.

## Validation
- `dotnet build AccessHarvester.sln`: 0 errors.
- `AccessHarvester.Tests`: 583 passed, 0 failed, 2 skipped (the COM
  report-baseline spot-check integration test PASSED); hermetic
  `ReportBaselineRunnerTests` 18/18.
- 8 other hermetic test projects: green (1,747 tests, 0 failures).
- `CanonicalGoldenRegressionTests` (corpus byte-diff gate): PASSED 2/2.
- Two `AccessHarvester.Integration` COM tests failed as **pre-existing
  known-flaky timeouts unrelated to this set's scope**:
  `ClassifyCommandTests.HomeHealthCare_classify...` (240s timeout — persistent,
  the documented next-up "classify-against-HomeHealthCare timeout" spike) and
  `SanitizeCommandTests.HomeHealthCare_sanitize...` (120s timeout, which PASSED
  in 0.5s on a targeted retry — a confirmed one-off contention flake). Neither
  exercises report-baseline / DisplayTotals / the spot-check.

## Files created or modified (Session 2)
- `AccessHarvester.Core/ReportBaseline/ReportBaselineRunner.cs`
- `AccessHarvester.Core/ReportBaseline/SpotCheckManifest.cs` (new)
- `AccessHarvester.Core/ReportBaseline/SpotCheckManifestWriter.cs` (new)
- `AccessHarvester.Core/ReportBaseline/BaselineManifest.cs`
- `AccessHarvester.Cli/Commands/ReportBaselineCommand.cs`
- `AccessHarvester.Cli/Program.cs`
- `tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineRunnerTests.cs`
- `tests/AccessHarvester.Tests/ReportBaselineCommandIntegrationTests.cs`
- `tests/AccessHarvester.Integration/Pipeline/GoldenCorpusRegenerator.cs` (new dev tool)
- `samples/GoldenOutput/{HomeHealthCare,NorthwindDevEdition}/reports-baseline/**/aggregates.json` (regenerated)
- `samples/GoldenOutput/{HomeHealthCare,NorthwindDevEdition}/READ-ME-FIRST.md`
- `docs/harvester/interpretation-guide.md`
- `docs/session-sets/ROADMAP.md`

## Next session set (routed recommendation)
See [`next-session-set-recommendation.md`](next-session-set-recommendation.md):
**`harvester-fix-classify-timeout`** — profile and fix the persistent
`classify`-against-HomeHealthCare 240s timeout (the `sanitize` half is closed as
a confirmed flake). Recommended orchestrator: Claude Sonnet @ low effort
(well-defined performance debugging, not novel architecture); mandatory
cross-provider session-verification stays cross-provider.
