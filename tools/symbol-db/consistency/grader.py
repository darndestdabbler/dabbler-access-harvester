"""Cross-provider grading runner for the consistency calibration pass.

For each sampled item, this module builds a single grading prompt that
shows three independent interpretations side by side, anonymized as A/B/C
to remove provider-name bias, and asks a judge model to score each of the
three pairs (1<->2, 2<->3, 1<->3) on two dimensions:

    interpretation_agreement_pct  (0-100)
    justification_agreement_pct   (0-100)

plus a one-line summary and a disagreement_type drawn from
{factual, scope, framing, convergent}.

Each item is graded by three judges (gemini-pro, opus, gpt-5-4), so the
total call volume is 42 items x 3 judges = 126 calls. Results land in
``tools/symbol-db/consistency/grades.sqlite``, kept separate from
``symbol-db.sqlite`` so the Session-1 schema stays stable.

Usage (from ``tools/symbol-db/``)::

    ../../.venv/Scripts/python.exe -m consistency.grader \\
        --symbol-db symbol-db.sqlite \\
        --grades-db consistency/grades.sqlite \\
        --sample consistency/sample.json \\
        --judge gemini-pro

The runner is idempotent: an (item, judge) row that already has all three
pair scores in the Grades table is skipped on re-run. Transient provider
errors and parse failures are recorded separately so the audit trail
distinguishes "we tried and failed" from "we never tried".
"""

from __future__ import annotations

import argparse
import datetime as _dt
import hashlib
import importlib.util
import json
import random
import re
import sqlite3
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Optional

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


# ── local imports ─────────────────────────────────────────────────────────
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from consistency.sampler import load_sample  # noqa: E402

_SESSION_SET = "vba-symbol-db"
_SESSION_NUMBER = 6

JUDGES = ("gemini-pro", "opus", "gpt-5-4")
PAIRS = ("1-2", "2-3", "1-3")
DISAGREEMENT_TYPES = ("factual", "scope", "framing", "convergent")

# Map (judge_provider) -> agent string used in symbol-db's AgentN columns
# (so we can identify which opinion came from which provider when reading
# back; not required for grading itself).
_AGENT_BY_OPINION = {
    1: "gemini-pro",
    2: "claude-opus-4-7",
    3: "gpt-5-4-2025",
}


# ── grades.sqlite schema ──────────────────────────────────────────────────


_GRADES_SCHEMA = """
CREATE TABLE IF NOT EXISTS Grades (
    item_fqn                       TEXT NOT NULL,
    item_table                     TEXT NOT NULL,
    item_stratum                   TEXT NOT NULL,
    judge_provider                 TEXT NOT NULL,
    pair                           TEXT NOT NULL,
    interpretation_agreement_pct   INTEGER NOT NULL,
    justification_agreement_pct    INTEGER NOT NULL,
    disagreement_type              TEXT NOT NULL,
    summary                        TEXT NOT NULL,
    created_at                     TEXT NOT NULL,
    PRIMARY KEY (item_fqn, judge_provider, pair)
);

CREATE INDEX IF NOT EXISTS idx_grades_judge_stratum
    ON Grades (judge_provider, item_stratum);

CREATE TABLE IF NOT EXISTS GradeRuns (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    judge_provider    TEXT NOT NULL,
    started_at        TEXT NOT NULL,
    completed_at      TEXT,
    items_graded      INTEGER NOT NULL DEFAULT 0,
    items_skipped     INTEGER NOT NULL DEFAULT 0,
    parse_failures    INTEGER NOT NULL DEFAULT 0,
    provider_errors   INTEGER NOT NULL DEFAULT 0,
    total_cost_usd    REAL NOT NULL DEFAULT 0.0,
    notes             TEXT
);

CREATE TABLE IF NOT EXISTS GradeRawResponses (
    item_fqn                TEXT NOT NULL,
    judge_provider          TEXT NOT NULL,
    raw_response            TEXT NOT NULL,
    anonymization_seed      INTEGER NOT NULL,
    letter_to_opinion_json  TEXT NOT NULL,
    cost_usd                REAL,
    model_id                TEXT,
    created_at              TEXT NOT NULL,
    PRIMARY KEY (item_fqn, judge_provider)
);

CREATE TABLE IF NOT EXISTS GradeFailures (
    item_fqn        TEXT NOT NULL,
    judge_provider  TEXT NOT NULL,
    reason          TEXT NOT NULL,
    detail          TEXT,
    raw_response    TEXT,
    created_at      TEXT NOT NULL,
    PRIMARY KEY (item_fqn, judge_provider, reason)
);
"""


