"""CLI entry point for the symbol-db interpretation pipeline.

Usage::

    python -m tools.symbol_db.interpret \
        --db tools/symbol-db/symbol-db.sqlite \
        --config tools/symbol-db/interpretation-config.json \
        --module modUtil

If no ``--module`` is given, the runner walks every non-deferred
module in the database (modUtil first per the spec).

The runner imports ``ai_router`` from the repo's ``ai-router/``
directory; the import dance is unchanged from the rest of the repo.
"""

from __future__ import annotations

import argparse
import importlib.util
import os
import sys

from . import db
from . import runner


def _load_ai_router(repo_root: str):
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        os.path.join(repo_root, "ai-router", "__init__.py"),
        submodule_search_locations=[os.path.join(repo_root, "ai-router")],
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load ai_router from {repo_root}")
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--db", required=True, help="Path to symbol-db.sqlite")
    p.add_argument("--config", required=True, help="Path to interpretation-config.json")
    p.add_argument("--module", help="Optional: limit to one module FQN")
    p.add_argument("--session-set", default="docs/session-sets/vba-symbol-db")
    p.add_argument("--session-number", type=int, default=4)
    p.add_argument("--orchestrator", default="claude-code")
    p.add_argument("--repo-root", default=".")
    p.add_argument("--include-macros", action="store_true",
                   help="(default unless --no-macros) Run the macro interpretation pass")
    p.add_argument("--no-macros", action="store_true",
                   help="Skip the macro interpretation pass")
    p.add_argument("--include-lookup-tables", action="store_true",
                   help="(default unless --no-lookup-tables) Run the lookup-table pass")
    p.add_argument("--no-lookup-tables", action="store_true",
                   help="Skip the lookup-table interpretation pass")
    p.add_argument("--include-queries", action="store_true",
                   help="(default unless --no-queries) Run the query interpretation pass")
    p.add_argument("--no-queries", action="store_true",
                   help="Skip the query interpretation pass")
    p.add_argument("--lookup-min-score", type=int, default=70)
    p.add_argument("--limit", type=int, default=0,
                   help="Stop after this many symbols (0 = no cap)")
    args = p.parse_args(argv)

    ai_router = _load_ai_router(args.repo_root)
    route = ai_router.route

    config = runner.load_config(args.config)
    con = db.open_db(args.db)

    # Section toggles: each pass runs by default unless explicitly suppressed.
    do_macros = not args.no_macros
    do_tables = not args.no_lookup_tables
    do_queries = not args.no_queries

    build_run_id = db.open_build_run(
        con,
        orchestrator=args.orchestrator,
        session_set=args.session_set,
        session_number=args.session_number,
        notes=f"interpret module={args.module or '*'} limit={args.limit}",
    )
    print(f"opened build run #{build_run_id} on {args.db}")

    total_cost = 0.0
    total_interpreted = 0
    aggregated = runner.RunStats()

    try:
        # 1. Emit deferred-module skip rows so audit history is complete
        #    before any routed call goes out.
        deferred_count = runner.emit_deferred_skips(con, build_run_id=build_run_id)
        if deferred_count:
            print(f"emitted {deferred_count} skipped-deferred history rows")

        # 2. Modules in spec order: modUtil (and other Modules) first,
        #    then Forms / Reports / ClassModules. The
        #    `Modules.kind == 'Module'` rows pre-sort so callees land first.
        modules = db.list_modules(con)
        if args.module:
            modules = [m for m in modules if m["fqn"] == args.module]
        modules.sort(key=lambda m: (m["kind"] != "Module", m["fqn"]))

        for mod in modules:
            if mod.get("is_deferred"):
                continue
            if args.limit and aggregated.interpreted >= args.limit:
                print(f"hit --limit {args.limit}; stopping module loop")
                break
            print(f"\n=== module {mod['fqn']} ({mod['kind']}) ===")
            stats = runner.interpret_module(
                con,
                mod["fqn"],
                route=route,
                config=config,
                build_run_id=build_run_id,
                session_set=args.session_set,
                session_number=args.session_number,
                stop_when_total_reaches=(
                    args.limit if args.limit else None
                ),
                running_total=aggregated.interpreted,
            )
            _merge_stats(aggregated, stats)
            total_cost += stats.cost_usd
            total_interpreted += stats.interpreted
            print(_format_stats(mod['fqn'], stats))

        if do_macros:
            print("\n=== macros ===")
            macro_stats = runner.interpret_macros(
                con,
                route=route,
                config=config,
                build_run_id=build_run_id,
                session_set=args.session_set,
                session_number=args.session_number,
            )
            _merge_stats(aggregated, macro_stats)
            total_cost += macro_stats.cost_usd
            total_interpreted += macro_stats.interpreted
            print(f"  interpreted={macro_stats.interpreted}  cost=${macro_stats.cost_usd:.4f}")

        if do_tables:
            print("\n=== lookup tables ===")
            table_stats = runner.interpret_lookup_tables(
                con,
                route=route,
                config=config,
                build_run_id=build_run_id,
                session_set=args.session_set,
                session_number=args.session_number,
                min_score=args.lookup_min_score,
            )
            _merge_stats(aggregated, table_stats)
            total_cost += table_stats.cost_usd
            total_interpreted += table_stats.interpreted
            print(f"  interpreted={table_stats.interpreted}  cost=${table_stats.cost_usd:.4f}")

        if do_queries:
            print("\n=== queries ===")
            q_stats = runner.interpret_queries(
                con,
                route=route,
                config=config,
                build_run_id=build_run_id,
                session_set=args.session_set,
                session_number=args.session_number,
            )
            _merge_stats(aggregated, q_stats)
            total_cost += q_stats.cost_usd
            total_interpreted += q_stats.interpreted
            print(
                f"  interpreted={q_stats.interpreted}  "
                f"twin-copies={q_stats.md5_copied}  cost=${q_stats.cost_usd:.4f}"
            )
    finally:
        # Close the BuildRun even if a non-transient exception bubbled up
        # — the partial work is still durable because the per-symbol
        # writers commit per row.
        db.close_build_run(
            con,
            build_run_id,
            interpretations_written=total_interpreted,
            total_routed_cost_usd=total_cost,
        )
        print(f"\nclosed build run #{build_run_id}: "
              f"interpretations={total_interpreted}  cost=${total_cost:.4f}")
    return 0


