# Verification Round 1

A. Determinism / gate integrity

The shipped command path is structurally correct for determinism. `ReportBaselineRunner.Run` always writes the gated artifacts into `reportsBaselineRoot`, and in `CaptureOne` the gated manifest path builder only ever adds `"aggregates"` after `_service.CaptureAndWrite(...)` succeeds. There is no code path that adds a `"pdf"` key to `BaselineManifestEntry.Paths`, and the updated `BaselineManifest` docs explicitly state that PDFs are excluded from the gated manifest. When `includePdf` is enabled, the PDF path is built exclusively under `spotCheckRoot` (`<spotCheckRoot>/<report>/output.pdf`), and the separate `SpotCheckManifestWriter` writes only `spot-check-manifest.json` in that sibling tree. The hermetic test `Run_GatedTree_IsByteIdentical_WithAndWithoutIncludePdf` directly compares the gated `baseline-manifest.json` plus `aggregates.json` between runs with and without PDF, and the COM integration test additionally asserts there are zero `*.pdf` files anywhere under the gated baseline directory. On the evidence provided, the CLI’s shipped wiring satisfies the non-contamination requirement.

The one caveat is that the guarantee is not enforced at the `ReportBaselineRunner` API boundary itself. The runner checks that `spotCheckRoot` is non-empty when `includePdf` is true, but it does not validate that `spotCheckRoot` is disjoint from `reportsBaselineRoot`. The CLI computes a proper sibling path, so the shipped command is safe, but a direct library caller could pass an overlapping path and reintroduce PDF contamination into the gated tree. So: current shipped path is sound; the core API is not fully hardened against misuse.

B. Non-blocking contract

The non-blocking behavior is implemented correctly in the runner. Aggregate capture is the gate: `CaptureOne` attempts `_service.CaptureAndWrite(...)` first, captures any exception into `aggregateFailure`, and immediately returns a gated `Failed` entry plus a spot-check `Skipped` entry when aggregation fails. That means `_renderer.Render(...)` is never reached for aggregate failures, unbound reports, or spec-read failures. For successful aggregate capture, the runner creates the gated entry as `Captured` before any render attempt. If rendering succeeds, the spot-check entry is `Rendered`; if rendering throws, the catch block returns the same gated `Captured` entry and records only a spot-check `Failed` entry with `RenderFailed: ...`. Exit behavior in `ReportBaselineCommand` is also derived solely from gated manifest failures (`failed > 0 ? 3 : 0`), so PDF failures cannot affect automation.

The tests back this up well. `Run_RenderFailure_IsNonBlocking_ReportStaysCaptured_SpotCheckRecordsFailure` proves render failure does not change gated status. `Run_RecordsAggregateFailure_AsFailed_WithoutInvokingRenderer` and `Run_WhenAggregateWouldFail_DoesNotAttemptRender_AndRecordsAggregateFailureOnly` prove render suppression on gate failure. The COM integration test exercises a mixed real-world set where some reports are `Captured` and others `Failed` at the aggregate step, while `--include-pdf` remains unattended because failed reports are surfaced as spot-check `Skipped`, not rendered.

C. Honesty / no silent drops

For the displayed-total side, the regenerated `rptInvoice/aggregates.json` is honest and grounded. It records exactly one derivable footer total in `DisplayTotals` (`txtSubTotal` with a supported `Sum(...)` expression), and it records the non-derivable controls in `Skipped` with specific reasons matching the concern: `txtShipping` and `txtTotal` are flagged as `DisplayTotalNonAggregate`, and `txtTax` is flagged as `DisplayTotalUnsupportedFunction` because of `IIf`. That is the right shape for “no silent drops”: unsupported footer expressions are preserved as explicit skips rather than omitted.

For report-level failures, the runner also surfaces all relevant cases instead of dropping them. `ReadSpec` exceptions become gated `Failed` with `ReadSpecFailed: ...`; blank/null `RecordSource` becomes gated `Skipped` with `UnboundReport`; aggregate execution failures become gated `Failed` with `AggregateFailed: ...`. In `includePdf` runs, each of those paths also returns a spot-check `Skipped` entry via `SkippedSpot(...)`, so the optional manifest mirrors the considered report set rather than only listing rendered PDFs. That matches the `SpotCheckManifest` contract: every considered report appears there when PDF mode is enabled. On logic, this concern is satisfied.

D. Test adequacy

The rewritten hermetic suite is strong and covers nearly all of the behavior the session introduced. Specifically:
- sibling-root behavior and no PDF leakage into the gated tree: covered;
- byte-identical gated output with and without `includePdf`: covered;
- render failure non-blocking: covered;
- spot-check `Skipped` entries for unbound and aggregate-failed reports: covered;
- stale spot-check cleanup when `cleanRoot=true`: covered;
- preservation of unprocessed spot-check dirs when `cleanRoot=false` plus recreation of processed dirs: covered;
- `ArgumentException` when `includePdf` is true without `spotCheckRoot`: covered.

