"""Round-3 cross-provider session-verification (final retry per workflow Step 7)."""
from __future__ import annotations
import pathlib

from ai_router import route

REPO = pathlib.Path(__file__).resolve().parents[2]

SESSION_SET = REPO / "docs" / "session-sets" / "vba-lexical-and-expression-grammar"
GRAMMAR = (REPO / "AccessHarvester.Vba.Grammar" / "Vba.g4").read_text(encoding="utf-8")
TESTS = (REPO / "tests" / "AccessHarvester.Vba.Grammar.Tests" / "Lexical" / "VbaLexerTests.cs").read_text(encoding="utf-8")
ROUND2 = (SESSION_SET / "session-reviews" / "session-001-round-2.md").read_text(encoding="utf-8")
ISSUE_LOG = (SESSION_SET / "issue-logs" / "session-001-round-2.json").read_text(encoding="utf-8")

PROMPT = f"""Round 3 (final retry) of cross-provider verification for
Session 1 of `vba-lexical-and-expression-grammar`. Round 2 found one
Major regression in REM_COMMENT (semantic-predicate gate now applied).
Confirm fix; flag any remaining issues.

Build status (post-fix):
- `dotnet build AccessHarvester.sln`: succeeded.
- `dotnet test AccessHarvester.sln --no-build`: 41 lexer tests passed,
  95 prior tests passed (136 total, 0 failures).

## Round-2 review (verbatim)

```markdown
{ROUND2}
```

## Round-2 issue log (orchestrator's per-finding disposition)

```json
{ISSUE_LOG}
```

## Updated file: AccessHarvester.Vba.Grammar/Vba.g4

```antlr
{GRAMMAR}
```

## Updated file: tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs

```csharp
{TESTS}
```

## Out of scope (do not flag as missing)

- Expression and statement grammar (sibling Sessions 2-4 / set #4).
- Preprocessor evaluation beyond tokenization (set #6).
- Non-reserved keywords lexed as IDENTIFIER per spec study.
- Bare-decimal-Long literal `100&` deferred (no corpus witness).
- Textual-month date literal `#January 1, 2024#` deferred (no corpus
  witness).
- The boundary set in `IsRemBoundary` intentionally treats whitespace
  as non-boundary (the rule has already consumed the optional body
  on the with-content path; only the bare-REM path needs CR/LF/EOF).

## Your output

Start with `VERIFIED` or `ISSUES_FOUND`. If verified, give a
one-paragraph summary citing which earlier findings were re-checked.
"""


def main() -> None:
    out_path = SESSION_SET / "session-reviews" / "session-001-round-3.md"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    result = route(
        content=PROMPT,
        task_type="session-verification",
        complexity_hint=85,
        session_set=str(SESSION_SET),
        session_number=1,
    )
    out_path.write_text(result.content, encoding="utf-8")
    print(f"Wrote {out_path}")
    print(f"Model: {result.model_name}")
    print(f"Tokens in/out: {result.input_tokens} / {result.output_tokens}")
    print(f"Cost: ${result.cost_usd:.4f}")


if __name__ == "__main__":
    main()
