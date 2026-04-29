"""End-to-end interpretation runner.

The runner walks one module's interpretable symbols leaves-first,
routes each interpretation through ``ai_router.route``, and persists
the result back to the symbol DB.

Policy decisions implemented here (per the spec):

* Procedure-local module_var / module_const rows are skipped — their
  meaning lives in the enclosing procedure's interpretation. Recorded
  with reason ``skipped-procedure-local-var``.
* Deferred or dead symbols are skipped explicitly with reason
  ``skipped-deferred`` / ``skipped-dead``.
* MD5-equivalent symbols copy the canonical twin's Interpretation1
  with reason ``copied-from-md5-twin``.
* Confidence-thresholded escalation: opinion 2 fires when
  ``Confidence1 < opinion2_below``; opinion 3 fires when
  ``avg(Confidence1, Confidence2) < opinion3_avg_below``.

The runner expects ``ai_router.route`` to be already imported into the
caller's namespace and passed in as a parameter (we keep the runner
hermetic for tests).
"""

from __future__ import annotations

import json
import os
import sqlite3
from dataclasses import dataclass, field
from typing import Callable, Optional  # noqa: F401

from . import db
from . import prompt_builder as pb
from .parsing import Interpretation, parse_interpretation
from .topology import OrderedNode, order_leaves_first


@dataclass
class RunStats:
    interpreted: int = 0
    opinion2: int = 0
    opinion3: int = 0
    md5_copied: int = 0
    skipped_deferred: int = 0
    skipped_dead: int = 0
    skipped_procedure_local: int = 0
    skipped_unsupported_kind: int = 0
    parse_failures: int = 0
    cost_usd: float = 0.0
    per_symbol: list[dict] = field(default_factory=list)


def load_config(config_path: str) -> dict:
    with open(config_path, encoding="utf-8") as f:
        return json.load(f)


def schedule_module(
    con: sqlite3.Connection,
    module_fqn: str,
    config: dict,
) -> tuple[list[db.SymbolRow], list[OrderedNode]]:
    """Return the schedulable symbols and their leaves-first order.

    Excluded:

    * symbols whose kind is not in ``policy.interpret_kinds``
    * procedure-local variables (declared_in_fqn IS NOT NULL on
      module_var / module_const) when ``skip_procedure_local_vars``
    * deferred or MD5-twin symbols (interpretation comes from the
      canonical twin or is explicitly skipped — handled at run time)

    The returned schedule excludes deferred/dead/twin rows so they do
    not appear in the topological ordering; the runner emits skip
    history rows for them in a separate pass.
    """
    interpret_kinds = set(config["policy"]["interpret_kinds"])
    skip_proc_local = config["policy"].get("skip_procedure_local_vars", True)

    syms = db.list_symbols(con, module_fqn)
    schedulable: list[db.SymbolRow] = []
    for s in syms:
        if s.kind not in interpret_kinds:
            continue
        if (
            skip_proc_local
            and s.kind in ("module_var", "module_const")
            and s.declared_in_fqn is not None
        ):
            continue
        if s.is_dead:
            continue
        if s.equivalence_canonical_fqn is not None:
            continue
        schedulable.append(s)

    fqns = {s.fqn for s in schedulable}
    edges: dict[str, set[str]] = {}
    for s in schedulable:
        callees = db.callees_of(con, s.fqn)
        edges[s.fqn] = {c for c in callees if c in fqns}
    order = order_leaves_first(fqns, edges)
    by_fqn = {s.fqn: s for s in schedulable}
    # Reorder schedulable to match the topo order so the runner can
    # iterate in lockstep with `order`.
    ordered = [by_fqn[n.fqn] for n in order]
    return ordered, order


