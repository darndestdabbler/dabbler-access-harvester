"""Step 4: Route test-generation for 5 uncovered hazards."""
import importlib.util, sys, os, json
from pathlib import Path

def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router", "ai-router/__init__.py",
        submodule_search_locations=["ai-router"])
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod

ar = load_ai_router()
route = ar.route
from ai_router.session_log import SessionLog

SESSION_SET = "docs/session-sets/vba-grammar-validation"
log = SessionLog(SESSION_SET)

Path("docs/test-cases/edge").mkdir(parents=True, exist_ok=True)

SCHEMA_EXCERPT = """
The expected.json must conform to harvester-schema.v0.json which requires:
{
  "schemaVersion": "0.1",
  "source": {
    "kind": "procedure",  // or "expression"
    "location": { "file": "<filename>", "module": "<ModuleName>", "startLine": N, "startColumn": 1, "endLine": N, "endColumn": N },
    "hostContext": { "hostKind": "standard" },  // standard for .bas modules without form context
    "originalSource": "<verbatim source>"
  },
  "items": [
    {
      "itemKind": "procedure",
      "name": "<ProcName>",
      "procedureKind": "Sub",  // or "Function", "Property"
      "visibility": "Public",  // or "Private"
      "isStatic": false,
      "signature": {
        "parameters": [...]  // list of {name, type, byRef, isOptional, paramKind}
      },
      "classification": {
        "purposeTags": [...],
        "purity": "pure",
        "complexity": N,
        "linesOfCode": N,
        "translatability": "mechanical"
      },
      "body": {
        "statements": [...],
        "references": [],
        "calls": [],
        "sqlStrings": []
      }
    }
  ],
  "unresolvedItems": [],
  "diagnostics": []
}
Minimize the body.statements to only the key AST nodes that demonstrate the hazard is parsed correctly.
For grammar-validation fixtures, resolution-level detail (resolvedTarget, confidence) is not required in items -- focus on the structural parse shape.
"""

