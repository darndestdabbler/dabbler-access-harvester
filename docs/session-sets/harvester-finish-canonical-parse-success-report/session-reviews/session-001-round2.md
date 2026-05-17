1. **Round-1 Major #1 — `unresolvedItems` hardcoded zero/empty**

This is **functionally fixed**.

What changed is the right shape change:

- `UnresolvedItemsSection` now has an explicit `status`.
- Session 1 emits `"status": "not-measured"`.
- `totalCount` and `bySourceAndReason` are nullable and omitted when not measured.
- The regenerated JSON files now show the new shape correctly.
- The spec was updated to say consumers must check `status` instead of treating absence as zero.

That resolves the original semantic problem: the JSON no longer claims “measured zero unresolved items.”

The only residual issue here is **documentation/comment drift**, not data-shape drift: some code comments still talk about “zero items” or an “empty list,” and one spec section still has stale wording about null-vs-omitted behavior. The shipped JSON contract itself is no longer misleading.

---

2. **Round-1 Major #2 — ValidationRule / DefaultValue dialect mismatch**

This is **partially fixed, but not fully reliable end-to-end**.

The specific criteria-normalization fix is present:

- `NormaliseCriteria` exists.
- `ParseAsJetWhere` calls it.
- The new ValidationRule counts moved in the expected direction:
  - HHC validation rules now total 15, with 11 parsed / 4 failed.
  - Northwind validation rules now total 3, with 3 parsed / 0 failed.

So the exact false-negative class called out in Round 1 — implicit-operand criteria like `Is Not Null`, `Between ... And ...`, `In (...)` — is being addressed in code.

However, this fix depends on the property extractor actually handing those values to the parser, and that is where the remaining major problem sits: the shared regex used to read `Key: "value"` lines still cannot capture values containing escaped inner quotes. That means examples explicitly referenced by the new code/comments such as:

- `In (\"M\",\"F\",\"X\")`
- quoted string default values like `\"Pending\"`

can be skipped before `NormaliseCriteria` or `ParseDefaultValue` ever runs.

So the normalization logic itself is in place, but the end-to-end measurement for quoted criteria/default-value forms is still not trustworthy.

---

3. **Round-1 Major #3 — Count reconciliation with the inventory tool**

This is **not fully fixed** and remains the blocking issue.

The visible improvements are real:

- `Directory.EnumerateFiles(..., SearchOption.AllDirectories)` is now used.
- The report totals now reconcile numerically with the inventory totals.
- The cited totals line up exactly in the regenerated JSON.

But the reconciliation is currently achieved by making the report generator and the inventory tool share the **same fragile extractor**, not by making the extractor robust.

The critical bug is the field regex:

```csharp
@"^\s*(?<key>[A-Za-z]+)\s*:\s*""(?<value>[^""]*)""\s*$"
```

`(?<value>[^"]*)` cannot consume any embedded `"` characters, even when they are escaped in the YAML source. So a line like:

```yaml
ValidationRule: "In (\"M\",\"F\",\"X\")"
```

or

```yaml
DefaultValue: "\"Pending\""
```

will not match correctly.

That means:

- the inventory tool can miss real values,
- the report generator can miss the same real values,
- and the two outputs can still “reconcile” while both are wrong.

Because the property-sheet totals and parse outcomes are part of the golden artifact, this remains a **Major** issue. The interim golden is not yet trustworthy enough to pin.

---

4. **Round-1 Major #4 — Macro fields misleadingly named**

This is **fixed**.

The change is the correct rescoping:

- `actionsParsed` was renamed to `actionLines`.
- `actionsUnresolved` was dropped.
- The spec now describes macro coverage as structural counting only for Session 1.
- The regenerated JSON reflects that rescoping.

This removes the misleading implication that semantic macro parsing/enrichment occurred inside this harness.

No regression introduced here.

---

5. **Round-1 Minor #5 — `UPDATE_PARSE_SUCCESS_REPORTS` not wired**

This is **mostly fixed in code**.

The test harness now does the important part:

- default path compares generated JSON to committed golden,
- `UPDATE_PARSE_SUCCESS_REPORTS=1` regenerates,
- mismatch reporting includes a useful first-divergence message.

That resolves the substance of the original complaint.

There is still a **minor spec/comment drift** problem:

- the spec’s session narrative still contains stale text saying Session 2 flips diff mode,
- Session 1 prose still mentions write-and-pass behavior in places,
- and some nearby comments still describe pre-fix behavior.

So the code contract is now mostly correct, but the documentation is not yet perfectly aligned with the shipped behavior.

---

**Overall conclusion**

Three of the five Round-1 findings are effectively fixed (#1, #4, and the main part of #5). The criteria-normalization work for #2 is present, but its practical effect is undercut by the still-broken property extractor. Round-1 Major #3 therefore remains unresolved in a more subtle form: report and inventory agreement no longer proves correctness because both sides share the same quoting bug.

Because of that remaining extractor problem, I would **not** call the regenerated reports fit for commit as the interim golden yet.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs; tools/parse_success_inventory.py",
      "summary": "The shared property-field regex still cannot read YAML quoted scalars that contain escaped inner quotes, so quoted ValidationRule and DefaultValue values can be skipped while the inventory and report falsely appear reconciled.",
      "fix": "Replace the ad hoc `Key: \"value\"` regex with a shared escaped-scalar extractor or real YAML parsing helper that correctly handles embedded quotes, then regenerate both the inventory artifact and the parse-success reports."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-parse-success-report/spec.md; tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportModel.cs; tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs",
      "summary": "Some updated documentation/comments still describe pre-fix behavior for unresolvedItems and test-mode sequencing, leaving small internal contract drift after the code changes.",
      "fix": "Align the stale comments and session text with the shipped `unresolvedItems.status = \"not-measured\"` contract and the Session-1 env-var-driven diff/update behavior."
    }
  ]
}
```