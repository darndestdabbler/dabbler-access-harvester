"""Mark Session 2 complete and send the Pushover notification.

Runs AFTER the closeout commit + push so the gates (working tree clean,
pushed to remote, disposition present, etc.) all pass.
"""
import os
os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from pathlib import Path
from ai_router import mark_session_complete, send_session_complete_notification
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
log = SessionLog(SESSION_SET)

print("Calling mark_session_complete...")
result = mark_session_complete(
    session_set=str(SESSION_SET),
    verification_verdict="VERIFIED",
)
print(f"mark_session_complete -> {result}")

print("\nSending session-complete notification...")
summary = (
    "Phase 4 CLOSES. Session 2 of 2 of harvester-finish-cli-packaging shipped "
    "the packaged-binary smoke test surface, --version flag (with schema "
    "version pinned), and install docs. VERIFIED Round 1 (gemini-pro fallback "
    "after gpt-5-4 429 - three-for-three on recent closeouts). Set-total "
    "routed cost ~$0.078, below the $0.10-$0.40 band. Next: no immediate "
    "next set in this repo; per strategy.md and ROADMAP.md the harvester "
    "moves to maintenance posture, and Access-to-Blazor application "
    "migration work moves to a future migration repo not yet created. "
    "See next-session-set-recommendation.md for three optional carry-overs."
)

try:
    notif = send_session_complete_notification(
        session_set=str(SESSION_SET),
        session_number=2,
        total_sessions=2,
        verification_verdict="VERIFIED",
        summary=summary,
    )
    print(f"Notification: {notif}")
except Exception as exc:
    print(f"Session completed, but Pushover notification failed: {exc}")
