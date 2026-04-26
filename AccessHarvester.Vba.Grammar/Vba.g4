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
