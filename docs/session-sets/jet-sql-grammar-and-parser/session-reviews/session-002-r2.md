## ISSUES_FOUND

- Verified → `SqlParserPipeline` no longer exposes fragment parsing; the public API surface is full-statement `Parse(...)` only.
- Verified → `ParseResult.ParseTree` is uniformly typed as `JetSqlParser.ParseContext`.

- Issue → Recovering mode reports a lossy fallback without actually invoking the lossy parse path.
  - Location → `AccessHarvester.Sql.Parser/SqlParserPipeline.cs`, `ParseRecovering`, in the `diagnostics.Count > options.LossyFallbackErrorThreshold` branch.
  - Fix → Replace the direct `new ParseResult(tree, diagnostics, ParseMode.Lossy)` return with an actual call to `ParseLossy(source, options)` (merging diagnostics if needed), or keep `ModeUsed = ParseMode.Recovering` and change the warning text if no reparse is intended.

- Issue → No additional structural problems are evident in the provided AST, visitor, walker, or `AstBuilder` snippets beyond the pipeline issue above.
  - Location → N/A
  - Fix → N/A