"""Cross-provider session-verification for vba-lexical-and-expression-grammar Session 1."""
from __future__ import annotations
import importlib.util, sys, pathlib

REPO = pathlib.Path(__file__).resolve().parents[2]


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        str(REPO / "ai-router" / "__init__.py"),
        submodule_search_locations=[str(REPO / "ai-router")],
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


SESSION_SET = REPO / "docs" / "session-sets" / "vba-lexical-and-expression-grammar"
GRAMMAR = (REPO / "AccessHarvester.Vba.Grammar" / "Vba.g4").read_text(encoding="utf-8")
TESTS = (REPO / "tests" / "AccessHarvester.Vba.Grammar.Tests" / "Lexical" / "VbaLexerTests.cs").read_text(encoding="utf-8")
CSPROJ = (REPO / "AccessHarvester.Vba.Grammar" / "AccessHarvester.Vba.Grammar.csproj").read_text(encoding="utf-8")
SPEC = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
HAZARDS_A = "\n".join((REPO / "docs" / "spec-coverage" / "hazard-list.md").read_text(encoding="utf-8").splitlines()[0:106])

PROMPT = f"""You are verifying Session 1 of the
`vba-lexical-and-expression-grammar` session set in the
dabbler-access-harvester repo. The session lands the lexical section of
the ANTLR4 VBA grammar plus targeted unit tests, and wires ANTLR4 code
generation that parser-foundations Session 3 explicitly deferred to
this set.

## Your job

Read the artifacts below and report VERIFIED or ISSUES_FOUND. Be a
strict cross-provider second opinion — flag specific, actionable
findings rather than generic critique. Prefer false-positive
ISSUES_FOUND over false-negative VERIFIED. For each finding give:

- **Severity:** Critical / Major / Minor
- **Location:** file path + line numbers
- **Description:** what is wrong
- **Suggested fix:** concrete change

Build status:
- `dotnet build AccessHarvester.sln`: succeeded (only NU1903 transitive
  warning on Microsoft.Build.Utilities.Core via Antlr4BuildTasks; no
  grammar/test errors).
- `dotnet test AccessHarvester.sln --no-build`: 24 lexer tests passed,
  95 prior tests passed (119 total, 0 failures).

## Spec (Session 1 only)

```markdown
{SPEC}
```

## Hazard list — Section A (Lexical hazards H-A01..H-A09)

The session is expected to address these hazards. Read this carefully:

```markdown
{HAZARDS_A}
```

## File: AccessHarvester.Vba.Grammar/Vba.g4

```antlr
{GRAMMAR}
```

## File: tests/AccessHarvester.Vba.Grammar.Tests/Lexical/VbaLexerTests.cs

```csharp
{TESTS}
```

## File: AccessHarvester.Vba.Grammar/AccessHarvester.Vba.Grammar.csproj

```xml
{CSPROJ}
```

## Out of scope but present in the repo (do not flag as missing)

- **Expression grammar (primary, member access, call, unary, binary,
  assignment).** Owned by Session 2 of THIS set per spec.md. The bang
  operator's lexical-vs-parse status is acknowledged as a hazard
  (H-B01) but resolved at parser level in Session 2.
- **Statement and module grammar** (Dim, Sub, Function, Property, If,
  For, etc.). Owned by `vba-statement-and-module-grammar` (#4). The
  `module` parser rule in Vba.g4 is intentionally a placeholder that
  swallows tokens to end-of-line — real production rules land in #4.
- **Preprocessor evaluation.** Per spec, preprocessor directives are
  tokenized in this session but their full handling (selecting which
  branches survive) lives in `parser-infrastructure` (#6).
- **Non-reserved keywords** (Error, Step, Mid, Binary, Base, Compare,
  Database, Text, Len, Append, Output, Random, Read, Write, etc.).
  Per spec study recommendation, these are intentionally lexed as
  IDENTIFIER and the parser disambiguates by production context. Not
  a missing-keyword bug.
- **Three spec-seeded hazards (H-D05 Resume 0, H-D12 named-args,
  H-E07 ParamArray).** These are statement-level / parameter-level
  hazards owned by sibling set #4, not lexical.
- **Harness integration.** Running the parser-foundations harness
  against fixtures 01/02/03/06/07 is Session 3 of this set, not
  Session 1.
- **CSV coverage matrix tick-off.** Mechanical tightening including
  schema-validation against the coverage matrix is Session 3.
- **Existing csproj transitive warning** NU1903 on
  Microsoft.Build.Utilities.Core 17.8.3 ships from Antlr4BuildTasks
  12.8.0 itself; not addressable in this repo without dropping
  ANTLR codegen.

## What to focus on

1. **Lexical correctness.** Do the rules in Vba.g4 correctly tokenize
   the constructs the hazard list calls out? Is rule ordering
   correct (e.g., HEX_LITERAL before AMP, preprocessor before
   DATE_LITERAL)?
2. **Test coverage.** Do the unit tests genuinely exercise the
   hazards they cite? Are the assertions strong enough that bugs
   would actually fail the test?
3. **Hazard coverage gaps.** Any hazard A01-A09 that has neither a
   lexer rule path nor a test? Flag it.
4. **ANTLR-specific bugs.** Greedy/non-greedy issues, fragment-rule
   collisions, accidentally hidden tokens, channel mistakes.
5. **Build wiring.** Is the switch from Antlr4.Runtime 4.6.6 to
   Antlr4.Runtime.Standard 4.13 + Antlr4BuildTasks defensible? Are
   listener/visitor metadata correct? Does the package version
   conflict with anything else in the repo?

Output format: start with the line `VERIFIED` or `ISSUES_FOUND`,
followed by the findings list (or a one-paragraph confirmation if
verified). Do not paraphrase the input back at me — go straight to
your findings.
"""


def main() -> None:
    ar = load_ai_router()
    out_path = SESSION_SET / "session-reviews" / "session-001-round-1.md"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    result = ar.route(
        content=PROMPT,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=1,
    )
    # Persist BEFORE any print/log side effects (per lessons-learned).
    out_path.write_text(result.content, encoding="utf-8")
    print(f"Wrote {out_path}")
    print(f"Model: {result.model_name}")
    print(f"Tokens in/out: {result.input_tokens} / {result.output_tokens}")
    print(f"Cost: ${result.cost_usd:.4f}")


if __name__ == "__main__":
    main()
