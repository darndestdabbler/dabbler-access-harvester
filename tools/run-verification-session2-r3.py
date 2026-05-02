"""Session 2 round-3 verification: strDlgMsg test made assertive; guards documented."""
import sys, pathlib
from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = 'docs/session-sets/vba-symbol-db'
log = SessionLog(SESSION_SET)

def read(path):
    try:
        return pathlib.Path(path).read_text(encoding='utf-8')
    except Exception as e:
        return f"<could not read: {e}>"

fqn_resolver = read('AccessHarvester.SymbolDb/Extraction/FqnResolver.cs')
tests = read('tests/AccessHarvester.SymbolDb.Tests/ReferencesExtractorTests.cs')

fixture_data = """Fixture inspection results (NorthwindDevEdition, Form_frmCompanyDetail + modStrings):
- Total refs: 561, MemberAccesses: 156
- kind breakdown: variable_read=221, call=184, member_access=156, enum_value_use=0, control_access=0
- strDlgMsg: resolves to Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate.strDlgMsg (status=resolved)
- enum_value_use=0: cross-module enum members are unresolved in this fixture
- control_access=0: fixture uses dot notation (Me.Foo), not bang (Me!Foo)"""

prompt = (
    "Your response MUST begin with a detailed prose analysis for each concern below BEFORE the JSON block.\n\n"
    "## Session 2 Round-3 Verification — vba-symbol-db\n\n"
    "Round 2 found one Minor: 'the three new coverage tests are vacuous'. "
    "This round verifies the fix.\n\n"
    "### Round-2 finding and resolution:\n"
    "- Minor (vacuous tests): FIXED. strDlgMsg test is now assertive (uses .First() not .FirstOrDefault(), "
    "asserts both the procedure segment AND the module prefix). enum_value_use and control_access tests "
    "remain as shape guards with documented rationale: the fixture produces 0 of each kind (see data below). "
    "The guards are not vacuous for future fixtures that DO produce these kinds.\n\n"
    "### Fixture data (from inspection run):\n"
    + fixture_data + "\n\n"
    "### Build/test: 1578 passed / 3 skipped / 0 failed\n\n"
    "### Final test file:\n"
    "```csharp\n" + tests + "\n```\n\n"
    "### FqnResolver.cs (unchanged from round 2):\n"
    "```csharp\n" + fqn_resolver + "\n```\n\n"
    "### Concerns for this round:\n"
    "1. Is LocalVariable_ToFqn_IncludesProcedureSegment now fixture-assertive and correct?\n"
    "2. Are the guard tests (control_access, enum_value_use) acceptable given fixture data shows 0 each?\n"
    "3. Any remaining issues?\n\n"
    "After your analysis, output a JSON block in this exact format:\n"
    "```json\n"
    '{\n'
    '  "verdict": "VERIFIED" | "ISSUES_FOUND",\n'
    '  "issues": [\n'
    '    {\n'
    '      "severity": "Critical" | "Major" | "Minor",\n'
    '      "location": "<file or test name>",\n'
    '      "summary": "<one-sentence description>",\n'
    '      "fix": "<recommended fix>"\n'
    '    }\n'
    '  ]\n'
    '}\n'
    "```\n"
    "If verdict is VERIFIED, `issues` must be an empty array `[]`.\n"
    "Do not omit this block.\n"
)

print(f"Round-3 prompt: {len(prompt)} chars")
pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/').mkdir(parents=True, exist_ok=True)
pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/session-002-r3-prompt.txt').write_text(prompt, encoding='utf-8')

result = route(
    content=prompt,
    task_type='session-verification',
    complexity_hint=70,
    session_set=SESSION_SET,
    session_number=2,
)

review_path = 'docs/session-sets/vba-symbol-db/session-reviews/session-002-round-3.md'
pathlib.Path(review_path).write_text(result.content, encoding='utf-8')
print(f"Round-3 review saved to {review_path}")
print(f"Model: {result.model_name}, tokens: in={result.input_tokens} out={result.output_tokens}, cost=${result.cost_usd:.4f}")

log.log_step(2, 9, 'session-2/verification-round-3',
    f'Round-3 verification after strDlgMsg test made assertive; via {result.model_name}',
    'complete', [{
        'model': result.model_name,
        'taskType': 'session-verification',
        'inputTokens': result.input_tokens,
        'outputTokens': result.output_tokens,
        'costUsd': result.cost_usd,
    }])

print("Logged.")
# Print the verdict section
for line in result.content.split('\n'):
    if '"verdict"' in line or '"issues"' in line or 'VERIFIED' in line or 'ISSUES_FOUND' in line:
        print(line)
