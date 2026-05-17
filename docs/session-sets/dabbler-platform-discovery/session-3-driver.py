"""Session 3 driver: smoke-test the access-to-platform migration map.

Picks one Northwind form (frmOrderDetails + its subform sfrmOrderLineItems)
and routes an analysis call that walks the form's structural elements
through the migration map, flagging any gaps the map missed.
"""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "dabbler-platform-discovery"
MIGRATION_MAP = REPO_ROOT / "docs" / "cross-repo" / "access-to-platform-migration-map.md"
COMPONENTS = REPO_ROOT / "docs" / "cross-repo" / "dabbler-platform-components.md"
FORM_YAML = REPO_ROOT / "samples" / "DatabaseYaml" / "NorthwindDevEdition.accdb" / "Forms" / "frmOrderDetails.yaml"
SUBFORM_YAML = REPO_ROOT / "samples" / "DatabaseYaml" / "NorthwindDevEdition.accdb" / "Forms" / "sfrmOrderLineItems.yaml"
RDF_SCHEMA = REPO_ROOT / "docs" / "cross-repo" / "dabbler-platform-report-rdf-schema.md"
OUTPUT = SESSION_SET / "smoke-test-frmOrderDetails.md"


CAPTURE_KEYS = {
    "Name", "ObjectType", "ControlType", "RecordSource", "Caption", "Tag",
    "SourceObject", "LinkMasterFields", "LinkChildFields",
    "ControlSource", "RowSource", "RowSourceType",
    "OnCurrent", "OnLoad", "OnClick", "OnDirty", "OnOpen", "OnClose",
    "BeforeUpdate", "AfterUpdate", "BeforeInsert", "AfterInsert",
    "OnDelete", "OnError", "OnUnload", "OnGotFocus", "OnLostFocus",
    "OnDblClick", "OnChange", "OnKeyPress", "OnKeyDown", "OnKeyUp",
    "OnTimer", "OnMouseMove", "OnMouseDown", "OnMouseUp", "OnEnter",
    "OnExit", "EventProcPrefix", "Validation", "ValidationRule",
    "ValidationText", "DefaultValue", "StatusBarText",
    "Format", "InputMask", "AllowEdits", "AllowAdditions", "AllowDeletions",
    "Visible", "Enabled", "Locked",
}


def digest_form(path: Path) -> str:
    text = path.read_bytes().decode("utf-8", errors="replace").replace("\x00", "")
    out: list[str] = []
    for ln in text.splitlines():
        m = re.match(r"^(\s*)([A-Za-z][A-Za-z0-9_]*):\s*(.*)$", ln)
        if not m:
            continue
        indent, key, val = m.group(1), m.group(2), m.group(3)
        if key in CAPTURE_KEYS and val and val not in ('""', "None"):
            out.append(f"{indent}{key}: {val}")
    return "\n".join(out)


