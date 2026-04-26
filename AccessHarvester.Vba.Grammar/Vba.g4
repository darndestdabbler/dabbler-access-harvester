grammar Vba;

// ANTLR4 C# target: `_input.La(1)` peeks at the next character without
// consuming it. Used by REM_COMMENT to require the bare-`REM` form
// (no body) to be followed by `\r`, `\n`, or EOF — preventing
// `REM:`, `REM.`, `REM(` from being mis-tokenized as comments.
@lexer::members {
    private bool IsRemBoundary() {
        int la = InputStream.LA(1);
        return la == '\r' || la == '\n' || la == -1; // CR, LF, or EOF
    }
}

// ============================================================
// VBA Grammar — Phase 1.4 (Lexical Section)
//
// This file holds the lexical (tokenizer) rules for VBA modules.
// Expression and statement rules land in subsequent sessions of
// vba-lexical-and-expression-grammar (#3) and
// vba-statement-and-module-grammar (#4).
//
// Sources:
//   - docs/spec-study/ms-vbal-summary.md  — Section A (Lexical)
//   - docs/spec-coverage/ms-vbal-coverage.csv — lexical rows
//   - docs/spec-coverage/hazard-list.md   — Section A hazards
//   - docs/test-cases/01-simple-validation.bas, 07-legacy-patterns.bas
//
// MS-VBAL section citations are inline near each rule. Where ANTLR4
// must deviate from the spec's process model (e.g., line-continuation
// is a pre-lex pass per §3.3.3 but is implemented here as a -> skip
// rule that consumes the entire continuation atom), the deviation
// is called out in a comment.
// ============================================================

// ------------------------------------------------------------
// Placeholder parser rules (filled in by sibling sessions)
// ------------------------------------------------------------

module : moduleItem* EOF ;

// `moduleItem` is a stub that swallows tokens until end-of-line so the
// generated parser stays syntactically usable for token-stream tests
// in this session. vba-statement-and-module-grammar (#4) replaces it
// with real declarations and procedures.
moduleItem
    : NEWLINE
    | ~NEWLINE+ NEWLINE?
    ;

// ============================================================
// Expression rules (Phase 1.4 expression section)
//
// Sources:
//   - docs/spec-study/ms-vbal-summary.md  — Section B (Expressions)
//   - docs/spec-coverage/ms-vbal-coverage.csv §5.* rows
//   - docs/spec-coverage/hazard-list.md   — Sections B, C
//   - docs/test-cases/02-cascading-combo-subform.bas (bang chains, calls)
//   - docs/test-cases/03-with-block-shadowing.bas (implicit-With . and !)
//   - docs/test-cases/06-calculated-expressions.txt (operator mix)
//
// Operator precedence follows MS-VBAL §5.6.9 (highest -> lowest):
//   ^                                     (right-associative)
//   unary -, unary +
//   *  /
//   \                                     (integer division)
//   Mod
//   +  -                                  (binary additive)
//   &                                     (string concat)
//   = <> < > <= >= Like Is                (comparisons)
//   Not                                   (logical/bitwise unary)
//   And  /  Or  /  Xor  /  Eqv  /  Imp
//
// ANTLR4 left-recursion handles precedence by alternative order
// (top alternative = highest precedence). Direct binary alternatives
// (`expression OP expression`) become precedence-climbed operators;
// prefix forms (`OP expression`) become unary prefixes; alternatives
// that do not begin with `expression` reference (typeOfExpr, newExpr,
// addressOfExpr, lExpression) act as primary cases at the deepest
// recursion level.
//
// Hazards addressed here:
//   H-B01  bang `!` binds at least as tight as dot `.` in chains.
//          Both are postfix in lExpression with single-IDENTIFIER
//          right operand, so left-to-right associativity yields
//          `((Forms!frm)!ctl).Property` regardless of alt order.
//   H-B02  bang access preserved as distinct AST node (BangAccess)
//          from dot access (MemberAccess) — downstream resolver
//          inserts the default-collection lookup.
//   H-B05  `expr(args)` parses as a single IndexOrCall production;
//          array-index vs. call vs. default-member access cannot be
//          distinguished without type information.
//   H-B06  TypeOf...Is is a separate construct; the literal `Is`
//          keyword and a `typeName` (not arbitrary expression) are
//          required.
//   H-B07  `Not rs Is Nothing` parses as `Not (rs Is Nothing)` —
//          `Is` is a comparison-tier operator (group 8) and `Not`
//          is one tier lower (group 9). Confirmed by alternative
//          order: comparison alternative precedes `NOT expression`.
//   H-C01  `.member` at primary-expression start = implicit-With
//          target member (handled in lExpression as a non-recursive
//          alternative `DOT memberName`).
//   H-C02  `!key` at primary-expression start = implicit-With
//          default-collection access (alternative `BANG memberName`).
//   H-D08  empty positional arguments (`f(a, , b)`) preserved as
//          missing argument slots in argumentList.
//   H-D12  named arguments (`f(name := value)`) tokenize via the
//          existing COLON_EQ lexer token and are kept distinct from
//          positional arguments in the AST.
//
// Expression-leading `=` (H-A06, fixture 06 ControlSource form) is
// the Access expression-language extension, NOT VBA. Parsing the
// `=...` form belongs to the Access-expression dispatcher landing
// in parser-infrastructure (#6) and the JET-SQL grammar in
// jet-sql-grammar-and-parser (#12). The rule below parses a VBA
// expression only.
//
// `Between ... And ...` (fixture 06 ValidationRule form) is also
// Access expression-language, deferred to #12.
//
// `As <type>` clauses inside parameter / variable declarations and
// the array-element-type form (`Object()`, `String*N`) are statement-
// level; the lightweight `typeName` rule here covers only the forms
// needed by `New T`, `TypeOf x Is T`, and qualified library types
// like `DAO.Recordset` that appear inside `New`. Fuller type
// expressions land in vba-statement-and-module-grammar (#4).
// ============================================================

