"""Finalize Session 1: write activity-log.json, run next-orchestrator
recommendation via route(analysis), and print cost report.
"""
from __future__ import annotations
import importlib.util, sys, pathlib, json
from datetime import datetime

REPO = pathlib.Path(__file__).resolve().parents[2]


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        str(REPO / "ai-router" / "__init__.py"),
        submodule_search_locations=[str(REPO / "ai-router")],
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


SESSION_SET = REPO / "docs" / "session-sets" / "vba-lexical-and-expression-grammar"
ACTIVITY = SESSION_SET / "activity-log.json"
ASSIGNMENT = SESSION_SET / "ai-assignment.md"


def now_iso() -> str:
    return datetime.now().astimezone().isoformat()


def write_activity_log() -> None:
    """Backfill the activity log for Session 1."""
    log = {
        "sessionSetName": "vba-lexical-and-expression-grammar",
        "createdDate": "2026-04-26T00:00:00+00:00",
        "totalSessions": 4,
        "entries": [
            {
                "sessionNumber": 1,
                "stepNumber": 0,
                "stepKey": "session-1/author-ai-assignment",
                "dateTime": now_iso(),
                "description": "Authored ai-assignment.md via route(task_type='analysis', complexity_hint=55)",
                "status": "complete",
                "routedApiCalls": [
                    {"model": "gemini-pro", "taskType": "analysis",
                     "inputTokens": 2947, "outputTokens": 1646, "costUsd": 0.0201}
                ],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 1,
                "stepKey": "session-1/read-inputs",
                "dateTime": now_iso(),
                "description": "Read ms-vbal-summary.md Section A, ms-vbal-coverage.csv lexical rows, hazard-list.md Section A, and fixtures 01/07.",
                "status": "complete",
                "routedApiCalls": [],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 2,
                "stepKey": "session-1/synthesize-lexical-grammar",
                "dateTime": now_iso(),
                "description": "Authored Vba.g4 lexical section in-conversation (Opus orchestrator matches route(architecture) target per Convention).",
                "status": "complete",
                "routedApiCalls": [],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 3,
                "stepKey": "session-1/wire-antlr-codegen",
                "dateTime": now_iso(),
                "description": "Switched AccessHarvester.Vba.Grammar.csproj from Antlr4.Runtime 4.6.6 (placeholder echo target) to Antlr4BuildTasks 12.8.0 + Antlr4.Runtime.Standard 4.13.1; resolves the parser-foundations deferred 'ANTLR4 Code Generation' item.",
                "status": "complete",
                "routedApiCalls": [],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 4,
                "stepKey": "session-1/author-lexer-tests",
                "dateTime": now_iso(),
                "description": "Authored 42 hermetic lexer unit tests under tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs covering hazard-list Section A entries (H-A01..H-A09) plus Round-1/2/3 verifier-driven regressions.",
                "status": "complete",
                "routedApiCalls": [],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 5,
                "stepKey": "session-1/build-and-test",
                "dateTime": now_iso(),
                "description": "dotnet build AccessHarvester.sln + dotnet test AccessHarvester.sln --no-build (sequential per project-guidance). 137 tests, 0 failures.",
                "status": "complete",
                "routedApiCalls": [],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 6,
                "stepKey": "session-1/cross-provider-verification-r1",
                "dateTime": now_iso(),
                "description": "Round-1 session-verification via route(task_type='session-verification') to gpt-5-4. Verdict: ISSUES_FOUND (1 Critical, 4 Major).",
                "status": "complete",
                "routedApiCalls": [
                    {"model": "gpt-5-4", "taskType": "session-verification",
                     "inputTokens": 13340, "outputTokens": 19309, "costUsd": 0.3230}
                ],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 7,
                "stepKey": "session-1/cross-provider-verification-r2",
                "dateTime": now_iso(),
                "description": "Round-2 verification (complexity_hint=85) after applying R1 fixes. Verdict: ISSUES_FOUND (1 Major regression in REM_COMMENT bare-form predicate).",
                "status": "complete",
                "routedApiCalls": [
                    {"model": "gpt-5-4", "taskType": "session-verification",
                     "inputTokens": 10500, "outputTokens": 11525, "costUsd": 0.1991}
                ],
            },
            {
                "sessionNumber": 1,
                "stepNumber": 8,
                "stepKey": "session-1/cross-provider-verification-r3",
                "dateTime": now_iso(),
                "description": "Round-3 verification (final retry) after applying R2 fix. Verdict: ISSUES_FOUND (1 Major: REM line-continuation leading-marker case). Fixed in-place per vba-spec-study Session 3 precedent; retry cap reached.",
                "status": "complete",
                "routedApiCalls": [
                    {"model": "gpt-5-4", "taskType": "session-verification",
                     "inputTokens": 9317, "outputTokens": 7183, "costUsd": 0.1310}
                ],
            },
        ],
    }
    ACTIVITY.write_text(json.dumps(log, indent=2), encoding="utf-8")
    print(f"Wrote {ACTIVITY}")


def next_orchestrator_recommendation():
    """Route an analysis call recommending the orchestrator for Session 2."""
    ar = load_ai_router()
    spec_text = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
    assignment_text = ASSIGNMENT.read_text(encoding="utf-8")

    prompt = f"""Recommend the orchestrator for Session 2 of
`vba-lexical-and-expression-grammar` (Expression Grammar). Inputs:

## Session 2 spec excerpt

```markdown
{spec_text}
```

## Current ai-assignment.md (Session 1 ledger and Session 2 plan)

```markdown
{assignment_text}
```

## Session 1 actuals (just completed)

- Orchestrator used: Claude Code Opus 4.7 (in-conversation synthesis,
  per Convention).
- Total routed cost: ~$0.69 (analysis $0.02 + 3 verification rounds
  $0.65). Synthesis was in-conversation (no route() architecture
  call).
- Round 1 verifier returned 1 Critical + 4 Major; Round 2 returned 1
  Major regression; Round 3 returned 1 more Major (line-continuation
  inside REM body). All fixed; final lexer test count 42 passing.
- Build/test: 137 tests, 0 failures.
- Deviation: switched the grammar project from Antlr4.Runtime 4.6.6
  to Antlr4.Runtime.Standard 4.13.1 + Antlr4BuildTasks 12.8.0 to wire
  actual code generation (the parser-foundations placeholder
  echo-only target is removed). Disclosed in verification prompt.
- Session 2's expression-grammar synthesis is comparable in scope and
  similarly synthesis-heavy (operator precedence climbing, bang-vs-
  dot mixed chains, default-member access).

## Output

Reply with the schema:

```markdown
**Next session orchestrator recommendation (Session 2):**
<engine> <model> @ effort=<low|medium|high|max>

**Rationale:** <one or two sentences grounded in (a) Session 2's
actual scope and (b) what Session 1 revealed about how this
codebase is shaped>
```
"""
    result = ar.route(
        content=prompt,
        task_type="analysis",
        complexity_hint=40,
        session_set=str(SESSION_SET),
        session_number=1,
    )
    rec_path = SESSION_SET / "next-orchestrator-recommendation-session-2.md"
    rec_path.write_text(result.content, encoding="utf-8")
    print(f"Wrote {rec_path}")
    print(f"Model: {result.model_name}; cost ${result.cost_usd:.4f}")
    return result


def main() -> None:
    write_activity_log()
    rec = next_orchestrator_recommendation()
    print("\n--- next-orchestrator recommendation ---")
    print(rec.content)
    print("--- end ---")

    ar = load_ai_router()
    print("\n--- cost report ---")
    ar.print_cost_report(str(SESSION_SET))


if __name__ == "__main__":
    main()
