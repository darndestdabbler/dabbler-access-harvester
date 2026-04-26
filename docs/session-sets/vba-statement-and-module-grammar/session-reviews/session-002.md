# Verification Round 1

- **Severity:** Major  
  **Category:** Correctness  
  **Location:** `AccessHarvester.Vba.Grammar/Vba.g4` — `propertyDeclaration`  
  **Finding:** `propertyDeclaration` collapses `Property Get`, `Property Let`, and `Property Set` into one shape, so it accepts syntactically invalid forms such as `Property Let X()`, `Property Let X(...) As String`, and `Property Set X(...) As Object`. In VBA, only `Property Get` may have a trailing `As <type>` clause, and `Let`/`Set` require at least one value parameter.  
  **Suggested fix:** Split the rule into accessor-specific forms: `propertyGetDeclaration`, `propertyLetDeclaration`, and `propertySetDeclaration`. Keep triplet pairing as downstream work, but enforce grammar-level constraints that `Let`/`Set` have a non-empty parameter list and no trailing return-type clause.

- **Severity:** Major  
  **Category:** Correctness  
  **Location:** `AccessHarvester.Vba.Grammar/Vba.g4` — `parameterList`, `parameter`  
  **Finding:** The parameter grammar accepts a superset of VBA syntax and misses the hazard-list requirements for `ParamArray`/optional defaults. It currently allows illegal forms such as `Optional ParamArray args() As Variant`, `ByVal ParamArray args() As Variant`, `ParamArray args As Variant` (missing `()`), non-final `ParamArray`, and `x As Long = 1` without `Optional`. H-E07 explicitly calls for rejecting conflicting `ParamArray` combinations.  
  **Suggested fix:** Model parameter kinds explicitly. Example: `parameterList : regularParameter (COMMA regularParameter)* (COMMA paramArrayParameter)? | paramArrayParameter ;`. Make `paramArrayParameter` require `PARAMARRAY name LPAREN RPAREN`, disallow `Optional`/`ByVal`/`ByRef`/default, and restrict defaults to an `optionalParameter` alternative only.

- **Severity:** Major  
  **Category:** Correctness  
  **Location:** `AccessHarvester.Vba.Grammar/Vba.g4` — `moduleVariableDeclItem` + `typeSpec`  
  **Finding:** `WithEvents` declarations can now parse `As New ...` because `moduleVariableDeclItem` uses `typeSpec`, and `typeSpec` was broadened to `NEW? typeName`. This admits illegal syntax such as `Private WithEvents m_Form As New Form`, which directly contradicts H-F01 (`WithEvents` cannot combine with `New`).  
  **Suggested fix:** Split `moduleVariableDeclItem` into `withEventsVariableDeclItem` and `normalVariableDeclItem`, or use a separate `noNewTypeSpec` in the `WITHEVENTS` branch so `WithEvents` declarations cannot consume `NEW`.

- **Severity:** Major  
  **Category:** Correctness  
  **Location:** `AccessHarvester.Vba.Grammar/Vba.g4` — `publicEventDeclaration`; `tests/AccessHarvester.Vba.Grammar.Tests/Modules/VbaModuleParserTests.cs` — `Event_PrivateModifier`  
  **Finding:** The grammar and tests both accept `Private Event ...`, but the session spec/comments describe VBA event declarations as `[Public] Event ...`. `Private Event` is not part of the VBA `Event` declaration syntax, so the rule is over-permissive and the test locks the bug in.  
  **Suggested fix:** Change the rule to `PUBLIC? EVENT IDENTIFIER LPAREN parameterList? RPAREN` (or just `EVENT ...` if you want the optional `Public` to be implicit). Replace `Event_PrivateModifier` with a rejection test.

- **Severity:** Minor  
  **Category:** Coverage  
  **Location:** `tests/AccessHarvester.Vba.Grammar.Tests/Modules/VbaModuleParserTests.cs` — module test suite overall  
  **Finding:** The new suite is almost entirely acceptance-only and does not exercise the syntax exclusions that matter most in this section. As a result, the over-acceptance bugs above are not caught, and one invalid form (`Private Event`) is positively asserted.  
  **Suggested fix:** Add negative parser tests for at least: `Private Event Internal()`, `Private WithEvents m_Form As New Form`, `Public Property Let X()`, `Public Property Set X(ByVal v As Object) As Object`, `Sub S(Optional ParamArray args() As Variant)`, and `Sub S(ParamArray args As Variant)`.

