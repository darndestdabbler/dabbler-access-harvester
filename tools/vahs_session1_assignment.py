"""One-shot helper: route the ai-assignment.md analysis for
vba-attribute-header-strip Session 1. Writes the routed output to
docs/session-sets/vba-attribute-header-strip/ai-assignment.md so the
orchestrator can read it back rather than printing the (possibly
non-cp1252) string to stdout.
"""
from __future__ import annotations
import os
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/vba-attribute-header-strip")
OUTPUT = SESSION_SET / "ai-assignment.md"
RAW_DUMP = SESSION_SET / "ai-assignment.routed.raw.md"


SPEC_EXCERPT = """\
Session set: vba-attribute-header-strip (1-session set, possible 2 if
spill triggers). Goal: introduce a VbaModuleBodyLoader that strips the
canonical leading VBA module-header block (VERSION/BEGIN/END/Attribute
VB_*) from Access-exported source before the parser sees it, and
captures the stripped header as a typed `ModuleHeaderMetadata`
attached to the Module AST node. Audit all production VBA-parsing
call sites and route them through the loader. Regenerate the
parse-success-report goldens. The grammar is NOT extended; the
loader is the strip mechanism.

Session 1 steps (per spec.md):
 1. Read stub + cross-engine inputs
 2. Elaborate stub into executed spec (loader API, regex, AST attach
    point)
 3. Author ai-assignment.md via routed analysis  ← this call
 4. Build ModuleHeaderMetadata record
 5. Build VbaModuleBodyLoader (Load + LoadAndParse)
 6. Attach Header to Module AST
 7. Call-site audit ledger
 8. Update production consumers (SymbolDbBuilder,
    ParseSuccessReportGenerator confirmed as production)
 9. Loader unit tests
10. Regenerate parse-success-report goldens
    (UPDATE_PARSE_SUCCESS_REPORTS=1)
11. End-to-end regression test
12. dotnet build && dotnet test sequentially
13. End-of-session cross-provider session-verification (always Opus +
    GPT-5.4 verifier per the task type)
14. Closeout artifacts + commit/push + notify
15. Post-notify reorganization review

Workload mix: mechanical C# implementation against a pinned spec,
~150-line regex/loader, ~50-line metadata record, ~3-line AST shape
change, ~2 production-call-site edits, ~250 lines of unit tests, plus
the routed steps (this analysis call, change-log.md summarization,
next-session-set-recommendation analysis, cross-provider
session-verification).

Lessons-learned context that matters:
- Mechanical-correspondence task types pin to Sonnet rather than Opus.
- Change-log uses `task_type=summarization` with `max_tier=1` to
  avoid Gemini 400 on >2KB prompts.
- Session-verification is always cross-provider — never bypass.
"""


PROMPT = f"""\
Author ai-assignment.md for the session described below. Use the schema in
docs/ai-led-session-workflow.md Step 3.5:

## Session 1: <Title>

### Recommended orchestrator
<engine> <model> @ effort=<low|medium|high|max>

### Rationale
<2 sentences — why this engine for this session's mix of work>

### Estimated routed cost
<qualitative — "low" / "moderate" / "high">

| Step | Action | Routing Decision |
|------|--------|------------------|
| ...  | ...    | ...              |

### Actuals (filled after the session)
- Orchestrator used: <engine + model + effort>
- Total routed cost: $X.XX
- Deviations from recommendation: <any>
- Notes for next-session calibration: <any>

(Leave the Actuals block as placeholders; the orchestrator fills them at
closeout.)

Map each numbered step in the session plan to a routing decision —
either "in-conversation" (orchestrator owns) or "route(task_type=X)"
with the task_type pinned. For routed calls, note which model the
router will pick at default settings (per
ai_router/router-config.yaml task_type defaults).

Pick a Recommended orchestrator that balances cost and capability.
Sonnet at high effort is the natural default for mechanical C#
work; Opus is justified only if the spec elaboration or the unit-test
authoring needs synthesis. Justify your choice in the Rationale line.

Context:
{SPEC_EXCERPT}
"""


def main() -> None:
    result = route(
        content=PROMPT,
        task_type="analysis",
        session_set=str(SESSION_SET),
        session_number=1,
    )

    RAW_DUMP.parent.mkdir(parents=True, exist_ok=True)
    RAW_DUMP.write_text(result.content, encoding="utf-8")
    OUTPUT.write_text(result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=1,
        step_number=3,
        step_key="session-1/ai-assignment",
        description="Routed ai-assignment.md analysis (task_type=analysis).",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    sys.stdout.write(
        f"OK model={result.model_name} input={result.input_tokens} "
        f"output={result.output_tokens} cost=${result.cost_usd:.4f}\n"
    )


if __name__ == "__main__":
    main()
