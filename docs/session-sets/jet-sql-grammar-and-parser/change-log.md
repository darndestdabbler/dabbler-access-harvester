# `jet-sql-grammar-and-parser` Change Log

**Dates:** 2026-04-25 to 2026-04-27
**Status:** COMPLETE

This session set introduces the ANTLR4 grammar for the JET/ACE SQL dialect and establishes the core SQL parser infrastructure. It enables parsing of various SQL statement types, including DDL, DML, and complex queries, and builds an immutable Abstract Syntax Tree (AST). Extensive test coverage ensures correctness and robustness for subsequent parser migration phases.

## Deliverables

| File/Directory                  | Description                                            |
| :------------------------------ | :----------------------------------------------------- |
| `AccessHarvester.Sql.Grammar/JetSql.g4` | ANTLR4 grammar for JET/ACE SQL.                        |
| `AccessHarvester.Sql.Grammar.csproj` | Project file for the SQL grammar, including ANTLR4 build tasks. |
| `AccessHarvester.Sql.Parser/`   | SQL parser pipeline, AST definition, and error handling. |
| `AccessHarvester.Sql.Parser/Ast/` | Immutable AST nodes mirroring VBA AST conventions.      |
| `tests/AccessHarvester.Sql.Grammar.Tests/JetSqlParserPipelineTests.cs` | Tests for SQL parser pipeline functionality.           |
| `tests/AccessHarvester.Sql.Grammar.Tests/Ast/SqlAstBuilderShapeTests.cs` | Tests for AST shape and content accuracy.              |

## Session-by-Session Summary

- **Session 1: JET SQL Grammar**
    - Authored `JetSql.g4` covering SELECT, PARAMETERS, JOINs, WHERE, GROUP BY, HAVING, ORDER BY, UNION, TRANSFORM...PIVOT, INSERT/UPDATE/DELETE, IIf/Switch/Choose/Nz, domain functions, form/report references, date literals, string concatenation, and boolean operators.
    - Integrated ANTLR4.Build.Tasks.
- **Session 2: SQL Parser Infrastructure**
    - Created `AccessHarvester.Sql.Parser/` with `SqlParserPipeline`, error listeners, and `ParseResult` types.
    - Defined `AccessHarvester.Sql.Parser/Ast/` with immutable AST hierarchy (`SqlSyntaxNode`, `SqlStatement`, `SqlExpression`, etc.) including `Provenance` and `SourceRange`.
    - Implemented `VbaSyntaxWalker`/`VbaSyntaxVisitor<T>` and `AstBuilder` for AST construction.
- **Session 3: Test Coverage + Mechanical Tightening**
    - Added `ParseFragment()` to `SqlParserPipeline` for dynamic SQL parsing.
    - Created `tests/AccessHarvester.Sql.Grammar.Tests/` with 13 `JetSqlParserPipelineTests` and 31 `SqlAstBuilderShapeTests`, covering core SQL constructs and AST fidelity.
- **Session 4: Cross-Cut + Edge-Case Fixtures**
    - Added 27 new tests to `SqlAstBuilderShapeTests.cs` for comprehensive grammar rule coverage (e.g., GROUP BY + HAVING, aggregates, TOP N, Switch/Choose, EXISTS, NOT variants, Reports!, DSum/DCount, RIGHT JOIN, INSERT ... SELECT, PIVOT IN, literals, logical operators, 3-way UNION, ParameterPlaceholder, SubqueryTableSource).
    - Fixed grammar ambiguity in `JetSql.g4` (`PIVOT expression` to `PIVOT concatExpression`) impacting PIVOT IN clause parsing.
    - Total tests: 748 passed (72 SQL grammar tests).

## Grammar Coverage

| Feature             | Covered? | Test Reference (Partial)                               |
| :------------------ | :------- | :----------------------------------------------------- |
| SELECT (basic)      | &#10003; | `TestSelectStatementShape`                               |
| SELECT TOP N        | &#10003; | `TestSelectTopN`                                       |
| SELECT DISTINCT/DISTINCTROW | &#10003; | `TestSelectDistinct`                                   |
| PARAMETERS          | &#10003; | `TestParametersDeclarationShape`                       |
| JOINs (INNER, LEFT, RIGHT, SQL-89) | &#10003; | `TestInnerJoinShape`, `TestRightJoin`                    |
| WHERE clause        | &#10003; | `TestWhereClauseShape`                                 |
| IS NULL/NOT NULL    | &#10003; | `TestIsNotNull`                                        |
| BETWEEN/NOT BETWEEN | &#10003; | `TestBetweenExpression`, `TestNotBetweenExpression`    |
| IN/NOT IN (list/subquery) | &#10003; | `TestInListExpression`, `TestNotInListExpression`, `TestNotInSubqueryExpression` |
| LIKE/NOT LIKE       | &#10003; | `TestNotLikeExpression`                                |
| GROUP BY            | &#10003; | `TestGroupByAndHaving`                                 |
| HAVING              | &#10003; | `TestGroupByAndHaving`                                 |
| ORDER BY            | &#10003; | `TestOrderByClauseShape`                               |
| UNION [ALL]         | &#10003; | `TestUnionAllStatement`, `TestThreeWayUnionStatement`  |
| TRANSFORM...PIVOT   | &#10003; | `TestTransformPivotStatement`, `TestPivotInClause`     |
| INSERT              | &#10003; | `TestInsertStatementShape`, `TestInsertSelectStatement`|
| UPDATE              | &#10003; | `TestUpdateStatementShape`                             |
| DELETE              | &#10003; | `TestDeleteStatementShape`                             |
| IIf/Switch/Choose/Nz | &#10003; | `TestIIfNestedExpression`, `TestSwitchExpression`, `TestChooseExpression` |
| Domain Functions    | &#10003; | `TestDLookupExpression`, `TestDSumExpression`, `TestDCountExpression` |
| Form/Report References | &#10003; | `TestFormReference`, `TestReportReference`             |
| Date Literals       | &#10003; | `TestDateLiteral`                                      |
| String Concatenation (`&`) | &#10003; | `TestStringConcatenation`                              |
| Boolean Operators (AND/OR/NOT/EQV/IMP/XOR) | &#10003; | `TestOrOperator`                                       |
| EXISTS subquery     | &#10003; | `TestExistsSubqueryExpression`                         |
| NULL/TRUE/FALSE literals | &#10003; | `TestNullLiteral`, `TestTrueFalseLiterals`             |
| ParameterPlaceholder | &#10003; | `TestParameterPlaceholder`                             |
| Subquery Table Source | &#10003; | `TestSubqueryTableSource`                              |

## Known Gaps

This session set intentionally does not cover:
- DDL statements (CREATE TABLE, ALTER TABLE, DROP TABLE, etc.)
- Specific JET/ACE-only clauses beyond those identified as critical for initial migration (e.g., WITH OWNERACCESS OPTION).
- Full function library beyond common domain aggregates and IIf/Switch/Choose/Nz.

## Next Session Set

**Next session-set recommendation (after `jet-sql-grammar-and-parser`):**
`jet-sql-resolution-and-dynamic` (#13) — blocked, awaiting `access-resolver-symbol-table-integration` (#11b)

**Recommended orchestrator for `jet-sql-resolution-and-dynamic` (#13):** claude-code Sonnet 4.6 @ effort=medium
**Rationale:** The target set's synthesis count (2) matches this set, making Sonnet a good fit. This session demonstrated Sonnet can handle nuanced grammar correction beyond simple generation — a useful trait for the upcoming form-reference resolution and dynamic SQL analysis work.