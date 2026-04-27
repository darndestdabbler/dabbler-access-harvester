## Session 1: Form-Reference Detection (Synthesis)

### Recommended orchestrator (engine + model + effort)
claude-code / claude-opus-4-7 @ effort=high

### Rationale
The core task is a complex (`complexity_hint=80`) architectural synthesis, making Opus the appropriate choice. Since the `architecture` task type is routed to Opus, selecting an Opus orchestrator allows the synthesis to be authored in-conversation per the "orchestrator-matches-routing-target" Convention — and avoids the documented Opus/architecture timeout pattern on synthesis prompts above ~3K tokens.

### Estimated routed cost (qualitative band)
Low — only `analysis` (gemini-pro) and `session-verification` (gpt-5-4) routed; the synthesis itself is in-conversation.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Read Phase 5.3, symbol table, SQL AST, and fixture `04`. | In-conversation (file reads) |
| 2 | Author `ai-assignment.md` per Step 3.5. | `route(task_type="analysis")` → gemini-pro |
| 3 | Produce the form-reference visitor design + implementation. | In-conversation (orchestrator matches `architecture` target = Opus) |
| 4 | Land detection code under `AccessHarvester.Sql.Parser/FormReferences/`. | In-conversation (mechanical) |
| 5 | Build + test sequentially. | In-conversation (shell) |
| 6 | Cross-provider session verification. | `route(task_type="session-verification", complexity_hint=70)` → gpt-5-4 |
| 7 | Cost report, ai-assignment actuals, next-orchestrator recommendation. | `route(task_type="analysis")` → gemini-pro |

### Actuals (filled after the session)
- Orchestrator used: claude-code / claude-opus-4-7 @ effort=high
- Total routed cost: $0.1727 across 3 calls — gemini-pro analysis $0.0154 (ai-assignment), gpt-5-4 verification round 1 $0.1039 (Major found), gpt-5-4 verification round 2 $0.0534 (VERIFIED)
- Deviations from recommendation: none. Synthesis authored in-conversation per the orchestrator-matches-target convention; architecture call not routed (intentional, sidesteps the documented Opus/architecture timeout pattern).
- Notes for next-session calibration: cross-provider verification caught a real Major (Bang vs Dot operator semantics) the orchestrator missed. The fix loop cost $0.0534 — modest tax on cross-provider review. In-conversation synthesis path is reliable for Opus orchestrator + architecture work; keep it as the Session 2 default. Round-1 prompt size was 28710 chars (~6.5K tokens for gpt-5-4); round-2 was 11753 chars (~2.6K tokens).

**Next-session orchestrator recommendation (Session 2):** claude-code / claude-opus-4-7 @ effort=high. Per routed analysis (gemini-pro, $0.0022): Session 2 is a complex synthesis where the `architecture` call routes to Opus; continuing with an Opus orchestrator allows the critical step to be authored in-conversation, which was reliable in Session 1 and sidesteps the documented routed-synthesis timeout risk. Estimated routed cost band: Medium (verification + analysis only — no routed architecture).

---

## Session 2: Dynamic SQL Analyzer (Synthesis)

### Recommended orchestrator (engine + model + effort)
claude-code / claude-opus-4-7 @ effort=high

### Rationale
Second `complexity_hint=80` architectural synthesis, this time over data-flow analysis of VBA-built SQL strings. Opus is the right tier; running it as the orchestrator and authoring in-conversation keeps the synthesis on-rail with the established pattern.

### Estimated routed cost (qualitative band)
Low — synthesis in-conversation; only `session-verification` and Step-8 analysis routed.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Read Phase 5.4 + fixture `05`'s `.bas` and `expected.json`. | In-conversation (file reads) |
| 2 | Produce the dynamic-SQL data-flow algorithm + candidate-template AST. | In-conversation (orchestrator matches `architecture` target = Opus) |
| 3 | Land analyzer under `AccessHarvester.Sql.Parser/DynamicSql/`. | In-conversation (mechanical) |
| 4 | Build + test sequentially. | In-conversation (shell) |
| 5 | Cross-provider session verification. | `route(task_type="session-verification", complexity_hint=70)` → gpt-5-4 |
| 6 | Cost report, ai-assignment actuals, next-orchestrator recommendation. | `route(task_type="analysis")` → gemini-pro |