// Test entry point: a single VBA expression terminated by EOF.
// Statement-level rules (#4) wire `expression` into broader productions.
expressionEof
    : expression EOF
    ;

expression
    : <assoc=right> expression CARET expression                       # PowerExpr
    | op=(PLUS | MINUS) expression                                    # UnarySignExpr
    | expression op=(STAR | SLASH) expression                         # MulDivExpr
    | expression BACKSLASH expression                                 # IntDivExpr
    | expression MOD expression                                       # ModExpr
    | expression op=(PLUS | MINUS) expression                         # AddSubExpr
    | expression AMP expression                                       # ConcatExpr
    | expression op=(EQ | NEQ | LT | GT | LE | GE | LIKE | IS) expression  # CompareExpr
    | NOT expression                                                  # NotExpr
    | expression AND expression                                       # AndExpr
    | expression OR expression                                        # OrExpr
    | expression XOR expression                                       # XorExpr
    | expression EQV expression                                       # EqvExpr
    | expression IMP expression                                       # ImpExpr
    | typeOfExpr                                                      # TypeOfPrimary
    | newExpr                                                         # NewPrimary
    | addressOfExpr                                                   # AddressOfPrimary
    | lExpression                                                     # LExprPrimary
    ;

// `TypeOf reference-expression Is typeName` — Boolean-valued construct
// per MS-VBAL §5.2.6 (H-B06). The operand is an *object-reference*,
// not an arbitrary expression, so `TypeOf x = 3 Is T` is a syntax
// error (the user must write `(x = 3) And TypeOf y Is T`). Restricting
// to `referenceExpression` (instead of the broader `lExpression`) also
// rejects `TypeOf 1 Is T` and `TypeOf "x" Is T` syntactically — those
// are not object expressions per §5.2.6 even though `lExpression` would
// accept them through the literal primary.
typeOfExpr
    : TYPEOF referenceExpression IS typeName
    ;

// Reference expression — a value-position l-expression restricted to
// forms that can yield an object reference. Excludes the literal /
// special-constant primaries (True, False, Null, Empty, integer/string/
// date literals) that would syntactically pass through `lExpression`
// but cannot semantically be object references. Used by `TypeOf`.
//
// Calls and indexing are still allowed (`obj.col(1)` returns whatever
// `col(1)` evaluates to, which may be an object); bang access is
// allowed; implicit-With leading-./! is allowed.
referenceExpression
    : referenceExpression DOT memberName
    | referenceExpression BANG memberName
    | referenceExpression LPAREN argumentList RPAREN
    | DOT memberName
    | BANG memberName
    | LPAREN referenceExpression RPAREN
    | ME
    | IDENTIFIER
    | TYPED_IDENTIFIER
    ;

// `New ClassName` — object-creation expression per §5.2.5.
newExpr
    : NEW typeName
    ;

// `AddressOf <procedure-reference>` — function-pointer expression
// per MS-VBAL §5.6.4. The operand is a procedure name, optionally
// qualified by a module/class name. Calls (`AddressOf F()`), bang
// access (`AddressOf Forms!frm`), implicit-With (`AddressOf .X`), and
// literal operands are all spec violations and rejected here at the
// syntactic level — see `procedureReference`.
addressOfExpr
    : ADDRESSOF procedureReference
    ;

// Procedure-pointer reference per MS-VBAL §5.6.4. A simple identifier
// or a qualified `module.member` chain. `Me.MyMethod` is allowed (Me
// can start a member-access chain to a method-of-Me) but `Me` alone
// is not — `Me` is the class instance, not a procedure pointer, so
// `AddressOf Me` is rejected. Reserved keywords are allowed as the
// right-hand member name per §3.3.5.
procedureReference
    : (IDENTIFIER | TYPED_IDENTIFIER) (DOT memberName)*
    | ME (DOT memberName)+
    ;

// L-expression — left-side / location expression per MS-VBAL §5.6.5.
// Postfix-style left-recursion encodes member-access chains, indexing
// or calling, and the implicit-With leading-./! forms. The right-hand
// operand of `.` and `!` is a single member name (not an lExpression),
// so left-to-right binding yields the canonical chain shape per H-B01.
lExpression
    : lExpression DOT memberName                                     # MemberAccess
    | lExpression BANG memberName                                    # BangAccess
    | lExpression LPAREN argumentList RPAREN                         # IndexOrCall
    | DOT memberName                                                 # ImplicitWithMember     // H-C01
    | BANG memberName                                                # ImplicitWithBang       // H-C02
    | primary                                                        # PrimaryExpr
    ;

// Member name on the right side of `.` or `!`. Bracketed identifiers
// are already absorbed by the IDENTIFIER lexer rule (Hazard H-A08),
// so `Forms![Order Form].Field` matches `IDENTIFIER BANG IDENTIFIER
// DOT IDENTIFIER` at the token level.
//
// Per MS-VBAL §3.3.5, reserved keywords are allowed as member names
// after `.` or `!` because the syntactic position is unambiguous.
// `Forms!frm!ctl.Property`, `obj.End`, `obj.Type`, `obj.Name`, etc.
// must all parse cleanly. The `reservedKeywordName` alternative folds
// every reserved-keyword lexer token back into the member-name slot.
// Without this rule the lexer's keyword tokenization (necessary for
// statement-level parsing in #4) prevents valid Access expressions
// like control names that happen to equal reserved words.
memberName
    : IDENTIFIER
    | TYPED_IDENTIFIER
    | reservedKeywordName
    ;

