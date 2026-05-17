"""
Session 1 / Step 3: route a cross-provider code-review of the schema + sample.

Per docs/session-sets/lowdefy-ir-foundation/ai-assignment.md, this is a routed
`code-review` call (tier 2). It runs AFTER the orchestrator has:
  - generated the schema via a routed analysis call (Step 1),
  - hand-authored the sample (Step 2),
  - and validated the sample against the schema mechanically (jsonschema 2020-12).

The review should focus on DESIGN issues, not mechanical authoring bugs. The
sample is known to validate; the review should evaluate whether the schema's
abstractions are right, what's missing for downstream codegen, and where the
hand-author hit friction.

Run from the repo root:
    .venv/Scripts/python.exe tools/review_modal_detail_form_session1.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

from ai_router import route

REPO_ROOT = Path(__file__).resolve().parents[3]
SCHEMA_PATH = REPO_ROOT / "AccessHarvester.Core" / "Ir" / "ModalDetailFormSchema.json"
SAMPLE_PATH = REPO_ROOT / "AccessHarvester.Core" / "Ir" / "samples" / "frmBudgetServicePlan.ir.yaml"
SYNTHESIS_PATH = REPO_ROOT / "docs" / "lowdefy-evaluation" / "synthesis.md"
SESSION_SET = "docs/session-sets/lowdefy-ir-foundation"


def build_prompt() -> str:
    schema = SCHEMA_PATH.read_text(encoding="utf-8")
    sample = SAMPLE_PATH.read_text(encoding="utf-8")
    synthesis = SYNTHESIS_PATH.read_text(encoding="utf-8")
    return f"""
You are doing a cross-provider design review of the FIRST first-class IR
schema for an Access-to-Blazor code generator, paired with the first
hand-authored sample that uses it.

The schema validates the sample mechanically (jsonschema Draft 2020-12,
strict). Both files are below. Mechanical authoring bugs in the schema have
been fixed by the orchestrator; do NOT spend tokens on `additionalProperties`
vs `unevaluatedProperties` or `oneOf` ambiguity issues -- those have already
been resolved. Spend your effort on DESIGN.

# What the review must produce

A markdown document with these sections, in order:

1. **Verdict.** Either "VERIFIED" (the design holds; only minor refinements
   needed) or "ISSUES_FOUND" (one or more major design issues need resolution
   before Session 2 builds the slice generator).

