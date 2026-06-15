# Change log — `016-ribbon-navigation-surface`

**Status:** COMPLETE (2 of 2 sessions) — 2026-06-15. Closes **DW-13**.
**Orchestrator:** Claude Opus 4.8 (`claude-code` / `anthropic`), S1 effort medium, S2 effort high.
**Verification:** routed cross-provider `session-verification` (gpt-5-4) — S1 VERIFIED (R3), S2 VERIFIED (R1).

## What shipped

A complete Microsoft Access custom-ribbon navigation producer: from the raw
`USysRibbons` customUI XML to a resolved ribbon→VBA entry-point surface in the
manifest. `NavigationGraph.Ribbon` (structure) and
`NavigationGraph.RibbonEntryPoints` (resolved callbacks) now populate on a
custom-ribbon database; both stay omitted (byte-neutral) on a database with no
custom ribbon.

### Session 1 — RibbonStructure facet (`NavigationGraph.Ribbon`)
- **Shape-gated `USysRibbons` row-read** — `RibbonExporter` + `RibbonSignatureDetector`
  detect a table carrying both `RibbonName` and `RibbonXML` columns (signature, not
  name) and emit the verbatim `(RibbonName, RibbonXML)` rows to
  `raw/Navigation/ribbon-xml.json`. A database without `USysRibbons` produces nothing.
- **Pure `RibbonXmlParser`** — namespace-tolerant (2006/01 + 2009/07, matched by local
  name) → recursive `RibbonStructure` (Tabs/ContextualTabSets → RibbonTab → Groups →
  RibbonControl, each with `Id`/`IdMso`/`Label`, open-string `ControlType`, static
  `OnAction`, verbatim `Attributes`, nested `Children`; root `OnLoad`). Faithful: custom
  AND built-in nodes surfaced, unslotted attributes preserved verbatim; no
  `imageMso`/dynamic-callback interpretation.
- **Wiring** — `Pipeline.BuildNavigationInputs` reads the artifact through
  `RibbonStructureBuilder` (deterministic single-ribbon selection) into
  `NavigationGraphInputs.Ribbon`, so `NavigationGraphBuilder.Build` emits
  `NavigationGraph.Ribbon`.
- **Model extended** per gpt-5-4 + gemini-pro consensus (`s1-ribbon-model-consensus.md`);
  `harvester-schema.v1.1` `RibbonStructure` `$def` updated (backward-compatible).
- Routed VERIFIED after 3 rounds (empty→null attr contract, dropped-content fidelity,
  tie-break determinism, signature-only detection — disposed/fixed). New **DW-20** /
  **DW-21** filed for the deliberately-out-of-scope dynamic callbacks and non-`<tabs>`
  sections.

### Session 2 — Ribbon entry points (`NavigationGraph.RibbonEntryPoints`)
- **New dedicated collection** `NavigationGraph.RibbonEntryPoints:
  IReadOnlyList<RibbonEntryPoint>?` — chosen over extending the startup-roots
  `EntryPoints` list by unanimous cross-provider design consensus
  (`s2-ribbon-entry-point-consensus.md`: both gpt-5-4 and gemini-pro picked the
  dedicated collection, matching the repo's separate-collection pattern for distinct
  navigation surfaces). Nullable → omitted when no ribbon → HHC byte-neutral.
- **`RibbonEntryPoint`** record: `Kind` (OnAction | OnLoad), verbatim `CallbackName`,
  control metadata (`ControlType`/`ControlId`/`ControlIdMso`/`ControlLabel`),
  `Resolution` (Resolved | Unresolved | Ambiguous), and `ResolvedProcedure`
  (`{ModuleName, ProcedureName}` on a unique match; null otherwise).
- **Derivation** — `NavigationGraphBuilder.BuildRibbonEntryPoints` walks the ribbon in
  deterministic source order (root `onLoad` first, then DFS tabs → groups → controls →
  children, then contextual tab sets) and resolves each callback name (case-insensitive,
  VBA semantics) against a procedure-name index of the harvested modules: exactly one
  match → `Resolved`; none → `Unresolved` (verbatim name retained, never dropped);
  more than one module → `Ambiguous` (no guessed module). Faithful-mechanical: name
  match only, no signature/visibility interpretation.
