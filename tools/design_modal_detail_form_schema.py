"""
Session 1 / Step 1: route an `analysis` call to design ModalDetailFormSchema.json.

Per docs/session-sets/lowdefy-ir-foundation/ai-assignment.md, schema design is a
routed analysis call (tier 2, complexity_hint=40). This script:
  1. Builds the design prompt from synthesis + spec + form-shape grounding.
  2. Calls ai_router.route().
  3. Extracts the JSON Schema body from the response.
  4. Writes AccessHarvester.Core/Ir/ModalDetailFormSchema.json.
  5. Returns RouteResult fields needed to log the step in activity-log.json.

Run from the repo root:
    .venv/Scripts/python.exe tools/design_modal_detail_form_schema.py
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from ai_router import route

REPO_ROOT = Path(__file__).resolve().parent.parent
SCHEMA_OUT = REPO_ROOT / "AccessHarvester.Core" / "Ir" / "ModalDetailFormSchema.json"
SESSION_SET = "docs/session-sets/lowdefy-ir-foundation"

SYNTHESIS_PATH = REPO_ROOT / "docs" / "lowdefy-evaluation" / "synthesis.md"
SPEC_PATH = REPO_ROOT / "docs" / "session-sets" / "lowdefy-ir-foundation" / "spec.md"


def build_prompt() -> str:
    synthesis = SYNTHESIS_PATH.read_text(encoding="utf-8")
    spec = SPEC_PATH.read_text(encoding="utf-8")

    return f"""
You are designing the FIRST first-class IR schema for an Access-to-Blazor code
generator. The target type is **ModalDetailForm** -- one of several domain DSL
types that will eventually cover the migration (others: MasterDetailListPage,
ContinuousForm, Switchboard, ReportParameterForm, Report, Subreport).

# Your output

A single, complete JSON Schema (Draft 2020-12) for ModalDetailForm IR files.
Output it inside a fenced ```json block at the END of your response. Before
the block, in <= 400 words of prose, explain:

  - which design decisions you made where the synthesis was silent or
    ambiguous,
  - any closed-set choices (operator catalog, action catalog, control-type
    catalog) and why those specific members,
  - which downstream concerns (T4 code generation, `_ref` cross-file resolution
    later, Blazor / MudBlazor target idioms) you weighted into shape decisions.

The schema must be the LAST artifact in the response, fenced as ```json, with
no trailing prose after the closing fence.

# Mandatory schema properties

1. `$schema`: "https://json-schema.org/draft/2020-12/schema"
2. `title` and `description` at the top level
3. **Strict**: every object uses `additionalProperties: false` (no exceptions),
   `required` lists every field that must be present, every property declares
   its type. Reject unknown properties everywhere.
4. **First-class type discriminator**: a top-level `type` (or `pageType`)
   field with `const: "ModalDetailForm"` so cross-file tools can recognize
   the IR variant without inferring from shape.
