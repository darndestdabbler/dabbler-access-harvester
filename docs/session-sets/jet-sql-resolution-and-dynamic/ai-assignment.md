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
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 3):** claude-code / claude-sonnet-4-6 @ effort=medium. Session 3 shifts from synthesis to mechanical cross-cut + schema validation. The primary routed task (`schema-validation`) is Sonnet-pinned per Convention; matching the orchestrator to Sonnet avoids paying Opus rates for mechanical correspondence work.

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
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 4 — optional buffer):** claude-code / claude-sonnet-4-6 @ effort=medium. If session 4 fires, it addresses verifier-surfaced findings — mechanical code fixes that Sonnet handles at a fraction of Opus cost.

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
