"""Session 1 close: cost report, next-orchestrator recommendation,
mark-complete. Commit + push + notify happen via shell after this."""
from pathlib import Path

from ai_router import (
    route,
    SessionLog,
    print_cost_report,
    print_session_set_status,
    mark_session_complete,
)

SESSION_SET = Path("docs/session-sets/jet-sql-grammar-and-parser")
log = SessionLog(str(SESSION_SET))
N = 1

print("=== COST REPORT ===")
print_cost_report(str(SESSION_SET))

print("=== SESSION-SET STATUS ===")
print_session_set_status()

# Next-orchestrator recommendation for Session 2
ai_assignment_md = (SESSION_SET / "ai-assignment.md").read_text(encoding="utf-8")[:4000]

prompt = f"""Author the next-session-orchestrator recommendation for
Session 2 of session-set `jet-sql-grammar-and-parser`. Output the
exact markdown block schema from `docs/ai-led-session-workflow.md`
Step 8 (the **Next session orchestrator recommendation** schema).

Inputs:

Session 1 actuals:
- Orchestrator used: claude-code Opus 4.7 @ effort=unknown.
- Authored JetSql.g4 (~640 LoC) IN-CONVERSATION per the
  "Opus At effort:max + Adaptive Thinking Timeouts on Large
  Synthesis-Document Prompts" Convention, NOT via route(architecture).
- Total routed cost: ~$0.33 (1 analysis call $0.018 + 3 verification
  rounds $0.144 + $0.083 + $0.083). The recommended Session-1 routed
  cost in ai-assignment.md was $2.30 against actual $0.33 — large
  underspend because the synthesis was inlined.
- Verification took 3 rounds; both R2 issues were overcorrections from
  R1 fixes (formRefRoot overmatching bracketed identifiers; STAR
  globally accepted as function arg). Final R3 VERIFIED cleanly.

Session 2 scope (per spec): Author SQL parser infrastructure
(immutable AST nodes for SELECT/UPDATE/INSERT/DELETE/TRANSFORM,
visitor walking, source-position tracking, error recovery with
synchronization at statement boundaries). Mirrors the shape of
`parser-infrastructure` (#6) for VBA but smaller in scope.

Session 2's row in ai-assignment.md (excerpt):
====
{ai_assignment_md}
====

Output a single markdown block in this schema (do not deviate):

**Next session orchestrator recommendation (Session 2):**
<engine> <model> @ effort=<low|medium|high>

**Rationale:** <one to two sentences grounded in (a) Session 2's actual
scope and (b) what Session 1 revealed about how the codebase is shaped>
"""

result = route(content=prompt, task_type="analysis", complexity_hint=40,
               session_set=str(SESSION_SET), session_number=N)
rec_path = SESSION_SET / "session-1-next-orchestrator-recommendation.md"
with open(rec_path, "w", encoding="utf-8") as f:
    f.write(result.content)
log.log_step(N, 8, "session-1/next-orchestrator-recommendation",
    f"Routed analysis recommending next orchestrator via {result.model_name}", "complete",
    [{"model": result.model_name, "taskType": "analysis",
      "inputTokens": result.input_tokens, "outputTokens": result.output_tokens,
      "costUsd": result.cost_usd}])
print(f"Wrote {rec_path}  cost=${result.cost_usd:.4f}")

# Append actuals + recommendation to ai-assignment.md
actuals_block = """

### Session 1 Actuals (filled by orchestrator at end of session)

- **Orchestrator used:** claude-code Opus 4.7 @ effort=unknown
- **Total routed cost:** ~$0.33 (analysis $0.018 + verification R1 $0.145 + R2 $0.083 + R3 $0.083)
- **Deviations from recommendation:** Synthesis was authored
  in-conversation by the Opus orchestrator rather than via
  `route(architecture, complexity_hint=80)`, per the
  "Opus At effort:max + Adaptive Thinking Timeouts on Large
  Synthesis-Document Prompts" Convention in `project-guidance.md`.
  This drove the routed cost from the estimated $2.30 down to $0.33;
  the in-conversation cost is borne by the orchestrator allotment.
- **Notes for next-session calibration:** Verifier returned
  ISSUES_FOUND on R1 (4 real grammar gaps) and R2 (2 overcorrections
  from R1 fixes). R3 VERIFIED cleanly. The pattern suggests Session 2
  should plan for 2-3 verification rounds for new infrastructure code.

"""

with open(SESSION_SET / "ai-assignment.md", "a", encoding="utf-8") as f:
    f.write(actuals_block)
    f.write("\n\n## Updated recommendation for Session 2\n\n")
    f.write(result.content)

# Mark session complete
mark_session_complete(session_set=str(SESSION_SET), verification_verdict="VERIFIED")
print("MARKED COMPLETE")

# Save the recommendation tail to a file (avoid cp1252 print)
Path("/tmp/next_orch_rec.txt").write_text(result.content, encoding="utf-8")
print("Recommendation written to /tmp/next_orch_rec.txt")
