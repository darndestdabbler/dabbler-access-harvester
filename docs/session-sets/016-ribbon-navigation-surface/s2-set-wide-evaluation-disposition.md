# Set-wide adversarial review — disposition (016)

> The human ran the `s2-set-wide-evaluation-prompt.md` adversarial review in the
> in-IDE GPT-5.4 and Gemini orchestrators (responses:
> `s2-set-wide-evaluation-gpt-5-4.md`, `s2-set-wide-evaluation-reply.md`). This
> records the triage of every finding and the action taken. Post-close follow-up
> (the set was already closed/VERIFIED) — the same pattern as 015's post-close
> in-IDE devil's-advocate fix. Human decision (2026-06-15): implement the
> bindability filter now + re-verify; fix docs + the A5 hardening; defer the
> enhancements as DW.

| # | Finding (severity) | Verdict | Action |
|---|---|---|---|
| F1 / A1 | Callback resolution matched by bare name across **all** procedures (incl. Private / class-module / wrong-kind), so a name collision forces a false `Ambiguous` that hides the real bindable target (**Major**, both reviewers) | **REAL — FIXED** | `BuildProcedureIndex` now gates to **bindable** targets: Public/modifier-less procedures in **standard** modules only. Applies Access's static binding rule (not dynamic interpretation). A name whose only matches are non-bindable → `Unresolved` (Access could not bind it either; `CallbackName` retained). 4 new hermetic tests (class-module collision, private collision, only-private→Unresolved, modifier-less→bindable). HHC golden re-proven byte-neutral; Northwind still resolves `cmdAbout_OnAction`→`modRibbonCallback`. |
| F3 / A7 | `interpretation-guide.md` still says "no ribbon producer exists yet" — stale after 016 (**Major**) | **REAL — FIXED** | Updated the guide (§ NavigationGraph facets + §5.7 + the navigation-facet summary): `Ribbon` + `RibbonEntryPoints` ship (016 / DW-13), omitted on HHC only because HHC has no custom ribbon; documented the coverage boundary (tabs/contextualTabs + root onLoad; `get*`/non-`<tabs>` raw-only per DW-20/DW-21), the null-vs-`[]` semantics, and the bindable-only resolution rule. |
| A5 | A detected-but-empty `USysRibbons` table writes a `rows:[]` raw artifact vs. nothing for a missing table (**Minor**) | **REAL — FIXED** | `RibbonExporter.Export` now returns no artifact when `rows.Count == 0` (an empty ribbon table behaves like a missing one). Raw-tier only; golden-neutral (HHC has no such table; no corpus exercises it — documented, not pinnable hermetically since the exporter is COM-only). |
| A6 | "Unhandled `FormatException` from `RibbonXmlParser.Parse` crashes the pipeline on a false-positive table" (**Major**, in-IDE) | **REFUTED** | `RibbonStructureBuilder.Build` already catches `FormatException` **per row** (records the malformed row, continues) and `Pipeline.BuildNavigationInputs` wraps the whole load in `try/catch` → `RIBBON_LOAD_FAILURE`. A malformed customUI payload degrades to a diagnostic, never a crash. The reviewer did not see the builder's catch. No change. |
| F2 → **DW-23** | Signature-only ribbon-table detection could surface a non-canonical table as the ribbon (**Major** escalation of an S1-disposed Minor) | **DEFERRED (DW-23)** | Low practical risk (needs both columns + valid customUI; raw tier preserves truth; no corpus hits it). Filed DW-23 (prefer canonical `USysRibbons`; demote non-canonical matches to diagnostic) for the ribbon-completeness set. |
| A8 → **DW-22** | `ProcedureReference` reuse: `ModuleName` nullable on the shared type vs. always-present for resolved ribbon entries (**Major**) | **DEFERRED (DW-22)** | Not a bug (no `Resolved` entry ships a null module; routed verifier judged reuse appropriate). A distinct non-null `ResolvedProcedureReference` is an additive contract refinement — filed DW-22. |
| A9 → **DW-22** | No `ParameterCount`/signature captured, so a consumer can't flag a callback bound to a wrong-arity procedure (**Critical**, in-IDE) | **DEFERRED (DW-22)** | The bound procedure's full signature is already recoverable from the manifest's `FunctionClassification`/`ModuleExport`, so nothing is lost — only co-located convenience. Filed DW-22 (signature capture). The bindability filter (F1) already removes the worst mis-bind (wrong module-kind/visibility). |
| A2 | `onLoad` (`IRibbonUI`) vs `onAction` (`IRibbonControl`) signature differ but share the record (**Minor**) | **DOCUMENTED** | `Kind` already discriminates `OnAction` vs `OnLoad`; the interpretation-guide now notes the distinct callback shapes. No code change. |
| A3 | `get*` dynamic callbacks relegated to the `Attributes` bag, not entry points (**Minor**) | **ALREADY DW-20** | Exactly the scope DW-20 carves out (now narrowed: `onLoad` is resolved, only `get*` remains). No new action. |
| A4 | `Attributes` order could drift if Access reorders XML attributes between saves (**Minor**) | **NO CHANGE (by design)** | The parser deliberately preserves **source order** verbatim (faithful-mechanical); the raw customUI XML is the lossless source of record. Sorting attributes would discard authored order. S1 verification already accepted source-order determinism; Access does not reorder a stored `RibbonXML` blob between reads of the same row. |

## Net code change this follow-up
- `NavigationGraphBuilder.BuildProcedureIndex` — bindable-target gate (standard module + Public/default) + `IsBindableCallbackTarget`; doc updates on `RibbonCallbackResolution` / `RibbonEntryPoints`.
- `RibbonExporter.Export` — empty-table guard (A5).
- `docs/harvester/interpretation-guide.md` — ribbon surface described accurately (F3/A7).
- `docs/planning/deferred-work.md` — DW-22, DW-23 filed.
- Tests: 4 new bindability hermetic tests (53 builder / 956 Pipeline.Tests green); HHC golden byte-neutral + Northwind local re-verified via fresh COM runs.
- Routed re-verification of the post-review changes: see `s2-verification.md` (R2).
