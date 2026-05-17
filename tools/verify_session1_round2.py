"""
Session 1 / Step 7: save the issue log for round 1 findings and re-run
session-verification at complexity_hint=85 per the workflow.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parent.parent
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "lowdefy-ir-foundation"
SCHEMA_PATH = REPO_ROOT / "AccessHarvester.Core" / "Ir" / "ModalDetailFormSchema.json"
SAMPLE_PATH = REPO_ROOT / "AccessHarvester.Core" / "Ir" / "samples" / "frmBudgetServicePlan.ir.yaml"
SPEC_PATH = SESSION_SET / "spec.md"


def main() -> int:
    # Save the issue log for round 1 with our resolutions.
    round1_issues = [
        {
            "issueNumber": 1,
            "severity": "Major",
            "category": "Completeness",
            "location": "AccessHarvester.Core/Ir/ModalDetailFormSchema.json",
            "summary": "Schema omitted the spec-required `operators` section for named reusable operator trees.",
            "resolution": "fixed",
            "resolutionNotes": "Added top-level `operators` property (object mapping name -> expression) and an `_op` reference operator to the expression catalog. References resolve at codegen time.",
        },
        {
            "issueNumber": 2,
            "severity": "Major",
            "category": "Correctness",
            "location": "AccessHarvester.Core/Ir/ModalDetailFormSchema.json#/properties/layout",
            "summary": "Root `layout` was not enforced as a container despite description saying so.",
            "resolution": "fixed",
            "resolutionNotes": "Added `$defs/containerBlock` (oneOf restricted to Section/Row/Column/Group) and pointed `layout` at it. Leaf controls at the root now fail validation.",
        },
        {
            "issueNumber": 3,
            "severity": "Major",
            "category": "Correctness",
            "location": "AccessHarvester.Core/Ir/ModalDetailFormSchema.json#/$defs/controls",
            "summary": "Writable bindings accepted non-assignable expressions, contradicting two-way binding semantics.",
            "resolution": "fixed",
            "resolutionNotes": "Added if/then on TextBox/ComboBox/CheckBox: if readOnly is not true, binding MUST be an object with `_request`. Computed expressions are now only valid on readOnly: true controls.",
        },
        {
            "issueNumber": 4,
            "severity": "Minor",
            "category": "Correctness",
            "location": "AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml#/events/onLoad",
            "summary": "Sample used `onLoad` but the spec called for `onInit`.",
            "resolution": "fixed",
            "resolutionNotes": "Renamed `onLoad:` to `onInit:` in the sample. Sample re-validates against the schema.",
        },
    ]
    log = SessionLog(SESSION_SET)
    log.save_issue_log(session_number=1, issues=round1_issues)
    print(f"Saved issue log for round 1 (4 issues, all fixed).", file=sys.stderr)

    # Build round-2 prompt: same shape but mention the fixes for context
    schema = SCHEMA_PATH.read_text(encoding="utf-8")
    sample = SAMPLE_PATH.read_text(encoding="utf-8")
    spec = SPEC_PATH.read_text(encoding="utf-8")

    prompt = f"""
You are the independent end-of-session verifier for Session 1 of the
`lowdefy-ir-foundation` session set, ROUND 2.

In Round 1, you (or a peer verifier) returned ISSUES_FOUND with four
findings. The orchestrator accepted all four and applied fixes:

1. Added top-level `operators` section (map of name -> expression) and a
   new `_op` reference operator in the expression catalog.
2. Added `$defs/containerBlock` and re-pointed `layout` at it, so the root
   is restricted to Section/Row/Column/Group.
3. Added if/then on TextBox/ComboBox/CheckBox: if `readOnly` is not true,
   `binding` MUST be a direct `_request` operator (non-assignable
   computed bindings are now only valid on readOnly: true controls).
4. Renamed `onLoad` to `onInit` in the sample to match spec Feature 2.

The sample mechanically validates against the schema via jsonschema Draft
2020-12. Re-evaluate the deliverables below. Apply the same JSON verdict
format as Round 1.

# Session spec (full)

```markdown
{spec}
```

# Deliverable 1: AccessHarvester.Core/Ir/ModalDetailFormSchema.json

```json
{schema}
```

# Deliverable 2: AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml

```yaml
{sample}
```

# Verdict requirements

Output your verdict in this exact JSON shape inside a fenced ```json block:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "category": "Correctness" | "Completeness" | "False Positive",
      "location": "<file path or json/yaml path>",
      "summary": "<one sentence>",
      "details": "<what is wrong and what the correct answer should be>"
    }}
  ]
}}
```

If verdict is `VERIFIED`, `issues` MUST be an empty array `[]`.

Check that the Round 1 findings are actually fixed (not just superficially)
and that no new issues were introduced by the fixes.
""".strip()

    print(f"Prompt length: {len(prompt)} chars", file=sys.stderr)
    print("Routing round-2 verification (complexity_hint=85)...", file=sys.stderr)

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,
        session_set=str(SESSION_SET.relative_to(REPO_ROOT)).replace("\\", "/"),
        session_number=1,
    )

    print(f"Verifier: {result.model_name} ({result.model_id})", file=sys.stderr)
    print(f"Tokens: {result.input_tokens} in / {result.output_tokens} out", file=sys.stderr)
    print(f"Cost: ${result.cost_usd:.4f}", file=sys.stderr)
    print(f"Elapsed: {result.elapsed_seconds:.1f}s", file=sys.stderr)

    log.save_session_review(
        session_number=1,
        review_text=result.content,
        round_number=2,
    )
    print("Saved round-2 review.", file=sys.stderr)

    verdict = "UNKNOWN"
    issues_count = 0
    try:
        m = re.search(r'```json\s*\n(.*?)\n```', result.content, re.DOTALL)
        parsed = json.loads(m.group(1)) if m else None
        if parsed:
            verdict = parsed.get("verdict", "UNKNOWN")
            issues_count = len(parsed.get("issues", []))
    except Exception as e:
        print(f"Could not parse verdict JSON: {e}", file=sys.stderr)

    print(f"ROUND 2 VERDICT: {verdict} (issues: {issues_count})", file=sys.stderr)

    print(json.dumps({
        "model": result.model_name,
        "taskType": "session-verification",
        "round": 2,
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
        "verdict": verdict,
        "issuesCount": issues_count,
    }))
    return 0


if __name__ == "__main__":
    sys.exit(main())
