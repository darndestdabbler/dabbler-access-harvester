"""Build the Session 7 comprehension packet for the probe target.

Given a target procedure FQN, assemble a markdown packet containing:
  - The target Symbols row (source_body included; Interpretation* blanked).
  - Every distinct directly-referenced symbol's Symbols row WITH Interpretation1.
  - FormControls rows for controls referenced from the target's host form.
  - The EventBindings row that fires the target.
  - Tables rows for any lookup tables those FormControls bind to.

Save the packet to docs/session-sets/vba-symbol-db/probe-results/comprehension-packet.md.
"""

from __future__ import annotations

import argparse
import sqlite3
import subprocess
import tempfile
from pathlib import Path
from typing import Iterable

REPO_ROOT = Path(__file__).resolve().parents[3]
DB_PATH = REPO_ROOT / "tools" / "symbol-db" / "symbol-db.sqlite"
PROBE_BUILDER_CSPROJ = (
    REPO_ROOT / "tools" / "symbol-db" / "probe-builder"
    / "AccessHarvester.SymbolDb.ProbeBuilder.csproj"
)
DEFAULT_TARGET = "Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate"
DEFAULT_OUTPUT = REPO_ROOT / "docs" / "session-sets" / "vba-symbol-db" / "probe-results" / "comprehension-packet.md"


def _format_record(label: str, row: sqlite3.Row, fields: Iterable[str]) -> str:
    lines = [f"### {label}", ""]
    for f in fields:
        v = row[f] if f in row.keys() else None
        if v is None:
            lines.append(f"- **{f}**: _(null)_")
        elif isinstance(v, str) and "\n" in v:
            lines.append(f"- **{f}**:")
            lines.append("")
            lines.append("```vba")
            lines.append(v.rstrip())
            lines.append("```")
        else:
            lines.append(f"- **{f}**: `{v}`")
    lines.append("")
    return "\n".join(lines)


def _row_dict(con: sqlite3.Connection, sql: str, params=()) -> sqlite3.Row | None:
    con.row_factory = sqlite3.Row
    cur = con.execute(sql, params)
    return cur.fetchone()


def _row_dicts(con: sqlite3.Connection, sql: str, params=()) -> list[sqlite3.Row]:
    con.row_factory = sqlite3.Row
    return con.execute(sql, params).fetchall()


def rebuild_fixed_db(output_path: Path) -> None:
    subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(PROBE_BUILDER_CSPROJ),
            "--",
            "--repo-root",
            str(REPO_ROOT),
            "--output",
            str(output_path),
        ],
        check=True,
        cwd=REPO_ROOT,
    )


