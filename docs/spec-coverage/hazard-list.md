# VBA Parser Hazard List

> **Purpose:** A seed list of concrete parsing hazards observed in the 12 test fixtures under `docs/test-cases/`, with MS-VBAL spec citations and parsing-difficulty estimates. Drives `vba-grammar-validation` (#5) edge-case fixture authoring and grammar-session checklists.
> **Source fixtures:** `docs/test-cases/01-simple-validation.bas` through `docs/test-cases/12-switchboard-navigation.bas` (12 fixtures, mixed `.bas` / `.cls` / `.sql` / `.txt`).
> **Spec source:** MS-VBAL section numbers as cataloged in `docs/spec-study/ms-vbal-summary.md` (Session 1 of this set). Where a hazard is Access-specific (extending MS-VBAL core), the entry says so explicitly.
> **Produced:** Session 3 of `vba-spec-study` (2026-04-25), Opus orchestrator authoring in-conversation (see Deviation note).
> **Session set:** `docs/session-sets/✅vba-spec-study/`

> **Deviation note.** The spec called for `route(task_type="architecture", complexity_hint=80)` synthesis. Batch A's first call succeeded (1909 input / 16788 output tokens, $1.29) but its content was lost to a Python `print()` Unicode error (`cp1252` codec) before the file write. The retry timed out at the provider boundary. Per the existing lessons-learned action item ("If a synthesis-document `route()` call to Opus times out and the orchestrator is Sonnet: escalate to Opus as the orchestrator for the next attempt") the human switched the orchestrator to Opus. Per `project-guidance.md` → Conventions → "When the orchestrator's model matches the routing target for a given task type, handle the task in-conversation rather than calling `route()`", this document was authored in-conversation rather than re-attempting `route()`. The non-negotiable session-verification cross-provider call (gpt-5-4) is unchanged.

> **Status — seed list.** This document is the seed authored by `vba-spec-study`; per the spec's hazard-list standards, hazards discovered during grammar work get appended later. Most hazards listed here are grounded in observed corpus syntax — no abstract concerns. Three hazards (H-D05 Resume, H-D12 named arguments, H-E07 ParamArray) are explicitly spec-seeded *with no corpus witness* — they cover MS-VBAL-critical constructs that are absent from the current 12 fixtures and require synthetic fixtures from `vba-grammar-validation` (#5). These three are flagged in their entries and in the cross-reference preamble.

---

## How to Use This Document

Each hazard is a self-contained entry with the following fields:

- **File:** the fixture filename and line numbers where the construct appears.
- **Construct:** quoted source text (1–2 lines) showing the exact syntax.
- **MS-VBAL Section:** the relevant section of the MS-VBAL spec, citing both the section number and the in-summary reference where applicable. Access-specific hazards beyond the MS-VBAL core are flagged "Access ext."
- **Parsing Difficulty:** Low / Medium / High / Very High. Calibrated against the construct in isolation; difficulty compounds when hazards co-occur.
- **Description:** the concrete technical reason this is hard to parse correctly. Generic concerns are excluded; only specific, actionable hazards are listed.

Grammar sessions (the `vba-grammar-*` child sets) should treat each hazard as a test-case requirement: every hazard listed must have at least one fixture (corpus or synthetic) that exercises it, and a passing parse against the `<n>.expected.json` contract.

The hazard categories below are presented in roughly the order a parser encounters them — lexical first, then expression-level, then statement and module structure, finishing with embedded DSLs and static-analysis-defeating constructs.

---

## Section A: Lexical and Token-Level Hazards (H-A01 – H-A09)

### H-A01: Line continuation `_` token disambiguation

- **File:** `02-cascading-combo-subform.bas:11–14, 22–23`; `03-with-block-shadowing.bas:15–16, 25–26, 36–37`; `05-dynamic-sql-construction.bas:20–21, 41–47`; `11-autoexec-startup-chain.bas:54–58`; `12-switchboard-navigation.bas:26–28, 35–36, 45–47, 60–61, 80–81, 94–95`.
- **Construct:** `Me!cboClient.RowSource = "SELECT ClientID, ClientName FROM tblClients " & _`
- **MS-VBAL Section:** §3.3.3 (Line Continuation).
- **Parsing Difficulty:** Medium.
- **Description:** A continuation is `<WS> _ <line-terminator>` and must be resolved as a pre-lex pass that joins physical lines into logical lines before any other tokenization. The lexer must distinguish `my_var_` (identifier ending in underscore) from `Call MyMethod _` (continuation) — the cue is whether the underscore is preceded by whitespace and immediately followed by a line terminator. Get this wrong and either valid identifiers are split or every `&`-concatenated SQL string in the corpus produces parse errors at the line break. Continuations may not appear inside string literals; a `_` between `"` quotes is literal data.

### H-A02: Type-declaration suffix characters `% & ! # @ $`

- **File:** `07-legacy-patterns.bas:12, 16, 18`.
- **Construct:** `Public Function ImportClientBatch(sFilePath$) As Variant` and `Dim i&, j&, lRow&, lCol&` and `Dim sErr$`.
- **MS-VBAL Section:** §3.3.5.1 (Type-Declaration Characters).
- **Parsing Difficulty:** Medium.
- **Description:** The suffixes `%` (Integer), `&` (Long), `!` (Single), `#` (Double), `@` (Currency), `$` (String) attach to identifiers in declarations and references and are part of the identifier token *for typing purposes* but not for symbol equality (`sErr$` and `sErr` refer to the same variable). The lexer must consume the suffix as part of the identifier token; the AST must record both the name and the declared type. Note the collision with §5.2.2: `!` is also the bang operator. The lexer must distinguish `i&` (Long suffix on identifier in declaration position) from `obj!key` (bang member access). Disambiguation is positional: in a `Dim` clause the suffix is type; in an l-expression the `!` is bang.

### H-A03: Line numbers as statement labels

- **File:** `07-legacy-patterns.bas:13–99` (every executable statement).
- **Construct:** `10  Dim oExcel As Object` ... `100 Set oExcel = CreateObject("Excel.Application")`.
- **MS-VBAL Section:** §3.3.2 (Line Numbers).
- **Parsing Difficulty:** Medium.
- **Description:** A leading numeric token at statement start (after optional whitespace) is a line label, not part of the following statement. The parser must recognize bare integer literals at the start of a logical line as labels and emit a label node, not a numeric expression. This collides with `On Error GoTo 0` (§5.4.11), where `0` is the literal integer zero meaning "deactivate handler", *not* a line label reference. The disambiguation is positional: at statement start a number is a label declaration; after `GoTo`/`GoSub`/`Resume`/`On Error GoTo` it is a label reference. Line numbers may be sparse and need not be sequential. Mixed line-numbered and unnumbered statements occur in this fixture and must coexist.

### H-A04: Compound statement separator `:` vs. label `:`

- **File:** `09-report-grouping-events.bas:39` (`If FormatCount = 1 Then m_GroupTotal = 0 : m_GroupCount = 0`); `11-autoexec-startup-chain.bas:85–89` (`Case "Administrator": DoCmd.OpenForm "frmAdminSwitchboard"`); `07-legacy-patterns.bas:74, 90, 94` (`ValidateRow:`, `NoExcel:`, `OpenFailed:`).
- **Construct:** `If FormatCount = 1 Then m_GroupTotal = 0 : m_GroupCount = 0` versus `ValidateRow:`.
- **MS-VBAL Section:** §5.4.9 (GoTo) for label form; §5.4.2 (Select Case) for the case-body lead-in usage; §5.4.1 (If...Then...Else) for the single-line If statement-separator usage.
- **Parsing Difficulty:** High.
- **Description:** The colon serves three distinct grammatical roles: (1) statement separator on a single logical line (`a = 1 : b = 2`), (2) line-label terminator (`Handler:`), and (3) `Case`-list-then-statement separator inside a `Select Case` single-line case body (`Case "Foo": DoX`). The parser must use position to disambiguate: a colon immediately after an identifier at the start of a logical line that has nothing else following is a label; a colon following a `Case` clause is the case-body lead-in; otherwise it is a statement separator. Note that `Case "Administrator": DoCmd.OpenForm ...` in fixture 11 is *not* a label — the identifier `"Administrator"` is a string literal, not a label name, and the construct is a single-line case body. Mis-parsing this drops the body of single-line cases.

### H-A05: Date literal `#…#` syntax

- **File:** `06-calculated-expressions.txt:19` (`Between #1/1/2000# And Date()`); `12-switchboard-navigation.bas:81` (`"#" & Format(Date() - 30, "yyyy-mm-dd") & "#"`); `08-unresolvable.bas:37` (`Replace(sFilter, "{Today}", "#" & Format(Date, "yyyy-mm-dd") & "#")`).
- **Construct:** `Between #1/1/2000# And Date()`.
- **MS-VBAL Section:** §3.4 (Literals).
- **Parsing Difficulty:** Medium.
- **Description:** A date literal is bracketed by `#` characters and contains a date/time string interpreted under the user's locale. The lexer must recognize `#` at expression position as the start of a date literal and consume to the closing `#`. The collision is with the type-declaration suffix character `#` (Double) — disambiguation is positional: after an identifier in a declaration, `#` is a type suffix; in expression position it is a date-literal delimiter. A second pitfall in this corpus: fixtures 12 and 08 build date literals as *runtime strings* (`"#" & Format(...) & "#"`) so the parser sees the `#` as part of a string literal, not a date literal — the resulting SQL filter contains a date literal at runtime, but the VBA AST does not. Downstream resolvers must understand this string-builder pattern produces an Access expression, not a VBA date-literal token.

### H-A06: `=` as assignment vs. comparison vs. expression-leading

- **File:** `01-simple-validation.bas:11` (`Cancel = True`); `06-calculated-expressions.txt:13` (`=Nz([SubTotal],0) + Nz([TaxAmount],0) - Nz([Discount],0) + Nz([ShippingCharge],0)`).
- **Construct:** `Cancel = True` (assignment) vs. `=Nz([SubTotal],0) + ...` (expression-leading `=`).
- **MS-VBAL Section:** §5.3.1 (Let Statement); Access expression-language extension for ControlSource.
- **Parsing Difficulty:** High.
- **Description:** In VBA proper, `=` at statement position is a Let assignment; in expression position it is comparison. The Access expression language (used in ControlSource, DefaultValue, etc.) extends this with a *third* meaning: a leading `=` indicates "this whole property value is a computed expression" rather than literal text. Fixture 06 shows the expression-leading form; fixture 01 shows the statement assignment form. The parser entry-point matters: when parsing a `.bas`/`.cls` body the dispatcher applies VBA grammar; when parsing a ControlSource property value, the dispatcher applies the Access expression grammar with the `=` prefix sentinel. ValidationRule context is even more delicate (see H-H04) — no `=` prefix at all.

### H-A07: Apostrophe vs. string-literal apostrophe

- **File:** Apostrophes-in-string-literal: `02-cascading-combo-subform.bas:23` (`" AND CaseStatus IN ('Open', 'Pending Review')"`); `05-dynamic-sql-construction.bas:27` (`" AND c.Status IN ('Open', 'Pending Review') "`); `08-unresolvable.bas:36` (`Replace(sFilter, "{CurrentUser}", "'" & CurrentUser() & "'")`); `10-class-with-events.cls:129` (`Replace(sOldValue, "'", "''")`). Apostrophe comments: `01-simple-validation.bas:1–6` (header block); `02-cascading-combo-subform.bas:1–7`; `09-report-grouping-events.bas:13–15, 21, 28, 38, 44, 55, 61, 73, 88` (inline comments). `REM`-form comments: not present in current corpus (synthetic fixture coverage required for `vba-grammar-validation` #5).
- **Construct:** `" AND CaseStatus IN ('Open', 'Pending Review')"` (apostrophes inside a string literal must not start a comment) and `Replace(sOldValue, "'", "''")` (apostrophe escaping in SQL).
- **MS-VBAL Section:** §3.3.1 (Comments).
- **Parsing Difficulty:** Low (with state) / High (without).
- **Description:** The apostrophe `'` introduces a comment when it appears outside a string literal; inside a `"..."` it is literal data. The lexer must track string-literal state before checking for comments. A correlated hazard appears with line continuation (H-A01): `' comment _` followed by another physical line — per §3.3.3 the continuation is processed before comment recognition, so the next physical line is *also* commented out. A trailing-`_` inside a comment is therefore a multi-line comment, which is non-obvious. Several fixture comment headers use this pattern accidentally if reformatted.

### H-A08: Bracketed identifier `[…]` reference

- **File:** `06-calculated-expressions.txt:13, 16, 22, 28` (`[SubTotal]`, `[DateOfBirth]`, `[Address1]`, `[cboCaseworker]`); `04-saved-query-form-params.sql:9–10, 18, 30` (`[Forms]![frmDistrictReport]![cboDistrict]`).
- **Construct:** `[Address1]` and `[Forms]![frmDistrictReport]![cboDistrict]`.
- **MS-VBAL Section:** §3.3.5 (Identifiers — Bracketed-identifier form). Access ext. for SQL/expression contexts.
- **Parsing Difficulty:** Medium.
- **Description:** Square brackets quote an identifier that may contain spaces, punctuation, or reserved words (`[End Date]`, `[Order]`). The lexer must recognize `[` at identifier position and consume through the closing `]`. Inside Access SQL and Access expressions this form is *required* for any identifier containing whitespace or `.`. The trap: `[Forms]![frm]!ctl` mixes bracketed forms (`[Forms]`) and bare identifiers (`ctl`) in the same chain — the lexer treats each bracketed segment as one identifier token but must continue chaining via `!` as a member-access operator (H-B01). In VBA modules, `[…]` is the alternate-syntax form for `Application.Evaluate("…")` (Access ext.) — the parser must know its enclosing context to choose the right interpretation.

### H-A09: `Currency` and other primitive type tokens

- **File:** `09-report-grouping-events.bas:14, 16` (`Private m_GroupTotal As Currency`).
- **Construct:** `Private m_GroupTotal As Currency`.
- **MS-VBAL Section:** §3.3 (Data Types and Type System) — primitive type-name tokens.
- **Parsing Difficulty:** Low.
- **Description:** Primitive type names (`Currency`, `Long`, `Integer`, `Double`, `Single`, `Boolean`, `String`, `Date`, `Variant`, `Byte`, `Object`) are non-reserved keywords — they are reserved in declaration-type position but available as identifiers elsewhere. The parser must recognize them as types after `As` and as identifiers otherwise. `Currency` in particular is a 64-bit fixed-point type whose literal form uses the `@` type-declaration character (H-A02); the parser must accept both `As Currency` and `x@` as references to the same type.

---

## Section B: Member Access and Operator Precedence Hazards (H-B01 – H-B08)

### H-B01: Bang `!` binds tighter than dot `.`

- **File:** `02-cascading-combo-subform.bas:18–19` (`Me.txtCaseworkerEmail = Me!cboCaseworker.Column(2)`); `09-report-grouping-events.bas:30` (`Forms!frmDistrictReport!cboDistrict.Column(1)`).
- **Construct:** `Me!cboCaseworker.Column(2)` and `Forms!frmDistrictReport!cboDistrict.Column(1)`.
- **MS-VBAL Section:** §5.2.2 (Bang Operator) — CRITICAL.
- **Parsing Difficulty:** High.
- **Description:** The bang operator `!` has higher precedence than `.` in member-access chains. The expression `Forms!frm!ctl.Property` parses as `((Forms!frm)!ctl).Property`, not `Forms!(frm!ctl).Property` or any other grouping. A naïve left-to-right binary-operator parser that gives `.` and `!` equal precedence will mis-shape every form-control reference in the corpus. The grammar must encode `!` as a left-binding operator with strictly higher precedence than `.`. The token after `!` is lexed as an identifier but must be reinterpreted as a string key (it indexes the default collection); see H-B02.

### H-B02: Bang as default-collection key access

- **File:** `03-with-block-shadowing.bas:21, 22, 26, 33, 38, 41` (`!LastReviewedBy`, `!ApplicantID`, `!Status`); `02-cascading-combo-subform.bas:11–13, 18, 19` (`Me!cboClient`, `Me!cboCaseworker`).
- **Construct:** `!LastReviewedBy = CurrentUser()` (where `LastReviewedBy` is a string key indexing the default collection of the With target).
- **MS-VBAL Section:** §5.2.2.
- **Parsing Difficulty:** Medium (syntax) / High (semantic).
- **Description:** `obj!key` is sugar for `obj.<DefaultCollection>("key")`. The default collection varies by object type — `Form.Controls`, `Recordset.Fields`, `TableDef.Fields` — and the parser cannot resolve which collection without type information. The AST must preserve the `!` as a distinct member-access kind from `.` so a downstream resolver can insert the correct default-collection lookup. Bracket form is permitted: `Me![Field With Spaces]` uses the bracketed identifier as the key string. The corpus uses the bare-identifier form exclusively, but synthetic fixtures must cover the bracketed form.

### H-B03: Default property access (invisible)

- **File:** `02-cascading-combo-subform.bas:14` (`Me!cboClient = Null`); `12-switchboard-navigation.bas:60–61` (`Forms!frmDistrictReport!cboDistrict = DLookup(...)`).
- **Construct:** `Me!cboClient = Null` (assigns Null to `Me!cboClient.Value` because Value is the default property of a ComboBox).
- **MS-VBAL Section:** §5.2.3 (Default Property Access) — CRITICAL.
- **Parsing Difficulty:** Low (syntax) / Very High (semantic).
- **Description:** Default property access is *absent from the source text* — the parser sees only `Me!cboClient = Null` but the runtime semantics are `Me!cboClient.Value = Null`. The parse output must preserve enough information to distinguish a raw identifier reference (which may take an implicit `.Value`) from an explicit property access (which already has an explicit `.X`). Without this distinction a downstream resolver cannot decide where to insert default-property dereferences and will produce wrong type information. This interacts with §5.3.1/5.3.2 (Set vs. Let, see H-B04): the *presence or absence* of `Set` is what flips between "assign object reference" and "assign through default property."

### H-B04: Set vs. Let on object variables

- **File:** `02-cascading-combo-subform.bas:14` (`Me!cboClient = Null` — Let, default property); `03-with-block-shadowing.bas:14, 15, 52, 53` (`Set db = CurrentDb()`, `Set rs = db.OpenRecordset(...)`, `Set rs = Nothing`, `Set db = Nothing`); `10-class-with-events.cls:42` (`Set m_Form = frmTarget` — Set, reference assignment).
- **Construct:** `Set m_Form = frmTarget` versus `Me!cboClient = Null`.
- **MS-VBAL Section:** §5.3.2 (Set Statement) — CRITICAL.
- **Parsing Difficulty:** Low (syntax) / Very High (semantic).
- **Description:** `Set <l-expr> = <expr>` is the *only* way to assign an object reference. Without `Set`, `obj = expr` is a Let assignment that triggers default-property access on both sides. The keyword presence is trivial to detect, but the parse output must preserve the Set/Let distinction on every assignment node — a flat `=` node that does not record the Set/Let kind throws away the semantics. The corpus has many cases where the same l-expression appears in both Set and non-Set form (e.g., `Set rs = ...` for object binding then `rs.Close` later as a method call); the parser must keep the Set/Let kind explicit.

### H-B05: Index vs. call vs. array `expr(args)` ambiguity

- **File:** `02-cascading-combo-subform.bas:18–19` (`Me!cboCaseworker.Column(2)`); `07-legacy-patterns.bas:32` (`oWB.Sheets(1)`); `08-unresolvable.bas:50` (`Mid(sFilter, iStart + 6, iEnd - iStart - 6)`).
- **Construct:** `Me!cboCaseworker.Column(2)` (could be array index, default-member access, method call, or property-with-args; only type info disambiguates).
- **MS-VBAL Section:** §5.2.4 (Index Expressions).
- **Parsing Difficulty:** High.
- **Description:** `expr(args)` is syntactically identical for: array element access (`arr(i)`), Collection/default-member access (`coll(key)`), function/method invocation (`Func(arg)`), and parameterized property access (`ctl.Column(2)`). The parser cannot distinguish these without type information. The AST must use a single "index-or-call" node that defers disambiguation to semantic analysis. This is also entangled with the `Call` statement (H-D03 — `Call Sub(x)` vs. `Sub x` vs. `Sub(x)`).

### H-B06: TypeOf...Is expression

- **File:** `10-class-with-events.cls:98–99, 117–118` (`If TypeOf ctl Is TextBox Or TypeOf ctl Is ComboBox Or TypeOf ctl Is CheckBox Then`).
- **Construct:** `TypeOf ctl Is TextBox`.
- **MS-VBAL Section:** §5.2.6 (TypeOf...Is Expression).
- **Parsing Difficulty:** Low.
- **Description:** `TypeOf <object-expression> Is <type-name>` is a Boolean-valued unary-prefix construct that is only valid in conditional contexts (inside `If`, `ElseIf`, `While`, etc.). The grammar requires the literal `Is` keyword — `TypeOf obj = TextBox` is invalid. The parser must recognize `TypeOf` as a unary prefix that consumes an object expression and then the keyword `Is` followed by a type identifier (not an arbitrary expression). The trap: `Is` is also the object-comparison operator (`If x Is Nothing Then`); `TypeOf...Is` is a *syntactically separate* construct, not a generalization of `Is`.

### H-B07: `Is` and `Is Not` for object comparison

- **File:** `05-dynamic-sql-construction.bas:82` (`If Not rs Is Nothing Then`).
- **Construct:** `If Not rs Is Nothing Then`.
- **MS-VBAL Section:** §5.1 (Expressions and Operator Precedence) — `Is` precedence relative to `Not` is the hazard.
- **Parsing Difficulty:** Medium.
- **Description:** `obj Is Nothing`, `obj1 Is obj2`, and `obj Is Not Nothing` test object-reference identity, not value equality. The parser must accept `Is` as a binary operator producing Boolean. The trap is `Not rs Is Nothing` — operator precedence requires `Is` to bind tighter than `Not`, so this parses as `Not (rs Is Nothing)`, not `(Not rs) Is Nothing`. The grammar must give `Is` higher precedence than logical `Not`. Fixture 05 line 82 is exactly this construct.

### H-B08: `Me` keyword resolution requires form/report/class module context

- **File:** `01-simple-validation.bas:9, 12, 13, 16, 19` (`Me.txtCustomerName`, `Me.txtPhone`); `09-report-grouping-events.bas:29–31, 47–51, 57, 64–67, 73–75, 84` (`Me.Caption`, `Me.txtCaseworkerEmail`, `Me.lblEmailLabel.Visible`, `Me.txtCaseValue`, `Me.txtCaseNumber`, `Me.txtGroupTotal`, `Me.txtGrandTotal`).
- **Construct:** `Me.txtCustomerName.SetFocus`.
- **MS-VBAL Section:** §5.6 (Me) — CRITICAL.
- **Parsing Difficulty:** Low (syntax) / Medium (resolution).
- **Description:** `Me` is a keyword that refers to the current instance of the enclosing class module. In a form module, `Me` is the form; in a report module, the report; in a class module, the instance. The parser recognizes `Me` as a special primary expression. The resolution challenge is that the *type* of `Me` depends on the module kind, which the parser must know in order to type-check the parse output. The parser must therefore either (a) know the module kind from its container (form, report, standard, class) before parsing or (b) emit a `Me` reference node and defer typing to a later pass. Contrast: fixture 10 (`10-class-with-events.cls`, a non-form/report class module) reaches its target form via the `WithEvents` variable `m_Form` rather than `Me` (see `10-class-with-events.cls:42` `m_AuditEntityName = frmTarget.Name`); fixture 11 (a standard module) has no `Me` at all — `Me` outside a class/form/report module is a parse error and the parser must detect this.

---

## Section C: With-Block Resolution Hazards (H-C01 – H-C04)

### H-C01: Leading `.` resolves against With target

- **File:** `03-with-block-shadowing.bas:18–24, 30, 47–49` (`.BOF`, `.EOF`, `.Edit`, `.RecordsetClone`, `.Update`, `.Close`).
- **Construct:** `With rs ... If Not (.BOF And .EOF) Then ... .Edit ... .Update ... .Close ... End With`.
- **MS-VBAL Section:** §5.4.7 (With...End With) — CRITICAL.
- **Parsing Difficulty:** Medium.
- **Description:** Inside a `With` block, a leading-dot member access at the start of a primary expression (`.BOF`, `.Update`) is shorthand for `<with-target>.BOF`, `<with-target>.Update`. Syntactically, `.member` at primary-expression start is a distinct token sequence from a qualified `object.member` — it lacks a left-hand operand. The grammar must treat `.identifier` (and `.identifier(args)`) at expression start as an "implicit-with-target member access" and pair it with the most-recently-opened With block during AST construction. A parser that treats `.` only as a binary operator (requiring a left-hand operand) will reject every leading-dot access in the corpus.

### H-C02: Leading `!` inside With resolves to default-collection-of-target

- **File:** `03-with-block-shadowing.bas:21, 22, 26, 33, 38, 41` (`!LastReviewedBy`, `!Status`, `!ApplicantID`).
- **Construct:** `With rs ... !LastReviewedBy = CurrentUser() ... End With` — `!LastReviewedBy` resolves to `rs.Fields("LastReviewedBy")`.
- **MS-VBAL Section:** §5.4.7 + §5.2.2.
- **Parsing Difficulty:** Medium.
- **Description:** A leading-bang `!key` inside a With block resolves the bang against the With target's default collection. This compounds H-B01 (bang precedence), H-B02 (default-collection key), and H-C01 (implicit With target). The parser must treat `!identifier` at primary-expression start as "implicit-with-target bang access" — the same shape as `.identifier` at primary-expression start (H-C01), except the operator is `!`. The semantic resolution requires knowing the With target's default collection, which depends on type information — typical Access patterns: `With rs` → `Fields`, `With Forms!frmMain` → `Controls`, `With CurrentDb.TableDefs("X")` → `Fields`.

### H-C03: Nested With shadowing — inner With shadows outer

- **File:** `03-with-block-shadowing.bas:18, 31–44`.
- **Construct:**
  ```
  With rs
      ...
      With Me.sfrmInspections.Form
          If .RecordsetClone.RecordCount = 0 Then
              !Status = "Pending Inspection"
  ```
- **MS-VBAL Section:** §5.4.7.
- **Parsing Difficulty:** High.
- **Description:** When With blocks nest, the inner With target shadows the outer for both `.member` and `!key` resolution. In fixture 03, the outer `With rs` makes `!Status` mean `rs.Fields("Status")` initially; entering the inner `With Me.sfrmInspections.Form` causes `.RecordsetClone` to mean `Me.sfrmInspections.Form.RecordsetClone` and `!Status` to mean `Me.sfrmInspections.Form!Status` (a control reference, NOT the recordset field). On exit from the inner block, leading-`.`/`!` resolution returns to `rs`. This is the canonical "With-block shadowing" hazard — a parser that flattens With blocks into a single resolution scope produces semantically wrong AST nodes and a downstream resolver cannot recover. The AST must preserve a stack of With targets and bind each leading-`.`/`!` to the innermost active target at the construct's source position.

### H-C04: With target single-evaluation

- **File:** `03-with-block-shadowing.bas:18` (`With rs`); the same fixture's nested `With Me.sfrmInspections.Form`.
- **Construct:** `With <expression>` — the expression is evaluated *once* at block entry, and the resulting reference is reused for every leading-`.`/`!` inside.
- **MS-VBAL Section:** §5.4.7.
- **Parsing Difficulty:** Low (syntax) / Medium (semantic).
- **Description:** The With target expression is evaluated once when the block is entered; subsequent references inside use the cached reference. This matters when the expression has side effects, when the referenced object is reassigned inside the block, or when the expression is itself a chain like `Me.sfrmInspections.Form` that could change between iterations of an outer loop. The parser need not enforce the single-evaluation semantics, but the parse output must preserve enough information that a downstream optimizer or analyzer can tell the With-target expression is evaluated once for the block, not re-evaluated per leading-`.`/`!` use.

---

## Section D: Statement and Control-Flow Hazards (H-D01 – H-D12)

### H-D01: GoTo labels (alpha and numeric)

- **File:** `05-dynamic-sql-construction.bas:18, 79` (`On Error GoTo ErrHandler` / `ErrHandler:`); `07-legacy-patterns.bas:25, 28, 42, 90, 94` (`GoTo NoExcel`, `GoTo OpenFailed`, `GoTo NextRow`).
- **Construct:** `On Error GoTo ErrHandler` paired with `ErrHandler:` later in the same procedure.
- **MS-VBAL Section:** §5.4.9 (GoTo) — CRITICAL.
- **Parsing Difficulty:** Low.
- **Description:** A label is an identifier or line-number followed by `:` at statement position. The parser must (1) recognize labels as statement-level constructs, not as part of the following statement, (2) avoid confusing label-`:` with statement-separator-`:` (H-A04), and (3) accept either an identifier label (`ErrHandler:`) or a numeric line-number label (`100`, see H-A03). Forward references are valid: `GoTo X` may refer to a label defined later in the procedure. The AST should record labels as members of their enclosing procedure with their source position and let a resolution pass match each `GoTo`/`GoSub`/`Resume` reference to its label.

### H-D02: GoSub / Return — local subroutine within procedure

- **File:** `07-legacy-patterns.bas:43` (`GoSub ValidateRow`); `07-legacy-patterns.bas:74–88` (`ValidateRow:` ... `Return`).
- **Construct:**
  ```
  GoSub ValidateRow
  ...
  ValidateRow:
      If ... Then sErr = "Missing name" : Return
  ```
- **MS-VBAL Section:** §5.4.10 (GoSub / Return) — CRITICAL.
- **Parsing Difficulty:** Low (syntax) / Medium (semantic).
- **Description:** `GoSub <label>` jumps to a label and pushes the return address; `Return` pops and resumes after the GoSub. There is no new stack frame, no parameter list, and all local variables are shared. The parser must distinguish `Return` (GoSub return) from a function return value (which in VBA is set by assigning to the function name, not by a `Return` statement — that is C-style). The keyword is unambiguous, but the AST node must mark the kind clearly (`GoSubReturnStmt`, not `FunctionReturnStmt`).

### H-D03: Multi-purpose label — GoSub target AND fall-through target

- **File:** `07-legacy-patterns.bas:74` (`ValidateRow:` is reachable via `GoSub ValidateRow` and as a fall-through after `Next i`).
- **Construct:** The label `ValidateRow:` in fixture 07 is reachable both by `GoSub` from inside the loop body and by sequential fall-through after `Next i` completes. (Fixture 07's control flow exits through `Exit Function` before fall-through, but the label is structurally reachable.)
- **MS-VBAL Section:** §5.4.9, §5.4.10.
- **Parsing Difficulty:** Medium (syntax) / High (control-flow analysis).
- **Description:** A label has no syntactic distinction between "GoSub target" and "GoTo target" — it is just a label. Whether `Return` is in scope at runtime depends on whether control reached the label via `GoSub` (in which case the return-address stack is non-empty) or via fall-through / `GoTo` (in which case `Return` raises a runtime error). The parser cannot determine at parse time which mode is active; the AST must preserve the label as a generic landing pad and a downstream control-flow analyzer must compute reachability. Fixture 07 is a documented legacy pattern where the same block serves as both a subroutine body and a fall-through tail; the parser must not "fix" this by emitting a parse error.

### H-D04: On Error variants

- **File:** `On Error GoTo <label>`: `05-dynamic-sql-construction.bas:18` (`On Error GoTo ErrHandler`); `11-autoexec-startup-chain.bas:35` (`On Error GoTo ErrHandler`). `On Error Resume Next`: `07-legacy-patterns.bas:22`; `12-switchboard-navigation.bas:107`. `On Error GoTo 0` (deactivate): not present in current corpus — synthetic fixture required in `vba-grammar-validation` (#5).
- **Construct:** `On Error GoTo <label>`, `On Error Resume Next`, `On Error GoTo 0`.
- **MS-VBAL Section:** §5.4.11 (On Error) — CRITICAL.
- **Parsing Difficulty:** Medium.
- **Description:** Three forms exist: `GoTo <label>` (activate handler), `Resume Next` (suppress and continue), `GoTo 0` (deactivate). The token after `On` disambiguates from the other `On <expression> GoTo …` form (computed GoTo) — if the next token is the keyword `Error`, it is error-handling; otherwise it is computed dispatch. `On Error GoTo 0` uses the literal `0` as a sentinel meaning "deactivate", *not* a label-zero reference (collides with H-A03). These are statements that execute at flow-reached points; a procedure may switch error-handling modes multiple times.

### H-D05: Resume / Resume Next / Resume `<label>`

- **File:** None in current 12-fixture corpus (spec-seeded hazard). Fixture 07 uses `On Error Resume Next` (a separate construct, see H-D04); fixture 05's error handler exits via `Exit Function` rather than `Resume`. Synthetic fixture required in `vba-grammar-validation` (#5).
- **Construct:** `Resume`, `Resume 0` (equivalent to `Resume`), `Resume Next`, `Resume <label>` — four valid forms per MS-VBAL §5.4.12.
- **MS-VBAL Section:** §5.4.12 (Resume) — CRITICAL.
- **Parsing Difficulty:** Low (syntax) / High (semantic).
- **Description:** `Resume` (or `Resume 0`) retries the failing statement; `Resume Next` continues at the statement after; `Resume <label>` jumps to a label. The latter is *syntactically* identical to `GoTo <label>` but additionally clears the error state. The parser must preserve the keyword `Resume` (vs. `GoTo`) in the AST so a downstream analyzer correctly models the side effect of clearing the `Err` object. `Resume 0` collides with H-A03 and H-D04 — disambiguation is positional: after `Resume` the `0` is a sentinel meaning "retry"; after `On Error GoTo` the `0` means "deactivate"; otherwise the `0` is a label or a literal.

### H-D06: Single-line If with multiple statements via `:`

- **File:** `09-report-grouping-events.bas:39` (`If FormatCount = 1 Then m_GroupTotal = 0 : m_GroupCount = 0`).
- **Construct:** `If FormatCount = 1 Then m_GroupTotal = 0 : m_GroupCount = 0`.
- **MS-VBAL Section:** §5.4.1 (If...Then...Else); colon serves as statement separator within the single-line Then-body.
- **Parsing Difficulty:** Medium.
- **Description:** Single-line If is `If <cond> Then <stmt> [Else <stmt>]` with no `End If`. When the Then-body contains multiple statements separated by `:`, the entire colon-separated chain belongs to the Then branch. The parser must consume statements separated by `:` until end-of-logical-line or `Else`. The trap: a trailing `Else` ends the Then block, so `If C Then S1 : S2 Else S3` puts S1 and S2 in the Then branch and S3 in the Else branch. Distinguishing single-line If from block If: a newline (not a line continuation) immediately after `Then` indicates block form; statements after `Then` on the same logical line indicate single-line form.

### H-D07: Select Case with single-line bodies via `:`

- **File:** `11-autoexec-startup-chain.bas:85–89` (`Case "Administrator": DoCmd.OpenForm "frmAdminSwitchboard"`).
- **Construct:**
  ```
  Select Case g_UserRole
      Case "Administrator": DoCmd.OpenForm "frmAdminSwitchboard"
      Case "Caseworker": DoCmd.OpenForm "frmCaseworkerHome"
      ...
      Case Else: DoCmd.OpenForm "frmMainSwitchboard"
  End Select
  ```
- **MS-VBAL Section:** §5.4.2 (Select Case).
- **Parsing Difficulty:** High.
- **Description:** A `Case` clause may be followed on the same logical line by a single statement using `:` as the separator. This is the same colon-collision as H-A04 — the parser must distinguish `Case X:` (case-clause-then-body) from `Label:` (statement label). Disambiguation: a colon immediately after a `Case` clause is the case-body lead-in; otherwise it is a label or statement separator. The body of a single-line case extends to end-of-logical-line and may itself contain colon-separated statements (like H-D06). Newer Access codebases tend to use block-form cases; legacy code uses the single-line form heavily.

### H-D08: Empty positional argument syntax

- **File:** `12-switchboard-navigation.bas:35–36, 60, 65` (`DoCmd.OpenForm "frmCaseDetail", , , , acFormAdd, , "NewCase|..."`).
- **Construct:** `DoCmd.OpenForm "frmCaseDetail", , , , acFormAdd, , "NewCase|..."` (positions 2, 3, 4, 6 omitted).
- **MS-VBAL Section:** §5.5 (Call) + procedure call grammar.
- **Parsing Difficulty:** Medium.
- **Description:** A procedure call may omit positional arguments by leaving the position blank between commas. The parser must accept consecutive commas in an argument list and emit "missing argument" placeholder nodes (typically `Empty` / `Missing`). The trap: a trailing missing argument (`, ,` immediately before `)`) is also valid, and a leading missing argument (`(, x, y)`) is valid. The AST must preserve the argument *position* — it is not enough to compress the list; the receiving procedure binds by position, and the seventh argument here (`"NewCase|..."`) is OpenArgs, distinct from the fifth (`acFormAdd`, DataMode). Fixture 12 uses this pattern repeatedly for `DoCmd.OpenForm` and `DoCmd.OpenReport`; getting the positional binding wrong breaks all switchboard navigation extraction.

### H-D09: `Call Sub(x)` vs. `Sub x` vs. `Sub(x)` parens-trap

- **File:** Call-prefixed: `02-cascading-combo-subform.bas:27` (`Call RecalculateCaseloadTotals(Me!cboCaseworker)`). Call-less (no parens around outermost arg list): `05-dynamic-sql-construction.bas:80` (`MsgBox "Error in RunFilteredCaseReport: " & Err.Description, vbCritical`); `11-autoexec-startup-chain.bas:86–95` (`DoCmd.OpenForm "frmAdminSwitchboard"`, etc.); `12-switchboard-navigation.bas:35–36, 60` (`DoCmd.OpenForm "frmCaseDetail", , , , acFormAdd, , "..."`).
- **Construct:** `Call RecalculateCaseloadTotals(Me!cboCaseworker)` versus `RecalculateCaseloadTotals Me!cboCaseworker` versus `RecalculateCaseloadTotals(Me!cboCaseworker)`.
- **MS-VBAL Section:** §5.5 (Call statement).
- **Parsing Difficulty:** High.
- **Description:** With `Call`, parentheses around the argument list are required and the call is by-reference for ByRef parameters. Without `Call`, parentheses around the *outermost* argument list are forbidden — `MySub x` is the canonical form. The trap: `MySub (x)` (no `Call`, with parens) is a single argument wrapped in parens, and the parens force *expression evaluation* on `x`, breaking ByRef into ByVal. The parser must record whether the call was Call-prefixed and whether outer parens are call parens vs. argument-grouping parens — the difference determines whether ByRef parameters get a real reference or a value-coerced copy. This is a notorious source of "why didn't my output parameter work" bugs in legacy Access code.

### H-D10: `Exit Sub` / `Exit Function` / `Exit Property` / `Exit For` / `Exit Do`

- **File:** `01-simple-validation.bas:14` (`Exit Sub`); `05-dynamic-sql-construction.bas:78, 87` (`Exit Function`); `08-unresolvable.bas:29, 44` (`Exit Sub`, `Exit Do`).
- **Construct:** `Exit For` / `Exit Do` / `Exit Sub` / `Exit Function` / `Exit Property`.
- **MS-VBAL Section:** §5.4.8 (Exit Statements).
- **Parsing Difficulty:** Low.
- **Description:** Each `Exit <kind>` transfers control out of the immediately enclosing construct of the named kind. The parser must validate nesting at parse time or in a follow-up pass: `Exit For` outside a For/For Each is invalid; `Exit Sub` inside a Function is invalid (must be `Exit Function`). Fixture 08 uses `Exit Do` inside a `Do While ... Loop`, and fixture 01 uses `Exit Sub` inside a Form_BeforeUpdate. Recording the kind in the AST (rather than collapsing all exits to a generic "exit") keeps the validator's job linear.

### H-D11: For Each iteration over collection of unknown member type

- **File:** `10-class-with-events.cls:96–105, 116–135` (`For Each ctl In m_Form.Controls`).
- **Construct:** `For Each ctl In m_Form.Controls`.
- **MS-VBAL Section:** §5.4.4 (For Each...Next).
- **Parsing Difficulty:** Low.
- **Description:** `For Each <var> In <collection>` requires the variable to be `Variant`, `Object`, or a specific class that matches the collection's item type. The parser must accept the construct uniformly and let semantic analysis check the type compatibility. The trap (downstream): inside the loop body, member access on `ctl` is late-bound when `ctl As Control` because `Control` is a base class — `ctl.ControlSource` succeeds at runtime only on Controls that have ControlSource (TextBox, ComboBox, CheckBox, ListBox, BoundObjectFrame), and fails on Label, Line, Image, etc. Fixture 10 guards with `If TypeOf ctl Is TextBox Or ...` (H-B06) before accessing `.ControlSource` — the parser must allow this pattern to type-check correctly, which means preserving the TypeOf guard in the AST so a flow-sensitive type narrower can apply it.

### H-D12: Named arguments at call sites

- **File:** None in current 12-fixture corpus (spec-seeded hazard). All call sites in the corpus use positional arguments. Synthetic fixture required in `vba-grammar-validation` (#5).
- **Construct:** `DoCmd.OpenForm FormName:="frmX", DataMode:=acFormAdd, OpenArgs:="ctx"` — argument names with `:=` separator instead of positional binding.
- **MS-VBAL Section:** §5.5 (Call statement); §4.6 (Parameter Declarations) — CRITICAL.
- **Parsing Difficulty:** Medium.
- **Description:** A call-site argument may take the form `<paramname> := <expression>` instead of a bare expression. Named and positional arguments may mix (positional first, named after) but a single argument list may not bind the same parameter twice. The lexer must accept `:=` as a single token distinct from `:` followed by `=` — colon-equal must be tokenized as one operator. The parser must record each argument with its kind (named vs. positional) and the resolver must match named arguments against the callee's formal parameters. A common trap: `DoCmd.OpenForm` accepts both styles, but mixing them with the empty-positional syntax (H-D08) is invalid — `DoCmd.OpenForm "frmX", , , , DataMode:=acFormAdd` requires the resolver to reconcile positional and named binding. Named arguments interact with `Optional` (H-E01): named-argument calls let the caller omit middle Optional parameters by name without needing the `, ,` empty-positional syntax.

---

## Section E: Procedure, Parameter, and Property Hazards (H-E01 – H-E07)

### H-E01: Optional parameter with default value

- **File:** `05-dynamic-sql-construction.bas:9` (`Public Function RunFilteredCaseReport(Optional ByVal sFilterArgs As String = "") As Long`).
- **Construct:** `Optional ByVal sFilterArgs As String = ""`.
- **MS-VBAL Section:** §4.6 (Parameter Declarations) — CRITICAL.
- **Parsing Difficulty:** Medium.
- **Description:** `Optional` declares a parameter as omittable; once any parameter in the list is `Optional`, all subsequent parameters must also be `Optional`. The default value is provided via `= <constant-expression>` after the type. The parser must accept the parameter modifier sequence `Optional [ByRef|ByVal] <name> [As <type>] [= <default>]` — `Optional` comes first, before any `ByVal`/`ByRef` keyword, matching the fixture witness `Optional ByVal sFilterArgs As String = ""`. A correlated trap: `Optional` without a default value defaults to the `Empty` Variant, and inside the procedure must be tested with `IsMissing()` — the parser need not enforce this but the parse output should preserve the distinction between "Optional with default" and "Optional without default."

### H-E02: ByVal vs. implicit ByRef

- **File:** `05-dynamic-sql-construction.bas:9` (`Optional ByVal sFilterArgs As String`); `10-class-with-events.cls:50, 56, 62` (`ByVal sValue As String`).
- **Construct:** `ByVal sValue As String` versus implicit `sValue As String` (which is ByRef).
- **MS-VBAL Section:** §4.6.
- **Parsing Difficulty:** Low (syntax) / High (semantic).
- **Description:** Unmarked parameters in VBA are ByRef by default — the opposite of most modern languages. Explicit `ByVal` switches to value semantics; explicit `ByRef` is redundant but legal. The parser must default the passing mode to ByRef when neither modifier is present and preserve the explicit `ByVal` keyword in the AST. This interacts with H-D09: a Call-less call site like `MySub (x)` (with parens around the single argument) coerces `x` to a value expression, defeating the ByRef declaration on `MySub`'s side. The parser cannot detect this mismatch at the call site without symbol-table information; the call-site AST must preserve the parens-vs-bare-arg distinction so a later resolver can flag it.

### H-E03: Property Get / Let / Set triplets

- **File:** `10-class-with-events.cls:49–55, 58–64` (`Property Get EntityName` / `Property Let EntityName`; `Property Get Enabled` / `Property Let Enabled`).
- **Construct:**
  ```
  Public Property Get EntityName() As String
      EntityName = m_AuditEntityName
  End Property

  Public Property Let EntityName(ByVal sValue As String)
      m_AuditEntityName = sValue
  End Property
  ```
- **MS-VBAL Section:** §4.5.3 (Property Declarations) — CRITICAL.
- **Parsing Difficulty:** Medium.
- **Description:** A property is conceptually one member but split across up to three procedure declarations: `Property Get` (read), `Property Let` (write a value), `Property Set` (write an object reference). The parser must accept each as its own procedure declaration but link them by name in the symbol table — a property's signature is determined by the union of its triplet entries. Constraints: `Get` and `Let` parameter lists must agree on input parameters except that `Let` has one extra trailing parameter (the value to assign); `Set` is `Let` for object types. The procedure body of `Property Get X() As T` returns by assigning to the procedure name (`X = …`), the same as Function; the `End Property` terminator is required (not `End Function`).

### H-E04: Public / Private / Friend procedure scope

- **File:** `10-class-with-events.cls:29` (`Private Sub Class_Initialize`); `10-class-with-events.cls:34` (`Private Sub Class_Terminate`); `10-class-with-events.cls:41` (`Public Sub AttachToForm`); `10-class-with-events.cls:49, 53` (`Public Property Get/Let EntityName`); `10-class-with-events.cls:67, 78, 86` (`Private Sub m_Form_BeforeUpdate/AfterUpdate/Close`); `10-class-with-events.cls:140` (`Private Sub IAuditLogger_LogEvent`).
- **Construct:** `Public Sub AttachToForm(...)` versus `Private Sub Class_Initialize()`.
- **MS-VBAL Section:** §6.4 (Friend); access-modifier prefix on procedure declarations. Default visibility differs by module kind (form/report event handlers default `Private`; class-module surface defaults `Public`).
- **Parsing Difficulty:** Low.
- **Description:** Procedures may be declared `Public` (default in standard modules; visible outside), `Private` (visible within the module), or `Friend` (visible within the project but not to external callers). The parser must accept the modifier as an optional prefix on the procedure declaration. The default differs by module kind: in a class module, `Public` is default for the public surface; in a form module, event handlers are typically `Private`. The parser should record the explicit modifier (and "default" if absent) so a later resolver can compute visibility correctly.

### H-E05: Implicit Function return via name-as-LHS assignment

- **File:** `05-dynamic-sql-construction.bas:76, 81` (`RunFilteredCaseReport = lCount`, `RunFilteredCaseReport = -1`); `11-autoexec-startup-chain.bas:99, 105` (`Initialize = True`, `Initialize = False`).
- **Construct:** `RunFilteredCaseReport = lCount`.
- **MS-VBAL Section:** §5.3.1 (Let Statement) — the procedure name in LHS position is the assignment-style return idiom.
- **Parsing Difficulty:** Low (syntax) / Medium (semantic).
- **Description:** A Function returns by assigning to its own name as if it were a local variable. The parser sees a regular Let-assignment whose LHS happens to be the enclosing procedure's name. The parser need not enforce this but should resolve such assignments as "Function return" rather than "self-recursive call to Property Get" — the heuristic is "if the identifier is the enclosing Function's name and it appears in LHS position, it is a return assignment." This collides with `Property Get` bodies, where the same idiom (`PropertyName = expr`) sets the return value. The parser cannot distinguish syntactically; the resolver must look up the enclosing procedure kind.

### H-E06: Module-level `Public` global variables

- **File:** `11-autoexec-startup-chain.bas:27–32` (`Public g_CurrentUserID As Long`, etc.).
- **Construct:** `Public g_CurrentUserID As Long`.
- **MS-VBAL Section:** §4.7 (Scope and Lifetime).
- **Parsing Difficulty:** Low.
- **Description:** A `Public` declaration at module level in a standard module declares a project-wide global variable. The parser must recognize module-level `Public` / `Private` / `Dim` declarations and distinguish them from procedure-local declarations. Fixture 11 declares six globals used pervasively in fixture 12 (`g_UserRole`, `g_CurrentUserID`, etc.). The harvester output must preserve cross-module symbol references — the consumer of the parse tree needs to follow `g_UserRole` from its declaration in fixture 11 to its uses in fixture 12. This is module-level structural metadata the parser must emit even though no statement in the body references the declaration directly.

### H-E07: `ParamArray` parameter

- **File:** None in current 12-fixture corpus (spec-seeded hazard). Synthetic fixture required in `vba-grammar-validation` (#5).
- **Construct:** `Public Sub Log(ParamArray args() As Variant)` — the trailing `args()` collects all remaining caller arguments into a Variant array.
- **MS-VBAL Section:** §4.6 (Parameter Declarations) — CRITICAL.
- **Parsing Difficulty:** Medium.
- **Description:** `ParamArray` may appear only as the last parameter, must be `As Variant` (or unspecified, which defaults to Variant), and is mutually exclusive with `Optional`/`ByRef`/`ByVal` modifiers. The parser must accept the modifier order `ParamArray <name>() [As <type>]` and reject any combination with the conflicting modifiers. At call sites, the caller passes a variable number of arguments by position; the parser must allow the call-site argument count to exceed the declared formal parameter count for a procedure whose last formal is `ParamArray`. The trap: at the call site, distinguishing a single-array argument from a multi-element ParamArray expansion (`Log argArray` vs. `Log a, b, c`) requires no special syntax — the parser binds positionally and lets the runtime do the array-vs-scalar decision.

---

## Section F: Object Model Hazards (H-F01 – H-F09)

### H-F01: WithEvents declaration

- **File:** `10-class-with-events.cls:23` (`Private WithEvents m_Form As Form`).
- **Construct:** `Private WithEvents m_Form As Form`.
- **MS-VBAL Section:** §6.2 (WithEvents) — CRITICAL.
- **Parsing Difficulty:** Low.
- **Description:** `WithEvents` is a modifier that may appear in a `Dim`/`Private`/`Public` variable declaration, indicating that the variable is an event source whose events are sunk in the same module. Constraints: the type must be a specific class (not `Object` or `Variant`); `WithEvents` cannot combine with `New` (no auto-instantiation). The parser must record `WithEvents` as a property of the variable declaration so that the event-handler-naming convention (H-F02) applies to procedures named `<varname>_<eventname>` in the same module.

### H-F02: Event handler naming convention `<varname>_<eventname>`

- **File:** `10-class-with-events.cls:73, 84, 92` (`m_Form_BeforeUpdate`, `m_Form_AfterUpdate`, `m_Form_Close`); `09-report-grouping-events.bas:25, 41, 60, 76, 82, 92` (`Report_Open`, `GroupHeader0_Format`, `Detail_Format`, `GroupFooter0_Format`, `ReportFooter_Format`, `Detail_Retreat`); `12-switchboard-navigation.bas:18, 33, 39, 43, 52, 56, 70, 74, 78, 86, 90, 105` (`Form_Load`, `btnNewCase_Click`, etc.).
- **Construct:** `Private Sub m_Form_BeforeUpdate(Cancel As Integer)`.
- **MS-VBAL Section:** §6.1 (Events) + §6.2.
- **Parsing Difficulty:** Medium.
- **Description:** A procedure named `<varname>_<eventname>` is conventionally an event handler when the module also contains a `WithEvents` declaration of `<varname>` of a class that publishes `<eventname>`. In Access form/report modules, the form engine auto-generates handler stubs named `<ControlName>_<EventName>` (e.g., `txtName_AfterUpdate`, `Form_Load`) — same naming convention, but the "WithEvents" binding is implicit in the form's design surface. The parser must treat any procedure name containing an underscore as a candidate for event-handler resolution and let a later pass match it against the module's WithEvents declarations and the form/report's design-time controls.

### H-F03: Implements method naming convention `<interface>_<member>`

- **File:** `10-class-with-events.cls:19, 144–155` (`Implements IAuditLogger`; `Private Sub IAuditLogger_LogEvent(...)`; `Private Property Get IAuditLogger_Enabled() As Boolean`).
- **Construct:** `Private Sub IAuditLogger_LogEvent(EventType As String, Description As String)`.
- **MS-VBAL Section:** §6.3 (Implements) — CRITICAL.
- **Parsing Difficulty:** Medium.
- **Description:** `Implements <Interface>` at module level requires the class to provide a procedure for every public member of the interface, named `<InterfaceName>_<MemberName>` and typically `Private`. Syntactically, `IAuditLogger_LogEvent` is *one identifier* — the parser cannot distinguish it from any other underscore-named procedure without knowing that `IAuditLogger` is implemented by this class. Resolution requires the module-level `Implements` declaration. The trap: a procedure name like `m_Form_BeforeUpdate` could be (a) an event handler for variable `m_Form` event `BeforeUpdate`, (b) an implementation for interface `m_Form` member `BeforeUpdate`, or (c) just a procedure with underscores. Disambiguation requires looking up the `WithEvents`/`Implements` context — the parser must emit the procedure with its raw name and let resolution do the matching.

### H-F04: Class_Initialize / Class_Terminate special names

- **File:** `10-class-with-events.cls:29–37` (`Private Sub Class_Initialize()`, `Private Sub Class_Terminate()`).
- **Construct:** `Private Sub Class_Initialize()` / `Private Sub Class_Terminate()`.
- **MS-VBAL Section:** Access ext. — class-module lifecycle naming convention (constructor/destructor sinks); not a distinct MS-VBAL grammar construct.
- **Parsing Difficulty:** Low.
- **Description:** `Class_Initialize` and `Class_Terminate` are two reserved procedure names in class modules that fire on instance creation and destruction. Syntactically they look like any other underscore-named procedure (collision with H-F02/H-F03), but their resolution is fixed by name: in any class module, a `Class_Initialize` is the constructor sink and `Class_Terminate` is the destructor sink. The parser must emit them as ordinary procedures and let a class-module-aware pass identify them — the AST need not have a special node, but the consumer of the parse tree needs to know the convention.

### H-F05: Form / Report module event signatures

- **File:** `01-simple-validation.bas:8` (`Form_BeforeUpdate(Cancel As Integer)`); `09-report-grouping-events.bas:25, 41, 60, 76, 82` (Format event signatures); `12-switchboard-navigation.bas:18` (`Form_Load()`).
- **Construct:** `Private Sub Form_BeforeUpdate(Cancel As Integer)`.
- **MS-VBAL Section:** §6.1 (Events) + Access-specific event signature catalog (Access ext.).
- **Parsing Difficulty:** Medium.
- **Description:** Form / Report event handlers have specific parameter signatures that must match the engine's published event. `Form_BeforeUpdate` takes a single `Cancel As Integer` ByRef parameter — assigning `Cancel = True` cancels the update. `Form_Load` takes no parameters. `Detail_Format` takes `(Cancel As Integer, FormatCount As Integer)`. The parser must accept the signatures as published; the harvester's output must include the parameter list so a downstream consumer can identify the event by name+arity. Fixture 09 line 41 (`Detail_Format(Cancel As Integer, FormatCount As Integer)`) is the canonical Access-report Format event — different from `Form_BeforeUpdate` which has only Cancel.

### H-F06: Detail_Retreat event with no parameters

- **File:** `09-report-grouping-events.bas:92` (`Private Sub Detail_Retreat()`).
- **Construct:** `Private Sub Detail_Retreat()`.
- **MS-VBAL Section:** §6.1 + Access ext.
- **Parsing Difficulty:** Low (syntax) / Medium (semantic — Format/Retreat pairing).
- **Description:** The Retreat event fires when the report engine has to back up and re-format a section (typical cause: a group block doesn't fit on the page and is moved). Retreat has no parameters and is paired with Format — any module-level state mutated in Detail_Format must be undone in Detail_Retreat to avoid double-counting. Syntactically the parser sees only `Sub Detail_Retreat()`; semantically the AST must carry the kind so a downstream analyzer can pair it with `Detail_Format`. This is the canonical "module-level state correctness" hazard for Access reports — fixture 09's `m_GroupTotal` accumulator is decremented in Retreat exactly to undo the Format-side increment.

### H-F07: Module-level `Private` declaration order vs. procedure declarations

- **File:** `09-report-grouping-events.bas:14–16` (`Private m_GroupTotal As Currency` / `Private m_GroupCount As Long` / `Private m_GrandTotal As Currency` appear before any `Sub` declaration in the module).
- **Construct:** `Private m_GroupTotal As Currency` declared at the top of the module body, before all procedure declarations.
- **MS-VBAL Section:** §4 (Module Body) — declaration ordering rules.
- **Parsing Difficulty:** Low (syntax) / Medium (parsing position).
- **Description:** Module-body grammar requires that all module-level variable declarations precede all procedure declarations within a module. The parser must (a) recognize a `Private`/`Public`/`Dim` at module-statement position as a module variable declaration rather than a procedure-local declaration, and (b) reject a module variable declaration that appears after the first procedure declaration in the same module. Fixture 09's three module-level declarations on lines 14–16 are the canonical "module-level state" pattern, but the *parsing* hazard is positional: the parser must distinguish module-statement context from procedure-statement context, which is determined by whether the parser is inside a `Sub`/`Function`/`Property` body. The semantic role (accumulator across Format/Retreat events) is for downstream analysis, but the parser must emit each declaration with module-level scope explicitly so a downstream cross-procedure reference resolver finds them.

### H-F08: `For Each ... In <collection>` over Controls

- **File:** `10-class-with-events.cls:96, 116` (`For Each ctl In m_Form.Controls`).
- **Construct:** `For Each ctl In m_Form.Controls`.
- **MS-VBAL Section:** §5.4.4.
- **Parsing Difficulty:** Low.
- **Description:** Iterating `Controls` is the canonical pattern for form-control discovery at runtime. The parser accepts the construct uniformly; the harvester's output must record that `m_Form.Controls` is the iterated collection so a downstream resolver can correlate the loop body's `ctl` with the form's design-time controls. Combined with `TypeOf` (H-B06), this becomes a flow-sensitive type-narrowing pattern that the harvester should preserve in the AST — collapsing the TypeOf guard loses the information needed to type-check `ctl.ControlSource` inside the guarded branch.

### H-F09: Implicit form-engine event hookup

- **File:** `01-simple-validation.bas:8` (`Form_BeforeUpdate`); `09-report-grouping-events.bas:25, 41, 60, 76, 82, 92` (Report_Open and Format/Retreat handlers); `12-switchboard-navigation.bas:18` (`Form_Load`); event handlers exist without an explicit `WithEvents` declaration anywhere in the module.
- **Construct:** `Private Sub Form_Load()` in fixture 12 — there is no `Dim WithEvents Form As Form` anywhere.
- **MS-VBAL Section:** §6.1 + Access ext.
- **Parsing Difficulty:** Medium (resolution).
- **Description:** Form and report modules implicitly host an instance of the form/report and auto-bind underscore-named procedures (`Form_X`, `Report_X`, `<ControlName>_<Event>`) to the engine's event publication. There is no `WithEvents` declaration in source; the binding is by-convention and by module-kind. The parser must know the module is a form/report module (from the file extension / containing artifact) to resolve handler-procedure names against the form's design-time event surface. The parse tree must mark the module kind so a resolver can correctly identify which underscore-named procedures are event sinks vs. ordinary helpers.

---

## Section G: Embedded-DSL Hazards (H-G01 – H-G07)

### H-G01: SQL string concatenation (unresolvable until runtime)

- **File:** `02-cascading-combo-subform.bas:11–13, 22–23`; `03-with-block-shadowing.bas:15–16, 25–26, 36–37`; `05-dynamic-sql-construction.bas:20–56`; `08-unresolvable.bas:21–23, 63`; `10-class-with-events.cls:124–131`; `11-autoexec-startup-chain.bas:54–57, 79–81`; `12-switchboard-navigation.bas:45–47, 80–81, 93–95`.
- **Construct:**
  ```
  sWhere = sWhere & " AND c.DistrictID = " & Forms!frmFilterPanel!cboDistrict
  ```
- **MS-VBAL Section:** Outside MS-VBAL core / Access ext. / harvester-policy concern (string concatenation itself is ordinary expression syntax; the hazard is that the *result* is SQL, not VBA).
- **Parsing Difficulty:** N/A for VBA layer / Very High for SQL inference.
- **Description:** The parser sees ordinary `&` concatenation with string literals and VBA expressions. The fact that the result is a SQL statement is invisible at the VBA token level. The harvester's policy is that SQL templates assembled by string concatenation are *not* parsed as SQL at the VBA layer — they remain VBA expressions whose runtime value is a SQL string. Per `project-guidance.md`, unresolvable templates emit a node with `{ source: "unresolved", reason: "<string>" }`; fully-resolved templates (where every concatenation operand is a string literal) may be emitted as a recognized SQL template. The hazard for grammar sessions: define the boundary precisely — a literal-only template like `"SELECT * FROM tblFoo"` should be classified differently from `"SELECT * FROM tblFoo WHERE Id = " & x`.

### H-G02: Access SQL `[Forms]![frm]![ctl]` parameter syntax

- **File:** `04-saved-query-form-params.sql:9–10, 18, 30` (`PARAMETERS [Forms]![frmDistrictReport]![cboDistrict] Long, [Forms]![frmDistrictReport]![txtAsOfDate] DateTime`).
- **Construct:** `[Forms]![frmDistrictReport]![cboDistrict]`.
- **MS-VBAL Section:** Access ext. (Access SQL grammar, not MS-VBAL).
- **Parsing Difficulty:** Medium.
- **Description:** Inside Access SQL (saved queries, `OpenRecordset` literal SQL), the form-control reference syntax is `[Forms]![frmName]![controlName]` — bracketed identifiers chained via `!`. The Access SQL parser is a different grammar from VBA; its expression sublanguage is VBA-flavored but distinct. The hazard: the same syntax inside a *VBA* expression context (`Forms!frmName!controlName` without brackets) is the bang-operator chain (H-B01); inside Access SQL the brackets are conventional and the chain is explicitly parameterized. The parser/dispatcher must select the right grammar based on context (`.sql` file vs. `.bas` file vs. embedded SQL string) and emit the parameter list as a structured field of the SQL parse tree.

### H-G03: Access expression language — leading `=` sentinel

- **File:** `06-calculated-expressions.txt:13, 16, 22, 25, 28` (every expression except 6c starts with `=`).
- **Construct:** `=Nz([SubTotal],0) + Nz([TaxAmount],0) - Nz([Discount],0) + Nz([ShippingCharge],0)`.
- **MS-VBAL Section:** Access ext.
- **Parsing Difficulty:** Medium.
- **Description:** ControlSource, DefaultValue, and similar form-design properties accept either a literal string (no leading `=`) or a computed expression (with leading `=`). The harvester must inspect the first non-whitespace character of the property value to decide which lexer mode to enter. Without the `=`, `Date()` is the literal string "Date()"; with `=`, it is the function call. Fixture 06 has both forms (most expressions lead with `=`; expression 6c is a ValidationRule that lacks the leading `=`).

### H-G04: ValidationRule context — no leading `=`

- **File:** `06-calculated-expressions.txt:19` (`Between #1/1/2000# And Date()`).
- **Construct:** `Between #1/1/2000# And Date()` — no leading `=`, but parsed as a Boolean expression.
- **MS-VBAL Section:** Access ext.
- **Parsing Difficulty:** Medium.
- **Description:** ValidationRule accepts a Boolean expression *without* the leading `=` sentinel — Access takes the entire property value as a Boolean expression. The dispatcher must know that ValidationRule is parsed as expression-by-default (unlike ControlSource, where `=` is required to opt into expression mode). The same fixture file has both styles, so the test runner must distinguish by the property the expression is sourced from. This is a property-context-driven dispatch decision that the harvester must encode in its extractor / parser orchestration.

### H-G05: `Between … And …` operator

- **File:** `06-calculated-expressions.txt:19` (`Between #1/1/2000# And Date()`).
- **Construct:** `Between #1/1/2000# And Date()`.
- **MS-VBAL Section:** Access ext. (SQL-flavored expression operator).
- **Parsing Difficulty:** Medium.
- **Description:** `Between <lower> And <upper>` is a ternary range-test operator in Access SQL and Access expression language. The parser must accept `Between` as a binary operator that consumes a left operand, then `And`, then a right operand. The trap: `And` is also the logical AND operator with different precedence; in `Between` context it is a delimiter, not a logical conjunction. Disambiguation is positional after `Between`. The Access expression grammar must encode this; the VBA grammar (which lacks `Between`) does not.

### H-G06: Bracketed identifier `[Name]` in Access expression

- **File:** `06-calculated-expressions.txt:13, 16, 22, 28` (`[SubTotal]`, `[DateOfBirth]`, `[Address1]`, `[cboCaseworker]`); `04-saved-query-form-params.sql:18` (`[Forms]![frmDistrictReport]![txtAsOfDate]`).
- **Construct:** `[SubTotal]` and `[Address1]`.
- **MS-VBAL Section:** Access ext.
- **Parsing Difficulty:** Medium.
- **Description:** Bracketed identifiers in Access expressions resolve to *the column or control of the same name* in the binding context. In a form's ControlSource, `[SubTotal]` resolves to the form's SubTotal control or the recordset's SubTotal column; in a query, to a column from one of the FROM tables. The parser must accept bracketed identifiers and emit them as resolution-deferred symbols; the harvester's enricher correlates them with the binding context. This is *not* the same as VBA's `[…]` form (H-A08), which is `Application.Evaluate("…")` syntactic sugar — same lexical shape, different semantics by context.

### H-G07: `IIf` nesting

- **File:** `04-saved-query-form-params.sql:19–23` (3-level IIf); `06-calculated-expressions.txt:16` (4-level IIf for age category).
- **Construct:**
  ```
  IIf(DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) > 90,
      "Overdue",
      IIf(DateDiff(...) > 60, "At Risk", "On Track"))
  ```
- **MS-VBAL Section:** Access ext. (Access expression-language built-in).
- **Parsing Difficulty:** Low (syntax) / Medium (depth).
- **Description:** `IIf` is a built-in three-argument function (condition, then-value, else-value). It is *not* short-circuiting — both branches are always evaluated. Syntactically nothing distinguishes it from any other three-argument function call, and the parser handles it as `IIf(arg1, arg2, arg3)`. The hazard is that fixtures use deep nesting (3–4 levels) for ladder-style classification. The grammar handles this via standard expression recursion, but the harvester's output schema must not collapse nested calls into a flat shape, or the conditional structure is lost.

---

## Section H: Static-Analysis-Defeating Constructs (H-H01 – H-H06)

These constructs are documented in `project-guidance.md` → Principles → "VBA Parser Strategy" as required to emit unresolved nodes (`{ source: "unresolved", reason: "..." }`), not to be guessed. Fixture 08 is the canonical exemplar.

### H-H01: `Eval()` — runtime expression evaluation

- **File:** `08-unresolvable.bas:48` (`sFilter = Left(sFilter, iStart - 1) & CStr(Eval(sExpr)) & Mid(sFilter, iEnd + 1)`).
- **Construct:** `CStr(Eval(sExpr))` where `sExpr` is a VBA expression string from a configuration table.
- **MS-VBAL Section:** Access ext. (Access built-in).
- **Parsing Difficulty:** N/A (syntactically a function call) / Unresolvable (semantic).
- **Description:** `Eval()` takes a string and evaluates it as a VBA expression at runtime. The parser sees an ordinary function call; the *contents* of the string are a black box. The harvester must emit `Eval` call sites as unresolved nodes with the argument expression preserved (so a downstream AI consumer can decide whether to flag for human review, e.g., "this Eval consumes a value from `tblFormConfig.FilterTemplate` — manually audit the template content"). Per `project-guidance.md`, the parser must NOT attempt to substitute likely string values; it emits unresolved.

### H-H02: `Application.Run` with variable callback name

- **File:** `08-unresolvable.bas:65` (`Application.Run sCallback, sFormName, sFilter`).
- **Construct:** `Application.Run sCallback, sFormName, sFilter` — `sCallback` is a string from a configuration lookup.
- **MS-VBAL Section:** Access ext.
- **Parsing Difficulty:** N/A (syntactically a call) / Unresolvable (semantic).
- **Description:** `Application.Run "ProcedureName", arg1, arg2` invokes a procedure by its string name at runtime. Static analysis cannot determine which procedure runs without resolving the string. The harvester emits the call as an unresolved-dispatch node and preserves the argument expressions. A downstream consumer that *can* resolve the name (e.g., by enumerating tblFormConfig.Callback values) can chain the resolution; the harvester does not.

### H-H03: `DoCmd.OpenForm` with variable form name

- **File:** `08-unresolvable.bas:58` (`DoCmd.OpenForm sFormName, , , sFilter`); `11-autoexec-startup-chain.bas:85–89` (`Case "Administrator": DoCmd.OpenForm "frmAdminSwitchboard"` — form name is a literal here, not unresolvable).
- **Construct:** `DoCmd.OpenForm sFormName, , , sFilter` (variable form name) versus `DoCmd.OpenForm "frmCaseDetail", , , , acFormAdd, , "NewCase|..."` (literal form name).
- **MS-VBAL Section:** Access ext.
- **Parsing Difficulty:** N/A (syntactically a call) / Unresolvable when the form name is variable.
- **Description:** When the first argument is a string literal, the harvester can emit a resolved navigation edge from the calling form/control to the named target form. When the first argument is a variable (e.g., `sFormName`), the navigation target is unresolvable and the edge must be marked as such. The harvester's output schema needs both shapes — fixture 12 uses the literal form throughout (resolvable navigation) while fixture 08 uses the variable form (unresolvable). A grammar session that drops the first-argument distinction (treating all `DoCmd.OpenForm` calls uniformly) loses the navigation graph.

### H-H04: `DLookup`-derived dispatch

- **File:** `08-unresolvable.bas:64` (`sCallback = DLookup("Callback", "tblFormConfig", "ConfigKey = '" & sConfigKey & "'")`).
- **Construct:** `sCallback = DLookup(...)` followed by `Application.Run sCallback, ...`.
- **MS-VBAL Section:** Access ext.
- **Parsing Difficulty:** N/A (syntactically a call sequence) / Unresolvable (semantic).
- **Description:** The variable `sCallback` is sourced from a database query at runtime, not from any in-source value. Even an aggressive flow analyzer cannot bind `sCallback` to a procedure name. The harvester emits the lookup as an unresolved data source and the subsequent dispatch as an unresolved call. The downstream AI consumer may still be able to enumerate plausible values (by querying `tblFormConfig.Callback`) — the harvester preserves the connection by recording both the DLookup source and the dispatch call site in the same unresolved-flow record.

### H-H05: Template-substitution loop driving runtime `Eval`

- **File:** `08-unresolvable.bas:41–51`.
- **Construct:**
  ```
  iStart = InStr(sFilter, "{Eval:")
  Do While iStart > 0
      iEnd = InStr(iStart, sFilter, "}")
      ...
      sExpr = Mid(sFilter, iStart + 6, iEnd - iStart - 6)
      sFilter = Left(sFilter, iStart - 1) & CStr(Eval(sExpr)) & Mid(sFilter, iEnd + 1)
      iStart = InStr(sFilter, "{Eval:")
  Loop
  ```
- **MS-VBAL Section:** §5.4.5 (Do...Loop) + Access ext. (`Eval`).
- **Parsing Difficulty:** Low (syntax) / Unresolvable (semantic).
- **Description:** The parser sees a `Do While` loop containing string-manipulation calls (`InStr`, `Mid`, `Left`) and an `Eval(sExpr)` call. Syntactically nothing is unusual. The hazard is that the *string body* of `sFilter` is an out-of-band template language with `{Eval:…}` placeholders, and `sExpr` (the substring extracted by `Mid`) is itself a VBA expression compiled and evaluated at runtime by `Eval`. The harvester must (a) recognize the loop as a known string-template pattern (a heuristic, since the template language is not in the VBA grammar) or (b) emit each `Eval` call site as unresolved per H-H01 and let a downstream analyzer correlate the flow. Either way, the AST and harvester output must preserve the data flow `sFilter ← rs!FilterTemplate ← tblFormConfig` so a downstream consumer can audit the template content.

### H-H06: Late-bound `CreateObject` and Object-typed receivers

- **File:** `07-legacy-patterns.bas:14, 19, 24, 31–32` (`Dim oExcel As Object`, `Dim db As Object`, `Set oExcel = CreateObject("Excel.Application")`, `Set oWB = oExcel.Workbooks.Open(...)`, `oWB.Sheets(1)`).
- **Construct:** `Set oExcel = CreateObject("Excel.Application")` followed by `oExcel.Workbooks.Open(sFilePath$)`.
- **MS-VBAL Section:** Access ext. / no direct MS-VBAL core section (`CreateObject` is a late-bound runtime call, not the MS-VBAL `New` expression form).
- **Parsing Difficulty:** Low (syntax) / Unresolvable (member resolution).
- **Description:** Variables typed `As Object` (or `As Variant`) are late-bound — the parser sees `oExcel.Workbooks.Open` but cannot resolve `Workbooks` or `Open` to a specific class member without runtime type information. The harvester must emit the chain as a sequence of late-bound member accesses, each with `{ source: "late-bound", target: "Workbooks" }` or similar provenance. A downstream consumer with a type catalog (or human review) can resolve specific calls; the harvester does not guess. This is the same shape as H-H01..H-H05 — emit unresolved with explicit provenance, not silent fall-through.

---

## Cross-Reference: Fixture → Hazards

| Fixture | Difficulty (per fixture header) | Primary hazards exercised |
|---|---|---|
| `01-simple-validation.bas` | LOW | H-A06, H-A07, H-B08, H-D10 |
| `02-cascading-combo-subform.bas` | MEDIUM-HIGH | H-A01, H-B01, H-B02, H-B03, H-B05, H-D09, H-G01 |
| `03-with-block-shadowing.bas` | HIGH | H-A01, H-B02, H-B04, H-C01, H-C02, H-C03, H-C04, H-G01 |
| `04-saved-query-form-params.sql` | MEDIUM | H-A08, H-G02, H-G06, H-G07 |
| `05-dynamic-sql-construction.bas` | VERY HIGH | H-A01, H-B07, H-D01, H-D04, H-E01, H-E02, H-G01 |
| `06-calculated-expressions.txt` | MEDIUM-HIGH | H-A05, H-A06, H-A08, H-G03, H-G04, H-G05, H-G06, H-G07 |
| `07-legacy-patterns.bas` | VERY HIGH | H-A02, H-A03, H-A04, H-D01, H-D02, H-D03, H-D04, H-H06 |
| `08-unresolvable.bas` | UNRESOLVABLE | H-A05, H-D10, H-G01, H-H01, H-H02, H-H03, H-H04, H-H05 |
| `09-report-grouping-events.bas` | MEDIUM-HIGH | H-A09, H-A04, H-D06, H-F02, H-F05, H-F06, H-F07 |
| `10-class-with-events.cls` | HIGH | H-B04, H-B06, H-D11, H-E03, H-E04, H-F01, H-F02, H-F03, H-F04, H-F08, H-G01 |
| `11-autoexec-startup-chain.bas` | HIGH | H-A01, H-A04, H-D04, H-D07, H-E05, H-E06, H-G01 |
| `12-switchboard-navigation.bas` | HIGH | H-A01, H-A05, H-D04, H-D08, H-F02, H-F09, H-G01 |

The 12 fixtures collectively exercise most hazards in the seed list at least once. Three hazards are explicitly **spec-seeded with no corpus witness** and require synthetic fixtures from `vba-grammar-validation` (#5):

- H-D05: `Resume` / `Resume Next` / `Resume <label>`
- H-D12: Named arguments at call sites (`Param := value`)
- H-E07: `ParamArray` parameter

For all other hazards, the synthesis sessions for grammar work should commission synthetic fixtures wherever a hazard appears in only one corpus fixture — single-witness coverage is fragile.

---

## Hazard Inventory Summary

| Section | IDs | Count | Notes |
|---|---|---|---|
| A: Lexical / Token-level | H-A01..H-A09 | 9 | Pre-lex transforms (line continuation, line numbers) and lexer-state hazards |
| B: Member access / Operator precedence | H-B01..H-B08 | 8 | Bang-vs-dot, default property, Set vs Let, TypeOf, Me |
| C: With-block resolution | H-C01..H-C04 | 4 | Implicit target binding, nesting / shadowing |
| D: Statement & Control flow | H-D01..H-D12 | 12 | GoTo/GoSub, On Error/Resume, single-line If, empty positional args, Call parens, named arguments |
| E: Procedure / Parameter / Property | H-E01..H-E07 | 7 | Optional defaults, ByVal/ByRef, Property triplets, scope, Function return idiom, globals, ParamArray |
| F: Object Model | H-F01..H-F09 | 9 | WithEvents, event naming, Implements naming, Class lifecycle, Form/Report event signatures |
| G: Embedded DSLs | H-G01..H-G07 | 7 | SQL concat, Access SQL form-params, Access expression language modes, Between, IIf nesting |
| H: Static-analysis-defeating | H-H01..H-H06 | 6 | Eval, Application.Run, dynamic OpenForm, DLookup-driven dispatch, template-substitution loop, late-bound Object |
| **Total** | | **62** | |

Six unresolvable hazards (Section H) plus one entire fixture (08-unresolvable.bas) intentionally exercise the harvester's unresolved-node emission path, not its resolution path.
