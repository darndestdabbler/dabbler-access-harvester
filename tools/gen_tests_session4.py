"""Session 4 test-generation script for jet-sql-grammar-and-parser."""
import os

# Historical artifact: see changelog_session4.py header comment.
base = 'C:/Users/denmi/source/repos/dabbler-access-harvester-jet-sql-grammar-and-parser'
if os.path.isdir(base):
    os.chdir(base)

from ai_router import route
from ai_router.session_log import SessionLog
log = SessionLog('docs/session-sets/jet-sql-grammar-and-parser')

PROMPT = r"""You are generating xUnit test methods in C# for the JET/ACE SQL parser (AccessHarvester.Sql.Grammar.Tests).

## Context

The test class `SqlAstBuilderShapeTests` in `tests/AccessHarvester.Sql.Grammar.Tests/Ast/SqlAstBuilderShapeTests.cs` already covers:
- SELECT * (StarSelectList), PARAMETERS form-refs and bracketed names
- SELECT list with aliases, QualifiedStarSelectItem
- FROM + simple table (SimpleTableSource), nested parenthesized LEFT/INNER JOINs
- WHERE: AND, IN list, IS NULL, IS NOT NULL
- ORDER BY ASC/DESC
- IIf (simple and nested), & concatenation, FormReference ([Forms]!...)
- Date literals (#1/1/2024#), DLookup domain function, BETWEEN, ColumnReference
- Basic TRANSFORM...PIVOT (without IN clause), UPDATE, INSERT VALUES, DELETE, UNION/UNION ALL
- Provenance (SourceProvenance on clean input)
- ParseFragment fixture-05 shapes

## What is NOT yet covered (author tests for these)

Author one focused xUnit [Fact] test per item. Each test must parse a minimal valid JET SQL string and assert on the produced AST shape.

1. GROUP BY + COUNT(*) aggregate: `SELECT COUNT(*) FROM tbl GROUP BY dept`. Assert GroupBy has one item; SelectList item value is CountStarExpression.
2. GROUP BY + HAVING: `SELECT dept, COUNT(*) FROM tbl GROUP BY dept HAVING COUNT(*) > 5`. Assert Having is BinaryExpression with GreaterThan.
3. SELECT DISTINCT: `SELECT DISTINCT name FROM tbl`. Assert Quantifier == SelectQuantifier.Distinct.
4. SELECT TOP N: `SELECT TOP 10 * FROM tbl`. Assert Top.N == 10, Top.Percent == false.
5. SELECT TOP N PERCENT: `SELECT TOP 25 PERCENT name FROM tbl`. Assert Top.Percent == true.
6. Switch function: `SELECT Switch(x=1, 'one', x=2, 'two', True, 'other') FROM t`. Assert IsType<SwitchExpression> with 3 pairs.
7. Choose function: `SELECT Choose(n, 'first', 'second', 'third') FROM t`. Assert IsType<ChooseExpression> with Index and 3 Choices.
8. EXISTS subquery: `SELECT 1 FROM t WHERE EXISTS (SELECT 1 FROM other WHERE other.id = t.id)`. Assert Where is ExistsExpression.
9. NOT IN list: `SELECT 1 FROM t WHERE status NOT IN ('Closed', 'Archived')`. Assert IsType<InListExpression> with Negated == true and 2 values.
10. NOT BETWEEN: `SELECT 1 FROM t WHERE age NOT BETWEEN 18 AND 65`. Assert IsType<NotBetweenExpression>.
11. NOT LIKE: `SELECT 1 FROM t WHERE name NOT LIKE '*Smith*'`. Assert IsType<BinaryExpression> with Operator == BinaryOperator.NotLike.
12. Reports! form reference: `SELECT Reports![rptSummary]![txtTotal] FROM t`. Assert IsType<FormReference>; Root is ReportsRoot.
13. DSum domain function (2 args, no criteria): `SELECT DSum('Amount', 'tblOrders') FROM t`. Assert IsType<DomainFunctionExpression> with FunctionName == "DSUM" and CriteriaExpr == null.
14. DCount domain function (3 args with criteria): `SELECT DCount('*', 'tblOrders', 'Status=1') FROM t`. Assert FunctionName == "DCOUNT" and CriteriaExpr != null.
15. RIGHT JOIN: `SELECT 1 FROM tblA RIGHT JOIN tblB ON tblA.id = tblB.id`. Assert join Kind == JoinKind.Right.
16. INSERT ... SELECT: `INSERT INTO tblArchive SELECT * FROM tblCases WHERE status = 'Closed'`. Assert InsertStatement.Source is SelectSource.
17. TRANSFORM with PIVOT IN clause: `TRANSFORM Sum(o.Amount) SELECT o.CustomerID FROM tblOrders AS o GROUP BY o.CustomerID PIVOT o.Quarter IN ('Q1','Q2','Q3','Q4')`. Assert IsType<TransformStatement>; PivotInValues has 4 items.
18. NULL literal: `SELECT 1 FROM t WHERE col IS NULL`. But this is already covered (IsNullExpression). Instead test NULL in expression list: `SELECT 1 FROM t WHERE col = NULL`. Assert WHERE is BinaryExpression whose Right is NullLiteralExpression.
19. TRUE / FALSE literals: `SELECT 1 FROM t WHERE active = True`. Assert BinaryExpression right side is TrueLiteralExpression.
20. OR logical operator: `SELECT 1 FROM t WHERE a = 1 OR b = 2`. Assert IsType<BinaryExpression> with Operator == BinaryOperator.Or.
21. UNION three-way: `SELECT 1 FROM a UNION SELECT 2 FROM b UNION ALL SELECT 3 FROM c`. Assert union.Selects.Count == 3; AllFlags[2] == true.
22. SUM aggregate function call: `SELECT SUM(amount) FROM tbl GROUP BY dept`. Assert item value IS FunctionCallExpression with FunctionName == "SUM".
23. Parameter placeholder ?: `SELECT 1 FROM t WHERE id = ?`. Assert WHERE right side is ParameterPlaceholderExpression.
24. Subquery table source: `SELECT 1 FROM (SELECT id FROM base) AS sub`. Assert From.Sources.Single() is SubqueryTableSource.

## Helper methods (already defined in the class — use them)

```csharp
private static SqlStatement BuildAst(string source) =>
    SqlParserPipeline.Parse(source, new ParseOptions { RequestedMode = ParseMode.Strict })
                     .BuildAst();

private static SqlStatement BuildFragmentAst(string source) =>
    SqlParserPipeline.ParseFragment(source, new ParseOptions { RequestedMode = ParseMode.Strict })
                     .BuildAst();
```

## AST node types (namespace AccessHarvester.Sql.Parser.Ast)

From SqlStatement.cs:
- SelectCore: Quantifier (SelectQuantifier), Top (TopClause?), GroupBy (IReadOnlyList<Expression>?), Having (Expression?)
- SelectQuantifier: None, All, Distinct, DistinctRow
- TopClause: N (int), Percent (bool)
- TransformStatement: PivotInValues (IReadOnlyList<Expression>?)
- InsertStatement.Source: ValuesSource or SelectSource (SelectSource.Select is UnionStatement)
- JoinKind: Inner, Left, Right
- SubqueryTableSource: Subquery, Alias, Joins

From SqlExpression.cs:
- SwitchExpression: Pairs (IReadOnlyList<SwitchPair>)
- ChooseExpression: Index, Choices (IReadOnlyList<Expression>)
- DomainFunctionExpression: FunctionName (string uppercase), CriteriaExpr (Expression? null on 2-arg form)
- CountStarExpression: ()
- FunctionCallExpression: FunctionName (string), Arguments
- ExistsExpression: Subquery
- InListExpression: Negated (bool), Values
- NotBetweenExpression: Operand, Low, High
- BinaryOperator: Or, And, Equal, GreaterThan, LessThan, Like, NotLike, ...
- FormReferenceRoot: FormsRoot, ReportsRoot
- NullLiteralExpression, TrueLiteralExpression, FalseLiteralExpression
- ParameterPlaceholderExpression: ()

## Output format

Output ONLY the C# test methods — no class declaration, no usings, no explanations. Each test:
- Has [Fact] attribute
- Has a name following pattern `Noun_Scenario_AssertsExpectedShape`
- Uses Assert.IsType<T>() for type checks
- Uses var, Linq .Single(), .Selects.Single(), etc.
- Follows existing code style exactly (2-space indent inside methods relative to class)
"""

result = route(
    content=PROMPT,
    task_type='test-generation',
    complexity_hint=40,
    session_set='docs/session-sets/jet-sql-grammar-and-parser',
    session_number=4,
)

out_path = 'docs/session-sets/jet-sql-grammar-and-parser/generated-tests-session4.md'
with open(out_path, 'w', encoding='utf-8') as f:
    f.write(result.content)

print(f'Test generation complete. Model: {result.model_name}')
print(f'Tokens: {result.input_tokens} in / {result.output_tokens} out')
print(f'Cost: ${result.cost_usd:.4f}')

log.log_step(4, 1, 'session-4/test-generation',
    description=f'Routed test-generation for 24 uncovered grammar rules via {result.model_name}',
    status='complete',
    api_calls=[{
        'model': result.model_name,
        'taskType': 'test-generation',
        'inputTokens': result.input_tokens,
        'outputTokens': result.output_tokens,
        'costUsd': result.cost_usd,
    }])
print('Logged.')
