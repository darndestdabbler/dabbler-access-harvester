# Session 1 Close Reason — Kb-Tier VBA Ingestion And Golden Regeneration (F-3-1)

**Outcome:** completed; cross-provider verification VERIFIED on round 1
(gpt-5-4, $0.099, substantive 6-point analysis — `s1-verification.md`).

## What shipped

- **F-3-1 (Critical) fixed.** `EmitManifestArtifacts` no longer feeds whole
  SaveAsText form/report dumps to the VBA parser. Forms/Reports now source
  their VBA from the kb tier's `VbaCode` field (everything from the
  `CodeBehindForm` marker onward), stripped via the shared line-based
  `VbeHeaderStripper` extracted from the SymbolDb ingestion path
  (`KbVbaSourceLoader` now delegates to it). ModulesAndClasses ingestion is
  unchanged.
- **The silent diagnostic discard at the call site is closed.** The parse
  result is captured; a degraded parse (`Diagnostics.Count > 0` or `Lossy`
  mode — NOT `ModeUsed != Strict`, which is the strategy default, not a
  health signal) emits a `VBA_PARSE_DEGRADED` warning into the manifest
  `Diagnostics` array. Full per-module surfacing remains Session 3 (F-3-6).
- **Hermetic pins** (`ManifestVbaIngestionTests`, 4 tests, staged temp kb/ir
  tree, no COM): form items, report items, degraded-not-silent, and
  no-code-form omission semantics.
- **Golden regenerated** via the new guarded `RegenerateCommittedPipelineCorpus`
  dev tool (same capture + GoldenNormalizer flow as the regression gate;
  HomeHealthCare only). Sole golden delta: `manifest.json` (kb/ and ir/
  byte-identical).

## Evidence highlights

- Golden manifest: forms with ≥1 parsed item 0/19 → **17/19**; reports 0/12 →
  **9/12**; 140 EventHandler items; `Me.` ×665 (was 0).
- New real catch: `rptClientProfile` parses degraded (Recovering, 3
  diagnostics) — previously silent.
- Integration TRX: `total=61 executed=58 passed=58 failed=0` (matches the 007
  baseline; sanitize quarantine intact). Hermetic: 69+69+789+463 green.
- Expected post-F-3-1 Unresolved wall present and disclosed, not "fixed":
  `ResolutionStatus` Unresolved=2222 vs Resolved=2131 (F-3-4 wiring is the
  P3 set's scope).

## Deferred / handed forward

- `basResetFixture` `VBA_PARSE_FAILURE` (AstBuilder NRE on `Dim step As
  String`) → Session 2 (F-3-2).
- Diagnostics/reason surfacing into schema-visible fields → Session 3 (F-3-6).
