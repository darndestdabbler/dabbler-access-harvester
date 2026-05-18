"""Round-2 cross-provider verification for
vba-attribute-header-strip Session 1. Same shape as R1; adds a
"Round 1 → Round 2 fix log" section pointing the verifier at the two
findings the orchestrator addressed since R1.
"""
from __future__ import annotations
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/vba-attribute-header-strip")
REVIEW_DIR = SESSION_SET / "session-reviews"
RAW_OUT = REVIEW_DIR / "session-001-r2.md"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def truncate(text: str, max_chars: int = 8000) -> str:
    if len(text) <= max_chars:
        return text
    return text[:max_chars] + f"\n\n... (truncated; {len(text) - max_chars} chars omitted)"


def file_block(label: str, path: Path, *, max_chars: int = 8000) -> str:
    if not path.exists():
        return f"### {label}: `{path}`\n\n*(file missing)*\n"
    return f"### {label}: `{path}`\n\n```\n{truncate(read(path), max_chars)}\n```\n"


R1_FIX_LOG = """\
Round 1 verifier returned ISSUES_FOUND with two findings; both are now addressed.

Finding 1 (Major) — `CanonicalHeader` regex allowed `VERSION + Attribute+`
without `BEGIN..END`:
- Fix: rewrote the regex so VERSION and BEGIN..END are coupled —
  either both appear together, or neither does. The whole
  VERSION..END preamble is now a single `(?: ... )?` group.
- Regression test: `Load_does_not_strip_when_version_followed_directly_by_attributes_without_begin_end`
  in tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs.
- Full Vba.Parser.Tests suite: 15 / 15 loader tests pass; full suite
  1869 passed, 2 skipped.

Finding 2 (Minor) — audit.md described P2 as `LoadAndParse(...).Result`
while the implementation uses two-step `Load + Parse`:
- Fix: updated the P2 row in audit.md to describe the actual
  two-step flow (no BuildAst), with an inline link to the
  Follow-up findings §1 entry that explains the workaround.
"""


def main() -> None:
    spec_excerpt = truncate(read(SESSION_SET / "spec.md"), max_chars=10000)
    audit = read(SESSION_SET / "audit.md")
    r1_review = read(SESSION_SET / "session-reviews" / "session-001.md")

    loader = read(Path("AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs"))
    metadata = read(Path("AccessHarvester.Vba.Parser/Ast/ModuleHeaderMetadata.cs"))
    module_ast = read(Path("AccessHarvester.Vba.Parser/Ast/Module.cs"))
    loader_tests = read(Path("tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs"))
    psr_gen = read(Path("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs"))

    prompt = f"""\
Round 2 verification for `vba-attribute-header-strip` Session 1. The
orchestrator addressed both findings from Round 1; please re-verify.

---

## Round 1 → Round 2 fix log

```
{R1_FIX_LOG}
```

---

## Round 1 verifier output (for reference)

```
{r1_review}
```

---

## Spec excerpt (unchanged from R1)

```
{spec_excerpt}
```

---

## Updated deliverables

{file_block("VbaModuleBodyLoader (regex rewritten)", Path("AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs"))}

{file_block("Loader unit tests (with malformed-VERSION regression)", Path("tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs"))}

{file_block("ParseSuccessReportGenerator (two-step Load + Parse)", Path("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs"), max_chars=12000)}

{file_block("Module AST node (unchanged from R1, included for context)", Path("AccessHarvester.Vba.Parser/Ast/Module.cs"))}

{file_block("ModuleHeaderMetadata (unchanged from R1)", Path("AccessHarvester.Vba.Parser/Ast/ModuleHeaderMetadata.cs"))}

### Audit ledger (updated)

```
{audit}
```

---

## Test summary post-fix

```
Loader unit tests: 15 / 15 passed (added Load_does_not_strip_when_version_followed_directly_by_attributes_without_begin_end).
End-to-end regression test: 1 / 1 passed.
Parse-success-report goldens: still 30 / 32 (HHC) and 61 / 66 (Northwind); regex tightening did not regress either fixture.
Full suite: 1869 passed, 2 skipped.
```

---

After your analysis, output a JSON block in this exact format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
"""

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    RAW_OUT.write_text(result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=1,
        step_number=13,
        step_key="session-1/verify-r2",
        description="Round-2 cross-provider session-verification after R1 fixes.",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )

    sys.stdout.write(
        f"OK verifier={result.model_name} input={result.input_tokens} "
        f"output={result.output_tokens} cost=${result.cost_usd:.4f}\n"
        f"R2 review saved to: {RAW_OUT}\n"
    )


if __name__ == "__main__":
    main()
