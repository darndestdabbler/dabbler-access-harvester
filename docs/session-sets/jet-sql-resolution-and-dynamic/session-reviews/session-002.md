# Verification Round 1

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Sql.Parser/DynamicSql/DynamicSqlAnalyzer.cs",
      "summary": "String parameters are added to the tracked-variable set but never seeded into the environment, so any SQL concatenation that uses a procedure parameter is misclassified as `unbound-variable` instead of a runtime-unknown hole.",
      "fix": "Initialize string parameters in `Analyze` with a dedicated symbolic unknown/parameter value, or otherwise treat missing parameter identifiers as unknown rather than unbound so parameter-derived SQL becomes wildcarded instead of falsely flagged as missed dataflow."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Sql.Parser/DynamicSql/DynamicSqlAnalyzer.cs",
      "summary": "The `combinatorial-blowup` safeguard runs only after `Flatten` has already materialized the full branch cross-product, so large dynamic-SQL spaces can consume excessive time or memory before the analyzer marks the site unanalyzable.",
      "fix": "Refactor flattening to be cap-aware and lazy: compute cardinality incrementally, stop expanding once `MaxFullEnumeration` is exceeded, and gather representative samples without constructing every leaf."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Sql.Parser/DynamicSql/DynamicSqlAnalyzer.cs",
      "summary": "Loop bodies currently mutate the shared environment as if they executed exactly once, which can fabricate post-loop SQL states instead of conservatively routing loop-driven mutation to manual review.",
      "fix": "When a loop can write tracked SQL variables, scan the body for nested execution calls but invalidate or mark affected bindings unanalyzable after the loop rather than propagating one-iteration state."
    }
  ]
}
```

---

# Verification Round 2

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "AccessHarvester.Sql.Parser/DynamicSql/DynamicSqlAnalyzer.cs",
      "summary": "Loop post-state invalidation relies on `SymbolicSqlValue.Equals`, but `SqlConcat`/`SqlBranches` and hole records contain list and AST-reference fields, so semantically unchanged SQL values are often treated as changed and replaced with `<loop-mutation:...>` after the loop.",
      "fix": "Add an explicit deep comparer for `SymbolicSqlValue` that sequence-compares `SqlConcat.Parts` / `SqlBranches.Alternatives` and compares hole payloads by semantic fields, then use that comparer in `WalkLoopBody` (and preferably `AllAlternativesEqual` as well) instead of record `Equals`."
    }
  ]
}
```
