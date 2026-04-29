"""SQLite read/write helpers for the interpretation runner.

Read paths return plain dicts; write paths run inside a transaction
opened by the caller. Every interpretation write also appends to
``InterpretationHistory``.
"""

from __future__ import annotations

import datetime as _dt
import hashlib
import json
import sqlite3
from dataclasses import dataclass
from typing import Iterable, Optional


# ── data shapes ────────────────────────────────────────────────────


@dataclass(frozen=True)
class SymbolRow:
    fqn: str
    module_fqn: str
    name: str
    kind: str
    access: Optional[str]
    data_type: Optional[str]
    declared_in_fqn: Optional[str]
    source_body: Optional[str]
    signature_string: Optional[str]
    body_hash_md5: Optional[str]
    is_dead: bool
    dead_code_reason: Optional[str]
    equivalence_canonical_fqn: Optional[str]
    interpretation1: Optional[str]
    confidence1: Optional[float]


@dataclass(frozen=True)
class MacroRow:
    fqn: str
    source_xml: str
    is_dead: bool
    interpretation1: Optional[str]


@dataclass(frozen=True)
class TableRow:
    fqn: str
    primary_key_column: Optional[str]
    candidate_label_column: Optional[str]
    is_lookup_table_score: Optional[int]
    is_lookup_table_reason: Optional[str]
    interpretation1: Optional[str]


@dataclass(frozen=True)
class QueryRow:
    fqn: str
    source_sql: str
    normalized_sql: Optional[str]
    is_dead: bool
    equivalence_canonical_fqn: Optional[str]
    interpretation1: Optional[str]


def open_db(path: str) -> sqlite3.Connection:
    con = sqlite3.connect(path)
    con.row_factory = sqlite3.Row
    con.execute("PRAGMA foreign_keys = ON")
    return con


# ── reads ──────────────────────────────────────────────────────────


def list_modules(con: sqlite3.Connection) -> list[dict]:
    return [dict(r) for r in con.execute("SELECT * FROM Modules ORDER BY fqn")]


def list_symbols(con: sqlite3.Connection, module_fqn: str) -> list[SymbolRow]:
    rows = con.execute(
        """
        SELECT fqn, module_fqn, name, kind, access, data_type,
               declared_in_fqn, source_body, signature_string,
               body_hash_md5, is_dead, dead_code_reason,
               equivalence_canonical_fqn, Interpretation1, Confidence1
        FROM Symbols
        WHERE module_fqn = ?
        ORDER BY source_line_start, fqn
        """,
        (module_fqn,),
    ).fetchall()
    return [
        SymbolRow(
            fqn=r["fqn"],
            module_fqn=r["module_fqn"],
            name=r["name"],
            kind=r["kind"],
            access=r["access"],
            data_type=r["data_type"],
            declared_in_fqn=r["declared_in_fqn"],
            source_body=r["source_body"],
            signature_string=r["signature_string"],
            body_hash_md5=r["body_hash_md5"],
            is_dead=bool(r["is_dead"]),
            dead_code_reason=r["dead_code_reason"],
            equivalence_canonical_fqn=r["equivalence_canonical_fqn"],
            interpretation1=r["Interpretation1"],
            confidence1=r["Confidence1"],
        )
        for r in rows
    ]


def list_macros(con: sqlite3.Connection) -> list[MacroRow]:
    rows = con.execute(
        "SELECT fqn, source_xml, is_dead, Interpretation1 FROM Macros ORDER BY fqn"
    ).fetchall()
    return [
        MacroRow(
            fqn=r["fqn"],
            source_xml=r["source_xml"],
            is_dead=bool(r["is_dead"]),
            interpretation1=r["Interpretation1"],
        )
        for r in rows
    ]


