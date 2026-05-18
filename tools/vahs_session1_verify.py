"""End-of-session cross-provider verification for
vba-attribute-header-strip Session 1. Builds a verification prompt
containing the spec excerpt, every file Session 1 created or modified,
and the build/test summary; routes through
`route(task_type="session-verification")`; persists the raw verifier
output to docs/session-sets/vba-attribute-header-strip/session-reviews/
session-001.md.

Per project-guidance: persist routed output to disk BEFORE any
display/logging side effects (cp1252 print() crashes have lost paid
output in the past).
"""
from __future__ import annotations
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/vba-attribute-header-strip")
REVIEW_DIR = SESSION_SET / "session-reviews"
RAW_OUT = REVIEW_DIR / "session-001.md"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def truncate(text: str, max_chars: int = 8000) -> str:
    if len(text) <= max_chars:
        return text
    return text[:max_chars] + f"\n\n... (truncated; {len(text) - max_chars} chars omitted)"


def file_block(label: str, path: Path, *, max_chars: int = 8000) -> str:
    if not path.exists():
        return f"### {label}: `{path}`\n\n*(file missing — flag as a verifier finding if relevant)*\n"
    return f"### {label}: `{path}`\n\n```\n{truncate(read(path), max_chars)}\n```\n"


def main() -> None:
    spec_excerpt = truncate(read(SESSION_SET / "spec.md"), max_chars=10000)
    audit = read(SESSION_SET / "audit.md")
    assignment = read(SESSION_SET / "ai-assignment.md")

    loader = read(Path("AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs"))
    metadata = read(Path("AccessHarvester.Vba.Parser/Ast/ModuleHeaderMetadata.cs"))
    module_ast = read(Path("AccessHarvester.Vba.Parser/Ast/Module.cs"))
    symboldb = read(Path("AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs"))
    psr_gen = read(Path("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs"))
    loader_tests = read(Path("tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs"))
    regression_test = read(Path("tests/AccessHarvester.Pipeline.Tests/VbaAttributeHeaderStripRegressionTests.cs"))
    hhc_golden = read(Path("samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json"))
    nw_golden = read(Path("samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json"))

    build_test_summary = """\
Build:  0 Errors, 4 Warnings (all pre-existing CA1416 platform-availability
        and NU1903 transitive-dependency warnings).
Tests:  1868 passed, 2 skipped (Access COM-gated `SyntheticFixtureSnapshotTests`).
        Loader unit tests:                14 / 14 passed.
        End-to-end regression test:       1  / 1  passed (Form_frmReportParam_FY).
        Parse-success-report goldens:     2  / 2  regenerated; assertion passes.

Parse-success shift (Stop Rule check):
  HomeHealthCare:     parseOk 3  / 32 → 30 / 32  (≥30 threshold met)
  NorthwindDevEdition: parseOk 14 / 66 → 61 / 66 (≥60 threshold met)

Modules still parseFailed after strip (real grammar gaps, OUT of this set's scope):
  HHC:        2 modules
  Northwind:  5 modules
"""

    out_of_scope = """\
Files / concerns explicitly OUT of scope for this verification:
- `AstBuilder.BuildSubProcedure` NRE at AccessHarvester.Vba.Parser/Ast/AstBuilder.cs:281
  (pre-existing recovery-time NRE exposed when LoadAndParse → BuildAst
  ran over header-stripped HHC modules). Workaround in this set:
  ParseSuccessReportGenerator uses two-step Load + Parse (no BuildAst).
  Full follow-up in audit.md → "Follow-up findings" §1.
- Extending Vba.g4 to natively parse `Attribute VB_*` — the strip-at-loader
  decision is the spec contract; grammar extension is a separate scope.
- Re-emitting Header metadata into `kb/` / `ir/` JSON outputs — F2's call
  (harvester-finish-canonical-golden-output).
- Refactoring the YAML mirror itself — committed mirrors preserve the header
  for source-of-truth fidelity.
- Test-side call sites (VbaParserPipelineTests, Resolver tests, etc.) that
  hand-build pre-normalized VBA source. The raw `VbaParserPipeline.Parse`
  API stays available as the documented escape hatch for these; see
  audit.md "Test call sites — STAY raw".
"""

    prompt = f"""\
You are the cross-provider verifier for `vba-attribute-header-strip`
Session 1 of 1 — the one and only session in the set. The orchestrator
(Claude Opus 4.7) introduced a shared loader that strips the canonical
leading VBA module-header block before the parser sees it, captures
the stripped header as typed `ModuleHeaderMetadata` attached to the
`Module` AST, routed the two production consumers through the loader,
added unit + end-to-end-regression tests, and regenerated the
parse-success-report goldens.

Review the work for: correctness against the spec, soundness of the
header-strip logic (regex, idempotence, malformed-input handling),
the AST attach point's compatibility with existing parser tests, the
audit's accuracy (no production consumer left bypassing the loader),
and any defects in the regenerated goldens.

---

## Spec excerpt

```
{spec_excerpt}
```

---

## Session deliverables

{file_block("ModuleHeaderMetadata record", Path("AccessHarvester.Vba.Parser/Ast/ModuleHeaderMetadata.cs"))}

{file_block("VbaModuleBodyLoader (Load + LoadAndParse)", Path("AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs"))}

{file_block("Module AST node (Header property added)", Path("AccessHarvester.Vba.Parser/Ast/Module.cs"))}

{file_block("SymbolDbBuilder routing edit", Path("AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs"))}

{file_block("ParseSuccessReportGenerator routing edit", Path("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs"), max_chars=12000)}

{file_block("Loader unit tests", Path("tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs"))}

{file_block("End-to-end regression test", Path("tests/AccessHarvester.Pipeline.Tests/VbaAttributeHeaderStripRegressionTests.cs"))}

{file_block("Regenerated HHC golden", Path("samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json"))}

{file_block("Regenerated Northwind golden", Path("samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json"))}

### Audit ledger

```
{audit}
```

### ai-assignment.md

```
{assignment}
```

---

## Build / Test summary

```
{build_test_summary}
```

---

## Out of scope

```
{out_of_scope}
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
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    RAW_OUT.write_text(result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=1,
        step_number=13,
        step_key="session-1/verify",
        description="Cross-provider session-verification (Opus orchestrator → GPT-5.4 verifier).",
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
        f"Review saved to: {RAW_OUT}\n"
    )


if __name__ == "__main__":
    main()
