"""Rename completed session-set directories with a ✅ prefix and rewrite
path references across the repo.

Strategy:
  1. Identify closed sets (folders containing change-log.md) under
     docs/session-sets/.
  2. Rename each via git mv to ✅<slug>.
  3. Rewrite every occurrence of `docs/session-sets/<slug>/` in
     tracked text files to `docs/session-sets/✅<slug>/`.
  4. Slug-only references (e.g., "the parser-foundations set is...")
     are intentionally left alone — the conceptual slug does not
     change, only the directory name.

Run from the repo root via `.venv/Scripts/python.exe tools/session-runner/batch_rename_completed_sets.py`.
"""
from __future__ import annotations
import pathlib, subprocess, sys, re

# Force UTF-8 on stdout so the ✅ marker prints cleanly on Windows
# cp1252 consoles (lessons-learned: cp1252 encoding crashes lose data).
sys.stdout.reconfigure(encoding="utf-8")

REPO = pathlib.Path(__file__).resolve().parents[2]
SESSION_ROOT = REPO / "docs" / "session-sets"
CHECK_MARK = "✅"  # ✅

EXTENSIONS_TO_REWRITE = {".md", ".json", ".py", ".yaml", ".yml", ".csv", ".txt"}


def find_closed_slugs() -> list[str]:
    closed = []
    for child in sorted(SESSION_ROOT.iterdir()):
        if not child.is_dir():
            continue
        if child.name.startswith(CHECK_MARK):
            continue  # already renamed
        if (child / "change-log.md").is_file():
            closed.append(child.name)
    return closed


def git_mv(src: pathlib.Path, dst: pathlib.Path) -> None:
    print(f"git mv {src.relative_to(REPO)} -> {dst.relative_to(REPO)}")
    subprocess.run(
        ["git", "mv", str(src), str(dst)],
        cwd=str(REPO),
        check=True,
    )


def rewrite_path_refs(slugs: list[str]) -> list[pathlib.Path]:
    """Rewrite path references; return list of files modified."""
    pattern = re.compile(
        r"docs/session-sets/(" + "|".join(re.escape(s) for s in slugs) + r")/"
    )
    modified: list[pathlib.Path] = []
    # Walk tracked files via git ls-files to avoid touching .venv/bin/obj.
    out = subprocess.run(
        ["git", "ls-files"], cwd=str(REPO), check=True,
        capture_output=True, text=True, encoding="utf-8",
    )
    for rel in out.stdout.splitlines():
        path = REPO / rel
        if path.suffix.lower() not in EXTENSIONS_TO_REWRITE:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except Exception as exc:
            print(f"  skip {rel}: {exc}")
            continue
        new_text = pattern.sub(lambda m: f"docs/session-sets/{CHECK_MARK}{m.group(1)}/", text)
        if new_text != text:
            path.write_text(new_text, encoding="utf-8")
            modified.append(path)
    return modified


def main() -> None:
    slugs = find_closed_slugs()
    if not slugs:
        print("No unmarked closed session sets found.")
        return
    print(f"Closed sets to rename: {slugs}")
    for slug in slugs:
        src = SESSION_ROOT / slug
        dst = SESSION_ROOT / (CHECK_MARK + slug)
        git_mv(src, dst)
    print()
    print("Rewriting path references...")
    modified = rewrite_path_refs(slugs)
    print(f"\nModified {len(modified)} files:")
    for p in modified:
        print(f"  {p.relative_to(REPO)}")


if __name__ == "__main__":
    main()