def list_tables(con: sqlite3.Connection) -> list[TableRow]:
    rows = con.execute(
        """
        SELECT fqn, primary_key_column, candidate_label_column,
               is_lookup_table_score, is_lookup_table_reason, Interpretation1
        FROM Tables
        ORDER BY fqn
        """
    ).fetchall()
    return [
        TableRow(
            fqn=r["fqn"],
            primary_key_column=r["primary_key_column"],
            candidate_label_column=r["candidate_label_column"],
            is_lookup_table_score=r["is_lookup_table_score"],
            is_lookup_table_reason=r["is_lookup_table_reason"],
            interpretation1=r["Interpretation1"],
        )
        for r in rows
    ]


def list_queries(con: sqlite3.Connection) -> list[QueryRow]:
    rows = con.execute(
        """
        SELECT fqn, source_sql, normalized_sql, is_dead,
               equivalence_canonical_fqn, Interpretation1
        FROM Queries
        ORDER BY fqn
        """
    ).fetchall()
    return [
        QueryRow(
            fqn=r["fqn"],
            source_sql=r["source_sql"],
            normalized_sql=r["normalized_sql"],
            is_dead=bool(r["is_dead"]),
            equivalence_canonical_fqn=r["equivalence_canonical_fqn"],
            interpretation1=r["Interpretation1"],
        )
        for r in rows
    ]


def references_from(con: sqlite3.Connection, from_fqn: str) -> list[dict]:
    """Return outgoing reference rows whose containing symbol is *from_fqn*."""
    return [
        dict(r)
        for r in con.execute(
            """
            SELECT identifier, to_fqn, kind, resolution_status, resolution_reason
            FROM [References] WHERE from_fqn = ?
            ORDER BY source_line, source_column
            """,
            (from_fqn,),
        ).fetchall()
    ]


def references_to(con: sqlite3.Connection, to_fqn: str) -> list[dict]:
    """Return incoming reference rows pointing at *to_fqn*."""
    return [
        dict(r)
        for r in con.execute(
            """
            SELECT from_fqn, identifier, kind, caller_kind
            FROM [References] WHERE to_fqn = ?
            ORDER BY from_fqn
            """,
            (to_fqn,),
        ).fetchall()
    ]


def event_binding_for(con: sqlite3.Connection, handler_fqn: str) -> Optional[dict]:
    row = con.execute(
        """
        SELECT form_fqn, control_name, event_name, handler_kind
        FROM EventBindings
        WHERE handler_fqn = ?
        LIMIT 1
        """,
        (handler_fqn,),
    ).fetchone()
    return dict(row) if row else None


def callees_of(con: sqlite3.Connection, from_fqn: str) -> list[str]:
    """Distinct resolved-callee FQNs for the given containing symbol."""
    rows = con.execute(
        """
        SELECT DISTINCT to_fqn FROM [References]
        WHERE from_fqn = ? AND to_fqn IS NOT NULL
        """,
        (from_fqn,),
    ).fetchall()
    return [r["to_fqn"] for r in rows]


def parameters_for(con: sqlite3.Connection, procedure_fqn: str) -> list[dict]:
    return [
        dict(r)
        for r in con.execute(
            """
            SELECT position, name, data_type, passing,
                   is_optional, is_paramarray, is_array, default_value
            FROM Parameters WHERE procedure_fqn = ?
            ORDER BY position
            """,
            (procedure_fqn,),
        ).fetchall()
    ]


def get_interpretation1(con: sqlite3.Connection, fqn: str, kind: str) -> Optional[str]:
    """Look up Interpretation1 across Symbols / Macros / Tables / Queries."""
    table = {
        "symbol": "Symbols",
        "macro": "Macros",
        "table": "Tables",
        "query": "Queries",
    }[kind]
    row = con.execute(
        f"SELECT Interpretation1 FROM {table} WHERE fqn = ?", (fqn,)
    ).fetchone()
    return row["Interpretation1"] if row else None


# ── writes ─────────────────────────────────────────────────────────


def open_build_run(
    con: sqlite3.Connection,
    *,
    orchestrator: str,
    session_set: str,
    session_number: int,
    notes: str = "",
) -> int:
    cur = con.execute(
        """
        INSERT INTO BuildRuns
            (started_at, orchestrator, session_set, session_number, notes)
        VALUES (?, ?, ?, ?, ?)
        """,
        (_now_iso(), orchestrator, session_set, session_number, notes),
    )
    con.commit()
    return cur.lastrowid