def interpret_module(
    con: sqlite3.Connection,
    module_fqn: str,
    *,
    route: Callable,
    config: dict,
    build_run_id: int,
    session_set: str,
    session_number: int,
    stop_when_total_reaches: Optional[int] = None,
    running_total: int = 0,
) -> RunStats:
    """Interpret all schedulable symbols in *module_fqn* leaves-first.

    ``stop_when_total_reaches`` is an optional cap honoured by the CLI
    ``--limit`` flag. When set, the loop stops cleanly once
    ``running_total + stats.interpreted`` reaches the cap. The wrapper
    raises no exception — the caller's outer module loop keeps the
    book-keeping.
    """
    stats = RunStats()
    ordered, schedule = schedule_module(con, module_fqn, config)

    # First, emit skip-history rows for the symbols we filtered out.
    _emit_skip_rows_for_excluded(con, module_fqn, config, build_run_id, stats)

    for sym, node in zip(ordered, schedule):
        if sym.interpretation1 is not None:
            # Re-run idempotency: already interpreted on a previous run.
            continue
        if (
            stop_when_total_reaches is not None
            and running_total + stats.interpreted >= stop_when_total_reaches
        ):
            break
        # Build callee summaries (already-interpreted leaves).
        callee_summaries = _collect_callee_summaries(con, sym, config)
        caller_summaries = _collect_caller_summaries(con, sym, max_count=8)

        enum_interp = None
        if sym.kind == "enum_value" and sym.declared_in_fqn:
            enum_interp = db.get_interpretation1(
                con, sym.declared_in_fqn, "symbol"
            )

        prompt = pb.build_symbol_prompt(
            con,
            sym,
            callee_summaries=callee_summaries,
            caller_summaries=caller_summaries,
            enum_interpretation=enum_interp,
        )
        result = _interpret_one(
            sym=sym,
            prompt_text=prompt.text,
            route=route,
            config=config,
            build_run_id=build_run_id,
            session_set=session_set,
            session_number=session_number,
            con=con,
            cycle_member=node.is_cycle_member,
        )
        stats.cost_usd += result["cost"]
        stats.per_symbol.append({
            "fqn": sym.fqn,
            "kind": sym.kind,
            "opinions": result["opinions"],
            "final_confidence": result["final_confidence"],
            "cost": result["cost"],
            "cycle_member": node.is_cycle_member,
        })
        if result["opinions"] >= 1:
            stats.interpreted += 1
        if result["opinions"] >= 2:
            stats.opinion2 += 1
        if result["opinions"] >= 3:
            stats.opinion3 += 1
        if result["parse_failure"]:
            stats.parse_failures += 1
        con.commit()

    # Pass 2: copy interpretations to MD5 twins.
    twin_count = _copy_interpretations_to_twins(con, module_fqn, build_run_id)
    stats.md5_copied += twin_count
    con.commit()
    return stats


def _emit_skip_rows_for_excluded(
    con: sqlite3.Connection,
    module_fqn: str,
    config: dict,
    build_run_id: int,
    stats: RunStats,
) -> None:
    interpret_kinds = set(config["policy"]["interpret_kinds"])
    skip_proc_local = config["policy"].get("skip_procedure_local_vars", True)

    def already_skipped(fqn: str, reason: str) -> bool:
        row = con.execute(
            "SELECT 1 FROM InterpretationHistory WHERE fqn=? AND opinion_slot=0 AND reason=? LIMIT 1",
            (fqn, reason),
        ).fetchone()
        return row is not None

    for s in db.list_symbols(con, module_fqn):
        # Already interpreted on a prior run — skip silently.
        if s.interpretation1 is not None:
            continue
        if s.is_dead:
            if not already_skipped(s.fqn, "skipped-dead"):
                db.write_skip(
                    con, fqn=s.fqn, fqn_kind="symbol",
                    build_run_id=build_run_id, reason="skipped-dead",
                    note=s.dead_code_reason or "",
                )
            stats.skipped_dead += 1
            continue
        if s.kind not in interpret_kinds:
            if not already_skipped(s.fqn, "skipped-unsupported-kind"):
                db.write_skip(
                    con, fqn=s.fqn, fqn_kind="symbol",
                    build_run_id=build_run_id, reason="skipped-unsupported-kind",
                    note=f"kind={s.kind}",
                )
            stats.skipped_unsupported_kind += 1
            continue
        if (
            skip_proc_local
            and s.kind in ("module_var", "module_const")
            and s.declared_in_fqn is not None
        ):
            if not already_skipped(s.fqn, "skipped-procedure-local-var"):
                db.write_skip(
                    con, fqn=s.fqn, fqn_kind="symbol",
                    build_run_id=build_run_id, reason="skipped-procedure-local-var",
                    note=f"declared_in={s.declared_in_fqn}",
                )
            stats.skipped_procedure_local += 1
            continue
        # Twin: handled in the post-pass copy step.


