# Cross-Provider Consistency + Confidence Calibration Report

> **Session set:** `vba-symbol-db` Session 6 of 7
> **Sample:** 42 stratified items from `tools/symbol-db/symbol-db.sqlite`
> (1 enum_decl, 1 macro, 3 queries, 6 functions, 7 module_vars, 8 subs,
> 8 enum_values, 8 lookup_tables)
> **Judges:** gemini-pro, opus, gpt-5-4 (3 judges × 42 items × 3 pairs = **378 grade rows**)
> **Routed grading cost:** ~$11.96 (within the $7-15 spec budget)
> **Companion files:**
> [`grades.sqlite`](grades.sqlite),
> [`correlation-scatter.png`](correlation-scatter.png),
> [`correlation-summary.json`](correlation-summary.json)

## Headline verdict: **split-dimensions, then recalibrate**

The two agreement dimensions cannot be aggregated into a single
consistency score (gemini-pro's intra-correlation is below the 0.30
non-trivial-magnitude threshold), so confidence must be calibrated
against each dimension separately.

When that calibration is performed, **self-reported confidence does
not predict cross-provider agreement on either dimension**:

| Dimension                                | Pearson r | Spearman r | Verdict |
|------------------------------------------|----------:|-----------:|---------|
| Confidence vs interpretation_agreement   |    +0.004 |     +0.113 | essentially zero |
| Confidence vs justification_agreement    |    -0.177 |     -0.230 | mildly **negative** |

Session 4's escalation thresholds (0.75 to opinion 2, 0.70 to opinion 3)
are not validated by this sample. The confidence column should be
**treated as untrustworthy** for downstream gating decisions until a
recalibration pass is run.

This is exactly the result the calibration was designed to surface.
The substrate is fine; the self-rating layer is not.

---

## Step 5 - Per-judge intra-correlation reduction

For each judge, computed Pearson r across 42 items between
`interpretation_agreement_pct` (averaged across the three pairs) and
`justification_agreement_pct` (likewise).

| Judge       | Pearson r |  Note      |
|-------------|----------:|------------|
| gemini-pro  |    +0.170 | near-zero  |
| opus        |    +0.496 | ok         |
| gpt-5-4     |    +0.443 | ok         |

**Decision: split path.** All three signs are positive, but gemini-pro
falls under the 0.30 magnitude threshold. The two dimensions are
treated as distinct and downstream analysis runs as two parallel
correlations rather than against an aggregated consistency score.

### Why the dimensions don't fully cohere

The judges agree directionally - more interpretation agreement does
seem to track more justification agreement - but at very different
strengths. The three judges' grading style (next section) suggests
gemini-pro decouples *what the procedure does* from *what evidence is
cited* in a way the other two don't. opus and gpt-5-4 both treat
"agreed on behavior + agreed on evidence" as a tight bundle.

A consistency-by-judge effect this large would have made an aggregated
score misleading; the spec's escape hatch was the right design.

---

## Step 6 - Confidence vs cross-provider agreement (split path)

### Headline correlations

Computed across all 42 items. Confidence is the mean of `Confidence1`,
`Confidence2`, `Confidence3` from the symbol DB; agreement is the
mean across the three pairs and the three judges.

| Dimension                                  | n  | Pearson r | Spearman r |
|--------------------------------------------|---:|----------:|-----------:|
| Confidence -> interpretation agreement     | 42 |    +0.004 |     +0.113 |
| Confidence -> justification  agreement     | 42 |    -0.177 |     -0.230 |

**Interpretation agreement** is essentially uncorrelated with
self-reported confidence. Pearson is at 0.4% which is statistically
indistinguishable from zero on n=42. The Spearman lift to 0.11 hints
at a very weak monotonic relationship - high-confidence items do
agree slightly more often than low-confidence items in *rank* terms -
but the magnitude is too small to be load-bearing.

**Justification agreement** is mildly *negatively* correlated with
confidence. Items rated more confidently by the original interpreters
cite slightly *less*-overlapping evidence than items rated less
confidently. The magnitude is small (-0.18) but the direction is the
opposite of what a calibrated confidence column would produce. A
plausible explanation: high-confidence interpretations lean on
shorthand and naming conventions ("the prefix says it all") and skip
explicit source-span citation, while lower-confidence interpretations
defensively over-cite. That's not a defect of the interpretations -
just a signal that confidence captures self-perceived obviousness, not
inter-rater convergence.