// All reserved keyword tokens that may appear as a member name in
// l-expression chains. Listed exhaustively so the rule survives
// future lexer additions (the grammar fails fast — by mismatched
// input — on any keyword the lexer adds without updating this list).
reservedKeywordName
    : PUBLIC | PRIVATE | FRIEND | STATIC | DIM | CONST | GLOBAL
    | DECLARE | PTRSAFE | LIB | ALIAS | WITHEVENTS | IMPLEMENTS
    | SUB | FUNCTION | PROPERTY | GET | LET | SET | END | EXIT
    | BYVAL | BYREF | OPTIONAL | PARAMARRAY
    | AS | NEW | TYPE | ENUM | EVENT | RAISEEVENT | OPTION | EXPLICIT
    | COMPARE | PRESERVE
    | IF | THEN | ELSEIF | ELSE | SELECT | CASE | DO | LOOP | WHILE
    | UNTIL | WEND | FOR | EACH | IN | TO | STEP | NEXT | WITH
    | GOTO | GOSUB | RETURN | ON | ERROR_KW | RESUME | CALL | STOP
    | AND | OR | NOT | XOR | EQV | IMP | MOD | LIKE | IS
    | ADDRESSOF | TYPEOF
    | TRUE_KW | FALSE_KW | NOTHING | NULL_KW | EMPTY_KW | ME
    | REDIM | ERASE | DEBUG | PRINT
    ;

primary
    : literal                                                        # LiteralPrimary
    | ME                                                             # MeExpr
    | TRUE_KW                                                        # TrueExpr
    | FALSE_KW                                                       # FalseExpr
    | NOTHING                                                        # NothingExpr
    | NULL_KW                                                        # NullExpr
    | EMPTY_KW                                                       # EmptyExpr
    | DEBUG                                                          # DebugObjectExpr
    | IDENTIFIER                                                     # IdentExpr
    | TYPED_IDENTIFIER                                               # TypedIdentExpr
    | LPAREN expression RPAREN                                       # ParenExpr
    ;

literal
    : INTEGER_LITERAL
    | FLOAT_LITERAL
    | HEX_LITERAL
    | OCTAL_LITERAL
    | STRING_LITERAL
    | DATE_LITERAL
    ;

typeName
    : IDENTIFIER (DOT IDENTIFIER)*
    ;

// Argument list per §5.6.13. Empty positional arguments (H-D08) are
// preserved as missing positions; named arguments (H-D12) use `:=`
// (the COLON_EQ token, lexed in the lexical section).
//
// The form `argument? (COMMA argument?)*` matches empty (the bare
// `()` call), a single argument, multiple comma-separated arguments,
// and any combination of leading / trailing / embedded missing
// positions. The AST consumer treats every missing position as the
// `Missing` Variant per §5.5.
argumentList
    : argument? (COMMA argument?)*
    ;

argument
    : namedArgument
    | expression
    ;

namedArgument
    : IDENTIFIER COLON_EQ expression
    ;

// ============================================================
// Statement rules (Phase 1.4 statement section, session #4 part 1)
//
// Sources:
//   - docs/spec-study/ms-vbal-summary.md  — Section §5.3, §5.4, §5.5
//   - docs/spec-coverage/ms-vbal-coverage.csv §5.3.* / §5.4.* / §5.5
//   - docs/spec-coverage/hazard-list.md   — Section D
//   - docs/test-cases/01,05,07,09,11,12 (statement-rich fixtures)
//
// Module-level structure (procedure declarations, parameter lists,
// Type / Enum / Implements / WithEvents / Event / Declare / Option
// directives) lands in Session 2 of this set. Session 1 owns:
//   - Control-flow statements (If, Select Case, For, For Each, Do,
//     While/Wend, With)
//   - Error handling (On Error / Resume)
//   - Special / control transfer (GoTo, GoSub, Return, Exit, Stop, End)
//   - RaiseEvent (statement form)
//   - Local declarations (Dim, Static, Public, Private, Global,
//     ReDim, Const)
//   - Assignment (Set, Let — implicit and explicit) and call forms
//
// Hazards addressed here:
//   H-A03  Numeric line-number labels — `100`, `200`. Recognized at
//          statement-start position; the trailing `:` is optional for
//          numeric labels per §3.3.2.
//   H-A04  Compound statement separator `:` vs label `:` — labels
//          appear only at statement-start position; separator-`:`
//          appears between simple statements within a single line
//          (single-line If body, Case body).
//   H-D01  GoTo labels (alpha and numeric). Both forms accepted by
//          `labelOrZero` / `statementLabel`.
//   H-D02  GoSub / Return — modeled as distinct AST kinds
//          (`GosubStmt`, `ReturnStmt`). The legacy fact that a
//          `Return` outside an active GoSub is a runtime error is
//          *not* enforced here; it is downstream control-flow work.
//   H-D04  On Error variants — `On Error GoTo <label-or-0>`,
//          `On Error Resume Next`. The literal `0` after `GoTo` here
//          is the deactivation sentinel and is captured via
//          `labelOrZero`.
//   H-D05  Resume / Resume Next / Resume <label> / Resume 0 — all
//          four forms parsed. `Resume 0` is parsed as `RESUME label`
//          where the label is `0`; equivalence-to-bare-Resume is a
//          downstream concern.
//   H-D06  Single-line If with multiple statements via `:` —
//          `inlineStatementList` collects colon-separated inline
//          statements as the Then- and Else-bodies.
//   H-D07  Select Case with single-line bodies via `:` — handled in
//          `caseClause` by allowing a colon-separator after the case
//          expression list, with the body running until end-of-line.
//   H-D08  Empty positional arguments — call statement reuses the
//          existing `argumentList` rule which already preserves
//          missing positions via `argument? (COMMA argument?)*`.
//   H-D09  `Call Sub(x)` vs `Sub x` vs `Sub(x)` parens-trap —
//          `callStatement` has explicit `CALL`-prefixed and bare
//          forms. The bare form's parens-vs-no-parens distinction is
//          captured by whether the `lExpression` ends in an
//          `IndexOrCall` postfix; downstream resolver applies the
//          ByVal-coercion semantics.
//   H-D10  Exit kind preserved (`SUB | FUNCTION | PROPERTY | FOR | DO`).
//
// `With ... End With` is parsed structurally; semantic flattening of
// leading `.member`/`!key` against the With-target (H-C03/H-C04) is
// resolver work in resolver-references-and-with-blocks (#8).
//
// Implements / WithEvents / Public Event / Class_Initialize /
// Class_Terminate / Form-Report event signatures (Section F hazards)
// are module-level constructs and land in Session 2.
// ============================================================

