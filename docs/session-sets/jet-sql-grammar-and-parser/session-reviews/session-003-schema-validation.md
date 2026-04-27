## VERDICT: ISSUES_FOUND

---

### Issue 1: `AllFlags` indexing is internally inconsistent across the two UNION tests

- **Severity:** Major
- **Location:** `SqlAstBuilderShapeTests.cs` — `Union_TwoSelects_BuildsUnionStatementWithTwoSelects` and `UnionAll_SecondSelectHasAllFlagTrue`
- **Issue:** The two tests assume contradictory indexing schemes for `AllFlags`.

  | Design | `AllFlags` length for 2 selects | Valid indexes |
  |---|---|---|
  | Operator-indexed (1 flag per operator) | 1 | `[0]` only |
  | Select-indexed (parallel to `Selects`) | 2 | `[0]`, `[1]` |

  `Union_TwoSelects` asserts `AllFlags[0]` is `false` with comment `// UNION (not UNION ALL)`. Under **select-indexed** design, `AllFlags[0]` is vacuous — it represents "was there a UNION ALL *before* the first select", which is always `false` regardless of union type. The comment is therefore wrong: the assertion does not test whether the operator is UNION vs UNION ALL.

  `UnionAll_SecondSelectHasAllFlagTrue` asserts `AllFlags[1]` is `true`. Under **operator-indexed** design, `AllFlags` has length 1 for a 2-select union; `AllFlags[1]` throws `IndexOutOfRangeException`.

  Only one interpretation can be correct at runtime, but neither test is self-consistent with the other's implicit assumption.

- **Fix:** Choose one design and apply it consistently. Under select-indexed (most likely, given `AllFlags[1]` in the second test):

  ```csharp
  // Union_TwoSelects — tests the actual operator between the two selects
  Assert.Equal(2, union.Selects.Count);
  Assert.False(union.AllFlags[1]); // operator between Selects[0] and Selects[1] is UNION, not UNION ALL

  // UnionAll_SecondSelectHasAllFlagTrue — correct as-is
  Assert.True(union.AllFlags[1]);
  ```

  Under operator-indexed:
  ```csharp
  // Union_TwoSelects
  Assert.Equal(1, union.AllFlags.Count);
  Assert.False(union.AllFlags[0]);

  // UnionAll_SecondSelectHasAllFlagTrue
  Assert.True(union.AllFlags[0]); // only one operator
  ```

---

### Issue 2: Nested JOIN structure assertion contradicts the SQL grammar

- **Severity:** Major
- **Location:** `SqlAstBuilderShapeTests.cs` — `FromClause_NestedJoins_Fixture04_HasLeftAndInnerJoin`, lines asserting `Assert.Empty(outer.Joins)` and `Assert.Single(middle.Joins)`
- **Issue:** The SQL is `FROM ((A LEFT JOIN B) INNER JOIN C)`. The outer parens syntactically enclose `(A LEFT JOIN B) INNER JOIN C`, making the INNER JOIN a constituent of the outer parenthesized expression, not the inner one. The test asserts `outer.Joins` is empty and `middle.Joins` contains the INNER JOIN, which places the join at the wrong structural level. If the parser models joins as attached to the left-side source of the operator, the INNER JOIN belongs to `outer`, not `middle`.

  ```
  // SQL structure:
  // OUTER_PAREN( MIDDLE_PAREN(tblCases LEFT JOIN cw) INNER JOIN cl )
  //                                                  ^^^^^^^^^^^
  //                           this join is lexically inside outer, outside middle
  ```

- **Fix:** Verify against the actual parser output. If the parser correctly represents syntactic grouping:

  ```csharp
  var outer = Assert.IsType<ParenthesizedTableSource>(select.From!.Sources.Single());
  Assert.Single(outer.Joins);                          // INNER JOIN is at outer level
  Assert.Equal(JoinKind.Inner, outer.Joins[0].Kind);

  var middle = Assert.IsType<ParenthesizedTableSource>(outer.Inner);
  Assert.Empty(middle.Joins);                          // middle paren is transparent

  var innerSrc = Assert.IsType<SimpleTableSource>(middle.Inner);
  Assert.Single(innerSrc.Joins);
  Assert.Equal(JoinKind.Left, innerSrc.Joins[0].Kind);
  ```

  If the parser instead attaches trailing joins to the preceding source node, the test comment must be corrected and the contradiction with standard parse tree structure documented explicitly.

---

### Issue 3: Hardcoded diagnostic code `SQL0050` with no spec backing

- **Severity:** Minor
- **Location:** `JetSqlParserPipelineTests.cs` — `Lossy_CleanInput_ReportsLossyWarning`
- **Issue:** The assertion `d.Code == "SQL0050"` is never established in the spec or any referenced document. For clean input in Lossy mode, it is not obvious that the pipeline must emit any diagnostic at all, let alone one with this specific code. If Lossy mode is defined as "silently drop parse errors", a clean input would produce no diagnostics, and `Assert.Contains` would fail.

  ```csharp
  Assert.Contains(result.Diagnostics,
      d => d.Code == "SQL0050" && d.Severity == DiagnosticSeverity.Warning);
  ```

