"""Per-judge intra-correlation reduction.

Spec step 5 (Session 6): for each of the three judging providers separately,
compute the correlation across the 42 items between
``interpretation_agreement_pct`` and ``justification_agreement_pct``
(averaged across the three pairs to one item-level value per dimension).
This produces three correlation coefficients - one per judge.

If **all three are positive** with non-trivial magnitude (the spec leaves
the magnitude threshold qualitative; we use ``r > 0.30`` as the
"non-trivial" cutoff), the two ratings can be aggregated to one
**consistency score** per item (mean of the two percentages) and downstream
analysis runs on the aggregate.

If **the signs disagree** or any judge returns a near-zero correlation, the
two ratings are treated as **distinct dimensions** and downstream analysis
splits into two parallel correlations.

The decision is recorded in the returned :class:`AggregationDecision`; the
caller persists it to ``correlation-report.md``.
"""

from __future__ import annotations

import math
import sqlite3
from dataclasses import dataclass
from typing import Optional


# Threshold below which a per-judge intra-correlation is treated as
# "near-zero" and forces the split path. Any judge with |r| < this triggers
# the split; otherwise the magnitude is enough to count as a real signal.
_NEAR_ZERO_THRESHOLD = 0.30


@dataclass(frozen=True)
class ItemAggregates:
    """One item, viewed through one judge's three pair scores."""
    fqn: str
    stratum: str
    judge_provider: str
    interpretation_pct_mean: float    # mean across the 3 pairs
    justification_pct_mean: float
    pair_count: int                   # should be 3 in healthy runs


@dataclass(frozen=True)
class JudgeCorrelation:
    judge_provider: str
    n_items: int
    pearson_r: float
    note: str  # e.g., "ok", "near-zero", "constant-input"


@dataclass(frozen=True)
class AggregationDecision:
    """Outcome of step 5: do we aggregate or split downstream?"""
    judge_correlations: tuple[JudgeCorrelation, ...]
    aggregate_path: bool
    rationale: str


# ── data access ───────────────────────────────────────────────────────────


def load_item_aggregates(grades_con: sqlite3.Connection) -> list[ItemAggregates]:
    """For each (item, judge) compute the per-pair means."""
    rows = grades_con.execute(
        """
        SELECT item_fqn, item_stratum, judge_provider,
               AVG(interpretation_agreement_pct) AS interp_mean,
               AVG(justification_agreement_pct)  AS justif_mean,
               COUNT(*)                          AS pair_count
        FROM Grades
        GROUP BY item_fqn, item_stratum, judge_provider
        """
    ).fetchall()
    return [
        ItemAggregates(
            fqn=r["item_fqn"],
            stratum=r["item_stratum"],
            judge_provider=r["judge_provider"],
            interpretation_pct_mean=float(r["interp_mean"]),
            justification_pct_mean=float(r["justif_mean"]),
            pair_count=int(r["pair_count"]),
        )
        for r in rows
    ]


# ── statistics ────────────────────────────────────────────────────────────


def pearson_r(xs: list[float], ys: list[float]) -> Optional[float]:
    """Pearson correlation; returns ``None`` if input is degenerate."""
    n = len(xs)
    if n != len(ys) or n < 2:
        return None
    mean_x = sum(xs) / n
    mean_y = sum(ys) / n
    sx = sum((x - mean_x) ** 2 for x in xs)
    sy = sum((y - mean_y) ** 2 for y in ys)
    if sx == 0.0 or sy == 0.0:
        return None
    cov = sum((x - mean_x) * (y - mean_y) for x, y in zip(xs, ys))
    denom = math.sqrt(sx * sy)
    if denom == 0.0:
        return None
    return cov / denom


def spearman_r(xs: list[float], ys: list[float]) -> Optional[float]:
    """Spearman rank correlation. Ties are average-ranked."""
    if len(xs) != len(ys) or len(xs) < 2:
        return None
    return pearson_r(_rank(xs), _rank(ys))


def _rank(values: list[float]) -> list[float]:
    """Average-tied ranks (1-based)."""
    indexed = sorted(enumerate(values), key=lambda iv: iv[1])
    ranks = [0.0] * len(values)
    i = 0
    while i < len(indexed):
        j = i
        while j + 1 < len(indexed) and indexed[j + 1][1] == indexed[i][1]:
            j += 1
        avg_rank = (i + j) / 2 + 1  # 1-based
        for k in range(i, j + 1):
            ranks[indexed[k][0]] = avg_rank
        i = j + 1
    return ranks


# ── per-judge correlation ─────────────────────────────────────────────────