The COM integration test is also materially useful: it exercises the real Access path with `--include-pdf`, confirms the gated baseline tree is PDF-free, confirms the sibling spot-check tree exists with its own manifest, confirms captured reports have PDFs there, and confirms aggregate-failed reports are represented as spot-check `Skipped` rather than entering render. Combined with the passing `CanonicalGoldenRegressionTests`, that is good evidence the shipped behavior matches the intended split between gated corpus and non-gated spot-check.

The remaining gap is narrow but real: there is no explicit `includePdf` test for the `ReadSpecFailed` branch asserting that the spot-check manifest records `Skipped` with the `ReadSpecFailed: ...` reason. The code clearly routes that path through `SkippedSpot(...)`, so I do not see a functional defect from the implementation itself, but from a verification standpoint that exact variant of the “both manifests, no silent drop” contract is not directly exercised.

E. Regen correctness

The regenerator is appropriately guarded for normal use. It is a `[SkippableFact]` and immediately `Skip.IfNot(...)` unless `AH_REGEN_GOLDEN == "1"`, so a standard `dotnet test` run with no special environment will skip it and will not touch the repo. It stages captures into a temp directory, then rewrites only the committed `reports-baseline` subtree after successful capture. It also clears the destination subtree first, preventing orphaned golden artifacts from lingering after canonical subset changes.

For normalization, the important part is sound for the current gated artifact set: the helper reuses the same public `GoldenNormalizer` methods the gate uses for `baseline-manifest.json` and `aggregates.json`, which are the only gated files in `reports-baseline/`. The provided host results are strong evidence this is working in practice: `CanonicalGoldenRegressionTests` passed 2/2 byte-for-byte against the regenerated corpus. So the tool is safe against accidental overwrite in normal runs and is aligned with the gate for the current artifact inventory.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "AccessHarvester.Core/ReportBaseline/ReportBaselineRunner.cs",
      "summary": "The runner does not validate that spotCheckRoot is outside and disjoint from reportsBaselineRoot, so a direct caller could place PDFs inside the gated tree despite the CLI's correct sibling wiring.",
      "fix": "When includePdf is true, compare full canonicalized paths and reject spotCheckRoot values that are equal to, nested under, or contain reportsBaselineRoot."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineRunnerTests.cs",
      "summary": "There is no explicit includePdf test covering the ReadSpecFailed path on the spot-check manifest, leaving one skip-reason variant of the 'both manifests, no silent drops' contract unexercised.",
      "fix": "Add a hermetic test with includePdf=true and a source.ReadSpec throw, then assert baseline-manifest.json records Failed/ReadSpecFailed and spot-check-manifest.json records Skipped with the same ReadSpecFailed reason."
    }
  ]
}
```

---

# Verification Round 2

Finding 1: Verified. The new `PathsOverlap()` logic correctly handles the intended cases. `Path.GetFullPath()` removes relative-segment ambiguity, and the trim-then-append-separator normalization makes the prefix check operate on directory boundaries rather than raw name prefixes. That means:
- equal paths are rejected,
- a child under the other root is rejected,
- a parent containing the other root is rejected,
- and the legitimate sibling layout `reports-baseline` vs `reports-baseline-spot-check` is allowed, because `reports-baseline\` is not a prefix of `reports-baseline-spot-check\`.

The guard is also placed correctly: it only runs when `includePdf == true`. So the default aggregates-only path and the existing `includePdf=false` gate path are unaffected.

Finding 2: Verified. The added tests do hit the intended branches and assert the right outcomes:
- `Run_IncludePdf_ReadSpecFailure_GatedFailed_SpotCheckSkippedWithReadSpecReason` exercises the `ReadSpec` exception path under `includePdf=true` and verifies gated `Failed` with `ReadSpecFailed`, spot-check `Skipped` with the same reason, and no renderer call.
- `Run_IncludePdf_WithSpotCheckRootOverlappingGatedRoot_Throws` covers equal, nested, and reverse-nested overlap cases and correctly expects `ArgumentException` for each.
- `Run_IncludePdf_WithSiblingSharingNamePrefix_DoesNotTreatAsOverlap` exercises the allowed sibling-prefix case and proves the runner proceeds normally and writes the PDF under the spot-check root.

I do not see a regression introduced by these fixes. The gated artifact behavior is unchanged, `includePdf=false` execution is untouched by the new guard, and the existing green build/test results are consistent with the code inspection.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
