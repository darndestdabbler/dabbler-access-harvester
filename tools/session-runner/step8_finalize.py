"""Step 8: Cost report, next-orchestrator recommendation, mark session complete."""
import os
from pathlib import Path

from ai_router import route, print_cost_report, print_session_set_status, mark_session_complete
from ai_router.session_log import SessionLog

SESSION_SET = "docs/session-sets/vba-grammar-validation"
log = SessionLog(SESSION_SET)

# Print cost report
print("=== COST REPORT ===")
print_cost_report(SESSION_SET)
print()

# Route next-orchestrator recommendation for Session 2
next_session_spec = """
Session 2 of 4: Grammar Fix Loop (Synthesis Session)

Steps:
1. Run grammar harness against full corpus (12 module-level + 5 edge fixtures)
2. Classify every parse failure (grammar bug / missing construct / malformed / ambiguous)
3. For non-trivial grammar bugs requiring rule-shape decisions, route architecture synthesis (1 call aggregating all decision points)
4. Implement grammar fixes in Vba.g4
5. Re-run harness; iterate until 100% parse on all fixtures
6. Build and test
7. Route session-verification
8. Next-orchestrator recommendation
9. Commit, push, notify

This is the SYNTHESIS session. It requires:
- Interpreting grammar failures and proposing ANTLR4 rule fixes
- Making non-trivial parser design decisions (e.g., ambiguity resolution, rule precedence)
- Implementing those fixes in Vba.g4 (an ANTLR4 grammar file)
- Iterating until 100% pass

The ai-assignment.md recommends: claude-code Opus 4.7 @ effort=medium (also per vba-statement-and-module-grammar change-log cross-set recommendation).
Session 1 actuals: Sonnet-level orchestrator, no grammar bugs encountered in Session 1 (Session 1 was fixture authoring only, no grammar changes). Session 2 will be the first real grammar stress test against the new edge fixtures.
"""

next_orch_prompt = f"""
Recommend the orchestrator for Session 2 of the vba-grammar-validation session set.

## Session 2 Scope
{next_session_spec}

## Session 1 Actuals
- Orchestrator: claude-code claude-sonnet-4-6 (low effort)
- Work performed: Fixture authoring (5 edge fixtures), schema validation, 3-round cross-provider verification
- Routed cost: ~$0.40
- No grammar changes made in Session 1 -- grammar work begins in Session 2
- Verifier (GPT-5.4) found 6 issues (4 Major, 1 Minor, 1 Info); all fixed within 2 re-verification rounds

## Prior Recommendation (from vba-statement-and-module-grammar change-log)
"Recommended orchestrator for vba-grammar-validation: claude-code Opus 4.7 @ effort=medium. Rationale: The vba-grammar-validation set has one synthesis session (edge-case-driven grammar fix loop) that requires strong analytical capability to identify corpus failures and design targeted fixtures. Sonnet can handle the mechanical sessions, but the synthesis session warrants Opus to avoid re-verification ping-pong on novel fixture design."

## ai-assignment.md Recommendation for Session 2
"engine: claude-code, model: opus-4.7, effort: medium"
"Rationale: This session requires a single, high-leverage synthesis call to resolve multiple grammar bugs and ambiguities; using an Opus orchestrator minimizes implementation errors when applying the architectural guidance."

Please produce the next-orchestrator recommendation in this format:

**Next session orchestrator recommendation (Session 2):**
<engine> <model> @ effort=<low|medium|high>

**Rationale:** <1-2 sentences grounded in Session 2's actual scope and what Session 1 revealed>
"""

print("Routing next-orchestrator recommendation for Session 2...")
result = route(
    content=next_orch_prompt,
    task_type="analysis",
    complexity_hint=40,
    session_set=SESSION_SET,
    session_number=1,
)

# Persist before display
rec_path = "docs/session-sets/vba-grammar-validation/next-orchestrator-recommendation-session-2.md"
with open(rec_path, "w", encoding="utf-8") as f:
    f.write(result.content)
print(f"Next-orchestrator recommendation saved to {rec_path}")
print(f"Recommendation:\n{result.content}")
print(f"Cost: ${result.cost_usd:.4f}")

log.log_step(1, 11, "session-1/next-orchestrator-recommendation",
             f"Routed analysis recommending orchestrator for Session 2. Model: {result.model_name}",
             "complete",
             [{
                 "model": result.model_name,
                 "taskType": "analysis",
                 "inputTokens": result.input_tokens,
                 "outputTokens": result.output_tokens,
                 "costUsd": result.cost_usd,
             }])

# Append actuals to ai-assignment.md
actuals_block = f"""

## Session 1 Actuals (added post-completion)

**Orchestrator used:** claude-code claude-sonnet-4-6 @ effort=unknown (low)
**Verification verdict:** VERIFIED (after 3 rounds: initial + 2 fix-and-reverify)
**Total routed cost:** ~$0.40 (analysis $0.02 + 5x test-generation $0.08 + verification $0.14 + re-verify $0.11 + re-verify-r3 $0.03 + next-orch $0.02)
**Deviations from recommendation:** None material. Low effort was appropriate for fixture authoring.
**Notes for Session 2:** Verifier found 6 issues in initial round (4 Major: fixture minimalism + source-span mismatches; 1 Minor: logic error in H-D04; 1 Info: no automated harness). All fixed by round 3. Session 2 will run the grammar harness against 17 fixtures (12 module-level + 5 edge) and likely find failures to fix.

{result.content}
"""

with open("docs/session-sets/vba-grammar-validation/ai-assignment.md", "a", encoding="utf-8") as f:
    f.write(actuals_block)
print("Actuals block appended to ai-assignment.md")

# Print session set status
print("\n=== SESSION SET STATUS ===")
print_session_set_status()
print()

# Mark session complete
mark_session_complete(
    session_set=SESSION_SET,
    verification_verdict="VERIFIED",
)
print("session-state.json marked complete")
