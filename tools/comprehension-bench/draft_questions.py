"""Draft candidate AI-comprehension questions for a gold-standard DB.

Routes ``task_type="analysis"`` against the DB's IR plus the user-authored
scenario list. Emits a YAML draft for human curation.

This script is session-agnostic tooling — per the
``integration-testing-and-acceptance`` spec amendment of 2026-04-28, the
drafting step lives in Session 3, but the script itself can be invoked from
any session that has scenarios.md in place.

The drafted YAML is intentionally labeled DRAFT — it is uncurated.

Usage:

    python tools/comprehension-bench/draft_questions.py \\
        --db NorthwindDevEdition \\
        --ir-root /tmp/ae-bench-out/AccessMetaData/NorthwindDevEdition

Produces ``docs/test-cases/comprehension/<db>/questions.draft.yaml``.

Per `lessons-learned.md` -> "Persist Routed Output To Disk Before Any Display
Or Logging Side Effects", the routed response is written to disk *before* any
print or parse step.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SCENARIOS_PATH = REPO_ROOT / "docs" / "test-cases" / "comprehension" / "scenarios.md"


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        REPO_ROOT / "ai-router" / "__init__.py",
        submodule_search_locations=[str(REPO_ROOT / "ai-router")],
    )
    module = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = module
    spec.loader.exec_module(module)
    return module


def collect_ir_inventory(ir_root: Path) -> str:
    """Return a flat listing of IR files with size, useful as prompt context."""
    if not ir_root.exists():
        raise FileNotFoundError(f"IR root not found: {ir_root}")
    lines: list[str] = []
    for path in sorted(ir_root.rglob("*.ir.json")):
        rel = path.relative_to(ir_root).as_posix()
        lines.append(f"{rel}  ({path.stat().st_size} bytes)")
    return "\n".join(lines)


def sample_ir_excerpt(ir_root: Path, max_files: int = 6, max_bytes_each: int = 8000) -> str:
    """Concatenate up to N IR files (truncated) so the model sees real shape."""
    excerpts: list[str] = []
    for path in sorted(ir_root.rglob("*.ir.json"))[:max_files]:
        rel = path.relative_to(ir_root).as_posix()
        text = path.read_text(encoding="utf-8")
        truncated = text[:max_bytes_each] + ("\n... [truncated]" if len(text) > max_bytes_each else "")
        excerpts.append(f"=== {rel} ===\n{truncated}")
    return "\n\n".join(excerpts)


def build_prompt(db_name: str, scenarios_md: str, inventory: str, excerpt: str) -> tuple[str, str]:
    """Return (content, context) for the analysis route call."""
    content = (
        f"Database under analysis: {db_name}\n\n"
        f"Available IR files:\n{inventory}\n\n"
        f"Sample IR content (truncated):\n\n{excerpt}"
    )
    context = (
        "Generate ~20 candidate AI-comprehension test questions for this database's IR. "
        "Spread across three difficulty tiers (low/medium/high). Prioritize questions "
        "that exercise the canonical Access patterns listed below.\n\n"
        f"=== Canonical scenarios (priority guidance) ===\n{scenarios_md}\n\n"
        "=== Output format ===\n"
        "Return a YAML list. Each entry must have these fields:\n"
        "  id: <slug>-<NNN>          # e.g., nw-form-customers-001\n"
        "  question: <free-form question an Access developer would ask>\n"
        "  accepted_answers:         # 1-3 valid phrasings of the correct answer\n"
        "    - <answer text>\n"
        "  supporting_ir_paths:      # files the runner should feed into the answer prompt\n"
        "    - ir/<category>/<object>.ir.json\n"
        "  difficulty: low | medium | high\n\n"
        "Difficulty calibration:\n"
        "  low    - one IR file, single object, direct lookup of declared behavior\n"
        "  medium - two-three IR files, requires cross-reference (form -> query, "
        "macro -> action chain)\n"
        "  high   - 4+ IR files OR a pattern that needs inference across categories "
        "(switchboard navigation, AutoExec startup chain, dynamic SQL assembly)\n\n"
        "Output ONLY the YAML list. No prose preamble, no closing remarks. "
        "Begin the response with `- id:` directly."
    )
    return content, context


def main() -> int:
    parser = argparse.ArgumentParser(description="Draft AI-comprehension questions for a DB.")
    parser.add_argument("--db", required=True, help="Database short name, e.g., NorthwindDevEdition")
    parser.add_argument("--ir-root", required=True, type=Path, help="IR root for the DB (...AccessMetaData/<db>/)")
    parser.add_argument("--out", type=Path, help="Output YAML path (default: docs/test-cases/comprehension/<db>/questions.draft.yaml)")
    parser.add_argument("--session-set", default="docs/session-sets/integration-testing-and-acceptance",
                        help="Session set path for metric grouping")
    parser.add_argument("--session-number", type=int, default=3,
                        help="Session number for metric grouping. Drafting lives in Session 3 "
                             "of integration-testing-and-acceptance per the 2026-04-28 spec amendment.")
    args = parser.parse_args()

    if not SCENARIOS_PATH.exists():
        sys.stderr.write(
            f"scenarios.md not found at {SCENARIOS_PATH}.\n"
            "The user-authored canonical-scenario list is required for question drafting; "
            "see docs/session-sets/integration-testing-and-acceptance/spec.md -> Feature 3.\n"
        )
        return 2

    out_path = args.out or (REPO_ROOT / "docs" / "test-cases" / "comprehension" / args.db / "questions.draft.yaml")
    out_path.parent.mkdir(parents=True, exist_ok=True)

    scenarios_md = SCENARIOS_PATH.read_text(encoding="utf-8")
    inventory = collect_ir_inventory(args.ir_root)
    excerpt = sample_ir_excerpt(args.ir_root)
    content, context = build_prompt(args.db, scenarios_md, inventory, excerpt)

    ar = load_ai_router()
    result = ar.route(
        content=content,
        task_type="analysis",
        context=context,
        session_set=args.session_set,
        session_number=args.session_number,
    )

    # Persist BEFORE any print/parse side effects per lessons-learned.
    raw_path = out_path.with_suffix(".raw.txt")
    raw_path.write_text(result.content, encoding="utf-8")

    # Header that makes the "draft, not curated" status loud.
    header = (
        "# AUTO-DRAFTED — NOT CURATED. Review against scenarios.md before promoting to questions.yaml.\n"
        f"# DB: {args.db}\n"
        f"# Model: {result.model_name}\n"
        f"# Cost: ${result.cost_usd:.4f}\n"
        f"# Generated: see router-metrics.jsonl\n"
        "#\n"
        "# Curate by:\n"
        "#   1. Removing questions that don't map to a real scenario or DB artifact.\n"
        "#   2. Editing accepted_answers for accuracy.\n"
        "#   3. Verifying supporting_ir_paths exist on disk.\n"
        "#   4. Renaming this file to questions.yaml when done.\n"
        "\n"
    )
    out_path.write_text(header + result.content, encoding="utf-8")

    def _display(p: Path) -> str:
        try:
            return str(p.relative_to(REPO_ROOT))
        except ValueError:
            return str(p)

    summary = {
        "db": args.db,
        "drafted_path": _display(out_path),
        "raw_path": _display(raw_path),
        "model": result.model_name,
        "input_tokens": result.input_tokens,
        "output_tokens": result.output_tokens,
        "cost_usd": result.cost_usd,
    }
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
