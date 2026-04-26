# Parse Failure Log — VBA Grammar

> **Purpose:** Records parse-failure data from each harness run against
> the 12-fixture corpus (`docs/test-cases/`). One entry per session set
> that contributes grammar work. The initial entry (Session 3 of
> `vba-lexical-and-expression-grammar`) establishes the baseline.
> Subsequent entries from `vba-statement-and-module-grammar` (#4) and
> `vba-grammar-validation` (#5) expand this log with statement-level and
> edge-case coverage.
>
> **Note on scope:** The parse-failure log tracks failures at the *grammar
> boundary* — what the grammar accepted vs. rejected. Resolution results
> (whether a parsed construct is semantically correct) live in the
> resolver session sets (#11–#16). Schema / metadata failures (does the
> extracted JSON match the harvester schema) live in
> `schema-finalization-and-docs` (#17).

---

## Harness run: Session 3 of `vba-lexical-and-expression-grammar` (2026-04-26)

### Grammar state at this run

- **Lexical section:** Complete (Session 1 of this set).
- **Expression section:** Complete (Session 2 of this set): operator
  precedence, member chains (`.`/`!`), index/call expressions,
  TypeOf...Is, New, AddressOf, implicit-With leading-./!.
- **Statement / module section:** Stub only — `module : moduleItem* EOF`
  with `moduleItem : NEWLINE | ~NEWLINE+ NEWLINE?`. No actual
  declaration or statement rules yet (owned by #4).
- **Parser entry point used:** `module` rule (stub).

### Corpus

All 12 fixtures under `docs/test-cases/` plus the 5 target fixtures
specified by the session-3 spec (`01`, `02`, `03`, `06`, `07`).

### Results (baseline)

| Fixture | Status | Errors | ERROR_TOKENs | Notes |
|---|---|---|---|---|
| 01-simple-validation.bas | parsed | 0 | 0 | |
| 02-cascading-combo-subform.bas | parsed | 0 | 0 | |
| 03-with-block-shadowing.bas | parsed | 0 | 0 | |
| 04-saved-query-form-params.sql | parsed | 0 | 0 | |
| 05-dynamic-sql-construction.bas | parsed | 0 | 0 | |
| 06-calculated-expressions.txt | parsed | 0 | 0 | |
| 07-legacy-patterns.bas | parsed | 0 | 0 | |
| 08-unresolvable.bas | parsed | 0 | 0 | |
| 09-report-grouping-events.bas | parsed | 0 | 0 | |
| 10-class-with-events.cls | parsed | 0 | 0 | |
| 11-autoexec-startup-chain.bas | parsed | 0 | 0 | |
| 12-switchboard-navigation.bas | parsed | 0 | 0 | |

**Summary:** 12/12 parsed, 0 failures, 0 lexer error tokens.

### Analysis

**Lexer:** Clean across the entire corpus. The `ERROR_TOKEN : .` catch-all
lexer rule did not fire for any character in any of the 12 fixtures. This
confirms the lexer's token set covers all characters present in the
module-level VBA fixture corpus.

**Expression grammar:** The harness test is at module granularity (stub
module rule), so expression-level parse failures would not surface here
unless they produced error tokens. A targeted expression-extraction test
confirms the expression grammar handles representative corpus expressions
correctly (see Session 2 expression unit tests under
`tests/AccessHarvester.Vba.Grammar.Tests/Expressions/`).

**Statement grammar (not yet present):** The stub module rule swallows
entire lines via `~NEWLINE+ NEWLINE?`. Once `vba-statement-and-module-grammar`
(#4) replaces the stub with real declaration and statement rules, the
harness will surface actual statement-parse failures. The next entry in
this log should be authored by Session 1 or Session 2 of that set.

### Schema-validation correspondence (in-session, Session 3)

Coverage-matrix rows for **lexical** (§3.3–§3.4) and **expression**
(§5.1–§5.2, §5.6) sections vs. the grammar:

| Coverage row | Priority | Grammar coverage |
|---|---|---|
| §3.3.1 Comments (apostrophe + REM) | must | ✅ LINE_COMMENT, REM_COMMENT |
| §3.3.3 Line continuation | must | ✅ LINE_CONTINUATION (→ skip) |
| §3.3.5 Identifiers (bare + bracketed + typed) | must | ✅ IDENTIFIER, TYPED_IDENTIFIER |
| §3.3.5.2 Reserved keywords | must | ✅ Exhaustive keyword token list; reservedKeywordName for member-name positions |
| §3.4.1 String literals | must | ✅ STRING_LITERAL |
| §3.4.2 Numeric literals (int, hex, octal) | must | ✅ INTEGER_LITERAL, HEX_LITERAL, OCTAL_LITERAL |
| §3.4.3 Float literals | must | ✅ FLOAT_LITERAL (fractional, leading-dot, exponent, suffix forms) |
| §3.4.4 Date literals | must | ✅ DATE_LITERAL |
| §5.1.1 Operator precedence + arithmetic | must | ✅ PowerExpr, UnarySignExpr, MulDivExpr, IntDivExpr, ModExpr, AddSubExpr |
| §5.1.1 Comparison operators | must | ✅ CompareExpr (EQ/NEQ/LT/GT/LE/GE/LIKE/IS) |
| §5.1.1 Logical/bitwise operators | must | ✅ NotExpr, AndExpr, OrExpr, XorExpr, EqvExpr, ImpExpr |
| §5.1.1 String concat (&) | must | ✅ ConcatExpr |
| §5.2.1 Dot member access | must | ✅ MemberAccess (lExpression postfix) |
| §5.2.2 Bang operator | must | ✅ BangAccess (lExpression postfix); distinct node from MemberAccess |
| §5.2.3 Default property access | must | ⚠️ Semantic constraint, not syntactic. Grammar preserves l-expression shape; downstream resolver adds default-property dereference. No grammar rule possible without type info. |
| §5.2.4 Index expressions | must | ✅ IndexOrCall (lExpression postfix); unified form per H-B05 |
| §5.2.5 New expression | must | ✅ newExpr |
| §5.2.6 TypeOf...Is | should | ✅ typeOfExpr (uses referenceExpression — rejects literal operands per §5.2.6) |
| §5.6 Me / Nothing / Null / Empty keywords | must | ✅ In primary rule |
| §5.6.4 AddressOf | must | ✅ addressOfExpr (uses procedureReference — rejects calls, bang, implicit-With, bare Me per §5.6.4) |
| §5.4.7 Implicit-With leading `.member` | must | ✅ ImplicitWithMember alternative in lExpression (H-C01) |
| §5.4.7 Implicit-With leading `!key` | must | ✅ ImplicitWithBang alternative in lExpression (H-C02) |
| §5.5 Named arguments (`:=`) | must | ✅ namedArgument rule; COLON_EQ lexer token disambiguates from `:` + `=` (H-D12) |
| §5.5 Empty positional arguments | must | ✅ `argument? (COMMA argument?)*` in argumentList (H-D08) |
| §5.3–5.5 Statement-level constructs | must | ❌ Out of scope — owned by vba-statement-and-module-grammar (#4) |

**Verdict: PASSED** for lexical + expression scope. One semantic gap
(§5.2.3 default property access) is correctly deferred to resolver — no
grammar change is possible or appropriate without type information.

### Grammar bugs discovered and fixed

None. The harness run was clean; the schema-validation found no missing
coverage for the in-scope tiers.

### Open items for #4 (vba-statement-and-module-grammar)

1. Replace stub `moduleItem` with real declaration and statement rules.
2. Once statement rules land, the next harness run will produce actual
   parse failures for constructs not yet covered. The next entry in this
   log should document that initial statement-grammar failure baseline.
3. The `ERROR_TOKEN : .` catch-all in the lexer should be removed once
   #4 closes all remaining token gaps.
