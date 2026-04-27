grammar JetSql;

// Parser-side helpers used by gated semantic predicates. The C# target
// emits these inside the generated parser class; `_input` is the
// TokenStream and `IsBracketedFormsOrReports` peeks the next token's
// text so the formRefRoot predicate can distinguish a bracketed
// `[Forms]` / `[Reports]` from any other bracketed identifier without
// having to introduce dedicated lexer tokens.
@parser::members {
    private bool IsBracketedFormsOrReports() {
        var t = TokenStream.LT(1);
        if (t == null) return false;
        var text = t.Text;
        if (string.IsNullOrEmpty(text) || text.Length < 2 || text[0] != '[') return false;
        var inner = text.Substring(1, text.Length - 2);
        return string.Equals(inner, "Forms",   System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(inner, "Reports", System.StringComparison.OrdinalIgnoreCase);
    }
}

// ============================================================
// JET / ACE SQL Grammar — Phase 5.1
//
// ANTLR4 grammar for the JET/ACE SQL dialect used by Microsoft
// Access. Authored as Session 1 of session set
// `jet-sql-grammar-and-parser` per Phase 5.1 of
// docs/VBA-Parser-Migration-Harvester-Plan.md.
//
// Scope (per spec):
//   - SELECT (with WHERE, GROUP BY, HAVING, ORDER BY, TOP, DISTINCT,
//     subqueries, set ops UNION [ALL])
//   - JOIN variants (INNER, LEFT, RIGHT — SQL-89-ish)
//   - PARAMETERS declaration
//   - TRANSFORM ... PIVOT (CROSSTAB)
//   - Limited UPDATE / INSERT / DELETE
//   - Parameter brackets [Forms]![frmX]![ctlY] and named [Enter ID]
//   - IIf / Switch / Choose / Nz in expression positions
//   - Domain functions (DLookup, DSum, DCount, DMax, DMin, DAvg,
//     DFirst, DLast)
//   - Identifier escaping with brackets [Field With Spaces]
//   - Date literals #1/1/2024#
//   - String concatenation via & (and standard +)
//   - Boolean operators AND / OR / NOT / EQV / IMP / XOR
//
// Out of scope (deferred to later sessions or unsupported):
//   - Full DDL (CREATE TABLE / ALTER TABLE / DROP)
//   - Stored procedures, user-defined functions defined in SQL
//   - WITH OWNERACCESS OPTION clauses (not in fixture corpus)
//
// Test-case anchors: docs/test-cases/04-saved-query-form-params.sql
// (PARAMETERS, joins, IIf, Forms!X!Y, date math, Nz) and the SQL
// fragments embedded in docs/test-cases/05-dynamic-sql-construction.bas.
//
// Style mirrors AccessHarvester.Vba.Grammar/Vba.g4: lexical rules at
// the bottom, semantic productions at the top, comments call out
// JET-specific deviations from ISO SQL.
// ============================================================

// ------------------------------------------------------------
// Entry rules
// ------------------------------------------------------------
//
// `parse` is the top entry — a full statement script terminated by
// EOF. JET SQL allows a single trailing `;` but does not require
// statement separators in the corpus we target (saved queries are
// always exactly one statement). `sqlFragment` is the entry used
// when the parser is invoked on dynamic-SQL strings reconstructed
// from VBA concatenation, where the fragment may be partial.

parse
    : sqlStatement SEMI? EOF
    ;

sqlFragment
    : sqlStatement EOF
    ;

sqlStatement
    : parametersDecl? (selectLikeStatement | updateStatement | insertStatement | deleteStatement)
    ;

selectLikeStatement
    : transformStatement
    | unionStatement
    ;

// `transformStatement` and `unionStatement` are matched by separate
// alternatives above; `transformStatement` itself only produces the
// TRANSFORM ... PIVOT shape so the two paths do not overlap.

// ------------------------------------------------------------
// PARAMETERS declaration
// ------------------------------------------------------------
//
// JET PARAMETERS prefix declares typed parameters used elsewhere in
// the statement. Names may be bare identifiers or bracketed
// (commonly `[Forms]![frmX]![ctlY]` for form-driven saved queries
// or `[Enter ID]` for runtime prompts).

parametersDecl
    : PARAMETERS parameterDecl (COMMA parameterDecl)* SEMI?
    ;

parameterDecl
    : parameterName sqlType
    ;

parameterName
    : formReference
    | bracketedIdentifier
    | identifier
    ;

sqlType
    : (SHORT | LONG | INTEGER_KW | BYTE | SINGLE | DOUBLE | CURRENCY | DECIMAL_KW
       | DATETIME | DATE_KW | TIME_KW | BIT | YESNO | LOGICAL
       | TEXT | CHAR_KW | VARCHAR | LONGTEXT | MEMO
       | BINARY | LONGBINARY | OLEOBJECT | GUID_KW)
       (LPAREN INTEGER_LITERAL (COMMA INTEGER_LITERAL)? RPAREN)?
    ;

// ------------------------------------------------------------
// TRANSFORM ... PIVOT (CROSSTAB)
// ------------------------------------------------------------
//
// JET-specific. Wraps an aggregating SELECT and pivots one column
// into a row of result columns.

transformStatement
    : TRANSFORM expression (AS? columnAlias)? unionStatement
      PIVOT expression (IN LPAREN expressionList RPAREN)?
    ;

// ------------------------------------------------------------
// UNION
// ------------------------------------------------------------
//
// JET supports UNION [ALL]. INTERSECT / EXCEPT are not part of the
// dialect and are intentionally omitted.

unionStatement
    : selectStatement (UNION ALL? selectStatement)*
    ;

// ------------------------------------------------------------
// SELECT
// ------------------------------------------------------------

selectStatement
    : LPAREN unionStatement RPAREN
    | selectCore
    ;

selectCore
    : SELECT selectQuantifier? topClause? selectList
      fromClause?
      whereClause?
      groupByClause?
      havingClause?
      orderByClause?
    ;

selectQuantifier
    : ALL
    | DISTINCT
    | DISTINCTROW
    ;

topClause
    : TOP INTEGER_LITERAL PERCENT?
    ;

selectList
    : STAR
    | selectItem (COMMA selectItem)*
    ;

selectItem
    : tableQualifier DOT STAR                            # selectStarQualified
    | expression (AS? columnAlias)?                      # selectExpression
    ;

tableQualifier
    : identifier
    | bracketedIdentifier
    ;

columnAlias
    : identifier
    | bracketedIdentifier
    | STRING_LITERAL
    ;

// ------------------------------------------------------------
// FROM and JOINs
// ------------------------------------------------------------
//
// JET uses SQL-89-style nested join syntax with parentheses, e.g.
//   FROM ((tblA AS a LEFT JOIN tblB AS b ON ...) INNER JOIN tblC AS c ON ...)
// Multiple table sources are still permitted (comma cross-join).

fromClause
    : FROM tableSource (COMMA tableSource)*
    ;

tableSource
    : LPAREN tableSource RPAREN joinChain?
    | atomicTableSource joinChain?
    ;

atomicTableSource
    : tableName (AS? tableAlias)?                        # atomicTableNameSource
    | LPAREN unionStatement RPAREN (AS? tableAlias)?     # atomicSubquerySource
    ;

tableName
    : identifier
    | bracketedIdentifier
    ;

tableAlias
    : identifier
    | bracketedIdentifier
    ;

joinChain
    : joinClause+
    ;

joinClause
    : (INNER | (LEFT | RIGHT) OUTER?) JOIN tableSource ON expression
    ;

// ------------------------------------------------------------
// WHERE / GROUP BY / HAVING / ORDER BY
// ------------------------------------------------------------

whereClause
    : WHERE expression
    ;

groupByClause
    : GROUP BY expression (COMMA expression)*
    ;

havingClause
    : HAVING expression
    ;

orderByClause
    : ORDER BY orderItem (COMMA orderItem)*
    ;

orderItem
    : expression (ASC | DESC)?
    ;

// ------------------------------------------------------------
// UPDATE / INSERT / DELETE
// ------------------------------------------------------------

updateStatement
    : UPDATE tableSource SET assignmentList whereClause?
    ;

assignmentList
    : assignment (COMMA assignment)*
    ;

assignment
    : columnReference EQ expression
    ;

insertStatement
    : INSERT INTO tableName columnListClause? (valuesClause | unionStatement)
    ;

columnListClause
    : LPAREN columnReference (COMMA columnReference)* RPAREN
    ;

valuesClause
    : VALUES LPAREN expression (COMMA expression)* RPAREN
    ;

deleteStatement
    : DELETE selectList? fromClause whereClause?
    ;

// ------------------------------------------------------------
// Expressions
// ------------------------------------------------------------
//
// Expression precedence (low → high) matches Access/JET:
//   1. logical OR-tier (OR, EQV, IMP, XOR)
//   2. logical AND
//   3. logical NOT
//   4. comparison (=, <>, <, <=, >, >=, LIKE, BETWEEN, IS [NOT] NULL, IN)
//   5. concatenation (&)
//   6. additive (+, -)
//   7. multiplicative (*, /, \, MOD)
//   8. exponent (^)
//   9. unary (+, -)
//
// Implemented bottom-up via precedence-climbing alternatives.

expressionList
    : expression (COMMA expression)*
    ;

expression
    : orExpression
    ;

orExpression
    : andExpression ((OR | EQV | IMP | XOR) andExpression)*
    ;

andExpression
    : notExpression (AND notExpression)*
    ;

notExpression
    : NOT notExpression
    | comparisonExpression
    ;

comparisonExpression
    : concatExpression
        ( (EQ | NEQ | LT | LTE | GT | GTE) concatExpression
        | NOT? LIKE concatExpression
        | NOT? BETWEEN concatExpression AND concatExpression
        | IS NOT? NULL
        | NOT? IN LPAREN (expressionList | unionStatement) RPAREN
        )?
    ;

concatExpression
    : additiveExpression (AMP additiveExpression)*
    ;

additiveExpression
    : multiplicativeExpression ((PLUS | MINUS) multiplicativeExpression)*
    ;

multiplicativeExpression
    : exponentExpression ((STAR | SLASH | BACKSLASH | MOD) exponentExpression)*
    ;

exponentExpression
    : unaryExpression (CARET unaryExpression)*
    ;

unaryExpression
    : (PLUS | MINUS) unaryExpression
    | primaryExpression
    ;

primaryExpression
    : literal                                              # litPrimary
    | EXISTS LPAREN unionStatement RPAREN                  # existsPrimary
    | LPAREN unionStatement RPAREN                         # subqueryPrimary
    | LPAREN expression RPAREN                             # parenPrimary
    | iifFunction                                          # iifPrimary
    | switchFunction                                       # switchPrimary
    | chooseFunction                                       # choosePrimary
    | domainFunction                                       # domainFunctionPrimary
    | functionCall                                         # functionCallPrimary
    | formReference                                        # formRefPrimary
    | columnReference                                      # columnRefPrimary
    | parameterPlaceholder                                 # paramPlaceholderPrimary
    ;

// ------------------------------------------------------------
// Built-in / Access-special function calls
// ------------------------------------------------------------
//
// IIf / Switch / Choose / Nz are not reserved but appear so heavily
// in the corpus they get dedicated rules so the AST can label them
// distinctly from generic functionCall.

iifFunction
    : IIF LPAREN expression COMMA expression COMMA expression RPAREN
    ;

switchFunction
    : SWITCH LPAREN expression COMMA expression (COMMA expression COMMA expression)* RPAREN
    ;

chooseFunction
    : CHOOSE LPAREN expression (COMMA expression)+ RPAREN
    ;

domainFunction
    : domainFunctionName LPAREN expression COMMA expression (COMMA expression)? RPAREN
    ;

domainFunctionName
    : DLOOKUP | DSUM | DCOUNT | DMAX | DMIN | DAVG | DFIRST | DLAST
    ;

functionCall
    : COUNT LPAREN STAR RPAREN                              # countStarCall
    | functionName LPAREN expressionList? RPAREN            # genericFunctionCall
    ;

functionName
    : identifier
    | NZ          // common enough to whitelist alongside the closed function names
    | COUNT
    | SUM
    | AVG
    | MIN
    | MAX
    | FIRST
    | LAST
    ;

// ------------------------------------------------------------
// Form / parameter references
// ------------------------------------------------------------
//
// JET has two distinct member-access operators. `.` accesses
// declared properties (e.g., `Forms!frmX.Form.RecordSource`); `!`
// (the bang operator) accesses members of a default collection
// (e.g., `Forms!frmX!ctlY` resolves to the controls collection).
// Both must be preserved in the AST so the resolver can later
// attribute the reference correctly. Identifier segments may be
// bracketed when they contain spaces or reserved words.

formReference
    : formRefRoot (BANG | DOT) refSegment ((BANG | DOT) refSegment)*
    ;

// JET saved queries reference the Forms/Reports collections with
// either bare keywords (`Forms`, `Reports`) or bracketed names
// (`[Forms]`, `[Reports]`) — fixture 04 uses the bracketed form
// throughout its PARAMETERS and WHERE clauses. The bracketed
// alternative is gated by a semantic predicate so that ordinary
// qualified names like `[Order Details].[Quantity]` do not
// collide with the form-reference path; the AST builder strips
// the brackets after matching.
formRefRoot
    : FORMS
    | REPORTS
    | {IsBracketedFormsOrReports()}? bracketedIdentifier
    ;

refSegment
    : identifier
    | bracketedIdentifier
    ;

columnReference
    : tableQualifier DOT (identifier | bracketedIdentifier)
    | bracketedIdentifier
    | identifier
    ;

parameterPlaceholder
    : QUESTION
    ;

// ------------------------------------------------------------
// Literals
// ------------------------------------------------------------

literal
    : INTEGER_LITERAL                                      # integerLit
    | DECIMAL_LITERAL                                      # decimalLit
    | STRING_LITERAL                                       # stringLit
    | DATE_LITERAL                                         # dateLit
    | TRUE                                                 # trueLit
    | FALSE                                                # falseLit
    | NULL                                                 # nullLit
    ;

// ------------------------------------------------------------
// Identifiers
// ------------------------------------------------------------
//
// `identifier` accepts most non-reserved words plus a small set of
// context-keywords (e.g., FIRST/LAST/COUNT) when they appear in
// identifier position rather than as aggregate calls.

identifier
    : IDENTIFIER
    | nonReservedKeyword
    ;

bracketedIdentifier
    : BRACKETED_IDENTIFIER
    ;

nonReservedKeyword
    : NZ
    | TEXT
    | DATETIME
    | DATE_KW
    | TIME_KW
    | INTEGER_KW
    | DECIMAL_KW
    | CHAR_KW
    | VARCHAR
    | YESNO
    | LOGICAL
    | MEMO
    | LONGTEXT
    | LONGBINARY
    | OLEOBJECT
    | GUID_KW
    | PERCENT
    | FIRST
    | LAST
    ;

// ============================================================
// LEXER
// ============================================================
//
// JET SQL is case-insensitive on keywords; ANTLR4 case-insensitive
// fragments are spelled out via [aA] [bB] ... patterns.
//
// Whitespace is skipped. Block comments /* ... */ and line comments
// `--` are skipped. JET also recognizes leading `'` line comments
// inside dynamic-SQL strings emitted from VBA — but those are
// stripped by the VBA-side preprocessor before this grammar sees
// them, so we do not match `'`-comments here.

// ------------------------------------------------------------
// Keywords (alphabetical within group)
// ------------------------------------------------------------

ALL          : [aA][lL][lL] ;
AND          : [aA][nN][dD] ;
AS           : [aA][sS] ;
ASC          : [aA][sS][cC] ;
BETWEEN      : [bB][eE][tT][wW][eE][eE][nN] ;
BY           : [bB][yY] ;
DELETE       : [dD][eE][lL][eE][tT][eE] ;
DESC         : [dD][eE][sS][cC] ;
DISTINCT     : [dD][iI][sS][tT][iI][nN][cC][tT] ;
DISTINCTROW  : [dD][iI][sS][tT][iI][nN][cC][tT][rR][oO][wW] ;
EQV          : [eE][qQ][vV] ;
EXISTS       : [eE][xX][iI][sS][tT][sS] ;
FALSE        : [fF][aA][lL][sS][eE] ;
FORMS        : [fF][oO][rR][mM][sS] ;
FROM         : [fF][rR][oO][mM] ;
GROUP        : [gG][rR][oO][uU][pP] ;
HAVING       : [hH][aA][vV][iI][nN][gG] ;
IIF          : [iI][iI][fF] ;
IMP          : [iI][mM][pP] ;
IN           : [iI][nN] ;
INNER        : [iI][nN][nN][eE][rR] ;
INSERT       : [iI][nN][sS][eE][rR][tT] ;
INTO         : [iI][nN][tT][oO] ;
IS           : [iI][sS] ;
JOIN         : [jJ][oO][iI][nN] ;
LEFT         : [lL][eE][fF][tT] ;
LIKE         : [lL][iI][kK][eE] ;
MOD          : [mM][oO][dD] ;
NOT          : [nN][oO][tT] ;
NULL         : [nN][uU][lL][lL] ;
ON           : [oO][nN] ;
OR           : [oO][rR] ;
ORDER        : [oO][rR][dD][eE][rR] ;
OUTER        : [oO][uU][tT][eE][rR] ;
PARAMETERS   : [pP][aA][rR][aA][mM][eE][tT][eE][rR][sS] ;
PIVOT        : [pP][iI][vV][oO][tT] ;
REPORTS      : [rR][eE][pP][oO][rR][tT][sS] ;
RIGHT        : [rR][iI][gG][hH][tT] ;
SELECT       : [sS][eE][lL][eE][cC][tT] ;
SET          : [sS][eE][tT] ;
TOP          : [tT][oO][pP] ;
TRANSFORM    : [tT][rR][aA][nN][sS][fF][oO][rR][mM] ;
TRUE         : [tT][rR][uU][eE] ;
UNION        : [uU][nN][iI][oO][nN] ;
UPDATE       : [uU][pP][dD][aA][tT][eE] ;
VALUES       : [vV][aA][lL][uU][eE][sS] ;
WHERE        : [wW][hH][eE][rR][eE] ;
XOR          : [xX][oO][rR] ;

// Domain functions (closed list per Appendix A)
DLOOKUP      : [dD][lL][oO][oO][kK][uU][pP] ;
DSUM         : [dD][sS][uU][mM] ;
DCOUNT       : [dD][cC][oO][uU][nN][tT] ;
DMAX         : [dD][mM][aA][xX] ;
DMIN         : [dD][mM][iI][nN] ;
DAVG         : [dD][aA][vV][gG] ;
DFIRST       : [dD][fF][iI][rR][sS][tT] ;
DLAST        : [dD][lL][aA][sS][tT] ;

// Aggregate functions (kept as named tokens so AST can distinguish)
COUNT        : [cC][oO][uU][nN][tT] ;
SUM          : [sS][uU][mM] ;
AVG          : [aA][vV][gG] ;
MIN          : [mM][iI][nN] ;
MAX          : [mM][aA][xX] ;
FIRST        : [fF][iI][rR][sS][tT] ;
LAST         : [lL][aA][sS][tT] ;

// Special non-reserved words
NZ           : [nN][zZ] ;
SWITCH       : [sS][wW][iI][tT][cC][hH] ;
CHOOSE       : [cC][hH][oO][oO][sS][eE] ;
PERCENT      : [pP][eE][rR][cC][eE][nN][tT] ;

// SQL data-type keywords
SHORT        : [sS][hH][oO][rR][tT] ;
LONG         : [lL][oO][nN][gG] ;
INTEGER_KW   : [iI][nN][tT][eE][gG][eE][rR] ;
BYTE         : [bB][yY][tT][eE] ;
SINGLE       : [sS][iI][nN][gG][lL][eE] ;
DOUBLE       : [dD][oO][uU][bB][lL][eE] ;
CURRENCY     : [cC][uU][rR][rR][eE][nN][cC][yY] ;
DECIMAL_KW   : [dD][eE][cC][iI][mM][aA][lL] ;
DATETIME     : [dD][aA][tT][eE][tT][iI][mM][eE] ;
DATE_KW      : [dD][aA][tT][eE] ;
TIME_KW      : [tT][iI][mM][eE] ;
BIT          : [bB][iI][tT] ;
YESNO        : [yY][eE][sS][nN][oO] ;
LOGICAL      : [lL][oO][gG][iI][cC][aA][lL] ;
TEXT         : [tT][eE][xX][tT] ;
CHAR_KW      : [cC][hH][aA][rR] ;
VARCHAR      : [vV][aA][rR][cC][hH][aA][rR] ;
LONGTEXT     : [lL][oO][nN][gG][tT][eE][xX][tT] ;
MEMO         : [mM][eE][mM][oO] ;
BINARY       : [bB][iI][nN][aA][rR][yY] ;
LONGBINARY   : [lL][oO][nN][gG][bB][iI][nN][aA][rR][yY] ;
OLEOBJECT    : [oO][lL][eE][oO][bB][jJ][eE][cC][tT] ;
GUID_KW      : [gG][uU][iI][dD] ;

// ------------------------------------------------------------
// Punctuation and operators
// ------------------------------------------------------------

LPAREN     : '(' ;
RPAREN     : ')' ;
COMMA      : ',' ;
SEMI       : ';' ;
DOT        : '.' ;
BANG       : '!' ;
STAR       : '*' ;
SLASH      : '/' ;
BACKSLASH  : '\\' ;
PLUS       : '+' ;
MINUS      : '-' ;
CARET      : '^' ;
AMP        : '&' ;
EQ         : '=' ;
NEQ        : '<>' | '!=' ;
LT         : '<' ;
LTE        : '<=' ;
GT         : '>' ;
GTE        : '>=' ;
QUESTION   : '?' ;

// ------------------------------------------------------------
// Literals
// ------------------------------------------------------------
//
// JET strings may use either '...' or "...". The doubled-quote
// form ("''", '""') is the in-string escape. Date literals are
// fenced by `#` and contain a date and/or time fragment — they are
// matched lazily to the next `#` so the AST builder can re-parse
// them with the JET date-format rules.

STRING_LITERAL
    : '\'' ( '\'\'' | ~['\r\n] )* '\''
    | '"'  ( '""'   | ~["\r\n] )* '"'
    ;

DATE_LITERAL
    : '#' ~[#\r\n]* '#'
    ;

DECIMAL_LITERAL
    : [0-9]+ '.' [0-9]* ([eE] [+-]? [0-9]+)?
    | '.' [0-9]+ ([eE] [+-]? [0-9]+)?
    | [0-9]+ [eE] [+-]? [0-9]+
    ;

INTEGER_LITERAL
    : [0-9]+
    ;

// ------------------------------------------------------------
// Identifiers
// ------------------------------------------------------------
//
// JET allows bracketed identifiers `[Field With Spaces]` containing
// any character except `]` — including spaces, dots, bangs, and
// punctuation. The brackets are part of the lexeme; the AST builder
// strips them.

BRACKETED_IDENTIFIER
    : '[' ~[\]\r\n]+ ']'
    ;

IDENTIFIER
    : [a-zA-Z_] [a-zA-Z_0-9]*
    ;

// ------------------------------------------------------------
// Whitespace and comments
// ------------------------------------------------------------

LINE_COMMENT
    : '--' ~[\r\n]* -> skip
    ;

BLOCK_COMMENT
    : '/*' .*? '*/' -> skip
    ;

WHITESPACE
    : [ \t\r\n]+ -> skip
    ;