def open_grades_db(path: str) -> sqlite3.Connection:
    con = sqlite3.connect(path)
    con.row_factory = sqlite3.Row
    con.executescript(_GRADES_SCHEMA)
    con.execute("PRAGMA foreign_keys = ON")
    return con


# ── triplet loader ────────────────────────────────────────────────────────


@dataclass(frozen=True)
class InterpretationTriplet:
    fqn: str
    table: str
    stratum: str
    kind: str  # for Symbols, the kind column; for others, the table name
    interpretation_1: str
    justification_1: str
    confidence_1: float
    interpretation_2: str
    justification_2: str
    confidence_2: float
    interpretation_3: str
    justification_3: str
    confidence_3: float


def _kind_for(con: sqlite3.Connection, fqn: str, table: str) -> str:
    if table == "Symbols":
        row = con.execute(
            "SELECT kind FROM Symbols WHERE fqn = ?", (fqn,)
        ).fetchone()
        return row[0] if row else "symbol"
    return {"Macros": "macro", "Tables": "table", "Queries": "query"}[table]


def load_triplet(symbol_con: sqlite3.Connection, item: dict) -> InterpretationTriplet:
    """Read all three interpretations for *item* from symbol-db.sqlite."""
    fqn = item["fqn"]
    table = item["table"]
    stratum = item["stratum"]
    sql = f"""
        SELECT Interpretation1, Justification1, Confidence1,
               Interpretation2, Justification2, Confidence2,
               Interpretation3, Justification3, Confidence3
        FROM {table} WHERE fqn = ?
    """
    row = symbol_con.execute(sql, (fqn,)).fetchone()
    if row is None:
        raise ValueError(f"{table} row not found: {fqn!r}")
    missing = [
        f"Interpretation{i}" for i in (1, 2, 3) if row[f"Interpretation{i}"] is None
    ]
    if missing:
        raise ValueError(f"{fqn} missing {missing} in {table}")
    return InterpretationTriplet(
        fqn=fqn,
        table=table,
        stratum=stratum,
        kind=_kind_for(symbol_con, fqn, table),
        interpretation_1=row["Interpretation1"],
        justification_1=row["Justification1"] or "",
        confidence_1=float(row["Confidence1"] or 0.0),
        interpretation_2=row["Interpretation2"],
        justification_2=row["Justification2"] or "",
        confidence_2=float(row["Confidence2"] or 0.0),
        interpretation_3=row["Interpretation3"],
        justification_3=row["Justification3"] or "",
        confidence_3=float(row["Confidence3"] or 0.0),
    )


# ── anonymization ─────────────────────────────────────────────────────────


def make_anonymization_seed(fqn: str, judge: str) -> int:
    """Deterministic per-(item, judge) seed so re-runs reproduce the mapping."""
    h = hashlib.sha256(f"{fqn}|{judge}".encode("utf-8")).digest()
    return int.from_bytes(h[:4], "big")


