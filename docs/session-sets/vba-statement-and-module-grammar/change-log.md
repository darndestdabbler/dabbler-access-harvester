### VBA Statement and Module Grammar (vba-statement-and-module-grammar)

**Purpose:** Extended `Vba.g4` to include comprehensive statement and module-level grammar rules. This is Phase 1.4 of the VBA Parser.

**Key Features & Design Decisions:**

*   **Statement Grammar:** Implemented rules for control flow (`If`/`For`/`Do`/`While`/`Select`/`With`), error handling (`On Error`), transfers (`GoTo`/`GoSub`/`Return`/`Exit`/`Stop`/`End`), `RaiseEvent`, local declarations (`Dim`/`Static`/`ReDim`/`Const`), and assignment (`Set`/`Let`/`Call`).
*   **Module Grammar:** Incorporated `Option` directives, `Implements`, module-level variables (tightening `WithEvents` to `typeName` only, `As` required, no `New`/array), module constants, `Type`/`Enum` definitions, `Declare`/`PtrSafe`, `Public Event` (required parameters only), and `Sub`/`Function`/`Property` declarations (split by shape).
*   **Parameter List Enforcement:** `parameterList` now enforces required parameters before optional, `ParamArray` last, and default values only on `Optional` parameters.
*   **Refinements & Tightening:**
    *   `preprocessorLine` integrated into both `moduleItem` and `statementListBody`.
    *   Restricted `localDeclHead` to `DIM` or `STATIC` within procedure bodies.
    *   Split `typeSpec`/`variableTypeSpec` to permit `As New` only in variable declarations, not in parameters or function returns.

**Testing:**
A total of **405** grammar tests were developed and passed, including 20+ Reject tests to ensure over-acceptance issues were addressed.

**Unblocks:** `vba-grammar-validation` (#5).

---

**Next session-set recommendation (after vba-statement-and-module-grammar):**
`vba-grammar-validation`

**Recommended orchestrator for `vba-grammar-validation`:** claude-code Opus 4.7 @ effort=medium
**Rationale:** The `vba-grammar-validation` set has one synthesis session (edge-case-driven grammar fix loop) that requires strong analytical capability to identify corpus failures and design targeted fixtures. Sonnet can handle the mechanical sessions, but the synthesis session warrants Opus to avoid re-verification ping-pong on novel fixture design.

*Note: The router recommended Opus 4.6; updated to 4.7 (the current model ID per project-guidance.md model table).*