def _collect_callee_summaries(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
    config: dict,
) -> list[tuple[str, str]]:
    max_count = int(config.get("max_callee_summaries", 12))
    callees = db.callees_of(con, sym.fqn)
    summaries: list[tuple[str, str]] = []
    seen = set()
    for c in callees:
        if c in seen:
            continue
        seen.add(c)
        # Try Symbols, then Macros, then Tables, then Queries.
        for kind in ("symbol", "macro", "table", "query"):
            interp = db.get_interpretation1(con, c, kind)
            if interp:
                summaries.append((c, interp))
                break
        else:
            # No interpretation yet — emit a placeholder so the
            # downstream prompt knows the callee is reachable but
            # uninterpreted. Cap at 6 such placeholders.
            if sum(1 for _, s in summaries if s.startswith("(uninterp")) < 6:
                summaries.append((c, "(uninterpreted; out of scope or upstream cycle)"))
        if len(summaries) >= max_count:
            break
    return summaries


def _collect_caller_summaries(
    con: sqlite3.Connection, sym: db.SymbolRow, *, max_count: int
) -> list[tuple[str, str]]:
    rows = db.references_to(con, sym.fqn)
    out: list[tuple[str, str]] = []
    seen = set()
    for r in rows:
        caller = r["from_fqn"]
        if caller in seen:
            continue
        seen.add(caller)
        # Caller summaries are intentionally short — just the FQN
        # plus its kind tag, so the model knows it is interpreting
        # a callee in context but does not get drowned in summaries.
        out.append((caller, r.get("caller_kind") or "(non-ui)"))
        if len(out) >= max_count:
            break
    return out