def anonymize(triplet: InterpretationTriplet, seed: int) -> tuple[dict[str, int], dict[str, dict]]:
    """Return ``(letter_to_opinion, letter_to_payload)``.

    ``letter_to_opinion`` maps 'A'/'B'/'C' to opinion slot 1/2/3 in original
    space. ``letter_to_payload`` maps each letter to its
    {interpretation, justification} dict.
    """
    rng = random.Random(seed)
    opinions = [1, 2, 3]
    rng.shuffle(opinions)
    letter_to_opinion = {"A": opinions[0], "B": opinions[1], "C": opinions[2]}
    letter_to_payload: dict[str, dict] = {}
    for letter, op in letter_to_opinion.items():
        letter_to_payload[letter] = {
            "interpretation": getattr(triplet, f"interpretation_{op}"),
            "justification": getattr(triplet, f"justification_{op}"),
        }
    return letter_to_opinion, letter_to_payload


def canonical_pair(opinion_a: int, opinion_b: int) -> str:
    a, b = sorted((opinion_a, opinion_b))
    return f"{a}-{b}"


# ── prompt builder ────────────────────────────────────────────────────────


_PROMPT_TEMPLATE = """\
You are an impartial reviewer evaluating cross-provider consistency on an
AI-interpretation task.  Three independent AI providers were each given the
same VBA artifact and asked to write a one-paragraph interpretation plus a
short justification citing source-level evidence.  The three responses are
shown below in randomized order, anonymized as A / B / C.  You will not be
told which provider wrote which response; rate the responses on their
content alone.

# Target artifact
- FQN: {fqn}
- Table: {table}
- Kind: {kind}
- Stratum: {stratum}

# Three independent interpretations (anonymized)

## Interpretation A
{interp_a}

### Justification A
{justif_a}

---

## Interpretation B
{interp_b}

### Justification B
{justif_b}

---

## Interpretation C
{interp_c}

### Justification C
{justif_c}

---

# Your task

For each of the three pairs (AB, BC, AC), produce two ratings on a 0-100
percent-agreement scale:

1. **interpretation_agreement_pct** - how much of what each interpretation
   *says about behavior* (preconditions, side effects, control flow,
   return shape, purpose) is the same across the pair.  100 means the two
   interpretations agree on the artifact's behavior; 0 means they disagree.
2. **justification_agreement_pct** - how much of the *evidence cited*
   (source spans, callees, callers, signature features, naming patterns)
   is the same across the pair.  100 means the two justifications point at
   the same evidence; 0 means they cite disjoint evidence.

Also produce, for each pair:

- **summary**: one line describing the most salient disagreement (or
  "convergent on all behavioral points" if they substantially agree).
- **disagreement_type**: one of:
  - "factual"     - the two interpretations make claims that cannot both be true.
  - "scope"       - one is materially broader/narrower than the other.
  - "framing"     - same facts, different emphasis or vocabulary.
  - "convergent"  - the two substantially agree.

Be honest: if the pair is genuinely identical, score both at 100; if they
diverge, score them lower.  Do not split-the-difference toward 50.

# Required output

Output a brief prose paragraph for each pair (2-3 sentences each), then a
single JSON block in this exact format:

```json
{{
  "AB": {{
    "interpretation_agreement_pct": <0-100 integer>,
    "justification_agreement_pct":  <0-100 integer>,
    "disagreement_type":            "factual" | "scope" | "framing" | "convergent",
    "summary":                      "<one line>"
  }},
  "BC": {{ ... same shape ... }},
  "AC": {{ ... same shape ... }}
}}
```

The JSON block is required.  Do not omit it and do not include any keys
beyond those shown.
"""


def build_prompt(triplet: InterpretationTriplet, letter_to_payload: dict[str, dict]) -> str:
    return _PROMPT_TEMPLATE.format(
        fqn=triplet.fqn,
        table=triplet.table,
        kind=triplet.kind,
        stratum=triplet.stratum,
        interp_a=letter_to_payload["A"]["interpretation"],
        justif_a=letter_to_payload["A"]["justification"],
        interp_b=letter_to_payload["B"]["interpretation"],
        justif_b=letter_to_payload["B"]["justification"],
        interp_c=letter_to_payload["C"]["interpretation"],
        justif_c=letter_to_payload["C"]["justification"],
    )


# ── response parsing ──────────────────────────────────────────────────────


