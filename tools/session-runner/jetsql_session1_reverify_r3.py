"""Round 3 verification after R2 fixes."""
import sys, importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "ai_router", "ai-router/__init__.py", submodule_search_locations=["ai-router"])
mod = importlib.util.module_from_spec(spec); sys.modules["ai_router"] = mod
spec.loader.exec_module(mod)
route = mod.route
SessionLog = mod.SessionLog

SESSION_SET = Path("docs/session-sets/jet-sql-grammar-and-parser")
log = SessionLog(str(SESSION_SET))
N = 1

# Append R2 issues + fixes to issue log
issues_r2 = [
    {"id": 5, "round": 2, "severity": "Major",
     "summary": "formRefRoot accepted any bracketed identifier — would absorb [Order Details].[Quantity] into formReference",
     "status": "fixed",
     "fix": "Added gated semantic predicate IsBracketedFormsOrReports() restricting bracketed root to [Forms]/[Reports] only."},
    {"id": 6, "round": 2, "severity": "Major",
     "summary": "STAR was accepted as the sole argument of any function (Nz(*), Sum(*) all parsed)",
     "status": "fixed",
     "fix": "functionCall now has a dedicated COUNT LPAREN STAR RPAREN alternative; generic functionCall reverts to expressionList only."},
]
log.save_issue_log(N, issues_r2)

g4 = Path("AccessHarvester.Sql.Grammar/JetSql.g4").read_text(encoding="utf-8")
prompt = f"""Round 3 verification of Session 1 of `jet-sql-grammar-and-parser`.

Round 2 returned ISSUES_FOUND with two new findings caused by the R1
fixes themselves (overcorrection). Both are now addressed:

5. formRefRoot overmatched any bracketed identifier — fixed by adding
   a gated semantic predicate `IsBracketedFormsOrReports()` (parser
   @members) that only admits the bracketed alternative when the next
   token's text is exactly `[Forms]` or `[Reports]` (case-insensitive).
   Ordinary `[Order Details].[Quantity]` now falls through to
   columnReference unchanged.
6. STAR was generally accepted by functionCall — fixed by giving
   `COUNT(*)` its own labeled alternative (`countStarCall`); the
   generic functionCall path now requires `expressionList?` only.

The grammar still builds cleanly via ANTLR4BuildTasks.

GRAMMAR FILE (`JetSql.g4`):
====
{g4}
====

Please confirm whether the R2 findings are now resolved and no new
issues arise. Output VERDICT: VERIFIED or VERDICT: ISSUES_FOUND.
"""

result = route(content=prompt, task_type="session-verification",
               complexity_hint=85, session_set=str(SESSION_SET), session_number=N)
log.save_session_review(N, result.content, round_number=3)
log.log_step(N, 7, "session-1/cross-provider-verification-r3",
    f"Round 3 verification via {result.model_name}", "complete",
    [{"model": result.model_name, "taskType": "session-verification",
      "inputTokens": result.input_tokens, "outputTokens": result.output_tokens,
      "costUsd": result.cost_usd}])

Path("/tmp/r3_tail.txt").write_text(result.content[-800:], encoding="utf-8")
print("R3 done; review saved.")
print("model:", result.model_name, "cost:", result.cost_usd)