### Actuals (filled after the session)
- Orchestrator used: claude-code / claude-opus-4-7 @ effort=high
- Total routed cost: $0.3852 across 4 calls — gemini-pro analysis $0.0021 (next-orch recommendation), gpt-5-4 verification round 1 $0.2082 (3 Majors found), gpt-5-4 verification round 2 $0.1749 (1 Minor — fixed without round-3 reverify since Minor doesn't block commit per workflow Step 7)
- Deviations from recommendation: none. Synthesis authored in-conversation per the orchestrator-matches-target convention; architecture call not routed (intentional, sidesteps the documented Opus/architecture timeout pattern). Round-3 skipped because the round-2 finding was Minor (record `Equals` reference-equality fabricates divergence in WalkLoopBody/AllAlternativesEqual) and was fixed pre-emptively via a SymbolicSqlValueEquality.DeepEquals helper rather than re-verified.
- Notes for next-session calibration: cross-provider verification caught three real Majors round-1 (param-seed, eager full-flatten before cap, loop one-iteration mutation) the orchestrator missed — exactly the verifier-as-second-pair-of-eyes value the workflow is designed for. Round-2 caught a structural-equality subtlety that record auto-equality hides. Verification spend ran ~$0.38 vs Session 1's $0.17 (Session 1 cleared in 1 reverify round; Session 2 needed 1 reverify plus a Minor pre-empt). The ~2x verifier spend was correct value at this tier of code complexity. Round-1 prompt size was 81420 chars (~17.7K input tokens to gpt-5-4); round-2 was 52740 chars (~11.1K).

**Next-session orchestrator recommendation (Session 3):** claude-code / claude-sonnet-4-6 @ effort=low. Per routed analysis (gemini-pro, $0.0021): Session 3's task mix (mechanical schema-validation + summarization, no novel synthesis) eliminates the need for a high-end synthesis orchestrator. Primary routed task (`schema-validation`) is Sonnet-pinned per Convention; the routed analysis returned `effort=low` (tighter than the Session-1-authored `effort=medium`) on the rationale that schema-validation against fixture 05's `expected.json` is pure mechanical correspondence. Estimated routed cost band: Low. (Note: the routed analysis emitted the model-id `claude-sonnet-3-5`, which does not exist; normalized to `claude-sonnet-4-6` — the deployed Sonnet — for the Session-3 trigger phrase.)

---

## Session 3: Cross-Cut + Mechanical Tightening

### Recommended orchestrator (engine + model + effort)
claude-code / claude-sonnet-4-6 @ effort=medium

### Rationale
End-to-end pipeline run on fixtures `04` and `05` plus mechanical schema validation against `expected.json`. No novel synthesis; Sonnet handles mechanical correspondence cheaply. Last regular session also generates the change-log via `summarization`.

### Estimated routed cost (qualitative band)
Low-to-Medium — adds `schema-validation` (Sonnet) and `summarization` (gemini-flash) on top of the standard verification + analysis calls.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Run end-to-end pipeline on fixtures `04` and `05`; confirm form-reference annotations and 5 candidate templates. | In-conversation (shell + file reads) |
| 2 | Compare output against `expected.json`. | `route(task_type="schema-validation")` → Sonnet-pinned |
| 3 | Land tightening commits. | In-conversation (mechanical) |
| 4 | Build + test sequentially. | In-conversation (shell) |
| 5 | Cross-provider session verification. | `route(task_type="session-verification", complexity_hint=70)` → gpt-5-4 |
| 6 | Generate change-log (last regular session). | `route(task_type="summarization", max_tier=1)` → gemini-flash |
| 7 | Cost report, ai-assignment actuals, next-orchestrator + next-set recommendations. | `route(task_type="analysis")` → gemini-pro |

### Actuals (filled after the session)
- Orchestrator used: claude-code / claude-sonnet-4-6 @ effort=medium
- Total routed cost: $0.1355 (gemini-pro schema-validation $0.0160, gpt-5-4 session-verification $0.1155, gemini-flash change-log $0.0002, gemini-pro next-set recommendation $0.0038 — schema-validation and next-set are incremental to Sessions 1+2 spend)
- Deviations from recommendation: none. Schema-validation used gemini-pro (within tier-2 per convention). Session verification went to gpt-5-4 (cross-provider, per established override). Change-log used gemini-flash (max_tier=1, per convention for summarization). Sonnet as orchestrator was correct for mechanical tightening session.
- Notes for next-session calibration: Verifier (gpt-5-4) returned VERIFIED with no issues on round-1 — this session's work was relatively straightforward tightening. Schema-validation by gemini-pro caught 2 real gaps (weak count assertion, missing conditional-fragment test) that Sonnet-at-orchestrator-level could have caught too. Consider whether schema-validation could be handled in-conversation on future Sessions 3 type tightening sessions.

**Next-session orchestrator recommendation (Session 4 — optional buffer):** claude-code / claude-sonnet-4-6 @ effort=medium. If session 4 fires, it addresses verifier-surfaced findings — mechanical code fixes that Sonnet handles at a fraction of Opus cost.

**Next-session-set recommendation:** `vba-semantic-resolution-and-typing` (#14). Per routed analysis (gemini-pro, $0.0038): with #13 complete, the critical path unblocks core VBA semantic analysis — symbol table construction, scope resolution, type inference. Requires Opus 4.7+ orchestrator (high synthesis depth).

---

## Session 4: Verifier-Surfaced Cleanup (optional buffer)

### Recommended orchestrator (engine + model + effort)
claude-code / claude-sonnet-4-6 @ effort=medium

### Rationale
Reserved for verifier-surfaced cleanup. Mechanical fix work; Sonnet is the right cost/quality balance.

### Estimated routed cost (qualitative band)
Low.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Address verifier findings deferred from sessions 1-3. | In-conversation (mechanical) |
| 2 | Build + test sequentially. | In-conversation (shell) |
| 3 | Cross-provider session verification. | `route(task_type="session-verification", complexity_hint=70)` → gpt-5-4 |
| 4 | Append addendum to change-log if scope changed. | `route(task_type="summarization", max_tier=1)` → gemini-flash |
| 5 | Cost report, ai-assignment actuals. | `route(task_type="analysis")` → gemini-pro |

### Actuals (filled after the session)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 5):** End of set; cross-set handoff authored at Step 8.