_FENCE_RE = re.compile(r"```(?:json)?\s*(\{.*?\})\s*```", re.DOTALL | re.IGNORECASE)


def _extract_json_block(text: str) -> Optional[dict]:
    if not text:
        return None
    m = _FENCE_RE.search(text)
    candidates: list[str] = []
    if m:
        candidates.append(m.group(1))
    # Fallback: walk back from each ``}`` to its matching ``{``.
    last_close = text.rfind("}")
    while last_close != -1:
        depth = 0
        in_str = False
        escape = False
        start = -1
        for i in range(last_close, -1, -1):
            ch = text[i]
            if escape:
                escape = False
                continue
            if ch == "\\":
                escape = True
                continue
            if ch == '"':
                in_str = not in_str
                continue
            if in_str:
                continue
            if ch == "}":
                depth += 1
            elif ch == "{":
                depth -= 1
                if depth == 0:
                    start = i
                    break
        if start != -1:
            candidates.append(text[start : last_close + 1])
        last_close = text.rfind("}", 0, last_close)

    for cand in candidates:
        try:
            obj = json.loads(cand)
        except json.JSONDecodeError:
            continue
        if isinstance(obj, dict) and {"AB", "BC", "AC"}.issubset(obj.keys()):
            return obj
    return None


@dataclass(frozen=True)
class PairScore:
    interpretation_pct: int
    justification_pct: int
    disagreement_type: str
    summary: str


@dataclass(frozen=True)
class GradeResponse:
    pair_AB: PairScore
    pair_BC: PairScore
    pair_AC: PairScore


def _coerce_pct(value) -> Optional[int]:
    try:
        v = int(round(float(value)))
    except (TypeError, ValueError):
        return None
    if v < 0 or v > 100:
        return None
    return v


def _coerce_disagreement(value) -> Optional[str]:
    if not isinstance(value, str):
        return None
    v = value.strip().lower()
    return v if v in DISAGREEMENT_TYPES else None


def parse_response(text: str) -> Optional[GradeResponse]:
    """Parse a judge's response into structured per-pair scores."""
    obj = _extract_json_block(text)
    if obj is None:
        return None
    pairs: dict[str, PairScore] = {}
    for key in ("AB", "BC", "AC"):
        pair = obj.get(key)
        if not isinstance(pair, dict):
            return None
        ip = _coerce_pct(pair.get("interpretation_agreement_pct"))
        jp = _coerce_pct(pair.get("justification_agreement_pct"))
        dt = _coerce_disagreement(pair.get("disagreement_type"))
        summary = pair.get("summary")
        if ip is None or jp is None or dt is None or not isinstance(summary, str):
            return None
        pairs[key] = PairScore(
            interpretation_pct=ip,
            justification_pct=jp,
            disagreement_type=dt,
            summary=summary.strip()[:500],
        )
    return GradeResponse(pair_AB=pairs["AB"], pair_BC=pairs["BC"], pair_AC=pairs["AC"])


# ── pair-letter -> canonical pair mapping ─────────────────────────────────


def map_letter_pair_to_canonical(
    letter_pair: str, letter_to_opinion: dict[str, int]
) -> str:
    a, b = letter_pair[0], letter_pair[1]
    return canonical_pair(letter_to_opinion[a], letter_to_opinion[b])


# ── persistence ───────────────────────────────────────────────────────────


def _now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds")


def already_graded(grades_con: sqlite3.Connection, fqn: str, judge: str) -> bool:
    """True if all three pair rows already exist for this (item, judge)."""
    row = grades_con.execute(
        "SELECT COUNT(*) FROM Grades WHERE item_fqn=? AND judge_provider=?",
        (fqn, judge),
    ).fetchone()
    return (row[0] if row else 0) >= 3


