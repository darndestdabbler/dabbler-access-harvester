# Parse Failure Log — VBA Grammar

> **Purpose:** Records parse-failure data from each harness run or
> grammar-authoring milestone. Corpus scope expanded to 15 VBA-module
> fixtures (10 module-level + 5 edge) by `vba-grammar-validation` (#5).
> The initial entry (Session 3 of `vba-lexical-and-expression-grammar`)
> establishes the baseline with the original 12-fixture corpus.
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

All 12 fixtures under `docs/test-cases/`, including the 5 target fixtures
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

---

## Grammar implementation: `vba-statement-and-module-grammar` (#4) (2026-04-26)

### Grammar state at this milestone

- **Statement section:** Complete — control flow (`If`/`For`/`Do`/`While`/
  `Select Case`/`With`), error handling (`On Error`), transfers
  (`GoTo`/`GoSub`/`Return`/`Exit`/`Stop`/`End`), `RaiseEvent`, local
  declarations (`Dim`/`Static`/`ReDim`/`Const`), assignment (`Set`/`Let`/`Call`).
- **Module section:** Complete — `Option` directives, `Implements`,
  module-level variables, module constants, `Type`/`Enum` definitions,
  `Declare`/`PtrSafe`, `Public Event`, `Sub`/`Function`/`Property`
  declarations. Parameter list enforces required-before-optional,
  `ParamArray` last.
- **Preprocessor directives:** Integrated into `moduleItem` and
  `statementListBody`.
- **Grammar state for downstream sets:** This milestone represents the
  first complete grammar; all 12 module-level test fixtures are
  parseable through the full `module` rule.

### Corpus harness run

Not applicable — the corpus harness (`VbaCorpusParserTests.cs`) was
authored by Session 2 of `vba-grammar-validation` (#5). Validation
during this milestone used 405 targeted unit tests covering individual
statement and module constructs, including 20+ reject tests.

**Covered by commit:** `d9e3139` (Merge session set
vba-statement-and-module-grammar) — merges all 4 sessions of grammar
authoring and unit tests into `main`.

### Results

| Fixture | Status | Notes |
|---|---|---|
| All 12 module-level fixtures | parsed | Validated via individual unit tests; corpus harness not yet built |

**Summary:** All module-level fixtures parseable after this milestone;
no corpus-harness run produced.

### Grammar implementation notes (milestone summary)

This section is a **milestone summary**, not an exhaustive per-failure log.
Grammar bugs in `vba-statement-and-module-grammar` were found and resolved
iteratively during rule authoring (not via a corpus harness run). The unit
test suite (405 tests, 20+ reject tests) is the authoritative record; test
failures during authoring guided grammar refinements. All work is covered
by merge commit `d9e3139`. The first corpus-harness-based failure log entry
is the `vba-grammar-validation` (#5) section below.

### Open items for #5 (vba-grammar-validation)

1. Build a full-corpus harness against all 15 VBA-module fixtures
   (10 module-level + 5 edge).
2. Run the harness and classify any failures under the grammar-bug /
   missing-construct / malformed-input / ambiguous taxonomy.
3. The `ERROR_TOKEN : .` catch-all in the lexer should be removed once
   all token gaps are confirmed closed by the harness run.

---

## Harness run: Session 2 of `vba-grammar-validation` (#5) (2026-04-26)

### Grammar state at this run

- **Complete grammar:** Lexical + expression (from #3) + statement +
  module (from #4). All `moduleItem` rules fully implemented;
  preprocessor directives integrated.
- **Edge fixtures added this set:** 5 fixtures under
  `docs/test-cases/edge/` (01–05), covering hazards H-D05, H-D12,
  H-E07, H-A07, H-D04.
- **Parser entry point used:** `module` rule (full implementation).

### Corpus

| Source | Files | Count |
|---|---|---|
| Module-level fixtures | `docs/test-cases/*.bas` + `*.cls` | 10 (9 `.bas` + 1 `.cls`) |
| Edge fixtures | `docs/test-cases/edge/*.bas` + `*.cls` | 5 `.bas` |
| **Total** | | **15 VBA-module fixtures** |

Excluded (not VBA-module scope): `04-saved-query-form-params.sql` and
`06-calculated-expressions.txt` — these exercise the JET-SQL and Access
expression grammars respectively and are out of scope for the `module`
start rule.

Edge fixtures (5) were authored in Session 1 of this set (`565319e`,
Session 1 of 4: Edge-Case Fixture Authoring, vba-grammar-validation).

### Initial harness run results (before fix)

| Fixture | Status | Errors | Notes |
|---|---|---|---|
| 01-simple-validation.bas | parsed | 0 | |
| 02-cascading-combo-subform.bas | parsed | 0 | |
| 03-with-block-shadowing.bas | parsed | 0 | |
| 05-dynamic-sql-construction.bas | parsed | 0 | |
| 07-legacy-patterns.bas | parsed | 0 | |
| 08-unresolvable.bas | parsed | 0 | |
| 09-report-grouping-events.bas | parsed | 0 | |
| 10-class-with-events.cls | parsed | 0 | |
| 11-autoexec-startup-chain.bas | parsed | 0 | |
| 12-switchboard-navigation.bas | parsed | 0 | |
| 01-h-d05-resume-forms.bas | parsed | 0 | |
| 02-h-d12-named-arguments.bas | parsed | 0 | |
| 03-h-e07-param-array.bas | parsed | 0 | |
| **04-h-a07-rem-comments.bas** | **FAILED** | 1 | Trailing `:` before hidden REM comment |
| 05-h-d04-on-error-goto-zero.bas | parsed | 0 | |

**Summary:** 14/15 parsed, 1 failure.

### Grammar bug discovered and fixed

| # | Hazard | Fixture | Location | Classification | Resolution |
|---|---|---|---|---|---|
| GF-001 | H-A07 (REM comment) | `04-h-a07-rem-comments.bas` | `Vba.g4`, `singleLineStatementList` | Grammar bug — rule did not allow trailing `:` before a comment token hidden on the HIDDEN channel | Fixed in commit `c6ebeab` (Session 2 of 4: Grammar Fix Loop, vba-grammar-validation): added `COLON?` tail to `singleLineStatementList: statement (COLON statement)* COLON?` |

**Classification detail (GF-001):** The grammar rule `singleLineStatementList`
allowed `stmt (: stmt)*` but not `stmt :` with nothing after the colon
(because `REM` comments are on the `HIDDEN` channel). The failing fixture
`04-h-a07-rem-comments.bas` contains `stmt : REM comment` — the parser
sees `stmt COLON <hidden> NEWLINE`, and without `COLON?` at the end of the
rule the trailing colon matched nothing. The fix (`COLON?`) is slightly
over-accepting (it also accepts bare `stmt :` at end-of-line without any
comment), but this is correct behavior under the current lexer design
where comments are fully hidden. The over-acceptance was noted by the
cross-provider verifier and accepted as an intentional minimal fix; strict
acceptance would require making end-of-line comments parser-visible.

### Final harness run results (after fix)

| Fixture | Status | Notes |
|---|---|---|
| All 15 VBA-module fixtures | **parsed** | 0 errors each |

**Summary:** 15/15 parsed. 0 failures. Grammar target achieved.

### Schema-validation correspondence (in-session, Session 3 of #5)

Routed `code-review` (mechanical-correspondence) in Session 3 of this
set. Initial pass: **ISSUES_FOUND** (2 Major, 3 Minor, 1 Info) — issues
corrected mechanically (added commit refs, updated header corpus count,
added Results/Grammar-bugs tables to #4 section, corrected GF-001 commit
label, added 565319e ref, removed placeholder). No independent
re-verification of the schema-validation cross-cut was performed; the
session-level cross-provider verification (Step 6) covers the full
deliverable including this section.

### Grammar bugs "won't-fix"

None. All failures in this set were fixed. GF-001 has one known
theoretical limitation (over-acceptance of bare trailing `:`) that is
accepted-by-design per the verifier discussion above.

### Open items for #6 (parser-infrastructure)

1. The `singleLineStatementList` `COLON?` over-acceptance is noted in
   `session-reviews/session-002.md` as a future improvement: if strict
   acceptance is needed, make end-of-line comments parser-visible rather
   than hiding them.
2. The `ERROR_TOKEN : .` catch-all lexer rule should be removed in #6
   once preprocessor and all remaining token gaps are confirmed closed.