def _interpret_one(
    *,
    sym: db.SymbolRow,
    prompt_text: str,
    route: Callable,
    config: dict,
    build_run_id: int,
    session_set: str,
    session_number: int,
    con: sqlite3.Connection,
    cycle_member: bool,
) -> dict:
    thresholds = config["thresholds"]
    task_type = config.get("task_type", "documentation")
    complexity = int(config.get("complexity_hints", {}).get(sym.kind, 30))
    src_hash = db.hash_source(sym.source_body)
    prompt_hash = db.hash_prompt(prompt_text)

    cost = 0.0
    opinions = 0
    confidences: list[float] = []
    parse_failure = False

    for slot in (1, 2, 3):
        if slot == 2 and not _should_run_opinion2(thresholds, confidences):
            break
        if slot == 3 and not _should_run_opinion3(thresholds, confidences):
            break

        try:
            result = _route_with_transient_skip(
                con=con, fqn=sym.fqn, kind="symbol",
                build_run_id=build_run_id, slot_label=f"opinion {slot}",
                route=route, content=prompt_text, task_type=task_type,
                complexity_hint=complexity, session_set=session_set,
                session_number=session_number,
            )
        except _TransientSkipped:
            return {
                "opinions": opinions,
                "final_confidence": confidences[-1] if confidences else 0.0,
                "cost": cost,
                "parse_failure": parse_failure,
                "skipped_transient": True,
            }
        cost += result.total_cost_usd
        parsed = parse_interpretation(result.content)
        if parsed is None:
            parse_failure = True
            # Re-prompt once with an explicit reminder.
            reminder = (
                "\n\n---\n"
                "Your previous response did not include a parseable JSON "
                "block. Please re-emit the response, ending with a JSON "
                "object of shape:\n"
                '{"interpretation": "...", "justification": "...", "confidence": 0.0}\n'
            )
            try:
                retry = _route_with_transient_skip(
                    con=con, fqn=sym.fqn, kind="symbol",
                    build_run_id=build_run_id, slot_label=f"opinion {slot} retry",
                    route=route, content=prompt_text + reminder,
                    task_type=task_type, complexity_hint=complexity,
                    session_set=session_set, session_number=session_number,
                )
            except _TransientSkipped:
                return {
                    "opinions": opinions,
                    "final_confidence": confidences[-1] if confidences else 0.0,
                    "cost": cost,
                    "parse_failure": parse_failure,
                    "skipped_transient": True,
                }
            cost += retry.total_cost_usd
            parsed = parse_interpretation(retry.content)
            if parsed is None:
                # Persist a low-confidence placeholder so downstream
                # dependents are not blocked. This still records an
                # opinion slot in InterpretationHistory.
                parsed = Interpretation(
                    interpretation=f"[parse-failure] raw response saved to history (model={result.model_name})",
                    justification=(retry.content or result.content or "")[:1500],
                    confidence=0.05,
                )

        reason = "first-opinion" if slot == 1 else (
            "low-confidence-escalation"
        )
        if cycle_member and slot == 1:
            reason = "first-pass; cycle member"
        db.write_interpretation(
            con,
            fqn=sym.fqn,
            fqn_kind="symbol",
            opinion_slot=slot,
            interpretation=parsed.interpretation,
            justification=parsed.justification,
            confidence=parsed.confidence,
            agent=result.model_name,
            prompt_hash=prompt_hash,
            source_hash_at_time=src_hash,
            build_run_id=build_run_id,
            reason=reason,
        )
        opinions = slot
        confidences.append(parsed.confidence)

    return {
        "opinions": opinions,
        "final_confidence": confidences[-1] if confidences else 0.0,
        "cost": cost,
        "parse_failure": parse_failure,
    }


def _should_run_opinion2(thresholds: dict, confidences: list[float]) -> bool:
    if not confidences:
        return False
    return confidences[0] < float(thresholds["opinion2_below"])


def _should_run_opinion3(thresholds: dict, confidences: list[float]) -> bool:
    if len(confidences) < 2:
        return False
    avg = (confidences[0] + confidences[1]) / 2.0
    return avg < float(thresholds["opinion3_avg_below"])


def _copy_interpretations_to_twins(
    con: sqlite3.Connection, module_fqn: str, build_run_id: int
) -> int:
    """Copy Interpretation1 from canonical twins to their hash-equivalent siblings.

    Returns the number of symbols updated.
    """
    rows = con.execute(
        """
        SELECT s.fqn, s.equivalence_canonical_fqn,
               c.Interpretation1, c.Justification1, c.Confidence1, c.Agent1
        FROM Symbols s
        JOIN Symbols c ON c.fqn = s.equivalence_canonical_fqn
        WHERE s.module_fqn = ?
          AND s.equivalence_canonical_fqn IS NOT NULL
          AND s.Interpretation1 IS NULL
          AND c.Interpretation1 IS NOT NULL
        """,
        (module_fqn,),
    ).fetchall()
    n = 0
    for r in rows:
        db.write_interpretation(
            con,
            fqn=r["fqn"],
            fqn_kind="symbol",
            opinion_slot=1,
            interpretation=r["Interpretation1"],
            justification=(r["Justification1"] or "") +
                "\n\n[copied from MD5-equivalent canonical " + r["equivalence_canonical_fqn"] + "]",
            confidence=r["Confidence1"] or 0.0,
            agent=r["Agent1"] or "md5-twin-copy",
            prompt_hash="",
            source_hash_at_time=None,
            build_run_id=build_run_id,
            reason="copied-from-md5-twin",
        )
        n += 1
    return n


