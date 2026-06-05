"""Generate ``interpretation-summary.md`` from the symbol DB.

The report is the Step 10 deliverable for vba-symbol-db Session 4. It
quantifies how the leaves-first + dedup + dead-code + skip-deferred
policy reduced AI calls compared to a naive "interpret everything once"
baseline, plus a per-confidence breakdown that calibrates how much of
the corpus the routed model felt sure about.
"""

from __future__ import annotations

import argparse
import sqlite3
import sys
from typing import Optional

from . import db


def build_report(con: sqlite3.Connection) -> str:
    sym = _symbol_stats(con)
    macros = _macro_stats(con)
    tables = _table_stats(con)
    queries = _query_stats(con)
    cost = _build_run_cost(con)
    confidence = _confidence_distribution(con)
    escalations = _escalation_breakdown(con)

    lines: list[str] = []
    lines.append("# vba-symbol-db Session 4 — Interpretation Summary")
    lines.append("")
    lines.append("Fixture: the local-only Access fixture  ")
    lines.append("Modules: `Form_frmRecordDetail` and `modUtil`.  ")
    lines.append("")
    lines.append("This report is generated from the symbol DB after the AI")
    lines.append("interpretation pass. It is the bookend to Session 1's")
    lines.append("`measurement-report.md` — Session 1 measured the")
    lines.append("dedup/dead/deferred savings on paper; this report measures")
    lines.append("what the leaves-first interpretation actually cost in")
    lines.append("dollars and routed calls.")
    lines.append("")

    lines.append("## Symbol coverage")
    lines.append("")
    lines.append(f"- Total symbols in DB: **{sym['total']}**")
    lines.append(f"- Schedulable (interpreted or queued for interpretation): "
                 f"**{sym['schedulable']}**")
    lines.append(f"- Interpreted (Interpretation1 populated): "
                 f"**{sym['interpreted']}**")
    lines.append(f"  - via routed call: **{sym['routed']}**")
    lines.append(f"  - via MD5-twin copy: **{sym['twin_copies']}**")
    lines.append(f"- Skipped (audit rows in InterpretationHistory):")
    for reason, count in sym["skipped_by_reason"]:
        lines.append(f"  - `{reason}`: {count}")
    lines.append("")

    lines.append("## Macros")
    lines.append("")
    lines.append(f"- Macros in DB: {macros['total']}")
    lines.append(f"- Interpreted: {macros['interpreted']}")
    if macros["dead"]:
        lines.append(f"- Skipped dead: {macros['dead']}")
    lines.append("")

    lines.append("## Lookup tables")
    lines.append("")
    lines.append(f"- Tables in DB: {tables['total']}")
    lines.append(f"- Interpreted (score >= threshold): {tables['interpreted']}")
    lines.append(f"- Skipped (low lookup score): {tables['skipped_low_score']}")
    lines.append("")

    lines.append("## Queries")
    lines.append("")
    lines.append(f"- Queries in DB: {queries['total']}")
    lines.append(f"- Interpreted: {queries['interpreted']}")
    lines.append(f"- Skipped dead: {queries['dead']}")
    lines.append(f"- Twin copies: {queries['twin_copies']}")
    lines.append("")

    lines.append("## Cost")
    lines.append("")
    lines.append(f"- Total routed cost (this run, all build runs combined): "
                 f"**${cost['total']:.4f}**")
    lines.append(f"- BuildRuns recorded: {cost['runs']}")
    if cost["runs"]:
        lines.append("")
        lines.append("| run_id | session | started_at | interpretations | cost |")
        lines.append("|---|---|---|---|---|")
        for r in cost["per_run"]:
            lines.append(
                f"| {r['run_id']} | {r['session_number']} | {r['started_at']} | "
                f"{r['interpretations_written'] or 0} | "
                f"${(r['total_routed_cost_usd'] or 0):.4f} |"
            )
    lines.append("")

    lines.append("## Confidence distribution (Interpretation1)")
    lines.append("")
    lines.append("| Bucket | Count | % |")
    lines.append("|---|---|---|")
    total_with_conf = sum(c["count"] for c in confidence)
    for c in confidence:
        pct = (c["count"] * 100.0 / total_with_conf) if total_with_conf else 0
        lines.append(f"| {c['bucket']} | {c['count']} | {pct:.0f}% |")
    lines.append("")
    lines.append(f"Total interpreted with confidence: {total_with_conf}.")
    lines.append("")

    lines.append("## Escalation rate")
    lines.append("")
    lines.append(f"- Symbols with opinion 1 only: {escalations['opinion1_only']}")
    lines.append(f"- Symbols where opinion 2 fired (Confidence1 < 0.75): "
                 f"{escalations['opinion2_fired']}")
    lines.append(f"- Symbols where opinion 3 fired (avg < 0.70): "
                 f"{escalations['opinion3_fired']}")
    if escalations["routed_total"]:
        rate = escalations["opinion2_fired"] * 100.0 / escalations["routed_total"]
        lines.append(f"- Escalation rate: {rate:.1f}% (vs. spec estimate ~20%)")
    lines.append("")

    lines.append("## Cost-savings vs. naive baseline")
    lines.append("")
    lines.append("The naive baseline interprets every Symbol/Macro/Table/")
    lines.append("Query once at full procedure-prompt cost. The current")
    lines.append("policy skips procedure-local variables, dead symbols,")
    lines.append("low-score tables, and copies MD5-twin interpretations,")
    lines.append("and orders interpretation leaves-first so callee summaries")
    lines.append("substitute for callee source.")
    lines.append("")
    naive_estimate = sym["total"]
    actual = sym["routed"]
    if naive_estimate:
        savings_pct = (1.0 - actual / naive_estimate) * 100
        lines.append(f"- Naive symbol interpretations: {naive_estimate}")
        lines.append(f"- Actual routed interpretations: {actual}")
        lines.append(f"- Reduction: {savings_pct:.1f}% of symbol calls avoided "
                     "by the policy filters above")
    lines.append("")

    lines.append("## Notes for Session 5")
    lines.append("")
    lines.append("- The probe target `Form_frmRecordDetail.cboRecordType_BeforeUpdate`")
    lines.append("  has its full direct-callee interpretation set populated:")
    lines.append("  the procedure itself (to be blanked at packet build time),")
    lines.append("  the seven `m_lng*` / `m_str*` module-level vars, the two")
    lines.append("  local `Dim` strings (skipped as proc-local — Session 5's")
    lines.append("  packet builder should reconstruct them inline from")
    lines.append("  `source_body`), `RecordIsActive`, `GetString`, and")
    lines.append("  `eRecordAction_TypeChange`.")
    lines.append("- `DLookup`, `DCount`, `Nz`, `MsgBox`, `DoCmd.OpenForm`, etc.")
    lines.append("  resolve to built-ins or the Access object model and stay")
    lines.append("  outside the symbol-table interpretation set.")
    lines.append("")
    return "\n".join(lines)


