"""Confidence vs. consistency correlation analysis.

Spec step 6 (Session 6): given the per-judge intra-correlation decision
from :mod:`consistency.aggregator`, compute the headline correlation
between self-reported confidence (mean of the three opinion confidences
per item) and consistency (per-item, per-judge mean).

Two paths, governed by the upstream :class:`AggregationDecision`:

* **Aggregated path** - one consistency score per item; report Pearson and
  Spearman correlation against mean opinion-confidence.
* **Split path** - two parallel correlations, one per agreement dimension.

Both paths produce a per-kind breakdown, inter-rater reliability across
the three judges (Krippendorff's alpha on the percent-agreement totals),
and the top-5 false-confidence and top-5 justified-uncertainty items.

This module is a pure-data layer; the report writer in
:mod:`consistency.report` (or the orchestrator's report-writing step)
owns the prose framing and the matplotlib chart.
"""

from __future__ import annotations

import math
import sqlite3
from dataclasses import dataclass
from typing import Optional

from consistency.aggregator import (
    AggregationDecision,
    ItemAggregates,
    ItemConsistency,
    pearson_r,
    spearman_r,
    per_item_scores,
)


# ── confidence loader ─────────────────────────────────────────────────────


def load_mean_confidences(symbol_con: sqlite3.Connection,
                          item_fqns_by_table: dict[str, list[str]]
                          ) -> dict[str, float]:
    """For each item across Symbols/Macros/Tables/Queries, return the mean of
    Confidence1/2/3."""
    out: dict[str, float] = {}
    for table, fqns in item_fqns_by_table.items():
        if not fqns:
            continue
        placeholders = ",".join("?" * len(fqns))
        rows = symbol_con.execute(
            f"""
            SELECT fqn, Confidence1, Confidence2, Confidence3
            FROM {table} WHERE fqn IN ({placeholders})
            """,
            fqns,
        ).fetchall()
        for r in rows:
            cs = [r[k] for k in ("Confidence1", "Confidence2", "Confidence3")
                  if r[k] is not None]
            if cs:
                out[r["fqn"]] = sum(cs) / len(cs)
    return out


def load_kind_map(symbol_con: sqlite3.Connection,
                  item_fqns_by_table: dict[str, list[str]]
                  ) -> dict[str, str]:
    """Map fqn -> kind (Symbols.kind for that table; 'macro'/'table'/'query'
    for the other tables)."""
    out: dict[str, str] = {}
    for table, fqns in item_fqns_by_table.items():
        if not fqns:
            continue
        placeholders = ",".join("?" * len(fqns))
        if table == "Symbols":
            rows = symbol_con.execute(
                f"SELECT fqn, kind FROM Symbols WHERE fqn IN ({placeholders})",
                fqns,
            ).fetchall()
            for r in rows:
                out[r["fqn"]] = r["kind"]
        else:
            kind = {"Macros": "macro", "Tables": "table", "Queries": "query"}[table]
            for fqn in fqns:
                out[fqn] = kind
    return out


# ── correlation outputs ───────────────────────────────────────────────────


@dataclass(frozen=True)
class CorrelationResult:
    n: int
    pearson_r: Optional[float]
    spearman_r: Optional[float]


@dataclass(frozen=True)
class HeadlineCorrelation:
    aggregate_path: bool
    # On aggregate path: only `consistency` is populated.
    # On split path: both are populated; consistency is None.
    consistency: Optional[CorrelationResult]
    interpretation: Optional[CorrelationResult]
    justification: Optional[CorrelationResult]


@dataclass(frozen=True)
class PerKindRow:
    kind: str
    n: int
    pearson_r: Optional[float]
    mean_confidence: float
    mean_consistency: float
    # Populated only on the split path. Hold the second dimension's
    # parallel correlation + mean. On the aggregate path these are None.
    pearson_r_secondary: Optional[float] = None
    mean_consistency_secondary: Optional[float] = None


@dataclass(frozen=True)
class OutlierRow:
    fqn: str
    kind: str
    confidence: float
    consistency: float


