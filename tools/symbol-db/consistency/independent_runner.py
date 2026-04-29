"""Cross-provider independent re-interpretation runner.

For each of the 42 sampled items, builds a neutral-evidence prompt
(no callee summaries, no prior interpretation context) and routes it to
a specific provider via ``query()``.

Opinion 2 → Opus (Anthropic)
Opinion 3 → GPT-5.4 (OpenAI)

Both runs are idempotent: an item whose target opinion slot is already
populated is skipped.  Transient provider errors, prompt-build failures,
and parse failures are recorded as distinct history reasons so the audit
trail is accurate.

Usage (from ``tools/symbol-db/``)::

    # opinion 2 (Opus)
    ../../.venv/Scripts/python.exe -m consistency.independent_runner \\
        --db symbol-db.sqlite \\
        --sample consistency/sample.json \\
        --opinion 2

    # opinion 3 (GPT-5.4)
    ../../.venv/Scripts/python.exe -m consistency.independent_runner \\
        --db symbol-db.sqlite \\
        --sample consistency/sample.json \\
        --opinion 3
"""

from __future__ import annotations

import argparse
import dataclasses
import datetime as _dt
import importlib.util
import sqlite3
import sys
from pathlib import Path

# ── bootstrap the AI router ────────────────────────────────────────────────
_ROUTER_PATH = Path(__file__).resolve().parents[3] / "ai-router" / "__init__.py"


def _load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router", str(_ROUTER_PATH),
        submodule_search_locations=[str(_ROUTER_PATH.parent)],
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


# ── local imports (relative to tools/symbol-db/) ──────────────────────────
_INTERPRET_DIR = Path(__file__).resolve().parents[1] / "interpret"
sys.path.insert(0, str(_INTERPRET_DIR.parent))

from interpret import db as _db                   # noqa: E402
from interpret import prompt_builder as _pb       # noqa: E402
from interpret.parsing import parse_interpretation  # noqa: E402

from .sampler import load_sample                  # noqa: E402

# ── opinion-slot → model mapping ──────────────────────────────────────────
_OPINION_MODEL = {
    2: "opus",
    3: "gpt-5-4",
}

_SESSION_SET = "vba-symbol-db"
_SESSION_NUMBER = 5


# ── skip reasons ───────────────────────────────────────────────────────────
def _skip_reason_provider(opinion_slot: int) -> str:
    return f"skipped-provider-error-opinion{opinion_slot}"


def _skip_reason_prompt(opinion_slot: int) -> str:
    return f"skipped-prompt-error-opinion{opinion_slot}"


def _skip_reason_parse(opinion_slot: int) -> str:
    return f"skipped-parse-failure-opinion{opinion_slot}"


# ── helpers ────────────────────────────────────────────────────────────────


def _already_written(con: sqlite3.Connection, fqn: str, opinion_slot: int, table: str) -> bool:
    """True if the target opinion column is already populated in the correct table."""
    row = con.execute(
        f"SELECT Interpretation{opinion_slot} FROM {table} WHERE fqn = ?",
        (fqn,),
    ).fetchone()
    if row is None:
        return False
    return row[0] is not None


def _already_skipped(
    con: sqlite3.Connection, fqn: str, opinion_slot: int, reason: str
) -> bool:
    row = con.execute(
        """
        SELECT 1 FROM InterpretationHistory
        WHERE fqn = ? AND opinion_slot = ? AND reason = ?
        LIMIT 1
        """,
        (fqn, opinion_slot, reason),
    ).fetchone()
    return row is not None


def _write_skip_for_opinion(
    con: sqlite3.Connection,
    fqn: str,
    fqn_kind: str,
    opinion_slot: int,
    reason: str,
    build_run_id: int,
) -> None:
    """Write an idempotent skip row keyed on (fqn, opinion_slot, reason)."""
    if _already_skipped(con, fqn, opinion_slot, reason):
        return
    con.execute(
        """
        INSERT INTO InterpretationHistory
            (fqn, fqn_kind, opinion_slot, interpretation, justification,
             confidence, agent, prompt_hash, source_hash_at_time,
             written_at, build_run_id, reason)
        VALUES (?, ?, ?, ?, NULL, NULL, ?, NULL, NULL, ?, ?, ?)
        """,
        (
            fqn,
            fqn_kind,
            opinion_slot,
            f"(skipped: {reason})",
            "skip-policy",
            _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
            build_run_id,
            reason,
        ),
    )


