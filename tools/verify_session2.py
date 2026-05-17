"""
Session 2 / Step 6: mandatory routed session-verification call.
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


def read(p): return p.read_text(encoding="utf-8")


def build_prompt() -> str:
    spec = read(SESSION_SET / "spec.md")
    gen_mdf = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "ModalDetailFormGenerator.cs")
    gen_prog = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "Program.cs")
    razor = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor")
    razor_cs = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor.cs")
    val_notes = read(SESSION_SET / "generator-validation-notes.md")
    refinements = read(SESSION_SET / "schema-refinements.md")
    step4 = read(SESSION_SET / "session-reviews" / "session-002-step-04-cross-provider-review.md")
    return f"""
You are the independent end-of-session verifier for Session 2 of the
`lowdefy-ir-foundation` session set. The orchestrator (Claude Opus 4.7)
built a minimal slice generator in C#, ran it on the Session 1 sample,
compiled the generated `.razor` / `.razor.cs` pair into the Blazor pilot
csproj (0 errors / 0 warnings), authored validation notes and a
schema-refinements log, and routed a cross-provider code-review
(Step 4). Mechanical correctness has been established. Your job is to
evaluate whether Session 2 has actually met its spec, not to rebuild
the work.

Be skeptical. Look for:

- **Spec deliverable gaps:** Are every artifact in the spec's
  "Artifacts & Deliverables -> Session 2" produced and present?
  (T4 template -- or its replacement -- yes? generated output yes?
  generator-validation-notes.md yes? schema-refinements.md yes?)
- **Routed-call compliance:** Were the spec-mandated routed calls
  actually run? (Step 4 routed code-review yes / no?)
- **Correctness regressions:** Did anything from Session 1 break?
  (The schema still validates the sample? The sample still validates
  against the schema?)
- **Honesty of the validation notes:** Does the orchestrator's framing
  match reality? Any oversold claims?
- **Refinements log redundancy or omissions:** Items already addressed
  but listed anyway? Items omitted that the spec required to capture?

# Session 2 spec excerpt (full session set spec attached for context)

```markdown
{spec}
```

# Deliverable 1: Generator (Program.cs)

```csharp
{gen_prog}
```

# Deliverable 1 (continued): ModalDetailFormGenerator.cs

```csharp
{gen_mdf}
```

# Deliverable 2: Generated output (.razor)

```razor
{razor}
```

# Deliverable 2 (continued): Generated output (.razor.cs)

```csharp
{razor_cs}
```

# Deliverable 3: generator-validation-notes.md

```markdown
{val_notes}
```

# Deliverable 4: schema-refinements.md

```markdown
{refinements}
```

# Step 4 routed cross-provider review (for context, treat as another data point)

```markdown
{step4}
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

Do NOT re-do the work. Evaluate only.
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
        session_number=2,
    )

    print(f"Verifier: {result.model_name} ({result.model_id})", file=sys.stderr)
    print(f"Tokens: {result.input_tokens} in / {result.output_tokens} out", file=sys.stderr)
    print(f"Cost: ${result.cost_usd:.4f}", file=sys.stderr)
    print(f"Elapsed: {result.elapsed_seconds:.1f}s", file=sys.stderr)

    log = SessionLog(SESSION_SET)
    log.save_session_review(session_number=2, review_text=result.content, round_number=1)
    print("Saved session review.", file=sys.stderr)

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

    print(f"VERDICT: {verdict} (issues: {issues_count})", file=sys.stderr)
    print(json.dumps({
        "model": result.model_name,
        "taskType": "session-verification",
        "round": 1,
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
        "verdict": verdict,
        "issuesCount": issues_count,
    }))
    return 0


if __name__ == "__main__":
    sys.exit(main())