@dataclass(frozen=True)
class IRRResult:
    """Inter-rater reliability across the three judges."""
    krippendorff_alpha_interpretation: Optional[float]
    krippendorff_alpha_justification: Optional[float]
    n_units: int   # items with >= 2 judges' values present


@dataclass(frozen=True)
class CorrelationReport:
    decision: AggregationDecision
    headline: HeadlineCorrelation
    per_kind: tuple[PerKindRow, ...]
    irr: IRRResult
    top_false_confidence: tuple[OutlierRow, ...]      # high conf, low consistency
    top_justified_uncertainty: tuple[OutlierRow, ...]  # low conf, low consistency
    item_count: int


# ── aggregate-path / split-path correlation ───────────────────────────────


def _per_item_views(aggregates: list[ItemAggregates],
                    confidences: dict[str, float],
                    kinds: dict[str, str]
                    ) -> list[tuple[ItemConsistency, str, float]]:
    """Return [(ItemConsistency, kind, mean_confidence)] for items with both
    a consistency view and a confidence value."""
    items = per_item_scores(aggregates)
    out: list[tuple[ItemConsistency, str, float]] = []
    for ic in items:
        conf = confidences.get(ic.fqn)
        kind = kinds.get(ic.fqn, "unknown")
        if conf is None:
            continue
        out.append((ic, kind, conf))
    return out


def compute_headline(
    decision: AggregationDecision,
    aggregates: list[ItemAggregates],
    confidences: dict[str, float],
    kinds: dict[str, str],
) -> HeadlineCorrelation:
    rows = _per_item_views(aggregates, confidences, kinds)
    if not rows:
        return HeadlineCorrelation(
            aggregate_path=decision.aggregate_path,
            consistency=None, interpretation=None, justification=None,
        )

    confs = [r[2] for r in rows]
    if decision.aggregate_path:
        ys = [r[0].consistency_score for r in rows]
        return HeadlineCorrelation(
            aggregate_path=True,
            consistency=CorrelationResult(
                n=len(rows),
                pearson_r=pearson_r(confs, ys),
                spearman_r=spearman_r(confs, ys),
            ),
            interpretation=None,
            justification=None,
        )

    interp_ys = [r[0].interpretation_score for r in rows]
    justif_ys = [r[0].justification_score for r in rows]
    return HeadlineCorrelation(
        aggregate_path=False,
        consistency=None,
        interpretation=CorrelationResult(
            n=len(rows),
            pearson_r=pearson_r(confs, interp_ys),
            spearman_r=spearman_r(confs, interp_ys),
        ),
        justification=CorrelationResult(
            n=len(rows),
            pearson_r=pearson_r(confs, justif_ys),
            spearman_r=spearman_r(confs, justif_ys),
        ),
    )


def compute_per_kind(
    decision: AggregationDecision,
    aggregates: list[ItemAggregates],
    confidences: dict[str, float],
    kinds: dict[str, str],
) -> list[PerKindRow]:
    """Per-kind correlation between confidence and the chosen consistency
    score(s).

    Aggregate path: ``pearson_r`` and ``mean_consistency`` reflect the
    aggregated consistency score; the secondary fields stay ``None``.

    Split path: ``pearson_r`` / ``mean_consistency`` reflect
    interpretation agreement; ``pearson_r_secondary`` /
    ``mean_consistency_secondary`` reflect justification agreement, so the
    spec's "per-kind breakdown either way" requirement is satisfied with
    two parallel series side by side.
    """
    rows = _per_item_views(aggregates, confidences, kinds)
    by_kind: dict[str, list] = {}
    for ic, kind, conf in rows:
        by_kind.setdefault(kind, []).append((ic, conf))

    out: list[PerKindRow] = []
    for kind, items in sorted(by_kind.items()):
        confs = [c for _, c in items]

        if decision.aggregate_path:
            ys = [ic.consistency_score for ic, _ in items]
            out.append(PerKindRow(
                kind=kind,
                n=len(items),
                pearson_r=pearson_r(confs, ys),
                mean_confidence=sum(confs) / len(confs) if confs else 0.0,
                mean_consistency=sum(ys) / len(ys) if ys else 0.0,
            ))
            continue

        # Split path: emit both dimensions.
        ys_interp = [ic.interpretation_score for ic, _ in items]
        ys_justif = [ic.justification_score for ic, _ in items]
        out.append(PerKindRow(
            kind=kind,
            n=len(items),
            pearson_r=pearson_r(confs, ys_interp),
            mean_confidence=sum(confs) / len(confs) if confs else 0.0,
            mean_consistency=sum(ys_interp) / len(ys_interp) if ys_interp else 0.0,
            pearson_r_secondary=pearson_r(confs, ys_justif),
            mean_consistency_secondary=(
                sum(ys_justif) / len(ys_justif) if ys_justif else 0.0
            ),
        ))
    return out


