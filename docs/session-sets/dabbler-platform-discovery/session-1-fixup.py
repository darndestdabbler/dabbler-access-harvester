"""Round-2 fixup driver: regenerate inventory and schema doc to address
verifier Issues 1, 2, 3, 4 from round 1.

Strategy:
  - Walk all *.razor and *.razor.cs files in the targeted libs and
    extract just the [Parameter] / [EditorRequired] / class signature
    / public method signatures. This is enough grounding for the model
    to author per-component entries WITHOUT including full file
    contents (which would blow the prompt budget for ~50 files).
  - Send the targeted grounding bundle plus the verifier's findings
    list as a redo prompt.
  - For the schema doc, send the full RDF model source again (as in
    round 1) and explicitly require Sorting + Expressions sections,
    reconciliation of resetAggregates / formatString, and synthetic
    examples per the Opus third-opinion verdict (C).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "dabbler-platform-discovery"
OUT_DIR = REPO_ROOT / "docs" / "cross-repo"
PLATFORM_REPO = REPO_ROOT.parent / "dabbler-platform"

PLATFORM_SHA = "aa77020e1ca0f5d0608670539153d0a22530bd25"
PLATFORM_DATE = "2026-05-16"
SNAPSHOT_DATE = "2026-05-17"

CURRENCY_HEADER_PLATFORM = f"""> **Snapshot of:** `../dabbler-platform` @ commit `{PLATFORM_SHA[:12]}` on `{PLATFORM_DATE}`
> **Captured on:** `{SNAPSHOT_DATE}`
> **Regenerate when:** the platform's component library changes
> meaningfully, or when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.
"""

# Libraries whose components are inventory targets.
INVENTORY_LIBS = [
    "src/Libs/Dabbler.Components",
    "src/Libs/Dabbler.Components.Compat",
    "src/Libs/Dabbler.Reports.Blazor",
    "src/Libs/Dabbler.Security.Blazor",
]

# Stubs/testing libs are catalogued at low fidelity (just listed by file)
# and tagged as not-a-migration-target.
STUB_LIBS = [
    "src/Libs/Dabbler.Platform.Stubs.Components",
    "src/Libs/Dabbler.Testing.Blazor",
]


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def _extract_component_signature(razor_path: Path) -> str:
    """Pull the @code { ... } block (or the full file if it's a .razor.cs)
    plus the first non-blank line of markup to give the model enough
    grounding for purpose+parameters without sending the full file.
    """
    text = _read(razor_path)
    # First non-blank markup line — typically the root MudComponent
    head_lines = []
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped:
            continue
        head_lines.append(line)
        if len(head_lines) >= 3:
            break

    # @code block (may be at end of file)
    code_block = ""
    m = re.search(r"@code\s*\{(.*)\n\}\s*$", text, re.DOTALL)
    if m:
        code_block = m.group(0)

    # Any [Parameter] decorations or class members visible in razor
    param_matches = re.findall(
        r"\[(?:Parameter|EditorRequired|CascadingParameter|Inject)[^\]]*\][\s\S]*?(?:\}|;)",
        text,
    )

    parts = []
    parts.append(f"--- head ---\n" + "\n".join(head_lines))
    if param_matches:
        parts.append("--- direct [Parameter]/[Inject] markers ---\n" + "\n\n".join(param_matches))
    if code_block:
        # Truncate very large @code blocks
        if len(code_block) > 4000:
            code_block = code_block[:4000] + "\n... (truncated)"
        parts.append("--- @code block ---\n" + code_block)
    return "\n".join(parts)


def _extract_codebehind(cs_path: Path) -> str:
    text = _read(cs_path)
    if len(text) > 6000:
        text = text[:6000] + "\n... (truncated)"
    return text


def _walk_inventory_grounding() -> str:
    """Build the per-component grounding bundle the model needs to
    inventory each public component.
    """
    blocks = []
    for lib_rel in INVENTORY_LIBS:
        lib_dir = PLATFORM_REPO / lib_rel
        if not lib_dir.is_dir():
            blocks.append(f"### {lib_rel}\n(directory missing)\n")
            continue
        razor_files = sorted(p for p in lib_dir.rglob("*.razor")
                             if not str(p).replace("\\", "/").endswith("_Imports.razor"))
        for rf in razor_files:
            rel = rf.relative_to(PLATFORM_REPO)
            sig = _extract_component_signature(rf)
            blocks.append(f"### {rel}".replace("\\", "/"))
            blocks.append(sig)
            # If there is a companion .razor.cs, include it.
            cs = rf.parent / (rf.stem + ".razor.cs")
            if cs.is_file():
                blocks.append(f"### {cs.relative_to(PLATFORM_REPO)} (code-behind)".replace("\\", "/"))
                blocks.append(_extract_codebehind(cs))
            blocks.append("---")
    return "\n".join(blocks)


def _stub_file_listing() -> str:
    lines = []
    for lib_rel in STUB_LIBS:
        lib_dir = PLATFORM_REPO / lib_rel
        if not lib_dir.is_dir():
            continue
        for p in sorted(lib_dir.rglob("*.razor")):
            if str(p).replace("\\", "/").endswith("_Imports.razor"):
                continue
            lines.append(str(p.relative_to(PLATFORM_REPO)).replace("\\", "/"))
    return "\n".join(lines)


def step_inventory_redo() -> None:
    grounding = _walk_inventory_grounding()
    stub_listing = _stub_file_listing()

    prompt = f"""You are re-authoring `dabbler-platform-components.md` for the
`dabbler-access-harvester` repo. The first pass (also by you) was
flagged by the cross-provider verifier as incomplete and partially
speculative. This pass MUST address those findings.

# Snapshot

  Snapshot date: `{SNAPSHOT_DATE}`
  Platform commit: `{PLATFORM_SHA[:12]}`

# Verifier findings to address

**Major #1** — Feature 1 is incomplete. Many public components,
especially under `Dabbler.Components.Compat` and `Dabbler.Security.Blazor`,
were left as unknown-file listings instead of full inventory entries
with purpose, parameters, usage context, and stability.

**Major #2** — The previous draft included speculative claims like
"DataProvider is likely required" in `FilterPanel<T>` / `DabblerReportPage`
entries. This pass must remove ALL speculation. If a parameter is
declared with `[Parameter] [EditorRequired]` or with a default of
`default!` (non-null required reference), call it Required. Otherwise
call it Optional. If you cannot tell from the grounding bundle, say so
explicitly — do NOT guess.

# Grounding: every targeted component file

Below is the head markup, direct `[Parameter]`/`[Inject]` markers, and
`@code` block for every `.razor` file in the inventory-target libraries
(`Dabbler.Components`, `Dabbler.Components.Compat`, `Dabbler.Reports.Blazor`,
`Dabbler.Security.Blazor`), plus the companion `.razor.cs` code-behind
where one exists. Use this — and only this — to populate per-component
entries.

---GROUNDING BUNDLE START---
{grounding}
---GROUNDING BUNDLE END---

# Stubs and testing libs (cataloged at low fidelity, NOT migration targets)

The following files exist under `Dabbler.Platform.Stubs.Components` and
`Dabbler.Testing.Blazor`. List them in a dedicated "Stubs and Testing"
section as test/dev support — DO NOT inventory them as migration
targets and DO NOT attempt to characterize their parameters.

---STUB LISTING START---
{stub_listing}
---STUB LISTING END---

# Output structure

1. **Document title:** `# dabbler-platform Components Inventory`

2. **Currency header** (verbatim):

{CURRENCY_HEADER_PLATFORM}

3. **Overview** — one paragraph: what the platform's component surface
   covers and what it does NOT cover (intentional gaps to flag for the
   migration repo).

4. **Categories** — one section per functional category. Suggested
   categories (use what fits, merge or split as needed):
   - Master/Detail + Filters (`Dabbler.Components`)
   - Application Shell (`Dabbler.Components.Compat/Shell`)
   - Compat Pages (`Dabbler.Components.Compat/Pages`)
   - Reports UI (`Dabbler.Reports.Blazor`)
   - Security — Authentication Pages (`Dabbler.Security.Blazor/Pages/Auth`)
   - Security — Administration Pages (`Dabbler.Security.Blazor/Pages/Admin`)
   - Security — Access Control Components (`Dabbler.Security.Blazor/Components`)
   - Security — Admin/Role Detail Components (`Dabbler.Security.Blazor/Components/Admin/...`)
   - Stubs and Testing (cataloged, not migration targets)

   In each category, one entry per public component using this shape:

   ```
   #### <ComponentName> — `<full path relative to ../dabbler-platform>`

   - **Purpose:** <one sentence; infer from class name + head markup; if
     you cannot, say "Purpose unclear from grounding.">
   - **Required parameters:** `<name>` (`<type>`) — <one-line desc>
   - **Optional parameters:** `<name>` (`<type>`, default `<value>`)
     — <one-line desc>
   - **Stability:** `stable` | `in-flight` | `unknown` — and a brief
     evidence anchor (TODO comment, stub-namespace, README reference,
     etc.). If you cannot find evidence, write `unknown`.
   - **Common usage context:** <one or two scenarios from Access app
     modernization>
   ```

   If a component file has no visible `[Parameter]` declarations in
   the grounding, write `Required parameters: (none visible in grounding)`
   and `Optional parameters: (none visible in grounding)`. Do NOT invent.

5. **Per-file gaps** — bullet list of files in the grounding where the
   role could not be determined. KEEP THIS SHORT — most files should
   be inventoried now.

# Hard rules

- **Zero speculation.** No "likely required," "probably," "appears to be
  required." If you cannot tell from the grounding bundle, write
  "(none visible in grounding)" or "Purpose unclear from grounding."
- **Cite the file path for every component entry** so a downstream
  agent can follow up.
- **Do not copy more than 2 lines of code** from any single file —
  paraphrase.
- **Stub-namespace components must be tagged as test/dev support**,
  NOT as migration targets.
- Emit raw Markdown only — no surrounding code fence.
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=55,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    out_path = OUT_DIR / "dabbler-platform-components.md"
    out_path.write_text(result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=1,
        step_number=8,
        step_key="session-1/component-inventory-redo",
        description="Redo Feature 1 inventory with full per-component grounding (verifier round 1 Issues 1, 2)",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"Wrote {out_path}  ({result.model_name}, ${result.cost_usd:.4f})")


# ---------------------------------------------------------------------------
# Schema doc redo
# ---------------------------------------------------------------------------

def step_schema_redo() -> None:
    rdf_dir = PLATFORM_REPO / "src/Libs/Dabbler.Reports.Core/Rdf"
    rdf_paths = sorted(rdf_dir.glob("*.cs"))
    file_listing = "\n".join(str(p.relative_to(PLATFORM_REPO)).replace("\\", "/") for p in rdf_paths)

    rdf_excerpts = []
    for p in rdf_paths:
        text = _read(p)
        if len(text) > 8000:
            text = text[:8000] + "\n... (truncated)"
        rdf_excerpts.append(f"=== {p.relative_to(PLATFORM_REPO)} ===\n{text}\n")

    prompt = f"""You are re-authoring `dabbler-platform-report-rdf-schema.md`
for the `dabbler-access-harvester` repo. The first pass was flagged by
the cross-provider verifier as field-incomplete (omitted `sorting` and
`expressions` sections; example-only fields `resetAggregates` and
`formatString` not reconciled with formal property tables) and as
violating the boundary rule by copying sample `.rdf.json` snippets
from the sibling repo.

An independent third-opinion model (Opus) ruled that the right resolution
is **synthetic minimal examples authored in this repo, with a "See also"
citation pointing to the canonical sibling-repo sample path**. NOT
verbatim copying, NOT prose-only.

# Snapshot

  Snapshot date: `{SNAPSHOT_DATE}`
  Platform commit: `{PLATFORM_SHA[:12]}`

# Files to ground against (full text follows)

{file_listing}

---RDF MODEL SOURCE START---
{chr(10).join(rdf_excerpts)}
---RDF MODEL SOURCE END---

# Output structure

1. **Document title:** `# `report.rdf.json` Schema Reference`

2. **Currency header** (verbatim):

{CURRENCY_HEADER_PLATFORM}

3. **Overview** — two paragraphs:
   - What `report.rdf.json` represents and the abstraction level
     (higher-level than a raw syntax tree).
   - Why this matters for Access-to-Blazor migration (the seam between
     harvested Access metadata and platform rendering).

4. **Top-level structure** — Outline the document's root shape from
   `ReportDefinition.cs`. Use a table with columns `JSON property | C#
   property | C# type | Required | Description`. Cite JSON property
   naming (look for `[JsonPropertyName]` attributes or the policy in
   `RdfParser.cs`).

5. **Per-section reference** — one subsection per top-level array or
   object in the root. The reference MUST include:
   - `Metadata` (from `ReportMetadata.cs`)
   - `Parameters` (from `ParameterDefinition.cs`)
   - `Data Sources` (from `DataSourceDefinition.cs`)
   - `Fields` (from `FieldDefinition.cs`)
   - `Bands` (from `BandDefinition.cs` + `BandType.cs`)
   - `Groups` (from `GroupDefinition.cs`)
   - **`Sorting`** (from `SortDefinition.cs`) — REQUIRED, was missing
     in round 1
   - **`Expressions`** (from `ExpressionDefinition.cs`) — REQUIRED, was
     missing in round 1
   - `Elements` (from `ElementDefinition.cs` + `ElementType.cs`)
   - `Styles` (from `StyleDefinition.cs`)
   - `Running totals` (from `RunningTotalMode.cs`,
     `RunningTotalAccumulator.cs`)
   - Validation (from `RdfValidator.cs`, `RdfValidationError.cs`)

   For each subsection:
   - **Property table** — `JSON property | C# property | Type | Required
     | Description`. Cover EVERY public property on the corresponding
     C# class. If a property is nullable / has a default / is computed,
     say so in the Description.
   - **Validation rules** referencing `RdfValidator.cs` where applicable.
   - **Synthetic example** — a minimal JSON snippet authored here,
     NOT copied from `../dabbler-platform`. Each example should be
     5-15 lines max, exercise the documented fields, and end with a
     `> See also: ../dabbler-platform/src/Apps/AcmeCoffeeOnline.Web/Reports/orders-by-customer.rdf.json`
     pointer (or a similarly relevant canonical sample path).

6. **Reconciliation rules**
   - The fields `resetAggregates` (on Groups) and `formatString` (on
     Elements) must EITHER appear in the formal property tables for
     their respective sections OR be omitted from examples. Reconcile
     either way — do not leave them in examples without table entries.
   - Verify each example you author against the property tables; do
     NOT introduce fields that are not in the tables.

7. **Worked example walkthrough** — pick a synthetic "products by
   category" report and walk through its top-level shape mapping to a
   hypothetical Access report structure. 2-3 paragraphs max.

8. **Generation contract for AI consumers** — short bulleted list of
   rules. Each rule MUST cite the source C# file that enforces it.

# Hard rules

- **No verbatim copying of `.rdf.json` content from
  `../dabbler-platform`.** All JSON examples must be synthetic
  ("acme-products-by-category" style), 5-15 lines each, and end with
  a `> See also: ...` citation pointer.
- **Sorting and Expressions sections are mandatory.** They were missing
  in round 1.
- **Every example's fields must appear in the property tables for
  that section.** If a field is in an example but not in the table,
  add it to the table OR remove it from the example.
- **Cite a source file path for every claim** about types, required-ness,
  JSON property names, or validation rules.
- Emit raw Markdown only — no surrounding code fence.
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=60,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    out_path = OUT_DIR / "dabbler-platform-report-rdf-schema.md"
    out_path.write_text(result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=1,
        step_number=9,
        step_key="session-1/rdf-schema-doc-redo",
        description="Redo Feature 2 schema doc with full sorting/expressions + synthetic examples (verifier round 1 Issues 3, 4)",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"Wrote {out_path}  ({result.model_name}, ${result.cost_usd:.4f})")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    step = sys.argv[1]
    if step == "inventory":
        step_inventory_redo()
    elif step == "schema":
        step_schema_redo()
    else:
        print(f"Unknown step: {step}")
        sys.exit(2)
