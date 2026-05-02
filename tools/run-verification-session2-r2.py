"""Session 2 round-2 verification: M1 fixed (local FQNs), Minor addressed (more tests).
M2 dismissed via tiebreaker (ACCEPT_DISMISSAL)."""
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

prompt = (
    "Your response MUST begin with a detailed prose analysis for each concern below BEFORE the JSON block.\n\n"
    "## Session 2 Round-2 Verification — vba-symbol-db\n\n"
    "Round 1 found 2 Major + 1 Minor. This round verifies the fixes.\n\n"
    "### Round-1 findings and resolution:\n"
    "- M1 (Major, FqnResolver local FQNs): FIXED. DeclarationFqn now detects ProcedureScope "
    "and emits <Module>.<Proc>.<Local> for procedure-local declarations.\n"
    "- M2 (Major, variable_write detection): DISMISSED via tiebreaker (Gemini Pro). "
    "Fix requires modifying the upstream ReferenceResolver contract; out of Session 2 scope. "
    "variable_read is the correct fallback for all identifier references in this session. "
    "Known limitation recorded.\n"
    "- Minor (test coverage): FIXED. Added tests for enum_value_use rows, local to_fqn "
    "includes procedure segment, and control_access kind rows.\n\n"
    "### Build/test results after fixes: 1578 passed / 3 skipped / 0 failed\n\n"
    "### Fixed files:\n\n"
    "#### FqnResolver.cs (M1 fix — DeclarationFqn now handles ProcedureScope locals)\n"
    "```csharp\n" + fqn_resolver + "\n```\n\n"
    "#### ReferencesExtractorTests.cs (Minor fix — 3 new test methods added)\n"
    "```csharp\n" + tests + "\n```\n\n"
    "### Files unchanged from round 1 (still present and correct):\n"
    "ReferenceRow.cs, MemberAccessRow.cs, ReferencesExtractor.cs, BuildResult.cs, "
    "SymbolDbWriter.cs WriteReferences/WriteMemberAccesses methods.\n\n"
    "### Concerns for this round:\n"
    "1. Is the M1 fix (local FQN three-part structure) consistent with Session 1's "
    "LocalVariableExtractor pattern of <Module>.<ProcFqn>.<LocalName>? Specifically: "
    "for a Property procedure, ProcedureFqn returns <module>.<name>#<kind> — does that "
    "extend correctly to locals: <module>.<name>#<kind>.<localName>?\n"
    "2. Is M2 dismissal acceptable? Confirm that persisting 'variable_read' for writes "
    "is a documented known limitation rather than a silent defect, given the tiebreaker's ruling.\n"
    "3. Do the three new tests adequately cover the gaps identified in the Minor finding?\n"
    "4. Any remaining issues?\n\n"
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

print(f"Round-2 prompt: {len(prompt)} chars")

pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/').mkdir(parents=True, exist_ok=True)
pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/session-002-r2-prompt.txt').write_text(prompt, encoding='utf-8')

result = route(
    content=prompt,
    task_type='session-verification',
    complexity_hint=85,
    session_set=SESSION_SET,
    session_number=2,
)

review_path = 'docs/session-sets/vba-symbol-db/session-reviews/session-002-round-2.md'
pathlib.Path(review_path).write_text(result.content, encoding='utf-8')
print(f"Round-2 review saved to {review_path}")
print(f"Model: {result.model_name}, tokens: in={result.input_tokens} out={result.output_tokens}, cost=${result.cost_usd:.4f}")

log.log_step(2, 8, 'session-2/verification-round-2',
    f'Round-2 verification after M1 fix + minor test additions; M2 dismissed via tiebreaker; via {result.model_name}',
    'complete', [{
        'model': result.model_name,
        'taskType': 'session-verification',
        'inputTokens': result.input_tokens,
        'outputTokens': result.output_tokens,
        'costUsd': result.cost_usd,
    }])

print("Logged.")
print("\n--- Round-2 verdict ---")
print(result.content[-400:])