- **Fix:** Either establish `SQL0050` in the diagnostic code registry and the spec, or change the assertion to verify the semantic contract (e.g., mode downgrade happened, or no exception was thrown):

  ```csharp
  Assert.Equal(ParseMode.Lossy, result.ModeUsed);
  Assert.NotNull(result.ParseTree);
  // Remove the diagnostic code assertion unless SQL0050 is formally defined
  ```

---

### Issue 4: `TypeKeyword` case normalization is an unverified assumption

- **Severity:** Minor
- **Location:** `SqlAstBuilderShapeTests.cs` — `ParametersDeclaration_TwoFormReferenceParams_BuiltCorrectly`
- **Issue:** The grammar input is `Long` and `DateTime` (mixed case). The assertions check for `"LONG"` and `"DATETIME"` (uppercase). Case normalization is a parser implementation detail not stated in the spec. If the AST builder preserves the lexer token text, these assertions will fail.

  ```csharp
  Assert.Equal("LONG", ast.Parameters.Parameters[0].TypeName.TypeKeyword);
  Assert.Equal("DATETIME", ast.Parameters.Parameters[1].TypeName.TypeKeyword);
  ```

- **Fix:** Either confirm normalization is specified and tested, or use case-insensitive comparison:

  ```csharp
  Assert.Equal("LONG", ast.Parameters.Parameters[0].TypeName.TypeKeyword,
      StringComparer.OrdinalIgnoreCase);
  Assert.Equal("DATETIME", ast.Parameters.Parameters[1].TypeName.TypeKeyword,
      StringComparer.OrdinalIgnoreCase);
  ```

---

### Issue 5: `ThrowsAny<Exception>` is overly permissive

- **Severity:** Minor
- **Location:** `JetSqlParserPipelineTests.cs` — `Strict_MalformedInput_Throws` and `Strict_SourcePath_AppearsInExceptionMessage`
- **Issue:** `ThrowsAny<Exception>` catches `OutOfMemoryException`, `StackOverflowException`, or any unexpected internal failure, masking bugs as passes. Strict mode should throw a defined type (e.g., `SqlParseException`).

- **Fix:**
  ```csharp
  Assert.Throws<SqlParseException>(() =>
      SqlParserPipeline.Parse(MalformedInput,
          new ParseOptions { RequestedMode = ParseMode.Strict }));
  ```

---

### Issue 6: Missing coverage gaps

- **Severity:** Minor (coverage)
- **Location:** `JetSqlParserPipelineTests.cs`
- **Issue:** Three gaps relative to the spec:
  1. No Lossy mode test for `ParseFragment` (spec requires Strict/Recovering/Lossy coverage of the new API).
  2. No test for `ParseFragment` with malformed input in Recovering mode — the recovery contract for fragments is unverified.
  3. `ParseFragment_Fixture05_StatusInFilter` uses a simplified WHERE (no `1=1 AND …` prefix), which means the `Fixture05AssembledSql` constant's compound WHERE (`1=1 AND c.Status IN …`) is never exercised as an AND expression in the shape tests.

- **Fix:** Add:
  ```csharp
  [Fact]
  public void ParseFragment_Lossy_CleanInput_ReturnsResult()
  {
      var result = SqlParserPipeline.ParseFragment(Fixture05AssembledSql,
          new ParseOptions { RequestedMode = ParseMode.Lossy });
      Assert.Equal(ParseMode.Lossy, result.ModeUsed);
      Assert.NotNull(result.ParseTree);
  }

  [Fact]
  public void ParseFragment_Recovering_MalformedInput_ProducesDiagnosticsNotNull()
  {
      var result = SqlParserPipeline.ParseFragment("SELECT @@@ FROM",
          new ParseOptions { RequestedMode = ParseMode.Recovering });
      Assert.NotEmpty(result.Diagnostics);
      Assert.NotNull(result.ParseTree);
  }

  [Fact]
  public void ParseFragment_Fixture05_CompoundWhere_BuildsAndExpression()
  {
      const string sql =
          "SELECT c.CaseID FROM tblCases AS c " +
          "WHERE 1=1 AND c.Status IN ('Open', 'Pending Review') " +
          "ORDER BY c.CaseNumber";
      var ast = BuildFragmentAst(sql);
      var select = Assert.IsType<SelectCore>(
          Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
      var and = Assert.IsType<BinaryExpression>(select.Where);
      Assert.Equal(BinaryOperator.And, and.Operator);
  }
  ```

---

### Summary

| # | Severity | File | Test / Line | Root Cause |
|---|---|---|---|---|
| 1 | **Major** | ShapeTests | `Union_*` pair | `AllFlags` index 0 vs 1 inconsistency — one test is vacuous or one throws at runtime |
| 2 | **Major** | ShapeTests | `FromClause_NestedJoins_*` | INNER JOIN placed on `middle` not `outer`, contradicting SQL grouping |
| 3 | Minor | PipelineTests | `Lossy_CleanInput_ReportsLossyWarning` | Hardcoded `SQL0050` unspecified; assertion may always fail on clean input |
| 4 | Minor | ShapeTests | `ParametersDeclaration_TwoFormRef*` | Case normalization of `TypeKeyword` assumed without spec support |
| 5 | Minor | PipelineTests | `Strict_MalformedInput_Throws` | `ThrowsAny<Exception>` too broad |
| 6 | Minor | PipelineTests | — | Missing Lossy/malformed coverage for `ParseFragment`; compound WHERE untested |