def _already_skipped_history(
    con: sqlite3.Connection, fqn: str, reason: str
) -> bool:
    return con.execute(
        "SELECT 1 FROM InterpretationHistory WHERE fqn=? AND opinion_slot=0 AND reason=? LIMIT 1",
        (fqn, reason),
    ).fetchone() is not None


def interpret_macros(
    con: sqlite3.Connection,
    *,
    route: Callable,
    config: dict,
    build_run_id: int,
    session_set: str,
    session_number: int,
) -> RunStats:
    stats = RunStats()
    macros = db.list_macros(con)
    for m in macros:
        if m.interpretation1 is not None:
            continue
        if m.is_dead:
            if not _already_skipped_history(con, m.fqn, "skipped-dead"):
                db.write_skip(
                    con, fqn=m.fqn, fqn_kind="macro",
                    build_run_id=build_run_id, reason="skipped-dead",
                )
            stats.skipped_dead += 1
            continue
        actions = [
            dict(r)
            for r in con.execute(
                "SELECT position, action_name, arguments_json "
                "FROM MacroActions WHERE macro_fqn = ? ORDER BY position",
                (m.fqn,),
            ).fetchall()
        ]
        prompt = pb.build_macro_prompt(m, actions)
        try:
            result = route(
                content=prompt.text,
                task_type=config.get("task_type", "documentation"),
                complexity_hint=int(config.get("complexity_hints", {}).get("macro", 25)),
                session_set=session_set,
                session_number=session_number,
            )
        except Exception as exc:
            if _is_transient(exc):
                if not _already_skipped_history(con, m.fqn, "skipped-provider-error"):
                    db.write_skip(
                        con, fqn=m.fqn, fqn_kind="macro",
                        build_run_id=build_run_id,
                        reason="skipped-provider-error",
                        note=f"transient: {exc}",
                    )
                con.commit()
                continue
            raise
        stats.cost_usd += result.total_cost_usd
        parsed = parse_interpretation(result.content) or Interpretation(
            interpretation=f"[parse-failure] {result.model_name}",
            justification=(result.content or "")[:1500],
            confidence=0.05,
        )
        db.write_interpretation(
            con,
            fqn=m.fqn,
            fqn_kind="macro",
            opinion_slot=1,
            interpretation=parsed.interpretation,
            justification=parsed.justification,
            confidence=parsed.confidence,
            agent=result.model_name,
            prompt_hash=db.hash_prompt(prompt.text),
            source_hash_at_time=db.hash_source(m.source_xml),
            build_run_id=build_run_id,
            reason="first-opinion",
        )
        stats.interpreted += 1
        con.commit()
    return stats


_TRANSIENT_HTTP_HINTS = ("503", "502", "504", "timeout", "Timeout", "Service Unavailable")


def _is_transient(exc: BaseException) -> bool:
    msg = str(exc)
    return any(hint in msg for hint in _TRANSIENT_HTTP_HINTS)


class _TransientSkipped(Exception):
    """Raised by ``_route_with_transient_skip`` after the helper has already
    persisted a ``skipped-provider-error`` history row.

    Callers catch this to break out of their per-symbol / per-opinion
    loop cleanly without re-writing the audit row.
    """


