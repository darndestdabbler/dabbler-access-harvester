# Verification Round 1

A. Determinism / byte-stability of emitted `DisplayTotals` + `Skipped`

The determinism story is mostly solid. `DisplayTotals` are sorted by `(Section, Control)` in `ReportAggregateComputer`, and the COM reader also sorts captured footer controls by the same shape before handing them downstream. `Skipped` is globally sorted at the end of `Compute()` by `(Kind, Detail)`, which is the right place to neutralize input-order and row-order effects. The tests around row/input-order invariance for `DisplayTotals` and `Skipped` are relevant and materially reduce risk here.

On numeric emission, the implementation keeps values as `decimal` and relies on `System.Text.Json`, so there is no culture-sensitive formatting path. Null row values are handled deterministically: any row whose expression evaluates to null is excluded from the `Sum`, and unsupported cases are routed to `Skipped` rather than emitted as nullable `ComputedValue`s. I do not see a byte-stability defect in the ordering/null-handling path itself for Session 1.

The only caveat is that there is no explicit decimal-scale normalization for `ComputedValue`; the code assumes “same input => same decimal representation,” which is generally fine here. I would not flag that as a defect given the current contract and the rest of the implementation.

B. Correctness of the closed-set grammar & evaluator

The overall design is good: a focused tokenizer/parser/evaluator for `Sum(<arith>)` is consistent with the corrected repo inventory and the closed-set survey. The leading-function-name classification is especially well thought out: it correctly classifies `IIf(...)`, `Avg(...)`, `Count(*)`, `Now()` as `DisplayTotalUnsupportedFunction` before tokenizing bodies that would otherwise create misleading parse errors. Case-insensitive column resolution is handled correctly. Null propagation and division-by-zero behavior are also aligned with the stated semantics: null argument => row excluded from the sum; divide by zero => row excluded.

There is, however, a real correctness/contract hole: numeric-literal parsing can throw out of `SumAggregateEvaluator.Evaluate` instead of returning a `SumAggregateUnsupported`. `ParseFactor()` calls `decimal.Parse(t.Text, ...)` directly, but `Evaluate()` only catches `ParseException` around parsing. That means malformed/oversized numeric tokens such as `=Sum(.)` or a huge literal can raise `FormatException`/`OverflowException` and escape the evaluator entirely. Since `ReportAggregateComputer.ComputeDisplayTotals()` does not catch those, a single unsupported footer expression can fail the entire aggregate capture instead of being routed to `Skipped` with `DisplayTotalParseError` or `DisplayTotalNumericOverflow`. That violates the explicit “unsupported, never silent / never guessed” contract and is more than theoretical because the tokenizer currently accepts `.` as a number token.

So on concern B: most of the grammar/evaluator behavior is correct, but this numeric-literal exception path is a substantive bug.

C. COM reader correctness

The section classification itself looks correct. `ReportFooter = 2`, page footer excluded, and even sections `>= 6` mapping to `GroupFooter{n}` is consistent with Access’s section numbering. Restricting to text boxes also matches the feature scope. RCW release discipline is generally reasonable: `report`, `controls`, `groupLevel`, and each iterated COM object are explicitly released, which is better than the average COM interop codebase.

The problem is failure isolation: `ReadFooterAggregates()` is too lossy. It catches any exception while enumerating `report.Controls` and returns an empty/partial list, and the per-property `TryRead*` helpers silently convert read failures into `null`/default paths that often just skip the control. That means a derivable report-footer total can disappear from both `DisplayTotals` and `Skipped` with no surfaced reason. For this feature, that is a contract-honesty problem: the whole point of the session is that derivable totals are computed and non-derivable ones are explicitly named. Silent omission is neither.

If the intent is “don’t fail the whole report on one COM hiccup,” the code still needs an explicit surfaced artifact for footer-read failures. As written, it can mask capture loss.

D. Contract honesty

Keeping `AggregateContract = "RecordSourceResultSet"` while adding `DisplayTotals` is defensible and matches the session’s stated decision. `GrandTotals` and `GroupLevels` semantics did not change, and the `ContractNote` was updated to describe the additive field. I would not flag the stable contract literal itself as dishonest.

The `Skipped` reasons are generally grounded and specific. `DisplayTotalGroupFooter`, `DisplayTotalRunningSum`, `DisplayTotalUnsupportedFunction`, `DisplayTotalNonAggregate`, etc. are concrete and useful. The big honesty problem is really the COM-reader silent-drop behavior discussed above, because omitted controls never become `Skipped` at all.

There are also two minor documentation/message rough edges: `ReportAggregateComputer`’s class comment still says it “does not evaluate Access expression syntax,” which is now stale for footer display totals, and the unsupported-function detail says “uses aggregate function 'Now'” even when the function is not an aggregate. Those are minor wording issues, not the main defect.

E. Test coverage adequacy

