### Executive Summary

The next session set must be `P0 integration-test-rebase`. It is a hard prerequisite for all other high-priority correctness work identified in set 006. The current red state of the integration suite (7 failing tests) makes it impossible to validate any fix that alters golden file outputs, which includes the critical and major bugs found in the parser, manifest pipeline, and output contracts. This set unblocks the entire repair-and-enhance roadmap.

### Analysis

The discovery work in set 006 successfully identified several critical and major correctness defects (F-3-1, F-1-1, F-2-1, F-3-2, F-3-4). However, fixing any of these defects will necessarily change the generated golden file outputs. The existing integration test suite is the sole mechanism for verifying these changes and preventing regressions. With 7 tests failing due to their dependency on the now-removed proprietary Northwind fixture, there is no stable baseline for verification.

Attempting any other set first (e.g., P1a `manifest-vba-ingestion-and-parser-hardening`) would be unverifiable. The orchestrator would be forced to work against a broken test harness, unable to distinguish between expected changes, unexpected changes, and pre-existing failures.

Therefore, `P0 integration-test-rebase` is the mandatory first step. It restores the project's core quality gate, enabling all subsequent work to be performed safely and with high confidence. This action directly respects the repo constraint to fix the suite by re-basing onto the `HomeHealthCare` fixture, not by regenerating forbidden artifacts.

### Recommended Next Session Set

*   **Identifier**: `007-integration-test-rebase`
*   **Classification**: `Tooling / Correctness`
*   **Estimated Size**: 1-2 sessions
*   **Scope**:
    *   Identify the 7 failing integration tests that previously asserted against `examples/northwind-output.json`.
    *   Analyze the assertion logic and update it to target the corresponding, correct data within the committed `HomeHealthCare` golden fixture.
    *   Ensure all tests are updated to load and use the `HomeHealthCare` fixture exclusively.
    *   Verify the full integration test suite passes (86/86).
    *   **Constraint**: This set must NOT regenerate or modify `examples/northwind-output.json` in any way.
*   **Goal**: Restore the integration test suite to a 100% passing state to provide a stable verification baseline for all subsequent bug fixes.

### Recommended Orchestrator

*   **Engine/Model Class**: `Claude Fable 5`
*   **Rationale**: This is a precise, technically demanding task requiring careful analysis of test code, assertion logic, and fixture data. As the designated frontier model for this repository (per set 006), Claude Fable 5 has demonstrated the required context awareness and analytical capability. Maintaining model consistency reduces variability while tackling the single most critical-path item on the roadmap.