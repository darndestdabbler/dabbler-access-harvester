"""Session 1 finalize: build/test logs, cross-provider verification,
cost report, next-orchestrator recommendation, mark-complete."""
from pathlib import Path

from ai_router import route, SessionLog

SESSION_SET = Path("docs/session-sets/jet-sql-grammar-and-parser")
SESSION_SET_STR = str(SESSION_SET)
log = SessionLog(SESSION_SET_STR)
N = 1

# Step 5 — log build + test outcome (already run; just record)
log.log_step(N, 2, "session-1/grammar-build", "Built AccessHarvester.Sql.Grammar; ANTLR generated lexer/parser cleanly", "complete", [])
log.log_step(N, 3, "session-1/full-solution-build", "Full-solution build green (279 warnings preexisting)", "complete", [])
log.log_step(N, 4, "session-1/test-suite", "All tests pass: 54 + 294 + 328 (+2 skipped on Windows-COM gates)", "complete", [])

# Step 6 — cross-provider verification
g4 = (Path("AccessHarvester.Sql.Grammar/JetSql.g4")).read_text(encoding="utf-8")
csproj = (Path("AccessHarvester.Sql.Grammar/AccessHarvester.Sql.Grammar.csproj")).read_text(encoding="utf-8")
spec_md = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
fix04 = Path("docs/test-cases/04-saved-query-form-params.sql").read_text(encoding="utf-8")
plan_phase5 = "\n".join(Path("docs/VBA-Parser-Migration-Harvester-Plan.md").read_text(encoding="utf-8").splitlines()[477:540])
appendix_a = "\n".join(Path("docs/VBA-Parser-Migration-Harvester-Plan.md").read_text(encoding="utf-8").splitlines()[744:771])

verify_prompt = f"""Verify Session 1 of session-set `jet-sql-grammar-and-parser`.

Session deliverable: ANTLR4 grammar `JetSql.g4` for the JET/ACE SQL
dialect plus `AccessHarvester.Sql.Grammar.csproj` build wiring.
Authored in-conversation by an Opus 4.7 orchestrator per the
"Opus At effort:max + Adaptive Thinking Timeouts on Large
Synthesis-Document Prompts" Convention.

Build/test status:
- `dotnet build` of the new csproj: SUCCESS (ANTLR generated lexer +
  parser without errors; the dll links).
- `dotnet build AccessHarvester.sln`: SUCCESS (279 preexisting warnings,
  no errors).
- `dotnet test AccessHarvester.sln`: 54 + 294 + 328 passed, 2 skipped
  (COM-gated synthetic-metadata tests). No new tests added in Session 1
  — Session 3 of this set adds the parser harness + tests.

Out of scope for this session (declared so you do not flag them as
missing):
- Parser AST + error recovery (Session 2 of this set, Phase 5.2).
- Tests under `tests/AccessHarvester.Sql.Grammar.Tests/` (Session 3).
- Form-reference resolver integration (a separate session set, #13).
- DDL (CREATE TABLE / ALTER TABLE) — not in scope per spec.
- The `AccessHarvester.Sql.Parser/` project — Session 2.

Spec excerpt:
====
{spec_md}
====

Plan Phase 5.1 reference:
====
{plan_phase5}
====

Appendix A (Access-specific constructs the grammar must support):
====
{appendix_a}
====

Test-case fixture 04 (must parse cleanly when the parser harness lands
in Session 3 — a smoke-parse against the current grammar would suffice
for now):
====
{fix04}
====

CSPROJ:
====
{csproj}
====

GRAMMAR FILE (`JetSql.g4`):
====
{g4}
====

Please verify:
1. Grammar covers the spec's listed surfaces (SELECT/WHERE/GROUP BY/
   HAVING/ORDER BY, JOIN INNER/LEFT/RIGHT, subqueries, PARAMETERS,
   IIf/Switch/Choose, domain functions DLookup/DSum/etc., TRANSFORM ...
   PIVOT, bracketed identifiers, date literals, & concat, Forms!X!Y).
2. No obvious ambiguities (left-recursion handled, precedence sane).
3. csproj wiring matches the AccessHarvester.Vba.Grammar pattern.
4. Fixture 04 — does the grammar admit it?
5. Grammar style follows the project conventions.

Prefer false-positive ISSUES_FOUND over false-negative VERIFIED.
Output format: end with a single line VERDICT: VERIFIED or
VERDICT: ISSUES_FOUND, optionally followed by an issue list.
"""

print("Routing session-verification (cross-provider)...")
result = route(
    content=verify_prompt, task_type="session-verification",
    complexity_hint=70, session_set=SESSION_SET_STR, session_number=N,
)
review_path = log.save_session_review(N, result.content, round_number=1)
print(f"WROTE review {review_path}  model={result.model_name}  cost=${result.cost_usd:.4f}")

log.log_step(N, 5, "session-1/cross-provider-verification",
    f"Cross-provider verification via {result.model_name}", "complete",
    [{"model": result.model_name, "taskType": "session-verification",
      "inputTokens": result.input_tokens, "outputTokens": result.output_tokens,
      "costUsd": result.cost_usd}])

# Print verdict tail for human visibility
tail = result.content[-400:]
print("---verifier tail---")
print(tail)
print("---/verifier tail---")
