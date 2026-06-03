"""Session 2 step logger: append a completed step to activity-log.json.

Usage:
  python -m ... NO — run via the workspace venv:
  .venv/Scripts/python.exe docs/session-sets/001-.../session-2-log.py \
      <step_number> <step_key> "<description>"

Keeps the activity-log gate satisfied (close_session reads activity-log.json,
which only log_step populates). Reasoning/routed steps with api_calls use the
dedicated session-2-verify.py instead.
"""
from __future__ import annotations

import sys
from pathlib import Path

from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "001-harvester-upstream-feedback-followups"


def main() -> int:
    if len(sys.argv) < 4:
        print("usage: session-2-log.py <step_number> <step_key> <description>")
        return 2
    step_number = int(sys.argv[1])
    step_key = sys.argv[2]
    description = sys.argv[3]
    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=2,
        step_number=step_number,
        step_key=step_key,
        description=description,
        status="complete",
        api_calls=[],
    )
    print(f"logged step {step_number}: {step_key}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
