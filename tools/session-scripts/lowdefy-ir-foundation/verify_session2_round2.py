"""
Session 2 / Step 7: round-2 verification with reshaped context.

Round 1 (gpt-5-4, $0.16) returned ISSUES_FOUND with 2 findings:
  - F1 (Major, Completeness): generator landed as a console exe rather than
    a T4 template or C# Source Generator. The orchestrator disagreed: the
    user explicitly chose the console-exe approach via an in-session
    decision prompt, and the spec has now been amended to record that.
    Resolution = reverify-reshaped; cause = context-gap.
  - F2 (Minor, False Positive): validation notes overstated coverage of
    Step 4 findings -- specifically the trailing `await Task.CompletedTask`
    item. Resolution = accept-finding; added SR-P to the refinements log
    and reworded the notes.

This round-2 prompt is a full re-verification with the amended spec and
updated notes/refinements log. It also passes adjudication context via
ai_router.record_adjudication so the disagreement appears in metrics.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from ai_router import route, record_adjudication
from ai_router.config import load_config
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "lowdefy-ir-foundation"


def read(p): return p.read_text(encoding="utf-8")


def build_prompt() -> str:
    spec = read(SESSION_SET / "spec.md")
    gen_prog = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "Program.cs")
    gen_mdf = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "ModalDetailFormGenerator.cs")
    razor = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor")
    razor_cs = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor.cs")
    val_notes = read(SESSION_SET / "generator-validation-notes.md")
    refinements = read(SESSION_SET / "schema-refinements.md")
    step4 = read(SESSION_SET / "session-reviews" / "session-002-step-04-cross-provider-review.md")
    return f"""
You are the independent end-of-session verifier for Session 2 of the
`lowdefy-ir-foundation` session set, ROUND 2.

In Round 1 you returned ISSUES_FOUND with 2 findings:

1. **(Major, Completeness):** Generator landed as a standalone console
   exe rather than a T4 template or C# Source Generator. The verifier
   noted: "Unless the spec was explicitly amended to permit a standalone
   console generator as the replacement, this is a deliverable-format
   gap." The orchestrator submits that **the spec WAS amended this
   session** to permit the console-exe approach, in response to an
   explicit human-in-the-loop decision prompt. The amended spec is
   below; the amendment is recorded in spec.md inline, immediately
   under the "Feature 3: Minimal Slice Generator" / Approach heading,
   and in the Deliverables section. Re-evaluate Finding 1 against the
   amended spec.

2. **(Minor, False Positive in the orchestrator's notes):** Validation
   notes overstated coverage of Step 4 findings -- the trailing
   `await Task.CompletedTask` finding was not in schema-refinements.md.
   Accepted: SR-P has been added to the refinements log, and the notes
   were reworded so they don't overstate coverage.

Evaluate the same five dimensions as round 1 (spec deliverable gaps,
routed-call compliance, correctness regressions, validation notes
honesty, refinements log completeness) against the materials below.

# Amended Session 2 spec (full)

```markdown
{spec}
```

# Generator Program.cs

```csharp
{gen_prog}
```

# Generator ModalDetailFormGenerator.cs

```csharp
{gen_mdf}
```

# Generated .razor

```razor
{razor}
```

# Generated .razor.cs

```csharp
{razor_cs}
```

# generator-validation-notes.md (reworded after round 1)

```markdown
{val_notes}
```

# schema-refinements.md (SR-P added after round 1)

```markdown
{refinements}
```

# Step 4 cross-provider review (unchanged from round 1; included for context)

```markdown
{step4}
```

# Verdict requirements

Same JSON shape as round 1 inside a fenced ```json block:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [ ... ]
}}
```

If verdict is `VERIFIED`, `issues` MUST be `[]`.
""".strip()


def main() -> int:
    prompt = build_prompt()
    print(f"Prompt length: {len(prompt)} chars", file=sys.stderr)

    # Record the adjudication for F1 BEFORE the round-2 call so the metric
    # appears regardless of round-2's outcome.
    config = load_config()
    record_adjudication(
        config,
        task_type="session-verification",
        cause="context-gap",
        resolution="reverify-reshaped",
        session_set=str(SESSION_SET.relative_to(REPO_ROOT)).replace("\\", "/"),
        session_number=2,
        generator_model="claude-opus-4-7",
        verifier_model="gpt-5-4",
        finding_summary=(
            "Generator landed as standalone console exe rather than T4 template; "
            "user explicitly chose this approach via in-session decision prompt."
        ),
        dismissal_reason=(
            "Spec was amended this session to record the console-exe approach as a "
            "permitted deliverable shape; the round-1 verifier did not see the "
            "amendment."
        ),
    )
    print("Recorded F1 adjudication (context-gap, reverify-reshaped).", file=sys.stderr)

    print("Routing round-2 verification (complexity_hint=85)...", file=sys.stderr)
    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,
        session_set=str(SESSION_SET.relative_to(REPO_ROOT)).replace("\\", "/"),
        session_number=2,
    )

    print(f"Verifier: {result.model_name} ({result.model_id})", file=sys.stderr)
    print(f"Tokens: {result.input_tokens} in / {result.output_tokens} out", file=sys.stderr)
    print(f"Cost: ${result.cost_usd:.4f}", file=sys.stderr)

    log = SessionLog(SESSION_SET)
    log.save_session_review(session_number=2, review_text=result.content, round_number=2)

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
