"""
Session 2 / Step 4: route a cross-provider code-review of the generator,
its output, the validation notes, and the schema-refinements log.

Per docs/session-sets/lowdefy-ir-foundation/ai-assignment.md, this is a
routed `code-review` call (tier 2). The orchestrator already evaluated
the generator's output for reasonableness; this routed call provides
cross-provider validation of those findings.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

from ai_router import route

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "lowdefy-ir-foundation"


def read(p): return p.read_text(encoding="utf-8")


def build_prompt() -> str:
    spec = read(SESSION_SET / "spec.md")
    gen_program = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "Program.cs")
    gen_mdf = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "ModalDetailFormGenerator.cs")
    razor = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor")
    razor_cs = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor.cs")
    schema = read(REPO_ROOT / "AccessHarvester.Core" / "Ir" / "ModalDetailFormSchema.json")
    sample = read(REPO_ROOT / "AccessHarvester.Core" / "Ir" / "samples" / "frmBudgetServicePlan.ir.yaml")
    val_notes = read(SESSION_SET / "generator-validation-notes.md")
    refinements = read(SESSION_SET / "schema-refinements.md")
    return f"""
You are doing a cross-provider design and quality review of Session 2 of
the `lowdefy-ir-foundation` session set: a minimal slice generator for
the `ModalDetailForm` IR type, plus its generated output against the
Blazor pilot.

The pilot csproj compiles cleanly with the generated component included
(`dotnet build output-tests/blazor-add-an-order` → 0 errors / 0
warnings). Mechanical authoring concerns are already resolved; focus on
**design, completeness, and false-positive findings** in the
orchestrator-authored notes.

# What to evaluate

For each of the artifacts below, look for:

1. **Generator correctness:** Does the emit logic produce structurally
   sound Blazor + MudBlazor? Are there edge cases the generator silently
   mishandles (e.g., an action type that isn't emitted, an operator that
   produces invalid C#)?
2. **Schema-generator contract:** The schema constrains the IR; the
   generator assumes those constraints. Spots where the generator
   would break if the schema relaxed (or where it imposes constraints
   the schema doesn't actually enforce)?
3. **Generated-output reasonableness:** Is the .razor / .razor.cs pair
   what a hand-written Blazor developer would produce for this form,
   modulo MVP simplifications? Where does it diverge from idiomatic
   Blazor in a way that future hand-authors would push back on?
4. **Validation-notes accuracy:** The orchestrator listed friction
   surfaced during authoring. Anything missing? Anything mis-attributed
   (e.g., a schema issue blamed as a generator issue)?
5. **Refinements log completeness:** The log consolidates Session 1's
   deferred findings plus new Session 2 findings. Anything that should
   be there but isn't? Any item that doesn't belong?

# What to produce

Output a markdown report with these sections:

1. **Verdict.** `VERIFIED` (Session 2 is ready to commit; only minor or
   defer-class refinements) or `ISSUES_FOUND` (something must change
   before commit).

2. **Findings.** Numbered list. For each:
   - **Severity:** Critical / Major / Minor.
   - **Location:** file path + line / json-path / yaml-path if applicable.
   - **Observation:** what the issue is.
   - **Recommendation:** concrete fix or "defer to schema-refinements.md
     as SR-X." For false positives in the orchestrator's notes,
     recommend removing or rewording the item.

3. **Confirmation list.** Items in the orchestrator's findings that you
   independently agree with (validates the cross-provider rigor).

# Files under review

## Spec (full)

```markdown
{spec}
```

## Schema (full, post Session 1 verifier-fix)

```json
{schema}
```

## Sample IR (full)

```yaml
{sample}
```

## Generator: tools/AccessHarvester.IrCodegen/Program.cs

```csharp
{gen_program}
```

## Generator: tools/AccessHarvester.IrCodegen/ModalDetailFormGenerator.cs

```csharp
{gen_mdf}
```

## Generated output: FrmBudgetServicePlan.razor

```razor
{razor}
```

## Generated output: FrmBudgetServicePlan.razor.cs

```csharp
{razor_cs}
```

## Orchestrator's validation notes

```markdown
{val_notes}
```

## Orchestrator's schema-refinements log

```markdown
{refinements}
```

End of brief.
""".strip()


def main() -> int:
    prompt = build_prompt()
    print(f"Prompt length: {len(prompt)} chars", file=sys.stderr)
    print("Routing code-review...", file=sys.stderr)

    result = route(
        content=prompt,
        task_type="code-review",
        context=(
            "Cross-provider review of the minimal ModalDetailForm slice "
            "generator + generated Blazor + validation notes + refinements log."
        ),
        complexity_hint=45,
        session_set=str(SESSION_SET.relative_to(REPO_ROOT)).replace("\\", "/"),
        session_number=2,
    )

    print(f"Model: {result.model_name} ({result.model_id})", file=sys.stderr)
    print(f"Tokens: {result.input_tokens} in / {result.output_tokens} out", file=sys.stderr)
    print(f"Cost: ${result.cost_usd:.4f}", file=sys.stderr)
    print(f"Elapsed: {result.elapsed_seconds:.1f}s", file=sys.stderr)
    if result.truncated:
        print("WARNING: response was truncated!", file=sys.stderr)

    review_dir = SESSION_SET / "session-reviews"
    review_dir.mkdir(parents=True, exist_ok=True)
    out = review_dir / "session-002-step-04-cross-provider-review.md"
    out.write_text(result.content, encoding="utf-8")
    print(f"Wrote {out}", file=sys.stderr)

    print(json.dumps({
        "model": result.model_name,
        "taskType": "code-review",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }))
    return 0


if __name__ == "__main__":
    sys.exit(main())
