"""Tiny CLI to append an activity-log step for session set 011.

Usage: vahs011_log.py <step_number> <step_key> <description> <status> [cost_json]
Keeps activity-log.json populated so close_session's gate does not force a
backfill commit (project-guidance Convention: call log_step at every major step).
"""
import json
import sys

from ai_router.session_log import SessionLog

SESSION_SET = "docs/session-sets/011-resolver-and-navigation-wiring"


def main() -> None:
    step_number = int(sys.argv[1])
    step_key = sys.argv[2]
    description = sys.argv[3]
    status = sys.argv[4]
    api_calls = json.loads(sys.argv[5]) if len(sys.argv) > 5 else []
    log = SessionLog(SESSION_SET)
    log.log_step(
        session_number=1,
        step_number=step_number,
        step_key=step_key,
        description=description,
        status=status,
        api_calls=api_calls,
    )
    print(f"logged step {step_number} ({step_key}) = {status}")


if __name__ == "__main__":
    main()