### Per-kind breakdown (split path - both dimensions)

Pearson r between mean confidence and mean agreement, within each
symbol kind, reported separately for the two split-path dimensions.
Sample sizes are small per stratum, so these should be read as
descriptive rather than inferential.

| Kind          | n | r (interp) | mean interp | r (justif) | mean justif | mean conf |
|---------------|--:|-----------:|------------:|-----------:|------------:|----------:|
| enum_decl     | 1 |        n/a |        89.8 |        n/a |        78.6 |      0.88 |
| enum_value    | 8 |     -0.160 |        86.0 |     -0.167 |        76.4 |      0.68 |
| function      | 6 |     +0.007 |        85.7 |     +0.055 |        79.5 |      0.95 |
| macro         | 1 |        n/a |        81.4 |        n/a |        85.9 |      0.93 |
| module_var    | 7 | **-0.729** |        82.1 | **-0.876** |        86.6 |      0.76 |
| query         | 3 |     +0.175 |        88.9 | **+0.967** |        78.9 |      0.97 |
| sub           | 8 |     +0.055 |        82.2 |     +0.125 |        73.0 |      0.94 |
| table         | 8 |     +0.214 |        83.1 |     -0.009 |        81.4 |      0.81 |

Several patterns line up across the two dimensions:

* **module_var** is the clearest single-stratum signal in the report
  on both dimensions: the *more* confidently the harvester rated a
  module variable, the *less* the providers agreed on either what it
  does (interp r = -0.73) or what evidence to cite (justif r = -0.88).
  With n=7 this is fragile, but the magnitudes are too large to
  dismiss as noise. Module vars whose names and types make their
  purpose look obvious in isolation (`m_strMsgCaption`,
  `m_lngCustomerOrderCount`) are exactly where lazy interpretations
  diverge across providers, because each provider invents a different
  plausible-sounding context.
* **query** flips between the two dimensions: confidence is roughly
  uncorrelated with interpretation agreement (r = +0.18) but very
  strongly correlated with justification agreement (r = +0.97 on n=3).
  Three points is too few to draw a real conclusion, but the direction
  is sensible: queries with structured SQL evidence are precisely the
  cases where high-confidence calls *should* line up with overlapping
  citations across providers.
* **table** moves in the opposite direction: a small positive on
  interpretation (r = +0.21) but essentially zero on justification
  (r = -0.01). The table prompts include normalized metadata that
  every provider grounds in the same way, which damps justification
  variance regardless of confidence.
* **enum_value, function, sub** are individually flat on both
  dimensions and their per-kind r values are small enough to be
  sampling noise.

The headline takeaway from this two-panel view: confidence either
fails to track agreement (most kinds), or it *anti*-tracks it
(module_var on both dimensions). Only `query` shows the kind of
positive co-movement on both dimensions that a calibrated confidence
column should produce, and that signal is fragile at n=3.

### Inter-rater reliability across the three judges

Krippendorff's alpha (interval) across the three judges, on per-item
mean scores:

| Dimension                       |   alpha  | Interpretation |
|---------------------------------|---------:|----------------|
| Interpretation agreement        |   +0.360 | weak agreement |
| Justification agreement         |   +0.199 | weak agreement |

Alpha values in the 0.2 - 0.4 range are typically considered weak
positive agreement. The judges agree directionally on which items the
providers converged on, but they disagree on magnitude often enough
that no single judge's grading should be taken as definitive.

This matters for the headline result: the modest alpha values mean we
shouldn't over-interpret the confidence/agreement zero. If the judges
disagreed strongly, we'd suspect rater noise was hiding a real signal.
Here the judges weakly agree, so a near-zero correlation across them
is more likely to reflect a real absence of signal than rater noise.

### Disagreement-type distribution

Across 126 (item, judge, pair_AB) labels per judge - 378 total
disagreement labels:

|              | convergent | scope |  framing | factual |
|--------------|-----------:|------:|---------:|--------:|
| **gemini-pro**  |   28 |    68 |       17 |      13 |
| **opus**        |   14 |    87 |       24 |       1 |
| **gpt-5-4**     |   42 |    66 |       14 |       4 |
| **Total**       |   84 |   221 |       55 |      18 |

