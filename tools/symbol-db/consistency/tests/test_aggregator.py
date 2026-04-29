"""Hermetic tests for the per-judge intra-correlation reduction."""

from __future__ import annotations

import math
import sqlite3
import unittest

from consistency.aggregator import (
    AggregationDecision,
    ItemAggregates,
    JudgeCorrelation,
    compute_aggregation_decision,
    compute_judge_correlation,
    decide_aggregation,
    load_item_aggregates,
    pearson_r,
    per_item_scores,
    spearman_r,
)


class TestPearsonR(unittest.TestCase):
    def test_perfect_positive(self):
        self.assertAlmostEqual(pearson_r([1, 2, 3, 4], [10, 20, 30, 40]), 1.0)

    def test_perfect_negative(self):
        self.assertAlmostEqual(pearson_r([1, 2, 3, 4], [40, 30, 20, 10]), -1.0)

    def test_zero_variance_returns_none(self):
        self.assertIsNone(pearson_r([5, 5, 5], [1, 2, 3]))
        self.assertIsNone(pearson_r([1, 2, 3], [5, 5, 5]))

    def test_too_few_returns_none(self):
        self.assertIsNone(pearson_r([1], [2]))
        self.assertIsNone(pearson_r([], []))

    def test_mixed_signal(self):
        # Loosely positive correlation.
        r = pearson_r([1, 2, 3, 4, 5], [2, 1, 5, 4, 6])
        self.assertGreater(r, 0.5)


class TestSpearmanR(unittest.TestCase):
    def test_monotonic_non_linear(self):
        # Spearman should be 1.0 even though Pearson is not.
        xs = [1, 2, 3, 4]
        ys = [1, 4, 9, 16]
        self.assertAlmostEqual(spearman_r(xs, ys), 1.0)

    def test_ties_handled(self):
        # Two pairs of ties; should not raise and should be defined.
        xs = [1, 1, 2, 2, 3, 3]
        ys = [1, 2, 3, 4, 5, 6]
        r = spearman_r(xs, ys)
        self.assertIsNotNone(r)
        self.assertGreater(r, 0)


def _agg(fqn: str, stratum: str, judge: str, ip: float, jp: float) -> ItemAggregates:
    return ItemAggregates(
        fqn=fqn, stratum=stratum, judge_provider=judge,
        interpretation_pct_mean=ip, justification_pct_mean=jp,
        pair_count=3,
    )


class TestComputeJudgeCorrelation(unittest.TestCase):
    def test_perfect_positive_correlation(self):
        agg = [
            _agg("a", "sub", "opus", 50, 50),
            _agg("b", "sub", "opus", 60, 60),
            _agg("c", "sub", "opus", 70, 70),
            _agg("d", "sub", "opus", 80, 80),
        ]
        jc = compute_judge_correlation(agg, "opus")
        self.assertAlmostEqual(jc.pearson_r, 1.0)
        self.assertEqual(jc.note, "ok")
        self.assertEqual(jc.n_items, 4)

    def test_constant_input_marked_degenerate(self):
        agg = [
            _agg("a", "sub", "opus", 50, 50),
            _agg("b", "sub", "opus", 50, 70),
        ]
        jc = compute_judge_correlation(agg, "opus")
        self.assertTrue(math.isnan(jc.pearson_r))
        self.assertEqual(jc.note, "constant-input-or-too-few")

    def test_near_zero_marked_near_zero(self):
        # r close to 0 but not exactly 0
        agg = [
            _agg("a", "sub", "opus", 50, 60),
            _agg("b", "sub", "opus", 60, 55),
            _agg("c", "sub", "opus", 70, 62),
            _agg("d", "sub", "opus", 80, 58),
        ]
        jc = compute_judge_correlation(agg, "opus")
        self.assertEqual(jc.note, "near-zero")