def _symbol_stats(con: sqlite3.Connection) -> dict:
    total = con.execute("SELECT COUNT(*) FROM Symbols").fetchone()[0]
    interpreted = con.execute(
        "SELECT COUNT(*) FROM Symbols WHERE Interpretation1 IS NOT NULL"
    ).fetchone()[0]
    twin_copies = con.execute(
        "SELECT COUNT(*) FROM InterpretationHistory WHERE reason = 'copied-from-md5-twin'"
    ).fetchone()[0]
    routed = con.execute(
        """
        SELECT COUNT(*) FROM InterpretationHistory
        WHERE fqn_kind = 'symbol'
          AND opinion_slot = 1
          AND reason != 'copied-from-md5-twin'
        """
    ).fetchone()[0]
    schedulable = con.execute(
        """
        SELECT COUNT(*) FROM Symbols
        WHERE is_dead = 0
          AND equivalence_canonical_fqn IS NULL
          AND NOT (kind IN ('module_var', 'module_const')
                   AND declared_in_fqn IS NOT NULL)
        """
    ).fetchone()[0]
    skipped = [
        (row["reason"], row["n"])
        for row in con.execute(
            """
            SELECT reason, COUNT(*) AS n
            FROM InterpretationHistory
            WHERE opinion_slot = 0
            GROUP BY reason ORDER BY n DESC
            """
        )
    ]
    return {
        "total": total,
        "interpreted": interpreted,
        "twin_copies": twin_copies,
        "routed": routed,
        "schedulable": schedulable,
        "skipped_by_reason": skipped,
    }


