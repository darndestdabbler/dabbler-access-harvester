"""Round 2 verification after applying R1 fixes."""
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

issues = [
    {"id": 1, "severity": "Major", "summary": "Bracketed [Forms]/[Reports] roots not parseable",
     "status": "fixed", "fix": "Added formRefRoot allowing bracketedIdentifier as form-reference root."},
    {"id": 2, "severity": "Minor", "summary": "selectLikeStatement redundantly admitted unionStatement twice",
     "status": "fixed", "fix": "transformStatement now produces only the TRANSFORM ... PIVOT shape; the bare unionStatement alternative was removed from transformStatement."},
    {"id": 3, "severity": "Major", "summary": "COUNT(*) not accepted by functionCall",
     "status": "fixed", "fix": "Added functionArgs : STAR | expressionList; functionCall now uses functionArgs."},
    {"id": 4, "severity": "Minor", "summary": "TRANSFORM aggregate alias (e.g. TRANSFORM Count(*) AS X) not accepted",
     "status": "fixed", "fix": "transformStatement now accepts an optional (AS? columnAlias) after the aggregate expression."},
]
log.save_issue_log(N, issues)

g4 = Path("AccessHarvester.Sql.Grammar/JetSql.g4").read_text(encoding="utf-8")
fix04 = Path("docs/test-cases/04-saved-query-form-params.sql").read_text(encoding="utf-8")

prompt = f"""Round 2 verification of Session 1 of `jet-sql-grammar-and-parser`.

Round 1 returned ISSUES_FOUND with four findings. All four have been
addressed:

1. Bracketed [Forms]/[Reports] roots — fixed by extracting `formRefRoot`
   to allow `FORMS | REPORTS | bracketedIdentifier`.
2. Redundant unionStatement in selectLikeStatement — fixed by removing
   the bare `unionStatement` alternative from `transformStatement`; it
   is reached via the sibling alternative in selectLikeStatement.
3. COUNT(*) — fixed by adding `functionArgs : STAR | expressionList;`
   and routing `functionCall` through it.
4. TRANSFORM alias (TRANSFORM Count(*) AS X ...) — fixed by adding an
   optional `(AS? columnAlias)?` to the transformStatement rule.

The grammar still builds cleanly via ANTLR (no rule-conflict errors).

Out of scope (Sessions 2-3 of this set):
- Parser AST + error recovery
- Tests/parser harness in tests/AccessHarvester.Sql.Grammar.Tests/
- Form-reference resolver integration

GRAMMAR FILE (`JetSql.g4`):
====
{g4}
====

Test-case fixture 04 (the verifier flagged this would not parse with
the round-1 grammar; please re-assess against the round-2 grammar):
====
{fix04}
====

Please verify whether the round-1 issues are now resolved and whether
any new issues arise from the fixes. Output VERDICT: VERIFIED or
VERDICT: ISSUES_FOUND on the final line.
"""

result = route(content=prompt, task_type="session-verification",
               complexity_hint=85, session_set=str(SESSION_SET), session_number=N)
log.save_session_review(N, result.content, round_number=2)
log.log_step(N, 6, "session-1/cross-provider-verification-r2",
    f"Round 2 verification via {result.model_name}", "complete",
    [{"model": result.model_name, "taskType": "session-verification",
      "inputTokens": result.input_tokens, "outputTokens": result.output_tokens,
      "costUsd": result.cost_usd}])

# write to file rather than print to dodge cp1252
Path("/tmp").mkdir(exist_ok=True)
Path("/tmp/r2_tail.txt").write_text(result.content[-500:], encoding="utf-8")
print("R2 done; review saved; tail in /tmp/r2_tail.txt")
print("model:", result.model_name, "cost:", result.cost_usd)
