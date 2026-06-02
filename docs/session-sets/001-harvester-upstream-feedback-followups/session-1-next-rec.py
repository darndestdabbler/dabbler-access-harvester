"""Route the Session 2 orchestrator recommendation (analysis, do-not-self-opine).

Per the workflow Step 3.5, the next-session orchestrator recommendation is routed
(analysis -> gemini-pro) rather than self-opined. Prints the recommendation for
the orchestrator to fold into ai-assignment.md and disposition.json.
"""
from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "001-harvester-upstream-feedback-followups"

PROMPT = """Recommend the orchestrator for Session 2 of the
`001-harvester-upstream-feedback-followups` session set in the Dabbler Access
Harvester (C#/.NET). Do not self-opine on the engine's behalf; give a routing
recommendation grounded in the repo's conventions.

Repo routing conventions (project-guidance.md):
- Mechanical-correspondence work (schema/plumbing, translating a spec verbatim
  into C#, test scaffolds) -> Sonnet at effort=medium.
- Synthesis work (architecture, novel heuristics, parser-grammar design) -> Opus.
- When the orchestrator's model matches the routing target, the synthesis is
  handled in-conversation; only session-verification is always routed.

Session 1 (just completed, VERIFIED) added launcher/navigation-shell recognition
and out-of-band entry uncertainty to the usage classifier. Session 2 is
"Parameterized Queries + Parameter-Dialog Flows":
- widen the usage-side IR so saved-query PARAMETERS metadata survives load,
- add explicit signals for bracketed/prompt-style query parameters and declared
  PARAMETERS blocks,
- heuristics for likely parameter-collection forms (e.g. frmReportParam_*) and
  report-open parameter flows,
- teach the question generator to express parameter uncertainty distinctly,
- regression tests.

There is a mix of mechanical plumbing (parameter metadata into IrQuery/loader) and
moderate heuristic synthesis (parameter-dialog form recognition). Recommend an
engine (claude-code), provider, model, and effort, with a one-paragraph rationale
that weighs the mechanical-vs-synthesis split. Output the recommendation plainly.
"""


def main() -> int:
    log = SessionLog(SESSION_SET)
    result = route(
        content=PROMPT,
        task_type="analysis",
        session_set=str(SESSION_SET),
        session_number=1,
    )
    log.log_step(
        session_number=1,
        step_number=8,
        step_key="session-1/next-session-recommendation",
        description="Routed Session 2 orchestrator recommendation (analysis)",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"model={result.model_name} cost=${result.cost_usd:.4f}")
    print("===RECOMMENDATION===")
    print(result.content)
    return 0


if __name__ == "__main__":
    sys.exit(main())