- **Schema** — `RibbonEntryPoint` `$def` + optional `RibbonEntryPoints` on
  `NavigationGraph` (additive, not `required`, no envelope bump).
- Routed VERIFIED (R1, clean) — closeout-grade packet embedded the S1 surface S2
  consumes per the cross-session-evidence convention.

## Validation
- `dotnet build`: 0 errors.
- `AccessHarvester.Pipeline.Tests`: **952 passed / 0 failed** (10 new ribbon-entry-point
  hermetic tests: resolved / unresolved / ambiguous / case-insensitive / onLoad-first /
  nested-child DFS / contextual-tab / present-but-empty / built-in idMso / no-ribbon-null).
- **HHC golden reproduced byte-level** by a fresh COM run
  (`CanonicalGoldenRegressionTests` PASSED) — neutral, proven not asserted.
- **NorthwindDevEdition** validated **locally** (`NorthwindLocalInvariantTests` PASSED;
  proprietary, never committed): `NavigationGraph.Ribbon` parses the real ribbon and
  `RibbonEntryPoints` resolves `cmdAbout_OnAction` → `modRibbonCallback`, surfaces the
  nested `ddMRU_OnAction`, and every entry carries a resolution status.

## Post-close set-wide adversarial review (2026-06-15)

After closeout, the human ran the set-wide in-IDE devil's-advocate prompt
(`s2-set-wide-evaluation-prompt.md`) in GPT-5.4 + Gemini. Triage:
`s2-set-wide-evaluation-disposition.md`. Outcome — three fixes landed (re-verified
gpt-5-4 R2 VERIFIED, golden re-proven byte-neutral):
- **F1/A1 (Major, both reviewers) — fixed.** `BuildProcedureIndex` now gates
  callback resolution to **bindable** targets (Public/modifier-less procedures in
  **standard** modules), so a same-named Private / class-module procedure no longer
  forces a false `Ambiguous` that hid the real target. Applies Access's static
  binding rule; a non-bindable-only collision now resolves to `Unresolved`. 4 new
  hermetic tests (956 Pipeline.Tests).
- **F3/A7 (Major) — fixed.** `interpretation-guide.md` no longer says "no ribbon
  producer exists"; it now describes the shipped 016 surface, the coverage boundary
  (DW-20/DW-21), and the null-vs-`[]` semantics.
- **A5 (Minor) — fixed.** A detected-but-empty `USysRibbons` writes no artifact.
- **A6 refuted** (FormatException already guarded). **F2 → DW-23**, **A8/A9 → DW-22**.

## Deferred-work ledger
- **DW-13 CLOSED** (this set) — moved to the Closed section, full entry retained with a
  CLOSED banner.
- **DW-20 narrowed** — the root `onLoad` is now resolved (S2); only the per-control
  `get*` dynamic-callback family remains deferred.
- **DW-21 unchanged** — non-`<tabs>` customUI sections (`<qat>`/`<officeMenu>`/
  `<backstage>`) remain raw-only in the structured view.

## Next-session-set handoff
**Consumer-driven-only STOP** (routed, `next-session-set-recommendation.md`): the
harvester arc stays consumer-driven; no set is self-initiated. Pre-scoped bundles are
recorded (ribbon-completeness = DW-20+DW-21; navigation-completeness = DW-14; encoding =
DW-5+DW-6; parser edge cases = DW-1..4; reference-output = DW-8+DW-17+DW-19). **DW-18**
(intrinsic-constant registry) is flagged as the one most likely to force action and the
only **golden-affecting** item.

## Set artifacts
`session-state.json`, `activity-log.json`, `session-events.jsonl`, `ai-assignment.md`
(with actuals), `s1-*` (S1 records), `s2-ribbon-entry-point-consensus.md`,
`s2-verification.md`, `s2-issues.json`, `s2-set-wide-evaluation-prompt.md`,
`next-session-set-recommendation.md`, `disposition.json`, this `change-log.md`.
