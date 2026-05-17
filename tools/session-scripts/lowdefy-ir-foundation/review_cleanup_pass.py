"""
Post-set cleanup pass: route a code-review of the SR fixes before commit.

This isn't a formal session step (the set was already VERIFIED and closed by
commit 3503242). It's a sanity-net for the user-requested cleanup pass that
expanded the schema (SR-G/A/3/1/O) and the generator (SR-H/I/F/K/L/P + the
new operators + cardinality dispatch).
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
    schema = read(REPO_ROOT / "AccessHarvester.Core" / "Ir" / "ModalDetailFormSchema.json")
    sample = read(REPO_ROOT / "AccessHarvester.Core" / "Ir" / "samples" / "frmBudgetServicePlan.ir.yaml")
    gen_prog = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "Program.cs")
    gen_mdf = read(REPO_ROOT / "tools" / "AccessHarvester.IrCodegen" / "ModalDetailFormGenerator.cs")
    razor = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor")
    razor_cs = read(REPO_ROOT / "output-tests" / "blazor-add-an-order" / "Ui" / "Pages" / "Generated" / "FrmBudgetServicePlan.razor.cs")
    refinements = read(SESSION_SET / "schema-refinements.md")
    return f"""
You are reviewing a post-set cleanup pass on the `lowdefy-ir-foundation`
session set, applied AFTER the set was already VERIFIED and closed
(commit 3503242). The cleanup addresses 11 of the schema-refinements
items the original Session 2 wrap-up captured as backlog:

- SR-1 (Major): optional `field` on Validate rule items, per-field SetFieldError emit.
- SR-3 (Major): 24 new operators added to the catalog + generator emits.
- SR-A (Major, covers SR-M): explicit `cardinality: single | list | command` on requests.
- SR-F (Minor): state initial values emitted as C# field initializers.
- SR-G (Minor): `propertyNames: {{ pattern: "^on[A-Z][a-zA-Z0-9]*$" }}` on events.
- SR-H (Major): visible/enabled emit (@if + Disabled).
- SR-I (Major): operators section emits one instance method per entry; _op references method.
- SR-K (Minor): debounce TODO comment in handlers.
- SR-L (Minor): suppress Section title when modal title matches.
- SR-O (Minor): dataType on writable controls dispatches MudTextField / MudNumericField / MudDatePicker.
- SR-P (Minor): non-async Task for sync handlers.

The pilot rebuilds clean (`dotnet build output-tests/blazor-add-an-order`
-> 0 errors / 0 warnings) and the sample mechanically validates against
the updated schema (jsonschema Draft 2020-12). Mechanical correctness
is established; this review is for DESIGN soundness, REGRESSIONS, and
NEW gaps.

# What to evaluate

1. **Did any prior SR fix get unintentionally undone?** (SR-J boolean
   coercion, SR-C writable-binding constraint, SR-D ListBox decision,
   SR-G original pattern.)
2. **Are the new operators emitted correctly?** Pay attention to
   `_concat`, `_mid`, `_instr`, the `_dateadd`/`_datediff`/`_datepart`
   helpers. Any C# semantics that won't match Access semantics?
3. **Does the cardinality dispatch hold up?** Specifically: any code
   path that still relies on a request having a row field, even when
   `cardinality: command`?
4. **`_op` operator's compile path.** If an IR uses `_op: "X"` but
   `X` is not in the `operators` section, what happens? Is the
   failure mode acceptable?
5. **`@if (visible)` wrapping.** Razor's `@if` blocks inside MudStack
   children — anything that could trip up the Razor compiler?
6. **`dataType` dispatch.** `MudDatePicker` uses different attribute
   names (Date / DateChanged) than `MudTextField` (Value / ValueChanged).
   Did the generator handle this correctly?
7. **Backward compatibility.** A pre-cleanup IR (no cardinality, no
   dataType, no field on rules) should still produce valid output.
   Did anything default-bind unsafely?

# What to produce

Markdown report:

1. **Verdict:** `VERIFIED` or `ISSUES_FOUND`.
2. **Findings** if any (severity + location + observation + recommendation).
3. **Confirmation list:** items the cleanup got right that you
   independently verify.

# Files under review

## Schema (post-cleanup)

```json
{schema}
```

## Sample IR (post-cleanup)

```yaml
{sample}
```

## Generator Program.cs

```csharp
{gen_prog}
```

## Generator ModalDetailFormGenerator.cs

```csharp
{gen_mdf}
```

## Generated .razor

```razor
{razor}
```

## Generated .razor.cs

```csharp
{razor_cs}
```

## schema-refinements.md (post-cleanup)

```markdown
{refinements}
```

End of brief.
""".strip()


def main() -> int:
    prompt = build_prompt()
    print(f"Prompt length: {len(prompt)} chars", file=sys.stderr)
    print("Routing cleanup-pass code-review...", file=sys.stderr)

    result = route(
        content=prompt,
        task_type="code-review",
        context=(
            "Post-set cleanup pass on lowdefy-ir-foundation. Verify that the SR fixes "
            "don't regress prior fixes and that the schema/generator/sample remain coherent."
        ),
        complexity_hint=50,
        session_set=str(SESSION_SET.relative_to(REPO_ROOT)).replace("\\", "/"),
        session_number=2,
    )

    print(f"Reviewer: {result.model_name} ({result.model_id})", file=sys.stderr)
    print(f"Tokens: {result.input_tokens} in / {result.output_tokens} out", file=sys.stderr)
    print(f"Cost: ${result.cost_usd:.4f}", file=sys.stderr)
    print(f"Elapsed: {result.elapsed_seconds:.1f}s", file=sys.stderr)

    out = SESSION_SET / "session-reviews" / "post-set-cleanup-review.md"
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
