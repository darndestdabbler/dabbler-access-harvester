"""Hermetic tests for the confidence-vs-consistency correlation analysis."""

from __future__ import annotations

import unittest

from consistency.aggregator import (
    AggregationDecision,
    ItemAggregates,
    JudgeCorrelation,
)
from consistency.correlation import (
    ConfidenceThresholds,
    OutlierRow,
    VERDICT_RECALIBRATE,
    VERDICT_SPLIT_DIMENSIONS,
    VERDICT_TRUST,
    compute_headline,
    compute_per_kind,
    compute_outliers,
    compute_irr,
    compute_report,
    krippendorff_alpha_interval,
    render_verdict,
)


def _agg(fqn: str, stratum: str, judge: str, ip: float, jp: float) -> ItemAggregates:
    return ItemAggregates(
        fqn=fqn, stratum=stratum, judge_provider=judge,
        interpretation_pct_mean=ip, justification_pct_mean=jp,
        pair_count=3,
    )


_AGGREGATE_DECISION = AggregationDecision(
    judge_correlations=(
        JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
        JudgeCorrelation("opus", 42, 0.5, "ok"),
        JudgeCorrelation("gpt-5-4", 42, 0.4, "ok"),
    ),
    aggregate_path=True,
    rationale="aggregate path",
)

_SPLIT_DECISION = AggregationDecision(
    judge_correlations=(
        JudgeCorrelation("gemini-pro", 42, 0.6, "ok"),
        JudgeCorrelation("opus", 42, -0.5, "ok"),
        JudgeCorrelation("gpt-5-4", 42, 0.4, "ok"),
    ),
    aggregate_path=False,
    rationale="split path",
)


class TestKrippendorffAlpha(unittest.TestCase):
    def test_perfect_agreement_alpha_one(self):
        units = [[80.0, 80.0, 80.0],
                 [70.0, 70.0, 70.0],
                 [60.0, 60.0, 60.0]]
        a = krippendorff_alpha_interval(units)
        self.assertAlmostEqual(a, 1.0, places=6)

    def test_no_disagreement_returns_none(self):
        # All values identical -> de = 0 -> undefined.
        units = [[50.0, 50.0, 50.0]] * 5
        self.assertIsNone(krippendorff_alpha_interval(units))

    def test_full_disagreement_yields_low_alpha(self):
        units = [[100.0, 0.0, 50.0],
                 [0.0, 100.0, 50.0],
                 [50.0, 50.0, 0.0]]
        a = krippendorff_alpha_interval(units)
        self.assertIsNotNone(a)
        self.assertLess(a, 0.5)

    def test_sparse_unit_skipped(self):
        # Only one rater on the unit -> excluded from observed disagreement
        # but still counted in expected (population).
        units = [[80.0, None, None],
                 [70.0, 70.0, 70.0],
                 [60.0, 60.0, 60.0]]
        a = krippendorff_alpha_interval(units)
        self.assertIsNotNone(a)
        # The two-rater units are perfectly agreed -> high alpha.
        self.assertGreater(a, 0.9)


class TestComputeHeadline(unittest.TestCase):
    def test_aggregate_path_pearson_present(self):
        agg = []
        for i, (fqn, conf) in enumerate(zip("ABCDE", [0.5, 0.6, 0.7, 0.8, 0.9])):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                ip = 50 + i * 10
                jp = 45 + i * 10
                agg.append(_agg(fqn, "sub", judge, ip, jp))
        confs = {"A": 0.5, "B": 0.6, "C": 0.7, "D": 0.8, "E": 0.9}
        kinds = {f: "sub" for f in "ABCDE"}
        h = compute_headline(_AGGREGATE_DECISION, agg, confs, kinds)
        self.assertTrue(h.aggregate_path)
        self.assertIsNotNone(h.consistency)
        self.assertGreater(h.consistency.pearson_r, 0.9)
        self.assertIsNone(h.interpretation)
        self.assertIsNone(h.justification)

    def test_split_path_two_correlations(self):
        agg = []
        for i, fqn in enumerate("ABCDE"):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                # interpretation rises; justification falls -> opposite signs.
                ip = 50 + i * 10
                jp = 90 - i * 10
                agg.append(_agg(fqn, "sub", judge, ip, jp))
        confs = {"A": 0.5, "B": 0.6, "C": 0.7, "D": 0.8, "E": 0.9}
        kinds = {f: "sub" for f in "ABCDE"}
        h = compute_headline(_SPLIT_DECISION, agg, confs, kinds)
        self.assertFalse(h.aggregate_path)
        self.assertIsNone(h.consistency)
        self.assertIsNotNone(h.interpretation)
        self.assertGreater(h.interpretation.pearson_r, 0.9)
        self.assertIsNotNone(h.justification)
        self.assertLess(h.justification.pearson_r, -0.9)


