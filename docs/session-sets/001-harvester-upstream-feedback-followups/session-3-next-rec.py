"""Route the next-session-set recommendation (do-not-self-opine).

Final session of 001-harvester-upstream-feedback-followups. Per project-guidance
(Always route next-orchestrator / next-session-set recommendations) this is routed,
not authored from the orchestrator's own opinion. Saves the routed reply verbatim
into next-session-set-recommendation.md (persist-to-disk before any print).
"""
from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "001-harvester-upstream-feedback-followups"

CONTEXT = """You are recommending the NEXT session set for the Dabbler Access Harvester
repo after the FINAL session of `001-harvester-upstream-feedback-followups` closed
VERIFIED.

What this set did (3 sessions): tightened the usage-identification explanation layer
in response to upstream consumer (HomeHealthCare) feedback —
  - S1: recognize launcher/switchboard/menu/startup surfaces (LauncherSurface /
    StartupEntry signals) and reframe "no harvested opener" as out-of-band entry
    uncertainty (OutOfBandEntryPossible) instead of a deletability/staleness hint;
  - S2: recognize parameterized queries and likely parameter-dialog forms
    (ParameterizedInput signal, ParameterDialogFlow / ParameterizedQuery questions);
  - S3 (this session): pilot + regenerate the committed HHC + Northwind usage goldens
    to bake in the cumulative changes, verify object-use-checklist.json casing is
    canonical PascalCase (verification-only; stop rule honored, no code change), and
    confirm no wording/threshold calibration was needed.

Important standing context about the repo's program of work:
  - The harvester's mechanical-extraction + deterministic-enrichment arc is regarded
    as COMPLETE. As of 2026-06-01 the two gating sets shipped (classify-timeout fix +
    the SQL-migration-generator capstone), and the repo is in a CONSUMER-DRIVEN-ONLY
    posture: no self-initiated feature work; new work is started only when a downstream
    consumer reports a concrete need. This set itself was such consumer-driven work
    (upstream HomeHealthCare feedback).
  - AI interpretation of the harvested output is explicitly OUT OF SCOPE for this repo;
    that belongs to a future downstream migration repo.
  - There is no UI in this repo; quality gate is build + hermetic tests + mandatory
    cross-provider verification.

Given that posture, recommend what should happen next. Be honest: if the right answer
is "no self-initiated next set — wait for the next concrete consumer-reported need,"
say so plainly and explain what signals would re-open work. If you do see a small,
genuinely-warranted, in-posture follow-up (e.g. regenerating a sibling consumer
artifact, or a deferred verification), name it specifically and scope it. Do NOT invent
speculative feature sets that violate the consumer-driven-only posture.

Write the recommendation as a short markdown document with a clear heading, a
Recommendation section, a Rationale section, and (if applicable) a "What would re-open
work" section. Keep it concise."""


def main() -> int:
    log = SessionLog(SESSION_SET)
    result = route(
        content=CONTEXT,
        task_type="analysis",
        session_set=str(SESSION_SET),
        session_number=3,
    )
    out = SESSION_SET / "next-session-set-recommendation.md"
    out.write_text(result.content, encoding="utf-8")
    log.log_step(
        session_number=3,
        step_number=8,
        step_key="session-3/next-set-recommendation",
        description="Routed next-session-set recommendation (analysis, do-not-self-opine)",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"model={result.model_name} in={result.input_tokens} out={result.output_tokens} cost=${result.cost_usd:.4f}")
    print("wrote", out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
