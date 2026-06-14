# Change Log — 011-resolver-and-navigation-wiring

**Set:** `011-resolver-and-navigation-wiring` (006 P3) · **Sessions:** 3 of 3
(**complete**) · **Orchestrator:** Claude (Opus 4.8, all three sessions) ·
**Verification:** S1 gpt-5-4 (routed cross-provider) R1 ISSUES_FOUND → R2
VERIFIED; S2 gpt-5-4 (routed) R1 VERIFIED; S3 in-IDE devil's-advocate pair
(Gemini Pro + GPT-5.4 over GitHub Copilot) R1 ISSUES_FOUND → R2 ISSUES_FOUND,
all findings fixed-in-session or deferred-with-DW.

> **What this set delivered.** The differentiating resolver + Phase-6 enrichment
> capabilities the harvester had built and hermetically tested but never wired
> into production output — the Access-integrated VBA resolver and the navigation
> graph / unresolved-items / classification substrate. This is the proven root
> cause of the Round-2 consumer feedback (R2-1 switchboard navigation, R2-2
> report chains). R2-1/R2-2 themselves were re-scoped out during S2 once the
> spec's "switchboard rows are harvested" premise was disproven, and are carried
> to the authored P4 set `012-switchboard-and-report-chain-edges` (DW-9/DW-10).

## Session 1 of 3 — Resolver Wiring (F-3-4) · VERIFIED (R2)

Wired the resolver's differentiating capabilities into the manifest parse loop.

- **Shared `ProjectScope`** across the parse loop (the `SymbolDbBuilder`
  two-pass pattern: build all module scopes, then resolve) so cross-module
  Public lookup can fire.
- **`AccessObjectModelRegistry` + `owningClass`** built from the already-extracted
  form/report metadata and passed for Form/Report modules, so bare-control and
  `Me`-member lookup fire.
- **`DefaultMemberRegistry` consulted** in `DecideMemberOutcome` via a synthetic
  declaration emitted through the existing `ResolvedToObjectModel` (no breaking
  change to the closed `ResolutionOutcome` hierarchy; resolves the indirection
  `rs!Field → Recordset.Fields` only, never a fabricated field symbol; gated on
  `isBang` + parameterized default member).
- **`SqlStrings` omitted** (schema-valid; the `DynamicSqlAnalyzer` remains
  unwired → DW-8) rather than emitting the forbidden always-empty array.
- Golden: Resolved references `3182 → 4103` (+921); every transition maps to
  F-3-4. Verification R1 raised a Major (shared-scope contamination) closed in R2
  on reachability evidence, and a Minor (owningClass alignment) closed by a pin.

## Session 2 of 3 — Navigation & Enrichment Wiring (F-2-2) · VERIFIED (R1)

Wired the Phase-6 enrichment substrate into the production pipeline off the
existing `resolvedModules` + `crossRefs` seam.

- **`NavigationGraph`** populated in `manifest.json` (`NavigationGraphBuilder.Build`)
  — 22 VBA-derived edges + 22 non-literal unresolved targets.
- **`unresolved-items.json`** populated (`UnresolvedItemAggregator.Build`) — 1414
  items; removed the 010 interim "always empty" note.
- **Enrichment classification content** — `ValidationRules` 0→76, `RowSourceSpecs`
  0→19, `FunctionClassifications` 0→233, `EventHandlerClassifications` 0→81.
- **Scope correction:** re-grounding disproved the spec's R2-1 premise —
  `tblSwitchboardItems` ships `(no data file)` and no harvested artifact carries
  switchboard rows. R2-1 (switchboard tree) + R2-2 (report chains) deferred to a
  P4 set by cross-provider consensus (gpt-5-4 + gemini-pro both Option A) + human
  sign-off → **DW-9 / DW-10**.
- Fixed a pre-existing S1 bug surfaced by the full schema gate:
  `ParserOutputProjector` emitted schema-invalid `"Kind": null` for 325
  ObjectModel targets → omit-null.