# ── outliers ──────────────────────────────────────────────────────────────


def compute_outliers(
    decision: AggregationDecision,
    aggregates: list[ItemAggregates],
    confidences: dict[str, float],
    kinds: dict[str, str],
    *,
    top_n: int = 5,
) -> tuple[tuple[OutlierRow, ...], tuple[OutlierRow, ...]]:
    """Return ``(top_false_confidence, top_justified_uncertainty)``.

    Both rankings are unfiltered: every item with a confidence and a
    consistency view is scored, and the top *top_n* are returned. This
    matches the spec's "top-5 false-confidence" / "top-5
    justified-uncertainty" wording, which describes ranking, not threshold
    filtering. If the sample is smaller than ``top_n`` the returned tuple
    is correspondingly shorter.

    On the aggregate path, ``consistency`` in OutlierRow is
    ``consistency_score``. On the split path, ``consistency`` is
    ``interpretation_score`` - the more behaviorally-central of the two
    split dimensions, chosen so a single ranking captures the
    "do the providers actually agree on what this does" axis. Justification
    agreement is intentionally not the ranking key here because justification
    diverging while interpretation agrees is a different (and more benign)
    failure mode than the false-confidence bucket is meant to surface.
    """
    rows = _per_item_views(aggregates, confidences, kinds)
    score_attr = "consistency_score" if decision.aggregate_path else "interpretation_score"

    # Normalise consistency to [0, 1] so the two bucket scores live on the
    # same scale as confidence.
    def _norm_cons(ic: ItemConsistency) -> float:
        return max(0.0, min(1.0, getattr(ic, score_attr) / 100.0))

    # False-confidence: HIGH confidence AND LOW consistency.
    # Rank by conf * (1 - consistency_norm); larger = more false-confidence.
    fc_scored = sorted(
        rows,
        key=lambda t: t[2] * (1.0 - _norm_cons(t[0])),
        reverse=True,
    )
    false_conf = tuple(
        OutlierRow(fqn=ic.fqn, kind=kind, confidence=conf,
                   consistency=getattr(ic, score_attr))
        for ic, kind, conf in fc_scored[:top_n]
    )

    # Justified uncertainty: LOW confidence AND LOW consistency.
    # Rank by (1 - conf) * (1 - consistency_norm); larger = both low.
    ju_scored = sorted(
        rows,
        key=lambda t: (1.0 - t[2]) * (1.0 - _norm_cons(t[0])),
        reverse=True,
    )
    just_unc = tuple(
        OutlierRow(fqn=ic.fqn, kind=kind, confidence=conf,
                   consistency=getattr(ic, score_attr))
        for ic, kind, conf in ju_scored[:top_n]
    )

    return false_conf, just_unc


# ── Krippendorff's alpha (interval) ───────────────────────────────────────


def krippendorff_alpha_interval(units: list[list[Optional[float]]]
                                ) -> Optional[float]:
    """Compute Krippendorff's alpha for interval data.

    Each row in *units* is one unit (item) with one value per coder, or
    ``None`` if the coder did not rate that unit.  Returns ``None`` if the
    data is too sparse to compute the statistic (no observed disagreement
    between coders).
    """
    pairable: list[tuple[float, float]] = []
    for row in units:
        present = [v for v in row if v is not None]
        if len(present) < 2:
            continue
        # All ordered pairs (i, j) i != j contribute equally.
        for i in range(len(present)):
            for j in range(len(present)):
                if i == j:
                    continue
                pairable.append((present[i], present[j]))

    if not pairable:
        return None

    # Observed disagreement.
    do = sum((a - b) ** 2 for a, b in pairable) / len(pairable)

    # Expected disagreement: pair up every observed value with every other
    # observed value across the whole sample.
    all_values: list[float] = []
    for row in units:
        all_values.extend(v for v in row if v is not None)
    n = len(all_values)
    if n < 2:
        return None
    sumsq = 0.0
    for i in range(n):
        for j in range(n):
            if i == j:
                continue
            sumsq += (all_values[i] - all_values[j]) ** 2
    de = sumsq / (n * (n - 1))

    if de == 0.0:
        return None
    return 1.0 - do / de