def _macro_stats(con: sqlite3.Connection) -> dict:
    total = con.execute("SELECT COUNT(*) FROM Macros").fetchone()[0]
    interpreted = con.execute(
        "SELECT COUNT(*) FROM Macros WHERE Interpretation1 IS NOT NULL"
    ).fetchone()[0]
    dead = con.execute(
        "SELECT COUNT(*) FROM Macros WHERE is_dead = 1"
    ).fetchone()[0]
    return {"total": total, "interpreted": interpreted, "dead": dead}


def _table_stats(con: sqlite3.Connection) -> dict:
    total = con.execute("SELECT COUNT(*) FROM Tables").fetchone()[0]
    interpreted = con.execute(
        "SELECT COUNT(*) FROM Tables WHERE Interpretation1 IS NOT NULL"
    ).fetchone()[0]
    skipped = con.execute(
        """
        SELECT COUNT(*) FROM InterpretationHistory
        WHERE fqn_kind = 'table' AND reason = 'skipped-low-lookup-score'
        """
    ).fetchone()[0]
    return {"total": total, "interpreted": interpreted, "skipped_low_score": skipped}


def _query_stats(con: sqlite3.Connection) -> dict:
    total = con.execute("SELECT COUNT(*) FROM Queries").fetchone()[0]
    interpreted = con.execute(
        "SELECT COUNT(*) FROM Queries WHERE Interpretation1 IS NOT NULL"
    ).fetchone()[0]
    dead = con.execute("SELECT COUNT(*) FROM Queries WHERE is_dead = 1").fetchone()[0]
    twin_copies = con.execute(
        """
        SELECT COUNT(*) FROM InterpretationHistory
        WHERE fqn_kind = 'query' AND reason = 'copied-from-md5-twin'
        """
    ).fetchone()[0]
    return {
        "total": total,
        "interpreted": interpreted,
        "dead": dead,
        "twin_copies": twin_copies,
    }


def _build_run_cost(con: sqlite3.Connection) -> dict:
    rows = [
        dict(r)
        for r in con.execute(
            """
            SELECT run_id, session_number, started_at,
                   interpretations_written, total_routed_cost_usd
            FROM BuildRuns ORDER BY run_id
            """
        )
    ]
    total = sum((r["total_routed_cost_usd"] or 0) for r in rows)
    return {"total": total, "runs": len(rows), "per_run": rows}


def _confidence_distribution(con: sqlite3.Connection) -> list[dict]:
    rows = con.execute(
        """
        SELECT
          CASE
            WHEN Confidence1 IS NULL THEN '(none)'
            WHEN Confidence1 >= 0.90 THEN '0.90 - 1.00'
            WHEN Confidence1 >= 0.75 THEN '0.75 - 0.89'
            WHEN Confidence1 >= 0.50 THEN '0.50 - 0.74'
            ELSE '< 0.50'
          END AS bucket,
          COUNT(*) AS count
        FROM Symbols
        WHERE Interpretation1 IS NOT NULL
        GROUP BY bucket
        ORDER BY MIN(Confidence1) DESC
        """
    ).fetchall()
    return [{"bucket": r["bucket"], "count": r["count"]} for r in rows]


def _escalation_breakdown(con: sqlite3.Connection) -> dict:
    routed_total = con.execute(
        """
        SELECT COUNT(DISTINCT fqn) FROM InterpretationHistory
        WHERE fqn_kind = 'symbol' AND opinion_slot = 1
          AND reason != 'copied-from-md5-twin'
        """
    ).fetchone()[0]
    op2 = con.execute(
        "SELECT COUNT(DISTINCT fqn) FROM InterpretationHistory "
        "WHERE fqn_kind='symbol' AND opinion_slot=2"
    ).fetchone()[0]
    op3 = con.execute(
        "SELECT COUNT(DISTINCT fqn) FROM InterpretationHistory "
        "WHERE fqn_kind='symbol' AND opinion_slot=3"
    ).fetchone()[0]
    return {
        "routed_total": routed_total,
        "opinion1_only": routed_total - op2,
        "opinion2_fired": op2,
        "opinion3_fired": op3,
    }


def main(argv: Optional[list[str]] = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--db", required=True)
    p.add_argument("--out", required=True)
    args = p.parse_args(argv)
    con = db.open_db(args.db)
    report = build_report(con)
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(report)
    print(f"wrote {args.out} ({len(report)} chars)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
