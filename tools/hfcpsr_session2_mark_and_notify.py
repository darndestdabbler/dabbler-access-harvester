"""Step 8 closeout finishers: mark_session_complete + Pushover notify.

Order matters: mark_session_complete enforces disposition_present +
clean-working-tree gates, so it must run AFTER the closeout commit (which
just landed) and BEFORE the notification.
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import mark_session_complete, send_session_complete_notification
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
log = SessionLog(SESSION_SET)

mark_session_complete(
    session_set=str(SESSION_SET),
    verification_verdict="VERIFIED",
)
print("mark_session_complete OK")

summary = (
    "F1 set CLOSED ($0.92 routed). Phase 4 Criterion #1 structurally done: per-fixture "
    "parse-success reports + live diff gate + sentinel + per-reason narrative. Reports "
    "ship unresolvedItems.status=\"not-measured\" — F2 wires live aggregator. "
    "Session 2 verification: 3 rounds vs gpt-5-4, all narrative-only findings resolved. "
    "NEXT SET: harvester-finish-canonical-golden-output (F2 per ROADMAP); spec is stub; "
    "Session 1 elaborates. Recommended: Opus 4.7 @ effort=high. Plan cost band $0.40-$0.80 "
    "across 4 sessions. Two human-attention items from Session 1 remain open "
    "(VBA Attribute grammar gap; property-sheet criteria-style wrapping heuristic). "
    "Step 9 reorg review runs after this notify."
)

try:
    send_session_complete_notification(
        session_set=str(SESSION_SET),
        session_number=2,
        total_sessions=2,
        verification_verdict="VERIFIED",
        summary=summary,
    )
    print("Pushover notification sent")
except Exception as exc:
    print(f"Pushover failed: {exc}")
