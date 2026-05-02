"""Session 4 verification runner."""
import pathlib
from ai_router import route
from ai_router.session_log import find_active_session_set, SessionLog

SESSION_SET = find_active_session_set('docs/session-sets')
log = SessionLog(SESSION_SET)

schema_content = pathlib.Path('docs/test-cases/harvester-schema.v0.json').read_text(encoding='utf-8')
fixture13 = pathlib.Path('docs/test-cases/13-preprocessor.expected.json').read_text(encoding='utf-8')
fixture13_bas = pathlib.Path('docs/test-cases/13-preprocessor.bas').read_text(encoding='utf-8')

verification_prompt = r"""# Session Verification: parser-infrastructure Session 4 of 4

## Session scope

This is the final session of the parser-infrastructure set. Prior sessions 1-3 landed:
- Error recovery strategy + parser pipeline (AccessHarvester.Vba.Parser/)
- AST node hierarchy + visitor infrastructure (AccessHarvester.Vba.Parser/Ast/)
- SourceLocation/SourceRange types, PreprocessorStatement AST node, preprocessor fixture pair

Session 4 work (all this session):
1. Ran full test suite: 676 passed (54 parser, 294 grammar, 328 core), 0 failed, 2 skipped.
2. Ran schema validation: fixtures 01-12 passed, fixture 13 FAILED.
3. Fixed fixture 13 and schema. All 13 now PASS.
4. Re-ran tests: still 676/0/2.

## Schema change (harvester-schema.v0.json)

Added "preprocessorStatement" to the statement.kind enum.
Before: ["assignment","set","call","if","select","for","forEach","doLoop","while",
         "with","exit","return","errorHandler","raiseError","declaration","comment",
         "directive","labelled","gosub","goto","stop","end","raw"]
After: same list with "preprocessorStatement" inserted before "labelled".

## 13-preprocessor.bas (source fixture, produced Session 3, unchanged this session)

```
Option Compare Database
Option Explicit

' Module-level preprocessor constant
#Const DEBUG_MODE = 1

' Module-level conditional compilation for API declares
#If VBA7 Then
    Private Declare PtrSafe Function GetTickCount64 Lib "kernel32" () As LongLong
#Else
    Private Declare Function GetTickCount Lib "kernel32" () As Long
#End If

Public Sub LogProcess(ByVal processName As String)
    Dim startTime As Long
    startTime = GetTickCount()

    #If DEBUG_MODE = 1 Then
        Debug.Print "DEBUG: Starting process '" & processName & "' at " & startTime
    #Else
        DoCmd.RunSQL "INSERT INTO tbl_Log (ProcessName, StartTime) VALUES ('" & processName & "', " & startTime & ")"
    #End If

    Debug.Print "Process logging complete."
End Sub
```

## 13-preprocessor.expected.json (fixed this session)

Changes made to fix schema violations:
- statement kind "variableDeclaration" -> "declaration"
- statement kind "let" -> "assignment"
- classification.purposeTags ["logging","debugging"] -> ["sideEffect","utility"]
- classification.purity "impure" -> "mixed"
- resolvedCallee.resolutionStatus "conditional" -> "unresolved"
- resolvedCallee.referenceKind "externalDeclare" -> "external"
- Added missing required resolvedCallee.location field
- Fixed sqlStrings: added required originalText and constructionMode fields

Current fixture content:
```json
""" + fixture13 + r"""
```

## Files not in scope this session (present in repo)

- AccessHarvester.Vba.Parser/Ast/Statement.cs -- PreprocessorStatement record added Session 3
- AccessHarvester.Vba.Parser/Ast/SourceLocation.cs -- added Session 3
- AccessHarvester.Vba.Parser/Ast/SourceRange.cs -- added Session 3
- AccessHarvester.Vba.Parser/Ast/Provenance.cs -- updated Session 3 (Range added)
- All other parser/AST/visitor/walker files -- Sessions 1-2, unchanged this session

## Verification questions

1. Schema: Is "preprocessorStatement" the right enum value to add? The schema already has "directive" -- is a distinct value appropriate, or should preprocessor lines use "directive" with kind-specific detail in the "details" field?

2. Fixture statement kinds: Are "declaration", "assignment", and "preprocessorStatement" correct for the three VBA constructs (Dim statement, assignment, preprocessor line)?

3. PreprocessorStatement shape: The fixture uses "kind": "preprocessorStatement" with "details": {"preprocessorKind": "If|Else|EndIf", "body": "..."} where body is the raw text after the directive keyword. Is this shape appropriate given the schema's open-ended "details" field?

4. resolutionStatus: Changed "conditional" (not in enum) to "unresolved". GetTickCount is declared conditionally under #If VBA7 -- is "unresolved" more accurate than "partiallyResolved" or "ambiguous"?

5. Provenance refactor deferred: Session 3 left a comment that Session 4 MAY promote SourceRange to primary and remove redundant Line/Column scalars. This was NOT done (lean-by-default; would require touching all Provenance call sites with no correctness benefit at this tier). Is this deferral acceptable?

Conclude with exactly one of: VERIFIED / ISSUES_FOUND
If ISSUES_FOUND, list each as: [Severity: Critical|Major|Minor|Nit] Description
"""

# Save prompt to disk
output_path = pathlib.Path('docs/session-sets/parser-infrastructure/session-reviews/verification-prompt-s4.txt')
output_path.parent.mkdir(exist_ok=True)
output_path.write_text(verification_prompt, encoding='utf-8')
print(f'Prompt written ({len(verification_prompt)} chars). Routing...')

result = route(
    content=verification_prompt,
    task_type='session-verification',
    complexity_hint=70,
    session_set=str(SESSION_SET),
    session_number=4,
)

# Write output to disk before any print
review_path = pathlib.Path('docs/session-sets/parser-infrastructure/session-reviews/session-004.md')
review_path.write_text(result.content, encoding='utf-8')

print(f'Model: {result.model_name}')
print(f'Cost: ${result.cost_usd:.4f}')
print(f'Input tokens: {result.input_tokens}, Output: {result.output_tokens}')
print()
# Print safely
try:
    print('--- VERIFIER OUTPUT ---')
    print(result.content)
except UnicodeEncodeError:
    print('[Output saved to disk - UnicodeEncodeError on print]')

log.log_step(4, 3, 'session-4/cross-provider-verification',
    f'Routed session-verification round 1 (model={result.model_name}, cost=${result.cost_usd:.4f})',
    'complete', [{
        'model': result.model_name,
        'taskType': 'session-verification',
        'inputTokens': result.input_tokens,
        'outputTokens': result.output_tokens,
        'costUsd': result.cost_usd
    }])
print('Step logged.')