// Test entry point: a sequence of statements (separated by NEWLINE
// or COLON) terminated by EOF. Used by Session 1 unit tests against
// statement-level fixtures. The module-level entry point still
// routes through `module` -> `moduleItem` (the placeholder rule),
// which Session 2 of this set replaces with real declarations.
statementListEof
    : statementListBody EOF
    ;

statementListBody
    : (NEWLINE | block)*
    ;

// One unit of work at statement-list position: an optional label
// followed by a logical-line of colon-separated statements, OR a
// bare label (label only, statement on a later line). The bare-label
// form is necessary for fixture 07's `NextRow:` which sits on its
// own line followed by `Next i` on the next line.
block
    : statementLabel singleLineStatementList?
    | singleLineStatementList
    ;

// A label at statement-start position. Per §3.3.2 a numeric label
// (line number) does not require a trailing colon, but accepts one;
// an alphanumeric label always requires the colon.
statementLabel
    : IDENTIFIER COLON
    | INTEGER_LITERAL COLON?
    ;

// A logical line of one-or-more colon-separated statements. Inline-
// safe statements are valid in any position; block-form statements
// (block If, For, Do, Select Case, With, While/Wend) consume their
// own NEWLINEs and are valid as a stand-alone single-element line.
// Mixing a block-form statement with later colon-separated entries
// on the same logical line is rejected by the parser because the
// block-form statement's right edge ends with its terminator
// keyword (`End If`, `Next`, `Loop`, etc.), which cannot also be
// followed by a `:` separator at the source level.
singleLineStatementList
    : statement (COLON statement)*
    ;

// Top-level statement choice. Block-opening forms come first so
// ANTLR's adaptive prediction sees their leading-keyword tokens
// before falling through to the call/assignment alternatives whose
// first set is the open lExpression first set.
statement
    : ifBlockStatement                                                # IfBlockStmt
    | ifSingleLineStatement                                           # IfSingleLineStmt
    | forNextStatement                                                # ForNextStmt
    | forEachStatement                                                # ForEachStmt
    | doLoopStatement                                                 # DoLoopStmt
    | whileWendStatement                                              # WhileWendStmt
    | selectCaseStatement                                             # SelectCaseStmt
    | withStatement                                                   # WithStmt
    | onErrorStatement                                                # OnErrorStmt
    | resumeStatement                                                 # ResumeStmt
    | gotoStatement                                                   # GotoStmt
    | gosubStatement                                                  # GosubStmt
    | returnStatement                                                 # ReturnStmt
    | exitStatement                                                   # ExitStmt
    | stopStatement                                                   # StopStmt
    | endStatement                                                    # EndStmt
    | raiseEventStatement                                             # RaiseEventStmt
    | localDeclStatement                                              # LocalDeclStmt
    | redimStatement                                                  # RedimStmt
    | constStatement                                                  # ConstStmt
    | setStatement                                                    # SetStmt
    | letStatement                                                    # LetStmt
    | callStatement                                                   # CallStmt
    ;

// --- 1. If statements (§5.4.1) ------------------------------------
//
// Block form: NEWLINE immediately follows THEN. Single-line form:
// at least one inline statement follows THEN on the same logical
// line. ANTLR LL(*) selects between the two by lookahead at the
// post-THEN token.

ifBlockStatement
    : IF expression THEN NEWLINE
        statementListBody
      (ELSEIF expression THEN NEWLINE statementListBody)*
      (ELSE NEWLINE statementListBody)?
      END IF
    ;

ifSingleLineStatement
    : IF expression THEN inlineStatementList (ELSE inlineStatementList)?
    ;

inlineStatementList
    : inlineStatement (COLON inlineStatement)*
    ;

// Inline-statement: anything that does not itself open a block. Used
// inside single-line If's Then/Else bodies and as the after-colon
// body of a single-line `Case <list>: <stmt>` (H-D06, H-D07).
inlineStatement
    : letStatement
    | setStatement
    | callStatement
    | gotoStatement
    | gosubStatement
    | returnStatement
    | exitStatement
    | stopStatement
    | endStatement
    | onErrorStatement
    | resumeStatement
    | raiseEventStatement
    | localDeclStatement
    | redimStatement
    | constStatement
    ;

// --- 2. For / For Each (§5.4.3, §5.4.4) ---------------------------

forNextStatement
    : FOR (IDENTIFIER | TYPED_IDENTIFIER) EQ expression TO expression
          (STEP expression)? NEWLINE
        statementListBody
      NEXT (IDENTIFIER | TYPED_IDENTIFIER)?
    ;

forEachStatement
    : FOR EACH (IDENTIFIER | TYPED_IDENTIFIER) IN expression NEWLINE
        statementListBody
      NEXT (IDENTIFIER | TYPED_IDENTIFIER)?
    ;

// --- 3. Do / Loop (§5.4.5) ----------------------------------------
//
// Four variants:
//   Do                   ... Loop                                   (infinite)
//   Do While e | Until e ... Loop                                   (pre-test)
//   Do                   ... Loop While e | Until e                 (post-test)
// The grammar accepts a condition at either or both ends; semantic
// rejection of two-condition forms is downstream (no fixture witness).

doLoopStatement
    : DO doLoopCondition? NEWLINE
        statementListBody
      LOOP doLoopCondition?
    ;

doLoopCondition
    : (WHILE | UNTIL) expression
    ;

// --- 4. While / Wend (§5.4.6) — legacy ----------------------------

whileWendStatement
    : WHILE expression NEWLINE
        statementListBody
      WEND
    ;

// --- 5. Select Case (§5.4.2) --------------------------------------
//
// `caseClause` accepts both the block form (NEWLINE introduces a
// multi-line body) and the single-line form (COLON introduces an
// inline statement chain on the same line — H-D07). `Case Else` is
// a distinct alternative that always ends the case sequence.

selectCaseStatement
    : SELECT CASE expression NEWLINE
        (NEWLINE)*
        caseClause*
        caseElseClause?
      END SELECT
    ;