def close_build_run(
    con: sqlite3.Connection,
    run_id: int,
    *,
    interpretations_written: int,
    total_routed_cost_usd: float,
) -> None:
    con.execute(
        """
        UPDATE BuildRuns SET finished_at = ?,
                             interpretations_written = ?,
                             total_routed_cost_usd = ?
        WHERE run_id = ?
        """,
        (_now_iso(), interpretations_written, total_routed_cost_usd, run_id),
    )
    con.commit()


_FQN_TABLE = {
    "symbol": "Symbols",
    "macro": "Macros",
    "table": "Tables",
    "query": "Queries",
}


def write_interpretation(
    con: sqlite3.Connection,
    *,
    fqn: str,
    fqn_kind: str,                      # 'symbol' | 'macro' | 'table' | 'query'
    opinion_slot: int,                  # 1, 2, or 3
    interpretation: str,
    justification: str,
    confidence: float,
    agent: str,
    prompt_hash: str,
    source_hash_at_time: Optional[str],
    build_run_id: int,
    reason: str,
) -> None:
    """Write opinion *N* into the matching parallel columns and append a
    history row in the same transaction.
    """
    table = _FQN_TABLE[fqn_kind]
    if opinion_slot not in (1, 2, 3):
        raise ValueError(f"opinion_slot must be 1, 2, or 3 (got {opinion_slot})")
    now = _now_iso()
    sql = f"""
        UPDATE {table}
        SET Interpretation{opinion_slot} = ?,
            Justification{opinion_slot} = ?,
            Confidence{opinion_slot} = ?,
            Agent{opinion_slot} = ?,
            InterpretedAt{opinion_slot} = ?
        WHERE fqn = ?
    """
    con.execute(sql, (interpretation, justification, confidence, agent, now, fqn))
    con.execute(
        """
        INSERT INTO InterpretationHistory
            (fqn, fqn_kind, opinion_slot, interpretation, justification,
             confidence, agent, prompt_hash, source_hash_at_time,
             written_at, build_run_id, reason)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            fqn,
            fqn_kind,
            opinion_slot,
            interpretation,
            justification,
            confidence,
            agent,
            prompt_hash,
            source_hash_at_time,
            now,
            build_run_id,
            reason,
        ),
    )


def write_skip(
    con: sqlite3.Connection,
    *,
    fqn: str,
    fqn_kind: str,
    build_run_id: int,
    reason: str,
    note: str = "",
) -> None:
    """Record an explicit skip in InterpretationHistory.

    The Symbols/Macros/Tables/Queries row is left with NULL Interpretation
    columns. The history record makes the skip auditable and, in particular,
    detectable when a previously-dead symbol becomes live in a future run.
    """
    con.execute(
        """
        INSERT INTO InterpretationHistory
            (fqn, fqn_kind, opinion_slot, interpretation, justification,
             confidence, agent, prompt_hash, source_hash_at_time,
             written_at, build_run_id, reason)
        VALUES (?, ?, 0, ?, NULL, NULL, ?, NULL, NULL, ?, ?, ?)
        """,
        (
            fqn,
            fqn_kind,
            note or f"(skipped: {reason})",
            "skip-policy",
            _now_iso(),
            build_run_id,
            reason,
        ),
    )


# ── helpers ────────────────────────────────────────────────────────


def _now_iso() -> str:
    return _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds")


def hash_prompt(prompt_text: str) -> str:
    return hashlib.md5(prompt_text.encode("utf-8")).hexdigest()


def hash_source(source: Optional[str]) -> Optional[str]:
    if source is None:
        return None
    return hashlib.md5(source.encode("utf-8")).hexdigest()


def coalesce_iter(*its: Iterable) -> list:
    out: list = []
    for it in its:
        out.extend(it)
    return out
