# AI Assignment Ledger -- vba-grammar-validation

> **Authored:** 2026-04-26 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals"
> block under each session row once it completes, and updates the
> *next* session's row if the actuals reveal a miscalibration.

## Session 1 of 4: Edge-Case Fixture Authoring
*   **Recommended orchestrator:** `engine: claude-code, model: sonnet-4.6, effort: low`
*   **Rationale:** The session's primary work is dispatching small, mechanical `test-generation` calls and managing file I/O, which is cost-effective and well-suited to a low-effort Sonnet orchestrator. Higher-tier models are not required for these orchestration and file management tasks.
*   **Estimated routed cost:** ~$0.50

| Step # | Type       | `route()` Task Type      | Model   | Effort | Rationale                                                              |
| :----- | :--------- | :----------------------- | :------ | :----- | :--------------------------------------------------------------------- |
| 1      | Mechanical | --                       | Sonnet  | 5      | Update internal session state; a simple file I/O operation.            |
| 2      | Mechanical | --                       | Sonnet  | 5      | Read and parse hazard list to identify uncovered test cases.           |
| 3      | Reasoning  | `test-generation`        | Sonnet  | 40     | Generate paired `.bas` and `.expected.json` fixtures per hazard.       |
| 4      | Mechanical | --                       | Sonnet  | 5      | Land newly generated fixture files into the `docs/test-cases/edge/` dir. |
| 5      | Mechanical | --                       | Sonnet  | 10     | Execute build and test scripts; parse output for success/failure.      |
| 6      | Mechanical | `session-verification`   | Opus    | 70     | Dispatch to cross-provider for quality assurance on new fixtures.      |
| 7      | Reasoning  | `analysis`               | Sonnet  | 15     | Analyze session outcome to recommend next session's orchestrator config. |
| 8      | Mechanical | --                       | Sonnet  | 5      | Git commit, push, and send completion notification.                    |

## Session 2 of 4: Grammar Fix Loop (Synthesis)
*   **Recommended orchestrator:** `engine: claude-code, model: opus-4.7, effort: medium`
*   **Rationale:** This session requires a single, high-leverage synthesis call to resolve multiple grammar bugs and ambiguities; using an Opus orchestrator minimizes implementation errors when applying the architectural guidance. The higher reasoning capability is necessary to manage the iterative fix-and-test loop effectively.
*   **Estimated routed cost:** ~$0.60

| Step # | Type       | `route()` Task Type      | Model   | Effort | Rationale                                                                        |
| :----- | :--------- | :----------------------- | :------ | :----- | :------------------------------------------------------------------------------- |
| 1      | Mechanical | --                       | Opus    | 15     | Run the full test harness against the corpus, capturing all parse failures.      |
| 2      | Reasoning  | --                       | Opus    | 30     | Classify all failures and aggregate them into a structured prompt for synthesis. |
| 3      | Mechanical | `architecture`           | Opus    | 80     | Dispatch aggregated failure context for a single, coherent grammar fix design.   |
| 4      | Reasoning  | --                       | Opus    | 50     | Interpret synthesis output and meticulously implement changes in `Vba.g4`.       |
| 5      | Mechanical | --                       | Opus    | 10     | Re-run the harness iteratively to validate fixes until 100% pass rate.           |
| 6      | Mechanical | --                       | Opus    | 10     | Execute final build and test scripts for verification.                           |
| 7      | Mechanical | `session-verification`   | Sonnet  | 70     | Dispatch to cross-provider (Sonnet) for quality assurance on grammar changes.    |
| 8      | Reasoning  | `analysis`               | Opus    | 15     | Analyze session outcome to recommend next session's orchestrator config.         |
| 9      | Mechanical | --                       | Opus    | 5      | Git commit, push, and send completion notification.                              |

## Session 3 of 4: Parse-Failure Log + Cross-Cut
*   **Recommended orchestrator:** `engine: claude-code, model: sonnet-4.6, effort: low`
*   **Rationale:** This session is administrative, focusing on documentation, mechanical validation, and summarization, all of which are routine tasks well within Sonnet's capabilities. Using a higher-tier model would be underutilized and not cost-effective for these structured I/O and dispatch operations.
*   **Estimated routed cost:** ~$0.25