def write_grades(
    grades_con: sqlite3.Connection,
    item: dict,
    judge: str,
    letter_to_opinion: dict[str, int],
    response: GradeResponse,
) -> None:
    rows = []
    now = _now()
    for letter_pair, score in (
        ("AB", response.pair_AB),
        ("BC", response.pair_BC),
        ("AC", response.pair_AC),
    ):
        canonical = map_letter_pair_to_canonical(letter_pair, letter_to_opinion)
        rows.append((
            item["fqn"], item["table"], item["stratum"], judge, canonical,
            score.interpretation_pct, score.justification_pct,
            score.disagreement_type, score.summary, now,
        ))
    grades_con.executemany(
        """
        INSERT OR REPLACE INTO Grades
            (item_fqn, item_table, item_stratum, judge_provider, pair,
             interpretation_agreement_pct, justification_agreement_pct,
             disagreement_type, summary, created_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        rows,
    )


def write_raw_response(
    grades_con: sqlite3.Connection,
    item: dict,
    judge: str,
    raw_text: str,
    seed: int,
    letter_to_opinion: dict[str, int],
    cost_usd: Optional[float],
    model_id: Optional[str],
) -> None:
    grades_con.execute(
        """
        INSERT OR REPLACE INTO GradeRawResponses
            (item_fqn, judge_provider, raw_response, anonymization_seed,
             letter_to_opinion_json, cost_usd, model_id, created_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            item["fqn"], judge, raw_text, seed,
            json.dumps(letter_to_opinion, sort_keys=True),
            cost_usd, model_id, _now(),
        ),
    )


def write_failure(
    grades_con: sqlite3.Connection,
    item: dict,
    judge: str,
    reason: str,
    detail: str = "",
    raw: str = "",
) -> None:
    grades_con.execute(
        """
        INSERT OR REPLACE INTO GradeFailures
            (item_fqn, judge_provider, reason, detail, raw_response, created_at)
        VALUES (?, ?, ?, ?, ?, ?)
        """,
        (item["fqn"], judge, reason, detail[:1000], raw[:8000], _now()),
    )


def open_run(grades_con: sqlite3.Connection, judge: str, notes: str = "") -> int:
    cur = grades_con.execute(
        "INSERT INTO GradeRuns (judge_provider, started_at, notes) VALUES (?, ?, ?)",
        (judge, _now(), notes),
    )
    return int(cur.lastrowid)


def close_run(
    grades_con: sqlite3.Connection,
    run_id: int,
    *,
    items_graded: int,
    items_skipped: int,
    parse_failures: int,
    provider_errors: int,
    total_cost_usd: float,
) -> None:
    grades_con.execute(
        """
        UPDATE GradeRuns SET completed_at=?, items_graded=?, items_skipped=?,
                             parse_failures=?, provider_errors=?, total_cost_usd=?
        WHERE id=?
        """,
        (_now(), items_graded, items_skipped, parse_failures,
         provider_errors, total_cost_usd, run_id),
    )


# ── runner ────────────────────────────────────────────────────────────────


def _is_transient(exc: Exception) -> bool:
    msg = str(exc).lower()
    return any(x in msg for x in ("503", "429", "timeout", "rate limit", "overloaded"))