def _format_stats(fqn: str, s: runner.RunStats) -> str:
    return (
        f"  module {fqn}:\n"
        f"    interpreted     = {s.interpreted}\n"
        f"    opinion2 fired  = {s.opinion2}\n"
        f"    opinion3 fired  = {s.opinion3}\n"
        f"    md5 copied      = {s.md5_copied}\n"
        f"    skipped (dead)  = {s.skipped_dead}\n"
        f"    skipped (proc-local) = {s.skipped_procedure_local}\n"
        f"    skipped (kind)  = {s.skipped_unsupported_kind}\n"
        f"    parse failures  = {s.parse_failures}\n"
        f"    cost            = ${s.cost_usd:.4f}"
    )


def _merge_stats(into: runner.RunStats, frm: runner.RunStats) -> None:
    into.interpreted += frm.interpreted
    into.opinion2 += frm.opinion2
    into.opinion3 += frm.opinion3
    into.md5_copied += frm.md5_copied
    into.skipped_dead += frm.skipped_dead
    into.skipped_procedure_local += frm.skipped_procedure_local
    into.skipped_unsupported_kind += frm.skipped_unsupported_kind
    into.parse_failures += frm.parse_failures
    into.cost_usd += frm.cost_usd
    into.per_symbol.extend(frm.per_symbol)


if __name__ == "__main__":
    sys.exit(main())