def compute_irr(aggregates: list[ItemAggregates],
                judges: tuple[str, ...] = ("gemini-pro", "opus", "gpt-5-4"),
                ) -> IRRResult:
    """Inter-rater reliability across the three judges per item.

    Uses ``interpretation_pct_mean`` and ``justification_pct_mean``
    (each item-judge value is the mean across the three pairs the judge
    rated, so the rater has one number per dimension per item).
    """
    by_fqn: dict[str, dict[str, ItemAggregates]] = {}
    for a in aggregates:
        by_fqn.setdefault(a.fqn, {})[a.judge_provider] = a

    interp_units: list[list[Optional[float]]] = []
    justif_units: list[list[Optional[float]]] = []
    n_units = 0
    for fqn, by_judge in by_fqn.items():
        present = sum(1 for j in judges if j in by_judge)
        if present >= 2:
            n_units += 1
        interp_row: list[Optional[float]] = []
        justif_row: list[Optional[float]] = []
        for j in judges:
            a = by_judge.get(j)
            interp_row.append(a.interpretation_pct_mean if a else None)
            justif_row.append(a.justification_pct_mean if a else None)
        interp_units.append(interp_row)
        justif_units.append(justif_row)

    return IRRResult(
        krippendorff_alpha_interpretation=krippendorff_alpha_interval(interp_units),
        krippendorff_alpha_justification=krippendorff_alpha_interval(justif_units),
        n_units=n_units,
    )


# ── full report assembly ──────────────────────────────────────────────────


def compute_report(
    decision: AggregationDecision,
    aggregates: list[ItemAggregates],
    confidences: dict[str, float],
    kinds: dict[str, str],
) -> CorrelationReport:
    headline = compute_headline(decision, aggregates, confidences, kinds)
    per_kind = compute_per_kind(decision, aggregates, confidences, kinds)
    irr = compute_irr(aggregates)
    false_conf, just_unc = compute_outliers(decision, aggregates, confidences, kinds)

    item_count = len({a.fqn for a in aggregates})
    return CorrelationReport(
        decision=decision,
        headline=headline,
        per_kind=tuple(per_kind),
        irr=irr,
        top_false_confidence=false_conf,
        top_justified_uncertainty=just_unc,
        item_count=item_count,
    )


# ── thresholds reference ──────────────────────────────────────────────────


@dataclass(frozen=True)
class ConfidenceThresholds:
    """Session-4 escalation thresholds, used in the verdict block."""
    escalate_to_opinion_2: float = 0.75
    escalate_to_opinion_3: float = 0.70


VERDICT_TRUST = "trust"
VERDICT_RECALIBRATE = "recalibrate"
VERDICT_SPLIT_DIMENSIONS = "split-dimensions"


