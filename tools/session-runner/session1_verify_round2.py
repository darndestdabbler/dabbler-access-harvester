"""Round-2 cross-provider session-verification after fixing Round-1 findings."""
from __future__ import annotations
import pathlib

from ai_router import route

REPO = pathlib.Path(__file__).resolve().parents[2]

SESSION_SET = REPO / "docs" / "session-sets" / "vba-lexical-and-expression-grammar"
GRAMMAR = (REPO / "AccessHarvester.Vba.Grammar" / "Vba.g4").read_text(encoding="utf-8")
TESTS = (REPO / "tests" / "AccessHarvester.Vba.Grammar.Tests" / "Lexical" / "VbaLexerTests.cs").read_text(encoding="utf-8")
ROUND1 = (SESSION_SET / "session-reviews" / "session-001-round-1.md").read_text(encoding="utf-8")
ISSUE_LOG = (SESSION_SET / "issue-logs" / "session-001-round-1.json").read_text(encoding="utf-8")

PROMPT = f"""You are running Round 2 of cross-provider verification on
Session 1 of `vba-lexical-and-expression-grammar`. Round 1 produced 1
Critical and 4 Major findings; the orchestrator has applied fixes.
Your job is to confirm each finding is genuinely fixed and to surface
any new issues introduced by the fixes.

Build status (post-fix):
- `dotnet build AccessHarvester.sln`: succeeded.
- `dotnet test AccessHarvester.sln --no-build`: 38 lexer tests
  passed, 95 prior tests passed (133 total, 0 failures).

## Round-1 review (verbatim)

```markdown
{ROUND1}
```

## Issue log (orchestrator's per-finding disposition)

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
- Non-reserved keywords (`Error`, `Step`, `Mid`, etc.) lexed as
  IDENTIFIER per spec study recommendation.
- Bare-decimal-Long literal `100&` reintroduction is explicitly
  deferred per the orchestrator's R1-1 disposition; coverage matrix
  has no corpus witness, and the deferral is recorded in the
  grammar comment.
- Textual-month date literal `#January 1, 2024#` is explicitly
  deferred per the orchestrator's R1-2 disposition; numeric forms
  are the only corpus witnesses.

## Your output

Start with `VERIFIED` or `ISSUES_FOUND` on the first line. For each
new or unresolved finding give Severity / Location / Description /
Suggested fix. If all Round-1 findings are resolved and no new
issues surface, output `VERIFIED` followed by a one-paragraph
confirmation that explicitly notes which Round-1 findings you
re-checked.
"""


def main() -> None:
    out_path = SESSION_SET / "session-reviews" / "session-001-round-2.md"
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