2. **Findings.** Numbered list. For each finding:
   - **Severity**: Critical / Major / Minor.
   - **Location**: file + json-path or yaml-path (schema or sample).
   - **Observation**: what is the design issue?
   - **Why it matters**: how would this trip up Session 2's T4 generator, the
     human author of the next sample, or the cross-file `_ref` resolver?
   - **Recommendation**: a concrete change to schema or sample. If you would
     defer the change to a future session, say so explicitly.

   Findings to specifically evaluate -- include each, even if you decide it's
   not a problem:

   a. **`_state` pattern is `^[a-zA-Z0-9_]+$` (no dots).** The hand-author
      worked around this by using only top-level state keys. Is flat state
      sufficient, or does a future ModalDetailForm need nested state
      (e.g., `currentRecord.X`)?

   b. **`Request` action has no per-call parameter overrides.** Parameters
      live on the request definition. Is this the right boundary, or will
      complex flows need per-call dynamic parameters? The sample exposes
      this with `loadNeedsForClient` -- its `clientId` parameter comes
      from `loadPlan.ClientID`, which works only because that request has
      already fired.

   c. **`binding` accepts any expression (a literal string OR an operator
      tree) but does not constrain literal strings to the `^[a-zA-Z0-9_]+
      \\.[a-zA-Z0-9_]+$` pattern that `_request` does.** Should there be a
      `_binding` operator for explicit request-output refs, or is the
      current "string is a literal expression that codegen parses as
      a dotted ref" acceptable?

   d. **`Validate.params.rules[]` items have `expression` + `message` but
      no `field` target.** The sample worked around this with
      `_request: "loadPlan.X"` references in the expression. Is that the
      right abstraction (rules are arbitrary boolean predicates over the
      whole IR state) or should rules be field-scoped?

   e. **Two-way binding semantics.** The schema has `TextBox.binding` and
      `TextBox.onValueChanged` but never specifies whether editing a bound
      TextBox writes back into the bound request's row or into `state`.
      The sample assumes the request's record is mutable (Access-style).
      Is that the design, or should the schema model write-back explicitly?

   f. **ListBox is not in the closed control catalog.** The source form has
      two ListBoxes (master-detail list panels). Per the synthesis, those
      belong to the `MasterDetailListPage` type. The hand-author dropped
      them. Is that the right scope decision, or should ModalDetailForm
      include ListBox for inline lists?

   g. **`modalType: "modal"` vs source form's `Modal: "False"`.** The
      hand-author chose `modal` based on migration intent (would be a
      MudDialog in Blazor), not source-form mechanical state. Is this the
      right semantics for `modalType`, or should it reflect the source?

   h. **Operator catalog completeness.** The schema's closed catalog is
      `_nz, _iif, _dlookup, _dcount, _dsum, _format, _add, _sub, _mul, _div,
      _mod, _eq, _ne, _lt, _le, _gt, _ge, _and, _or, _not, _isnull, _state,
      _request, _const`. What's missing that an Access-to-Blazor migration
      will hit in the next 5 forms? (E.g., string ops: Trim, Left, Right,
      Mid, Len, InStr, UCase, LCase, Replace, Concat; date ops: DateAdd,
      DateDiff, DatePart, Now, Date; type coercion: CStr, CInt, CDate,
      CCur.)

   i. **Action catalog completeness.** The schema's closed catalog is
      `Validate, Request, SetState, CallMethod, CloseModal, ShowError,
      Navigate, Confirm`. What's missing? (E.g., RefreshRequest, SetFocus,
      ClearForm, ToggleVisibility, OpenForm/OpenReport for Switchboard-style
      navigation that doesn't use modalType=page semantics.)

   j. **`debounce` on Events is currently a sibling to `actions` and
      `tryCatch`, not a wrapper.** The schema permits an event to have
      `actions`, `tryCatch`, and `debounce` simultaneously. What's the
      execution semantics when all three are set? Is the schema's intended
      order of evaluation explicit enough?

3. **Suggested ordering.** Of the findings, list which the orchestrator
   should fix BEFORE committing Session 1 (true blockers for Session 2),
   vs which should land in `schema-refinements.md` as backlog for later
   sessions.

# Files under review

## Schema: AccessHarvester.Core/Ir/ModalDetailFormSchema.json

```json
{schema}
```

## Sample: AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml

```yaml
{sample}
```

## Reference: docs/lowdefy-evaluation/synthesis.md (May 2026 consensus)

```markdown
{synthesis}
```

# End of brief

Output the review markdown directly -- no preamble, no commentary outside
the structure above. The orchestrator will read the markdown and decide
which findings to apply before commit.
""".strip()


def main() -> int:
    prompt = build_prompt()
    print(f"Prompt length: {len(prompt)} chars", file=sys.stderr)
    print("Routing...", file=sys.stderr)

    result = route(
        content=prompt,
        task_type="code-review",
        context=(
            "Cross-provider design review of the first ModalDetailForm IR "
            "schema and hand-authored sample for Access-to-Blazor codegen."
        ),
        complexity_hint=45,
        session_set=SESSION_SET,
        session_number=1,
    )

    print(f"Model: {result.model_name} ({result.model_id})", file=sys.stderr)
    print(f"Tokens: {result.input_tokens} in / {result.output_tokens} out", file=sys.stderr)
    print(f"Cost: ${result.cost_usd:.4f}", file=sys.stderr)
    print(f"Elapsed: {result.elapsed_seconds:.1f}s", file=sys.stderr)
    print(f"Escalated: {result.escalated}", file=sys.stderr)
    if result.truncated:
        print("WARNING: response was truncated!", file=sys.stderr)

    review_dir = REPO_ROOT / SESSION_SET / "session-reviews"
    review_dir.mkdir(parents=True, exist_ok=True)
    review_path = review_dir / "session-001-step-03-cross-provider-review.md"
    review_path.write_text(result.content, encoding="utf-8")
    print(f"Wrote {review_path}", file=sys.stderr)

    api_call = {
        "model": result.model_name,
        "taskType": "code-review",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }
    print(json.dumps(api_call))
    return 0


if __name__ == "__main__":
    sys.exit(main())
