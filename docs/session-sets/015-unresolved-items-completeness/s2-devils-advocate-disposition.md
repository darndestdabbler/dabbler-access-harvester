# 015 S2 — Devil's-Advocate Disposition (post-close follow-up)

The user ran the S2 in-IDE devil's-advocate pair (GPT-5.4 + Gemini 3.1 Pro) on the
shipped Layer B closeout. Both returned **SOUND-WITH-CAVEATS** and **independently
converged on one real Major** that the routed R1–R4 cross-provider verification
missed. Responses: [`s2-devils-advocate-gpt-5-4.md`](s2-devils-advocate-gpt-5-4.md),
[`s2-devils-advocate-reply-gemini.md`](s2-devils-advocate-reply-gemini.md).

## Findings and disposition

| # | Finding | Both? | Disposition |
|---|---|---|---|
| Major | `docs/harvester/interpretation-guide.md` §6 was stale after Layer B: still said module-level unresolved refs have **no manifest signal** ("until DW-11 lands") and listed only the pre-S2 five `Source` values. | GPT + Gemini | **FIXED (this commit).** Rewrote the DW-11 scope-caveat block to describe the shipped state (procedure-scoped = index + inline; module-level = index-only under `Source: "ModuleLevelReference"`, no `ProcedureName`, DW-17 asymmetry); added `ModuleLevelReference` to the `Source` list + reason-class table. |
| Drift | Same §6: stale **top-level `Severity`** field in the example + bullet (the record has **no** `Severity` property — severity lives in `AdditionalContext.severity` for `Resolver` entries), and the example omitted `ConstructDescription` + `AdditionalContext`. | GPT | **FIXED (this commit).** Corrected the example and field bullets against the committed golden (ground-truth field set verified). Pre-existing drift, fixed in the same honesty pass. |
| Comment | `ParserOutputProjector.cs` "module-level references are dropped" comment did not note they are recovered in the run-wide index. | (implied by GPT's "comment drift") | **FIXED (this commit, comment-only).** Now cross-references `EnumerateModuleLevelReferences` + DW-17. |
| Caveat | Enum widening (`Source` gains `ModuleLevelReference`) can crash an external consumer that deserialises `Source` into a closed enum without an unknown-value path. | GPT + Gemini (#4) | **Accepted (documented).** Generic external forward-compat risk, not a tree-provable break under the living-schema model; the guide now warns consumers to tolerate an unknown `Source`. No code change. |
| Hyp 1–3, 6 | Partition is the exact complement of one predicate (no double-count, no neither-bucket); `FromModuleLevelReferences` guards resolved refs; the `CanonicalResidual` total-order tiebreaker is injective; golden +2 delta legitimate; no assertions weakened. | both HOLD | **No action.** The Layer B mechanics are sound; both reviewers ran the narrow pipeline tests green (GPT: 44/44). |
| Hyp 5 (DW-17) | The index-vs-per-module-`ParserOutput` asymmetry itself. | both | **Accepted by design;** already recorded as **DW-17** in `deferred-work.md` by S2. The doc fix above makes it explicit to consumers. |

## Why this was docs-only

Both reviewers confirmed the Layer B **code** is correct (no double-count, no
resolved-ref leak, provably-deterministic sort). The single real defect was
consumer-contract honesty in the interpretation guide. The fix is documentation +
one code comment — **no behavior, test, or golden impact** — so it is a post-close
follow-up commit, not a reopened verification round. (Same shape as the Set 013
DW-15 devil's-advocate follow-up.)

The catch validates the standing pattern: the in-IDE devil's-advocate pair, run on
the fresh closeout, surfaced a real consumer-facing gap that four routed
verification rounds did not — they were focused on the dedup/determinism mechanics
the prompt steered them to, and none re-grounded the public guide.
