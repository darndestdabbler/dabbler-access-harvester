## Executive Summary

The completion of both P1a (008) and P1b (009) clears the path for the next prioritized work item. Based on the established 006 review ranking, P2, "output-contract-fidelity-batch," is the correct next recommendation. The set 010-output-contract-fidelity-batch has already been authored in `spec.md` and a `not-started` state stub, confirming its readiness. This session set will address several critical issues related to the fidelity of emitted artifacts, necessitating a coordinated golden regeneration at its conclusion.

## Analysis

The 006 review ranking clearly establishes the sequence P0 -> P1a || P1b -> P2 -> P3 -> P4. With P1a (008) and P1b (009) now completed, the immediate successor in the sequence is P2. The pre-authored set 010-output-contract-fidelity-batch directly corresponds to the P2 findings described in the context.

The P2 set addresses several significant findings, including:
*   **F-2-1 Major DAO query-type decode shift / crosstabs mislabeled MakeTable:** This is a critical fidelity issue impacting how DAO query types are represented. The cross-check with F-3-7 suggests a parser interaction.
*   **F-3-5 Major explicit ByVal never emitted:** A fundamental correctness issue if `ByVal` is expected but not emitted, potentially affecting marshaling or calling conventions in generated code.
*   **F-2-3 BOM on 4 usage/cleanup JSON:** BOM issues can cause parsing problems in downstream consumers.
*   **F-2-6 label casing:** Inconsistency in casing can break integrations or lead to unexpected behavior.
*   **F-2-4 implement-or-strike:** Indicates unaddressed contract items that need resolution.
*   **F-2-5/F-2-7/F-1-4/F-1-7 minors:** Smaller but important fixes for overall robustness.
*   **O-1-2 name-form doc; O-1-1 interim note:** Documentation and organizational improvements.

The context explicitly states that P2 "CHANGES shipped output," which mandates a single, coordinated HomeHealthCare golden regen at the end of the set. This is a crucial aspect of the session's execution and verification strategy.

Therefore, proceeding with 010-output-contract-fidelity-batch is not only aligned with the defined priorities but also necessary to improve the quality and correctness of the harvester's output.

## Recommended Next Session Set

*   **Identifier:** 010-output-contract-fidelity-batch
*   **Classification:** P2 (Output Contract Fidelity)
*   **Estimated Size:** 2 sessions
*   **Scope bullets:**
    *   Address F-2-1: Correctly decode DAO query types, resolving the `MakeTable` mislabeling for crosstabs, potentially requiring coordination with F-3-7 parser cross-check.
    *   Address F-3-5: Ensure explicit `ByVal` is emitted where required.
    *   Address F-2-3: Fix BOM presence on the 4 identified usage/cleanup JSON files.
    *   Address F-2-6: Standardize label casing across relevant outputs.
    *   Address F-2-4: Resolve "implement-or-strike" findings by either implementing the missing contract items or explicitly striking them with justification.
    *   Address F-2-5, F-2-7, F-1-4, F-1-7: Implement fixes for minor findings.
    *   Address O-1-2 and O-1-1: Incorporate name-form documentation and interim notes.
    *   Execute a single, coordinated golden regeneration of HomeHealthCare artifacts after all behavioral changes are implemented.
*   **Hard Constraints:**
    *   NEVER regenerate, recreate, or commit examples/northwind-output.json or any NorthwindDevEdition-derived artifact (regeneration reintroduces residue sets 003-005 removed; Northwind is proprietary/local-only). All fixtures/goldens derive from committed HomeHealthCare artifacts only.
    *   Golden regeneration is HomeHealthCare-only via samples/GoldenOutput/HomeHealthCare/; regenerate once, after the session's behavioral changes, then re-run CanonicalGoldenRegressionTests. For P2 a single coordinated regen at the END of Session 2 is expected (emission changes alter shipped output); every golden delta must map to a named finding's fix.
    *   COM-tier runs are deliberate, sequential, foreground; TRX logger on, TRX <Counters> as completion signal; never background a dotnet invocation; sweep spurious samples/*.accdb diffs (git checkout -- samples/*.accdb) before staging.
    *   The quarantined sanitize test stays quarantined.
    *   Do not weaken assertions or tests to force green; intent changes require explicit disclosure in the verification prompt.

## Recommended Orchestrator

*   **Engine/Model Class:** `DotNetTestDrivenDevelopment`
*   **Rationale:** The nature of the findings in P2 (output contract fidelity) involves precise changes to output generation logic and internal decoding. `DotNetTestDrivenDevelopment` is the most appropriate orchestrator due to its emphasis on:
    1.  **Precise Regression:** Changes to output behavior can have broad impacts. TDD encourages writing specific tests to isolate and verify each fix (e.g., a test for F-2-1's correct DAO query type decoding, a test for `ByVal` emission).
    2.  **Iterative Refinement:** While the set is 2 sessions, individual findings can be tackled with a red-green-refactor cycle.
    3.  **Verification of Golden Output:** The ultimate verification for this set is the HomeHealthCare golden regeneration. TDD complements this by ensuring individual behavioral changes are correct before the final golden output comparison.
    4.  **Minimizing Unintended Side Effects:** By focusing on individual findings with dedicated tests, the risk of introducing new regressions in other parts of the output contract is reduced.