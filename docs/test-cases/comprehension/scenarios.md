# VBA Parser Test Coverage Checklist

A consolidated checklist of test case categories for a VBA parser targeting Microsoft Access codebases. Organized to support gap analysis against an existing test suite.

## Priority guidance for Layer C question drafting

When this file is consumed by `tools/comprehension-bench/draft_questions.py` to generate AI-comprehension questions, the section weighting (highest → lower) is:

1. **§1 Form and Control Reference Patterns** — highest priority
2. **§2 Expression-Level Constructs** — highest priority
3. **§3 Syntactic Edge Cases** — highest priority
4. §4–§7 are background context — draw from them only after §1–§3 coverage is exhausted

The drafter should bias the ~20 questions per DB so that the majority exercise patterns from §1–§3, with the remaining slots covering §4–§7 only where the gold-standard DBs naturally surface them.

## Scope notes

This checklist assumes the parser may need to handle VBA code appearing in three distinct execution contexts:

1. **VBA modules** — standard modules, form/report class modules, custom class modules
2. **Expression contexts** — `ControlSource`, `ValidationRule`, `DefaultValue`, query calculated columns, macro conditions (related but not identical grammar; no statements, `Forms!...` bang paths are first-class)
3. **Embedded strings** — VBA string literals containing expression-grammar fragments (e.g., `WhereCondition` arguments, `DLookup` criteria), which the parser may treat opaquely or peek into

A given problematic construct is a *coordinate* across three axes: object/scope pattern × expression construct × execution context.

---

## 1. Form and Control Reference Patterns

The full set of architecturally distinct ways VBA code reads or modifies form/control values and properties.

| # | Category | Use Case | Description |
|---|---|---|---|
| 1 | Filtering / data sourcing | Filter subform `RecordSource` / `RowSource` | Dropdowns, textboxes, checkboxes, option buttons, or list boxes are read to rebuild a subform's source (typically datasheet view). |
| 2 | Filtering / data sourcing | Filter a DAO/ADODB recordset in VBA | Same controls as #1, but VBA opens a recordset and uses it to populate a form. |
| 3 | Filtering / data sourcing | Cascading dropdowns | One combo's value rebuilds the `RowSource` of another combo. |
| 4 | Filtering / data sourcing | Datasheet drill-through to another form | Selected datasheet record's key drives the `RecordSource`/`Filter` of another (often dynamically opened) form. |
| 5 | Filtering / data sourcing | Form controls → report parameters | Control values build `DoCmd.OpenReport`'s `WhereCondition` (or set the report's `RecordSource`/`Filter`/`OrderBy`). |
| 6 | Filtering / data sourcing | Form-level `Me.Filter` from controls | Control values are concatenated into a filter string assigned to `Me.Filter` with `FilterOn = True`, leaving `RecordSource` intact. |
| 7 | Parameter passing | Values passed by value to a procedure | Control values extracted at the call site and passed as scalars to a standard VBA function/sub. |
| 8 | Parameter passing | Controls passed as `Control` objects | The control objects themselves are passed; the procedure reads `.Value` (and possibly properties) inside. |
| 9 | Parameter passing | Form reference passed | A `Form` object reference is passed; the procedure reaches into named controls through it. |
| 10 | Parameter passing | Form-backing module using `Me` | Code in the form's own module uses `Me.ControlName` to extract values for processing/updates/calculations. |
| 11 | Parameter passing | `OpenArgs` | Control values are concatenated into `OpenArgs` when opening another form/report; the receiver parses `Me.OpenArgs`. |
| 12 | Declarative expression reference | Validation rule expression | A control's `ValidationRule` contains fully qualified `Forms!Form!Control` references. |
| 13 | Declarative expression reference | Macro expression | A macro condition or argument contains `Forms!Form!Control` references. |
| 14 | Declarative expression reference | Calculated `ControlSource` | A control's `ControlSource` is an expression like `=[Quantity]*[UnitPrice]` that references other controls on the form. |
| 15 | Cross-form / hierarchy reference | Sibling-form references | Code on one open form reads/writes controls on a separately open form via `Forms!OtherForm!Control`. |
| 16 | Cross-form / hierarchy reference | Parent ↔ subform references | Subform code uses `Me.Parent.Control`; main-form code uses `Me.SubformCtl.Form.Control` to traverse hierarchy. |
| 17 | Cross-form / hierarchy reference | Runtime active references | Code uses `Screen.ActiveControl` / `Screen.ActiveForm` / `Screen.ActiveReport` with no statically resolvable target (common in ribbon callbacks and shared utility routines). |
| 18 | Event-driven property/value manipulation | Event handler reads/writes control properties | A form or control event triggers VBA that reads and/or modifies properties (`Value`, `Visible`, `Enabled`, `Locked`, `BackColor`, `DefaultValue`, etc.) on one or more controls. |
| 19 | Event-driven property/value manipulation | Multi-select listbox processing | Code iterates `lst.ItemsSelected` to build IN-clauses or process each chosen value. |
| 20 | Event-driven property/value manipulation | `Controls` collection iteration | Code loops `Me.Controls` to apply bulk property changes (e.g., lock all bound controls when in read-only mode, hide/show by `Tag`). |
| 21 | Event-driven property/value manipulation | Section-scoped iteration | Code loops `Me.Section(acDetail).Controls` (or `acHeader`, `acFooter`) to scope bulk operations to a form region. |
| 22 | Event-driven property/value manipulation | Recordset navigation by control value | Control values drive `Me.RecordsetClone.FindFirst` followed by `Me.Bookmark = rs.Bookmark` to jump the form to a specific record. |
| 23 | Event-driven property/value manipulation | `Tag`-based metadata | `.Tag` reads passed through `Split`, `InStr`, or pattern matching to drive conditional behavior across grouped controls. |
| 24 | Class-based delegation | `WithEvents` control wrappers | Custom class module declares `Private WithEvents ctl As Access.TextBox` (or similar); form's `Form_Load` instantiates the class and assigns a control reference, centralizing event handling. |
| 25 | Ribbon / extensibility | IRibbonControl callbacks | `Sub Cb(ctl As IRibbonControl, ByRef returnedVal)` signatures (and the three-arg `cancelDefault` variant); often reach back into `Screen.ActiveForm` to read form state. |
| 26 | Design-time metaprogramming | `CreateControl` / `DeleteControl` | `Application.CreateControl` calls that fabricate UI elements at runtime (only valid in Design view; rare but real). |
| 27 | Parallel reference grammar | `TempVars` references | `TempVars!name` and `TempVars("name")` parallel `Forms!...` syntax and appear in the same expression contexts. |

