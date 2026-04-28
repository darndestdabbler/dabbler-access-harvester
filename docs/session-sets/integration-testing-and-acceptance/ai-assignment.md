# AI Assignment — integration-testing-and-acceptance

## Session 1: End-to-End Test Consolidation

### Recommended orchestrator
anthropic claude-3-opus-20240229 @ effort=high

### Rationale
The session involves creating a new test project, authoring multiple end-to-end tests, and auditing existing coverage. High effort with a top-tier model ensures correct project setup and robust initial test implementations.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Analyze existing test projects (`AccessHarvester.Tests`, `AccessHarvester.Pipeline.Tests`) to audit current test coverage patterns and identify gaps in end-to-end pipeline validation. | Orchestrator-direct |
| 2 | Create a new xUnit test project named `AccessHarvester.Integration` within the `tests/` directory. | Orchestrator-direct |
| 3 | Add the new project to `AccessHarvester.sln` and add a project reference from `AccessHarvester.Integration` to the main `AccessHarvester` CLI project. | Orchestrator-direct |
| 4 | Author end-to-end tests in `AccessHarvester.Integration` for the primary CLI commands (`extract`, `translate`, `run`, `parse`, `generate-docs`) against the `NorthwindDevEdition.accdb` sample. Use `[SkippableFact]` for tests invoking the CLI to ensure COM dependency is handled correctly in CI. | Orchestrator-direct |
| 5 | Author a specific E2E test that executes the `run` command and validates the generated `ir/output.json` against the `docs/schema/harvester-schema.v1.0.json`. | Orchestrator-direct |
| 6 | Run `dotnet build && dotnet test` for the entire solution to confirm all existing and new tests pass. | Orchestrator-direct |
| 7 | Audit the `harvester-schema.v1.0.json` against all existing test coverage to identify schema properties not yet asserted by any test. | `route(task_type="schema-validation", engine="anthropic", model="claude-3-sonnet-20240229", complexity_hint=50)` |
| 8 | Verify session outputs, including the new test project and the schema coverage audit from the routed call. | `route(task_type="session-verification", complexity_hint=70)` |

