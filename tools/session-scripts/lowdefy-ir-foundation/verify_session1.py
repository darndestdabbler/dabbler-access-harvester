"""
Session 1 / Step 6: mandatory routed session-verification call.

Per docs/ai-led-session-workflow.md Step 6, this MUST run before commit /
push of a session. The router routes it to a cross-provider verifier (a
different provider than the orchestrator). The response uses the structured
JSON verdict schema from ai_router/prompt-templates/verification.md.

Run from the repo root:
    .venv/Scripts/python.exe tools/verify_session1.py
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "lowdefy-ir-foundation"
SCHEMA_PATH = REPO_ROOT / "AccessHarvester.Core" / "Ir" / "ModalDetailFormSchema.json"
SAMPLE_PATH = REPO_ROOT / "AccessHarvester.Core" / "Ir" / "samples" / "frmBudgetServicePlan.ir.yaml"
SPEC_PATH = SESSION_SET / "spec.md"
REVIEW_DIR = SESSION_SET / "session-reviews"


def build_prompt() -> str:
    schema = SCHEMA_PATH.read_text(encoding="utf-8")
    sample = SAMPLE_PATH.read_text(encoding="utf-8")
    spec = SPEC_PATH.read_text(encoding="utf-8")

    return f"""
You are the independent end-of-session verifier for Session 1 of the
`lowdefy-ir-foundation` session set in the `dabbler-access-harvester` repo.

The orchestrator (Claude Opus 4.7, 1M context) executed Session 1: designed
a strict JSON Schema for the `ModalDetailForm` IR type via a routed Gemini
Pro analysis call, hand-authored a sample IR file translating
`frmBudgetServicePlan` from HomeHealthCare.accdb, routed a Sonnet 4.6
cross-provider code-review of the schema+sample, and applied six
commit-blocker findings from that review.

You did not produce this work; you are reviewing it. Be skeptical. Look
for: incorrect or silently-relaxed strictness, schema/sample
disagreement, missing pieces from the spec, deviations from the May 2026
cross-provider synthesis's binding constraints, or domain-modeling
errors specific to the Access source form.

# Session spec (full)

```markdown
{spec}
```

# Deliverable 1: AccessHarvester.Core/Ir/ModalDetailFormSchema.json

This is the schema. ~1055 lines, JSON Schema Draft 2020-12 with strict
validation.

```json
{schema}
```

# Deliverable 2: AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml

This is the hand-authored sample IR. It MUST validate against the schema
above via jsonschema's Draft 2020-12 validator (the orchestrator confirms
mechanical validation passes, but you should sanity-check the structural
match).

```yaml
{sample}
```

# Process facts (provided so you can spot gaps)

- Orchestrator: Claude Opus 4.7 (1M context).
- Step 1 (schema design, routed analysis): Gemini Pro,
  $0.0600, 48s.
- Step 2 (hand-author sample, direct): orchestrator.
- Step 3 (cross-provider code-review, routed): Sonnet 4.6,
  $0.1996, 13 findings (verdict ISSUES_FOUND). The orchestrator applied
  six commit-blocker findings (3, 5, 9, 10, 11, 12 in the review). Seven
  deferred to a `schema-refinements.md` backlog Session 2 will produce.
  The full review is at
  `docs/session-sets/lowdefy-ir-foundation/session-reviews/session-001-step-03-cross-provider-review.md`.

- No build/test was required this session (schema + YAML only; no C#
  compilation involved). The sample mechanically validates against the
  schema via jsonschema 4.26.0 Draft 2020-12 validator.

- The PRIOR commit on this work (41694a3, by Haiku 4.5) was reverted because
  the schema and sample disagreed on shape AND both spec-mandated routed
  calls were skipped. The current commit is the redo.

# Verdict requirements

Output your verdict in this exact JSON shape (after any prose findings):

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

Reminder of the criteria you are evaluating against:

1. **Correctness:** Are there factual errors, logical flaws, incorrect
   schema constraints, or wrong conclusions baked into the schema or
   sample?
2. **Completeness:** Did the session miss anything the spec required for
   Session 1? (The spec's Session Plan, Artifacts & Deliverables sections
   are the canonical list.)
3. **False Positives** (from the prior code-review): Did the orchestrator
   apply any of the six commit-blocker findings incorrectly?

The cross-provider synthesis's binding constraints (Block/Request
separation; C#-shaped operator catalog; Events as ordered Actions with
try/catch/debounce; CallMethod as escape hatch not `_js`; named queries
not inline SQL; page/modal/drawer distinction; strict
additionalProperties / unevaluatedProperties throughout) are
non-negotiable.

Do NOT re-do the work. Only evaluate what was produced.
""".strip()


def main() -> int:
    prompt = build_prompt()
    print(f"Prompt length: {len(prompt)} chars", file=sys.stderr)
    print("Routing verification...", file=sys.stderr)

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET.relative_to(REPO_ROOT)).replace("\\", "/"),
        session_number=1,
    )

    print(f"Verifier: {result.model_name} ({result.model_id})", file=sys.stderr)
    print(f"Tokens: {result.input_tokens} in / {result.output_tokens} out", file=sys.stderr)
    print(f"Cost: ${result.cost_usd:.4f}", file=sys.stderr)
    print(f"Elapsed: {result.elapsed_seconds:.1f}s", file=sys.stderr)
    if result.truncated:
        print("WARNING: response was truncated!", file=sys.stderr)

    log = SessionLog(SESSION_SET)
    log.save_session_review(
        session_number=1,
        review_text=result.content,
        round_number=1,
    )
    print(f"Saved session review.", file=sys.stderr)

    # Parse verdict so the orchestrator can decide flow
    verdict = "UNKNOWN"
    issues_count = 0
    try:
        m = re.search(r'```json\s*\n(.*?)\n```', result.content, re.DOTALL)
        if m:
            parsed = json.loads(m.group(1))
        else:
            m = re.search(r'(\{[^{}]*"verdict"[^{}]*\})', result.content, re.DOTALL)
            parsed = json.loads(m.group(1)) if m else None
        if parsed:
            verdict = parsed.get("verdict", "UNKNOWN")
            issues_count = len(parsed.get("issues", []))
    except Exception as e:
        print(f"Could not parse verdict JSON: {e}", file=sys.stderr)

    print(f"VERDICT: {verdict} (issues: {issues_count})", file=sys.stderr)

    api_call = {
        "model": result.model_name,
        "taskType": "session-verification",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
        "verdict": verdict,
        "issuesCount": issues_count,
    }
    print(json.dumps(api_call))
    return 0


if __name__ == "__main__":
    sys.exit(main())