---

## 2. Expression-Level Constructs

These are *expression-level* and orthogonal to the patterns above. They appear inside almost any of the contexts in §1.

| # | Construct | Notes for parser / interpretation |
|---|---|---|
| E1 | `IIf(cond, truepart, falsepart)` | **Both branches are evaluated** — not short-circuiting. AST should preserve this semantic. |
| E2 | Nested `IIf` | Real Access code nests 4+ levels deep. Parser should handle without stack issues. |
| E3 | `Nz(value)` (one-arg) | Returns zero-length string when value is Null. |
| E4 | `Nz(value, default)` (two-arg) | Return type follows the second argument's type. |
| E5 | `DLookup(expr, domain, criteria)` | Three-arg and two-arg forms; criteria is a stringly-typed mini-SQL expression. |
| E6 | `DCount` / `DSum` / `DMax` / `DMin` / `DAvg` / `DFirst` / `DLast` / `DStDev` / `DVar` | Same shape as `DLookup`; all return Null on no-match (not error). |
| E7 | Domain function with control-reference criteria | `"[ID]=" & Forms!frmMain!txtID` — the parser must decide whether to peek inside the string or treat opaquely. |
| E8 | `Switch(expr1, val1, expr2, val2, ...)` | All expressions evaluated; returns value for first true. |
| E9 | `Choose(index, opt1, opt2, ...)` | Index-based selection. |
| E10 | `Eval(stringExpr)` | Runtime string-to-expression evaluation; the nuclear option. |
| E11 | Combined nesting | `Nz(DLookup(...), IIf(...))` inside a calculated `ControlSource` — combines E1, E3/E4, E5, and §1 #14. |

---

## 3. Syntactic Edge Cases

VBA-specific syntax that commonly catches hand-built parsers.

| # | Edge case | Notes |
|---|---|---|
| S1 | Line continuation `_` mid-expression | Including inside string concatenations and after operators. No implicit continuation after `,`. |
| S2 | `:` statement separators | Multiple statements on one line. |
| S3 | Bang vs. dot vs. indexed access | `Forms!frmMain!txtID` vs. `Forms!frmMain.Form.txtID` vs. `Forms("frmMain")("txtID")`. All reference the same thing; AST shape should distinguish. |
| S4 | Bracketed identifiers | `Forms![My Form]![My Control]` — names containing spaces or reserved words. |
| S5 | Default property access | `Me.txtFoo` (control object) vs. `Me.txtFoo.Value` (value). Often interchangeable; AST should record what was written. |
| S6 | Chained access precedence | `Me.sfrmChild.Form!txtX.Value` — bang and dot mixed. |
| S7 | `With ... End With` blocks | `.txtFoo` as shorthand inside `With Me`. |
| S8 | `For Each ctl In Me.Controls` | Loop variable typing inferred from collection. |
| S9 | Event handler naming convention | `Private Sub txtFoo_AfterUpdate()`, `Private Sub Form_Load()` — the underscore-separated name encodes binding. |
| S10 | `WithEvents` declarations | `Private WithEvents ctl As Access.TextBox` at module scope. |
| S11 | Late-bound calls | `CallByName(Me, "txtFoo", VbGet)`. |
| S12 | `Rem` comments | Older comment syntax alongside `'`. |
| S13 | Conditional compilation | `#If VBA7 Then` / `#If Win64 Then` / `#Else` / `#End If` — preprocessor-like; parser must decide whether to evaluate, preserve as AST, or strip. |
| S14 | `Declare PtrSafe Function` | API declarations with `PtrSafe` keyword and `LongPtr` type. |
| S15 | Type suffix characters | `myVar$`, `myInt%`, `myLong&`, `myCurrency@`, `myDouble#`, `mySingle!` — legacy but legal. |
| S16 | Implicit `Let` / `Set` | Assignment without keyword vs. `Set obj = ...` for objects. |

