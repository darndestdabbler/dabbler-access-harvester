ISSUES_FOUND

1. **Severity:** Critical  
   **Location:** `AccessHarvester.Vba.Grammar/Vba.g4:155-168, 327-328`; `tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs:127-169`  
   **Description:** `INTEGER_LITERAL` greedily absorbs `&` and `^` via `INT_SUFFIX`, so ANTLR longest-match will mis-tokenize valid operator forms with no whitespace. Examples: `1&2` becomes `INTEGER_LITERAL("1&"), INTEGER_LITERAL("2")` instead of `INTEGER_LITERAL, AMP, INTEGER_LITERAL`; `2^3` becomes `INTEGER_LITERAL("2^"), INTEGER_LITERAL("3")` instead of `INTEGER_LITERAL, CARET, INTEGER_LITERAL`. This will break concatenation and exponent expressions as soon as Session 2 relies on these tokens. The current tests only cover suffix literals like `100&`, not the operator-adjacency failure cases.  
   **Suggested fix:** Do not unconditionally include operator characters in numeric-suffix matching. Either:
   - remove `&`/`^` from `INT_SUFFIX` for now, or
   - guard suffix consumption with a token-boundary predicate so suffixes are only accepted before whitespace/delimiters/end-of-input.  
   Add lexer tests for `1&2`, `1&"x"`, and `2^3` expecting `AMP`/`CARET` operator tokens.

2. **Severity:** Major  
   **Location:** `AccessHarvester.Vba.Grammar/Vba.g4:102-114, 128-129`; `tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs:184-200`  
   **Description:** The preprocessor/date-literal disambiguation is incorrect for ANTLR. Rule order does not save `HASH_IF`/`HASH_CONST` when `DATE_LITERAL` can match a longer span from the same leading `#`. Valid directive lines that contain another `#` later on the line, such as `#Const X = "#"` or `#If Foo("#") Then`, will be lexed starting as `DATE_LITERAL` instead of directive tokens. The existing tests only cover simple directive lines without a later `#`, so this bug is unpinned.  
   **Suggested fix:** Use a dedicated lexer mode or whole-line directive rules for preprocessor lines so they beat `DATE_LITERAL` on match length, or tighten `DATE_LITERAL` so it cannot start with directive heads. Add regression tests for `#Const X = "#"` and a `#If ... "#"... Then` case.

3. **Severity:** Major  
   **Location:** `AccessHarvester.Vba.Grammar/Vba.g4:54-56, 86-91`; `tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs:63-78, 92-103`  
   **Description:** Comment handling does not implement the H-A07 / §3.3.3 rule that line continuation is resolved before comment recognition. For input like `' comment _\r\nnextLine`, the current lexer emits a hidden `LINE_COMMENT`, then a `NEWLINE`, then tokens for `nextLine`. Per the hazard list, the continuation should make the next physical line part of the same logical comment.  
   **Suggested fix:** Allow comment rules to consume continuation atoms inside comment text, e.g. include `([ \t]+ '_' ('\r''\n'|'\n'|'\r'))` as part of `LINE_COMMENT`/`REM_COMMENT` content and stop only at the final non-continued newline. Add a test where a trailing-underscore comment suppresses the next physical line.

4. **Severity:** Major  
   **Location:** `AccessHarvester.Vba.Grammar/Vba.g4:79-91`; `tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs:72-78`  
   **Description:** `REM_COMMENT` requires a space/tab after `REM`, so valid bare `REM` comments at end-of-line or EOF are mis-tokenized as `IDENTIFIER`. The source comment explicitly defers this, but Session 1’s lexical scope includes comment forms and H-A07 calls out REM comments as required synthetic coverage.  
   **Suggested fix:** Accept `REM` followed by whitespace, newline, or EOF. For example, broaden the rule to allow an empty comment body and terminate on line end/end-of-input. Add tests for `REM\n` and `REM` at EOF.

5. **Severity:** Major  
   **Location:** `AccessHarvester.Vba.Grammar/Vba.g4:346-355`; `tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs:205-223`  
   **Description:** Identifier type-declaration suffixes are intentionally emitted as separate tokens (`sFilePath$` → `IDENTIFIER DOLLAR`, `i&` → `IDENTIFIER AMP`). That directly diverges from H-A02, which requires suffixes to be consumed as part of the identifier token for typing purposes. Deferring all suffix composition to later parser work is a spec-level behavioral deviation, not just an implementation detail.  
   **Suggested fix:** Emit dedicated typed-identifier tokens for the unambiguous suffixes (`$`, `%`, `&`, `#`, `@`) and normalize the base name in the parser/AST; only keep `!` parser-disambiguated if needed for bang access. Update the tests to cover all suffix kinds under the chosen strategy.