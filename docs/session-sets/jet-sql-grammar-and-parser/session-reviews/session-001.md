# Verification Round 1

- **Issue → Bracketed form/report references required by the spec are not parseable; fixture 04 fails on them.**  
  **Location →** `formReference`, `parameterName`, `primaryExpression`/`columnReference`.  
  **Fix →** Add a syntax path for bracketed roots like `[Forms]` / `[Reports]` followed by `!` / `.` segments. As written, `[Forms]![frmDistrictReport]![cboDistrict]` tokenizes as `BRACKETED_IDENTIFIER BANG BRACKETED_IDENTIFIER ...`, but no parser rule consumes that full sequence. The comment about “post-parse promotion” is not sufficient because the parse never succeeds. This blocks the `PARAMETERS` clause and all `WHERE`/expression references in fixture 04.

- **Issue → `selectLikeStatement` redundantly admits `unionStatement` twice, creating an avoidable ambiguity/dead branch.**  
  **Location →** `selectLikeStatement`, `transformStatement`.  
  **Fix →** Remove one of the duplicated `unionStatement` paths. Recommended shape: keep `selectLikeStatement : transformStatement | unionStatement;` and change `transformStatement` to only the `TRANSFORM ... PIVOT ...` form.

- **Issue → Common aggregate `COUNT(*)` is not accepted, weakening normal SELECT and TRANSFORM coverage.**  
  **Location →** `functionCall`, `expressionList`, `primaryExpression`.  
  **Fix →** Add a dedicated aggregate-call rule, or a function-argument rule that permits `COUNT(*)` (and only where appropriate). Current `functionCall : functionName LPAREN expressionList? RPAREN` cannot consume `*` because `STAR` is not an `expression`.

- **Issue → Standard Access crosstab aliasing after the TRANSFORM aggregate is not accepted.**  
  **Location →** `transformStatement`.  
  **Fix →** Allow an optional alias after the leading transform expression, e.g. `TRANSFORM expression (AS? columnAlias)? unionStatement PIVOT expression ...`. Typical Access crosstab queries use `TRANSFORM Count(*) AS CountOf... SELECT ... PIVOT ...`.

VERDICT: ISSUES_FOUND

---

# Verification Round 2

- Round-1 findings 1-4 are resolved.
- Fixture 04 now parses with the round-2 grammar.

- **Issue** → `formRefRoot` now overmatches any bracketed identifier, so ordinary qualified names like `[Order Details].[Quantity]` will be parsed as `formReference` instead of `columnReference`.
  **Location** → `formRefRoot : FORMS | REPORTS | bracketedIdentifier;` together with `primaryExpression` ordering (`formReference` before `columnReference`).
  **Fix** → Restrict bracketed roots to bracketed `[Forms]` / `[Reports]` only. Add dedicated lexer tokens such as `BRACKETED_FORMS` / `BRACKETED_REPORTS` (placed before `BRACKETED_IDENTIFIER`) or use a semantic predicate on `BRACKETED_IDENTIFIER` text; do not accept arbitrary `bracketedIdentifier` here.

- **Issue** → `STAR` is now accepted as the sole argument of any function, so invalid calls like `Nz(*)`, `DateDiff(*)`, or `Sum(*)` will parse.
  **Location** → `functionCall : functionName LPAREN functionArgs? RPAREN;` and `functionArgs : STAR | expressionList;`.
  **Fix** → Special-case `COUNT(*)` (or the exact supported function set) instead of permitting `STAR` generically. Example shape: keep normal functions on `expressionList?`, and add a dedicated `COUNT LPAREN STAR RPAREN` alternative.

VERDICT: ISSUES_FOUND

---

# Verification Round 3

VERDICT: VERIFIED
