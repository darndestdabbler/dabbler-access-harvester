#!/usr/bin/env python3
"""Route the mandatory architecture synthesis call for the Session 1 redo
of vba-symbol-resolution-and-enrichment.

The first Session 1 attempt skipped this routed call (workflow rule violation
#5; spec Session 1 step 2 explicitly required it). This script issues the
call now and saves the architecture design to disk for human review before
implementation begins.
"""
import sys
from pathlib import Path

from ai_router import route


def main():
    # Read the IR JSON for the form (UTF-16 source) so the architecture
    # designer has a real example of the structured input that's already
    # been mechanically processed once. Read raw modStrings.txt directly
    # since it's plain ASCII.
    modstrings_raw = Path(
        "tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/raw/"
        "ModulesAndClasses/modStrings.txt"
    ).read_text(encoding="utf-8")

    # For the form we'll pull a small targeted excerpt from the IR JSON
    # (the form's raw .txt is UTF-16; the IR pipeline has already decoded
    # it once into VbaCode-as-string).
    import json
    form_ir = json.loads(Path(
        "tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/"
        "Forms/frmCompanyDetail.ir.json"
    ).read_text(encoding="utf-8"))

    # Pull just the VbaCode for the architect. It's large; trim to
    # declarations + the probe target + CompanyIsActive.
    vba_code = form_ir.get("VbaCode", "")
    # Find the probe target and a window around it
    target = "cboCompanyTypeID_BeforeUpdate"
    idx = vba_code.find(target)
    if idx < 0:
        raise RuntimeError(f"Target {target} not found in form IR VbaCode")
    # Take from start of file (header + module-level Private declarations)
    # and the procedure body itself + CompanyIsActive
    header_end = vba_code.find("Private Sub ")
    header = vba_code[:header_end] if header_end > 0 else vba_code[:2000]

    # Find the procedure boundaries
    proc_start = vba_code.rfind("Private Sub ", 0, idx)
    proc_end = vba_code.find("End Sub", idx) + len("End Sub")
    procedure = vba_code[proc_start:proc_end]

    # Find CompanyIsActive
    cia_start = vba_code.find("Private Function CompanyIsActive")
    cia_end = vba_code.find("End Function", cia_start) + len("End Function")
    company_is_active = vba_code[cia_start:cia_end]

    form_excerpt = (
        "## Module-level header (from Form_frmCompanyDetail)\n\n"
        f"```vba\n{header}\n```\n\n"
        "## Probe target procedure\n\n"
        f"```vba\n{procedure}\n```\n\n"
        "## Same-module helper called by probe target\n\n"
        f"```vba\n{company_is_active}\n```\n"
    )

    prompt = f"""You are designing the symbol resolver for a VBA-to-AI-comprehension pipeline.

## Context

The harvester extracts Microsoft Access metadata, including VBA source
code, into a three-tier `raw -> kb -> ir` model. The IR layer captures
event mappings and procedure signatures but does not capture what
identifiers resolve to (cross-module calls, enum values, module-level
state). A downstream AI agent will be asked questions like "what does
this event handler do" — the symbol resolver's job is to give that AI
agent the bindings it needs to reason about behavior.

## What this design must enable

A separate Session 2 will run a "comprehension probe": it builds a
data packet describing one specific procedure
(`Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`) and feeds it
to three different AI models (Gemini Pro, GPT-5.4, Opus). Each model
gets the packet WITHOUT the original VBA source code and is asked to
describe what the procedure does. If they can describe it accurately,
the resolver's data shape is validated as useful for downstream AI
comprehension. If they can't, the entire approach is reconsidered.

The probe target uses these 12 identifiers, all of which the packet
must resolve:

- `m_lngCompanyContactCount`, `m_lngCustomerOrderCount`,
  `m_lngShipperOrderCount`, `m_lngVendorPurchaseOrderCount`,
  `m_lngVendorProductCount`, `m_strMsgCaption`, `m_lngCalledFromID`
  (7 module-level `Private` Long/String variables)
- `strDlgMsg`, `strOpenArgs` (2 local `Dim` variables)
- `CompanyIsActive` (same-module Private Function returning Boolean)
- `GetString` (cross-module Public Function in `modStrings`)
- `sCompanyDelete_CompanyTypeChange` (one value in the
  `Public Enum enumStrings` declared in `modStrings`)

## Narrow scope (deliberate)

This Session 1 is narrowed to the constructs that appear in the probe
target. The previous Session 1 attempt over-claimed broad coverage and
failed verification. This redo handles ONLY:

1. `Public Enum` declarations (with explicit numeric values)
2. `Public`/`Private` Function and Sub declarations with parameters
   including `ByVal`, `ByRef`, `ParamArray`, and qualified parameter
   types (`As enumStrings`)
3. Module-level `Private` declarations (in addition to `Dim`)
4. Local `Dim` declarations (and the scope distinction that lets us
   tell module-level from local)
5. Cross-module function-call resolution
6. Argument-list identifier scanning (so an enum value passed as an
   argument is recognized and resolved)
7. Per-procedure dependency edges (so the call chain trace is accurate)

OUT OF SCOPE for this Session 1 (deferred to a possible Phase 2 if the
probe succeeds): `Private Enum`, `Static`, multi-variable Dim,
qualified return types like `As Scripting.Dictionary`, member-level
resolution of `DoCmd.Close` vs. just `DoCmd`, late-bound `CallByName`,
`Optional` parameter defaults, type libraries.

## Inputs

The harvester writes Access-native VBA to `raw/.../*.txt`. The form's
file is UTF-16 encoded (Access's SaveAsText output for forms); the
module file is plain ASCII. Both must be readable by the resolver.

The IR pipeline has already decoded the form to a JSON with `VbaCode`
as a normal string field; we can either re-decode the raw or read the
IR. The first Session 1 attempt was criticised for reading the IR
instead of the raw — the resolver should support both, but the test
of correctness is reading the raw and producing the same result the IR
produces for `VbaCode`.

## Sample inputs

### `modStrings.txt` (full file, ~220 lines, ASCII)

```vba
{modstrings_raw}
```

### `frmCompanyDetail.txt` excerpt (from the IR's already-decoded VbaCode)

{form_excerpt}

## Your task

Design the symbol resolver as a small Python package under
`tools/symbol-resolver/`. The design should cover:

1. **Module model.** What does a `SymbolTable` for a single module look
   like (data class shapes, scoping)? How are module-level vs. local
   symbols distinguished?

2. **Extractor design.** What is the parsing strategy — pure regex,
   line-based scanner, mini-grammar? Justify the choice given the
   narrow scope. The previous attempt used regex and failed on several
   constructs; if you stick with regex, explain which patterns and how
   they avoid the prior failures (e.g., `Private Enum`, `ParamArray`,
   `As enumStrings`, `Private m_xxx As Long` at module level).

3. **Resolution algorithm.** Given a target procedure, how does the
   resolver discover every identifier it references (including those
   in argument lists, not just call heads), and bind each to a
   declaration somewhere — same module, cross module, VBA built-in,
   enum value, or unresolved?

4. **Per-procedure dependency edges.** How does the resolver scope
   "calls" to the procedure that issues them, rather than to the whole
   module?

5. **Output schema.** What JSON shape does the resolver emit per
   procedure? It should be sufficient for a downstream AI to describe
   the procedure's behavior without seeing the original source. Show
   a concrete sketch using `cboCompanyTypeID_BeforeUpdate` as the
   example, including how the 12 probe-target identifiers appear in
   the output.

6. **Disclosed gaps.** State clearly which constructs the resolver
   does NOT handle and how those omissions are surfaced in the output
   (e.g., a `coverage_disclaimer` field, an `unresolved` list with
   reason codes).

7. **Validation strategy.** How will the implementation be tested
   against the probe target? What would constitute "this works"? What
   tests/assertions belong in Session 1 vs. deferred to a later
   verification round?

Keep the design lean. We do not need an interface for every class, a
plugin system, or a configurable rule engine. The narrow scope is
deliberate — propose the simplest design that handles the seven
in-scope items above and explicitly disclaims the deferred items.

Return the design as a Markdown document suitable for committing as
`docs/session-sets/vba-symbol-resolution-and-enrichment/architecture.md`.
"""

    Path("tmp").mkdir(exist_ok=True)
    Path("tmp/session1-redo-architecture-prompt.md").write_text(
        prompt, encoding="utf-8"
    )
    print(f"Architecture prompt: {len(prompt)} chars")

    SESSION_SET = "docs/session-sets/vba-symbol-resolution-and-enrichment"
    print("Routing architecture call...")
    result = route(
        content=prompt,
        task_type="architecture",
        complexity_hint=75,
        session_set=SESSION_SET,
        session_number=1,
    )

    out_path = Path(
        "docs/session-sets/vba-symbol-resolution-and-enrichment/architecture.md"
    )
    out_path.write_text(result.content, encoding="utf-8")
    print(f"Wrote {out_path} ({len(result.content)} chars)")
    print(f"Model: {result.model_name}, cost: ${result.cost_usd:.4f}")

    Path("tmp/session1-redo-architecture-response.md").write_text(
        result.content, encoding="utf-8"
    )

    # Don't log_step yet — we want the human to bless the architecture
    # before we declare this step complete and start implementing.


if __name__ == "__main__":
    main()