def _route_with_transient_skip(
    *,
    con: sqlite3.Connection,
    fqn: str,
    kind: str,                    # 'symbol' | 'macro' | 'table' | 'query'
    build_run_id: int,
    slot_label: str,
    route: Callable,
    **route_kwargs,
):
    """Call ``route()`` and convert a transient HTTP error into an
    idempotent ``skipped-provider-error`` audit row plus
    ``_TransientSkipped`` for the caller to catch.

    Non-transient exceptions propagate unchanged.
    """
    try:
        return route(**route_kwargs)
    except Exception as exc:
        if not _is_transient(exc):
            raise
        if not _already_skipped_history(con, fqn, "skipped-provider-error"):
            db.write_skip(
                con, fqn=fqn, fqn_kind=kind,
                build_run_id=build_run_id,
                reason="skipped-provider-error",
                note=f"transient at {slot_label}: {exc}",
            )
        con.commit()
        raise _TransientSkipped() from exc


def emit_deferred_skips(
    con: sqlite3.Connection, *, build_run_id: int
) -> int:
    """Record `skipped-deferred` history rows for symbols inside any
    ``Modules.is_deferred = 1`` row.

    Idempotent: re-runs do not duplicate history rows.
    """
    deferred = [
        r["fqn"]
        for r in con.execute("SELECT fqn FROM Modules WHERE is_deferred = 1").fetchall()
    ]
    if not deferred:
        return 0
    n = 0
    for module_fqn in deferred:
        rows = con.execute(
            "SELECT fqn FROM Symbols WHERE module_fqn = ? AND Interpretation1 IS NULL",
            (module_fqn,),
        ).fetchall()
        for r in rows:
            if _already_skipped_history(con, r["fqn"], "skipped-deferred"):
                continue
            db.write_skip(
                con, fqn=r["fqn"], fqn_kind="symbol",
                build_run_id=build_run_id, reason="skipped-deferred",
                note=f"module {module_fqn} flagged deferred",
            )
            n += 1
    con.commit()
    return n


def interpret_queries(
    con: sqlite3.Connection,
    *,
    route: Callable,
    config: dict,
    build_run_id: int,
    session_set: str,
    session_number: int,
) -> RunStats:
    """Interpret every live, non-twin query.

    Dead queries get a `skipped-dead` audit row; MD5/text-hash twins
    have their interpretation copied from the canonical row in a
    post-pass, mirroring the symbol pipeline.
    """
    stats = RunStats()
    for q in db.list_queries(con):
        if q.interpretation1 is not None:
            continue
        if q.is_dead:
            if not _already_skipped_history(con, q.fqn, "skipped-dead"):
                db.write_skip(
                    con, fqn=q.fqn, fqn_kind="query",
                    build_run_id=build_run_id, reason="skipped-dead",
                )
            stats.skipped_dead += 1
            continue
        if q.equivalence_canonical_fqn is not None:
            # Twin — handled by the post-pass copy below.
            continue
        prompt = pb.build_query_prompt(con, q)
        try:
            result = route(
                content=prompt.text,
                task_type=config.get("task_type", "documentation"),
                complexity_hint=int(config.get("complexity_hints", {}).get("query", 30)),
                session_set=session_set,
                session_number=session_number,
            )
        except Exception as exc:
            if _is_transient(exc):
                if not _already_skipped_history(con, q.fqn, "skipped-provider-error"):
                    db.write_skip(
                        con, fqn=q.fqn, fqn_kind="query",
                        build_run_id=build_run_id,
                        reason="skipped-provider-error",
                        note=f"transient: {exc}",
                    )
                con.commit()
                continue
            raise
        stats.cost_usd += result.total_cost_usd
        parsed = parse_interpretation(result.content) or Interpretation(
            interpretation=f"[parse-failure] {result.model_name}",
            justification=(result.content or "")[:1500],
            confidence=0.05,
        )
        db.write_interpretation(
            con,
            fqn=q.fqn,
            fqn_kind="query",
            opinion_slot=1,
            interpretation=parsed.interpretation,
            justification=parsed.justification,
            confidence=parsed.confidence,
            agent=result.model_name,
            prompt_hash=db.hash_prompt(prompt.text),
            source_hash_at_time=db.hash_source(q.source_sql),
            build_run_id=build_run_id,
            reason="first-opinion",
        )
        stats.interpreted += 1
        con.commit()

    # Post-pass: twins copy from canonical.
    rows = con.execute(
        """
        SELECT q.fqn, q.equivalence_canonical_fqn,
               c.Interpretation1, c.Justification1, c.Confidence1, c.Agent1
        FROM Queries q
        JOIN Queries c ON c.fqn = q.equivalence_canonical_fqn
        WHERE q.equivalence_canonical_fqn IS NOT NULL
          AND q.Interpretation1 IS NULL
          AND c.Interpretation1 IS NOT NULL
        """
    ).fetchall()
    for r in rows:
        db.write_interpretation(
            con,
            fqn=r["fqn"],
            fqn_kind="query",
            opinion_slot=1,
            interpretation=r["Interpretation1"],
            justification=(r["Justification1"] or "") +
                f"\n\n[copied from query twin {r['equivalence_canonical_fqn']}]",
            confidence=r["Confidence1"] or 0.0,
            agent=r["Agent1"] or "twin-copy",
            prompt_hash="",
            source_hash_at_time=None,
            build_run_id=build_run_id,
            reason="copied-from-md5-twin",
        )
        stats.md5_copied += 1
    con.commit()
    return stats