### Actuals (Session 1 — filled after completion)
- Orchestrator used: claude-code (Claude Opus 4.7) @ effort=high.
- Total routed cost: $0.2783 (3 routed calls: 1 gemini-pro for ai-assignment authoring + schema-validation coverage audit, 2 gpt-5-4 for session-verification rounds 1 & 2). Plus $0.0026 for the next-orchestrator routed analysis below.
- Deviations from recommendation: the recommendation listed "claude-3-opus-20240229" — a stale model ID predating the repo's claude-opus-4-7 config. The actual orchestrator (Claude Opus 4.7) matches the spirit of the recommendation. Step 7 (`schema-validation` for coverage-vs-target audit) was added during execution to elaborate the spec's "Route schema-validation for coverage-vs-target correspondence" bullet.
- Notes for next-session calibration: Session 1 closed in 2 verification rounds. Round 1 surfaced 1 Major (silent-pass branch in EndToEndPipelineTests' Forms-directory check) and 2 Minor; all fixed; Round 2 returned VERIFIED. The Major was a real correctness hole that the test's defensive `Directory.Exists` guard concealed — the lesson is to make integration-test directory existence checks mandatory rather than defensive when the test's purpose IS to assert the directory exists. The schema-validation routed audit also surfaced systematic producing-set numbering errors in the original draft of coverage-audit.md (Critical + Majors), which were applied — confirming that mechanical-correspondence routing earns its keep.

**Next-session orchestrator recommendation (Session 2) — routed via task_type="analysis" 2026-04-28:**
gemini-code @ gemini-pro, effort=medium.
Rationale: Session 2 is mechanical scripting (sample-DB scrub + snapshot capture + CI wiring) with low novel-reasoning content; mid-tier at moderate effort is the most efficient choice.
Recommended addition to the routed-task list: a privacy/scrub audit step (the routed analysis suggested `task_type="data-privacy-audit"` but no such task type exists in router-config.yaml today — Session 2 should either route the audit as `task_type="security-review"` or handle in-conversation with explicit grep-based scrub verification).

## Session 2: Regression Suite + Corpus Expansion

### Recommended orchestrator
anthropic claude-3-opus-20240229 @ effort=high

### Rationale
This session requires careful file handling, running the harvester against new databases, and generating consistent snapshot files. A high-capability model minimizes the risk of errors in the mechanical but critical process of establishing the regression baseline.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Identify and commit 5-10 new, scrubbed `.accdb` samples to the `samples/` directory per the spec. | Orchestrator-direct (assuming manual sourcing/scrubbing) |
| 2 | For each new sample, author a test in `AccessHarvester.Integration` that runs the full `run` command. | Orchestrator-direct |
| 3 | Capture the initial JSON output for each new sample and commit it as a snapshot file under `tests/AccessHarvester.Integration/RegressionSnapshots/`. | Orchestrator-direct |
| 4 | Modify the tests from Step 2 to be snapshot regression tests: run the pipeline and assert the output matches the committed snapshot file. | Orchestrator-direct |
| 5 | Run `dotnet test` to confirm all new regression tests pass and that no existing tests were broken. | Orchestrator-direct |
| 6 | Verify session outputs, including the new samples and the passing regression suite. | `route(task_type="session-verification", complexity_hint=70)` |

### Actuals (filled after the session)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 3):**
anthropic claude-3-opus-20240229 @ effort=high
Rationale: Session 3 requires generating specialized `BenchmarkDotNet` project code, which benefits from a high-capability model.

## Session 3: Performance Benchmarks

### Recommended orchestrator
anthropic claude-3-opus-20240229 @ effort=high

### Rationale
Session requires setting up a `BenchmarkDotNet` project, writing correct benchmark harnesses, and establishing a valid baseline. A high-capability orchestrator is best for generating this specialized, boilerplate-heavy code correctly.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Create a new console application project `AccessHarvester.Benchmarks` under `tests/` and add the `BenchmarkDotNet` package. | Orchestrator-direct |
| 2 | Author a benchmark suite that measures the execution time and memory allocation of the full `run` command. | Orchestrator-direct |
| 3 | Parameterize the benchmark to run against a small (`MiniComplex.accdb`), medium (`NorthwindDevEdition.accdb`), and large (one of the new samples) database. | Orchestrator-direct |
| 4 | Execute the benchmarks to establish a performance baseline and commit the results summary to `docs/performance/baseline.md`. | Orchestrator-direct |
| 5 | Update the CI configuration to run the benchmarks and fail the build if key metrics regress by more than a set threshold (e.g., 15%) against the baseline. | Orchestrator-direct |
| 6 | Verify session outputs, particularly the new benchmark project and the CI integration. | `route(task_type="session-verification", complexity_hint=70)` |

### Actuals (filled after the session)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 4):**
anthropic claude-3-opus-20240229 @ effort=medium
Rationale: The orchestrator's main task is to package context for a single, high-stakes routed synthesis call, not iterative coding.

## Session 4: Acceptance Criteria Checkoff (Synthesis)

### Recommended orchestrator
anthropic claude-3-opus-20240229 @ effort=medium

### Rationale
The orchestrator's role is to gather extensive context from the entire project and previous sessions and then trigger a single, high-stakes routed call. The effort is medium as it's primarily context packaging, not iterative development.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Gather evidence: Collect test results, regression suite status, and performance benchmark reports from Sessions 1-3. | Orchestrator-direct |
| 2 | Gather context: Load the `docs/acceptance-criteria.md` template and the change-logs from all prerequisite session sets (#1-#17). | Orchestrator-direct |
| 3 | Synthesize the context and evidence into a comprehensive prompt for the synthesis task. | Orchestrator-direct |
| 4 | Produce the final, checked-off `docs/acceptance-criteria.md` by evaluating all evidence against each criterion. | `route(task_type="architecture", complexity_hint=75)` |
| 5 | Commit the generated `docs/acceptance-criteria.md`. | Orchestrator-direct |
| 6 | Verify the generated acceptance document for completeness and accuracy based on the provided evidence. | `route(task_type="session-verification", complexity_hint=70)` |

### Actuals (filled after the session)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 5):**
anthropic claude-3-opus-20240229 @ effort=high
Rationale: The final session combines potential code fixes with a major summarization task, requiring a top-tier model for both.

## Session 5: Verifier-Surfaced Cleanup + Final Change-Log

### Recommended orchestrator
anthropic claude-3-opus-20240229 @ effort=high

### Rationale
This session combines code fixes based on verifier feedback with a significant summarization task for the final v1 changelog. A high-effort, top-tier orchestrator is needed for both code quality and summarization quality.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Review and triage all verifier findings from Sessions 1-4. | Orchestrator-direct |
| 2 | Implement code changes to address any accepted findings. | Orchestrator-direct |
| 3 | Run all test suites (unit, integration, regression, benchmarks) to confirm fixes and the absence of new regressions. | Orchestrator-direct |
| 4 | Gather all `change-log.md` files from the entire project dependency tree (#1 through #18). | Orchestrator-direct |
| 5 | Generate the consolidated `CHANGELOG.md` for the v1 release, summarizing the entire project's accomplishments. | `route(task_type="summarization", max_tier=1)` |
| 6 | Review and commit the final `CHANGELOG.md`. | Orchestrator-direct |

### Actuals (filled after the session)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 6):**
N/A. This is the final session of the set.