| Step # | Type       | `route()` Task Type      | Model   | Effort | Rationale                                                               |
| :----- | :--------- | :----------------------- | :------ | :----- | :---------------------------------------------------------------------- |
| 1      | Reasoning  | --                       | Sonnet  | 30     | Consolidate all failures, classifications, and resolutions into the log.  |
| 2      | Mechanical | `schema-validation`      | Sonnet  | 30     | Route log for mechanical verification against git history to confirm fixes. |
| 3      | Mechanical | --                       | Sonnet  | 10     | Execute final build and test scripts as a last sanity check.            |
| 4      | Mechanical | `session-verification`   | Opus    | 70     | Dispatch to cross-provider for final sign-off on all session set artifacts. |
| 5      | Mechanical | `summarization`          | Sonnet  | 20     | Route for `change-log.md` generation and next session set recommendation. |
| 6      | Mechanical | --                       | Sonnet  | 5      | Git commit, push, and send completion notification.                     |
| 7      | Reasoning  | --                       | Sonnet  | 10     | Perform internal review of file/code organization post-changes.         |

## Session 4 of 4: Verifier-Surfaced Cleanup (optional buffer)
*   **Recommended orchestrator:** `engine: claude-code, model: sonnet-4.6, effort: low`
*   **Rationale:** As a buffer session for minor, well-defined fixes identified by verifiers, the work is mechanical code and documentation editing. A low-effort Sonnet orchestrator is the most efficient and cost-effective choice for implementing these targeted changes.
*   **Estimated routed cost:** ~$0.15

| Step # | Type       | `route()` Task Type      | Model   | Effort | Rationale                                                                  |
| :----- | :--------- | :----------------------- | :------ | :----- | :------------------------------------------------------------------------- |
| 1      | Mechanical | --                       | Sonnet  | 10     | Ingest and parse structured verifier findings from prior sessions.         |
| 2      | Reasoning  | --                       | Sonnet  | 25     | Implement the specific, targeted code or documentation fixes as described. |
| 3      | Mechanical | --                       | Sonnet  | 10     | Execute build and test scripts to validate the applied fixes.              |
| 4      | Mechanical | `session-verification`   | Opus    | 70     | Dispatch to cross-provider for final sign-off on the cleanup work.         |
| 5      | Mechanical | --                       | Sonnet  | 5      | Git commit, push, and send completion notification.                        |

## Session 1 Actuals (added post-completion)

**Orchestrator used:** claude-code claude-sonnet-4-6 @ effort=unknown (low)
**Verification verdict:** VERIFIED (after 3 rounds: initial + 2 fix-and-reverify)
**Total routed cost:** ~$0.40 (analysis $0.02 + 5x test-generation $0.08 + verification $0.14 + re-verify $0.11 + re-verify-r3 $0.03 + next-orch $0.02)
**Deviations from recommendation:** None material. Low effort was appropriate for fixture authoring.
**Notes for Session 2:** Verifier found 6 issues in initial round (4 Major: fixture minimalism + source-span mismatches; 1 Minor: logic error in H-D04; 1 Info: no automated harness). All fixed by round 3. Session 2 will run the grammar harness against 17 fixtures (12 module-level + 5 edge) and likely find failures to fix.

**Next session orchestrator recommendation (Session 2):**
claude-code opus-4.7 @ effort=medium

**Rationale:**
Session 2 is a synthesis-heavy session requiring non-trivial ANTLR4 grammar fixes, which aligns with the original high-capability model recommendation. Session 1 actuals confirmed that a weaker orchestrator produced multiple major defects even on a simpler authoring task, making a high-capability model like Opus necessary to correctly resolve complex parse failures and avoid verification churn.

## Session 2 Actuals (added post-completion)

**Orchestrator used:** claude-code claude-sonnet-4-6 @ effort=unknown (Opus was recommended; Sonnet was used)
**Verification verdict:** VERIFIED (2 rounds: round-1 found 3 minor/advisory findings; round-2 confirmed all fixed)
**Total routed cost:** ~$0.11 (verification $0.08 + re-verify $0.03 + next-orch $0.00)
**Deviations from recommendation:** Used Sonnet instead of Opus. Session was simpler than expected: only 1 grammar failure (trailing COLON before hidden REM comment in `singleLineStatementList`) — a trivially mechanical 1-line grammar fix. No `architecture` route call was needed. Created `VbaCorpusParserTests.cs` (15 VBA-module fixture harness). All 15 fixtures parse to 100%.
**Notes for Session 3:** Parse-failure log authoring + schema-validation cross-cut. Administrative documentation; no synthesis required.

**Next session orchestrator recommendation (Session 3):**
claude-code sonnet-4.6 @ effort=low

**Rationale:**
Session 3 specification correctly identifies the work as administrative documentation and mechanical validation, tasks well within Sonnet's capabilities. Session 2 actuals further support this — Sonnet successfully completed a task originally scoped for Opus, proving its sufficiency for low-complexity work.