def grade_one(
    grades_con: sqlite3.Connection,
    symbol_con: sqlite3.Connection,
    item: dict,
    judge: str,
    query_fn: Callable,
) -> dict:
    """Grade a single (item, judge). Returns a status dict."""
    fqn = item["fqn"]
    if already_graded(grades_con, fqn, judge):
        return {"status": "already_graded", "fqn": fqn, "judge": judge, "cost_usd": 0.0}

    try:
        triplet = load_triplet(symbol_con, item)
    except Exception as exc:
        write_failure(grades_con, item, judge, "triplet_missing", detail=str(exc))
        return {"status": "triplet_missing", "fqn": fqn, "judge": judge,
                "error": str(exc), "cost_usd": 0.0}

    seed = make_anonymization_seed(fqn, judge)
    letter_to_opinion, letter_to_payload = anonymize(triplet, seed)
    prompt = build_prompt(triplet, letter_to_payload)

    cost = 0.0
    raw_text = ""
    model_id = None
    try:
        result = query_fn(
            judge, prompt, task_type="analysis",
            session_set=_SESSION_SET, session_number=_SESSION_NUMBER,
        )
    except Exception as exc:
        write_failure(grades_con, item, judge, "provider_error", detail=str(exc))
        if _is_transient(exc):
            return {"status": "provider_error", "fqn": fqn, "judge": judge,
                    "error": str(exc), "cost_usd": 0.0}
        raise

    cost = float(getattr(result, "cost_usd", 0.0) or 0.0)
    raw_text = result.content
    model_id = getattr(result, "model_id", None) or getattr(result, "model_name", None)

    parsed = parse_response(raw_text)
    if parsed is None:
        # One retry with an explicit JSON-format reminder appended.
        reminder = (
            raw_text
            + "\n\nThe required JSON block was missing or malformed. Output it now exactly as:\n"
            "```json\n"
            '{"AB": {"interpretation_agreement_pct": <0-100>, "justification_agreement_pct": <0-100>, '
            '"disagreement_type": "factual|scope|framing|convergent", "summary": "<one line>"},\n'
            '"BC": {...}, "AC": {...}}\n```'
        )
        try:
            result2 = query_fn(
                judge, reminder, task_type="analysis",
                session_set=_SESSION_SET, session_number=_SESSION_NUMBER,
            )
            cost += float(getattr(result2, "cost_usd", 0.0) or 0.0)
            raw_text = (raw_text or "") + "\n\n---RETRY---\n\n" + (result2.content or "")
            parsed = parse_response(result2.content)
        except Exception as exc2:
            # Always persist the failure before deciding whether to retry or
            # re-raise, so the audit trail captures the second-call error
            # even on the non-transient path.
            write_failure(grades_con, item, judge, "provider_error",
                          detail=str(exc2), raw=raw_text)
            if _is_transient(exc2):
                return {"status": "provider_error", "fqn": fqn, "judge": judge,
                        "error": str(exc2), "cost_usd": cost}
            raise

    if parsed is None:
        write_failure(grades_con, item, judge, "parse_failure", raw=raw_text)
        write_raw_response(grades_con, item, judge, raw_text, seed,
                           letter_to_opinion, cost, model_id)
        return {"status": "parse_failure", "fqn": fqn, "judge": judge,
                "cost_usd": cost}

    write_grades(grades_con, item, judge, letter_to_opinion, parsed)
    write_raw_response(grades_con, item, judge, raw_text, seed,
                       letter_to_opinion, cost, model_id)
    return {"status": "graded", "fqn": fqn, "judge": judge, "cost_usd": cost}