class TestPerKind(unittest.TestCase):
    def test_split_by_kind(self):
        agg = []
        for i, fqn in enumerate("AB"):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                agg.append(_agg(fqn, "sub", judge, 70, 60))
        for i, fqn in enumerate("CD"):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                agg.append(_agg(fqn, "enum_value", judge, 90, 80))
        confs = {"A": 0.6, "B": 0.7, "C": 0.85, "D": 0.95}
        kinds = {"A": "sub", "B": "sub", "C": "enum_value", "D": "enum_value"}
        rows = compute_per_kind(_AGGREGATE_DECISION, agg, confs, kinds)
        kind_to_row = {r.kind: r for r in rows}
        self.assertIn("sub", kind_to_row)
        self.assertIn("enum_value", kind_to_row)
        self.assertEqual(kind_to_row["sub"].n, 2)
        self.assertEqual(kind_to_row["enum_value"].n, 2)

    def test_aggregate_path_leaves_secondary_none(self):
        agg = []
        for i, fqn in enumerate("AB"):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                agg.append(_agg(fqn, "sub", judge, 70 + i * 5, 60 + i * 5))
        confs = {"A": 0.6, "B": 0.9}
        kinds = {"A": "sub", "B": "sub"}
        rows = compute_per_kind(_AGGREGATE_DECISION, agg, confs, kinds)
        for r in rows:
            self.assertIsNone(r.pearson_r_secondary)
            self.assertIsNone(r.mean_consistency_secondary)

    def test_split_path_emits_both_dimensions_per_kind(self):
        # Engineer a fixture where interpretation rises and justification
        # falls across items so the two per-kind r values have opposite
        # signs - asserts both are computed and are not the same number.
        agg = []
        for i, fqn in enumerate("ABCD"):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                agg.append(_agg(fqn, "sub", judge,
                                interpretation_pct_mean := 50 + i * 10,
                                justification_pct_mean := 90 - i * 10))
                # Re-bind to silence unused warning; aliases for clarity only.
                _ = (interpretation_pct_mean, justification_pct_mean)
        confs = {f: 0.5 + i * 0.1 for i, f in enumerate("ABCD")}
        kinds = {f: "sub" for f in "ABCD"}
        rows = compute_per_kind(_SPLIT_DECISION, agg, confs, kinds)
        self.assertEqual(len(rows), 1)
        row = rows[0]
        self.assertIsNotNone(row.pearson_r)
        self.assertIsNotNone(row.pearson_r_secondary)
        # Interpretation correlates positively with confidence in this fixture;
        # justification correlates negatively.
        self.assertGreater(row.pearson_r, 0.5)
        self.assertLess(row.pearson_r_secondary, -0.5)
        self.assertIsNotNone(row.mean_consistency_secondary)


