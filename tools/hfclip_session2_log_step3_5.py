"""Step 3.5 log entry for Session 2 of harvester-finish-cli-packaging.

Session 2 is the last session of the set, so:
  - Session 1 actuals are already in ai-assignment.md (filled at Session 1 closeout).
  - The Session 2 next-orchestrator recommendation is already routed and lives at
    next-orchestrator-recommendation-session-2.md.
  - There is no Session N+1 to recommend (Step 8 calls for next-orchestrator on
    every session except the last; the next-session-set recommendation is the
    last-session counterpart and is routed at Step 8).

This script logs Step 3.5 as complete with no new routed calls.
"""
import os
os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from pathlib import Path
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
log = SessionLog(str(SESSION_SET), total_sessions=2)
log.log_step(
    session_number=2,
    step_number=1,
    step_key="session-2/ai-assignment-step3_5-noop",
    description=(
        "Step 3.5 acknowledged: Session 1 actuals already recorded at Session 1 "
        "closeout; Session 2 next-orchestrator recommendation already routed at "
        "Session 1 Step 8 and lives at next-orchestrator-recommendation-session-2.md. "
        "Session 2 is last — no Session 3 recommendation to author."
    ),
    status="complete",
    api_calls=[],
)
print("Logged Step 3.5 for Session 2.")
