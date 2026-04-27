# Verification Round 1

### Review finding: Critical

*None.*

---

### Review finding: Major

**ID:** `MAJ-2.1`
**Issue:** The test suite provides a false sense of security regarding visitor dispatch correctness. The reflective test `VbaSyntaxWalkerTests.Walker_HasVisitMethodForEveryConcreteAstNodeType` confirms that a `Visit[TypeName]` method exists for every concrete AST node type, but it does not confirm that the central `VbaSyntaxWalker.Visit` dispatch `switch` actually *calls* it. A new node type could be added, the corresponding `Visit` method created (passing the test), but if the `switch` is not updated, the call will fall through to the `default` case, silently bypassing the specific visitor method. This undermines the primary contract of the visitor pattern.
â†’ **Location:** `tests/AccessHarvester.Vba.Parser.Tests/Ast/VbaSyntaxWalkerTests.cs:18`
â†’ **Fix:** The test `Walker_HasVisitMethodForEveryConcreteAstNodeType` is insufficient. It must be augmented or replaced with a test that verifies the dispatch mechanism itself. One valid approach is to create a mock walker that records which specific `Visit[TypeName]` override was called, then instantiate one of every concrete node type and pass it to the main `Visit` method, asserting that the correct override was invoked each time. Alternatively, remove the `default:` case from the `switch` in `VbaSyntaxWalker.Visit` and resolve the resulting uncovered switch arms. While the compiler won't issue a warning for a non-exhaustive switch on a class hierarchy, this change would make the reflective test's failure more immediately actionable. The current implementation is fragile.

---

### Review finding: Minor

**ID:** `MIN-2.1`
**Issue:** Several AST node types that represent a named identifier (`Parameter`, `ForStatement`, `ForEachStatement`) include a `TypeSuffix` field (e.g., `string? TypeSuffix`, `string? LoopVariableTypeSuffix`) in the code to capture VBA's type hint characters (e.g., `%`, `&`, `$`). This is a correct and necessary feature for semantic analysis. However, the design document omits this field from the specification for these nodes, while correctly including it for others (`IdentifierExpression`). This creates a discrepancy between the design contract and the implementation.
â†’ **Location:** `docs/session-sets/parser-infrastructure/ast-shape.md`, section "4. Node hierarchy"
â†’ **Fix:** Update the `ast-shape.md` document to include the type suffix field on the affected node types to match the implementation.
    - `Parameter` should list `string? TypeSuffix`.
    - `ForStatement` should list `string? LoopVariableTypeSuffix`.
    - `ForEachStatement` should list `string? LoopVariableTypeSuffix`.

---

### Review finding: Info / nit

**ID:** `NIT-2.1`
**Issue:** The `AstBuilder` correctly implements `PreprocessorDirective` as a `ModuleItem`, per the design document. A consequence of this is that preprocessor directives inside a procedure body (which consists of `Statement` nodes) cannot be represented in the AST. The builder correctly handles this by dropping them, with a comment noting this is a Session 3 concern.
â†’ **Location:** `AccessHarvester.Vba.Parser/Ast/AstBuilder.cs:417` (in `BuildStatementList`)
â†’ **Fix:** No action required for this session. This is an advisory note for Session 3, which will need to decide how to represent these directives. Options include introducing a `PreprocessorStatement : Statement` node type or finding another mechanism. The current implementation is correct per the Session 2 contract.

---

### Verification result

ISSUES_FOUND
- `MAJ-2.1`

---

# Verification Round 2

VERIFIED
