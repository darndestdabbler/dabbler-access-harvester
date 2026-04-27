"""Backfill ai-assignment.md, session-state.json, and a Session Set
Configuration block diff for in-progress session sets.

Run once after the workflow upgrade lands. For each in-progress set under
``docs/session-sets/``, this script:

1. Reconstructs prior sessions' actuals from ``activity-log.json`` and the
   global ``router-metrics.jsonl`` (orchestrator engine, total routed cost,
   per-step model usage).
2. Authors ``ai-assignment.md`` if it does not already exist, with the
   prior-session actuals filled in. The forward-looking recommendation is
   produced via ``route(task_type='analysis')`` — never self-opined.
3. Writes ``session-state.json`` with ``currentSession`` set to the most
   recent in-progress session number and orchestrator metadata set to
   ``unknown`` (cannot be reliably reconstructed from the activity log alone).
4. Proposes a Session Set Configuration block diff against ``spec.md``,
   inferring ``requiresUAT`` from presence of a ``<slug>-uat-checklist.json``
   and ``requiresE2E`` from presence of any matching Playwright tests or
   ``E2ETestReference`` entries. The diff is written to
   ``<set>/.proposed-config-block.md`` for human review — nothing is
   committed automatically.

Idempotent: safe to re-run. Existing ``ai-assignment.md`` and
``session-state.json`` files are preserved; only the proposed-config-block
file is overwritten on each run.

Usage:

    .venv/Scripts/python.exe tools/backfill_ai_assignment.py
    .venv/Scripts/python.exe tools/backfill_ai_assignment.py --dry-run
    .venv/Scripts/python.exe tools/backfill_ai_assignment.py --set <slug>
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
import re
import sys
from datetime import datetime
from pathlib import Path
from typing import Optional


REPO_ROOT = Path(__file__).resolve().parent.parent
SESSION_SETS_DIR = REPO_ROOT / "docs" / "session-sets"
PLAYWRIGHT_DIR = REPO_ROOT / "tests" / "AcmeCoffeeOnline.Playwright"
ROUTER_METRICS = REPO_ROOT / "ai-router" / "router-metrics.jsonl"
AI_ASSIGNMENT_FILENAME = "ai-assignment.md"
SESSION_STATE_FILENAME = "session-state.json"
PROPOSED_CONFIG_FILENAME = ".proposed-config-block.md"


def load_ai_router():
    spec_path = REPO_ROOT / "ai-router" / "__init__.py"
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        spec_path,
        submodule_search_locations=[str(spec_path.parent)],
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


# --- Set discovery -----------------------------------------------------

def discover_in_progress_sets(filter_slug: Optional[str] = None) -> list[Path]:
    if not SESSION_SETS_DIR.is_dir():
        return []
    sets: list[Path] = []
    for entry in sorted(SESSION_SETS_DIR.iterdir()):
        if not entry.is_dir():
            continue
        if entry.name.startswith("_"):
            continue
        if filter_slug and entry.name != filter_slug:
            continue
        spec_path = entry / "spec.md"
        change_log = entry / "change-log.md"
        activity = entry / "activity-log.json"
        state = entry / SESSION_STATE_FILENAME
        if not spec_path.is_file():
            continue
        if change_log.is_file():
            continue  # already complete
        if not (activity.is_file() or state.is_file()):
            continue  # not started — backfill is for in-progress only
        sets.append(entry)
    return sets


# --- Activity-log + metrics reconstruction -----------------------------

def load_activity_log(set_dir: Path) -> dict:
    path = set_dir / "activity-log.json"
    if not path.is_file():
        return {"entries": [], "totalSessions": None}
    try:
        with path.open("r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError):
        return {"entries": [], "totalSessions": None}


def load_metrics_for_set(slug: str) -> list[dict]:
    if not ROUTER_METRICS.is_file():
        return []
    rows: list[dict] = []
    try:
        with ROUTER_METRICS.open("r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    rec = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if rec.get("session_set", "").endswith(slug):
                    rows.append(rec)
    except OSError:
        pass
    return rows


def session_actuals(activity: dict, metrics: list[dict]) -> list[dict]:
    """Group prior-session actuals by session number."""
    by_session: dict[int, dict] = {}
    for entry in activity.get("entries", []):
        n = entry.get("sessionNumber")
        if not isinstance(n, int):
            continue
        slot = by_session.setdefault(
            n,
            {"sessionNumber": n, "totalCost": 0.0, "models": set(), "steps": 0},
        )
        slot["steps"] += 1
        for call in entry.get("apiCalls", []):
            slot["totalCost"] += float(call.get("costUsd", 0.0) or 0.0)
            m = call.get("model")
            if m:
                slot["models"].add(m)
    # Augment with metrics rows (richer per-call detail).
    for rec in metrics:
        n = rec.get("session_number")
        if not isinstance(n, int):
            continue
        slot = by_session.setdefault(
            n,
            {"sessionNumber": n, "totalCost": 0.0, "models": set(), "steps": 0},
        )
        cost = rec.get("cost_usd") or rec.get("costUsd")
        if isinstance(cost, (int, float)):
            slot["totalCost"] = max(slot["totalCost"], float(cost))  # avoid double-count
        m = rec.get("model")
        if m:
            slot["models"].add(m)
    out = []
    for n in sorted(by_session):
        slot = by_session[n]
        slot["models"] = sorted(slot["models"])
        out.append(slot)
    return out


# --- Spec configuration inference --------------------------------------

CONFIG_BLOCK_RE = re.compile(
    r"##\s*Session Set Configuration[\s\S]*?```ya?ml[\s\S]*?```",
    re.IGNORECASE,
)


def spec_has_config_block(spec_path: Path) -> bool:
    try:
        text = spec_path.read_text(encoding="utf-8")
    except OSError:
        return False
    return bool(CONFIG_BLOCK_RE.search(text))


def has_uat_checklist(set_dir: Path, slug: str) -> bool:
    return (set_dir / f"{slug}-uat-checklist.json").is_file()


def has_playwright_match(slug: str) -> bool:
    if not PLAYWRIGHT_DIR.is_dir():
        return False
    tokens = [t.lower() for t in slug.split("-") if len(t) >= 3]
    if not tokens:
        return False
    for root, _dirs, files in os.walk(PLAYWRIGHT_DIR):
        # Skip build outputs.
        if any(skip in root for skip in ("\\bin\\", "/bin/", "\\obj\\", "/obj/")):
            continue
        for f in files:
            if not f.endswith((".cs", ".ts", ".js")):
                continue
            lower = f.lower()
            if any(t in lower for t in tokens):
                return True
    return False


def propose_config_block(slug: str, set_dir: Path) -> dict:
    requires_uat = has_uat_checklist(set_dir, slug)
    requires_e2e = requires_uat or has_playwright_match(slug)
    return {
        "requiresUAT": requires_uat,
        "requiresE2E": requires_e2e,
        "uatScope": "per-session" if requires_uat else "none",
    }


def render_config_block(config: dict, slug: str) -> str:
    return (
        "---\n\n"
        "## Session Set Configuration\n\n"
        "```yaml\n"
        f"requiresUAT: {str(config['requiresUAT']).lower()}\n"
        f"requiresE2E: {str(config['requiresE2E']).lower()}\n"
        f"uatScope: {config['uatScope']}\n"
        "```\n\n"
        "> Rationale (auto-inferred): "
        f"requiresUAT is {'true' if config['requiresUAT'] else 'false'} based on "
        f"{'presence' if config['requiresUAT'] else 'absence'} of "
        f"`{slug}-uat-checklist.json`. requiresE2E is "
        f"{'true' if config['requiresE2E'] else 'false'} based on the same plus "
        "any Playwright file matching the slug tokens. **Review and adjust before "
        "accepting.**\n\n"
        "---\n"
    )


# --- ai-assignment.md authoring ---------------------------------------

def render_ai_assignment(
    slug: str,
    activity: dict,
    actuals: list[dict],
    routed_recommendation: str,
) -> str:
    total_sessions = activity.get("totalSessions") or "?"
    today = datetime.now().strftime("%Y-%m-%d")
    lines = [
        f"# AI Assignment — {slug}",
        "",
        f"> **Backfilled:** {today}. Prior-session actuals reconstructed from",
        "> `activity-log.json` and `ai-router/router-metrics.jsonl`. Forward-",
        "> looking recommendation produced via `route(task_type='analysis')`",
        "> per workflow Rule #17.",
        "",
        f"Sessions completed: {len(actuals)} of {total_sessions}",
        "",
    ]
    for slot in actuals:
        n = slot["sessionNumber"]
        models = ", ".join(slot["models"]) or "—"
        lines.extend([
            f"## Session {n}: (backfilled)",
            "",
            "### Recommended orchestrator",
            "(unknown — pre-dates ai-assignment.md authoring)",
            "",
            "### Actuals (reconstructed)",
            f"- Steps logged: {slot['steps']}",
            f"- Routed models used: {models}",
            f"- Approximate routed cost: ${slot['totalCost']:.4f}",
            "- Deviations from recommendation: n/a (no recommendation existed)",
            "",
        ])
    lines.extend([
        "## Forward-looking recommendation",
        "",
        routed_recommendation.strip(),
        "",
    ])
    return "\n".join(lines) + "\n"


def author_routed_recommendation(
    ar, slug: str, actuals: list[dict], inferred_config: dict
) -> str:
    """Route a forward-looking recommendation. Falls back to a stub on
    router error so the backfill can still produce a reviewable file.

    The prompt is assembled conditionally from the (inferred) config
    block: UAT-plan and E2E-coverage sections are only requested when
    the corresponding flag is true. This keeps the recommendation
    appropriate for non-UI sets and non-UI repos."""
    summary_lines = [f"Session set: {slug}", f"Sessions completed: {len(actuals)}"]
    for slot in actuals:
        summary_lines.append(
            f"  - Session {slot['sessionNumber']}: "
            f"{slot['steps']} steps, ${slot['totalCost']:.4f}, "
            f"models={','.join(slot['models']) or '—'}"
        )
    summary = "\n".join(summary_lines)

    # Sections every recommendation includes regardless of repo type.
    sections = [
        "Recommended orchestrator (engine + model + effort)",
        "2-sentence rationale grounded in the actuals",
        "Scope of the next session (what to do, what is out of scope)",
        "Verification approach (build / test / cross-provider AI verification)",
        "Open risks or unknowns",
        "Token-window budget caveat (one line)",
    ]
    # Conditional sections only added when the spec opts in.
    if inferred_config.get("requiresUAT"):
        sections.append(
            "UAT plan: which checklist items the next session adds or completes"
        )
    if inferred_config.get("requiresE2E"):
        sections.append(
            "E2E coverage: which Playwright tests the next session adds or updates"
        )

    section_list = "\n".join(f"- {s}" for s in sections)
    config_summary = (
        f"requiresUAT={inferred_config.get('requiresUAT')}, "
        f"requiresE2E={inferred_config.get('requiresE2E')}"
    )
    prompt = (
        "You are recommending the cheapest-capable orchestrator and the "
        "scope of the next session for this session set, based on the "
        "actuals and the inferred Session Set Configuration below. The "
        "configuration may be wrong — flag any disagreement with the "
        "inferred flags in your output.\n\n"
        f"Session set: {slug}\n"
        f"Inferred config: {config_summary}\n\n"
        f"Actuals:\n{summary}\n\n"
        "Output a Markdown block with these sections (omit any section "
        "that does not apply given the inferred config):\n"
        f"{section_list}\n"
    )
    try:
        result = ar.route(
            content=prompt,
            task_type="analysis",
            session_set=str(SESSION_SETS_DIR / slug),
        )
        return result.content
    except Exception as exc:
        return (
            "_Router call failed during backfill — fill in manually before "
            f"the next session._\n\n```\n{exc!r}\n```\n"
        )


# --- session-state.json ------------------------------------------------

def write_session_state(set_dir: Path, slug: str, actuals: list[dict],
                        total_sessions: Optional[int]) -> None:
    if (set_dir / SESSION_STATE_FILENAME).is_file():
        return  # idempotent: don't overwrite existing
    current = max((s["sessionNumber"] for s in actuals), default=1)
    state = {
        "schemaVersion": 1,
        "sessionSetName": slug,
        "currentSession": current,
        "totalSessions": total_sessions,
        "status": "in-progress",
        "startedAt": datetime.now().astimezone().isoformat(),
        "completedAt": None,
        "verificationVerdict": None,
        "orchestrator": {
            "engine": "unknown",
            "provider": None,
            "model": "unknown",
            "effort": "unknown",
        },
    }
    (set_dir / SESSION_STATE_FILENAME).write_text(
        json.dumps(state, indent=2) + "\n", encoding="utf-8",
    )


# --- main --------------------------------------------------------------

def process_set(set_dir: Path, *, dry_run: bool, ar) -> None:
    slug = set_dir.name
    print(f"\n=== {slug} ===")

    activity = load_activity_log(set_dir)
    metrics = load_metrics_for_set(slug)
    actuals = session_actuals(activity, metrics)

    # Infer the Session Set Configuration up front so the
    # recommendation prompt can omit UAT/E2E sections when neither
    # flag applies. The same inference drives the proposed config-block
    # diff written below; doing it once keeps both consistent.
    inferred_config = propose_config_block(slug, set_dir)

    # 1. ai-assignment.md
    ai_path = set_dir / AI_ASSIGNMENT_FILENAME
    if ai_path.is_file():
        print(f"  - {AI_ASSIGNMENT_FILENAME}: already exists, leaving in place")
    else:
        recommendation = author_routed_recommendation(
            ar, slug, actuals, inferred_config
        )
        body = render_ai_assignment(slug, activity, actuals, recommendation)
        if dry_run:
            print(f"  - {AI_ASSIGNMENT_FILENAME}: would write {len(body)} chars")
        else:
            ai_path.write_text(body, encoding="utf-8")
            print(f"  - {AI_ASSIGNMENT_FILENAME}: wrote {len(body)} chars")

    # 2. session-state.json
    state_path = set_dir / SESSION_STATE_FILENAME
    if state_path.is_file():
        print(f"  - {SESSION_STATE_FILENAME}: already exists, leaving in place")
    elif dry_run:
        print(f"  - {SESSION_STATE_FILENAME}: would write")
    else:
        write_session_state(set_dir, slug, actuals, activity.get("totalSessions"))
        print(f"  - {SESSION_STATE_FILENAME}: wrote")

    # 3. Proposed config block
    spec_path = set_dir / "spec.md"
    proposed_path = set_dir / PROPOSED_CONFIG_FILENAME
    if spec_has_config_block(spec_path):
        print("  - spec.md: already has Session Set Configuration block")
        if proposed_path.is_file() and not dry_run:
            proposed_path.unlink()
    else:
        config = inferred_config  # reuse the inference above
        block = render_config_block(config, slug)
        if dry_run:
            print(
                "  - spec.md: would propose config block "
                f"(UAT={config['requiresUAT']}, E2E={config['requiresE2E']})"
            )
        else:
            proposed_path.write_text(block, encoding="utf-8")
            print(
                f"  - {PROPOSED_CONFIG_FILENAME}: wrote proposed block "
                f"(UAT={config['requiresUAT']}, E2E={config['requiresE2E']}). "
                "Review then paste into spec.md."
            )


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--dry-run", action="store_true",
                   help="Print what would change but do not write any files.")
    p.add_argument("--set", dest="filter_slug",
                   help="Only process the named session-set slug.")
    args = p.parse_args()

    sets = discover_in_progress_sets(args.filter_slug)
    if not sets:
        print("No in-progress session sets to backfill.")
        return 0

    print(f"Found {len(sets)} in-progress session set(s) to backfill.")

    ar = load_ai_router()
    for set_dir in sets:
        process_set(set_dir, dry_run=args.dry_run, ar=ar)

    print(
        f"\nDone. {'(dry-run)' if args.dry_run else ''} "
        "Review each .proposed-config-block.md and paste into spec.md "
        "after confirming the inferred flags are correct."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
