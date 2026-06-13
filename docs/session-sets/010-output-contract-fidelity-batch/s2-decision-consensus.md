# Set 010 Session 2 — Decision Consensus (F-2-4, F-2-5)

Cross-provider decision consensus per spec ('decision-consensus if non-obvious')
and the seek-AI-consensus-before-asking-human standing feedback.

## gpt-5-4 (gpt-5-4) — $0.0359

DECISION 1 VERDICT: IMPLEMENT — The strongest case for striking Q6 is the lean one: the current code path is provably dead, HHC never exercises the rule, and deleting it from the strategy doc would reduce surface area instead of adding behavior the only golden fixture cannot validate end-to-end. That argument fails here because the doc is explicitly authoritative and says Q6 ships; leaving it unimplemented is a silent spec/code divergence the project’s contract rules reject. The recommended implementation is small, uses inputs already present, matches the documented intent exactly, and is golden-neutral on HHC, so it fixes a real authority mismatch without creating any unexplained regen churn. In this project, “lean” does not justify silently shipping less than the declared strategy.

DECISION 2 VERDICT: FIX-NOW (A) — The best case for deferring is review hygiene: this session is already busy, HHC is the only committed end-to-end golden, and F-2-5 will expand the golden-delta surface from one likely category flip into multiple fired-signal-list changes, which can make attribution noisier than a later dedicated classifier-focused pass. I still choose FIX-NOW because the defect is no longer hypothetical: HHC demonstrably contains the no-credit pattern, so the current output contract is verifiably wrong on the project’s sole golden fixture. The fix is narrow and principled—credit inline-SQL FROM/JOIN targets found in the IR object catalog into UsedAsRecordSource, while keeping object-specific record-source sets query-only—and every resulting delta is directly explainable by F-2-5, which satisfies the stop-rule exactly. Since the downstream consumer is an AI agent, corrected signal lists are contract improvements, not cosmetic churn, so the extra delta surface is worth taking now rather than carrying known-bad output into a later set where causality is harder to isolate.

Risks/caveats for implementation:
- Q6 should fire only when `targets.Count > 0 && targets.All(KnownQueryNames.Contains)`; keep the `> 0` guard so empty/unanalyzable target sets do not accidentally qualify.
- For F-2-5, restrict inline-SQL credits to names present in the IR catalog (`db.Tables ∪ db.Queries`) to avoid false positives from aliases, external names, or parser artifacts.
- Preserve the distinction between global `UsedAsRecordSource` credit and form/report-specific record-source sets; only the former should broaden to tables.
- Add one hermetic unit test for each finding, and in the golden regen notes enumerate each HHC delta explicitly under F-2-5 so no change appears “ambient.”
- If session review load is a concern, split commits/regens by finding even if they land in the same session; that keeps attribution crisp without deferring the fix.

---

## gemini-pro (gemini-pro) — $0.0080

DECISION 1 VERDICT: **IMPLEMENT**

The proposal to implement Q6 is correct. The strongest counter-argument (the steelman) is that implementing a golden-neutral rule violates the "lean-by-default" value by adding code and test surface for no immediate benefit in the reference fixture. This argument fails because it incorrectly weighs the project's values. "Explicit-over-silent contracts" is the dominant value here. The authoritative strategy document is an explicit contract with the consumer (the AI agent) that the harvester can identify this specific query pattern. Silently failing to do so is a contract breach. Given that the implementation is trivial, uses existing data, and carries zero regression risk to the HHC golden, correcting the doc-vs-code divergence by implementing the specified rule is the superior choice. This ensures the harvester is correct for real-world databases that exhibit this pattern, even if HHC does not.

DECISION 2 VERDICT: **FIX-NOW (A)**

The proposal to fix F-2-5 now is correct. The steelman for deferring is based on risk management: the session is already heavy, and this fix introduces a new class of golden deltas, increasing the review surface and the potential for confounding regressions. This argument is unpersuasive because the core purpose of this session is "output-contract fidelity." F-2-5 is a proven, non-inferred fidelity bug directly observable in the HHC golden. The resulting deltas are not noise; they are the direct, verifiable evidence of the fix, and they are fully explainable under the project's stop-rule ("every...delta must be explained by a named finding's fix"). For the AI consumer, the current output is factually incorrect—it withholds a valid `RecordSource` signal. Deferring a known, verifiable, in-scope data-correctness bug from a session dedicated to fixing them would be a failure of the session's objective. The value of shipping a more correct golden file outweighs the operational cost of reviewing the additional deltas.

**RISKS & CAVEATS:**

1.  **F-2-4 (Q6 Implementation):** The risk is negligible but non-zero. The hermetic unit test must validate the exact logic (`targets.Count > 0 && targets.All(KnownQueryNames.Contains)`) to prevent future regressions or incorrect classifications on non-HHC databases.
2.  **F-2-5 (Inline-SQL Fix):** The primary risk is that the golden delta review may be more complex than anticipated. The orchestrator must rigorously confirm that *every* resulting change in the golden file—both category flips and signal-list additions—is directly and exclusively attributable to crediting table names from inline-SQL RecordSources. Any other unexpected delta would indicate a confounding bug and must be investigated before merging.

---


**Total routed consensus cost:** $0.0439
