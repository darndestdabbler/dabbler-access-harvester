"""Closeout helper for vba-attribute-header-strip Session 1.

Generates the three routed closeout artifacts (in this order):
  1. change-log.md   via route(task_type="summarization", max_tier=1)
  2. next-session-set-recommendation.md via route(task_type="analysis")
The orchestrator hand-writes disposition.json afterwards (it is a
structured artifact, not narrative prose, so no routed call).

Per project-guidance:
- Always persist routed output to disk BEFORE any display/log side
  effects (cp1252 print() crashes have lost paid output in the past).
- Use task_type="summarization" + max_tier=1 for change-log to avoid
  Gemini 400s on >2KB prompts.
"""
from __future__ import annotations
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/vba-attribute-header-strip")
CHANGE_LOG = SESSION_SET / "change-log.md"
NEXT_REC = SESSION_SET / "next-session-set-recommendation.md"


CHANGE_LOG_PROMPT = """\
Write a markdown change-log.md for `vba-attribute-header-strip` Session 1
in this repo. This is a one-session set; Session 1 is also the last
session. Follow the structural shape of prior change-logs in this repo
(header block with Created/Sessions Ran/Total Routed Cost/Verification
Verdict; Summary; Deliverables; Per-session summary).

Use these inputs verbatim where they appear:

- **Created:** 2026-05-17
- **Sessions Ran:** 1 (one-session set)
- **Total Routed Cost:** ~$0.54 (ai-assignment Gemini Pro $0.006 + R1 verification gpt-5-4 $0.16 + R2 verification $0.13 + R3 verification $0.23 + this summarization + next-set recommendation)
- **Verification Verdict:** VERIFIED-after-fix (R3 against gpt-5-4 with reshaped context; R1 found 1 Major + 1 Minor both fixed; R2 raised an out-of-scope AstBuilder NRE flagged in R3's OOS disclaimer)
- **Prerequisite For Next Set:** F2 (`harvester-finish-canonical-golden-output`) — F2's per-object IR counts now reconcile against this set's regenerated parse-success-report goldens.

What this session delivered (state these clearly in Summary):
1. A new `VbaModuleBodyLoader` class with two entry points: `Load(rawSource) → (Body, Header)` (pure header strip) and `LoadAndParse(rawSource, options?, sourceFile?) → (Module, ParseResult)` (combines Load + Parse + BuildAst, attaching the header to the resulting Module AST node).
2. A new `ModuleHeaderMetadata` sealed record under `AccessHarvester.Vba.Parser/Ast/` with typed fields (`VersionMajor`, `VersionMinor`, `MultiUse`, `VbName`, `VbGlobalNameSpace`, `VbCreatable`, `VbPredeclaredId`, `VbExposed`) plus an `AdditionalAttributes` sorted-ordinal map for forward-compat.
3. An init-only `Header: ModuleHeaderMetadata?` property on the existing `Module` AST node — default null, doesn't break any positional-constructor call site.
4. A strict regex anchored at `\\A` that tolerates UTF-8 BOM and CRLF/LF endings and matches either (a) the full SaveAsText form `VERSION + BEGIN..END + Attribute+` or (b) the Attribute-only form (the dominant case in this repo's YAML mirror). `VERSION` and `BEGIN..END` are *coupled* (R1 fix): VERSION-without-BEGIN..END is malformed and is NOT heuristic-stripped.
5. The two production VBA-parsing call sites routed through the loader: `AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs:85` (`LoadAndParse`) and `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs:107` (two-step `Load` + `Parse` — see audit.md Follow-up Finding 1 for why this consumer skips `BuildAst`).
6. A 15-test loader unit-test suite at `tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs` covering canonical strip, both header shapes, idempotence, CRLF/BOM tolerance, class-module headers, all malformed/partial cases (incl. the R1-fix regression), in-body Attribute-lookalikes, and AdditionalAttributes ordering.
7. An end-to-end regression test at `tests/AccessHarvester.Pipeline.Tests/VbaAttributeHeaderStripRegressionTests.cs` that reads HHC's Form_frmReportParam_FY YAML mirror at runtime, runs `LoadAndParse`, and asserts no Error-severity diagnostics plus expected Header metadata.
8. Regenerated parse-success-report goldens for both fixtures. HomeHealthCare: parseOk 3 / 32 → 30 / 32 (94%). NorthwindDevEdition: parseOk 14 / 66 → 61 / 66 (92%). Both hit the spec's Stop-Rule thresholds (HHC ≥30; NW ≥60).
9. A call-site audit ledger at `docs/session-sets/vba-attribute-header-strip/audit.md` enumerating every `VbaParserPipeline.Parse(` call site, classifying each as production (routed) or test-with-hand-built-source (raw), and documenting two follow-up findings for the next set: the AstBuilder recovery NRE and the AST-equality implication of the new Header property.

Surface for human attention (these are real and worth a paragraph in the change-log's "Surfaced for human attention" section):
- The AstBuilder NRE on header-stripped HHC modules (Follow-up Finding 1) is a pre-existing bug masked by the previous parser-fails-on-header behavior. SymbolDbBuilder uses LoadAndParse and would crash on real HHC YAML; its current unit tests use hand-built sources so don't expose it. A follow-up session set should add defensive null-IDENTIFIER guards to BuildSubProcedure/BuildFunctionProcedure/BuildPropertyProcedure.
- 2 HHC + 5 Northwind modules still parseFailed after the strip — those are real grammar gaps (not header issues). Candidates for a future grammar-extension session.
- The workflow-doc vs. seek-AI-consensus-memory conflict that surfaced on R2 verifier disagreement — the orchestrator surfaced the (a)/(b)/(c)/(d) options to the human per workflow Step 7, but the new feedback memory directs the orchestrator to AI-consensus first. Step 9 reorganization review will propose aligning the workflow doc.

Total tests after the set: 1869 passed, 2 skipped (COM-gated synthetic-DB tests).

Format the change-log as markdown. Keep the prose tight and consumer-facing — this is read by a future orchestrator and by the human reviewing the set.
"""