class TestOutliers(unittest.TestCase):
    """Outlier ranking is now top-N (no thresholds), so all items are
    eligible and ordering reflects the score function. The
    high-conf-low-cons item should rank above the high-conf-high-cons
    item in false-confidence, and the low-conf-low-cons item should
    rank above the low-conf-high-cons item in justified-uncertainty."""

    def test_false_confidence_ranks_high_conf_low_consistency_first(self):
        agg = [
            _agg("HighConfLowCons", "sub", "opus", 40, 40),
            _agg("HighConfLowCons", "sub", "gemini-pro", 40, 40),
            _agg("HighConfLowCons", "sub", "gpt-5-4", 40, 40),
            _agg("HighConfHighCons", "sub", "opus", 95, 95),
            _agg("HighConfHighCons", "sub", "gemini-pro", 95, 95),
            _agg("HighConfHighCons", "sub", "gpt-5-4", 95, 95),
        ]
        confs = {"HighConfLowCons": 0.95, "HighConfHighCons": 0.95}
        kinds = {"HighConfLowCons": "sub", "HighConfHighCons": "sub"}
        false_conf, _ = compute_outliers(_AGGREGATE_DECISION, agg, confs, kinds)
        self.assertEqual(false_conf[0].fqn, "HighConfLowCons")

    def test_justified_uncertainty_ranks_low_conf_low_consistency_first(self):
        agg = [
            _agg("LowConfLowCons", "sub", "opus", 40, 40),
            _agg("LowConfLowCons", "sub", "gemini-pro", 40, 40),
            _agg("LowConfLowCons", "sub", "gpt-5-4", 40, 40),
            _agg("LowConfHighCons", "sub", "opus", 95, 95),
            _agg("LowConfHighCons", "sub", "gemini-pro", 95, 95),
            _agg("LowConfHighCons", "sub", "gpt-5-4", 95, 95),
        ]
        confs = {"LowConfLowCons": 0.55, "LowConfHighCons": 0.55}
        kinds = {"LowConfLowCons": "sub", "LowConfHighCons": "sub"}
        _, just_unc = compute_outliers(_AGGREGATE_DECISION, agg, confs, kinds)
        self.assertEqual(just_unc[0].fqn, "LowConfLowCons")


class TestRenderVerdict(unittest.TestCase):
    def _build_report_with_pearson(self, r_value: float):
        agg = []
        for i, fqn in enumerate("ABCDE"):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                # We don't actually need to engineer pearson exactly; we
                # build a CorrelationReport with a synthetic headline.
                agg.append(_agg(fqn, "sub", judge, 50 + i * 10, 45 + i * 10))
        from consistency.correlation import CorrelationReport, HeadlineCorrelation, CorrelationResult, IRRResult
        return CorrelationReport(
            decision=_AGGREGATE_DECISION,
            headline=HeadlineCorrelation(
                aggregate_path=True,
                consistency=CorrelationResult(n=5, pearson_r=r_value, spearman_r=r_value),
                interpretation=None,
                justification=None,
            ),
            per_kind=(),
            irr=IRRResult(0.8, 0.8, 5),
            top_false_confidence=(),
            top_justified_uncertainty=(),
            item_count=5,
        )

    def test_high_positive_yields_trust(self):
        r = self._build_report_with_pearson(0.55)
        verdict, _ = render_verdict(r)
        self.assertEqual(verdict, VERDICT_TRUST)

    def test_low_positive_yields_recalibrate(self):
        r = self._build_report_with_pearson(0.10)
        verdict, _ = render_verdict(r)
        self.assertEqual(verdict, VERDICT_RECALIBRATE)

    def test_negative_yields_recalibrate(self):
        r = self._build_report_with_pearson(-0.30)
        verdict, _ = render_verdict(r)
        self.assertEqual(verdict, VERDICT_RECALIBRATE)

    def test_split_path_yields_split_dimensions(self):
        from consistency.correlation import CorrelationReport, HeadlineCorrelation, CorrelationResult, IRRResult
        rep = CorrelationReport(
            decision=_SPLIT_DECISION,
            headline=HeadlineCorrelation(
                aggregate_path=False,
                consistency=None,
                interpretation=CorrelationResult(n=5, pearson_r=0.6, spearman_r=0.6),
                justification=CorrelationResult(n=5, pearson_r=-0.4, spearman_r=-0.4),
            ),
            per_kind=(),
            irr=IRRResult(0.8, 0.8, 5),
            top_false_confidence=(),
            top_justified_uncertainty=(),
            item_count=5,
        )
        verdict, _ = render_verdict(rep)
        self.assertEqual(verdict, VERDICT_SPLIT_DIMENSIONS)


