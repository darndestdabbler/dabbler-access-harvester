"""One-shot, idempotent backfill of `totalSessions` in session-set metadata.

Historical context: about half of the activity-log.json files under
docs/session-sets/ have totalSessions=0 because the SessionLog
constructor's default was never overwritten. The display fix in
ai-router/__init__.py and tools/vscode-session-sets/extension.js
papers over this for the Done subtree, but downstream consumers still
see 0. This script normalizes the data at rest.

Rules (only applied when change-log.md exists for the set, i.e. the set
is done; in-progress sets with totalSessions=0 are left alone because
the total is genuinely unknown):

  - activity-log.json: if totalSessions in (0, null, missing) and there
    are entries, set totalSessions = count of unique sessionNumber
    values across entries.
  - session-state.json: if totalSessions in (0, null, missing), copy
    from the now-corrected activity-log.json's totalSessions.

Idempotent: re-running on already-corrected data is a no-op.

Usage:
    python tools/backfill_session_set_metadata.py [--base-dir docs/session-sets] [--dry-run]
"""
from __future__ import annotations

import argparse
import json
import os
import sys


def _load_json(path: str) -> dict | None:
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError):
        return None


def _save_json(path: str, data: dict, trailing_newline: bool) -> None:
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        if trailing_newline:
            f.write("\n")


def _backfill_set(set_dir: str, dry_run: bool) -> tuple[bool, bool]:
    """Backfill one set. Returns (activity_log_changed, state_changed)."""
    log_path = os.path.join(set_dir, "activity-log.json")
    state_path = os.path.join(set_dir, "session-state.json")
    changelog_path = os.path.join(set_dir, "change-log.md")

    # Only backfill done sets; in-progress sets may legitimately have an
    # unknown total (e.g., spec says "4-5 sessions").
    if not os.path.isfile(changelog_path):
        return False, False

    activity_changed = False
    state_changed = False

    new_total: int | None = None

    log_data = _load_json(log_path) if os.path.isfile(log_path) else None
    if log_data is not None:
        current = log_data.get("totalSessions") or 0
        if current > 0:
            new_total = current
        else:
            sessions = {
                e.get("sessionNumber")
                for e in log_data.get("entries", [])
                if isinstance(e.get("sessionNumber"), int)
            }
            if sessions:
                new_total = len(sessions)
                log_data["totalSessions"] = new_total
                activity_changed = True
                if not dry_run:
                    # activity-log.json has no trailing newline historically
                    _save_json(log_path, log_data, trailing_newline=False)

    if new_total is not None and os.path.isfile(state_path):
        state_data = _load_json(state_path)
        if state_data is not None:
            state_total = state_data.get("totalSessions") or 0
            if state_total <= 0:
                state_data["totalSessions"] = new_total
                state_changed = True
                if not dry_run:
                    # session-state.json keeps a trailing newline
                    _save_json(state_path, state_data, trailing_newline=True)

    return activity_changed, state_changed


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--base-dir",
        default="docs/session-sets",
        help="Root containing session-set folders (default: %(default)s)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Report what would change without writing files",
    )
    args = parser.parse_args(argv)

    if not os.path.isdir(args.base_dir):
        print(f"ERROR: {args.base_dir!r} is not a directory", file=sys.stderr)
        return 2

    activity_count = 0
    state_count = 0
    set_count = 0

    for name in sorted(os.listdir(args.base_dir)):
        set_dir = os.path.join(args.base_dir, name)
        if not os.path.isdir(set_dir):
            continue
        if not os.path.isfile(os.path.join(set_dir, "spec.md")):
            continue
        set_count += 1
        a, s = _backfill_set(set_dir, dry_run=args.dry_run)
        if a or s:
            tag = "[dry-run]" if args.dry_run else "[updated]"
            parts = []
            if a:
                parts.append("activity-log.json")
            if s:
                parts.append("session-state.json")
            print(f"  {tag} {name}: {', '.join(parts)}")
        if a:
            activity_count += 1
        if s:
            state_count += 1

    verb = "would update" if args.dry_run else "updated"
    print()
    print(f"Scanned {set_count} session set(s).")
    print(f"  {verb} activity-log.json totalSessions: {activity_count}")
    print(f"  {verb} session-state.json totalSessions: {state_count}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
