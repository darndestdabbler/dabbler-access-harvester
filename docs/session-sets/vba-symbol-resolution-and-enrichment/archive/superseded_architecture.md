# Symbol Resolver — Narrow-Scope Architecture (Session 1 Redo)

**Authored:** 2026-04-28
**Author:** Opus 4.7 (orchestrator), in-conversation
**Status:** Awaiting human blessing before implementation

> **Routing-deviation disclosure.** Workflow Step 4 / Spec Session 1
> Step 2 require routing the architecture synthesis through
> `route(task_type="architecture", complexity_hint=75)`. The routed
> call to Opus at `effort:max` + adaptive thinking on a ~12K-token
> input was started and timed out past the 600s provider boundary —
> the documented failure mode in `project-guidance.md` →
> *Conventions → Workflow Expectations* under "Use Opus as the
> orchestrator (and author in-conversation) for synthesis-document
> work above ~3K input tokens." The convention's recommended fallback
> (a) is "use Opus as the orchestrator and handle the synthesis
> in-conversation per the When-the-orchestrator's-model-matches-the-
> routing-target Convention." This document is that fallback. The
> deviation will be disclosed to the cross-provider verifier in the
> Step 6 prompt.

## Goal

Build a narrow-scope symbol resolver that produces a JSON packet
describing the procedure
`Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` and the 12
identifiers it references, in a shape that downstream AI agents can
read to describe what the procedure does — without having seen the
original VBA source.

The resolver's correctness is validated by Session 2's comprehension
probe: three different AI models (Gemini Pro, GPT-5.4, Opus) get the
packet (no source) and are asked what the procedure does. The human
adjudicates.

## Non-goals (deferred to a possible Phase 2 if probe succeeds)

The previous Session 1 attempt over-claimed broad coverage and failed
verification. This redo deliberately handles a narrow subset:

| Construct | In scope | Notes |
|---|---|---|
| `Public Enum` with explicit numeric values | yes | `enumStrings` is the canonical case |
| `Private Enum` | **no** | not needed for the probe; deferred |
| `Public`/`Private` Function/Sub with `ByVal`/`ByRef` | yes | needed for `GetString` and the probe target |
| `ParamArray params() As Variant` | yes | `GetString` signature |
| Qualified parameter type (`As enumStrings`) | yes | `GetString`'s `ID` parameter |
| Qualified return type (`As Scripting.Dictionary`) | **no** | `StringToDictionary` uses this; not in probe path |
| Module-level `Private` declarations (`Private m_xxx As Long`) | yes | the seven probe-target module variables |
| Module-level `Public` / `Static` declarations | **no** | not in probe path |
| Multi-variable Dim (`Dim a, b As String`) | **no** | doesn't appear in probe target |
| Local `Dim` (`Dim strDlgMsg As String`) | yes | the two local probe variables |
| Cross-module function-call resolution | yes | `GetString` |
| Argument-list identifier scanning | yes | so `sCompanyDelete_CompanyTypeChange` is recognized |
| Per-procedure dependency edges | yes | so the call chain is accurate |
| Member-level resolution (`DoCmd.Close` vs. `DoCmd`) | **no** | knowing it's a `DoCmd` member call is enough for the probe |
| Late-bound (`CallByName`) | **no** | not in probe path |
| `Optional` parameter defaults | **no** | not in probe path |

The output schema MUST surface the deferred categories explicitly so
that downstream AI agents do not read absence as "this construct
doesn't exist."

## Inputs

The resolver reads the harvester's raw `.txt` files. Two encoding
realities:

- **Modules and class modules** (`raw/ModulesAndClasses/*.txt`) are
  plain ASCII / Windows-1252.
