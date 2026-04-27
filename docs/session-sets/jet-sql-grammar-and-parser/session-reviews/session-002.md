# Verification Round 1

## ISSUES_FOUND

- **Issue** → `ParseFragment` is API-incompatible with the declared result shape.  
  **Location** → `AccessHarvester.Sql.Parser/ParseResult.cs`, `AccessHarvester.Sql.Parser/SqlParserPipeline.cs`  
  **Fix** → `ParseResult.ParseTree` cannot stay typed as `JetSqlParser.ParseContext` if `ParseFragment` is meant to use the grammar’s `sqlFragment` entry rule. Change the property to `ParserRuleContext`, or split results into dedicated full-statement and fragment result types.

- **Issue** → Fragment parsing has no corresponding AST build path, leaving a grammar surface gap.  
  **Location** → `AccessHarvester.Sql.Parser/Ast/AstBuilder.cs`  
  **Fix** → `AstBuilder` only accepts `JetSqlParser.ParseContext` and produces `SqlStatement`. Add a `Build(JetSqlParser.SqlFragmentContext ...)` path plus fragment AST node(s), or narrow/remove the public `ParseFragment` contract until fragment AST support exists.

---

# Verification Round 2

## ISSUES_FOUND

- Verified → `SqlParserPipeline` no longer exposes fragment parsing; the public API surface is full-statement `Parse(...)` only.
- Verified → `ParseResult.ParseTree` is uniformly typed as `JetSqlParser.ParseContext`.

- Issue → Recovering mode reports a lossy fallback without actually invoking the lossy parse path.
  - Location → `AccessHarvester.Sql.Parser/SqlParserPipeline.cs`, `ParseRecovering`, in the `diagnostics.Count > options.LossyFallbackErrorThreshold` branch.
  - Fix → Replace the direct `new ParseResult(tree, diagnostics, ParseMode.Lossy)` return with an actual call to `ParseLossy(source, options)` (merging diagnostics if needed), or keep `ModeUsed = ParseMode.Recovering` and change the warning text if no reparse is intended.

- Issue → No additional structural problems are evident in the provided AST, visitor, walker, or `AstBuilder` snippets beyond the pipeline issue above.
  - Location → N/A
  - Fix → N/A
