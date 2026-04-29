"""Generate sample-report.md from the consistency DB state.

Reports per-stratum sample sizes, per-provider cost and mean confidence,
and any parse failures or provider errors.
"""

from __future__ import annotations

import sqlite3
from pathlib import Path

from .sampler import load_sample


def generate_report(
    db_path: str,
    sample_path: str,
    output_path: str,
) -> str:
    """Write sample-report.md and return the text."""
    items = load_sample(sample_path)
    con = sqlite3.connect(db_path)
    con.row_factory = sqlite3.Row

    # ── per-stratum counts ─────────────────────────────────────────────────
    by_stratum: dict[str, list[str]] = {}
    for item in items:
        by_stratum.setdefault(item["stratum"], []).append(item["fqn"])

    # ── per-provider stats from InterpretationHistory ─────────────────────
    provider_stats: dict[str, dict] = {}
    for slot, label in ((2, "opus"), (3, "gpt-5-4")):
        rows = con.execute(
            """
            SELECT h.fqn, h.confidence, h.agent
            FROM InterpretationHistory h
            WHERE h.opinion_slot = ? AND h.reason = 'independent-cross-provider'
            """,
            (slot,),
        ).fetchall()
        confidences = [r["confidence"] for r in rows if r["confidence"] is not None]
        agents = sorted({r["agent"] for r in rows})
        # cost from BuildRuns for session 5
        cost_rows = con.execute(
            """
            SELECT SUM(total_routed_cost_usd) as total_cost,
                   SUM(interpretations_written) as total_written
            FROM BuildRuns
            WHERE session_set = 'vba-symbol-db' AND session_number = 5
              AND notes LIKE ?
            """,
            (f"%opinion {slot}%",),
        ).fetchone()
        provider_stats[label] = {
            "slot": slot,
            "written": len(rows),
            "mean_confidence": sum(confidences) / len(confidences) if confidences else None,
            "agents": agents,
            "total_cost_usd": cost_rows["total_cost"] or 0.0,
        }

    # ── error counts ──────────────────────────────────────────────────────
    errors: dict[int, int] = {}
    for slot in (2, 3):
        row = con.execute(
            """
            SELECT COUNT(*) as cnt FROM InterpretationHistory
            WHERE opinion_slot = ? AND reason LIKE 'skipped-provider-error%'
            """,
            (slot,),
        ).fetchone()
        errors[slot] = row["cnt"] if row else 0

    con.close()

    # ── render markdown ────────────────────────────────────────────────────
    lines = ["# Sample Report — vba-symbol-db Session 5\n"]

    lines.append("## Stratification")
    lines.append("")
    lines.append("| Stratum | Population (DB) | Sample |")
    lines.append("|---------|-----------------|--------|")
    order = ["enum_decl", "macro", "query", "function", "module_var", "sub", "enum_value", "lookup_table"]
    for stratum in order:
        fqns = by_stratum.get(stratum, [])
        lines.append(f"| `{stratum}` | — | {len(fqns)} |")
    lines.append(f"| **Total** | **103** | **{len(items)}** |")
    lines.append("")

    lines.append("## Per-Provider Results")
    lines.append("")
    for label, ps in provider_stats.items():
        mc = f"{ps['mean_confidence']:.3f}" if ps["mean_confidence"] is not None else "n/a"
        lines.append(f"### Opinion {ps['slot']} — {label}")
        lines.append(f"- Written: {ps['written']}")
        lines.append(f"- Mean confidence: {mc}")
        lines.append(f"- Provider errors (skipped): {errors.get(ps['slot'], 0)}")
        lines.append(f"- Total routed cost: ${ps['total_cost_usd']:.4f}")
        if ps["agents"]:
            lines.append(f"- Agents used: {', '.join(ps['agents'])}")
        lines.append("")

    lines.append("## Sampled FQNs\n")
    for stratum in order:
        fqns = by_stratum.get(stratum, [])
        if not fqns:
            continue
        lines.append(f"### {stratum} ({len(fqns)} items)")
        for fqn in sorted(fqns):
            lines.append(f"- `{fqn}`")
        lines.append("")

    text = "\n".join(lines) + "\n"
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(text)
    return text
