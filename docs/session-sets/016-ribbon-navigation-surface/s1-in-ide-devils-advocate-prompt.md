# S1 — In-IDE devil's-advocate review prompt (parser + shape-gate seam)

> **For the human to run** via the subscription-paid in-IDE orchestrator pair
> (GitHub Copilot → **Gemini Pro** AND **GPT-5.4**), per
> `feedback_fable_review_queue_for_high_value_work` and the spec's S1 step 7
> ("in-IDE devil's-advocate pair as primary on the parser + the shape-gate
> seam"). This is **additive to**, not a replacement for, the routed
> cross-provider `session-verification` (`s1-verification.md`).

## Bias-cautions preamble (paste verbatim)

> Bias cautions: This prompt was authored by an AI agent that may have an opinion
> on the answer. Its framing may inadvertently constrain you to in-scope
> refinements when the right answer is to question the scope. The work being
> reviewed may be presented as further along than it should be. Before answering
> as posed, briefly check whether this is the right question. If a different
> question would be more useful, answer that one too.

## Null hypothesis

Each item below is a **real defect** until you disprove it. Steelman the specific
contrarian hypothesis given; do not produce a generic "looks fine" review.

## Files in scope

- `AccessHarvester.Pipeline/Enrichment/RibbonXmlParser.cs` (the parser)
- `AccessHarvester.Pipeline/Enrichment/RibbonStructureBuilder.cs` (selection)
- `AccessHarvester.Core/Extraction/RibbonExporter.cs` +
  `RibbonSignatureDetector.cs` (the COM/DAO shape-gate seam)
- `AccessHarvester.Core/Pipeline.cs` → `BuildNavigationInputs` (wiring + diagnostics)
- `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs` (the `Ribbon*` records)
- `docs/test-cases/harvester-schema.v1.1.json` → `RibbonStructure`/`RibbonControl` $defs
- The design note `s1-ribbon-model-consensus.md`

## Contrarian hypotheses to steelman (one pass each)

1. **Parser drops or mis-parents real content.** Hypothesis: a control element
   that is NOT a direct child of `group`/`splitButton`/`menu`/`box` (e.g. a
   `dialogBoxLauncher` on a group, a `documentControls`/`qat` section, controls
   directly under `tab`) is silently dropped because the parser only walks
   `tabs>tab>group>control(recursive)`. Prove whether any authored customUI
   structure escapes the walk and is lost with no raw-tier-only signal.
2. **Golden neutrality is not airtight.** Hypothesis: there is an input where
   `RibbonExporter` writes `ribbon-xml.json` even though no real custom ribbon
   exists (e.g. an unrelated table that happens to carry `RibbonName`+`RibbonXML`
   columns), changing a consumer's `Navigation` category output unexpectedly —
   or, conversely, a real `USysRibbons` whose system-ness causes it to be
   skipped. Find the false-positive / false-negative detection case.
3. **Determinism break.** Hypothesis: two runs over the same DB can produce
   different `ribbon-xml.json` or different `NavigationGraph.Ribbon` ordering
   (DAO row order, attribute order, multi-table detection order, multi-row
   selection). Find a non-total ordering or an order-dependent attribute bag.
4. **Schema/model contract mismatch.** Hypothesis: the emitted JSON shape does
   not match the updated `harvester-schema.v1.1` `RibbonStructure` $def (a field
   name/casing mismatch, a required field that can be absent, or a recursive
   `Children` the schema rejects), so a strict consumer validation fails on a
   real ribbon. (Note: a local Northwind run already validates green — try to
   find a shape the local corpus does not exercise that would break.)
5. **"Surface don't interpret" violated.** Hypothesis: the parser interprets
   something it should only surface (e.g. inferring a control's semantics from
   `imageMso`, deciding which `onLoad`/`get*` callback "matters", or normalizing
   an attribute value), OR it drops a callback that a faithful extractor should
   preserve. Check the slotting boundary.

## Output

For each item: REFUTED or UPHELD, with the specific evidence (a line, a missing
case, a counter-example input). End with a one-line overall verdict.