HAZARDS = [
    {
        "id": "h-d05",
        "slug": "resume-forms",
        "num": "01",
        "title": "H-D05: Resume / Resume Next / Resume <label>",
        "description": """
H-D05 tests all four Resume forms per MS-VBAL §5.4.12:
1. `Resume` (retry the failing statement)
2. `Resume 0` (equivalent to Resume -- the 0 is a sentinel, not a label or literal)
3. `Resume Next` (continue at statement after the failing statement)
4. `Resume <label>` (jump to a named label, also clears Err object)

None of these forms appear in the 12 corpus fixtures (fixture 05's error handler exits via Exit Function; fixture 07 uses On Error Resume Next which is a different construct).
The fixture must contain a Sub with an error handler that exercises all four Resume forms.
The tricky disambiguation: Resume 0 looks like a GoTo to numeric label 0, but it is NOT -- it is the same as bare Resume.
After Resume <label>, control jumps to the label AND the Err object is cleared.
""",
        "filename": "01-h-d05-resume-forms.bas",
        "module": "Module_ResumeHazard",
    },
    {
        "id": "h-d12",
        "slug": "named-arguments",
        "num": "02",
        "title": "H-D12: Named arguments at call sites (:=)",
        "description": """
H-D12 tests named arguments at procedure call sites per MS-VBAL §5.5.
Key parsing requirements:
1. `:=` must be tokenized as a single COLON_EQ token, NOT as colon + equals.
2. Named arguments can mix with positional (positional first, then named).
3. Named arguments allow skipping Optional parameters in the middle without `, ,` empty-positional syntax.

The fixture must contain:
- A procedure definition with Optional parameters and a ParamArray (H-E07 overlap fine).
- Call sites using named argument syntax: `Sub(Param1:=value1, Param2:=value2)`
- A mixed call site: `Sub(positionalArg, Param2:=value2)`
- DoCmd.OpenForm with named arguments: `DoCmd.OpenForm FormName:="frmX", DataMode:=acFormAdd`

No corpus witnesses -- all 12 fixtures use positional arguments.
""",
        "filename": "02-h-d12-named-arguments.bas",
        "module": "Module_NamedArgHazard",
    },
    {
        "id": "h-e07",
        "slug": "param-array",
        "num": "03",
        "title": "H-E07: ParamArray parameter",
        "description": """
H-E07 tests ParamArray per MS-VBAL §4.6.
Key parsing requirements:
1. `ParamArray args() As Variant` -- must be last parameter, As Variant, no Optional/ByRef/ByVal.
2. The parser must reject: `ParamArray args() As String` (non-Variant type).
3. At call sites, the caller can pass a variable number of arguments beyond the fixed params.
4. ParamArray and Optional are mutually exclusive.

The fixture must contain:
- A Public Sub with mixed fixed + ParamArray: `Public Sub Log(prefix As String, ParamArray args() As Variant)`
- A Function with only ParamArray: `Public Function Join(separator As String, ParamArray parts() As Variant) As String`
- Call sites that pass 0, 1, and N arguments to the ParamArray parameter.

No corpus witnesses.
""",
        "filename": "03-h-e07-param-array.bas",
        "module": "Module_ParamArrayHazard",
    },
    {
        "id": "h-a07-rem",
        "slug": "rem-comments",
        "num": "04",
        "title": "H-A07 (REM sub-case): REM-form comments",
        "description": """
H-A07 covers apostrophe comments, which ARE in the corpus. However, the hazard description explicitly
calls out: 'REM-form comments: not present in current corpus (synthetic fixture coverage required for
vba-grammar-validation #5).'

REM comment rules per MS-VBAL §3.3.1:
1. `REM <comment text>` on its own line is a comment statement.
2. `REM` is a statement-level keyword -- it may appear wherever a statement can appear.
3. Unlike apostrophe comments, REM cannot appear after a statement on the same line separated by `:`.
   (Actually: `stmt : REM comment` IS valid when REM is a statement at the separator position)
4. Inside a string literal, `REM` is not a comment trigger.

The fixture must contain:
- Module header with REM comments (instead of apostrophe header).
- A Sub with REM comments at statement position.
- A colon-separated line with REM after the separator: `x = 1 : REM this is a comment`
- A string literal containing the word REM to confirm it is NOT treated as comment inside strings.
""",
        "filename": "04-h-a07-rem-comments.bas",
        "module": "Module_RemCommentHazard",
    },
    {
        "id": "h-d04-zero",
        "slug": "on-error-goto-zero",
        "num": "05",
        "title": "H-D04 (On Error GoTo 0 sub-case): Deactivate error handler",
        "description": """
H-D04 covers On Error in three forms. 'On Error GoTo <label>' and 'On Error Resume Next' appear in
the corpus (fixtures 05, 07, 11, 12). However, the hazard description explicitly states:
'On Error GoTo 0 (deactivate): not present in current corpus -- synthetic fixture required in
vba-grammar-validation (#5).'

The parsing hazard: the literal 0 in 'On Error GoTo 0' is a SENTINEL meaning 'deactivate handler',
NOT a reference to label 0 (H-A03 line numbers) and NOT the integer literal zero.
This is a positional disambiguation: after 'On Error GoTo' the 0 is a deactivate sentinel; elsewhere
numeric 0 is a literal.

The fixture must contain a Sub that:
1. Activates a handler: `On Error GoTo ErrHandler`
2. Has some error-prone code
3. Deactivates the handler: `On Error GoTo 0`
4. Continues with code that should run outside error-handling scope
5. Has the label: `ErrHandler:` with a Resume statement

Also include `On Error Resume Next` in the same Sub to confirm all three variants coexist.
""",
        "filename": "05-h-d04-on-error-goto-zero.bas",
        "module": "Module_OnErrorHazard",
    },
]

total_cost = 0.0
api_calls_all = []

