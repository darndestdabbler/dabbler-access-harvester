"""
Round 2 cross-provider session-verification for remaining-work-planner Session 2.
Sends ONLY the changed artifacts + the R1 finding list, asking gpt-5-4 to
confirm the three Major findings are resolved and to do a fresh pass for
any drift introduced by the fixes.

Per the project-guidance Convention: persist routed output to disk BEFORE
displaying.
"""
from pathlib import Path
import json
import sys

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/remaining-work-planner")
SESSION_NUMBER = 2

REPO = Path(".")

def read(p: str) -> str:
    return (REPO / p).read_text(encoding="utf-8")

# Read the 5 stubs' Out of Scope sections + the revised tree + the
# R1 verifier output for context.
tree = read("docs/session-sets/remaining-work-planner/remaining-work-tree.md")
r1_review = read("docs/session-sets/remaining-work-planner/session-reviews/session-002.md")

stubs = {
    "F1 harvester-finish-canonical-parse-success-report": read(
        "docs/session-sets/harvester-finish-canonical-parse-success-report/spec.md"
    ),
    "F2 harvester-finish-canonical-golden-output": read(
        "docs/session-sets/harvester-finish-canonical-golden-output/spec.md"
    ),
    "F3 harvester-finish-cli-e2e-test-audit": read(
        "docs/session-sets/harvester-finish-cli-e2e-test-audit/spec.md"
    ),
    "F4 harvester-finish-operations-and-interpretation-docs": read(
        "docs/session-sets/harvester-finish-operations-and-interpretation-docs/spec.md"
    ),
    "F5 harvester-finish-cli-packaging": read(
        "docs/session-sets/harvester-finish-cli-packaging/spec.md"
    ),
}

prompt = f"""You are the same cross-provider verifier for `remaining-work-planner`
Session 2, Round 2.

In Round 1 you returned ISSUES_FOUND with three Major findings:

1. **Tree prereq inconsistency** — F3 marked as F2-dep in tree/order but
   narrative said F3 is independent. F2's per-feature table didn't reflect
   F2's stated F1 prerequisite.
2. **Tree-amendments.md / routing-playbook-delta.md skip not documented**
   — tree referred to tree-amendments.md as if it were a sibling artifact
   when it had not been authored. The intentional-skip rationale was missing.
3. **Stubs' Out of Scope sections missing freeze-boundary bullets** —
   no explicit bar against Blazor / IR-codegen / platform-integration /
   CRUD-scaffolder expansion / additional workflows.

Round 1's full review is at the end of this prompt for reference.

# Round 2 task

For each of the three findings, confirm whether the fix below resolves it.
Then do a fresh pass for any drift the fixes may have introduced (per the
"Post-Fix Sweep All Narrative Artifacts When A Rule's Confidence Tag
Changes" lesson — re-running verification without sweeping for stale
references is the canonical retry-round-cost driver in this repo).

# Fix 1 + Fix 2 — revised remaining-work-tree.md

```markdown
{tree}
```

# Fix 3 — five stubs' Out of Scope sections (full stubs follow)

## F1 stub

```markdown
{stubs["F1 harvester-finish-canonical-parse-success-report"]}
```

## F2 stub

```markdown
{stubs["F2 harvester-finish-canonical-golden-output"]}
```

## F3 stub

```markdown
{stubs["F3 harvester-finish-cli-e2e-test-audit"]}
```

## F4 stub

```markdown
{stubs["F4 harvester-finish-operations-and-interpretation-docs"]}
```

## F5 stub

```markdown
{stubs["F5 harvester-finish-cli-packaging"]}
```

# Drift-sweep checklist

Confirm:

- The total session-count cell on the tree (currently "13 sessions
  total") matches F1=2 + F2=4 + F3=2 + F4=3 + F5=2.
- The set count cell ("5 session sets") matches the 5 stubs.
- The "6 in-scope GAPs" reference in tree's GAP-#5-fold-in narrative
  still aligns with the gap-audit's enumeration.
- F2's set-level prereq column ("F1") agrees with the per-feature
  table's "Golden output generation + commit" feature row.
- F3's recommended-execution-order paragraph is consistent with the
  per-feature table (one feature F2-dep, one independent;
  partial-parallel from start is possible if the determinism gate
  defers).
- No "(this set's sibling artifact)" or similar phantom-file references
  remain.

# Response format

Begin with a per-finding-resolution prose analysis (Finding 1 RESOLVED /
PARTIAL / UNRESOLVED, Finding 2..., Finding 3...) followed by a
drift-sweep paragraph. Then output the JSON block.

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or stub name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.

# Round 1 review (for reference)

```markdown
{r1_review}
```
"""

print(f"Round 2 prompt size: {len(prompt)} chars", file=sys.stderr)

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=70,
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

# PERSIST FIRST
out_path = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}-round2.md"
out_path.write_text(result.content, encoding="utf-8")

meta_path = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}-round2-meta.json"
meta_path.write_text(json.dumps({
    "model_name": result.model_name,
    "input_tokens": result.input_tokens,
    "output_tokens": result.output_tokens,
    "cost_usd": result.cost_usd,
    "task_type": "session-verification",
    "session_set": str(SESSION_SET),
    "session_number": SESSION_NUMBER,
    "round": 2,
}, indent=2), encoding="utf-8")

# Append the R1 review (raw) under session-002.md so it stays the
# authoritative review file; Round 2 lives in its own -round2.md file.
# (Per workflow Step 6 #4, "Never edit the saved review file" — so
# the round 2 file is separate.)

# Display with ASCII-safe replacement to avoid cp1252 crash
import re
safe_preview = re.sub(r'[^\x00-\x7f]', '?', result.content)
print(f"Verifier model: {result.model_name}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Cost USD: ${result.cost_usd:.4f}")
print(f"Output written to: {out_path}")
print("--- LAST 2000 CHARS (ASCII-safe) ---")
print(safe_preview[-2000:])

# Log step
log = SessionLog(SESSION_SET)
log.log_step(
    session_number=SESSION_NUMBER,
    step_number=11,
    step_key="session-2/verify-round2",
    description="Cross-provider session-verification (Round 2) via gpt-5-4",
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "session-verification",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)
print("Step logged.")
