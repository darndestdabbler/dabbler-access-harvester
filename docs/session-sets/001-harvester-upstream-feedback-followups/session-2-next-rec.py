"""Route the Session 3 orchestrator recommendation (analysis, do-not-self-opine).

Per the workflow Step 3.5, the next-session orchestrator recommendation is routed
(analysis -> gemini-pro) rather than self-opined. Prints the recommendation for the
orchestrator to fold into ai-assignment.md and disposition.json.
"""
from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "001-harvester-upstream-feedback-followups"

PROMPT = """Recommend the orchestrator for Session 3 (the FINAL session) of the
`001-harvester-upstream-feedback-followups` session set in the Dabbler Access
Harvester (C#/.NET). Do not self-opine on the engine's behalf; give a routing
recommendation grounded in the repo's conventions.

Repo routing conventions (project-guidance.md):
- Mechanical-correspondence work (running the pipeline, regenerating a committed
  golden, schema/casing verification against a fresh run, calibration of wording)
  -> Sonnet at effort=medium.
- Synthesis work (architecture, novel heuristics, the end-of-set Step 9
  reorganization review, change-log / next-session-set-recommendation authoring)
  -> Opus.
- When the orchestrator's model matches the routing target, the synthesis is
  handled in-conversation; only session-verification is always routed cross-provider.

Sessions 1 and 2 (both complete, VERIFIED) added launcher/out-of-band recognition
and parameterized-query + parameter-dialog recognition to the usage classifier.

Session 3 is "Pilot, Fresh-Output Verification, And Closeout":
- run identify-usage against the committed HomeHealthCare sample/golden that
  exercises launcher/menu and parameterized-query cases,
- review the regenerated object-use.json / object-use-checklist.json for whether
  launcher and parameter explanations now read clearly,
- VERIFY object-use-checklist.json casing against the fresh run; only patch code if
  a current run still shows drift (the spec's stop rule: do not change casing code
  unless a fresh run drifts) — the Session 1/2 pilots showed checklist top-level
  keys remain canonical PascalCase,
- regenerate the committed usage golden (deferred from Sessions 1-2),
- calibrate wording/thresholds only if the pilot exposes local defects,
- update planning/consumer docs if the artifacts expose new concepts,
- dotnet build + dotnet test,
- cross-provider verification of output quality + the verification-only-unless-broken
  handling of checklist casing,
- FINAL closeout: disposition.json, change-log.md, next-session-set-recommendation.md,
  and the Step 9 reorganization review (lessons -> conventions/principles).

This is predominantly verification + mechanical golden regeneration, but as the
FINAL session it also owns the synthesis closeout artifacts (change-log,
next-session-set-recommendation, Step 9 reorganization). Recommend an engine
(claude-code), provider, model, and effort, with a one-paragraph rationale that
weighs the mechanical-verification body against the synthesis closeout. Output the
recommendation plainly.
"""


def main() -> int:
    log = SessionLog(SESSION_SET)
    result = route(
        content=PROMPT,
        task_type="analysis",
        session_set=str(SESSION_SET),
        session_number=2,
    )
    log.log_step(
        session_number=2,
        step_number=9,
        step_key="session-2/next-session-recommendation",
        description="Routed Session 3 orchestrator recommendation (analysis)",
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
