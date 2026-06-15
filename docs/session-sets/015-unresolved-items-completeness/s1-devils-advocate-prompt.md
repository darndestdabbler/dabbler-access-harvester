# 015 Session 1 — Devil's-Advocate Prompt (DW-11 Layer A)

> For the user's in-IDE adversarial pair (GPT + Gemini). The routed
> cross-provider verification already ran (gpt-5-4): **R1 found a Major**
> on the dedup seam (intra-feed `Add` could drop distinct chained-header
> refs) which was **fixed**; **R2 VERIFIED**. This prompt invites an
> independent third/fourth opinion focused on the same seam — try to find
> what R1/R2 and the pins missed.

## What landed (Layer A only)

`unresolved-items.json` undercounted: procedure-**HEADER** unresolved references
(an `Optional` parameter's default expression) were absent from the index because
its VBA half is built from `FunctionClassification.UnresolvedReferences`, and
`FunctionClassificationBuilder` walks `procedureAst.Body` **only**. The same ref
**is** present inline on `ParserOutput.Body.References[]` (the projector groups by
enclosing procedure *scope*, header included), so the index was not a structural
superset of the inline unresolved references.

**Fix — dedup-additive feed.** The body-only `FunctionClassification` feed is kept
unchanged (body refs stay byte-identical). The aggregator is additionally fed the
full procedure-scoped resolver reference set (body + header) — the exact set the
projector groups into `Body.References` — via the new
`ParserOutputProjector.EnumerateProcedureScopedReferences`, filtered to
(post-with-flattening) Unresolved, and **deduplicated against the body-only set**
on the key `(module, procedure, line, column, reason, memberName)`. The dedup is
**non-mutating** (membership test against a snapshot of the body-only keys; the
procedure-scoped feed is never dedup'd against itself). Net-new items are exactly
the header refs. Header items are tagged `UnresolvedSource.FunctionClassification`.

Files: `AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs`
(Build dedup + `FromProcedureScopedReferences` + `DedupKey` + `ProcedureScopedReference`),
`AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs`
(`EnumerateProcedureScopedReferences`), `AccessHarvester.Core/Pipeline.cs` (wiring),
`tests/AccessHarvester.Pipeline.Tests/UnresolvedItemsCompletenessTests.cs` (5 pins).

## Contrarian hypotheses to pursue (try hard to break these)

1. **Dedup over-suppression (R1's catch — re-attack).** With the non-mutating
   `Contains` + the 6-tuple key, can you STILL construct two legitimately-distinct
   procedure-scoped unresolved references that collapse to one index entry, dropping
   one? (R1's `Foo.Bar` / `Foo.Bar.Baz` chain is now pinned green by
   `LayerA_ChainedHeaderUnresolvedRefs_AllReachIndex_NoIntraFeedDrop`.) Consider
   same-name repeated chains, implicit-With member refs, bang access.
2. **Dedup UNDER-suppression (double-count).** Can a single physical BODY reference
   produce a DIFFERENT key in the two feeds (so it is NOT suppressed and appears
   twice)? Both feeds derive line/column from the same AST node's `Provenance`, the
   reason via the same with-flattening, and the name via the same `ExtractName`
   switch — find a node type where they diverge if you can. (HHC golden is
   byte-identical, so any divergence must be a construct absent from the 32-module
   corpus.)
3. **Net-new body item (golden risk).** Could `resolvedModule.References` carry a
   body-scoped unresolved reference the `ClassificationVisitor` body walk does NOT
   visit (it overrides Visit for Identifier / MemberAccess / BangAccess /
   ImplicitWithMember / ImplicitWithBang / Me only)? Such a ref would survive dedup
   as net-new and change the golden.
4. **Source-tagging honesty.** Header refs are tagged
   `UnresolvedSource.FunctionClassification` (same as body refs). Is conflating
   header and body provenance under one source value acceptable, or should the
   index distinguish them?
5. **Superset claim.** Is the index now provably a structural superset of
   `Body.References[Unresolved]` for procedure-scoped refs, given the feed reuses the
   projector's own grouping — or is there a path where an inline unresolved ref has no
   index entry?
6. **Scope.** Module-level (Layer B) refs are STILL dropped (Session 2 owns them).
   Confirm nothing about module-level was pulled in and no schema changed this session.

## Evidence already established

- 912/912 `AccessHarvester.Pipeline.Tests` pass (incl. the 5 new Layer A pins).
- COM golden gate `CanonicalGoldenRegressionTests.HomeHealthCare_ReproducesGoldenOutput`
  passes byte-identical (no regen): Layer A is golden-neutral on HHC.
- The chained-header pin was RED before the R1 fix and GREEN after.
