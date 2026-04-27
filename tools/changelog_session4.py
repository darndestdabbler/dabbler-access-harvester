"""Generate change-log for jet-sql-grammar-and-parser."""
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

prompt = """Write a concise change-log for the `jet-sql-grammar-and-parser` session set.

## Session set summary

This set delivered Phase 5.1-5.2 of the VBA Parser Migration plan: the ANTLR4 grammar
for the JET/ACE SQL dialect and the SQL parser infrastructure.

### Session 1: JET SQL Grammar
- Authored `AccessHarvester.Sql.Grammar/JetSql.g4` covering: SELECT (TOP, DISTINCT,
  DISTINCTROW, star, columns), PARAMETERS declaration with form-reference and bracketed
  names, JOIN variants (INNER, LEFT, RIGHT, SQL-89-style nested parenthesized), WHERE
  (comparison, IS NULL, BETWEEN, IN, LIKE, NOT variants), GROUP BY, HAVING, ORDER BY,
  UNION [ALL], TRANSFORM...PIVOT (with optional IN clause), INSERT/UPDATE/DELETE,
  IIf/Switch/Choose/Nz as dedicated functions, domain functions (DLookup/DSum/DCount/
  DMax/DMin/DAvg/DFirst/DLast), form/report references ([Forms]!/[Reports]!), date
  literals (#...#), string concatenation (&), boolean operators (AND/OR/NOT/EQV/IMP/XOR).
- Wired ANTLR4.Build.Tasks into `AccessHarvester.Sql.Grammar.csproj`.
- Build green; all existing tests pass.

### Session 2: SQL Parser Infrastructure
- Created `AccessHarvester.Sql.Parser/` with: `ParseMode`, `ParseOptions`, `ParseResult`,
  `ParseDiagnostic`, `DiagnosticSeverity`, `ParseDiagnosticCollectingErrorListener`,
  `StatementBoundaryErrorStrategy`, `ThrowingErrorListener`, `SqlParserPipeline`
  (Parse + ParseFragment entry points).
- Created `AccessHarvester.Sql.Parser/Ast/` with immutable sealed record AST hierarchy
  mirroring VBA AST conventions: `SqlSyntaxNode`, `SqlStatement`, `ParametersDeclaration`,
  `DmlStatement`, `UnionStatement`, `SelectCore`, `TransformStatement`, `UpdateStatement`,
  `InsertStatement`, `DeleteStatement`, expression hierarchy (BinaryExpression,
  UnaryExpression, IIfExpression, SwitchExpression, ChooseExpression, DomainFunctionExpression,
  CountStarExpression, FunctionCallExpression, FormReference, ColumnReference,
  InListExpression, InSubqueryExpression, ExistsExpression, BetweenExpression, etc.),
  `Provenance` (Source/Recovered), `SourceRange`, `SourceLocation`.
- `VbaSyntaxWalker` / `VbaSyntaxVisitor<T>` visitor pair; `AstBuilder` (ANTLR visitor
  producing AST from parse tree); `ParseResult.BuildAst()` extension.
- Build green; all existing tests pass.

### Session 3: Test Coverage + Mechanical Tightening
- Added `ParseFragment()` to `SqlParserPipeline` so dynamic-SQL strings reconstructed
  from VBA can be parsed without trailing EOF concerns.
- Created `tests/AccessHarvester.Sql.Grammar.Tests/` with:
  - `JetSqlParserPipelineTests.cs` (13 tests): Strict/Recovering/Lossy mode, fixture-04
    full parse, fixture-05 assembled/subquery strings via ParseFragment, BuildAst round-trips.
  - `Ast/SqlAstBuilderShapeTests.cs` (31 tests): AST shape assertions for PARAMETERS,
    SELECT list, FROM, JOINs, WHERE predicates, ORDER BY, IIf (nested), concatenation,
    FormReference, date literal, DLookup, BETWEEN, UNION/UNION ALL, TRANSFORM, UPDATE,
    INSERT VALUES, DELETE, provenance.
- All 44 new SQL tests pass.

### Session 4: Cross-Cut + Edge-Case Fixtures (this session)
- Added 27 new tests to `SqlAstBuilderShapeTests.cs` covering grammar rules not exercised
  by fixtures 04/05: GROUP BY + HAVING + COUNT(*), SUM aggregate, SELECT DISTINCT, SELECT
  TOP N, SELECT TOP N PERCENT, Switch(3-pair), Choose(3-choice), EXISTS subquery, NOT IN
  list (Negated=true), NOT BETWEEN, NOT LIKE, Reports! reference (bare and bracketed),
  DSum (2-arg, CriteriaExpr=null), DCount (3-arg, CriteriaExpr≠null), RIGHT JOIN, INSERT
  ... SELECT (SelectSource), TRANSFORM with PIVOT IN clause (4-value list), NULL literal,
  TRUE/FALSE literals, OR operator, 3-way UNION (UNION then UNION ALL), ParameterPlaceholder,
  subquery table source (SubqueryTableSource), NOT IN (SELECT ...) negated subquery.
- Grammar fix: changed `PIVOT expression` to `PIVOT concatExpression` in `JetSql.g4` to
  resolve an ambiguity where the `expression` rule greedily consumed the optional
  PIVOT IN (...) clause as a comparison-expression IN predicate. Updated AstBuilder
  accordingly. This was a correctness gap surfaced by the new PIVOT-IN test.
- Final test counts: 72 SQL grammar tests + 54 VBA parser tests + 294 VBA grammar tests
  + 328 harvester tests = 748 total passed, 2 COM-gated skipped.
- Session verified VERIFIED by GPT-5-4 cross-provider review.

## Output format

Write a markdown change-log file with these sections:
- Header: session set name, dates (created 2026-04-25, completed 2026-04-27), status: COMPLETE
- Summary paragraph (2-3 sentences)
- Deliverables table (file → what it is)
- Session-by-session bullet list
- Grammar coverage table (feature → covered? → test reference)
- Known gaps (what this set intentionally does not cover)
- Next session-set pointer (placeholder — will be filled below)

Keep it concise. Technical audience. No fluff.
"""

result = route(
    content=prompt,
    task_type='summarization',
    complexity_hint=20,
    max_tier=1,
    session_set='docs/session-sets/jet-sql-grammar-and-parser',
    session_number=4,
)

out_path = 'docs/session-sets/jet-sql-grammar-and-parser/change-log.md'
with open(out_path, 'w', encoding='utf-8') as f:
    f.write(result.content)

print(f'Change-log generated. Model: {result.model_name}')
print(f'Tokens: {result.input_tokens} in / {result.output_tokens} out')
print(f'Cost: ${result.cost_usd:.4f}')

log.log_step(4, 5, 'session-4/change-log',
    description=f'Generated change-log via {result.model_name} (summarization, max_tier=1)',
    status='complete',
    api_calls=[{
        'model': result.model_name,
        'taskType': 'summarization',
        'inputTokens': result.input_tokens,
        'outputTokens': result.output_tokens,
        'costUsd': result.cost_usd,
    }])
print('Logged.')