- **Forms and reports** (`raw/Forms/*.txt`, `raw/Reports/*.txt`) are
  UTF-16 LE with BOM (this is what Access's `SaveAsText` writes).

The resolver auto-detects encoding from the BOM. Probe confirmed:
`raw/Forms/frmCompanyDetail.txt` starts with `0xff 0xfe` (UTF-16 LE
BOM) followed by `Version =21\r\n`.

The resolver does **not** read the IR JSON. The previous Session 1
attempt was criticised for reading IR instead of raw — and the IR
JSON is itself produced by the harvester from the raw, so reading raw
is the source of truth.

## Module model

```python
@dataclass
class Symbol:
    name: str
    kind: SymbolKind            # ENUM_DECL, ENUM_VALUE, FUNCTION,
                                # SUB, MODULE_VAR, LOCAL_VAR, CONST
    module: str                 # e.g. "Form_frmCompanyDetail"
    declared_in_procedure: str | None  # None for module-level;
                                       # otherwise "cboCompanyTypeID_BeforeUpdate"
    access: str | None          # "Public" | "Private" | None
    data_type: str | None       # "Long", "String", "Boolean",
                                # "enumStrings", "Variant", ...
    line_number: int            # 1-based, in source file
    parent: str | None          # for ENUM_VALUE: the enum name
    numeric_value: int | None   # for ENUM_VALUE


@dataclass
class Parameter:
    name: str
    data_type: str | None       # qualified type as written (e.g., "enumStrings")
    passing: str                # "ByVal" | "ByRef" (default to ByRef per VBA)
    is_paramarray: bool
    is_array: bool


@dataclass
class Procedure:
    name: str
    kind: str                   # "Function" | "Sub"
    module: str
    access: str                 # "Public" | "Private"
    return_type: str | None     # None for Subs
    parameters: list[Parameter]
    line_start: int             # 1-based; line of "Private Sub ..." line
    line_end: int               # 1-based; line of "End Sub" / "End Function"
    body: str                   # source between line_start+1 and line_end-1


@dataclass
class Module:
    name: str
    kind: str                   # "Module" | "ClassModule" | "Form" | "Report"
    source_path: str
    encoding: str               # "ascii" | "utf-16-le"
    symbols: list[Symbol]       # all symbols at module-level scope
    procedures: list[Procedure] # parsed procedures
```

Module-vs-local scope is captured by `declared_in_procedure`: `None`
means module-level; otherwise the procedure name. This solves the
previous attempt's gap of misclassifying local `Dim` declarations as
module symbols.

## Extractor strategy

**Line-based scanner with simple state.** Pure regex over the entire
file mis-handles scoping; a full grammar is overkill for the seven
in-scope items. Approach:

1. Read the file with auto-detected encoding; strip the BOM if
   present; normalize line endings to `\n`.
2. For Form/Report `SaveAsText` files, **strip the form-definition
   prelude** (everything before the first `Attribute VB_Name` or
   `Option Compare` line). The probe-target form file starts with
   ~1950 lines of design-time geometry/property definitions; the VBA
   module starts at the `Attribute` block. (The IR pipeline already
   does an equivalent split via `VbaCode`; we replicate just enough
   of that to run on raw.)
3. Walk lines with a small state machine:
   - `MODULE` (default): module-level declarations, looking for
     `Public Enum`, `Private/Public ... As ...`, `Private/Public Sub`,
     `Private/Public Function`, `Const`.
   - `ENUM(name)`: inside an enum block; collect `name = value` lines
     until `End Enum`.
   - `PROC(name)`: inside a procedure; collect local `Dim` lines and
     the body verbatim until `End Sub` / `End Function`.

State transitions:
- `MODULE → ENUM(n)` on `Public Enum n` (case-insensitive).
- `MODULE → PROC(n)` on `(Public|Private)? Sub n(...)` or
  `(Public|Private)? Function n(...) As T`.
- `ENUM(_) → MODULE` on `End Enum`.
- `PROC(_) → MODULE` on `End Sub` / `End Function`.

Patterns (anchored, case-insensitive, line-based):

```python
ENUM_OPEN   = r"^\s*Public\s+Enum\s+(\w+)\s*$"
ENUM_VALUE  = r"^\s*(\w+)\s*=\s*(\d+)\s*(?:'.*)?$"
ENUM_CLOSE  = r"^\s*End\s+Enum\s*$"

MODULE_VAR  = r"^\s*(Private|Public|Dim)\s+(\w+)(?:\s+|\s*)As\s+([A-Za-z_]\w*)\s*$"
                # at MODULE state only; "Dim" at module-level is rare
                # but allowed by VBA — treat as Private

PROC_OPEN   = r"^\s*(Public|Private)?\s*(Sub|Function)\s+(\w+)\s*\((.*?)\)(?:\s+As\s+(\w+))?\s*$"
PROC_CLOSE  = r"^\s*End\s+(Sub|Function)\s*$"

LOCAL_DIM   = r"^\s*Dim\s+(\w+)(?:\s+|\s*)As\s+(\w+)\s*$"
                # at PROC state only
```

**Parameter parsing.** Split the parameter list on commas (no nested
commas in VBA parameter lists for our scope). For each slice, apply
in order:

```python
PARAM_PATTERN = re.compile(
    r"^\s*"
    r"(?:Optional\s+)?"             # we ignore Optional flag (deferred)
    r"(ByVal|ByRef)?\s*"            # passing
    r"(ParamArray\s+)?"             # paramarray flag
    r"(\w+)"                        # name
    r"(\(\s*\))?"                   # array suffix
    r"\s+As\s+"
    r"([A-Za-z_]\w*)"               # qualified type (just the type name token)
    r"\s*$",
    re.IGNORECASE,
)
```

This handles `ByVal ID As enumStrings`, `ParamArray params() As
Variant`, and `Cancel As Integer`. Default `passing` to `"ByRef"` (VBA
default) when unspecified.

The previous attempt's `FUNCTION_PATTERN` had `As (\w+)` for the
return type, which fails on `As Scripting.Dictionary` — explicitly
deferred (out of probe path), so we keep a single-token return-type
match and emit `unresolved_return_type` in the disclaimer when we
can't parse one.

## Resolution algorithm

For a target procedure `P` in module `M`:

1. **Parameter symbols** are bound to their declared types
   (`parameters` field of the `Procedure`).
2. **Local symbols** come from `LOCAL_DIM` matches inside `P`'s body.
3. **Identifier discovery in the body.** Scan the procedure body for
   tokens matching `[A-Za-z_]\w*` (case-insensitive), excluding VBA
   keywords (a small allowlist: `If`, `Then`, `Else`, `End`, `For`,
   `Next`, `Sub`, `Function`, `Dim`, `Set`, `Let`, `As`, `New`,
   `True`, `False`, `Nothing`, `Or`, `And`, `Not`, `Mod`, `Exit`,
   `On`, `Error`, `GoTo`, `Resume`, `Select`, `Case`, `Do`, `Loop`,
   `While`, `Until`, `Each`, `In`, `To`, `Step`, `Stop`, `Const`,
   `Private`, `Public`, `ByVal`, `ByRef`, `Optional`, `ParamArray`).
4. **Bind each identifier** in order:
   - If it matches a parameter → `kind: parameter`, `data_type: ...`
   - If it matches a local Dim → `kind: local_var`, `data_type: ...`
   - If it matches a module-level symbol in `M` → `kind: module_var |
     module_const | local_function | local_sub | enum_decl`,
     `data_type: ...`
   - If it matches a symbol in any other registered module → `kind:
     cross_module_*`, `referenced_module: <name>`
   - If it matches a known VBA built-in → `kind: vba_builtin` (small
     hand-curated list scoped to what the probe target needs:
     `DoCmd`, `Me`, `MsgBox`, `DLookup`, `DCount`, `Nz`, `Debug`,
     `IsFormOpen`, `RunCommand`, `acForm*`, `acDialog`, `acFirst`,
     etc.)
   - Otherwise → `unresolved`, with `reason` populated
5. **Argument-list scanning** is implicit in step 3 — we scan the
   whole body, not just call heads. `GetString(sCompanyDelete_CompanyTypeChange,
   ...)` discovers both `GetString` and the enum-value identifier.

**Per-procedure dependency edges.** The resolver keeps a `calls`
list per procedure: every identifier whose binding kind is `function`,
`sub`, `cross_module_function`, `cross_module_sub`, or `vba_builtin`.
Same identifier called twice in the body → listed once. The previous
attempt added every reference in the module to every procedure; here
each procedure's `calls` list is built only from its own body scan.

## Output schema

The resolver emits one JSON file per analyzed procedure. Sketch for
the probe target:

```json
{
  "schema_version": "0.1-narrow-scope",
  "module": "Form_frmCompanyDetail",
  "module_kind": "Form",
  "procedure": {
    "name": "cboCompanyTypeID_BeforeUpdate",
    "kind": "Sub",
    "access": "Private",
    "parameters": [
      { "name": "Cancel", "data_type": "Integer", "passing": "ByRef",
        "is_paramarray": false, "is_array": false }
    ],
    "line_start": 1981,
    "line_end": 2019,
    "body_excerpt": "10        On Error GoTo Err_Handler\n\n          Dim strDlgMsg ..."
  },
  "module_state": [
    { "name": "m_lngCompanyContactCount", "access": "Private",
      "data_type": "Long", "line_number": 1956 },
    { "name": "m_lngCustomerOrderCount", "access": "Private",
      "data_type": "Long", "line_number": 1957 },
    { "name": "m_lngShipperOrderCount", "access": "Private",
      "data_type": "Long", "line_number": 1958 },
    { "name": "m_lngVendorPurchaseOrderCount", "access": "Private",
      "data_type": "Long", "line_number": 1959 },
    { "name": "m_lngVendorProductCount", "access": "Private",
      "data_type": "Long", "line_number": 1960 },
    { "name": "m_lngCalledFromID", "access": "Private",
      "data_type": "Long", "line_number": 1964 },
    { "name": "m_strMsgCaption", "access": "Private",
      "data_type": "String", "line_number": 1966 }
  ],
  "locals": [
    { "name": "strDlgMsg", "data_type": "String", "line_number": 1984 },
    { "name": "strOpenArgs", "data_type": "String", "line_number": 1985 }
  ],
  "references": [
    { "identifier": "CompanyIsActive",
      "kind": "local_function",
      "binding": {
        "module": "Form_frmCompanyDetail",
        "procedure": "CompanyIsActive",
        "return_type": "Boolean",
        "body_excerpt": "..."
      }
    },
    { "identifier": "GetString",
      "kind": "cross_module_function",
      "binding": {
        "module": "modStrings",
        "procedure": "GetString",
        "return_type": "String",
        "parameters": [
          { "name": "ID", "data_type": "enumStrings", "passing": "ByVal" },
          { "name": "params", "data_type": "Variant",
            "passing": "ByRef", "is_paramarray": true, "is_array": true }
        ],
        "body_excerpt": "..."
      }
    },
    { "identifier": "sCompanyDelete_CompanyTypeChange",
      "kind": "enum_value",
      "binding": {
        "module": "modStrings",
        "enum": "enumStrings",
        "numeric_value": 37,
        "string_text_at_runtime": "You can't {0} if the Company has<br />     Customer Orders ({1}) <br />     Shipper Orders ({2}) <br />     Vendor Purchase Orders ({3}) <br />     {4}  <br />",
        "string_text_source": "Strings table, StringID=37 (extracted from samples/NorthwindDevEdition.accdb at packet build time)"
      }
    },
    { "identifier": "DoCmd", "kind": "vba_builtin" },
    { "identifier": "Me", "kind": "vba_builtin" },
    { "identifier": "IsFormOpen", "kind": "unresolved",
      "reason": "Identifier not found in any registered module; not a known built-in. May be a same-application helper not in registered modules." }
  ],
  "calls": [
    "CompanyIsActive", "GetString", "StringFormat", "DoCmd",
    "IsFormOpen", "Me"
  ],
  "coverage_disclaimer": {
    "in_scope_constructs_handled": [
      "Public Enum with explicit numeric values",
      "Public/Private Function and Sub with ByVal/ByRef/ParamArray",
      "Qualified parameter types (single-token)",
      "Module-level Private declarations",
      "Local Dim declarations",
      "Cross-module function-call resolution",
      "Argument-list identifier scanning",
      "Per-procedure dependency edges"
    ],
    "deferred_constructs_NOT_handled": [
      "Private Enum",
      "Public/Static module-level declarations",
      "Multi-variable Dim",
      "Qualified return types (e.g. As Scripting.Dictionary)",
      "Member-level resolution (DoCmd.Close vs. DoCmd)",
      "Late-bound (CallByName, Variant dispatch)",
      "Optional parameter defaults"
    ],
    "what_unresolved_means": "An identifier marked 'unresolved' may be a real symbol the resolver did not register, or a typo, or a member-access where only the receiver is registered. Treat as evidence to investigate, not as proof that the symbol does not exist.",
    "string_text_at_runtime_provenance": "For enum values used as keys to the Strings table, string_text_at_runtime is fetched from samples/NorthwindDevEdition.accdb via Access COM at packet build time, not by the resolver itself."
  }
}
```

## Strings-table integration

The probe target's behavior is determined in part by the *runtime
text* `GetString` returns. The resolver itself does not query Access
DBs; instead, the Session 2 packet builder runs a one-shot Access COM
query against `samples/NorthwindDevEdition.accdb` to pull `StringID =
37`'s `StringData` and injects it into the `enum_value` binding.

This split keeps the resolver hermetic (no COM dependency, runs
anywhere) and confines Windows/Access dependencies to the packet
builder.

## Validation strategy

Three layers:

1. **Pure unit assertions** (Python, no Access dependency):
   - `Module` for `modStrings.txt` parses 1 enum (49 values), 4
     functions, 0 module variables, 1 module constant
     (`SINGLE_QUOTE`/`TWO_SINGLE_QUOTES` are inside `StringFormatSQL`
     — confirm whether they're module-level or local during
     implementation).
   - `Module` for `Form_frmCompanyDetail` parses 7 module-level
     `Private m_xxx` variables, the `cboCompanyTypeID_BeforeUpdate`
     procedure with parameter `Cancel As Integer`, and the
     `CompanyIsActive` Function returning Boolean.
   - The procedure's `references` list contains, at minimum:
     `CompanyIsActive` (local_function), `GetString`
     (cross_module_function), `sCompanyDelete_CompanyTypeChange`
     (enum_value), `m_lngVendorProductCount` (module_var),
     `strDlgMsg` (local_var), `strOpenArgs` (local_var), `DoCmd`
     (vba_builtin), `Me` (vba_builtin).
   - Every procedure's `calls` list is derived only from its own
     body, not the module's references.
2. **End-to-end sanity** (Python + a Strings.json fixture; no live
   Access required for this gate): packet for the probe target
   produces output matching the schema sketch above.
3. **Build + test gate** (Session 1's mandatory `dotnet build &&
   dotnet test`). The C# solution does not depend on this Python
   tooling, so the gate confirms we did not break the existing repo.
   The resolver's Python tests run separately via `pytest`.

What "this works" means for Session 1: the assertions in (1) all
pass, the schema in (2) matches the sketch above, and the C# build
+ test still pass. The deeper question — "is this packet shape
useful to a downstream AI?" — is what Session 2's comprehension probe
answers.

## File layout

```
tools/symbol-resolver/
├── __init__.py
├── extractor.py        # line-based scanner, state machine, parsers
├── resolver.py         # binding, per-procedure dependency edges
├── packet.py           # builds the per-procedure JSON output
└── tests/
    ├── test_extractor.py
    ├── test_resolver.py
    └── test_packet.py
```

The previous attempt's three files (`symbol_extractor.py`,
`reference_resolver.py`, `runner.py`) are kept in place but
superseded; the redo uses the new `extractor.py` / `resolver.py` /
`packet.py` so the false-start code stays as audit trail without
being on the import path.

## What this design deliberately does NOT include

- A configurable rule engine for symbol classification.
- An interface or plugin system for alternative parsers.
- A general VBA grammar; the line-based scanner is enough for the
  narrow scope.
- Performance instrumentation; the probe target runs in milliseconds.
- A CLI; `python -m tools.symbol-resolver.packet <module-files>` is
  enough.

## Open questions for the human

1. The `body_excerpt` in the `cross_module_function` binding includes
   the body of `GetString`. Per the user's confirmation in
   conversation ("function body" answer to question 1), this is
   intended. Confirm at review time.
2. For the `enum_value` binding, `string_text_at_runtime` requires a
   one-shot Access COM query at packet-build time. Confirmed by the
   user ("string value" answer to question 2). Already pulled:
   `StringID = 37` → `"You can't {0} if the Company has<br />..."`.
3. Should the packet also include the body of `CompanyIsActive` (a
   *same-module* function)? My read is yes for symmetry with the
   cross-module case, but the user's enumeration only specified
   "function body" generically. Defaulting to yes; flag if wrong.