def _fqn_kind(table: str) -> str:
    return {
        "Symbols": "symbol",
        "Macros":  "macro",
        "Tables":  "table",
        "Queries": "query",
    }[table]


def _build_neutral_prompt(
    con: sqlite3.Connection,
    item: dict,
) -> str:
    """Build the neutral-evidence prompt for the given sampled item.

    Key invariant: row objects passed to the Session-4 prompt builders have
    their prior-interpretation fields cleared (interpretation1, confidence1)
    so no previously-written opinion can leak into the prompt even if a
    future builder update were to render those fields.  Callee summaries and
    enum-parent interpretation are also withheld.
    """
    table = item["table"]
    fqn = item["fqn"]

    if table == "Symbols":
        sym = _get_symbol(con, fqn)
        if sym is None:
            raise ValueError(f"Symbol not found in DB: {fqn!r}")
        # C1 fix: clear prior-interpretation fields before passing to builder.
        sym = dataclasses.replace(sym, interpretation1=None, confidence1=None)
        caller_fqns = _db.callees_of(con, fqn)
        # Supply caller FQN names but withhold all interpretation text.
        caller_names_only: list[tuple[str, str]] = [
            (c, "(interpretation withheld for independence)") for c in caller_fqns[:8]
        ]
        prompt = _pb.build_symbol_prompt(
            con,
            sym,
            callee_summaries=[],
            caller_summaries=caller_names_only,
            enum_interpretation=None,
            classification=None,
        )
        return prompt.text

    if table == "Macros":
        row = con.execute(
            "SELECT fqn, source_xml, is_dead FROM Macros WHERE fqn = ?",
            (fqn,),
        ).fetchone()
        if row is None:
            raise ValueError(f"Macro not found: {fqn!r}")
        # C1 fix: construct row with interpretation1=None explicitly.
        macro = _db.MacroRow(
            fqn=row["fqn"],
            source_xml=row["source_xml"],
            is_dead=bool(row["is_dead"]),
            interpretation1=None,
        )
        actions = [
            dict(r)
            for r in con.execute(
                "SELECT position, action_name, arguments_json FROM MacroActions "
                "WHERE macro_fqn = ? ORDER BY position",
                (fqn,),
            ).fetchall()
        ]
        prompt = _pb.build_macro_prompt(macro, actions)
        return prompt.text

    if table == "Tables":
        row = con.execute(
            """SELECT fqn, primary_key_column, candidate_label_column,
                      is_lookup_table_score, is_lookup_table_reason
               FROM Tables WHERE fqn = ?""",
            (fqn,),
        ).fetchone()
        if row is None:
            raise ValueError(f"Table not found: {fqn!r}")
        # C1 fix: construct row with interpretation1=None explicitly.
        table_row = _db.TableRow(
            fqn=row["fqn"],
            primary_key_column=row["primary_key_column"],
            candidate_label_column=row["candidate_label_column"],
            is_lookup_table_score=row["is_lookup_table_score"],
            is_lookup_table_reason=row["is_lookup_table_reason"],
            interpretation1=None,
        )
        prompt = _pb.build_table_prompt(con, table_row)
        return prompt.text

    if table == "Queries":
        row = con.execute(
            """SELECT fqn, source_sql, normalized_sql, is_dead,
                      equivalence_canonical_fqn
               FROM Queries WHERE fqn = ?""",
            (fqn,),
        ).fetchone()
        if row is None:
            raise ValueError(f"Query not found: {fqn!r}")
        # C1 fix: construct row with interpretation1=None explicitly.
        query_row = _db.QueryRow(
            fqn=row["fqn"],
            source_sql=row["source_sql"],
            normalized_sql=row["normalized_sql"],
            is_dead=bool(row["is_dead"]),
            equivalence_canonical_fqn=row["equivalence_canonical_fqn"],
            interpretation1=None,
        )
        prompt = _pb.build_query_prompt(con, query_row)
        return prompt.text

    raise ValueError(f"Unknown table {table!r} for fqn={fqn!r}")