NEXT_REC_PROMPT = """\
Author a `next-session-set-recommendation.md` for the
`vba-attribute-header-strip` set's closeout.

This repo IS plan-driven: see `docs/planning/strategy.md` and the
Phase 4 plan referenced in CLAUDE.md (the project is in Phase 4 —
mechanical harvester finish line). The standing plan reads in
priority order:
  - F1: harvester-finish-canonical-parse-success-report (DONE)
  - F1.5: vba-attribute-header-strip (THIS SET — completing now)
  - F2: harvester-finish-canonical-golden-output (NEXT — stub exists at docs/session-sets/harvester-finish-canonical-golden-output/spec.md)
  - F3+: Other harvester-finish-* sets per the plan

Confirm or deviate. Specifically:
1. Does the standing plan put F2 next? (Per the docs, yes.)
2. Are F2's prerequisites satisfied now? F2's spec says it depends on
   F1 + this set's regenerated goldens — those are now committed.
3. Recommend an orchestrator + effort for F2 Session 1. Mirror the
   pattern from the prior change-log:
   "Recommended orchestrator: Claude Opus 4.7 @ effort=high"
   when the first session of the set is heavy on spec elaboration /
   synthesis; otherwise Sonnet at high effort.
4. Surface any deviation candidates. The two notable ones from this
   set's audit:
   - The AstBuilder NRE (Follow-up Finding 1). Could justify a small
     "vba-astbuilder-recovery-guards" set BEFORE F2 since F2 will
     likely re-exercise LoadAndParse and re-expose the crash. Worth
     calling out as a sequencing question for the human.
   - The 7 still-failing modules across HHC + Northwind. Real grammar
     gaps. Worth calling out as a sequencing question.

Output a single markdown document. Lead with the recommendation. Then
list deviation candidates with rationale.
"""


def main() -> None:
    cl_result = route(
        content=CHANGE_LOG_PROMPT,
        task_type="summarization",
        max_tier=1,
        session_set=str(SESSION_SET),
        session_number=1,
    )
    CHANGE_LOG.write_text(cl_result.content, encoding="utf-8")

    next_result = route(
        content=NEXT_REC_PROMPT,
        task_type="analysis",
        session_set=str(SESSION_SET),
        session_number=1,
    )
    NEXT_REC.write_text(next_result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=1,
        step_number=14,
        step_key="session-1/change-log",
        description="Routed change-log.md (summarization, max_tier=1).",
        status="complete",
        api_calls=[{
            "model": cl_result.model_name,
            "taskType": "summarization",
            "inputTokens": cl_result.input_tokens,
            "outputTokens": cl_result.output_tokens,
            "costUsd": cl_result.cost_usd,
        }],
    )
    log.log_step(
        session_number=1,
        step_number=14,
        step_key="session-1/next-session-set-recommendation",
        description="Routed next-session-set-recommendation.md (analysis).",
        status="complete",
        api_calls=[{
            "model": next_result.model_name,
            "taskType": "analysis",
            "inputTokens": next_result.input_tokens,
            "outputTokens": next_result.output_tokens,
            "costUsd": next_result.cost_usd,
        }],
    )

    total = cl_result.cost_usd + next_result.cost_usd
    sys.stdout.write(
        f"OK change-log model={cl_result.model_name} cost=${cl_result.cost_usd:.4f}\n"
        f"OK next-rec   model={next_result.model_name} cost=${next_result.cost_usd:.4f}\n"
        f"OK closeout total: ${total:.4f}\n"
    )


if __name__ == "__main__":
    main()
