# Session 6 verification (round 1)

Verifier: gpt-5-4  cost=$0.2334

verifier model_id: gpt-5.4

---

1. **Grading rubric and prompt construction:** `grader.py` does implement the step-2/3 rubric as specified: for each of AB, BC, and AC it asks for two 0–100 agreement percentages, a `summary`, and a `disagreement_type` constrained to `{factual, scope, framing, convergent}`. The anonymization round trip is sound: a deterministic per-`(fqn, judge)` seed drives `anonymize()`, the prompt only exposes A/B/C, the parser reads AB/BC/AC, and `write_grades()` maps those back to canonical `1-2` / `2-3` / `1-3` pairs using the stored `letter_to_opinion` mapping. `GradeRawResponses` also persists both the seed and the mapping, so the round trip is reproducible. The “three judges for every sampled item” requirement is satisfied by the delivered artifacts rather than by one enforcing function in `grader.py`: `correlation-report.md` and `correlation-summary.json` both show 42 items per judge and 378 grade rows total, which is the expected full matrix.

2. **`grades.sqlite` schema and idempotency:** The schema is appropriate and slightly richer than the minimum spec: `Grades` holds the per-pair outputs, while `GradeRuns`, `GradeRawResponses`, and `GradeFailures` preserve run metadata and failure auditability. `already_graded()` correctly treats an item as complete only when three `Grades` rows exist for that `(item_fqn, judge_provider)`, so reruns after transient provider errors or parse failures will retry instead of skipping prematurely. The failure taxonomy is also mostly correct: normal paths distinguish `triplet_missing`, `provider_error`, and `parse_failure` exactly as the spec asks. The notable edge-case gap is in sequential `grade_one()`: if the retry call throws a non-transient exception, the function re-raises without first persisting a `provider_error`, so that branch loses the expected audit row.

3. **Per-judge intra-correlation:** `aggregator.py` implements the step-5 reduction rule correctly: it computes one Pearson `r` per judge across item-level means and chooses aggregation only when all three coefficients are positive and clear the magnitude threshold. The current delivered result is therefore handled correctly: gemini-pro at `+0.170` triggers the split path even though all three judges are positive, because it is below the configured `0.30` near-zero cutoff. Degenerate inputs are also handled defensibly: `pearson_r()` returns `None`, `compute_judge_correlation()` converts that to `NaN`, and `decide_aggregation()` forces the split path with a specific degenerate-correlation rationale. The only precision issue is explanatory text: an exact `0.0` coefficient currently falls into the “negative or zero” branch instead of the more accurate “near-zero” branch, so the rationale is not always as specific as the spec requested.

4. **Confidence vs. consistency correlation:** The headline correlation logic is correct for both paths, and the delivered numbers are internally consistent: on the split path, `compute_headline()` correlates mean confidence against `interpretation_score` and `justification_score` separately, matching the reported `+0.004` and `-0.177` Pearson values. The per-kind breakdown uses `consistency_score` on aggregate path and `interpretation_score` on split path, which matches the implementation and the way the markdown report is framed. The Krippendorff interval-alpha implementation itself is reasonable for sparse interval data: units with fewer than two raters are excluded from observed disagreement, and missing values are tolerated. The bug is in `compute_irr()`’s bookkeeping, not the alpha formula: `IRRResult.n_units` is documented as “items with >= 2 judges’ values present,” but the code increments it for any item with any judge present, so sparse reruns would misstate the IRR sample size.

5. **Outlier selection:** This is the clearest spec miss. The spec asks for top-5 false-confidence items and top-5 justified-uncertainty items, but `compute_outliers()` first applies hard thresholds (`>= 0.85`, `<= 0.65`, `<= 70.0`) that are not in the spec, and then returns up to five survivors from that filtered pool. That design is exactly why the delivered report only shows one false-confidence item and zero justified-uncertainty items: the code is not ranking the sample to produce five items, it is filtering and then truncating. On the split path it also silently defines “low consistency” as `interpretation_score` only; that is a plausible heuristic, but it should be explicit and justified if retained.

