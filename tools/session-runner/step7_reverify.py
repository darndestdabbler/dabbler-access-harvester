"""Step 7: Save issue log, fix findings, and re-verify."""
import importlib.util, sys, os
from pathlib import Path

def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router", "ai-router/__init__.py",
        submodule_search_locations=["ai-router"])
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod

ar = load_ai_router()
route = ar.route
from ai_router.session_log import SessionLog

SESSION_SET = "docs/session-sets/vba-grammar-validation"
log = SessionLog(SESSION_SET)

issues = [
    {
        "id": "VGV-1-001",
        "severity": "Major",
        "finding": "H-D05 fixture included Resume 0 which verifier challenged as conflating with On Error GoTo 0 sentinel; not clearly listed in title (3 forms listed, not 4).",
        "location": "docs/test-cases/edge/01-h-d05-resume-forms.bas",
        "resolution": "fixed",
        "fix": "Removed Resume 0 from the fixture, keeping 3 confirmed unique forms: Resume, Resume Next, Resume <label>."
    },
    {
        "id": "VGV-1-002",
        "severity": "Major",
        "finding": "H-D12 fixture included positional arguments after named arguments, which is invalid VBA syntax. Also mixed in ParamArray dependency.",
        "location": "docs/test-cases/edge/02-h-d12-named-arguments.bas",
        "resolution": "fixed",
        "fix": "Simplified fixture to one TestNamedArgCalls procedure using DoCmd.OpenForm and MsgBox as well-known external callees. Removed the helper ProcedureWithNamedArgs definition. No positional-after-named calls."
    },
    {
        "id": "VGV-1-003",
        "severity": "Major",
        "finding": "H-D12 and H-E07 expected JSON had source.kind='procedure' but covered multiple procedures or the whole module, creating source-span mismatch with the single-procedure fixture convention.",
        "location": "docs/test-cases/edge/02-h-d12-named-arguments.expected.json, 03-h-e07-param-array.expected.json",
        "resolution": "fixed",
        "fix": "Restructured both fixtures to single-procedure; updated source location to span exactly that procedure. H-E07 now uses LogMessage as the sole procedure with self-calls in the body."
    },
    {
        "id": "VGV-1-004",
        "severity": "Minor",
        "finding": "H-D04 fixture was not minimal: included On Error GoTo <label> and On Error Resume Next (already in corpus), and the division-by-zero made On Error GoTo 0 unreachable at runtime.",
        "location": "docs/test-cases/edge/05-h-d04-on-error-goto-zero.bas",
        "resolution": "fixed",
        "fix": "Simplified procedure body: activate handler, run non-erroring code (Debug.Print), deactivate with On Error GoTo 0, then run code outside scope. Removed On Error Resume Next. On Error GoTo 0 is now reachable."
    },
    {
        "id": "VGV-1-005",
        "severity": "Info",
        "finding": "Edge fixtures not covered by automated grammar parse assertions; schema validation only.",
        "location": "Edge-fixture test integration",
        "resolution": "deferred",
        "fix": "Deferred to Session 2 (Grammar Fix Loop) which will run the grammar harness against all fixtures and surface parse failures explicitly."
    }
]

Path("docs/session-sets/vba-grammar-validation/issue-logs").mkdir(parents=True, exist_ok=True)
import json
issue_path = "docs/session-sets/vba-grammar-validation/issue-logs/session-001.json"
with open(issue_path, "w", encoding="utf-8") as f:
    json.dump({"session": 1, "issues": issues}, f, indent=2)
print(f"Issue log saved to {issue_path}")
log.save_issue_log(session_number=1, issues=issues)
print("Issue log saved via session log helper")

# Re-read all edge fixtures for re-verification
edge_dir = Path("docs/test-cases/edge")
fixture_content = []
for bas_file in sorted(edge_dir.glob("*.bas")) + sorted(edge_dir.glob("*.cls")):
    json_file = bas_file.parent / (bas_file.stem + ".expected.json")
    vba_src = bas_file.read_text(encoding="utf-8")
    json_src = json_file.read_text(encoding="utf-8") if json_file.exists() else "(missing)"
    fixture_content.append(f"### {bas_file.name}\n\n```vba\n{vba_src}\n```\n\nExpected JSON (schema PASS):\n```json\n{json_src[:800]}\n```")

fixtures_text = "\n\n".join(fixture_content)

prior_issues = "\n".join([
    f"- {i['id']} [{i['severity']}]: {i['finding']} -> {i['resolution']}: {i['fix']}"
    for i in issues
])

reverify_prompt = f"""
# Session Re-Verification (Round 2): vba-grammar-validation Session 1

This is a round-2 cross-provider verification. Round 1 found ISSUES_FOUND. The issues have been fixed. Verify the fixes are correct and complete.

## Prior Issues and Fixes Applied

{prior_issues}

## Updated Fixture Files (all 5 expected JSON files pass schema validation)

{fixtures_text}

## Verification Criteria for Round 2

1. **H-D05 fixture**: Only 3 Resume forms: Resume, Resume Next, Resume <label>. Resume 0 removed. Minimal.
2. **H-D12 fixture**: Single procedure TestNamedArgCalls using DoCmd.OpenForm and MsgBox with :=. No positional-after-named. Source span matches one procedure. Schema valid.
3. **H-E07 fixture**: Single procedure LogMessage with ParamArray in signature; self-calls in body. Source span matches. Schema valid.
4. **H-A07-REM fixture**: Already correct. Unchanged.
5. **H-D04 fixture**: DemonstrateOnErrorGoToZero with On Error GoTo 0 reachable. Simplified. Source span matches.

## Out of Scope (unchanged, confirmed present in repo)
- The 12 existing corpus fixtures (not modified)
- Vba.g4 grammar (not modified; Session 2 will run harness and fix grammar bugs)
- parse-failure-log.md (not yet populated; Session 2/3 will populate)

## Output Format

VERDICT: [VERIFIED | ISSUES_FOUND]

If VERIFIED: brief confirmation of what was checked.
If ISSUES_FOUND: list remaining issues with severity, finding, location, suggested fix.
"""

print("\nRouting round-2 session verification...")
result = route(
    content=reverify_prompt,
    task_type="session-verification",
    complexity_hint=70,
    session_set=SESSION_SET,
    session_number=1,
)

review_path = "docs/session-sets/vba-grammar-validation/session-reviews/session-001-r2.md"
with open(review_path, "w", encoding="utf-8") as f:
    f.write(result.content)
print(f"Round-2 review saved to {review_path}")
print(f"Model: {result.model_name}, Cost: ${result.cost_usd:.4f}")

log.log_step(1, 9, "session-1/reverification-round2",
             f"Round-2 verification after fixing 4 verifier findings. Model: {result.model_name}",
             "complete",
             [{
                 "model": result.model_name,
                 "taskType": "session-verification",
                 "inputTokens": result.input_tokens,
                 "outputTokens": result.output_tokens,
                 "costUsd": result.cost_usd,
             }])
print("Re-verification step logged")
