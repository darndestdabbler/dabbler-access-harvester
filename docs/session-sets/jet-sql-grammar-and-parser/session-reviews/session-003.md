1. **Yes.** The session changes correctly implement `ParseFragment()` against the `sqlFragment` entry rule:
   - `SqlParserPipeline.ParseFragment(...)` dispatches by `RequestedMode`
   - dedicated `ParseFragmentStrict/Recovering/Lossy` helpers were added
   - `ParseResult` now stores the normalized `sqlStatement` node, so both `parse` and `sqlFragment` flows return the same shape

2. **Yes, for `BuildAst()`.** The `ParseTree` type change to `JetSqlParser.SqlStatementContext` is compatible with the AST build path because:
   - `ParseResult` now always exposes `SqlStatementContext`
   - `AstBuilder.Build(...)` was updated from `ParseContext` to `SqlStatementContext`
   - `BuildAst()` can continue consuming `result.ParseTree` without semantic loss

   **Note:** this is an API shape change for any external code that directly expected `ParseContext`, but it does **not** break the `BuildAst()` extension path.

3. **Yes.** The 45 tests provide meaningful coverage for Session 3 goals:
   - parser mode contract coverage: `Strict`, `Recovering`, `Lossy`
   - `ParseFragment()` coverage for fixture-05-style dynamic SQL
   - fixture 04 coverage: full parse plus AST shape checks for parameters, joins, `IIf`, `DateDiff`, `IN`, `IS NULL`, `ORDER BY`
   - fixture 05 coverage: assembled dynamic SQL, compound `WHERE`, `DateDiff` comparison, `IN` subquery, fragment AST construction
   - source-path propagation and `BuildAst()` round-trip checks

   Coverage is representative and structurally meaningful for both fixtures.

4. **Critical/Major issues:** None.

**Verdict: VERIFIED**