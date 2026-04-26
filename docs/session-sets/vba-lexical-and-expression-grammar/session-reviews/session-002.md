## ISSUES_FOUND

- **Issue:** Over-broad reuse of `lExpression` makes `TypeOf` and `AddressOf` accept invalid operands  
  **Severity:** Major  
  **Location:** `AccessHarvester.Vba.Grammar/Vba.g4` — rules `typeOfExpr`, `addressOfExpr`, `lExpression`  
  **Fix:** `lExpression` currently bottoms out in `primary`, so it accepts literals, special constants, and parenthesized arbitrary expressions. That means invalid forms parse, including `TypeOf 1 Is TextBox`, `TypeOf "x" Is Foo`, `AddressOf MyHandler()`, `AddressOf Forms!frm`, and `AddressOf .Handler`. Split the broad postfix/member/call rule from the restricted reference forms:
  ```antlr
  typeOfExpr
      : TYPEOF referenceExpression IS typeName
      ;

  addressOfExpr
      : ADDRESSOF procedureReference
      ;

  referenceExpression
      : referenceExpression DOT memberName
      | referenceExpression BANG memberName
      | referenceExpression LPAREN argumentList RPAREN
      | DOT memberName
      | BANG memberName
      | ME
      | IDENTIFIER
      | TYPED_IDENTIFIER
      | LPAREN referenceExpression RPAREN
      ;

  procedureReference
      : (IDENTIFIER | TYPED_IDENTIFIER)
        (DOT (IDENTIFIER | TYPED_IDENTIFIER))*
      ;
  ```
  Keep the current broad postfix rule for ordinary expression parsing only.

- **Issue:** No negative tests cover the invalid `TypeOf`/`AddressOf` forms the grammar should reject  
  **Severity:** Minor  
  **Location:** `tests/AccessHarvester.Vba.Grammar.Tests/Expressions/VbaExpressionParserTests.cs`  
  **Fix:** Add parse-failure tests for the cases the current grammar wrongly accepts:
  - `TypeOf 1 Is TextBox`
  - `TypeOf "x" Is Foo`
  - `AddressOf MyHandler()`
  - `AddressOf Forms!frm`
  - `AddressOf .Handler`