def run_judge(
    grades_db_path: str,
    symbol_db_path: str,
    sample_path: str,
    judge: str,
    *,
    dry_run: bool = False,
    max_workers: int = 1,
) -> dict:
    """Run one judging pass over the 42-item sample.

    *max_workers* controls API-call concurrency. The grader ran sequentially
    historically (max_workers=1); raising this to 4-6 cuts wall time for
    Gemini-Pro's ~30s per-call latency without changing semantics, because
    each (item, judge) is independent. SQLite writes funnel back through the
    main thread so we never contend on the connection.
    """
    if judge not in JUDGES:
        raise ValueError(f"judge must be one of {JUDGES}, got {judge!r}")

    ar = _load_ai_router()
    query_fn = ar.query

    items = load_sample(sample_path)
    grades_con = open_grades_db(grades_db_path)
    symbol_con = sqlite3.connect(symbol_db_path)
    symbol_con.row_factory = sqlite3.Row

    run_id = open_run(grades_con, judge,
                      notes=f"Session {_SESSION_NUMBER} cross-provider grading: {judge}")
    grades_con.commit()

    stats = {"judge": judge, "graded": 0, "already_graded": 0,
             "parse_failures": 0, "provider_errors": 0, "triplet_missing": 0,
             "total_cost_usd": 0.0, "per_item": []}

    def _record(i: int, item: dict, res: dict) -> None:
        fqn = item["fqn"]
        stats["per_item"].append(res)
        stats["total_cost_usd"] += res.get("cost_usd", 0.0)
        status = res["status"]
        if status == "graded":
            stats["graded"] += 1
            print(f"  [{i:>3}/{len(items)}] {fqn[:60]:<60} {judge:<10} "
                  f"cost=${res['cost_usd']:.4f}", flush=True)
        elif status == "already_graded":
            stats["already_graded"] += 1
        elif status == "parse_failure":
            stats["parse_failures"] += 1
            print(f"  [{i:>3}/{len(items)}] {fqn[:60]:<60} {judge:<10} "
                  f"PARSE FAILURE", flush=True)
        elif status == "provider_error":
            stats["provider_errors"] += 1
            print(f"  [{i:>3}/{len(items)}] {fqn[:60]:<60} {judge:<10} "
                  f"PROVIDER ERROR: {res.get('error','')[:60]}", flush=True)
        elif status == "triplet_missing":
            stats["triplet_missing"] += 1

    try:
        if dry_run:
            for i, item in enumerate(items, 1):
                stats["per_item"].append({"fqn": item["fqn"], "status": "dry_run"})

        elif max_workers <= 1:
            for i, item in enumerate(items, 1):
                res = grade_one(grades_con, symbol_con, item, judge, query_fn)
                grades_con.commit()
                _record(i, item, res)

        else:
            # Two-phase: (1) preload all triplets + prompts on the main
            # thread (sqlite3 connections are not thread-safe by default),
            # (2) thread-pool the API calls; SQLite writes funnel back
            # through the main thread to avoid contention.
            from concurrent.futures import ThreadPoolExecutor, as_completed

            todo: list[tuple[int, dict, int, dict, str]] = []
            for i, item in enumerate(items, 1):
                if already_graded(grades_con, item["fqn"], judge):
                    res = {"status": "already_graded", "fqn": item["fqn"],
                           "judge": judge, "cost_usd": 0.0}
                    _record(i, item, res)
                    continue
                try:
                    triplet = load_triplet(symbol_con, item)
                except Exception as exc:
                    write_failure(grades_con, item, judge, "triplet_missing",
                                  detail=str(exc))
                    grades_con.commit()
                    res = {"status": "triplet_missing", "fqn": item["fqn"],
                           "judge": judge, "error": str(exc), "cost_usd": 0.0}
                    _record(i, item, res)
                    continue

                seed = make_anonymization_seed(item["fqn"], judge)
                letter_to_opinion, letter_to_payload = anonymize(triplet, seed)
                prompt = build_prompt(triplet, letter_to_payload)
                todo.append((i, item, seed, letter_to_opinion, prompt))

            def _call(payload):
                idx, item, seed, mapping, prompt = payload
                cost = 0.0
                try:
                    result = query_fn(judge, prompt, task_type="analysis",
                                      session_set=_SESSION_SET, session_number=_SESSION_NUMBER)
                except Exception as exc:
                    return ("provider_error", idx, item, seed, mapping,
                            None, str(exc), cost, "")
                cost = float(getattr(result, "cost_usd", 0.0) or 0.0)
                raw = result.content or ""
                model_id = (getattr(result, "model_id", None)
                            or getattr(result, "model_name", None))
                parsed = parse_response(raw)
                if parsed is None:
                    reminder = (
                        raw +
                        "\n\nThe required JSON block was missing or malformed. Output it now exactly as:\n"
                        "```json\n"
                        '{"AB": {"interpretation_agreement_pct": <0-100>, "justification_agreement_pct": <0-100>, '
                        '"disagreement_type": "factual|scope|framing|convergent", "summary": "<one line>"},\n'
                        '"BC": {...}, "AC": {...}}\n```'
                    )
                    try:
                        result2 = query_fn(judge, reminder, task_type="analysis",
                                           session_set=_SESSION_SET, session_number=_SESSION_NUMBER)
                        cost += float(getattr(result2, "cost_usd", 0.0) or 0.0)
                        raw = raw + "\n\n---RETRY---\n\n" + (result2.content or "")
                        parsed = parse_response(result2.content)
                    except Exception as exc2:
                        return ("provider_error", idx, item, seed, mapping,
                                None, str(exc2), cost, raw)
                return ("ok", idx, item, seed, mapping, parsed,
                        None, cost, raw, model_id)

            with ThreadPoolExecutor(max_workers=max_workers) as ex:
                futures = [ex.submit(_call, p) for p in todo]
                for fut in as_completed(futures):
                    out = fut.result()
                    kind = out[0]
                    if kind == "ok":
                        _, idx, item, seed, mapping, parsed, _err, cost, raw = out[:9]
                        model_id = out[9]
                        if parsed is None:
                            write_failure(grades_con, item, judge, "parse_failure", raw=raw)
                            write_raw_response(grades_con, item, judge, raw, seed,
                                               mapping, cost, model_id)
                            res = {"status": "parse_failure", "fqn": item["fqn"],
                                   "judge": judge, "cost_usd": cost}
                        else:
                            write_grades(grades_con, item, judge, mapping, parsed)
                            write_raw_response(grades_con, item, judge, raw, seed,
                                               mapping, cost, model_id)
                            res = {"status": "graded", "fqn": item["fqn"],
                                   "judge": judge, "cost_usd": cost}
                    elif kind == "provider_error":
                        _, idx, item, seed, mapping, _parsed, err, cost, raw = out[:9]
                        write_failure(grades_con, item, judge, "provider_error",
                                      detail=err, raw=raw)
                        if not _is_transient(Exception(err)):
                            grades_con.commit()
                            raise RuntimeError(f"non-transient provider error: {err}")
                        res = {"status": "provider_error", "fqn": item["fqn"],
                               "judge": judge, "error": err, "cost_usd": cost}
                    else:
                        raise RuntimeError(f"unexpected return kind: {kind}")
                    grades_con.commit()
                    _record(out[1], out[2], res)

    finally:
        close_run(
            grades_con, run_id,
            items_graded=stats["graded"],
            items_skipped=stats["already_graded"],
            parse_failures=stats["parse_failures"],
            provider_errors=stats["provider_errors"],
            total_cost_usd=stats["total_cost_usd"],
        )
        grades_con.commit()
        grades_con.close()
        symbol_con.close()

    return stats


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run cross-provider grading for one judge.")
    parser.add_argument("--symbol-db", default="symbol-db.sqlite",
                        help="Path to symbol-db.sqlite (interpretations source).")
    parser.add_argument("--grades-db", default="consistency/grades.sqlite",
                        help="Path to grades.sqlite (output).")
    parser.add_argument("--sample", default="consistency/sample.json",
                        help="Path to sample.json.")
    parser.add_argument("--judge", choices=list(JUDGES), required=True,
                        help="Which judge provider to run.")
    parser.add_argument("--dry-run", action="store_true",
                        help="Build prompts but do not route.")
    parser.add_argument("--max-workers", type=int, default=4,
                        help="API-call concurrency (default 4). Set 1 to "
                             "force sequential execution.")
    args = parser.parse_args(argv)

    print(f"Running judge {args.judge!r} with max_workers={args.max_workers}...",
          flush=True)
    stats = run_judge(args.grades_db, args.symbol_db, args.sample, args.judge,
                      dry_run=args.dry_run, max_workers=args.max_workers)
    print(
        f"\nDone. judge={args.judge}  graded={stats['graded']}  "
        f"already_graded={stats['already_graded']}  "
        f"parse_failures={stats['parse_failures']}  "
        f"provider_errors={stats['provider_errors']}  "
        f"triplet_missing={stats['triplet_missing']}  "
        f"cost=${stats['total_cost_usd']:.4f}",
        flush=True,
    )
    return stats


if __name__ == "__main__":
    main()