caseClause
    : CASE caseExpressionList caseClauseBody
    ;

caseElseClause
    : CASE ELSE caseClauseBody
    ;

caseClauseBody
    : NEWLINE statementListBody
    | COLON inlineStatementList NEWLINE statementListBody
    ;

caseExpressionList
    : caseExpression (COMMA caseExpression)*
    ;

caseExpression
    : expression TO expression                          # CaseRange
    | IS comparisonOperator expression                  # CaseIsCompare
    | expression                                        # CaseValue
    ;

comparisonOperator
    : EQ | NEQ | LT | GT | LE | GE
    ;

// --- 6. With block (§5.4.7) ---------------------------------------

withStatement
    : WITH expression NEWLINE
        statementListBody
      END WITH
    ;

// --- 7. On Error / Resume (§5.4.11, §5.4.12) ----------------------

onErrorStatement
    : ON ERROR_KW GOTO labelOrZero                      # OnErrorGoto
    | ON ERROR_KW RESUME NEXT                           # OnErrorResumeNext
    ;

resumeStatement
    : RESUME (NEXT | labelOrZero)?
    ;

// `0` after `On Error GoTo` is the deactivation sentinel; after
// `Resume` it means "retry the failing statement" (equivalent to
// bare `Resume`). Both flavors share `labelOrZero` because at this
// position the literal `0` is just an INTEGER_LITERAL — the AST
// consumer interprets meaning from the surrounding statement kind.
labelOrZero
    : IDENTIFIER
    | INTEGER_LITERAL
    ;

// --- 8. GoTo / GoSub / Return (§5.4.9, §5.4.10) -------------------

gotoStatement
    : GOTO labelOrZero
    ;

gosubStatement
    : GOSUB labelOrZero
    ;

returnStatement
    : RETURN
    ;

// --- 9. Exit (§5.4.8) ---------------------------------------------

exitStatement
    : EXIT exitKind
    ;

exitKind
    : SUB | FUNCTION | PROPERTY | FOR | DO
    ;

// --- 10. Stop / End (§5.4 — terminate / breakpoint) ---------------
//
// `End` as a free-standing statement terminates the program; the
// `End <kind>` terminators (End If, End With, End Sub, End Select,
// End Type, End Enum, End Property, End Function) are all consumed
// by their enclosing block rules and do not reach `endStatement`.

stopStatement
    : STOP
    ;

endStatement
    : END
    ;

// --- 11. RaiseEvent (§6.1) ----------------------------------------
//
// Statement form: `RaiseEvent <eventName>[(<args>)]`. Optional
// argument-list parens; empty arglist `()` is accepted. The Public
// Event declaration (`Public Event Foo(...)`) is module-level and
// lands in Session 2.

raiseEventStatement
    : RAISEEVENT IDENTIFIER (LPAREN argumentList RPAREN)?
    ;

// --- 12. Local declarations (§4.4) --------------------------------
//
// `Dim`, `Static`, `Public`, `Private`, `Global` declaration forms
// for variables. Procedure-body scope only emits `Dim` / `Static`;
// module-level Public/Private/Global declarations share the same
// right-hand syntax and are reused by Session 2 inside the module
// rule. The declaration head set is intentionally permissive.

localDeclStatement
    : declHead variableDecl (COMMA variableDecl)*
    ;

declHead
    : DIM | STATIC | PUBLIC | PRIVATE | GLOBAL
    ;

// One declared variable. Type-decl suffix on the identifier
// (`i&`, `sErr$`) is the TYPED_IDENTIFIER token from the lexical
// section. An explicit `As <typeSpec>` may follow; if neither is
// present the variable is Variant. Array bounds (`a(1 To 10)` or
// `a(10)`) lex as a parenthesized list of arrayBound entries.

variableDecl
    : (IDENTIFIER | TYPED_IDENTIFIER)
      (LPAREN arrayBoundsList? RPAREN)?
      (AS typeSpec)?
    ;

arrayBoundsList
    : arrayBound (COMMA arrayBound)*
    ;

arrayBound
    : expression (TO expression)?
    ;

// --- 13. ReDim (§4.4) ---------------------------------------------

redimStatement
    : REDIM PRESERVE? redimDecl (COMMA redimDecl)*
    ;

redimDecl
    : (IDENTIFIER | TYPED_IDENTIFIER) LPAREN arrayBoundsList RPAREN (AS typeSpec)?
    ;

// --- 14. Const (§4.4) ---------------------------------------------
//
// Module-level Const may carry a Public/Private modifier; procedure-
// body Const omits the modifier. Both forms accepted.

constStatement
    : (PUBLIC | PRIVATE)? CONST constDecl (COMMA constDecl)*
    ;

constDecl
    : (IDENTIFIER | TYPED_IDENTIFIER) (AS typeSpec)? EQ expression
    ;

// --- 15. Type specification (statement-level) ---------------------
//
// Lightweight type spec for variable / parameter / return-type
// positions. Covers:
//   - Simple type names: `String`, `Long`, `Variant`, `Object`,
//     `Form`, etc. (every primitive type lexes as IDENTIFIER —
//     the lexer does not emit reserved tokens for primitive type
//     names per the spec study's recommendation).
//   - Qualified types: `DAO.Recordset`, `Excel.Application`.
//   - Fixed-length string: `String * <const-expression>` (rare;
//     `should` priority in the coverage matrix, no corpus witness,
//     accepted to avoid a parse error if the form arrives).
// Session 2 will extend this to cover array-element types
// (`Object()`) when ParamArray and array-typed parameters land.

typeSpec
    : typeName (STAR expression)?
    ;

