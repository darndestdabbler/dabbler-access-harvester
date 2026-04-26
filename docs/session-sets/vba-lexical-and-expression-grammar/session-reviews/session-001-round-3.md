ISSUES_FOUND

- Confirmed: the Round-2 boundary regression is fixed. `REM:` / `REM.` / `REM(` no longer lex as `REM_COMMENT`, and bare `REM` at EOL/EOF still does.

1. **Issue** → **Major:** `REM` comments still miss the spec-required line-continuation case when the continuation marker is the first body content after the mandatory separator. `REM _\r\nstillCommented` is truncated as `REM_COMMENT("REM _")` followed by `NEWLINE` and normal tokens on the next line, instead of one hidden comment spanning both physical lines.  
   **Location** → `AccessHarvester.Vba.Grammar/Vba.g4` (`REM_COMMENT` / `LINE_CONT_FRAGMENT` interaction); missing regression coverage in `tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs` near the existing `RemKeyword_*` tests.  
   **Fix** → Restructure `REM_COMMENT` so the whitespace immediately after `REM` can participate in a leading `LINE_CONT_FRAGMENT`, rather than always being consumed separately before the body loop. Add lexer tests for `REM _\r\nstillCommented` and `REM\t_\nstillCommented` asserting a single hidden `REM_COMMENT` token spanning both lines.