def build_packet(target_fqn: str, db_path: Path, title: str = "Comprehension Packet") -> str:
    con = sqlite3.connect(db_path)
    con.row_factory = sqlite3.Row
    try:
        target = _row_dict(con, "SELECT * FROM Symbols WHERE fqn = ?", (target_fqn,))
        if target is None:
            raise SystemExit(f"target {target_fqn} not found in {db_path}")

        module_fqn = target["module_fqn"]
        module = _row_dict(con, "SELECT * FROM Modules WHERE fqn = ?", (module_fqn,))

        # All distinct identifiers + resolved targets referenced from the probe target.
        refs = _row_dicts(
            con,
            """
            SELECT identifier, to_fqn, kind, resolution_status, resolution_reason, COUNT(*) AS occurrences
            FROM "References"
            WHERE from_fqn = ?
            GROUP BY identifier, to_fqn, kind, resolution_status, resolution_reason
            ORDER BY identifier, kind
            """,
            (target_fqn,),
        )

        referenced_fqns: list[str] = []
        seen: set[str] = set()
        for r in refs:
            if r["to_fqn"] and r["to_fqn"] not in seen:
                referenced_fqns.append(r["to_fqn"])
                seen.add(r["to_fqn"])

        referenced_rows: list[sqlite3.Row] = []
        for fqn in referenced_fqns:
            row = _row_dict(con, "SELECT * FROM Symbols WHERE fqn = ?", (fqn,))
            if row is not None:
                referenced_rows.append(row)

        # FormControls on the target's host form (when the host is a Form/Report module).
        form_controls: list[sqlite3.Row] = []
        if module and module["kind"] in ("Form", "Report"):
            form_controls = _row_dicts(
                con,
                "SELECT * FROM FormControls WHERE form_fqn = ? ORDER BY control_name",
                (module_fqn,),
            )

        # The EventBinding that fires the target (handler_fqn = target_fqn).
        bindings = _row_dicts(
            con,
            "SELECT * FROM EventBindings WHERE handler_fqn = ? ORDER BY event_name",
            (target_fqn,),
        )

        # Tables that the controls on this form bind to.
        bound_tables = sorted(
            {
                c["bound_table_or_query"]
                for c in form_controls
                if c["bound_table_or_query"]
            }
        )
        table_rows: list[sqlite3.Row] = []
        for t in bound_tables:
            row = _row_dict(
                con,
                "SELECT * FROM Tables WHERE fqn = ?",
                (t,),
            )
            if row is not None:
                table_rows.append(row)

        out = [
            f"# {title} — `{target_fqn}`",
            "",
            "_Generated by `tools/symbol-db/probe/build_packet.py`._",
            "",
            "This is the full evidence package given to the comprehension probe.",
            "All `Interpretation*` columns on the **target row only** are intentionally",
            "blanked. Every directly-referenced symbol carries its `Interpretation1` so",
            "the probe can use callee summaries instead of re-reading callee source.",
            "",
            "## Target Symbol",
            "",
        ]

        blanked_target = dict(target)
        for k in (
            "Interpretation1", "Justification1", "Confidence1", "Agent1", "InterpretedAt1",
            "Interpretation2", "Justification2", "Confidence2", "Agent2", "InterpretedAt2",
            "Interpretation3", "Justification3", "Confidence3", "Agent3", "InterpretedAt3",
        ):
            blanked_target[k] = None

        target_fields = [
            "fqn", "module_fqn", "name", "kind", "access", "data_type",
            "declared_in_fqn", "source_line_start", "source_line_end",
            "signature_string", "body_hash_md5", "is_dead", "dead_code_reason",
            "equivalence_canonical_fqn", "human_review_required",
            "Interpretation1", "Justification1", "Confidence1",
            "Interpretation2", "Justification2", "Confidence2",
            "Interpretation3", "Justification3", "Confidence3",
            "source_body",
        ]

        out.append(_format_record(target_fqn, blanked_target, target_fields))

        out.append("## Containing Module")
        out.append("")
        if module is not None:
            out.append(_format_record(module_fqn, module, module.keys()))
        else:
            out.append("_(module row not found)_")
            out.append("")

        out.append("## Direct References (raw, by-identifier)")
        out.append("")
        out.append("| identifier | kind | to_fqn | resolution_status | reason | occurrences |")
        out.append("|---|---|---|---|---|---|")
        for r in refs:
            out.append(
                "| `{ident}` | {kind} | {to} | {res} | {reason} | {n} |".format(
                    ident=r["identifier"],
                    kind=r["kind"] or "",
                    to=f"`{r['to_fqn']}`" if r["to_fqn"] else "_(unresolved)_",
                    res=r["resolution_status"] or "",
                    reason=r["resolution_reason"] or "",
                    n=r["occurrences"],
                )
            )
        out.append("")

        out.append("## Referenced Symbol Rows (with Interpretation1 visible)")
        out.append("")
        if not referenced_rows:
            out.append("_(no resolved targets — every direct reference was unresolved or to-built-in)_")
            out.append("")
        for row in referenced_rows:
            ref_fields = [
                "fqn", "module_fqn", "name", "kind", "access", "data_type",
                "declared_in_fqn", "source_line_start", "source_line_end",
                "signature_string", "is_dead", "Interpretation1", "Justification1",
                "Confidence1", "Agent1", "source_body",
            ]
            out.append(_format_record(row["fqn"], row, ref_fields))

        out.append("## Directly Referenced Procedure Bodies")
        out.append("")
        procedure_rows = [
            row for row in referenced_rows
            if row["kind"] in {"function", "sub", "property"}
        ]
        if not procedure_rows:
            out.append("_(no directly referenced procedures resolved in the source DB)_")
            out.append("")
        for row in procedure_rows:
            procedure_fields = [
                "fqn", "signature_string", "Interpretation1", "source_body",
            ]
            out.append(_format_record(row["fqn"], row, procedure_fields))

        out.append("## FormControls (on target's host form)")
        out.append("")
        if not form_controls:
            out.append("_(target's host module is not a Form/Report, or no controls registered)_")
            out.append("")
        else:
            out.append("| control_name | type | label | control_source | row_source | bound_table_or_query | bound_column |")
            out.append("|---|---|---|---|---|---|---|")
            for c in form_controls:
                out.append(
                    "| `{n}` | {t} | {l} | {cs} | {rs} | {bt} | {bc} |".format(
                        n=c["control_name"],
                        t=c["control_type"],
                        l=c["label_text"] or "",
                        cs=c["control_source"] or "",
                        rs=c["row_source"] or "",
                        bt=c["bound_table_or_query"] or "",
                        bc=c["bound_column"] or "",
                    )
                )
            out.append("")

        out.append("## EventBinding (fires the target)")
        out.append("")
        if not bindings:
            out.append("_(no event binding rows reference this handler)_")
            out.append("")
        for b in bindings:
            out.append(_format_record(
                f"{b['form_fqn']} :: {b['control_name'] or '<form-level>'} :: {b['event_name']}",
                b, b.keys(),
            ))

        out.append("## Tables Bound by Controls on This Form")
        out.append("")
        if not table_rows:
            out.append("_(no controls on this form bind to a Tables row that exists in the symbol DB)_")
            out.append("")
        for t in table_rows:
            table_fields = [
                "fqn", "primary_key_column", "candidate_label_column",
                "is_lookup_table_score", "is_lookup_table_reason",
                "Interpretation1", "Justification1", "Confidence1", "Agent1",
            ]
            out.append(_format_record(t["fqn"], t, table_fields))

        return "\n".join(out)
    finally:
        con.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--target", default=DEFAULT_TARGET,
                        help=f"target Symbol FQN (default: {DEFAULT_TARGET})")
    parser.add_argument("--db", default=str(DB_PATH),
                        help=f"path to symbol-db.sqlite (default: {DB_PATH})")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT),
                        help=f"output markdown path (default: {DEFAULT_OUTPUT})")
    parser.add_argument(
        "--title",
        default="Comprehension Packet",
        help="markdown title prefix for the generated packet",
    )
    parser.add_argument(
        "--rebuild-fixed-db",
        action="store_true",
        help="build a temporary Session-1-fixed symbol DB before generating the packet",
    )
    args = parser.parse_args()

    with tempfile.TemporaryDirectory() as tmp_dir:
        db_path = Path(args.db)
        if args.rebuild_fixed_db:
            db_path = Path(tmp_dir) / "probe-fixed-symbol-db.sqlite"
            rebuild_fixed_db(db_path)

        packet = build_packet(args.target, db_path, title=args.title)
        Path(args.output).parent.mkdir(parents=True, exist_ok=True)
        Path(args.output).write_text(packet, encoding="utf-8")
        print(f"wrote {args.output} ({len(packet):,} bytes)")


if __name__ == "__main__":
    main()