`VERDICT: ISSUES_FOUND (0 Critical, 4 Major)`

---

# Verification Round 2

## R1 fix verification

- **Issue** → R1-1: invalid `Property Let/Set` forms with `As` clause or zero params  
  **Location** → `propertyGetDeclaration`, `propertyLetDeclaration`, `propertySetDeclaration`  
  **Fix** → **Confirmed.** Splitting the accessor forms correctly removes `As <type>` from `Let/Set` and makes `parameterList` mandatory for `Let/Set`, so `Property Let X()`, `Property Let X() As Long`, and `Property Set X() As Foo` no longer parse. **Broader test to add:** `Reject_PropertySet_NoParams`.

- **Issue** → R1-2: `parameterList` over-permissive on `ParamArray` and default-value combinations  
  **Location** → `parameterList`, `requiredParameter`, `optionalParameter`, `paramArrayParameter`  
  **Fix** → **Confirmed for the Round 1 defect.** The new shape correctly enforces:
  - default values only on `optionalParameter`
  - `ParamArray` requires `()`
  - `ParamArray` cannot combine with `Optional` / `ByVal` / `ByRef`
  - `ParamArray` is trailing-only  
  **Broader tests to add:** `Reject_ByRefParamArrayCombination`, `Reject_ParamArray_DefaultValue`.

- **Issue** → R1-3: `WithEvents` declarations could parse `As New T`  
  **Location** → `moduleVariableDeclItem`  
  **Fix** → **Confirmed for the Round 1 defect.** The `WithEventsVarItem` alternative now requires `As`, uses `typeName` instead of full `typeSpec`, and structurally excludes `New`, array declarators, and fixed-length-string forms. **Broader tests to add:** `Reject_WithEvents_Array`, `Reject_WithEvents_FixedLengthString`.

- **Issue** → R1-4: `publicEventDeclaration` accepted `Private Event`  
  **Location** → `publicEventDeclaration`  
  **Fix** → **Confirmed.** `PUBLIC? EVENT ...` rejects `Private Event` at the grammar layer. **Broader test to add:** `Reject_FriendEvent`.

- **Issue** → R1-5: test suite lacked negative coverage for over-acceptance bugs  
  **Location** → grammar tests  
  **Fix** → **Confirmed.** The added `Reject_*` cases materially improve regression coverage for all Round 1 findings. Coverage is still narrow around a few adjacent shapes noted above.

## New findings

- **Issue** → **Major:** `parameterList` still accepts required parameters after an optional parameter  
  **Location** → `parameterList` / `parameterEntry`  
  **Fix** → Replace the mixed `parameterEntry*` form with an ordered structure: required-parameter prefix, then optional-parameter suffix, then optional trailing `ParamArray`. Example invalid form still accepted today: `Sub F(Optional x As Variant = Empty, y As Long)`. Add `Reject_RequiredAfterOptional`.

- **Issue** → **Major:** `publicEventDeclaration` still over-accepts illegal event-parameter modifiers by reusing `parameterList`  
  **Location** → `publicEventDeclaration`  
  **Fix** → Introduce a dedicated `eventParameterList` / `eventParameter` that excludes `OPTIONAL`, `PARAMARRAY`, and therefore default values. Example invalid forms still accepted today: `Public Event E(Optional x As Variant)` and `Public Event E(ParamArray a())`. Add `Reject_EventOptionalParameter` and `Reject_EventParamArray`.

`VERDICT: ISSUES_FOUND (0 Critical, 2 Major)`

---

# Verification Round 3

- Confirmed → `parameterList` / `positionalParameters` / `requiredParameter` / `optionalParameter` / `paramArrayParameter` → No fix. The revised shape correctly enforces required-before-optional ordering, keeps `ParamArray` last-only, and blocks defaults on non-`Optional` parameters at the grammar layer.

- Confirmed → `publicEventDeclaration` / `eventParameterList` / `eventParameter` → No fix. The dedicated event-parameter rule correctly excludes `Optional`, `ParamArray`, and default values, while the declaration rule continues to exclude non-`Public` access modifiers such as `Friend`.

- Remaining structural correctness issue → None found in the reviewed R2 surface → No fix.

VERDICT: VERIFIED