for i, hazard in enumerate(HAZARDS):
    step_num = i + 2  # steps 2-6 (step 1 was ai-assignment)
    step_key = f"session-1/fixture-{hazard['id']}"

    prompt = f"""
# Test-Generation Request: {hazard['title']}

## Hazard Description

{hazard['description']}

## Output Required

Produce TWO files:

### File 1: {hazard['filename']} (VBA source)

A minimal VBA module fixture that exercises EXACTLY this hazard. Requirements:
- Module header comment block (use REM comments for the REM hazard, apostrophe for others)
- Module-level `Option Explicit`
- One or two Sub/Function procedures demonstrating the hazard constructs
- Minimal: no extra complexity beyond what the hazard requires
- Self-contained: does not require Access form/report context
- Correct VBA syntax that the ANTLR4 grammar should parse without errors

### File 2: {hazard['filename'].replace('.bas', '.expected.json').replace('.cls', '.expected.json')} (expected harvester output)

Minimal valid JSON conforming to harvester-schema.v0.json. Required top-level fields: schemaVersion, source, items.

Schema shape guide:
{SCHEMA_EXCERPT}

For the items[] array, include one entry per procedure in the fixture. For each procedure, the body.statements array should contain only the key statement nodes that demonstrate the hazard construct is parsed correctly. Use this minimal statement node shape:
{{
  "kind": "<statementKind>",   // if, assignment, call, label, errorHandler, resumeStmt, etc.
  "location": {{ "startLine": N, "startColumn": N, "endLine": N, "endColumn": N }},
  "summary": "<one-line description of what this statement does re: the hazard>"
}}

For named argument calls (H-D12), include the call arguments showing named vs positional kind:
{{
  "kind": "call",
  "callee": "<name>",
  "arguments": [
    {{ "kind": "positional", "expression": "..." }},
    {{ "kind": "named", "parameterName": "X", "expression": "..." }}
  ]
}}

For ParamArray procedures (H-E07), include the parameter list with paramKind: "paramArray".

Module: {hazard['module']}
Filename: {hazard['filename']}

## Output Format

Return EXACTLY this structure (two fenced code blocks):

```vba
<FULL VBA SOURCE for {hazard['filename']}>
```

```json
<FULL JSON for {hazard['filename'].replace('.bas', '.expected.json').replace('.cls', '.expected.json')}>
```
"""

    print(f"\nRouting test-generation for {hazard['title']}...")
    result = route(
        content=prompt,
        task_type="test-generation",
        complexity_hint=40,
        session_set=SESSION_SET,
        session_number=1,
    )

    # Persist output before any print or parse
    raw_path = f"docs/session-sets/vba-grammar-validation/session-reviews/fixture-gen-{hazard['num']}-raw.txt"
    Path("docs/session-sets/vba-grammar-validation/session-reviews").mkdir(parents=True, exist_ok=True)
    with open(raw_path, "w", encoding="utf-8") as f:
        f.write(result.content)

    print(f"  Model: {result.model_name}, Cost: ${result.cost_usd:.4f}")
    total_cost += result.cost_usd

    # Parse out the two code blocks
    content = result.content
    vba_start = content.find("```vba")
    vba_end = content.find("```", vba_start + 3)
    json_start = content.find("```json", vba_end)
    json_end = content.find("```", json_start + 3)

    vba_source = ""
    json_content = ""
    if vba_start >= 0 and vba_end > vba_start:
        vba_source = content[vba_start + 6:vba_end].strip()
    if json_start >= 0 and json_end > json_start:
        json_content = content[json_start + 7:json_end].strip()

    if not vba_source or not json_content:
        print(f"  WARNING: Could not extract code blocks for {hazard['id']}. Check raw output at {raw_path}")
        log.log_step(1, step_num, step_key, f"PARTIAL: fixture-{hazard['id']} -- extraction failed, see raw output", "partial", [{
            "model": result.model_name, "taskType": "test-generation",
            "inputTokens": result.input_tokens, "outputTokens": result.output_tokens, "costUsd": result.cost_usd,
        }])
        continue

    # Write fixture files
    bas_path = f"docs/test-cases/edge/{hazard['filename']}"
    json_path = bas_path.replace(".bas", ".expected.json").replace(".cls", ".expected.json")

    with open(bas_path, "w", encoding="utf-8") as f:
        f.write(vba_source)
    with open(json_path, "w", encoding="utf-8") as f:
        f.write(json_content)

    print(f"  Written: {bas_path}")
    print(f"  Written: {json_path}")

    api_calls_all.append({
        "model": result.model_name,
        "taskType": "test-generation",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    })

    log.log_step(1, step_num, step_key, f"Routed test-generation for {hazard['id']}: authored {bas_path} + {json_path}", "complete", [{
        "model": result.model_name, "taskType": "test-generation",
        "inputTokens": result.input_tokens, "outputTokens": result.output_tokens, "costUsd": result.cost_usd,
    }])

print(f"\nTotal fixture authoring cost: ${total_cost:.4f}")
print("All 5 hazard fixtures authored.")