def main() -> int:
    log = SessionLog(SESSION_SET)

    form_digest = digest_form(FORM_YAML)
    subform_digest = digest_form(SUBFORM_YAML)
    migration_map = MIGRATION_MAP.read_text(encoding="utf-8")

    prompt = f"""You are walking a real Microsoft Access form through a draft Access-to-Blazor
migration map to find gaps. The map was authored from a read-only inspection of
the dabbler-platform component library and the report.rdf.json schema. The goal
of this smoke-test is NOT to design a migration; it is to surface migration-map
cells that are missing, ambiguous, or incorrect when applied to a real
multi-feature Access form.

# Migration map (authoritative artifact under test)

{migration_map}

# Form under test: frmOrderDetails (Northwind Developer Edition)

This is a bound master-detail order form. The digest below strips properties
that don't influence migration intent (geometry, fonts, colors, etc.) and
keeps only structural anchors: form-level bindings, control identity and
type, control-source expressions, row sources, subform links, and event
attachments.

The numeric ControlType values correspond to Access constants. The ones used
in this form: 100=Label, 101=Rectangle, 103=Image, 104=CommandButton,
109=TextBox, 111=ComboBox, 112=Subform/Subreport.

## frmOrderDetails (parent)

```yaml
{form_digest}
```

## sfrmOrderLineItems (subform of frmOrderDetails, linked on OrderID)

```yaml
{subform_digest}
```

# Your task

1. **Coverage check.** For each notable structural piece of the form (the
   form binding, each meaningful control category, the subform link, the
   calculated expressions, the VBA event attachments, the row sources), say
   which row of the migration map covers it. If multiple rows partially
   cover it, name them and explain the split. If no row covers it, say so
   explicitly.

2. **Gap inventory.** Produce a numbered list of concrete gaps the form
   exposes in the migration map. A "gap" is one of:
   - A migration-map row whose guidance does NOT cover a real construct in
     this form (the row is silent on the case).
   - A migration-map row that names a target component or pattern that does
     not match how the construct is actually used here.
   - A construct in this form that has NO corresponding row.

   For each gap, identify (a) the form element that triggers it, (b) which
   row(s) of the map are involved (or that no row applies), and (c) a
   one-sentence proposal for what the map should say. Be specific: cite
   control names, expressions, and migration-map row titles verbatim.

3. **Particular things to look at.** These are the structural anchors most
   likely to expose gaps in a real-world Access form. Examine each:
   - Cross-form expressions in ControlSource (e.g.,
     `=IIf([TaxStatusID]=1,[TaxRate]*[sfrmOrderLineItems].[Form].[txtSubTotal],0)`
     and `=[sfrmOrderLineItems].[Form].[txtSubTotal]+[txtTaxAmount]+Nz([ShippingFee])`).
   - UDF references in ControlSource (e.g.,
     `=RecordModifiedString([AddedBy],[AddedOn],[ModifiedBy],[ModifiedOn])`).
   - ComboBox with `RowSourceType: "Value List"` vs.
     `RowSourceType: "Table/Query"` — the Form row treats all RowSources
     uniformly but the migration target differs.
   - The eight CommandButtons (`cmdAddOrder` through `cmdCloseOrder`) — does
     the map distinguish "form-level navigation/CRUD" buttons from "domain
     workflow" buttons (CreateInvoice / ShipOrder / ReceivePayment /
     CloseOrder)?
   - StatusBarText with provenance notes (e.g., "Copied from
     SystemSettings.TaxRate", "Copied from Companies.DefaultTaxStatusID")
     — is this migrated as inline tooltip, audit metadata, or dropped?
   - The subform's own OnEnter/OnExit event attachments (separate from the
     subform's internal form-level events).
   - `EventProcPrefix` on every control even when no events are attached —
     is this a real migration concern, or noise?

4. **Non-gaps.** If you find places where the map is unexpectedly precise
   (e.g., the Subreport row's "no open gaps" claim, the security-wrapper
   cross-cutting note), confirm them briefly. The goal is to leave the map
   stronger, not just to flag problems.

# Output format

Write a Markdown document with this exact structure:

```
# Smoke Test: frmOrderDetails Through the Migration Map

> Date: 2026-05-17 | Form: frmOrderDetails (+ sfrmOrderLineItems) from samples/NorthwindDevEdition.accdb | Map under test: docs/cross-repo/access-to-platform-migration-map.md

## 1. Coverage check
... (table or bullets, one row per notable structural piece)

## 2. Gap inventory
1. **Gap title.** Form element: ... Map rows involved: ... Proposed map text: ...
2. ...

## 3. Confirmed-precise rows
... (brief)

## 4. Summary
- N gaps surfaced; M severity-Major (would block a real migration); K severity-Minor (would slow it down).
- The map's biggest blindspot is: ...
- The map's strongest section is: ...
```

Be concrete. The acceptance criterion is that a migration engineer reading
this document plus the map can decide whether to start translating
frmOrderDetails today, or whether the map needs amendments first.
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=55,
        session_set=str(SESSION_SET),
        session_number=3,
    )

    # Persist BEFORE any display side effects (cp1252 print-crash defense).
    OUTPUT.write_text(result.content, encoding="utf-8")

    log.log_step(
        session_number=3,
        step_number=1,
        step_key="session-3/smoke-test",
        description=f"Route smoke-test analysis of frmOrderDetails against the migration map ({OUTPUT.name})",
        status="complete",
        api_calls=[
            {
                "model": result.model_name,
                "taskType": "analysis",
                "inputTokens": result.input_tokens,
                "outputTokens": result.output_tokens,
                "costUsd": result.cost_usd,
            }
        ],
    )

    print(f"WROTE {OUTPUT}  (model={result.model_name}, "
          f"in={result.input_tokens}, out={result.output_tokens}, "
          f"cost=${result.cost_usd:.4f})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