// --- 16. Set / Let / Call (§5.3.1, §5.3.2, §5.5) ------------------
//
// `Set` is keyword-prefixed object-reference assignment. `Let` is
// the value-assignment statement; the LET keyword is rare in modern
// VBA but legal, so it is accepted as an optional prefix. The
// implicit-Let form (no LET keyword) is dominant.
//
// `Call` statement has three witnessed forms (H-D09):
//   (a) `Call <l-expression>` — the canonical Call-prefix form. Any
//       parens around the argument list belong to the l-expression's
//       trailing IndexOrCall postfix.
//   (b) `<l-expression>` (no Call keyword, no bare args) — bare
//       call. The l-expression's tail may itself be an IndexOrCall
//       (`MySub(x)`), which is the H-D09 parens-trap form.
//   (c) `<l-expression> <bare-arg-list>` (no Call keyword, no outer
//       parens) — the canonical no-Call call statement. The bare
//       arg list reuses `argumentList` so empty positionals are
//       preserved (H-D08).

setStatement
    : SET lExpression EQ expression
    ;

letStatement
    : LET? lExpression EQ expression
    ;

callStatement
    : CALL lExpression                                  # ExplicitCallStmt
    | lExpression bareCallArgumentList?                 # ImplicitCallStmt
    ;

// At-least-one-argument bare argument list. An interior or trailing
// missing argument is preserved as a `Missing` placeholder in the
// AST consumer (H-D08). A leading missing argument (`Foo , x`) has
// no corpus witness and is not accepted here; if a witness arrives,
// the rule expands to `argument? (COMMA argument?)+`.

bareCallArgumentList
    : argument (COMMA argument?)*
    ;

// ============================================================
// Lexical rules
// ============================================================

// ------------------------------------------------------------
// 1. White space, line continuation, line terminators (§3.3.1, §3.3.3)
// ------------------------------------------------------------
//
// Line continuation per §3.3.3 is `<WS>+ '_' <line-terminator>`. The
// spec frames it as a pre-lex pass that joins physical lines into
// logical lines. ANTLR4 does not have a pre-lex hook; the same effect
// is achieved by making LINE_CONTINUATION a single token sent to the
// skip channel — its match consumes the whitespace, underscore, and
// terminator as one atom, so the next NEWLINE token is emitted only
// when an actual statement-ending line break is reached.
//
// Hazard H-A01 (line-continuation `_` token disambiguation): the
// underscore here is preceded by required whitespace and followed
// immediately by a line terminator — neither condition is satisfied
// by an identifier ending in underscore (e.g., `my_var_`).

LINE_CONTINUATION
    : [ \t]+ '_' ('\r' '\n' | '\n' | '\r') -> skip
    ;

WS
    : [ \t]+ -> channel(HIDDEN)
    ;

// NEWLINE is significant — it is the primary statement terminator in
// VBA. Multiple physical newlines collapse into one logical statement
// boundary in the parser, not in the lexer.
NEWLINE
    : ('\r' '\n' | '\n' | '\r')
    ;

// ------------------------------------------------------------
// 2. Comments (§3.3.1)
// ------------------------------------------------------------
//
// Two forms: apostrophe and REM keyword. Hazard H-A07 covers the
// apostrophe-inside-string-literal case — handled implicitly by rule
// order: STRING_LITERAL appears earlier and consumes its enclosed
// apostrophes.
//
// Per §3.3.3 + §3.3.1, line continuation is resolved BEFORE comment
// recognition, so a `<WS>+ '_' <terminator>` sequence inside a
// comment body extends the comment across the next physical line.
// Both comment rules accept LINE_CONT_FRAGMENT inside their bodies
// to model this — the fragment must be the first alternative in the
// loop so it wins over the `~[\r\n]` single-character alternative on
// the trailing whitespace + underscore.
//
// REM disambiguation per spec: REM is a comment keyword only when
// followed by whitespace, end-of-line, or end-of-input — never when
// fused into an identifier such as `Remediate`. The optional body
// (`([ \t] ~[\r\n]*)?`) allows the bare-REM-at-EOL/EOF form; longest
// match against IDENTIFIER ensures `Remediate` still tokenizes as
// IDENTIFIER (length 9 > REM_COMMENT length 3).

LINE_COMMENT
    : '\'' ( LINE_CONT_FRAGMENT | ~[\r\n] )* -> channel(HIDDEN)
    ;

REM_COMMENT
    : R E M ( ( [ \t] | LINE_CONT_FRAGMENT ) ( LINE_CONT_FRAGMENT | ~[\r\n] )* )?
      {IsRemBoundary()}?
      -> channel(HIDDEN)
    ;

fragment LINE_CONT_FRAGMENT
    : [ \t]+ '_' ('\r' '\n' | '\n' | '\r')
    ;

// ------------------------------------------------------------
// 3. Preprocessor directive heads (§6)
// ------------------------------------------------------------
//
// Per the session-set scope, preprocessor directives are tokenized
// here so source containing `#If`/`#End If`/`#Const` does not crash
// the lexer; full evaluation lives in parser-infrastructure (#6).
//
// These rules must precede DATE_LITERAL: when a `#If VBA7 Then` line
// has no closing `#`, the date-literal rule cannot match and the
// directive token wins. Where both could match (e.g., a malformed
// `#If 1/1/2024#`), the first declared rule wins on equal length.
// Per the lessons-learned "tokenize but do not evaluate" framing,
// the parser-infrastructure session may need to revisit this with a
// lexer-mode if the corpus reveals real ambiguity.

HASH_CONST   : '#' C O N S T ;
HASH_ELSEIF  : '#' E L S E I F ;
HASH_ELSE    : '#' E L S E ;
HASH_END_IF  : '#' E N D [ \t]+ I F ;
HASH_IF      : '#' I F ;

// ------------------------------------------------------------
// 4. Date literal (§3.4.4) — Hazard H-A05
// ------------------------------------------------------------
//
// `#` ... `#` on a single physical line, content is locale-dependent
// free-form date/time text. The closing `#` must exist on the same
// logical line; otherwise the lexer falls through to HASH and the
// parser will reject. Type-suffix `#` (Double) and the date-literal
// `#` are disambiguated positionally by the parser (declaration vs.
// expression-leading position) — this token captures the literal
// form only.
//
// First-content-character restriction: ANTLR longest-match would
// otherwise let DATE_LITERAL consume preprocessor lines that contain
// a stray `#` later on the line (e.g., `#Const X = "#"`). Anchoring
// the first content character to a digit, hyphen, or whitespace
// keeps DATE_LITERAL from outrunning HASH_CONST/HASH_IF/etc. on such
// lines. Limitation: the textual-month form `#January 1, 2024#` is
// not currently captured — corpus uses numeric forms exclusively
// (fixtures 06, 12, 08), and a relaxed pattern lands when a corpus
// witness for the textual form arrives.

