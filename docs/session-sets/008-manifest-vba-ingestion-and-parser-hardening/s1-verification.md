Analysis:

1. On F-3-1 itself: the kb-tier sourcing looks correct and targeted. `Pipeline.EmitManifestArtifacts` now keeps `raw/ModulesAndClasses/*.txt` for standard/class modules, but switches Forms/Reports to `kb/<category>/*.json`, reads `VbaCode`, strips the VBE header, and parses that body. That matches the spec and the existing SymbolDb ingestion pattern instead of re-parsing whole SaveAsText layout dumps. The evidence is consistent with a real fix: regenerated `manifest.json` now has populated form/report `ParserOutput.Items`, `Me.` references appear in volume, and the integration/TRX and broader hermetic suites stayed green. I do not see a module-ingestion regression in the diff; that path is intentionally left on raw `.txt` and existing suites cover it.

2. The degraded-parse signal is also correct for this parser contract. Because `LoadAndParse` defaults through a Recovering strategy, `ModeUsed != Strict` would be a false-positive on clean parses. Using `parseResult.Diagnostics.Count > 0 || parseResult.ModeUsed == Lossy` is the right minimum predicate here, and—most importantly—the caller no longer discards `parseResult`. That closes the silent-discard problem at this site: hard failures still surface via the existing failure path, and degraded-but-surviving parses now emit `VBA_PARSE_DEGRADED`. The new degraded test exercises exactly the previously silent class of problem.

3. The hermetic pins are meaningful. The clean form/report tests would fail if the pipeline regressed to raw whole-file parsing, failed to source from `kb`, or stopped stripping the `CodeBehindForm`/`Attribute VB_*` preamble cleanly. The degraded test would fail if diagnostics were discarded again. The no-code test protects the intended omission semantics and prevents accidental emission of empty/spurious `ParserOutput`. They are well-targeted to the bug mechanism.

4. The `VbeHeaderStripper` extraction appears behavior-preserving for SymbolDb. The implementation is effectively moved verbatim, `KbVbaSourceLoader` now delegates to it, and the unchanged SymbolDb tests staying green is strong evidence that the shared implementation did not alter that path’s behavior.

5. The golden regeneration change is mechanically sound. It is hard-guarded by `AH_REGEN_GOLDEN=1`, restricted to `HomeHealthCare`, uses the same `CorePipeline.Run` plus `GoldenNormalizer` flow the regression gate relies on, normalizes only the pipeline slice, clears/rebuilds `kb/` and `ir/`, and explicitly avoids overwriting the curated `READ-ME-FIRST.md`. It does not touch Northwind. That is the right shape for a reproducible, low-risk regen tool.

6. I do not see a material correctness, determinism, or schema-conformance risk in the shown diff. File enumeration is ordered, schema shape is unchanged, `ParserOutput` remains optional for no-code forms, and `VBA_PARSE_DEGRADED` uses the existing diagnostic envelope with a free-string `Code`. The fact that only `samples/GoldenOutput/HomeHealthCare/manifest.json` changed after the final regen, and that `CanonicalGoldenRegressionTests` plus the full integration TRX passed, is good confirmation that the change is isolated and deterministic enough for this session’s scope.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```