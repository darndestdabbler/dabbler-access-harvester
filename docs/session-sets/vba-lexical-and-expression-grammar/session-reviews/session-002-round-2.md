## ISSUES_FOUND

- **Issue:** `procedureReference` still accepts invalid bare `AddressOf Me`, so the `AddressOf` operand restriction is not fully closed.  
  **Severity:** Minor  
  **Location:** `AccessHarvester.Vba.Grammar/Vba.g4` — `procedureReference`, `procedureRefHead`  
  **Fix:** Disallow `ME` as a complete procedure reference; require at least one qualified member after `Me`. For example:
  ```antlr
  procedureReference
      : (IDENTIFIER | TYPED_IDENTIFIER) (DOT memberName)*
      | ME DOT memberName (DOT memberName)*
      ;
  ```
  Add a negative parse-failure test for `AddressOf Me` in `tests/AccessHarvester.Vba.Grammar.Tests/Expressions/VbaExpressionParserTests.cs`.