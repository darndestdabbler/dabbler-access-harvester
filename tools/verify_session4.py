"""Session 4 cross-provider verification script."""
import importlib.util, sys, os

def load_ai_router(base):
    spec = importlib.util.spec_from_file_location(
        'ai_router', f'{base}/ai-router/__init__.py',
        submodule_search_locations=[f'{base}/ai-router'])
    mod = importlib.util.module_from_spec(spec)
    sys.modules['ai_router'] = mod
    spec.loader.exec_module(mod)
    return mod

base = 'C:/Users/denmi/source/repos/dabbler-access-harvester-jet-sql-grammar-and-parser'
os.chdir(base)
ar = load_ai_router(base)
route = ar.route
from ai_router.session_log import SessionLog
log = SessionLog('docs/session-sets/jet-sql-grammar-and-parser')

def read(path):
    with open(path, encoding='utf-8') as f:
        return f.read()

g4 = read('AccessHarvester.Sql.Grammar/JetSql.g4')
shape_tests = read('tests/AccessHarvester.Sql.Grammar.Tests/Ast/SqlAstBuilderShapeTests.cs')
pipeline_tests = read('tests/AccessHarvester.Sql.Grammar.Tests/JetSqlParserPipelineTests.cs')
ast_builder = read('AccessHarvester.Sql.Parser/Ast/AstBuilder.cs')
sql_statement = read('AccessHarvester.Sql.Parser/Ast/SqlStatement.cs')
sql_expression = read('AccessHarvester.Sql.Parser/Ast/SqlExpression.cs')

session4_spec = """
## Session 4 spec (jet-sql-grammar-and-parser)

Session 4 of 4-5: Cross-Cut + Edge-Case Fixtures

Goal: Author additional SQL fixture strings for dialect surfaces not covered by
fixtures 04/05 — specifically TRANSFORM...PIVOT, SELECT TOP/DISTINCT, GROUP BY/HAVING,
Switch/Choose functions, NOT IN/NOT BETWEEN/NOT LIKE, Reports! references, domain
functions (DSum/DCount), RIGHT JOIN, INSERT...SELECT, UNION 3-way, NULL/TRUE literals,
OR, parameter placeholder, subquery table source.

Grammar fix applied in this session:
- Changed `PIVOT expression` to `PIVOT concatExpression` in transformStatement rule
  to resolve ambiguity where the `expression` rule was greedily consuming the
  optional `IN (...)` clause as a comparison expression (IN predicate).
  This fixes PivotInValues always being null when the IN clause was present.
  Updated AstBuilder.BuildTransformStatement to use ctx.concatExpression() and
  ctx.expression() (no index) for the aggregate.

Build result: 0 errors, 279 warnings (all pre-existing). Tests: 745 passed, 2 COM-skipped.

Files not in scope this session (present in repo, unchanged):
- AccessHarvester.Sql.Parser/SqlParserPipeline.cs
- AccessHarvester.Sql.Parser/Ast/SqlSyntaxNode.cs, Provenance.cs, SourceLocation.cs
- All VBA parser/grammar projects
- All Access harvester projects
"""

prompt = f"""You are an independent cross-provider reviewer verifying Session 4 of the
`jet-sql-grammar-and-parser` session set for the Dabbler Access Harvester project.

## Session context

{session4_spec}

## Files changed this session

### AccessHarvester.Sql.Grammar/JetSql.g4 (grammar — key change: transformStatement rule)
```
{g4}
```

### AccessHarvester.Sql.Parser/Ast/AstBuilder.cs (only BuildTransformStatement changed)
```
{ast_builder}
```

### AccessHarvester.Sql.Parser/Ast/SqlStatement.cs
```
{sql_statement}
```

### AccessHarvester.Sql.Parser/Ast/SqlExpression.cs
```
{sql_expression}
```

### tests/AccessHarvester.Sql.Grammar.Tests/Ast/SqlAstBuilderShapeTests.cs (24 new tests appended)
```
{shape_tests}
```

### tests/AccessHarvester.Sql.Grammar.Tests/JetSqlParserPipelineTests.cs (unchanged)
```
{pipeline_tests}
```

## Review instructions

1. Check that the 24 new test methods in SqlAstBuilderShapeTests.cs are correct:
   - Each test uses the correct API (BuildAst / BuildFragmentAst, correct type assertions)
   - Each test targets a grammar rule NOT covered by the pre-existing tests
   - Each test's SQL string is valid JET SQL that the grammar can parse

2. Verify the grammar fix for PIVOT concatExpression:
   - Confirm the fix resolves the ambiguity (IN clause no longer consumed by expression)
   - Confirm the fix does not break any existing grammar rules
   - Confirm AstBuilder.BuildTransformStatement correctly uses ctx.concatExpression() and ctx.expression()

3. Check for any correctness issues in the new tests:
   - Wrong type assertions
   - Incorrect property accesses
   - SQL strings that the grammar would reject (syntax errors)

4. Identify any grammar rules that remain uncovered that would be HIGH priority for
   downstream use (form-reference resolution, dynamic SQL, enrichment pipeline).

Respond with:
VERDICT: VERIFIED or ISSUES_FOUND

Followed by findings (if ISSUES_FOUND). Be specific about which test or line has an issue.
Prefer false-positive ISSUES_FOUND over false-negative VERIFIED.
"""

round_num = int(sys.argv[1]) if len(sys.argv) > 1 else 1
complexity = 85 if round_num > 1 else 70

result = route(
    content=prompt,
    task_type='session-verification',
    complexity_hint=complexity,
    session_set='docs/session-sets/jet-sql-grammar-and-parser',
    session_number=4,
)

out_path = f'docs/session-sets/jet-sql-grammar-and-parser/session-reviews/session-004-r{round_num}.md'
os.makedirs('docs/session-sets/jet-sql-grammar-and-parser/session-reviews', exist_ok=True)
with open(out_path, 'w', encoding='utf-8') as f:
    f.write(result.content)

print(f'Verification R{round_num} complete. Model: {result.model_name}')
print(f'Tokens: {result.input_tokens} in / {result.output_tokens} out')
print(f'Cost: ${result.cost_usd:.4f}')
print()

# Print first 500 chars for visibility
preview = result.content[:500].encode('ascii', errors='replace').decode('ascii')
print(f'Preview:\n{preview}')

log.log_step(4, 3 + round_num, f'session-4/cross-provider-verification-r{round_num}',
    description=f'Cross-provider verification R{round_num} via {result.model_name}',
    status='complete',
    api_calls=[{
        'model': result.model_name,
        'taskType': 'session-verification',
        'inputTokens': result.input_tokens,
        'outputTokens': result.output_tokens,
        'costUsd': result.cost_usd,
    }])
log.save_session_review(session_number=4, review_text=result.content, round_number=round_num)
print('Logged.')