Two notes worth flagging:

1. **Scope is the dominant disagreement type across all three judges.**
   Two-thirds of pairs that aren't convergent show one provider being
   materially broader or narrower than the other. This is consistent
   with what the per-kind module_var result suggests: providers don't
   often *contradict* each other, they vary on how much context they
   assume.
2. **Opus is meaningfully stricter than the other two judges**
   (14 convergent vs gpt-5-4's 42, and 1 factual disagreement vs
   gemini-pro's 13). For Session 7's three-provider probe, this is
   useful calibration: opus tends to score consistency lower at the
   margin. If a future session uses one of these models as a single
   judge, the choice will move the absolute number significantly.

### Outliers

Both rankings are unfiltered top-5 over the full 42-item sample.
False-confidence is ranked by `confidence * (1 - consistency_norm)`;
justified-uncertainty by `(1 - confidence) * (1 - consistency_norm)`.
Larger score in each ranking means more outlier-like.

**Top-5 false-confidence candidates** (high confidence, low interpretation
agreement - the harvester is sure but the providers disagree):

| FQN                                                       | Kind     | Conf | Interp |
|-----------------------------------------------------------|----------|-----:|-------:|
| `Form_frmCompanyDetail.cmdEmail_Click`                    | sub      | 0.95 |   68.2 |
| `Form_frmCompanyDetail.CompanyCantBeDeleted`              | sub      | 0.96 |   73.8 |
| `Employees`                                               | table    | 0.80 |   71.2 |
| `Form_frmCompanyDetail.cboCompanyTypeID_AfterUpdate`      | sub      | 0.90 |   78.1 |
| `modStrings.StringFormatSQL`                              | function | 0.96 |   80.9 |

These are the items where the symbol DB is most likely to mislead a
downstream consumer that trusts the confidence column: short event
handlers in `Form_frmCompanyDetail` whose names suggest behavior but
whose body is brittle enough that providers fill in the rest from
differing templates, plus the `Employees` lookup table (the harvester
is reasonably confident but providers disagree on whether it is
primarily a contact-info table or a HR/role table) and
`modStrings.StringFormatSQL` (a one-line helper whose specific role -
SQL escaping vs general string formatting - the providers split on).

**Top-5 justified-uncertainty candidates** (low confidence, low
interpretation agreement - the harvester is unsure and the providers
disagree):

| FQN                                                       | Kind        | Conf | Interp |
|-----------------------------------------------------------|-------------|-----:|-------:|
| `modStrings.enumStrings.sOneLineItem`                     | enum_value  | 0.55 |   81.3 |
| `SystemSettings`                                          | table       | 0.66 |   80.4 |
| `modStrings.enumStrings.sPOCannotDelete`                  | enum_value  | 0.69 |   78.9 |
| `Employees`                                               | table       | 0.80 |   71.2 |
| `modStrings.enumStrings.sCannotDelete`                    | enum_value  | 0.73 |   81.6 |

Most justified-uncertainty items in this sample are enum values whose
display text is opaque from the symbol name alone (`sOneLineItem`,
`sPOCannotDelete`) and one widely-used lookup table whose role mixes
configuration and audit data (`SystemSettings`). These are honest
"unsure" calls by the harvester that downstream tooling should
generally re-interpret with the actual referenced rows in scope rather
than relying on the symbol DB's interpretation.

`Employees` shows up in both rankings, which is the most informative
single item in the report: high confidence + low agreement makes it a
false-confidence candidate, but the agreement is low enough that it
also clears the justified-uncertainty bar. Its true label is closer to
"the harvester misjudged how much the table's role would split across
providers."

---

## What this means for downstream consumers

1. **Do not gate on confidence at the configured 0.75 / 0.70
   thresholds.** The thresholds were set in Session 4 as a heuristic;
   this session's data shows they don't earn their keep. Items above
   0.85 confidence are no more likely to be cross-provider-agreed-on
   than items at 0.65.
2. **Treat the symbol DB's confidence column as a *hint*, not a
   *gate*.** Useful for sorting interpretations by
   the harvester's perceived obviousness, not for deciding which
   interpretations are reliable enough to skip a second opinion.
3. **Module variables and short event handlers are the highest-risk
   stratum.** When migrating, prefer to re-interpret module_vars and
   short Form_*\_Click handlers across at least two providers, even
   when confidence is high. Their interpretations diverge most when
   the harvester thinks they're easiest.
4. **Tables and queries are the safest stratum.** Mean interpretation
   agreement at 83-89 across providers; the structured nature of the
   evidence (column lists, SQL) gives less room for divergence.

## What this means for the calibration program

The sensible follow-on is a small recalibration pass:

* **Refit the threshold.** Run a 0.65 / 0.55 / 0.45 confidence cutoff
  on a held-out sample and compare per-cutoff agreement against the
  current 0.75 / 0.70 numbers. The right threshold is whichever one
  separates "cross-provider-agreed" from "cross-provider-divergent" by
  the largest margin - which this sample suggests will require
  meaningful work to find, since the lift is small at every
  confidence level we observed.
* **Or replace confidence with a structural signal.** The per-kind
  table suggests kind + name-pattern features (event handler vs
  query, prefix conventions) carry more signal than confidence does.
  A 1-feature classifier on stratum alone may already beat the
  confidence column at sorting items by reinterpretation risk.
* **The Session 4 escalation logic is unchanged.** Even if confidence
  doesn't predict consistency, escalating to opinion 2 / 3 *does* give
  us the second/third opinion that lets downstream calibration
  happen at all. The pipeline shape is fine; the value of the
  threshold setting is what's in question.

## Methodology + caveats

* **Sample size is 42.** Per-stratum n is 1 to 8 with most strata at 6
  to 8; per-kind correlations are descriptive, not inferential. The
  full-DB n is 84, and a future scaling pass on all 84 items would
  shrink the headline confidence interval roughly 1.4x.
* **Anonymization is per-(item, judge), seeded deterministically by
  `sha256(fqn|judge)[:4]`.** Each judge sees the three interpretations
  in a different randomly-assigned letter order, which means no judge
  can learn which provider wrote which interpretation across the
  sample. Per-pair, the canonical opinion mapping is preserved in
  `GradeRawResponses.letter_to_opinion_json` for full reproducibility.
* **One transient gemini-pro provider error landed in the failure
  log** (`Form_frmCompanyDetail.CompanyConfirmDelete`); the item was
  successfully re-graded by gemini-pro on a follow-up pass (no
  duplicate row in `Grades` since the runner is idempotent).
* **The grading prompt asked judges to score all three pairs in a
  single call** rather than three separate calls per item. This costs
  call-count overhead but keeps the three pairs' ratings calibrated
  against a single context window of the three interpretations - a
  judge that is "harsh" on one pair tends to be similarly harsh on
  the other two pairs of the same item.
* **Krippendorff alpha is computed on the per-item mean across the
  three pairs**, treating the judge as the "rater" and the
  pair-averaged item score as the "value". Alternative computations
  (per-pair separate, full triple x triple x judge tensor) are
  defensible; this one is the most legible against a 42-item sample.

## Files produced this session

| Path                                                | Role |
|-----------------------------------------------------|------|
| [`grader.py`](grader.py)                            | Prompt builder + threaded runner + `grades.sqlite` schema |
| [`aggregator.py`](aggregator.py)                    | Per-judge intra-correlation + aggregate vs split decision |
| [`correlation.py`](correlation.py)                  | Confidence vs consistency, per-kind, IRR, outliers, scatter PNG, JSON summary |
| [`grades.sqlite`](grades.sqlite)                    | 378 grade rows + raw responses + run + failure tables |
| [`correlation-scatter.png`](correlation-scatter.png)| Two-panel scatter: confidence vs interp / vs justif (split path) |
| [`correlation-summary.json`](correlation-summary.json) | Compact JSON dump of all numbers in this report |
| [`tests/test_grader.py`](tests/test_grader.py)      | 18 hermetic unit tests for prompt + parser + persistence |
| [`tests/test_aggregator.py`](tests/test_aggregator.py) | Pearson/Spearman + decision-rule tests |
| [`tests/test_correlation.py`](tests/test_correlation.py) | Krippendorff alpha + headline + outliers + verdict |
