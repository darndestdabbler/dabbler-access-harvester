# Change Log — 012-switchboard-and-report-chain-edges

**Set complete: 2026-06-14.** 006's **P4**. Delivered the two Round-2 consumer
requests the wiring arc deferred — **R2-1** (switchboard menu rows → structured
navigation; DW-9) and **R2-2** (dialog→query→report chain edges; DW-10) — over
the HomeHealthCare fixture. Full tier, no UI; quality gate = build + tests +
cross-provider verification.

## Session 1 — Switchboard Row Materialization + Entry Points (R2-1, DW-9)

- Shape-gated `SwitchboardExporter` (column-signature detection, narrow ordered
  OleDb row-read — the one new COM/data surface this set was allowed) materialises
  the `tblSwitchboardItems` rows that previously shipped `(no data file)` into
  `raw|kb/Navigation/switchboard-items.json`.
- `Pipeline.BuildNavigationInputs` threads the populated `NavigationGraphInputs`
  (switchboard tree + macro chains + `StartUpForm` default form) into
  `NavigationGraphBuilder.Build`, retiring the `inputs: null`. The HHC golden's
  `NavigationGraph` now carries ordered `EntryPoints`, the generic `Switchboard`
  page tree, and `Macros`. **DW-9 closed.** Ribbon (`USysRibbons`) carved out as
  **DW-13** (no custom-ribbon corpus to validate against).
- gpt-5-4 R2 VERIFIED (2 Major + 1 Minor fixed; 1 Minor — absent-StartUpForm vs
  real DAO read failure — initially accepted-by-design, later hardened in S3-era
  code per the devil's-advocate C6 note).

## Session 2 — Chain-Edge Model + Mechanical Producers (R2-2 part 1, DW-10)

- New `NavigationChainBuilder` derives, purely over the IR surfaces, the closed
  chain-edge family `NavigationGraph.ChainEdges` / `.UnresolvedChainTargets`
  (kinds `OpensDialog|OpensReport|OpensForm|FeedsRecordSource|SuppliesParameter`;
  closed kebab-case provenance set): the embedded-macro launcher→report hop, the
  record-source → query/table edges (incl. inline-SQL FROM/JOIN), the query
  `[Forms]!`/`[Reports]!` references, the `TempVars!` record-source references,
  and prompt-style declared parameters as explicit unresolved targets.
- HHC golden: 37 ChainEdges + 3 UnresolvedChainTargets. gpt-5-4 took 4 rounds to
  VERIFIED — the TempVars recovery moved from a regex to a **token-aware
  single-pass scanner** (skips quoted/bracketed spans wholesale) and the
  form-reference path parser to a **strict 2-or-3-segment** contract, closing the
  fabricated-edge class.

## Session 3 — Code-Behind Chain Edges + Coordinated Closeout (R2-2 part 2, DW-10)

- New **code-behind** producer (`NavigationChainBuilder.CodeBehind.cs`, partial)
  over the resolved VBA modules, provenance `vba-call`: `DoCmd.OpenForm/OpenReport`
  literal dispatches → `OpensDialog` (acDialog) / `OpensForm` / `OpensReport`;
  `Forms!dlg!ctl` / `Reports!rpt!ctl` reads → `SuppliesParameter`; `TempVars!x =`
  setters → `SuppliesParameter` `tempvar-flow` (the supplier side that completes
  the report→`TempVar`→sub-report chain). Explicit unresolved for dynamic targets
  (`non-literal-target`) and record-source TempVars with no harvested setter
  (`tempvar-no-supplier`).
- Owner kind is derived from each module's own `ModuleScope.Kind` (not a
  name→kind map), so a form/report name collision cannot misattribute edges.
- HHC golden: **ChainEdges 37 → 63** (+24 `vba-call` = 13 deduped opens + 11
  control reads; +2 `tempvar-flow` setters); UnresolvedChainTargets unchanged at 3
  (the two new unresolved reasons are golden-neutral, pinned synthetically).
  `kb`/`ir` byte-identical (chain edges live only in the manifest). Every delta
  maps to R2-2. **DW-10 closed.** Residual coverage gaps (named-argument opens,
  indexer/`With` reads, non-bang TempVars supplier edges) → **DW-14**.
- Interpretation-guide §5.7 updated for the full chain surface + the DW-14
  coverage boundary. `interpretation-guide` §8 limitations updated (navigation
  surfaces now mostly complete; `Ribbon` the one null).

### Verification (S3)

- **Routed cross-provider (gpt-5-4): R1 ISSUES_FOUND → R4 VERIFIED.** Caught three
  real false-positives in the new code (member-wrapped LHS write; non-bang TempVars
  setter suppression; `.Add` name-slot scan-forward), all fixed + pinned.
- **Set-wide devil's-advocate (Gemini Pro + GPT-5.4, in-IDE pair) — PRIMARY
  closeout review.** Both independently surfaced **C3** (form/report name-collision
  overwriting owner kind — a wrong-data bug the routed rounds missed); fixed by
  keying owner kind off `ModuleScope.Kind`. Gemini's **C4** setter-syntax gaps
  (`.Item`, `Name:=`) fixed by completing the finite TempVars setter API in the
  supplied-universe scan. C1/C2/C4-edge deferred to DW-14 (golden-neutral coverage,
  accepted by both providers + routed R4). C5/C6/C7 no-action with recorded
  rationale. Routed R4 re-confirmed the C3/C4 fixes VERIFIED.

## Test posture at close

- Hermetic: full `AccessHarvester.Pipeline.Tests` green (incl. 46
  `NavigationChainBuilderTests` pins). COM gates `CanonicalGoldenRegressionTests`
  + `ManifestSchemaConformanceTests` 6/6; HHC golden reproduces byte-identically.

## Deferred work

- **Closed:** DW-9 (S1), DW-10 (S2+S3).
- **Opened:** DW-13 (ribbon surface), DW-14 (navigation-target completeness —
  named-arg opens, indexer/`With` reads, non-bang TempVars supplier edges).
- **Still open (off-theme, not this set's):** DW-11, DW-12.

## Next session set

`013-cli-input-robustness` (006's P5) authored this session (spec + not-started v4
stub) per the routed recommendation. Highest remaining consumer risk: F-1-3 — a
misspelled `--types` value yields a silent empty success (exit 0). Recommended
orchestrator: Sonnet 4.6 @ effort=high.
