# 015 — Set-Wide Adversarial Evaluation Prompt (GPT + Gemini)

> Paste into your in-IDE GPT (Codex / GPT-5.4) and Gemini (3.1 Pro) sessions, each
> pointed at the working tree at or after commit `7769957` (the set is CLOSED). This
> is a **whole-set** review of `015-unresolved-items-completeness` — both Session 1
> (Layer A) and Session 2 (Layer B) together — NOT a single-session pass. The
> per-session routed verifications (gpt-5-4) and the S2 in-IDE pair already ran; the
> S2 pair already caught one Major the routed rounds missed (a stale
> `interpretation-guide.md`, fixed in `7769957`). Your job now is to attack the SET
> AS A WHOLE: cross-session consistency, the end-to-end completeness claim, the
> shared design decisions, and any residual drift. Be adversarial — a finding with a
> concrete reproducer is worth far more than an endorsement.

## What the set claims to have achieved (the thesis to attack)

DW-11 said `unresolved-items.json` (the run-wide index a consumer reads to enumerate
every VBA reference the resolver could not bind) **undercounted** in two layers. The
set claims to have closed both so the index is now a **complete structural superset**
of the unresolved references the harvester can see:

- **Layer A (Session 1):** procedure-HEADER unresolved refs (an `Optional` parameter's
  default expression) were dropped because `FunctionClassificationBuilder` walks
  `procedureAst.Body` only. Fix: a dedup-additive procedure-scoped feed
  (`EnumerateProcedureScopedReferences` → `FromProcedureScopedReferences`), deduped
  against the body-only feed on `(module, procedure, line, column, reason, memberName)`
  with a **non-mutating** `Contains` test, tagged `Source=FunctionClassification`.
- **Layer B (Session 2):** module-level unresolved refs (a `Const`/`Dim` initializer,
  `Implements <unresolved>`, module-scope `As New <unresolved>`) — those on
  `ResolvedModule.References` with no enclosing `ProcedureScope` — had no manifest
  signal at all. Fix: a module-level feed (`EnumerateModuleLevelReferences` →
  `FromModuleLevelReferences`), new `Source=ModuleLevelReference` with
  `ProcedureName=null`, enum widened in the living v1.1 schema (no envelope bump),
  index-only (the DW-17 asymmetry). Plus an injective `CanonicalResidual` sort
  tiebreaker (the determinism hardening).

Key files to read together: `AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs`
(both feeds, the dedup, `SortDeterministically` + `CanonicalResidual`),
`AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs`
(`EnumerateProcedureScopedReferences` + `EnumerateModuleLevelReferences` +
`GroupReferencesByProcedure`), `AccessHarvester.Core/Pipeline.cs` (the two feeds wired),
`tests/.../UnresolvedItemsCompletenessTests.cs` + `UnresolvedItemAggregatorTests.cs`,
`docs/test-cases/harvester-schema.v1.1.json` (Source enum), and
`docs/harvester/interpretation-guide.md` §6 (the consumer contract).

## Set-wide hypotheses to try to PROVE (each is a potential undercount or contract break)

1. **The partition is NOT exhaustive (the strongest attack).** Layer A covers
   references WITH an enclosing `ProcedureScope`; Layer B covers references with NONE.
   Together they claim to cover ALL of `ResolvedModule.References`. Find a reference
   that lands in **neither** — e.g. one whose `EnclosingScope` is null (not even a
   ModuleScope), or a scope kind that is neither Procedure nor Module. If one exists,
   the "complete superset" thesis is false and there's a THIRD undercount layer.

2. **Double-count across the two layers.** Could a single `ResolvedReference` be
   enumerated by BOTH `EnumerateProcedureScopedReferences` and
   `EnumerateModuleLevelReferences` (e.g. a nested-procedure scope, a `With` block at
   module scope, a lambda/anonymous scope)? Layer B claims disjointness "by
   construction" — verify the scope-walk (`FindEnclosingProcedureScope`) actually
   guarantees it for every scope kind the resolver produces.

3. **Superset claim vs. the inline view.** The set claims the index is a structural
   superset of `Body.References[ResolutionStatus="Unresolved"]`. Is that true for
   EVERY procedure-scoped reason class (not just `not-in-scope`)? Consider
   with-flattening (`ResolvedViaWithFlattening` whose inner outcome is Unresolved),
   default-member-not-yet-resolved, member-access-on-unresolved-target. Does
   `UnresolvedReasonOf` agree with what the projector writes into `Body.References` in
   every case, so the dedup neither double-counts nor over-suppresses?

4. **Layer A / Layer B interaction in the dedup + sort.** Layer A's dedup keys on a
   6-tuple; Layer B items carry `ProcedureName=null`. Could a Layer B item's key ever
   collide with a Layer A item's key in the dedup `HashSet` (suppressing a real
   module-level ref), or in `SortDeterministically` (mis-ordering)? Trace both layers
   through `Build` together.

5. **Determinism end-to-end.** `CanonicalResidual` = `JsonSerializer.Serialize(item)`.
   Is the serialization provably stable across runs/platforms (culture, enum
   representation, dictionary order, null handling)? Could two genuinely distinct
   items serialize identically (residual collapses them → undefined order), or could
   one item serialize differently across runs (non-determinism)?

6. **Schema / contract honesty across the whole set.** The Source enum was widened
   with no envelope bump (living-schema). Is every place the manifest is validated or
   consumed consistent (CLI `SupportedSchemaVersions`, embedded schema, example
   fixtures, `interpretation-guide.md`)? Does any consumer rely on the enum being
   closed at the prior values? Is the DW-17 asymmetry (module-level refs are
   index-only, absent from per-module `ParserOutput`) disclosed honestly everywhere a
   consumer would look?

7. **Golden integrity.** The only golden data delta across the whole set is +2
   `ModuleLevelReference` items (Layer A was golden-neutral). Is that fully explained?
   Are the 2 HHC cases (`vbObjectError` in `basDataOperations`) genuinely
   module-level unresolved refs and not an artifact of the resolver's built-in set
   missing a VBA intrinsic it *should* know (which would be a different bug — a
   missing built-in, mis-surfaced as unresolved)?

8. **Test integrity across both sessions.** Were any assertions weakened to force
   green? Do the Layer A and Layer B pins actually pin the production path (they drive
   the same `Build` the pipeline calls), or do they test a parallel construction that
   could drift from `Pipeline.cs`?

9. **Scope discipline.** Did either session pull in work beyond DW-11, or leave a
   genuine DW-11 sub-case unaddressed and unrecorded? Is DW-17 the right (and only)
   carve-out, or is something else being silently deferred?

## Output

For each hypothesis: **CONFIRMED-BROKEN** (with a concrete reproducer — VBA snippet,
failing assertion, or contract contradiction) or **HOLDS** (with the reasoning that
makes it airtight). Then a set-wide verdict: **SOUND** / **SOUND-WITH-CAVEATS** /
**RECONSIDER**, and — most valuable — name the single highest-risk thing the two
sessions' combined verification (routed R1–R4 + the S2 in-IDE pair) is most likely to
have still missed.