def compute_judge_correlation(
    aggregates: list[ItemAggregates],
    judge: str,
) -> JudgeCorrelation:
    judge_rows = [a for a in aggregates if a.judge_provider == judge]
    xs = [a.interpretation_pct_mean for a in judge_rows]
    ys = [a.justification_pct_mean for a in judge_rows]
    r = pearson_r(xs, ys)
    if r is None:
        return JudgeCorrelation(
            judge_provider=judge, n_items=len(judge_rows),
            pearson_r=float("nan"), note="constant-input-or-too-few",
        )
    note = "near-zero" if abs(r) < _NEAR_ZERO_THRESHOLD else "ok"
    return JudgeCorrelation(
        judge_provider=judge, n_items=len(judge_rows),
        pearson_r=r, note=note,
    )


def decide_aggregation(
    judge_correlations: list[JudgeCorrelation],
    *,
    near_zero_threshold: float = _NEAR_ZERO_THRESHOLD,
) -> AggregationDecision:
    """Apply step 5 decision rule."""
    valid = [jc for jc in judge_correlations
             if not math.isnan(jc.pearson_r)]
    if len(valid) < len(judge_correlations):
        return AggregationDecision(
            judge_correlations=tuple(judge_correlations),
            aggregate_path=False,
            rationale=("At least one judge returned a degenerate correlation "
                       "(constant input or too few items). Falling back to the "
                       "split path so each dimension is reported separately."),
        )

    signs_positive = all(jc.pearson_r > 0 for jc in valid)
    all_above_threshold = all(abs(jc.pearson_r) >= near_zero_threshold for jc in valid)

    if signs_positive and all_above_threshold:
        return AggregationDecision(
            judge_correlations=tuple(judge_correlations),
            aggregate_path=True,
            rationale=(
                f"All three per-judge intra-correlations are positive and "
                f">= {near_zero_threshold:.2f} in magnitude, so "
                f"interpretation_agreement_pct and justification_agreement_pct "
                f"are reduced to a single consistency score (mean of the two)."
            ),
        )

    # Distinguish: any negative coefficient, any exact-zero coefficient,
    # any subthreshold-magnitude coefficient. Each branch produces a
    # rationale that names the actual trigger so downstream readers can
    # interpret the split path correctly.
    has_negative = any(jc.pearson_r < 0 for jc in valid)
    has_exact_zero = any(jc.pearson_r == 0 for jc in valid)
    if has_negative:
        rationale = (
            "Per-judge intra-correlations include at least one negative "
            "value, indicating the two agreement dimensions move in opposite "
            "directions for that judge. Reporting them separately along the "
            "split path."
        )
    elif has_exact_zero:
        rationale = (
            "At least one judge returned an exact-zero correlation between "
            "the two agreement dimensions; that pair is treated as carrying "
            "no shared signal, so the split path is taken to keep the two "
            "dimensions reported independently."
        )
    else:
        rationale = (
            f"At least one judge returned a near-zero correlation (|r| < "
            f"{near_zero_threshold:.2f}). The two agreement dimensions are "
            f"treated as distinct and reported separately."
        )

    return AggregationDecision(
        judge_correlations=tuple(judge_correlations),
        aggregate_path=False,
        rationale=rationale,
    )


def compute_aggregation_decision(
    grades_con: sqlite3.Connection,
    judges: tuple[str, ...] = ("gemini-pro", "opus", "gpt-5-4"),
) -> tuple[AggregationDecision, list[ItemAggregates]]:
    """Compute the per-judge correlations + aggregation decision in one pass."""
    aggregates = load_item_aggregates(grades_con)
    correlations = [compute_judge_correlation(aggregates, j) for j in judges]
    decision = decide_aggregation(correlations)
    return decision, aggregates


# ── per-item consistency views ────────────────────────────────────────────


@dataclass(frozen=True)
class ItemConsistency:
    """Per-item consistency, averaged across the three judges.

    On the aggregate path: ``consistency_score = mean(interpretation_pct,
    justification_pct)`` per (item, judge), then averaged across judges.

    On the split path: ``interpretation_score`` and ``justification_score``
    are returned separately, each averaged across judges.
    """
    fqn: str
    stratum: str
    interpretation_score: float
    justification_score: float
    consistency_score: float  # mean of the two; valid on aggregate path


def per_item_scores(aggregates: list[ItemAggregates]) -> list[ItemConsistency]:
    """Average per-judge scores into per-item scores."""
    by_fqn: dict[str, list[ItemAggregates]] = {}
    for a in aggregates:
        by_fqn.setdefault(a.fqn, []).append(a)
    out: list[ItemConsistency] = []
    for fqn, items in by_fqn.items():
        if not items:
            continue
        stratum = items[0].stratum
        interp = sum(a.interpretation_pct_mean for a in items) / len(items)
        justif = sum(a.justification_pct_mean for a in items) / len(items)
        out.append(ItemConsistency(
            fqn=fqn,
            stratum=stratum,
            interpretation_score=interp,
            justification_score=justif,
            consistency_score=(interp + justif) / 2.0,
        ))
    return out