def render_verdict(report: CorrelationReport,
                   thresholds: ConfidenceThresholds = ConfidenceThresholds(),
                   ) -> tuple[str, str]:
    """Choose between trust / recalibrate / split-dimensions and write
    a one-paragraph rationale."""
    headline = report.headline
    if not headline.aggregate_path:
        # Split path: confidence calibrated against each dimension separately.
        return VERDICT_SPLIT_DIMENSIONS, (
            "The two agreement dimensions are not interchangeable: per-judge "
            "intra-correlations did not all clear the positive non-trivial "
            "threshold. Confidence should be calibrated against each "
            "dimension separately rather than against a single consistency "
            "score. See the split-path correlations for the actual signs and "
            "magnitudes."
        )

    cons = headline.consistency
    if cons is None or cons.pearson_r is None:
        return VERDICT_RECALIBRATE, (
            "No usable correlation could be computed - too few items with "
            "matched confidence/consistency or constant input. Recalibration "
            "advised before the confidence column is trusted at the "
            f"{thresholds.escalate_to_opinion_2:.2f}/"
            f"{thresholds.escalate_to_opinion_3:.2f} thresholds."
        )

    r = cons.pearson_r
    if r >= 0.30:
        return VERDICT_TRUST, (
            f"Self-reported confidence and cross-provider consistency are "
            f"positively correlated (Pearson r = {r:.2f}), which validates "
            f"Session 4's escalation thresholds at "
            f"{thresholds.escalate_to_opinion_2:.2f}/"
            f"{thresholds.escalate_to_opinion_3:.2f}. The confidence column "
            f"is trustworthy as a downstream signal."
        )

    return VERDICT_RECALIBRATE, (
        f"Self-reported confidence does not show a meaningful positive "
        f"correlation with cross-provider consistency (Pearson r = {r:.2f}). "
        f"The confidence column is not yet trustworthy as a downstream "
        f"signal at the configured thresholds; recalibration is advised "
        f"before scaling beyond the probe modules."
    )


# ── helpers for report writers ────────────────────────────────────────────


def items_by_table(symbol_con: sqlite3.Connection,
                   aggregates: list[ItemAggregates]
                   ) -> dict[str, list[str]]:
    """Group item FQNs by the table they live in (Symbols/Macros/...)."""
    fqns = list({a.fqn for a in aggregates})
    out: dict[str, list[str]] = {"Symbols": [], "Macros": [], "Tables": [], "Queries": []}
    for table in out:
        placeholders = ",".join("?" * len(fqns)) if fqns else ""
        if not fqns:
            continue
        rows = symbol_con.execute(
            f"SELECT fqn FROM {table} WHERE fqn IN ({placeholders})",
            fqns,
        ).fetchall()
        out[table] = [r[0] for r in rows]
    return out


# ── CLI entry point ───────────────────────────────────────────────────────


