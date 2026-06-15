# 015 Session 1 — Layer A design note (re-grounding result)

> Re-grounded against the current tree (post-014). This records the seam and the
> chosen design so the focused S1 execution starts precisely. **Status: design
> decided; implementation not yet started.**

## Re-grounding (confirmed on the current tree)

- **Aggregator call site:** `AccessHarvester.Core/Pipeline.cs:655` —
  `UnresolvedItemAggregator.Build(navigationGraph: navigationGraph,
  functionClassifications: classifications)`. The comment at 651–654 explicitly
  feeds **only** `functionClassifications` (not `resolverDiagnostics`) to avoid
  double-emitting a construct under two `UnresolvedSource` values.
- **Why header refs are dropped (Layer A root cause):**
  `FunctionClassificationBuilder.ClassificationVisitor.Visit(ParserProcedure)`
  (`AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs:177-187`)
  **deliberately walks only `procedureAst.Body`**, skipping the header — by design,
  because header type-refs are not runtime behaviour to classify. So
  `FunctionClassification.UnresolvedReferences` is body-only, and a header
  unresolved ref (an `Optional`-param default expression) never reaches
  `unresolved-items.json`.
- **Why the same header ref DOES reach `Body.References`:**
  `ParserOutputProjector.GroupReferencesByProcedure`
  (`AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs:120-138`) groups
  `resolvedModule.References` by enclosing **ProcedureScope** — which includes the
  header. So `resolvedModule.References` is the source of truth that carries the
  header ref; the body-only classification feed is the gap.

## Chosen design (lowest-risk, dedup-safe, provably golden-neutral on HHC)

**Dedup-additive feed.** Keep the existing `FunctionClassification.UnresolvedReferences`
feed unchanged (body refs → byte-identical output), and **additionally** feed the
aggregator the **procedure-scoped unresolved references from
`resolvedModule.References`** (the exact set the projector groups into
`Body.References`, filtered to `ResolutionStatus == Unresolved`), **deduplicated**
against the body-only set by a stable key (module + procedure + line + column +
reason). Net-new items = exactly the **header** refs.

- **Why golden-neutral on HHC:** DW-11 establishes HHC has **zero** header/module-
  level unresolved cases, so net-new = 0 and the dedup drops every body ref the
  FunctionClassification feed already emitted → `unresolved-items.json` is
  byte-identical. (Must still be **proven**, not asserted — see verification path.)
- **Why no double-count:** the dedup key suppresses any procedure-scoped ref the
  FunctionClassification feed already carries; only header refs (absent from the
  body-only set) survive as net-new.
- **Superset property:** with the resolver-reference feed, every procedure-scoped
  `Unresolved` inline `Body.References` entry has a corresponding `unresolved-items`
  entry by construction (same source set).

### Open sub-decision for execution
Confirm the emitted `UnresolvedItem` for a net-new header ref carries the right
`Source` value. Candidate: keep `Source = FunctionClassification` (so consumers see
one VBA-procedure source) vs. a distinct source. Decide + pin; keeping
`FunctionClassification` preserves the golden's existing Source value for body refs
and is the leaning choice. Map fields (Reason ← `Outcome` reason via the same
`UnresolvedReasons` set; Line/Column ← `reference.Location.Start`; SurroundingText ←
expression text) to match the body-ref shape so no body-ref output shifts.

## Verification path for S1 execution
1. **Red first:** encode the DW-11 Layer A reproducer
   (`Public Sub Demo(Optional ByVal x As Variant = MissingThing)`) as a failing
   `UnresolvedItemAggregatorTests` / `EndToEndFixtureTests` pin proving the header
   ref is absent today.
2. Implement the dedup-additive feed; turn the pin green; add a **no-double-count**
   pin and a **superset** pin.
3. **Golden:** prove byte-level neutrality. Hermetic first via
   `EndToEndFixtureTests` over committed HHC inputs if it builds `unresolved-items`
   without COM; otherwise a deliberate, foreground, babysat
   `CanonicalGoldenRegressionTests` / `GoldenCorpusRegenerator` COM run (TRX as the
   completion signal; sweep spurious `samples/*.accdb` diffs before staging).
4. Closeout-grade cross-provider `session-verification` (prose-first + JSON block);
   in-IDE devil's-advocate pair as primary — focus the contrarian hypothesis on the
   dedup key (could it suppress a *legitimately distinct* header ref that shares a
   body ref's line/column/reason?).
