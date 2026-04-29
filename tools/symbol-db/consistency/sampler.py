"""Deterministic stratified sampler for the cross-provider calibration pass.

Draws a block-randomized 42-item sample from the 103 Session-4-interpreted
items.  The sample policy:

    Stratum               Population  Sample
    ──────────────────    ──────────  ──────
    enum_decl                     1       1  (all)
    macro                         1       1  (all)
    query  (live)                 3       3  (all)
    function                      6       6  (all)
    module_var (module-level)     7       7  (all)
    sub                          22       8  (random)
    enum_value                   48       8  (random)
    lookup_table  (score >= 70)  15       8  (random)
    ─────────────────────────────────────────────
    Total                       103      42

Each item carries ``{fqn, table, stratum}`` where ``table`` is the SQLite
table the FQN lives in (``Symbols`` / ``Macros`` / ``Tables`` / ``Queries``).

The sample is deterministic given the same *seed*.  Call ``draw_sample`` once
and persist the result to ``sample.json``; ``independent_runner`` reads it on
every subsequent run so opinion-2/3 always cover the same 42 items.
"""

from __future__ import annotations

import json
import random
import sqlite3
from typing import Optional


POLICY: dict[str, dict] = {
    "enum_decl":    {"table": "Symbols",  "max_sample": None},  # take all
    "macro":        {"table": "Macros",   "max_sample": None},
    "query":        {"table": "Queries",  "max_sample": None},
    "function":     {"table": "Symbols",  "max_sample": None},
    "module_var":   {"table": "Symbols",  "max_sample": None},
    "sub":          {"table": "Symbols",  "max_sample": 8},
    "enum_value":   {"table": "Symbols",  "max_sample": 8},
    "lookup_table": {"table": "Tables",   "max_sample": 8},
}


def _fetch_stratum(con: sqlite3.Connection, stratum: str) -> list[str]:
    """Return all interpreted, live FQNs in the given stratum."""
    info = POLICY[stratum]
    table = info["table"]

    if table == "Symbols":
        kind = stratum  # enum_decl / function / module_var / sub / enum_value
        rows = con.execute(
            """
            SELECT fqn FROM Symbols
            WHERE kind = ?
              AND Interpretation1 IS NOT NULL
              AND is_dead = 0
              AND (kind != 'module_var' OR declared_in_fqn IS NULL)
            ORDER BY fqn
            """,
            (kind,),
        ).fetchall()
        return [r[0] for r in rows]

    if table == "Macros":
        rows = con.execute(
            "SELECT fqn FROM Macros WHERE Interpretation1 IS NOT NULL ORDER BY fqn"
        ).fetchall()
        return [r[0] for r in rows]

    if table == "Tables":
        rows = con.execute(
            """
            SELECT fqn FROM Tables
            WHERE Interpretation1 IS NOT NULL
              AND is_lookup_table_score >= 70
            ORDER BY fqn
            """
        ).fetchall()
        return [r[0] for r in rows]

    if table == "Queries":
        rows = con.execute(
            """
            SELECT fqn FROM Queries
            WHERE Interpretation1 IS NOT NULL
              AND is_dead = 0
            ORDER BY fqn
            """
        ).fetchall()
        return [r[0] for r in rows]

    raise ValueError(f"Unknown table mapping for stratum {stratum!r}")


def draw_sample(
    db_path: str,
    seed: int = 42,
) -> list[dict]:
    """Return the 42-item stratified sample as a list of dicts.

    Each dict has keys ``fqn``, ``table``, ``stratum``.
    """
    con = sqlite3.connect(db_path)
    rng = random.Random(seed)

    items: list[dict] = []
    for stratum, info in POLICY.items():
        population = _fetch_stratum(con, stratum)
        max_n = info["max_sample"]
        if max_n is None or len(population) <= max_n:
            selected = population
        else:
            selected = sorted(rng.sample(population, max_n))
        for fqn in selected:
            items.append({"fqn": fqn, "table": info["table"], "stratum": stratum})

    con.close()
    return items


def load_sample(sample_path: str) -> list[dict]:
    with open(sample_path, encoding="utf-8") as f:
        data = json.load(f)
    return data["items"]


def save_sample(
    items: list[dict],
    sample_path: str,
    *,
    seed: int,
    db_path: str,
    policy: Optional[dict] = None,
) -> None:
    """Persist the sample + metadata to *sample_path* (JSON)."""
    payload = {
        "seed": seed,
        "db_path": db_path,
        "policy": policy or {k: v["max_sample"] for k, v in POLICY.items()},
        "total": len(items),
        "items": items,
    }
    with open(sample_path, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2)
        f.write("\n")
