"""Register Session 2 of harvester-finish-canonical-parse-success-report.

Flips session-state.json from Session 1 'complete' to Session 2 'in-progress'
ahead of the first activity-log entry (per workflow Step 1 / Rule #16).
"""
import os
os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from pathlib import Path
from ai_router import register_session_start
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
log = SessionLog(SESSION_SET)
next_session = log.get_next_session_number()
print(f"Next session number: {next_session}")
print(f"Total sessions: {log.total_sessions}")

register_session_start(
    session_set=str(SESSION_SET),
    session_number=next_session,
    total_sessions=log.total_sessions,
    orchestrator_engine="claude-code",
    orchestrator_provider="anthropic",
    orchestrator_model="claude-opus-4-7",
    orchestrator_effort="high",
)
print("Registered Session", next_session)