class TestDecideAggregation(unittest.TestCase):
    def test_all_positive_above_threshold_aggregate(self):
        jcs = [
            JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
            JudgeCorrelation("opus", 42, 0.5, "ok"),
            JudgeCorrelation("gpt-5-4", 42, 0.4, "ok"),
        ]
        d = decide_aggregation(jcs)
        self.assertTrue(d.aggregate_path)

    def test_one_negative_split(self):
        jcs = [
            JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
            JudgeCorrelation("opus", 42, -0.4, "ok"),
            JudgeCorrelation("gpt-5-4", 42, 0.4, "ok"),
        ]
        d = decide_aggregation(jcs)
        self.assertFalse(d.aggregate_path)
        self.assertIn("opposite directions", d.rationale)

    def test_one_near_zero_split(self):
        jcs = [
            JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
            JudgeCorrelation("opus", 42, 0.05, "near-zero"),
            JudgeCorrelation("gpt-5-4", 42, 0.4, "ok"),
        ]
        d = decide_aggregation(jcs)
        self.assertFalse(d.aggregate_path)
        self.assertIn("near-zero", d.rationale)

    def test_degenerate_judge_split(self):
        jcs = [
            JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
            JudgeCorrelation("opus", 42, float("nan"), "constant-input-or-too-few"),
            JudgeCorrelation("gpt-5-4", 42, 0.4, "ok"),
        ]
        d = decide_aggregation(jcs)
        self.assertFalse(d.aggregate_path)

    def test_rationale_distinguishes_negative_from_near_zero(self):
        neg = decide_aggregation([
            JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
            JudgeCorrelation("opus", 42, -0.4, "ok"),
            JudgeCorrelation("gpt-5-4", 42, 0.5, "ok"),
        ])
        self.assertIn("negative", neg.rationale.lower())
        self.assertNotIn("near-zero", neg.rationale.lower())

        nz = decide_aggregation([
            JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
            JudgeCorrelation("opus", 42, 0.10, "near-zero"),
            JudgeCorrelation("gpt-5-4", 42, 0.5, "ok"),
        ])
        self.assertIn("near-zero", nz.rationale.lower())
        self.assertNotIn("negative", nz.rationale.lower())

    def test_rationale_distinguishes_exact_zero(self):
        zero = decide_aggregation([
            JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
            JudgeCorrelation("opus", 42, 0.0, "near-zero"),
            JudgeCorrelation("gpt-5-4", 42, 0.5, "ok"),
        ])
        # Exact-zero items are not "negative" but they are "no shared signal".
        self.assertNotIn("negative", zero.rationale.lower())
        self.assertIn("zero", zero.rationale.lower())


class TestLoadAndPerItem(unittest.TestCase):
    def _seed(self, con: sqlite3.Connection):
        con.executescript("""
            CREATE TABLE Grades (
                item_fqn TEXT, item_table TEXT, item_stratum TEXT,
                judge_provider TEXT, pair TEXT,
                interpretation_agreement_pct INTEGER,
                justification_agreement_pct INTEGER,
                disagreement_type TEXT, summary TEXT, created_at TEXT,
                PRIMARY KEY (item_fqn, judge_provider, pair)
            );
        """)
        rows = []
        for fqn, stratum in [("A", "sub"), ("B", "enum_value")]:
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                for pair, ip, jp in (("1-2", 80, 70), ("2-3", 90, 85), ("1-3", 60, 55)):
                    rows.append((fqn, "Symbols", stratum, judge, pair, ip, jp,
                                 "scope", "x", "now"))
        con.executemany(
            "INSERT INTO Grades VALUES (?,?,?,?,?,?,?,?,?,?)",
            rows,
        )

    def test_load_aggregates_average(self):
        con = sqlite3.connect(":memory:")
        con.row_factory = sqlite3.Row
        self._seed(con)
        agg = load_item_aggregates(con)
        # 2 items x 3 judges = 6 rows.
        self.assertEqual(len(agg), 6)
        sample = next(a for a in agg if a.fqn == "A" and a.judge_provider == "opus")
        self.assertAlmostEqual(sample.interpretation_pct_mean, (80 + 90 + 60) / 3)
        self.assertAlmostEqual(sample.justification_pct_mean, (70 + 85 + 55) / 3)

    def test_compute_aggregation_decision_end_to_end(self):
        con = sqlite3.connect(":memory:")
        con.row_factory = sqlite3.Row
        self._seed(con)
        decision, agg = compute_aggregation_decision(con)
        # Synthetic data is identical across judges -> within-judge constant
        # spread on interp / justif -> non-trivial Pearson.
        self.assertEqual(len(agg), 6)
        self.assertEqual(len(decision.judge_correlations), 3)

    def test_per_item_scores_average_across_judges(self):
        con = sqlite3.connect(":memory:")
        con.row_factory = sqlite3.Row
        self._seed(con)
        agg = load_item_aggregates(con)
        items = per_item_scores(agg)
        self.assertEqual(len(items), 2)
        a = next(i for i in items if i.fqn == "A")
        # all judges identical in seed -> per-item == per-judge mean
        self.assertAlmostEqual(a.interpretation_score, (80 + 90 + 60) / 3)
        self.assertAlmostEqual(
            a.consistency_score,
            ((80 + 90 + 60) / 3 + (70 + 85 + 55) / 3) / 2,
        )


if __name__ == "__main__":
    unittest.main()
