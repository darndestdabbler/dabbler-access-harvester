#!/usr/bin/env python3
"""Restore mtime on samples/*.accdb files when Access has bumped the
timestamp without changing the bytes.

Why this exists: opening an .accdb in Microsoft Access updates the
file's modification time even when nothing inside the database changed.
Git's status fast-path uses mtime as a "definitely changed" signal
before reading bytes, so `git status` then flags the file as modified.
The diff is genuinely zero-byte — `git hash-object <path>` matches the
staged hash — but the mtime drift is noisy.

What this script does:
  For each samples/*.accdb (and a few other binary fixtures), compare
  the file's current content hash to the hash git has staged. If the
  hashes match, restore the file's mtime to the committer date of the
  last commit that touched the file. If the hashes differ, leave the
  file alone — a real content change should not be silently masked.

Usage:
  python tools/refresh-sample-timestamps.py            # actually restore
  python tools/refresh-sample-timestamps.py --dry-run  # report only

Exit codes:
  0 on success (no errors; some files may have been skipped because
    their content actually differs).
  1 on error (could not invoke git, files not found, etc.).
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from datetime import datetime
from pathlib import Path


# Files to manage. Add more here as needed; .laccdb lock files and
# .ldb's are deliberately excluded — those are transient by nature.
DEFAULT_TARGETS = [
    "samples/*.accdb",
    "samples/*.mdb",
]


def run(cmd: list[str], cwd: Path | None = None) -> str:
    """Run a command, capture stdout, raise if it fails."""
    result = subprocess.run(
        cmd,
        cwd=cwd,
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"command failed ({result.returncode}): {' '.join(cmd)}\n"
            f"stderr: {result.stderr.strip()}"
        )
    return result.stdout


def staged_hash(path: Path, repo_root: Path) -> str | None:
    """The blob SHA git has staged for this path, or None if untracked."""
    rel = path.relative_to(repo_root).as_posix()
    out = run(["git", "ls-files", "-s", "--", rel], cwd=repo_root).strip()
    if not out:
        return None
    # Format: <mode> <sha> <stage> <path>
    return out.split()[1]


def worktree_hash(path: Path, repo_root: Path) -> str:
    """The blob SHA the file would have if committed right now."""
    return run(["git", "hash-object", str(path)], cwd=repo_root).strip()


def last_commit_iso(path: Path, repo_root: Path) -> str | None:
    """Committer date (ISO 8601) of the last commit touching this path."""
    rel = path.relative_to(repo_root).as_posix()
    out = run(
        ["git", "log", "-1", "--format=%cI", "--", rel],
        cwd=repo_root,
    ).strip()
    return out or None


def find_repo_root(start: Path) -> Path:
    """Walk up from `start` looking for the worktree root."""
    out = run(["git", "rev-parse", "--show-toplevel"], cwd=start).strip()
    return Path(out).resolve()


def expand_targets(patterns: list[str], repo_root: Path) -> list[Path]:
    """Glob the patterns relative to repo_root; sorted, deduplicated."""
    matches: set[Path] = set()
    for pat in patterns:
        for p in repo_root.glob(pat):
            if p.is_file():
                matches.add(p.resolve())
    return sorted(matches)


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Restore mtime on tracked binary fixtures when their content "
            "matches the staged blob (i.e., the diff is mtime-only)."
        ),
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Report what would change; do not modify any file.",
    )
    parser.add_argument(
        "patterns",
        nargs="*",
        default=DEFAULT_TARGETS,
        help=(
            f"Glob patterns relative to the repo root. "
            f"Default: {DEFAULT_TARGETS}"
        ),
    )
    args = parser.parse_args()

    here = Path.cwd()
    try:
        repo_root = find_repo_root(here)
    except RuntimeError as exc:
        print(f"error: not inside a git work tree ({exc})", file=sys.stderr)
        return 1

    targets = expand_targets(args.patterns, repo_root)
    if not targets:
        print(f"no files matched patterns {args.patterns} under {repo_root}")
        return 0

    restored = 0
    skipped_untracked = 0
    skipped_changed = 0
    skipped_no_commit = 0

    for path in targets:
        rel = path.relative_to(repo_root).as_posix()
        try:
            staged = staged_hash(path, repo_root)
            if staged is None:
                print(f"skip (untracked): {rel}")
                skipped_untracked += 1
                continue

            current = worktree_hash(path, repo_root)
            if current != staged:
                print(f"skip (content differs): {rel}")
                skipped_changed += 1
                continue

            iso = last_commit_iso(path, repo_root)
            if iso is None:
                print(f"skip (no commit history): {rel}")
                skipped_no_commit += 1
                continue

            target_dt = datetime.fromisoformat(iso)
            target_ts = target_dt.timestamp()

            current_mtime = path.stat().st_mtime
            current_dt = datetime.fromtimestamp(current_mtime).astimezone()

            if abs(current_mtime - target_ts) < 1:
                print(f"ok (already current): {rel}")
                continue

            verb = "would restore" if args.dry_run else "restore"
            print(
                f"{verb}: {rel}\n"
                f"    from: {current_dt.isoformat(timespec='seconds')}\n"
                f"    to:   {target_dt.isoformat(timespec='seconds')}"
            )
            if not args.dry_run:
                # Preserve atime; only mtime needs the rewind.
                atime = path.stat().st_atime
                os.utime(path, (atime, target_ts))
                restored += 1
        except RuntimeError as exc:
            print(f"error processing {rel}: {exc}", file=sys.stderr)
            return 1

    if args.dry_run:
        print(f"\ndry-run summary: {len(targets)} examined")
    else:
        print(
            f"\ndone: {restored} restored, "
            f"{skipped_untracked} untracked, "
            f"{skipped_changed} content-differs, "
            f"{skipped_no_commit} no-history"
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