DATE_LITERAL
    : '#' [0-9 \t\-] ~[#\r\n]* '#'
    ;

// ------------------------------------------------------------
// 5. Numeric literals (§3.4.2, §3.4.3)
// ------------------------------------------------------------
//
// Hex (`&H...&?`) and octal (`&O...&?`) prefixes must be matched
// before the bare `&` token so ANTLR's longest-match preference
// captures them as single literal tokens. The optional trailing `&`
// on hex/octal forces Long type per §3.4.2.
//
// FLOAT_LITERAL covers fractional, leading-dot, and integer-with-
// exponent forms. The trailing-suffix branches (`100&`, `42#`, `7@`)
// MUST NOT consume operator characters that would otherwise be
// parsed as binary operators on adjacent literals. Specifically:
//
//   - `&` and `^` are excluded from INT_SUFFIX so `1&2` (theoretical
//     concat without spaces) tokenizes as INTEGER AMP INTEGER, not
//     INTEGER+& followed by INTEGER. Same logic for `2^3`.
//   - The bare-decimal-Long form (`100&` as a single Long-typed
//     literal) is therefore deferred — current corpus has no witness
//     (coverage matrix row "Type-declaration chars on literals" is
//     In_Corpus: N). Reintroducing requires a token-boundary
//     predicate or a lexer mode and will land in
//     vba-grammar-validation (#5) once a synthetic fixture exists.
//   - HEX_LITERAL and OCTAL_LITERAL still capture trailing `&`
//     because the leading `&H`/`&O` already disambiguates them from
//     a bare `&` operator before the suffix is reached.
//   - FLOAT_SUFFIX (`! # @`) is retained: those characters do not
//     also serve as binary operators in VBA expression syntax.

HEX_LITERAL
    : '&' [Hh] [0-9A-Fa-f]+ '&'?
    ;

OCTAL_LITERAL
    : '&' [Oo] [0-7]+ '&'?
    ;

FLOAT_LITERAL
    : [0-9]+ '.' [0-9]+ EXPONENT? FLOAT_SUFFIX?
    | '.' [0-9]+ EXPONENT? FLOAT_SUFFIX?
    | [0-9]+ EXPONENT FLOAT_SUFFIX?
    | [0-9]+ FLOAT_SUFFIX
    ;

INTEGER_LITERAL
    : [0-9]+ INT_SUFFIX?
    ;

fragment EXPONENT     : [EeDd] [+\-]? [0-9]+ ;
fragment FLOAT_SUFFIX : [!#@] ;
fragment INT_SUFFIX   : [%] ;

// ------------------------------------------------------------
// 6. String literal (§3.4.1)
// ------------------------------------------------------------
//
// Double-quote delimited; a doubled quote (`""`) is the only escape.
// Cannot span a line terminator — long strings use line-continuation
// with concatenation per the corpus pattern in fixtures 02, 03, 05.

STRING_LITERAL
    : '"' ('""' | ~["\r\n])* '"'
    ;

// ------------------------------------------------------------
// 7. Reserved keywords (§3.3.5.2)
// ------------------------------------------------------------
//
// Lexer rules for keywords precede IDENTIFIER so they win when input
// matches both. Case-insensitivity is implemented via fragment
// letter rules at the bottom of this file (Antlr4.Runtime 4.6.x does
// not support the `caseInsensitive` grammar option).
//
// This list covers the reserved-keyword set called out in
// ms-vbal-summary.md §5 plus those exercised by the 12-fixture
// corpus. Non-reserved keywords (Error, Step, Mid, Binary, Base,
// Compare, Database, Text, Len, etc.) are intentionally not
// tokenized here — per the spec study's recommendation, they are
// lexed as IDENTIFIER and disambiguated by the parser through
// production-rule context.

// Modifier and declaration keywords
PUBLIC      : P U B L I C ;
PRIVATE     : P R I V A T E ;
FRIEND      : F R I E N D ;
STATIC      : S T A T I C ;
DIM         : D I M ;
CONST       : C O N S T ;
GLOBAL      : G L O B A L ;
DECLARE     : D E C L A R E ;
PTRSAFE     : P T R S A F E ;
LIB         : L I B ;
ALIAS       : A L I A S ;
WITHEVENTS  : W I T H E V E N T S ;
IMPLEMENTS  : I M P L E M E N T S ;

// Procedure structure
SUB         : S U B ;
FUNCTION    : F U N C T I O N ;
PROPERTY    : P R O P E R T Y ;
GET         : G E T ;
LET         : L E T ;
SET         : S E T ;
END         : E N D ;
EXIT        : E X I T ;

// Parameter modifiers
BYVAL       : B Y V A L ;
BYREF       : B Y R E F ;
OPTIONAL    : O P T I O N A L ;
PARAMARRAY  : P A R A M A R R A Y ;

// Types
AS          : A S ;
NEW         : N E W ;
TYPE        : T Y P E ;
ENUM        : E N U M ;
EVENT       : E V E N T ;
RAISEEVENT  : R A I S E E V E N T ;
OPTION      : O P T I O N ;
EXPLICIT    : E X P L I C I T ;
COMPARE     : C O M P A R E ;       // 'Option Compare' contextual
PRESERVE    : P R E S E R V E ;     // 'ReDim Preserve' contextual

// Control flow
IF          : I F ;
THEN        : T H E N ;
ELSEIF      : E L S E I F ;
ELSE        : E L S E ;
SELECT      : S E L E C T ;
CASE        : C A S E ;
DO          : D O ;
LOOP        : L O O P ;
WHILE       : W H I L E ;
UNTIL       : U N T I L ;
WEND        : W E N D ;
FOR         : F O R ;
EACH        : E A C H ;
IN          : I N ;
TO          : T O ;
STEP        : S T E P ;
NEXT        : N E X T ;
WITH        : W I T H ;
GOTO        : G O T O ;
GOSUB       : G O S U B ;
RETURN      : R E T U R N ;
ON          : O N ;
ERROR_KW    : E R R O R ;
RESUME      : R E S U M E ;
CALL        : C A L L ;
STOP        : S T O P ;

// Operator keywords
AND         : A N D ;
OR          : O R ;
NOT         : N O T ;
XOR         : X O R ;
EQV         : E Q V ;
IMP         : I M P ;
MOD         : M O D ;
LIKE        : L I K E ;
IS          : I S ;
ADDRESSOF   : A D D R E S S O F ;
TYPEOF      : T Y P E O F ;

// Constants and identity
TRUE_KW     : T R U E ;
FALSE_KW    : F A L S E ;
NOTHING     : N O T H I N G ;
NULL_KW     : N U L L ;
EMPTY_KW    : E M P T Y ;
ME          : M E ;

// Statements
REDIM       : R E D I M ;
ERASE       : E R A S E ;
DEBUG       : D E B U G ;
PRINT       : P R I N T ;

// ------------------------------------------------------------
// 8. Operators and separators (§3.3, §5.6)
// ------------------------------------------------------------
//
// Hazards H-A02 (type-decl suffix vs. operator) and H-A06 (`=` as
// assignment vs. comparison vs. expression-leading) are resolved by
// the parser through production-rule context, not by the lexer.
// `&` here is bare concatenation OR the Long type suffix in
// declarations; the parser disambiguates. `&H` and `&O` were
// captured by HEX_LITERAL/OCTAL_LITERAL above before this rule fires.

LPAREN     : '(' ;
RPAREN     : ')' ;
LBRACE     : '{' ;
RBRACE     : '}' ;
DOT        : '.' ;
BANG       : '!' ;       // bang/dictionary-access; or Single suffix in decls
COMMA      : ',' ;
SEMICOLON  : ';' ;
COLON_EQ   : ':=' ;       // named-argument separator
COLON      : ':' ;
NEQ        : '<>' ;
LE         : '<=' ;
GE         : '>=' ;
EQ         : '=' ;
LT         : '<' ;
GT         : '>' ;
PLUS       : '+' ;
MINUS      : '-' ;
STAR       : '*' ;
SLASH      : '/' ;
BACKSLASH  : '\\' ;       // integer division
CARET      : '^' ;
AMP        : '&' ;        // concatenation OR Long suffix (parser disambiguates)
HASH       : '#' ;        // Double suffix (only reached when not date/preprocessor)
PERCENT    : '%' ;        // Integer suffix
AT         : '@' ;        // Currency suffix
DOLLAR     : '$' ;        // String suffix

// ------------------------------------------------------------
// 9. Identifiers (§3.3.5)
// ------------------------------------------------------------
//
// Hazard H-A08 (bracketed identifier): `[anything-but-]` is an
// alternate identifier form that allows reserved-word names. Inside
// brackets, the content is taken verbatim — case is preserved but
// not normalized for equality (per §3.3.5 the unbracketed form
// remains case-insensitive). The lexer captures the brackets as
// part of the token; the parser strips them when building symbols.
//
// Per hazard H-A02 the lexer captures the type-declaration suffix as
// part of the identifier token for typing purposes — the AST records
// both the base name and the declared type. `!` is the lone exception:
// because it also denotes the bang/dictionary-access operator
// (Hazard H-B01) the lexer cannot distinguish `rate!` (Single suffix
// in declaration) from `Forms!frm` (bang chain) without parser
// context. `!` is therefore left as a separate BANG token, and the
// parser composes `IDENTIFIER BANG` as a Single-typed name in
// declaration positions and as bang access elsewhere.
//
// TYPED_IDENTIFIER and IDENTIFIER share a prefix; ANTLR longest-match
// picks TYPED_IDENTIFIER when an unambiguous suffix is present and
// IDENTIFIER when bare. Bracketed identifiers `[...]` carry no
// suffix — the brackets already escape any name including reserved
// words.

TYPED_IDENTIFIER
    : [A-Za-z] [A-Za-z0-9_]* [%&#@$]
    ;

IDENTIFIER
    : [A-Za-z] [A-Za-z0-9_]*
    | '[' ~[\]\r\n]+ ']'
    ;

// ------------------------------------------------------------
// 10. Case-insensitive letter fragments
// ------------------------------------------------------------
//
// Antlr4.Runtime 4.6.6 (the version pinned by parser-foundations) does
// not support `options { caseInsensitive=true; }`. These fragments
// give every keyword rule a case-insensitive match without affecting
// IDENTIFIER (which preserves source case for diagnostics).

fragment A : [Aa] ;
fragment B : [Bb] ;
fragment C : [Cc] ;
fragment D : [Dd] ;
fragment E : [Ee] ;
fragment F : [Ff] ;
fragment G : [Gg] ;
fragment H : [Hh] ;
fragment I : [Ii] ;
fragment J : [Jj] ;
fragment K : [Kk] ;
fragment L : [Ll] ;
fragment M : [Mm] ;
fragment N : [Nn] ;
fragment O : [Oo] ;
fragment P : [Pp] ;
fragment Q : [Qq] ;
fragment R : [Rr] ;
fragment S : [Ss] ;
fragment T : [Tt] ;
fragment U : [Uu] ;
fragment V : [Vv] ;
fragment W : [Ww] ;
fragment X : [Xx] ;
fragment Y : [Yy] ;
fragment Z : [Zz] ;

// Unrecognized token capture — keeps the lexer from raising on novel
// input during this session's rule scope (e.g., expression operators
// that arrive in session 2). Sibling sets remove this catch-all once
// every grammatical token has a home.
ERROR_TOKEN : . ;