def render_scatter_png(
    decision: AggregationDecision,
    aggregates: list[ItemAggregates],
    confidences: dict[str, float],
    kinds: dict[str, str],
    output_path: str,
) -> None:
    """Save a confidence-vs-consistency scatter PNG to *output_path*."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    rows = _per_item_views(aggregates, confidences, kinds)
    confs = [r[2] for r in rows]
    kinds_list = sorted({r[1] for r in rows})
    palette = plt.get_cmap("tab10")(range(len(kinds_list)))
    color_by_kind = {k: palette[i] for i, k in enumerate(kinds_list)}

    if decision.aggregate_path:
        fig, ax = plt.subplots(figsize=(8, 6), dpi=120)
        for kind in kinds_list:
            x = [r[2] for r in rows if r[1] == kind]
            y = [r[0].consistency_score for r in rows if r[1] == kind]
            ax.scatter(x, y, label=kind, color=color_by_kind[kind], alpha=0.75, s=60)
        ax.set_xlabel("Mean self-reported confidence (Confidence1/2/3 mean)")
        ax.set_ylabel("Cross-provider consistency (mean of two agreement scores)")
        ax.set_title("Confidence vs. consistency (aggregate path)")
        ax.legend(loc="best", fontsize=8)
        ax.grid(alpha=0.3)
        fig.tight_layout()
        fig.savefig(output_path)
        plt.close(fig)
        return

    fig, axes = plt.subplots(1, 2, figsize=(13, 6), dpi=120)
    for ax, score_attr, title in (
        (axes[0], "interpretation_score", "Confidence vs. interpretation agreement"),
        (axes[1], "justification_score",  "Confidence vs. justification agreement"),
    ):
        for kind in kinds_list:
            x = [r[2] for r in rows if r[1] == kind]
            y = [getattr(r[0], score_attr) for r in rows if r[1] == kind]
            ax.scatter(x, y, label=kind, color=color_by_kind[kind], alpha=0.75, s=60)
        ax.set_xlabel("Mean self-reported confidence")
        ax.set_ylabel(title.split(" vs. ")[1].capitalize())
        ax.set_title(title + " (split path)")
        ax.legend(loc="best", fontsize=8)
        ax.grid(alpha=0.3)
    fig.tight_layout()
    fig.savefig(output_path)
    plt.close(fig)


def _summary_json(report: CorrelationReport, verdict: str, verdict_text: str) -> dict:
    """Compact, human-friendly JSON dump of the headline numbers."""
    h = report.headline

    def _cr(c: Optional[CorrelationResult]) -> Optional[dict]:
        if c is None:
            return None
        return {"n": c.n, "pearson_r": c.pearson_r, "spearman_r": c.spearman_r}

    return {
        "item_count": report.item_count,
        "aggregate_path": h.aggregate_path,
        "decision_rationale": report.decision.rationale,
        "judge_correlations": [
            {"judge": jc.judge_provider, "n": jc.n_items,
             "pearson_r": jc.pearson_r if not (jc.pearson_r != jc.pearson_r) else None,
             "note": jc.note}
            for jc in report.decision.judge_correlations
        ],
        "headline": {
            "consistency": _cr(h.consistency),
            "interpretation": _cr(h.interpretation),
            "justification": _cr(h.justification),
        },
        "per_kind": [
            {"kind": r.kind, "n": r.n,
             "pearson_r": r.pearson_r,
             "mean_confidence": r.mean_confidence,
             "mean_consistency": r.mean_consistency,
             "pearson_r_secondary": r.pearson_r_secondary,
             "mean_consistency_secondary": r.mean_consistency_secondary}
            for r in report.per_kind
        ],
        "irr": {
            "krippendorff_alpha_interpretation":
                report.irr.krippendorff_alpha_interpretation,
            "krippendorff_alpha_justification":
                report.irr.krippendorff_alpha_justification,
            "n_units": report.irr.n_units,
        },
        "outliers": {
            "false_confidence": [
                {"fqn": o.fqn, "kind": o.kind, "confidence": o.confidence,
                 "consistency": o.consistency}
                for o in report.top_false_confidence
            ],
            "justified_uncertainty": [
                {"fqn": o.fqn, "kind": o.kind, "confidence": o.confidence,
                 "consistency": o.consistency}
                for o in report.top_justified_uncertainty
            ],
        },
        "verdict": verdict,
        "verdict_text": verdict_text,
    }


def main(argv=None):
    import argparse
    import json
    from pathlib import Path

    from consistency.aggregator import compute_aggregation_decision

    parser = argparse.ArgumentParser(
        description="Compute the confidence-vs-consistency correlation report.")
    parser.add_argument("--symbol-db", default="symbol-db.sqlite")
    parser.add_argument("--grades-db", default="consistency/grades.sqlite")
    parser.add_argument("--scatter-png", default="consistency/correlation-scatter.png")
    parser.add_argument("--summary-json", default="consistency/correlation-summary.json",
                        help="Optional path for a JSON summary suitable for "
                             "feeding into the markdown writer.")
    args = parser.parse_args(argv)

    grades_con = sqlite3.connect(args.grades_db)
    grades_con.row_factory = sqlite3.Row
    symbol_con = sqlite3.connect(args.symbol_db)
    symbol_con.row_factory = sqlite3.Row

    decision, aggregates = compute_aggregation_decision(grades_con)
    fqns_by_table = items_by_table(symbol_con, aggregates)
    confidences = load_mean_confidences(symbol_con, fqns_by_table)
    kinds = load_kind_map(symbol_con, fqns_by_table)

    report = compute_report(decision, aggregates, confidences, kinds)
    verdict, verdict_text = render_verdict(report)

    # Persist the scatter PNG before any console-display side effects.
    Path(args.scatter_png).parent.mkdir(parents=True, exist_ok=True)
    render_scatter_png(decision, aggregates, confidences, kinds, args.scatter_png)
    summary = _summary_json(report, verdict, verdict_text)
    with open(args.summary_json, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
        f.write("\n")
    print(json.dumps(summary, indent=2))
    grades_con.close()
    symbol_con.close()


if __name__ == "__main__":
    main()
