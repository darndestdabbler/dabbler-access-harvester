# 015 — Set-Wide Adversarial Review: Disposition

> Disposition of the two set-wide responses (`set-wide-adversarial-gpt-5-4.md`,
> `s3-devils-advocate-reply-gemini.md`) to `set-wide-adversarial-prompt.md`. The set
> was already CLOSED; these are post-closeout findings handled as a follow-up commit
> (the established pattern — cf. the S2 in-IDE follow-up `7769957`). No set reopen.

## Verdicts

| Reviewer | Verdict |
|---|---|
| GPT-5.4 | SOUND-WITH-CAVEATS (all 9 hypotheses HOLD) |
| Gemini 3.1 Pro | SOUND-WITH-CAVEATS (H1–H6, H8 HOLD; H7 + H9 CONFIRMED-BROKEN) |

Both agree the DW-11 thesis is met: the Layer A / Layer B partition over
`ResolvedModule.References` is exhaustive and disjoint (binary `FindEnclosingProcedureScope`
`null` vs non-`null`), the superset/reason-class alignment holds (three-way:
projector `MapOutcomeWithFlattening` ↔ `FunctionClassificationBuilder.DispatchOutcome`
↔ aggregator `UnresolvedReasonOf`), Layer B never enters the Layer A dedup HashSet
(no cross-layer suppression), the injective `CanonicalResidual` is a materially total
order, the schema/guide contract is coherent after `7769957`, and the tests pin the
production `Pipeline.EmitManifestArtifacts` path (focused rerun 44/44).

## Findings and disposition

1. **vbObjectError = missing intrinsic-constant registry** (GPT H7 caveat + "highest
   risk"; Gemini H7 CONFIRMED-BROKEN). **Already recorded as DW-18** (created during
   the 2026-06-15 Northwind local-validation pass). Both reviewers correctly note this
   is a *resolver-completeness* false-positive class, NOT a Set-015 bug — Set 015
   faithfully *surfaces* what the resolver could not bind; it does not decide what
   *should* bind. **No new action** — DW-18 owns it (golden-affecting; deliberate
   resolver-builtin-constants set). The set-wide thesis is unaffected: completeness of
   the index ≠ semantic cleanliness of the index.

2. **Stale "HHC golden-neutral / zero module-level cases" test comments** (GPT H9
   low-risk drift). **FIXED this commit** — `UnresolvedItemsCompletenessTests.cs` lines
   ~165 and ~377 still framed HHC as golden-neutral, which Layer B disproved (+2
   `ModuleLevelReference` items). Corrected to scope golden-neutrality to **Layer A
   only** and state plainly that Layer B was golden-affecting. Assertions were always
   correct; only the explanatory comments were stale.

3. **`UnresolvedItemAggregator.Build` scalar SQL/dynamic-SQL module-name attribution
   vs. single run-wide call** (Gemini H9 CONFIRMED-BROKEN / highest-risk). **Recorded
   as new DW-19**, owned by **DW-8** (the set that actually wires SQL form-references /
   dynamic-SQL into the run-wide index). Verified accurate: `Build` takes scalar
   `sqlFormReferenceModuleName` / `dynamicSqlModuleName` etc., and `Pipeline.cs` calls
   it once run-wide — so multi-module SQL attribution is structurally impossible.
   **Latent, not live**: the pipeline does not feed the SQL/dynamic feeds into the
   run-wide `Build` today, so nothing is mis-attributed. Pre-existing since Set 011
   F-2-2; Set 015 neither introduced nor worsened it (it surfaced because the VBA half
   is now complete). Out of DW-11 scope; the fix is to make those feeds
   per-item-attributed when DW-8 lands.

## Net

No Set-015 regression or undercount survived the set-wide review. Two real findings,
both **out of DW-11 scope and pre-existing**: one already tracked (DW-18), one newly
recorded (DW-19). One in-repo doc-honesty fix applied (stale comments). The set's
closed/VERIFIED status stands.
