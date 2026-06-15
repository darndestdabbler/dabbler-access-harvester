# 015 S2 — Devil's-Advocate Prompt (in-IDE GPT + Gemini, primary)

> Paste into your in-IDE GPT (Codex) and Gemini sessions, pointed at the working
> tree at the 015-S2 closeout commit. The routed `session-verification` (gpt-5-4)
> already ran R1→R4 (VERIFIED); this contrarian pass targets the seams that
> verification is weakest on. Be adversarial — find the break, don't endorse.

## What shipped (DW-11 Layer B, the FINAL session of set 015)

`unresolved-items.json` is the run-wide INDEX of every VBA reference the resolver
could not bind. **Layer A** (Session 1, VERIFIED) closed the procedure-HEADER
undercount via an additive procedure-scoped feed deduped against the body-only
`FunctionClassification` feed. **Layer B** (this session) closes the MODULE-LEVEL
undercount: a `Const`/`Dim` initializer / `Implements <unresolved>` reference lives
on `resolvedModule.References` with NO enclosing `ProcedureScope`, so the projector
dropped it from both `ParserOutput.Body.References` and the index.

The fix:
- `ParserOutputProjector.EnumerateModuleLevelReferences` returns exactly the
  references `GroupReferencesByProcedure` drops (those with no `ProcedureScope`).
- `UnresolvedItemAggregator.FromModuleLevelReferences` emits them under the new
  `UnresolvedSource.ModuleLevelReference` with `ProcedureName=null`.
- Schema: the `Source` enum in `harvester-schema.v1.1.json` widened (no envelope
  bump — living-schema, additive, string-serialized enum).
- Determinism: `SortDeterministically` gained a precomputed, INJECTIVE
  `CanonicalResidual` (full-item JSON serialization) as the final tiebreaker.

## Real-world outcome (NOT golden-neutral — DW-11's "zero cases" was wrong)

HHC `basDataOperations` lines 4-5 are
`Public Const ERR_SAME_FY As Long = vbObjectError + 1001` /
`Public Const ERR_CANCELED As Long = vbObjectError + 1002`. `vbObjectError` is a VBA
intrinsic outside the resolver's built-in set → two module-level `not-in-scope`
refs. The golden was regenerated; the proven multiset delta is EXACTLY +2
`ModuleLevelReference` items (0 removed), `UnresolvedItemCount` 1414→1416. Every
other textual diff line is reordering from the new total-order tiebreaker.

## Contrarian hypotheses to try to PROVE (in priority order)

1. **Double-count via a non-procedure, non-module scope.** The claim is that
   `EnumerateModuleLevelReferences` (FindEnclosingProcedureScope == null) and
   `EnumerateProcedureScopedReferences` (!= null) partition `resolvedModule.References`
   exactly. Find a reference whose `EnclosingScope` chain makes it appear in BOTH or
   NEITHER — e.g. a reference inside a module-level `Const` whose initializer contains
   a lambda/`With`/nested construct; a property/field initializer; a reference whose
   `EnclosingScope` is null (not even ModuleScope). If one lands in neither, Layer B
   has its OWN undercount.
2. **A module-level ref that should NOT surface.** Could a module-level reference that
   actually RESOLVES (e.g. to another module's Public const) be wrongly emitted?
   `UnresolvedReasonOf` returns null for resolved/with-flattened-resolved — confirm a
   resolved module-level ref contributes nothing.
3. **Determinism residual injectivity.** `CanonicalResidual` = full JSON serialization.
   Can two genuinely-distinct items serialize identically (so the residual collapses
   them and order is undefined), or can the SAME logical run produce different JSON for
   one item across runs (non-determinism)? Consider AdditionalContext ordering, culture,
   enum representation, null handling.
4. **Enum widening contract.** Widening the `Source` enum with no envelope bump — does
   any consumer/test rely on the enum being closed at the 5 prior values? Does the
   string serialization hold everywhere the manifest is validated?
5. **Asymmetry (DW-17).** Module-level refs are in the index but NOT per-module
   `ParserOutput`. Is the interpretation-guide / consumer contract honest about this, or
   will a consumer reading a single module's `ParserOutput` silently miss them?
6. **Scope / test integrity.** Was any assertion weakened to force green? Is the golden
   regen fully explained by the change (multiset proof: 0 removed, +2 added)?

## Cross-session evidence (Layer A, embedded per the final-session convention)

Review `UnresolvedItemAggregator.cs` (both the Layer A `FromProcedureScopedReferences`
+ `DedupKey` and the Layer B `FromModuleLevelReferences`) and the `LayerA_*` + `LayerB_*`
pins in `UnresolvedItemsCompletenessTests.cs` together — Layer B's no-double-count claim
rests on the same scope partition Layer A's dedup relies on.

Return: per hypothesis, CONFIRMED-BROKEN (with a concrete reproducer) or HOLDS (with
reasoning). End with an overall verdict: SOUND / SOUND-WITH-CAVEATS / RECONSIDER.
