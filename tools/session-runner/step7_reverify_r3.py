"""Round 3 verification: line-number fixes only."""
import importlib.util, sys, os, json
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

# Read just the two repaired files
fixes_summary = """
Round-2 findings and fixes applied before this round-3 check:

1. 01-h-d05-resume-forms.expected.json: source.location.startLine corrected from 11 to 12.
   (Public Sub ResumeForms begins at line 12 in the .bas file after 9 comment lines + Option Explicit + blank.)

2. 04-h-a07-rem-comments.expected.json: source.location corrected from startLine:9/endLine:18 to startLine:8/endLine:17.
   Body statement locations corrected by -1 (lines 15→14 and 17→16).
   (Public Sub RemCommentExamples() begins at line 8 after 4 REM lines + blank + Option Explicit + blank.)

All 5 edge expected JSON files still pass harvester-schema.v0.json validation.
Build and tests still green (unchanged since round-1 check).
"""

# Read the two corrected files for verifier review
h_d05_json = Path("docs/test-cases/edge/01-h-d05-resume-forms.expected.json").read_text(encoding="utf-8")
h_d05_bas = Path("docs/test-cases/edge/01-h-d05-resume-forms.bas").read_text(encoding="utf-8")
h_a07_json = Path("docs/test-cases/edge/04-h-a07-rem-comments.expected.json").read_text(encoding="utf-8")
h_a07_bas = Path("docs/test-cases/edge/04-h-a07-rem-comments.bas").read_text(encoding="utf-8")

reverify_r3_prompt = f"""
# Session Re-Verification (Round 3 / Final): vba-grammar-validation Session 1

Round 2 found two Major issues (source-span line-number errors). Both have been corrected. This round verifies only those two fixes.

## Fixes Applied

{fixes_summary}

## File 1: 01-h-d05-resume-forms.bas (with line numbers)

```
{chr(10).join(f"{i+1:3d}  {line}" for i, line in enumerate(h_d05_bas.splitlines()))}
```

Fixed expected JSON:
```json
{h_d05_json}
```

## File 2: 04-h-a07-rem-comments.bas (with line numbers)

```
{chr(10).join(f"{i+1:3d}  {line}" for i, line in enumerate(h_a07_bas.splitlines()))}
```

Fixed expected JSON:
```json
{h_a07_json}
```

## Verification Task

Confirm:
1. `source.location.startLine` in file 1's JSON matches where `Public Sub ResumeForms(...)` begins in the .bas.
2. `source.location.startLine` and `endLine` in file 2's JSON match where `Public Sub RemCommentExamples()` begins and `End Sub` appears in the .bas.
3. Body statement locations in file 2 are consistent with the .bas line numbers.

Output VERDICT: VERIFIED or ISSUES_FOUND with remaining findings.
"""

print("Routing round-3 verification (line-number fixes only)...")
result = route(
    content=reverify_r3_prompt,
    task_type="session-verification",
    complexity_hint=55,
    session_set=SESSION_SET,
    session_number=1,
)

review_path = "docs/session-sets/vba-grammar-validation/session-reviews/session-001-r3.md"
with open(review_path, "w", encoding="utf-8") as f:
    f.write(result.content)
print(f"Round-3 review saved to {review_path}")
print(f"Model: {result.model_name}, Cost: ${result.cost_usd:.4f}")

log.log_step(1, 10, "session-1/reverification-round3",
             f"Round-3 verification (line-number fixes). Model: {result.model_name}",
             "complete",
             [{
                 "model": result.model_name,
                 "taskType": "session-verification",
                 "inputTokens": result.input_tokens,
                 "outputTokens": result.output_tokens,
                 "costUsd": result.cost_usd,
             }])
print("Round-3 step logged")