---

## 4. Statement-Level Constructs

Beyond the obvious — patterns that interact with the form/control reference grammar.

| # | Construct | Notes |
|---|---|---|
| T1 | `On Error GoTo` / `On Error Resume Next` / `On Error GoTo 0` | All three forms; nested handlers in macro-converted code. |
| T2 | `GoTo` and labels | Including the boilerplate `ExitHandler:` / `ErrorHandler:` shape from converted macros. |
| T3 | `Resume` / `Resume Next` / `Resume <label>` | Error-handler control flow. |
| T4 | `Property Get` / `Property Let` / `Property Set` | Class module property accessors. |
| T5 | `Enum` declarations | Module-level enumerations. |
| T6 | `Type` ... `End Type` | User-defined types. |
| T7 | Optional parameters with defaults | `Optional ByVal x As Long = 0` — including `IsMissing` checks for Variants. |
| T8 | `ParamArray` | Variadic argument lists. |
| T9 | Array declarations | `Dim a(10)`, `Dim a(1 To 10)`, `Dim a()`, `ReDim`, `ReDim Preserve`. |

---

## 5. Negative and Recovery Cases

Real-world VBA code is messy. A parser meant for production codebases needs cases that *shouldn't crash* but should produce diagnostics.

| # | Case | Notes |
|---|---|---|
| N1 | Commented-out code mixed with live code | Including code inside `If False Then ... End If` blocks. |
| N2 | Trailing whitespace and stray Unicode | Smart quotes pasted from email, BOM markers, non-breaking spaces. |
| N3 | Missing `Option Explicit` | Undeclared variables are legal; parser shouldn't reject. |
| N4 | DAO/ADO ambiguity | `Recordset` could be either; resolution depends on reference order. |
| N5 | Mismatched block terminators | `End If` vs. `End With` — VBA editor tolerates some cases. |
| N6 | Empty procedures | `Sub Foo()` immediately followed by `End Sub`. |
| N7 | Unclosed string literals at line end | Should produce a recoverable diagnostic. |
| N8 | Excessively long lines | Some legacy code exceeds typical line-length assumptions. |

---

## 6. Macro-Converted Code as Test Corpus

The Access **Convert Macros to Visual Basic** command (`DoCmd.RunCommand acCmdConvertMacrosToVisualBasic`) produces VBA with a distinctive shape that's useful as parser input:

- Functions named `Converted Macro- <macroname>` (note the space and hyphen).
- Heavy use of `DoCmd.*` calls with stringly-typed arguments containing `Forms!...` references embedded in string literals.
- Repetitive `On Error GoTo` boilerplate with `ExitHandler:` / `ErrorHandler:` labels.
- `DoCmd.SetWarnings` and `DoCmd.Echo` wrappers around action sequences.
- Macro `RunMacro` actions become `DoCmd.RunMacro "name"` — not inlined.

Converting a real Access app's macros yields hundreds of small functions with predictable structure — useful for regression testing and for exercising the "expression embedded in a string literal" boundary case (§1 #5, §2 E7).

**Note:** Data macros (table-level `Before Change`, `After Insert`, etc.) cannot be converted to VBA. They run in the database engine.

---

## 7. Coverage Self-Check Questions

Before declaring the test suite complete, verify the parser handles:

- [ ] Every reference variant in §1 #15–17 against both forms and reports.
- [ ] All three argument forms of every domain function in E5/E6.
- [ ] Domain-function criteria strings concatenated with control references (the parser's stance on string-peek vs. opaque should be a deliberate decision, not an accident).
- [ ] Calculated `ControlSource` containing nested expression constructs (E11).
- [ ] Conditional compilation directives at module, procedure, and statement scope.
- [ ] `WithEvents` class modules with control-reference initialization.
- [ ] At least one negative case from each row of §5.
- [ ] Macro-converted output as a corpus, not just hand-written examples.

---

## Known interpretation hazards

Things AI-based or naive analyzers commonly get wrong — worth dedicated test assertions:

- `IIf` is **not** short-circuiting. Both branches evaluate.
- `Nz` return type depends on the second argument; one-arg form returns zero-length string.
- Domain functions return **Null on no-match**, not an error.
- Bang (`!`) and dot (`.`) are different *operators* with different *semantic lookups* — not a late-bound vs. early-bound distinction. Both can be early- or late-bound depending on the static type of the left operand.
- Domain function criteria are stringly-typed; quoting rules differ for text/numeric/date, and `#...#` date delimiters are locale-sensitive.
- Domain functions inside query columns or `ControlSource` run **per row**.
- `Screen.ActiveControl` cannot be statically resolved to a specific control.
- `OpenArgs` is opaque to the parser — receiver parses it however it likes.
