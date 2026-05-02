"""Generate next-session-set recommendation."""
import os

# Historical artifact: see changelog_session4.py header comment.
base = 'C:/Users/denmi/source/repos/dabbler-access-harvester-jet-sql-grammar-and-parser'
if os.path.isdir(base):
    os.chdir(base)

from ai_router import route
from ai_router.session_log import SessionLog
log = SessionLog('docs/session-sets/jet-sql-grammar-and-parser')

def read(path):
    with open(path, encoding='utf-8') as f:
        return f.read()

tree_row = """
| 12 | `jet-sql-grammar-and-parser` | Phase 5.1-5.2 | #6 | 4-5 | 2 | Grammar + parser infrastructure |
| 13 | `jet-sql-resolution-and-dynamic` | Phase 5.3-5.4 (form-reference detection in SQL + dynamic SQL analysis) | #11b, #12 | 3-4 | 2 | AccessHarvester.Sql.Parser/FormReferences/, DynamicSql/ |
"""

# Check which prereqs of #13 are complete
prereq_11b_complete = os.path.exists('docs/session-sets/access-resolver-symbol-table-integration/change-log.md')

prompt = f"""You are advising on which session set to start next after completing `jet-sql-grammar-and-parser` (#12).

## DAG context

{tree_row}

## Prereq status for the immediate downstream set (#13)

- `jet-sql-grammar-and-parser` (#12): COMPLETE (just finished)
- `access-resolver-symbol-table-integration` (#11b): {'COMPLETE' if prereq_11b_complete else 'NOT YET COMPLETE'}

## This session's actuals

- Orchestrator: claude-code Sonnet 4.6 @ effort=unknown
- Routed cost: ~$0.22 (test-gen $0.028, verification $0.196, change-log $0.001)
- Deviations: Test-generation routed output was truncated (gemini-pro only generated ~11/24 tests); remaining tests authored in-conversation. Grammar fix (PIVOT concatExpression) identified and applied by orchestrator directly.
- Verification: VERIFIED by GPT-5-4 on round 1.

## Output

{'Since `access-resolver-symbol-table-integration` (#11b) is NOT yet complete, `jet-sql-resolution-and-dynamic` (#13) is BLOCKED. Recommend what to start instead.' if not prereq_11b_complete else 'The next set `jet-sql-resolution-and-dynamic` (#13) is now unblocked.'}

Produce the next-session-set recommendation in this exact format:

**Next session-set recommendation (after `jet-sql-grammar-and-parser`):**
`<next-slug>` {'(blocked — awaiting #11b; recommend next-available unblocked set instead)' if not prereq_11b_complete else ''}

**Recommended orchestrator for `<next-slug>`:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2 sentences grounded in (a) next set synthesis count and (b) what this session revealed>
"""

result = route(
    content=prompt,
    task_type='analysis',
    complexity_hint=40,
    session_set='docs/session-sets/jet-sql-grammar-and-parser',
    session_number=4,
)

out_path = 'docs/session-sets/jet-sql-grammar-and-parser/next-set-recommendation.md'
with open(out_path, 'w', encoding='utf-8') as f:
    f.write(result.content)

print(f'Next-set recommendation generated. Model: {result.model_name}')
print(f'Tokens: {result.input_tokens} in / {result.output_tokens} out')
print(f'Cost: ${result.cost_usd:.4f}')

preview = result.content[:600].encode('ascii', errors='replace').decode('ascii')
print(f'\nPreview:\n{preview}')

log.log_step(4, 6, 'session-4/next-session-set-recommendation',
    description=f'Routed next-session-set recommendation via {result.model_name}',
    status='complete',
    api_calls=[{
        'model': result.model_name,
        'taskType': 'analysis',
        'inputTokens': result.input_tokens,
        'outputTokens': result.output_tokens,
        'costUsd': result.cost_usd,
    }])
print('Logged.')