5. **Block / Request separation** (the synthesis's #1 borrow): Requests and
   the layout block tree are independent top-level sections. Layout blocks
   bind to request outputs by name; a single request can feed multiple
   bindings.
6. **Named queries / commands, not inline SQL**: Requests reference a named
   query or command in a registry external to the IR. The IR carries the
   *reference* and its parameter bindings, NOT the SQL string. (Inline SQL
   in the IR is the WRONG abstraction per the synthesis's "thin named-query
   layer" decision -- the prior schema attempt embedded SQL and that was
   explicitly flagged as a defect.)
7. **C#-shaped operator catalog as a CLOSED enum** of operator-key names. At
   minimum include: `_nz`, `_iif`, `_dlookup`, `_dcount`, `_dsum`, `_format`,
   `_add`, `_sub`, `_mul`, `_div`, `_mod`, `_eq`, `_ne`, `_lt`, `_le`, `_gt`,
   `_ge`, `_and`, `_or`, `_not`, `_isnull`, `_state`, `_request`, `_const`.
   Each operator's argument arity should be specifiable so the schema rejects
   `_iif` with two args or `_eq` with three. (You can model arity with
   per-operator `oneOf` branches.)
8. **Events as ordered Actions** with a CLOSED action-type catalog, including
   at least: `Validate`, `Request`, `SetState`, `CallMethod`, `CloseModal`,
   `ShowError`, `Navigate`, `Confirm`. Each action's `params` shape is
   discriminated by `type` (use `oneOf` with `discriminator.propertyName`).
9. **`try`/`catch` and `debounce` hygiene** on the action sequence (the
   synthesis explicitly lists these as borrowed primitives). Model these as
   first-class action types or as wrappers (your call -- explain in the
   prose).
10. **`CallMethod` is the escape hatch**, NOT a `_js` operator. The synthesis
    is explicit: escape hatch is "a named method on a partial class
    (`<Page>.Custom.cs`)". `CallMethod` carries `methodName` and an args
    array; the runtime resolves it to a method on the page's partial-class
    code-behind.
11. **page vs modal vs drawer** distinction at metadata level (the synthesis
    calls this out as a borrow item, mapping to Access's `acDialog`/`PopUp`/
    `Modal`).
12. **Control-type catalog**: a closed enum of layout block types. For
    ModalDetailForm, at minimum support `TextBox`, `Label`, `ComboBox`,
    `CheckBox`, `Button`, `Line`, `Section`, `Row`, `Column`, `Group`. (Other
    types -- subforms, list boxes, etc. -- belong to other domain types,
    keep them out.) Each block type's allowed properties differ: `Button`
    has no `binding`, `Section` has `blocks: []`, etc. Discriminate by
    `type`.
13. **Binding shape**: A block's `binding` references a request output as
    `<requestName>.<column>` (string) OR is an operator expression. Reading
    a request's output without binding to a column should fail validation.
14. **`_ref` documented but unimplemented**: include an `$comment` or
    `description` noting that cross-file `_ref` resolution is a preprocessing
    step performed before this schema validates; the schema itself does not
    accept `_ref` nodes.

# Anti-requirements (things to NOT include)

- No `_js` or `_javascript` operator (wrong target language).
- No generic connector abstraction (EF Core against SQL Server / Postgres is
  fixed; named queries/commands suffice).
- No inline SQL strings inside Request bodies.
- No report-shaped properties (`LinkMasterFields`, `GroupLevel`, page
  headers, etc.) -- those belong to the Report IR.
- No generic open Block composition: blocks are a closed enum, not arbitrary
  custom types.

# Target form (concrete grounding)

The schema will be exercised by hand-authoring a sample IR for
`frmBudgetServicePlan` from `samples/HomeHealthCare.accdb`. The form has:

- RecordSource: `SELECT cb.*, Trim(Nz([c.FirstName],"") & ' ' & Nz([c.LastName],"")) AS ClientName FROM tblClientBudgets AS cb INNER JOIN tblClients AS c ON cb.ClientID = c.ClientID`
  (note: no WHERE clause; in Access this form receives a record via OpenArgs
  and filters in code, NOT in the RecordSource).
- ~22 distinct controls: ~6 Labels, ~7 TextBoxes (txtClientBudgetID,
  txtFiscalYear, txtAnnualBudget, txtRemaining, txtTotalPlanned, ...), ~3
  ComboBoxes, ~2 Buttons, 2 Lines.
- Event procs (will be VBA, translated into action sequences): OnLoad,
  OnCurrent, OnDirty, OnUndo, BeforeUpdate, AfterUpdate, OnOpen.
- Modal: "False", PopUp: "False" in the Access metadata -- so when this is
  mapped to ModalDetailForm IR, `modalType` will be one of the non-modal
  variants (page/drawer) OR the author will need a justification for
  overriding to `modal`. The schema must allow either.

You do not need to write the sample -- that's the next step. Just make sure
the schema can REPRESENT this form's control set, event procs, and
parameter-by-OpenArgs pattern.

# Cross-provider synthesis (full text)

This is the May 2026 consensus that all three providers signed off on
(Claude, Gemini Pro, Codex). Treat the borrow items and reject items as
binding constraints, not suggestions.

```
{synthesis}
```

# Spec scope (full text)

This is the session set spec for context on what Sessions 1 and 2 cover. You
are designing the schema referenced in Feature 1 only.

```
{spec}
```

# Reminder

The prior attempt at this schema (commit 41694a3, by Haiku) was reverted for
two reasons relevant to your output:

1. The schema was internally directionally OK but lacked a closed operator
   catalog, lacked `try`/`catch`/`debounce`, and the action `Validate` had no
   `rules` shape. Don't repeat these gaps.
2. The schema and the hand-authored sample disagreed on the shape (array vs
   object for `requests`, recursive block tree vs sections-with-controls for
   `layout`, `type` vs `action` keyword for action items). Pick one shape per
   axis and document it clearly so the sample author has no ambiguity.

Output prose followed by the schema in a fenced ```json block. End of prose.
""".strip()


def extract_schema(content: str) -> dict:
    """Pull the last ```json block out of the response."""
    blocks = re.findall(r"```json\s*\n(.*?)\n```", content, re.DOTALL)
    if not blocks:
        raise ValueError("No ```json fenced block found in response")
    body = blocks[-1].strip()
    return json.loads(body)


def main() -> int:
    prompt = build_prompt()
    print(f"Prompt length: {len(prompt)} chars", file=sys.stderr)
    print("Routing...", file=sys.stderr)

    result = route(
        content=prompt,
        task_type="analysis",
        context=(
            "Designing the first first-class domain-DSL IR schema for "
            "Access-to-Blazor codegen. Bounded scope: ModalDetailForm only."
        ),
        complexity_hint=40,
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

    # Save raw response for the session-review folder
    review_dir = REPO_ROOT / SESSION_SET / "session-reviews"
    review_dir.mkdir(parents=True, exist_ok=True)
    (review_dir / "session-001-step-01-schema-design.md").write_text(
        result.content, encoding="utf-8"
    )

    try:
        schema = extract_schema(result.content)
    except (ValueError, json.JSONDecodeError) as e:
        print(f"ERROR extracting schema: {e}", file=sys.stderr)
        print("Raw response saved; inspect session-001-step-01-schema-design.md", file=sys.stderr)
        return 1

    SCHEMA_OUT.parent.mkdir(parents=True, exist_ok=True)
    SCHEMA_OUT.write_text(json.dumps(schema, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {SCHEMA_OUT}", file=sys.stderr)

    # Print the routedApiCall entry to stdout so the caller can append it
    api_call = {
        "model": result.model_name,
        "taskType": "analysis",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }
    print(json.dumps(api_call))
    return 0


if __name__ == "__main__":
    sys.exit(main())
