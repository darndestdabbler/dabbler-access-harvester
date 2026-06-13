# S2 External Verification (Devil's Advocate Review)

## H1 (S2-1, ambiguity): Admitting `contextualKeywordName` into `primary` causes ambiguity
* **Verdict:** `OVERTURNED`
* **Evidence:** ANTLR4's ALL(*) lookahead seamlessly resolves the overlapping token sequences. Test scenarios like `ReDim Preserve(10)` vs `ReDim Preserve a(10)` resolve correctly: for `Preserve(10)`, the prediction mechanism sees that consuming `PRESERVE` as the optional modifier expects an upcoming identifier for `redimDecl`, which encounters `(` and fails. It therefore correctly takes the path skipping the modifier and consuming `PRESERVE` as `contextualKeywordName`. Similarly, `For step = 1 To 10 Step step` parses accurately because the optional `STEP` modifier requires a sequence that distinguishes it from an expression.  No concrete ambiguity exists.
* **Severity:** Observation
* **Recommended action:** None; the LL(*) parser handles this exact structure securely.

## H2 (S2-2, error-swallow): `HasErrorNode(item)` hides a real builder bug
* **Verdict:** `UPHELD`
* **Evidence:** `item` here represents the entirety of `moduleItem` (which encapsulates structures as massive as `procedureDeclaration` with its full `statementListBody`). `HasErrorNode` is a deep recursive check (`GetChild(i)`). This creates a massive blind spot: if a developer writes `Sub MyProc()` with a genuine syntax error on line 5 (e.g. `x = 1 ! 2` generating an `ErrorNode`), but the AST builder subsequently throws an `ArgumentNullException` or `IndexOutOfRange` on line 50 due to a legitimate bug in parsing array bounds, `catch (Exception)` intercepts the crash. Because `HasErrorNode` finds the unrelated syntax error on line 5, the true `Exception` stack trace is swallowed and the whole procedure is silently masked as an `UnknownModuleItem`.
* **Severity:** Major
* **Recommended action:** Do not use a deep-recursive error node assessment as a blanket alibi for `catch (Exception)`. Restrict the check to shallow error nodes near the procedure's definition head to identify when shape mangling occurred, or better, catch explicitly the NREs that ANTLR generates on mangled nodes rather than `Exception`.

## H3 (S2-1 scope): Contextual-keyword scope is too narrow
* **Verdict:** `UPHELD`
* **Evidence:** The author added `contextualKeywordName` to `primary` and `variableDecl`, but hardcoded `IDENTIFIER` remains in numerous structural slots. Concrete constructs that now throw fatal syntax errors because they no longer lex as standard identifiers:
   1. `Call MyFunc(Step:=1)` fails (rule `namedArgument` expects `IDENTIFIER COLON_EQ`).
   2. `Sub Step()` fails (rule `subDeclaration` expects `... SUB IDENTIFIER`).
   3. `Declare Function Compare Lib "x"` fails (`declareStatement` expects `IDENTIFIER`).
   4. `Type Info \n Step As String \n End Type` fails (`typeMember` expects `(IDENTIFIER | TYPED_IDENTIFIER)`).
   5. `Compare:` label fails (`statementLabel` expects `IDENTIFIER COLON`).
   6. `Enum \n Step = 1 \n End Enum` fails (`enumMember` expects `IDENTIFIER`).
* **Severity:** Critical
* **Recommended action:** Extend the parameter slot `(IDENTIFIER | TYPED_IDENTIFIER | contextualKeywordName)` replacement to cover `namedArgument`, `procedureDeclaration` forms (sub/function/props), `declareStatement`, `typeMember`, `enumMember`, and `statementLabel`.

## H4 (S2-4): Capturing TypeSuffix without emitting it causes issues
* **Verdict:** `UPHELD`
* **Evidence:** By merely splitting `$` into `AST.ProcedureDeclaration.TypeSuffix` (and `Name`), the pipeline abandons the type signature. Specifically: `ScopeBuildingVisitor.cs` reads `n.Name` and `n.ReturnType` (which is `null`) to build SymbolDb's `FunctionDeclaration`. It ignores `TypeSuffix`. Subsequently, `TypeInferencePass.cs` entirely overlooks procedure declarations in `TypeSuffixEvidence()` (it returns `_ => (null, null)` for procedures). Finally, `ParserOutputProjector.cs` invokes `BuildSignature(..., proc.ReturnType)`, serializing `null` as the return type. An AI downstream reading the JSON will incorrectly deduce `Function SqlStr$()` returns Variant, not String.
* **Severity:** Critical
* **Recommended action:** Implement Type Inference from procedure `TypeSuffix` in `TypeInferencePass.cs`, or alternatively enact the approach outlined in H5.

## H5 (better strategy): Overlooked architectural approach
* **Verdict:** `UPHELD`
* **Evidence:** Creating a standalone `TypeSuffix` string property across `VariableDeclarator`/`ProcedureDeclaration` and injecting it down to SymbolDb fundamentally misunderstands what the type suffix is: syntactic sugar for `As <Type>`. Pushing this lexical artifact down the pipeline led straight to the bug identified in H4. A materially simpler approach is to handle it strictly within `AstBuilder`: when stripping `$` from a `TYPED_IDENTIFIER`, `AstBuilder` should immediately populate `ReturnType` (or `Type` for variables) with a Built-in `TypeReference("String")`. This completely eliminates the need for `TypeSuffix` properties on the AST, shields the resolver entirely from needing to care about syntactic characters, and ensures `ParserOutputProjector` emits the accurate return signature seamlessly.
* **Severity:** Major
* **Recommended action:** Drop `TypeSuffix` from `ProcedureDeclaration`. Have `AstBuilder` map any stripped typ-declaration characters (`$`/`&`/`%`...) directly into a populated `TypeReference`, leaving the downstream SymbolDb and JSON projector agnostic of the legacy shorthand.

---
**Overall:** This change set is **NOT safe** to build further work upon. The partial deployment of `contextualKeywordName` completely breaks several standard statement elements (such as named arguments and labels leveraging those keywords), and the abstraction leakage of `TypeSuffix` drops deterministic return types from the compiled intermediate-representation JSON. H3 and H4 MUST be fixed before locking any golden test.