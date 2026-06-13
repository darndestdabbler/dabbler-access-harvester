Verified. Both follow-up fixes are correct and scoped as intended.

Fix 1 is sound:
- The new predicate prevents the generic AST `"Select"` shape from overwriting a more-specific DAO query type like `"MakeTable"`, eliminating the consumer-facing contradiction.
- It preserves the F-3-7 refinement behavior because non-generic AST refinements still win: `"Crosstab"` and `"Union"` continue to override DAO `"Select"` when present.
- Normal `"Select"` remains unchanged (`DAO Select` + `AST Select` => `"Select"`).
- Parse failure / no mapped AST type still falls back to the DAO type (`astType == null` => `schemaQueryType`).

Fix 2 is also sound:
- The broadened regex now correctly accepts valid T-SQL numeric literals `.5`, `1.`, `-.25`, and `1.2e10`.
- It remains tightly anchored and only permits sign/digits/decimal/exponent structure, so it still rejects the listed bad inputs: `1; DROP`, `abc`, `1,234`, `0x1F`, `1D2`, `.`, `1.2.3`.
- So this removes false degradations without opening an injection path.

The added tests match the intended behavior, and 835/835 passing is consistent with these changes being additive and golden-neutral for HomeHealthCare.

```json
{"verdict":"VERIFIED","issues":[]}
```