6. **Report deliverable:** `correlation-report.md` is otherwise strong and defensible. Its main conclusion—split the two agreement dimensions, then recalibrate confidence—is justified by the step-5 split decision and by the step-6 near-zero / negative headline correlations, and the numbers in the report line up with `correlation-summary.json`. The limitations section is candid about sample size, tiny per-kind counts, and judge-style differences, which is exactly what the spec wanted. The one report-level weakness is inherited from the code: the outlier section is presented as a top-5 analysis even though the implementation can emit fewer than five items.

7. **Hermetic Python tests:** The test suite covers most of the requested analytical surface: prompt shape, anonymization, parser behavior, Pearson/Spearman, aggregation-vs-split logic, headline correlation behavior, Krippendorff alpha basics, outliers, and verdict rendering. What it does not cover are the precise places where the implementation drifted from the spec: there is no sparse-data test asserting that `compute_irr().n_units` only counts items with at least two judges, and no test asserting that outlier selection returns an actual ranked top five rather than a threshold-filtered subset. There is also no test for the sequential retry branch in `grade_one()` that should log a second-call provider failure before re-raising. Those are the main gaps I would close before Session 7 depends on these analytics.

8. **Anything else:** Before commit, I would fix the outlier logic and the IRR `n_units` counting bug; both affect spec-visible outputs and are easy to correct. I would also patch the sequential grader retry path to always write a `provider_error` row before re-raising, and I would tighten the aggregation rationale so exact-zero coefficients are described as near-zero rather than as sign disagreement. One smaller cleanup worth doing now is naming: on split path, machine-readable `per_kind.mean_consistency` actually contains interpretation means, which is misleading for downstream consumers. I would defer any larger report-generation refactor unless Session 7 needs reproducible markdown synthesis, because the existing artifact is numerically coherent and the higher-priority fixes are analytical correctness issues.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "tools/symbol-db/consistency/correlation.py",
      "summary": "Outlier selection uses unspecced hard thresholds and can return fewer than the required top five false-confidence or justified-uncertainty items.",
      "fix": "Replace threshold filtering with an explicit ranking over all eligible items so the code always emits the top 5 items, and document how split-path outliers are scored."
    },
    {
      "severity": "Major",
      "location": "tools/symbol-db/consistency/correlation.py",
      "summary": "`compute_irr` miscounts `IRRResult.n_units` by including items with only one judge value even though the contract says items need at least two judges.",
      "fix": "Count a unit only when at least two judge values are present, and add a sparse-rater unit test that fails on the current behavior."
    },
    {
      "severity": "Minor",
      "location": "tools/symbol-db/consistency/grader.py",
      "summary": "The sequential retry path can raise a non-transient second-call provider error without persisting a `provider_error` failure row.",
      "fix": "Call `write_failure(..., reason='provider_error', ...)` in the retry exception branch before re-raising non-transient errors."
    },
    {
      "severity": "Minor",
      "location": "tools/symbol-db/consistency/aggregator.py",
      "summary": "Aggregation-decision rationale text conflates exact-zero coefficients with sign disagreement instead of distinguishing near-zero magnitude from negative sign.",
      "fix": "Split the rationale branches for NaN, negative sign, exact zero, and `|r| < threshold` so the reported reason matches the actual trigger."
    }
  ]
}
```

---

# Session 6 verification (round 2)

Verifier: gpt-5-4  cost=$0.1913


---

Fix 1 is correctly resolved. `compute_outliers()` in `correlation.py` now ranks the full eligible item set with the stated formulas, without any threshold gate, and slices to `top_n`. The false-confidence path uses `conf * (1 - consistency_norm)` and the justified-uncertainty path uses `(1 - conf) * (1 - consistency_norm)`, exactly matching the round-2 fix description. The new `TestOutliersTopN` coverage checks both the ranked top-5 behavior and the `< N` case, and the older ordering tests still verify that low-consistency items sort ahead of high-consistency items within each bucket. The report was updated accordingly: the outlier section now contains 5 false-confidence items and 5 justified-uncertainty items, not 1 and 0. Those lists also match `correlation-summary.json` numerically and in order after rounding.

Fix 2 is correctly resolved. `compute_irr()` now increments `n_units` only when an item has at least two judges present:
```python
present = sum(1 for j in judges if j in by_judge)
if present >= 2:
    n_units += 1