class TestComputeIRR(unittest.TestCase):
    def test_irr_perfect_agreement(self):
        agg = [
            _agg("a", "sub", "gemini-pro", 80, 70),
            _agg("a", "sub", "opus",       80, 70),
            _agg("a", "sub", "gpt-5-4",    80, 70),
            _agg("b", "sub", "gemini-pro", 60, 50),
            _agg("b", "sub", "opus",       60, 50),
            _agg("b", "sub", "gpt-5-4",    60, 50),
        ]
        irr = compute_irr(agg)
        self.assertAlmostEqual(irr.krippendorff_alpha_interpretation, 1.0)
        self.assertAlmostEqual(irr.krippendorff_alpha_justification, 1.0)

    def test_irr_n_units_excludes_single_judge_items(self):
        # Item "lonely" only has gemini-pro; should not count toward n_units.
        agg = [
            _agg("lonely", "sub", "gemini-pro", 80, 70),
            _agg("a", "sub", "gemini-pro", 80, 70),
            _agg("a", "sub", "opus",       80, 70),
            _agg("b", "sub", "gemini-pro", 60, 50),
            _agg("b", "sub", "opus",       60, 50),
            _agg("b", "sub", "gpt-5-4",    60, 50),
        ]
        irr = compute_irr(agg)
        self.assertEqual(irr.n_units, 2)


class TestOutliersTopN(unittest.TestCase):
    """The spec says top-5 outliers, ranked, not threshold-filtered."""
    def test_returns_five_items_when_pool_has_more(self):
        agg = []
        for fqn, ip in [("a", 95), ("b", 90), ("c", 85), ("d", 80), ("e", 75),
                        ("f", 70), ("g", 65), ("h", 60)]:
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                agg.append(_agg(fqn, "sub", judge, ip, ip))
        confs = {fqn: 0.9 for fqn in "abcdefgh"}  # all "high confidence"
        kinds = {fqn: "sub" for fqn in "abcdefgh"}
        false_conf, just_unc = compute_outliers(_AGGREGATE_DECISION, agg, confs, kinds)
        # No threshold filtering, so we always get top-5 from an 8-item pool.
        self.assertEqual(len(false_conf), 5)
        # Lowest consistency items rank highest in false-confidence:
        # h(60) < g(65) < f(70) < e(75) < d(80).
        self.assertEqual([o.fqn for o in false_conf], ["h", "g", "f", "e", "d"])

    def test_justified_uncertainty_ranks_low_conf_low_consistency(self):
        agg = []
        items = [
            ("hh", 90, 0.95),  # high conf, high consistency
            ("hl", 50, 0.95),  # high conf, low consistency
            ("lh", 90, 0.45),  # low conf, high consistency
            ("ll", 50, 0.45),  # low conf, low consistency  <- top justified-uncertainty
            ("mm", 70, 0.70),
        ]
        for fqn, ip, _conf in items:
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                agg.append(_agg(fqn, "sub", judge, ip, ip))
        confs = {fqn: c for fqn, _ip, c in items}
        kinds = {fqn: "sub" for fqn, _ip, _c in items}
        _, just_unc = compute_outliers(_AGGREGATE_DECISION, agg, confs, kinds)
        # Top of the ranking should be the low-conf-low-consistency item.
        self.assertEqual(just_unc[0].fqn, "ll")

    def test_returns_fewer_than_five_when_sample_smaller(self):
        agg = []
        for fqn in ("a", "b"):
            for judge in ("gemini-pro", "opus", "gpt-5-4"):
                agg.append(_agg(fqn, "sub", judge, 70, 70))
        confs = {"a": 0.9, "b": 0.5}
        kinds = {"a": "sub", "b": "sub"}
        false_conf, just_unc = compute_outliers(_AGGREGATE_DECISION, agg, confs, kinds)
        self.assertEqual(len(false_conf), 2)
        self.assertEqual(len(just_unc), 2)


if __name__ == "__main__":
    unittest.main()