def interpret_lookup_tables(
    con: sqlite3.Connection,
    *,
    route: Callable,
    config: dict,
    build_run_id: int,
    session_set: str,
    session_number: int,
    min_score: int = 50,
) -> RunStats:
    """Interpret tables whose lookup score >= min_score.

    The full Tables row set runs into the dozens; interpreting every
    table at first run defeats the cost-savings the dead-code /
    deferred filters give us. The lookup-score threshold limits
    interpretation to the tables that genuinely look like reference
    domains. Lower-score tables are recorded with a skip reason so a
    later run can promote them.
    """
    stats = RunStats()
    for t in db.list_tables(con):
        if t.interpretation1 is not None:
            continue
        score = t.is_lookup_table_score or 0
        if score < min_score:
            if not _already_skipped_history(con, t.fqn, "skipped-low-lookup-score"):
                db.write_skip(
                    con, fqn=t.fqn, fqn_kind="table",
                    build_run_id=build_run_id,
                    reason="skipped-low-lookup-score",
                    note=f"score={score} < {min_score}",
                )
            stats.skipped_unsupported_kind += 1
            continue
        prompt = pb.build_table_prompt(con, t)
        try:
            result = route(
                content=prompt.text,
                task_type=config.get("task_type", "documentation"),
                complexity_hint=int(config.get("complexity_hints", {}).get("lookup_table", 25)),
                session_set=session_set,
                session_number=session_number,
            )
        except Exception as exc:
            if _is_transient(exc):
                # Provider hiccup — log a skip and keep going so one
                # 503 does not abort the whole table pass.
                if not _already_skipped_history(con, t.fqn, "skipped-provider-error"):
                    db.write_skip(
                        con, fqn=t.fqn, fqn_kind="table",
                        build_run_id=build_run_id,
                        reason="skipped-provider-error",
                        note=f"transient: {exc}",
                    )
                stats.skipped_unsupported_kind += 1
                con.commit()
                continue
            raise
        stats.cost_usd += result.total_cost_usd
        parsed = parse_interpretation(result.content) or Interpretation(
            interpretation=f"[parse-failure] {result.model_name}",
            justification=(result.content or "")[:1500],
            confidence=0.05,
        )
        db.write_interpretation(
            con,
            fqn=t.fqn,
            fqn_kind="table",
            opinion_slot=1,
            interpretation=parsed.interpretation,
            justification=parsed.justification,
            confidence=parsed.confidence,
            agent=result.model_name,
            prompt_hash=db.hash_prompt(prompt.text),
            source_hash_at_time=None,
            build_run_id=build_run_id,
            reason="first-opinion",
        )
        stats.interpreted += 1
        con.commit()
    return stats