The hermetic coverage for `SumAggregateEvaluator` and `ReportAggregateComputer` is strong on the core happy paths and several negative cases. The rptInvoice derivable case is proven at compute level, and the negative routing cases for group footer, running sum, unsupported function, and non-aggregate are present. That part of the session is well covered.

Coverage is weaker in two places:

1. The malformed numeric-literal path described above is untested. There is no test for tokens like `.` or a too-large numeric literal, which is exactly where the evaluator currently escapes its “return unsupported, don’t throw” contract.
2. There is no automated coverage for the new COM reader behavior in `AccessReportSpecReader`: no test of report-footer vs. group-footer vs. page-footer classification, and no test around control-enumeration/property-read failure handling. The live `rptInvoice` capture is valuable proof of one success path, but it does not cover the asymmetrical risk areas introduced by this change.

So I would call test coverage adequate for the pure compute path, but not fully adequate for the COM capture path or for the evaluator’s malformed-number edge cases.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/ReportBaseline/SumAggregateEvaluator.cs",
      "summary": "Malformed or oversized numeric literals can throw out of Evaluate instead of returning a DisplayTotalParseError/DisplayTotalNumericOverflow result, which can fail the whole aggregate capture.",
      "fix": "Catch FormatException and OverflowException around numeric-literal parsing (or validate literals in the tokenizer) and map them to SumAggregateUnsupported; add tests for inputs like '=Sum(.)' and very large literals."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Com/AccessReportSpecReader.cs",
      "summary": "Footer control enumeration/property-read failures are swallowed into an empty or partial FooterAggregates list, allowing derivable display totals to be silently omitted from both DisplayTotals and Skipped.",
      "fix": "Do not silently convert Controls enumeration failures into success; either fail ReadSpec for footer-read errors or carry explicit footer-read-failure items through to output so omitted controls are surfaced honestly."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Tests/ReportBaseline",
      "summary": "Automated coverage does not exercise the malformed-numeric-literal evaluator path or the COM reader's section-classification/failure-isolation behavior introduced in this session.",
      "fix": "Add tests for malformed numeric literals in SumAggregateEvaluator and add targeted tests for AccessReportSpecReader footer section classification and read-failure handling, ideally by extracting pure helpers or a small COM abstraction seam."
    }
  ]
}
```

---

# Verification Round 2

I reviewed the three accepted R1 findings against the patched code and the fixes look correct, complete, and non-regressive.

For the numeric-literal crash in `SumAggregateEvaluator`, the important question was whether any malformed or oversized numeric token could still escape `Evaluate` as an uncaught runtime exception. The new `ParseFactor` implementation closes that hole. The tokenizer may still admit a syntactically number-like token such as `.` or an oversized decimal literal, but `decimal.TryParse(..., NumberStyles.Number, InvariantCulture, ...)` now converts those cases into an explicit `ParseException("invalid numeric literal ...")`. `Evaluate` already catches parser-side `ParseException` and maps it to `DisplayTotalParseError`, so the previously unhandled `FormatException`/`OverflowException` path is gone. I also checked for regressions around valid literals: the tokenizer grammar is unchanged, valid decimals still parse, and accumulator/runtime arithmetic overflow is still separately handled by the existing outer `catch (OverflowException)` as `DisplayTotalNumericOverflow`. This fix is sound.

For the swallowed COM `Controls` enumeration failure, the patch does exactly what the accepted finding required: it removes the broad catch around `ReadFooterAggregates`, while preserving only the intentionally soft per-property reads (`TryReadInt`, `TryReadString`) and RCW cleanup. That means a failure to get `report.Controls` or to structurally enumerate it now propagates out of `ReadSpec`, which in turn allows the baseline runner to classify the report as failed instead of fabricating an empty footer-aggregate set. I checked that there is no new resource-handling regression: `controls` is still released in `finally`, each yielded `control` is still released in its per-iteration `finally`, and the report itself is still closed in `ReadSpec`’s outer `finally` even when `ReadFooterAggregates` throws. The behavior now matches the intent described in the fix note.

For the missing hermetic section-classification tests, the extraction into `ReportFooterSection` is clean and the logic is correct for the documented Access section numbering: report footer `2`, page footer `4` excluded, and group footers as even indices `>= 6`. `AccessReportSpecReader` now delegates to this helper consistently for both inclusion and labeling, so the tested pure logic is the actual production logic. The new tests cover the key cases the original finding called out: detail/header exclusion, report footer inclusion, page footer exclusion, group footer inclusion, report-vs-group distinction, and label mapping. I do not see any behavioral regression from the extraction.

I also looked for collateral issues introduced by the fixes themselves and did not find any. In particular, I do not see a remaining silent-failure path for malformed numeric literals, I do not see the old swallowed-controls-enumeration behavior persisting anywhere in `ReadFooterAggregates`, and I do not see an inconsistency between the new pure section classifier and its call site.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