def _get_symbol(con: sqlite3.Connection, fqn: str):
    rows = con.execute(
        """
        SELECT fqn, module_fqn, name, kind, access, data_type,
               declared_in_fqn, source_body, signature_string,
               body_hash_md5, is_dead, dead_code_reason,
               equivalence_canonical_fqn, Interpretation1, Confidence1
        FROM Symbols WHERE fqn = ?
        """,
        (fqn,),
    ).fetchone()
    if rows is None:
        return None
    return _db.SymbolRow(
        fqn=rows["fqn"],
        module_fqn=rows["module_fqn"],
        name=rows["name"],
        kind=rows["kind"],
        access=rows["access"],
        data_type=rows["data_type"],
        declared_in_fqn=rows["declared_in_fqn"],
        source_body=rows["source_body"],
        signature_string=rows["signature_string"],
        body_hash_md5=rows["body_hash_md5"],
        is_dead=bool(rows["is_dead"]),
        dead_code_reason=rows["dead_code_reason"],
        equivalence_canonical_fqn=rows["equivalence_canonical_fqn"],
        interpretation1=rows["Interpretation1"],
        confidence1=rows["Confidence1"],
    )


def _is_transient(exc: Exception) -> bool:
    msg = str(exc).lower()
    return any(x in msg for x in ("503", "429", "timeout", "rate limit", "overloaded"))


# ── main runner ────────────────────────────────────────────────────────────


