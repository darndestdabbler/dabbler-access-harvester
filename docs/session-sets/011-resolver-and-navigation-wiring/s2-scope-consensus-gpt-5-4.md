Recommend **Option A**.

The decisive issue is not preference; it is that the session’s R2-1 premise has been falsified by re-grounding. The spec explicitly required re-checking the current tree, and that check shows the needed switchboard row content is **not present in any harvested artifact**. Once that is true, “add the Switchboard Items table reader” is no longer a wiring task on existing outputs; it becomes a **new extraction surface** problem, which this set forbids.

That makes F-2-2 and R2-1/R2-2 qualitatively different kinds of work. F-2-2 is exactly the right P3 payload: existing substrate, existing seam, no new COM/data access, already validated against real corpus behavior, and high user-visible value. By contrast, R2-1 and R2-2 are not simple tail items hanging off the same seam. They require either:
- new row acquisition, which violates constraints, or
- alternative derivation logic from macro IR / existing surfaces, which is **new production logic** and only a partial substitute for the stated deliverable.

That is why set 006 already classified them as **P4**. Trying to “satisfy the letter” now via partial approximations would cut across the prior architecture decision and likely produce code the P4 set will later replace. That is poor scope hygiene in an automated spec-driven session, especially when the stop rule explicitly says to stop at the P3/P4 boundary and record deferred work.

Option B is attractive only if you optimize for checklist closure over evidence integrity. But it creates several risks:
1. **Semantic mismatch**: macro-driven switchboard edges are not the same as reading switchboard rows; captions and menu structure remain unavailable.
2. **Partial completeness risk**: consumers may infer that switchboard/report-chain coverage is “implemented” when it is only opportunistic.
3. **Golden-delta ambiguity**: you would introduce output changes from heuristics/partial readers that are harder to explain and review than the clean F-2-2 substrate wiring delta.
4. **Rework risk**: P4 is already scoped to do this properly; best-effort code now is likely disposable or will constrain the real design later.

Consumer value also favors A. F-2-2 alone unlocks the core missing production value: populated `NavigationGraph`, non-empty unresolved-items, and classification arrays sourced from already-built enrichers. That is broad, honest, immediately useful output. A partial R2-1/R2-2 adds narrower, incomplete edges and may mislead consumers more than it helps them.

So the right move is to treat this as a **scope correction, not a failure to execute**: ship F-2-2, add explicit DW entries for R2-1 and R2-2 citing the disproven premise and the P4 ownership, and get human sign-off on the narrowed acceptance set. That best honors the spec’s own re-grounding rule, the no-new-COM constraint, and the stop rule.

VERDICT: A — Ship core wiring only; defer disproven-premise R2-1/R2-2 to P4.