```
That directly fixes the single-judge overcount from round 1. The new `test_irr_n_units_excludes_single_judge_items` exercises the exact failure mode and passes. I do not see a regression in the alpha calculation itself: the function still builds sparse rows and leaves `krippendorff_alpha_interval()` to skip non-pairable units for observed disagreement, which is compatible with the intended missing-data handling.

Fix 3 is correctly resolved. In the sequential grader path, the retry exception branch inside `grade_one()` now calls:
```python
write_failure(grades_con, item, judge, "provider_error", detail=str(exc2), raw=raw_text)
```
before deciding whether to return on transient failure or re-raise on non-transient failure. That is the right ordering for audit persistence. The new `TestRetryFailureLogging` injects a non-transient second-call error and asserts that the `GradeFailures` row exists even though the exception propagates. That precisely covers the previously missing behavior. I also checked the threaded path: it already persists the `provider_error` before re-raising in the main thread, so round 2 does not introduce an inconsistency between sequential and concurrent execution.

Fix 4 is correctly resolved. `decide_aggregation()` now distinguishes four rationale classes instead of conflating them: degenerate/NaN (`constant input or too few items`), negative correlation, exact-zero correlation, and near-zero nonzero magnitude. The branching order is correct: negative is checked before exact zero, and exact zero before the generic near-zero bucket. The two new tests (`test_rationale_distinguishes_negative_from_near_zero` and `test_rationale_distinguishes_exact_zero`) cover the previously ambiguous cases, and the existing degenerate/split/aggregate tests still pass.

On the report itself, the round-2 outlier update is real, not cosmetic. `correlation-report.md` now lists 5 false-confidence items and 5 justified-uncertainty items, and those entries line up with `correlation-summary.json`. The headline correlations, per-judge reduction numbers, IRR values, and outlier values are internally consistent with the data shown in `correlation-summary.json`:
- per-judge intra-correlations round to `+0.170`, `+0.496`, `+0.443`;
- split-path headline correlations round to `+0.004 / +0.113` and `-0.177 / -0.230`;
- IRR rounds to `+0.360` and `+0.199` with `n_units = 42`;
- the outlier tables match the JSON entries after rounding.
The report’s narrative conclusion is also directionally consistent with those numbers: split path is required, and the observed split-path correlations do not validate the Session 4 thresholds.

One additional issue remains outside the four round-1 fixes. In split mode, the implementation and report still provide only a single per-kind breakdown based on `interpretation_score`. `compute_per_kind()` selects:
```python
score_attr = "consistency_score" if decision.aggregate_path else "interpretation_score"
```
and never emits a justification-side per-kind analysis. `correlation-report.md` mirrors that by showing only “Pearson r between mean confidence and mean interpretation agreement” per kind. The spec’s split-path requirement is two parallel analyses—interpretation and justification—and “per-kind breakdown either way” implies the per-kind section should stay parallel too. So the headline split-path analysis is complete, but the per-kind split-path analysis is still only half-implemented.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "tools/symbol-db/consistency/correlation.py::compute_per_kind; tools/symbol-db/consistency/correlation-report.md (Per-kind breakdown)",
      "summary": "Split-path per-kind analysis still reports only interpretation-agreement and omits the required justification-agreement per-kind breakdown.",
      "fix": "In split mode, compute and render two per-kind correlation series side by side (interpretation and justification), update correlation-summary.json accordingly, and add tests that assert both dimensions are present on the split path."
    }
  ]
}
```

---

# Session 6 verification (round 3)

Verifier: gpt-5-4  cost=$0.0615


---

The round-2 Minor finding is fixed. In `correlation.py`, the split-path per-kind computation now emits two parallel series per kind: interpretation in the existing `pearson_r` / `mean_consistency` fields and justification in `pearson_r_secondary` / `mean_consistency_secondary`, while the aggregate path correctly leaves the secondary fields unset. `_summary_json()` serializes those fields, and the checked-in `correlation-summary.json` contains both per-kind dimensions for split mode.

The presentation layer is also repaired: `correlation-report.md` now shows both interpretation and justification columns side by side in the per-kind table and discusses the cross-dimension patterns explicitly. The new tests cover both the aggregate-path `None` behavior and split-path dual-emission behavior, so the original omission is both corrected and regression-protected.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```