def run_opinion(
    db_path: str,
    sample_path: str,
    opinion_slot: int,
    *,
    dry_run: bool = False,
) -> dict:
    """Run one opinion pass (2 or 3) over the sampled items.

    Returns a summary dict with per-item results and aggregate stats.
    """
    if opinion_slot not in (2, 3):
        raise ValueError("opinion_slot must be 2 or 3")

    ar = _load_ai_router()
    query = ar.query

    model = _OPINION_MODEL[opinion_slot]
    items = load_sample(sample_path)

    con = _db.open_db(db_path)
    build_run_id = _db.open_build_run(
        con,
        orchestrator="claude-code",
        session_set=_SESSION_SET,
        session_number=_SESSION_NUMBER,
        notes=f"Session 5 independent cross-provider: opinion {opinion_slot} ({model})",
    )

    stats = {
        "opinion_slot": opinion_slot,
        "model": model,
        "written": 0,
        "skipped_already_written": 0,
        "skipped_provider_error": 0,
        "skipped_prompt_error": 0,
        "skipped_parse_failure": 0,
        "cost_usd": 0.0,
        "per_item": [],
    }

    try:
        for item in items:
            fqn = item["fqn"]
            table = item["table"]
            kind = _fqn_kind(table)

            # Idempotency: skip if already written (M1 fix: table-scoped check).
            if _already_written(con, fqn, opinion_slot, table):
                stats["skipped_already_written"] += 1
                stats["per_item"].append({"fqn": fqn, "status": "already_written"})
                continue

            if dry_run:
                stats["per_item"].append({"fqn": fqn, "status": "dry_run"})
                continue

            # Build neutral-evidence prompt.
            try:
                prompt_text = _build_neutral_prompt(con, item)
            except Exception as exc:
                reason = _skip_reason_prompt(opinion_slot)
                print(f"[WARN] prompt build failed for {fqn}: {exc}", flush=True)
                _write_skip_for_opinion(con, fqn, kind, opinion_slot, reason, build_run_id)
                con.commit()
                stats["skipped_prompt_error"] += 1
                stats["per_item"].append({"fqn": fqn, "status": "prompt_error", "error": str(exc)})
                continue

            prompt_hash = _db.hash_prompt(prompt_text)
            source_row = con.execute(
                "SELECT source_body FROM Symbols WHERE fqn=?", (fqn,)
            ).fetchone()
            source_hash = _db.hash_source(source_row[0] if source_row else None)

            # Route the call.
            try:
                result = query(
                    model,
                    prompt_text,
                    task_type="documentation",
                    session_set=_SESSION_SET,
                    session_number=_SESSION_NUMBER,
                )
            except Exception as exc:
                if _is_transient(exc):
                    reason = _skip_reason_provider(opinion_slot)
                    _write_skip_for_opinion(con, fqn, kind, opinion_slot, reason, build_run_id)
                    con.commit()
                    stats["skipped_provider_error"] += 1
                    stats["per_item"].append({"fqn": fqn, "status": "provider_error", "error": str(exc)})
                    print(f"[WARN] transient error for {fqn}: {exc}", flush=True)
                    continue
                raise

            stats["cost_usd"] += result.total_cost_usd or 0.0

            parsed = parse_interpretation(result.content)
            if parsed is None:
                # One retry with a reminder.
                reminder = (
                    result.content
                    + '\n\nPlease output the JSON block in this exact format:\n'
                    '```json\n{"interpretation": "...", "justification": "...", "confidence": 0.0}\n```'
                )
                try:
                    result2 = query(
                        model,
                        reminder,
                        task_type="documentation",
                        session_set=_SESSION_SET,
                        session_number=_SESSION_NUMBER,
                    )
                    stats["cost_usd"] += result2.total_cost_usd or 0.0
                    parsed = parse_interpretation(result2.content)
                except Exception as exc2:
                    if _is_transient(exc2):
                        reason = _skip_reason_provider(opinion_slot)
                        _write_skip_for_opinion(con, fqn, kind, opinion_slot, reason, build_run_id)
                        con.commit()
                        stats["skipped_provider_error"] += 1
                        stats["per_item"].append({"fqn": fqn, "status": "provider_error_retry"})
                        continue
                    raise

            if parsed is None:
                # M3 fix: distinct reason for parse failure.
                reason = _skip_reason_parse(opinion_slot)
                _write_skip_for_opinion(con, fqn, kind, opinion_slot, reason, build_run_id)
                con.commit()
                stats["skipped_parse_failure"] += 1
                stats["per_item"].append({"fqn": fqn, "status": "parse_failure"})
                print(f"[WARN] parse failure for {fqn}", flush=True)
                continue

            _db.write_interpretation(
                con,
                fqn=fqn,
                fqn_kind=kind,
                opinion_slot=opinion_slot,
                interpretation=parsed.interpretation,
                justification=parsed.justification,
                confidence=parsed.confidence,
                agent=result.model_id or model,
                prompt_hash=prompt_hash,
                source_hash_at_time=source_hash,
                build_run_id=build_run_id,
                reason="independent-cross-provider",
            )
            con.commit()
            stats["written"] += 1
            stats["per_item"].append({
                "fqn": fqn,
                "status": "written",
                "confidence": parsed.confidence,
                "cost_usd": result.total_cost_usd,
            })
            print(f"  [{stats['written']:>3}] {fqn[:70]} conf={parsed.confidence:.2f}", flush=True)

    finally:
        _db.close_build_run(
            con,
            build_run_id,
            interpretations_written=stats["written"],
            total_routed_cost_usd=stats["cost_usd"],
        )
        con.close()

    return stats


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run cross-provider independent re-interpretation")
    parser.add_argument("--db", default="symbol-db.sqlite", help="Path to symbol-db.sqlite")
    parser.add_argument("--sample", default="consistency/sample.json", help="Path to sample.json")
    parser.add_argument("--opinion", type=int, choices=[2, 3], required=True,
                        help="Opinion slot to populate (2=Opus, 3=GPT-5.4)")
    parser.add_argument("--dry-run", action="store_true", help="Build prompts but do not route")
    args = parser.parse_args(argv)

    print(f"Running opinion {args.opinion} ({_OPINION_MODEL[args.opinion]})...", flush=True)
    stats = run_opinion(args.db, args.sample, args.opinion, dry_run=args.dry_run)

    print(f"\nDone. written={stats['written']}  "
          f"already_written={stats['skipped_already_written']}  "
          f"provider_errors={stats['skipped_provider_error']}  "
          f"prompt_errors={stats['skipped_prompt_error']}  "
          f"parse_failures={stats['skipped_parse_failure']}  "
          f"cost=${stats['cost_usd']:.4f}")
    return stats


if __name__ == "__main__":
    main()
