# Change Log: `harvester-finish-canonical-parse-success-report`

> **Created:** 2026-05-17
> **Sessions Ran:** 2 (Session 1 + Session 2, both 2026-05-17)
> **Total Routed Cost:** ~$1.02 (Session 1: ~$0.58, Session 2: ~$0.44)
> **Verification Verdict:** VERIFIED-after-fix (both sessions; 3 verification rounds each against gpt-5-4, all findings resolved in-session)
> **Prerequisite For Next Set:** `harvester-finish-canonical-golden-output`

## Summary

This two-session set delivered the canonical parse-success reports for the `HomeHealthCare.accdb` and `NorthwindDevEdition.accdb` fixtures. It established the reporting harness, generated initial golden JSON files, and implemented a robust diff-gate for regression testing. The set also produced a comprehensive narrative detailing the meaning and suggested next steps for all `UnresolvedItem` reason codes currently emitted by the harvester, which is a critical piece of documentation for downstream AI consumers.

A key outcome was the explicit verification of parser coverage for VBA, Jet SQL, and a scoped subset of property-sheet expressions. The findings revealed specific grammar gaps (e.g., VBA `Attribute VB_*` statements) and the need for a heuristic to handle Access criteria-style expressions within Jet SQL parsing contexts, informing future strategy. This set significantly advances the project towards meeting Phase 4 Criterion #1.

## Deliverables

*   `docs/session-sets/harvester-finish-canonical-parse-success-report/spec.md`: Elaborated spec for canonical parse-success reports.
*   `docs/session-sets/harvester-finish-canonical-parse-success-report/property-sheet-inventory.md`: Inventory of property-sheet expressions per fixture, informing parser scope.
*   `samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json`: Canonical parse-success report for HHC.
*   `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`: Canonical parse-success report for Northwind.
*   `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs`: The generator/harness for producing parse-success reports.
*   `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportModel.cs`: C# model for the parse-success report JSON.
*   `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportTests.cs`: Diff gate for parse-success reports, using `UPDATE_PARSE_SUCCESS_REPORTS=1` for regeneration.
*   `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiff.cs`: Helper class for byte-identical diffing.
*   `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiffTests.cs`: Sentinel test for the diffing logic.
*   `docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`: Per-reason narrative explaining `UnresolvedItem` codes and suggested next steps.
*   `tools/parse_success_inventory.py`: Tool used for property-sheet inventory.

## Phase 4 Criterion #1 status

This set delivers the **verifiable artifact** for Phase 4 Criterion #1 from `docs/planning/strategy.md` ("every documented VBA / Jet SQL / property-sheet construct in the canonical fixtures parses correctly OR emits an explicit `UnresolvedItem` with provenance and reason") — committed per-fixture parse-success reports, a live diff-gate that fails on parse-coverage regression, and a per-reason narrative that documents the full `UnresolvedItem` reason set the aggregator emits.

What is **not** yet done in this set, and is intentionally deferred to F2 (`harvester-finish-canonical-golden-output`): the reports' `unresolvedItems` section currently ships `"status": "not-measured"` because the live `UnresolvedItemAggregator` requires the full enrichment pipeline (resolver / dynamic-SQL / navigation / function-classification) that lives in F2. The report shape accommodates the live measurements stably — F2 fills `unresolvedItems.bySourceAndReason` against the same JSON schema. Criterion #1 is therefore **structurally closed** by this set (artifact + gate + narrative) and **fully closed** once F2 flips `status` to `"measured"`.

Surfaced for human attention (raised in Session 1's notification, not folded into Phase 4 closure work): the high VBA `parseFailed` rates (HHC 29/32, Northwind 52/66) driven by form/report code-behind `Attribute VB_*` lines the `Vba.g4` grammar does not handle. Strategy-level decision per the spec's stop-rules.

## Per-session summary

### Session 1

**Work:** Elaborated the stub spec into a full execution plan, performed the property-sheet inventory pass, implemented the YAML-mirror-driven first-pass report generator, and committed interim-golden JSONs. The diff gate was made live with a regeneration toggle.
**Verification Outcome:** VERIFIED-after-fix (3 rounds against GPT-5.4; R1 4 Major + 1 Minor, R2 1 Major + 1 Minor, R3 1 Minor; all resolved in-session).
**Routed Cost:** ~$0.58 (gemini-pro ai-assignment $0.011; gpt-5-4 verification rounds $0.182 + $0.182 + $0.100).

### Session 2

**Work:** Reconfirmed the diff gate, authored `unresolved-reason-narrative.md` covering all emitted `UnresolvedItem` reasons, extracted and tested the `ParseSuccessReportDiff` helper, and added a sentinel test for the diff logic.
**Verification Outcome:** VERIFIED-after-fix (3 rounds against GPT-5.4, 2 Major + 1 Minor issue in R1; 1 Minor issue in R2; 1 Minor issue in R3, all narrative-only and resolved in-session).
**Routed Cost:** ~$0.44 (gpt-5-4 verification rounds $0.189 + $0.107 + $0.128; gemini-pro next-set analysis $0.015; gemini-flash change-log summarization $0.002).

## Findings that may interest the human

*   **VBA `Attribute VB_*` Parser Gap:** The Home Health Care (HHC) fixture showed 29/32 VBA modules `parseFailed`, and Northwind 52/66, primarily due to `Attribute VB_GlobalNameSpace = "..."` lines in form/report code-behind. The `Vba.g4` grammar currently does not handle these `Attribute` statements. This is a genuine parser-scope gap requiring a strategy-level decision regarding stub stop-rules, as it's not folded into Phase 4 parser development.
*   **Property-Sheet Wrapping Heuristic:** For criteria-style property-sheet values (e.g., `ValidationRule`, `DefaultValue`), a Jet SQL wrapping heuristic was developed. It prepends a synthetic operand (e.g., `x `) to expressions that start with criteria keywords (Is, Like, In, Between, Not, or an operator) before wrapping them in a `SELECT 1 FROM t WHERE ...` SQL fragment. This allows Jet SQL parsing to evaluate the expression.

## Lessons learned candidates

*   **"orchestrator-model-matches-routing-target" Convention Confirmed:** The narrative for `unresolved-reason-narrative.md` was authored effectively by in-conversation Opus, aligning with the convention that an orchestrator model should match the routing target for synthesis tasks.
*   **"Persist Routed Output To Disk Before Any Display" Convention Confirmed:** All routed scripts, including the parse-success reports, were persisted to disk before any display or further processing, confirming this convention.
*   **"Write disposition.json before closeout commit" Convention Confirmed:** Both Session 1 and Session 2 followed this convention, ensuring that the `disposition.json` artifact accurately reflected the session's outcome and state before the final commit.

## Handoff to next set

Please refer to `docs/session-sets/harvester-finish-canonical-parse-success-report/next-session-set-recommendation.md` for the detailed handoff to the next session set, `harvester-finish-canonical-golden-output` (Phase 4, F2).