## Session 3 of 3 — Consumer Validation + Docs + Coordinated Closeout · ISSUES_FOUND → all dispositioned

Docs / artifacts only — **no C# source or golden output changed.** The committed
golden was COM-confirmed equal to a fresh extraction
(`CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests`, 6/6).

- **Documented the navigation-graph consumer surface** in
  `interpretation-guide.md`: new §5.7 (edge / unresolved-target shapes, join
  keys, the omit-null-vs-empty-array split, the R2-1/R2-2 deferral, determinism),
  §4 envelope key + per-category entry-shape correction, §8 limitation, §3.2
  reading step; corrected stale `SchemaVersion 1.0 → 1.1`.
- **Authored the P4 next set** `012-switchboard-and-report-chain-edges` (spec +
  not-started v4 stub), carrying the Hard Constraints verbatim plus a
  narrow-shape-gated-row-read constraint; repointed DW-9/DW-10.

### Closeout verification — in-IDE devil's-advocate pair (2 prompts × 2 rounds)

Run as a two-provider devil's-advocate pair via GitHub Copilot (Gemini Pro +
GPT-5.4), in place of the routed `gpt-5-4` used in S1/S2. A **set-wide addendum**
prompt extended the critical pass to S1/S2 claims that had been closed on weak
evidence. The pass surfaced **four findings the prior single-provider routed
verifications missed**:

- **C1 (Major) — FIXED.** §5.7/§4 told consumers to join navigation
  `TargetName` to a `Name` field on `Forms`/`Reports` entries; those entries are
  `{ Spec, … }` (name at `Spec.SourceFormName`). Corrected.
- **C9 (Major/Critical) — DEFERRED → DW-11.** `unresolved-items.json` is
  procedure-body-scoped and undercounts header-level (Layer A) and module-level
  (Layer B) unresolved references; demonstrated by probe. Operator-adjudicated to
  defer the code fix (double-emit design decision; golden-neutral) and disclose
  the scoping in §6 (inline `ResolutionReason` is the authoritative signal for
  procedure-scoped refs; module-level refs have no manifest signal yet).
- **C7(b) — doc guard FIXED + DW-12.** A `Resolved` cross-module reference can
  point to a `VBA_RESOLVE_FAILURE` stub (`ParserOutput=null`); latent (not
  triggerable today). §4 now instructs a `ParserOutput` null-check regardless of
  resolved status; latent code asymmetry → DW-12.
- **C7(a), C8 — disproved by both providers** (no action).
- **R2-1 / R2-2 (Major) — FIXED.** R2 caught two factual errors in the
  orchestrator's *own* R1 fixes (§4 `ParserOutput`-optional-on-both;
  §6 module-level-completeness overclaim). Both fixed and ground-truthed.

Operator declined an R3 confirmation: findings had converged to documentation
precision with no code defect, and the R2 fixes were directly verified against
the golden + projector code.

## Deferred work carried out of this set

| ID | What | Owner |
|---|---|---|
| DW-8 | `DynamicSqlAnalyzer` / `FormReferenceResolver` unwired (`SqlStrings` omitted) | future dynamic-sql set |
| DW-9 | Switchboard / ribbon / macro navigation inputs unproduced (R2-1) | `012-switchboard-and-report-chain-edges` |
| DW-10 | Dialog→query→report chain edges not derived (R2-2) | `012-switchboard-and-report-chain-edges` |
| DW-11 | `unresolved-items.json` undercount (header + module-level) — demonstrated | focused `unresolved-items-completeness` set |
| DW-12 | Latent `VBA_RESOLVE_FAILURE` resolved-ref-to-stub asymmetry | future resolver-hardening set |

## Next set

`012-switchboard-and-report-chain-edges` (006's P4) — authored this session;
the ranked critical-path successor (prereqs P3 + P2 met). Recommended Session-1
orchestrator: Claude Code Opus 4.8 @ effort=high (synthesis-heavy). See
`next-session-set-recommendation.md`.
