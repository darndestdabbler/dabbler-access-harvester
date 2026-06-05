# AI Assignment: `harvester-report-baseline-capture`

This assignment guides the three sessions to implement a new harvester capability for capturing Access report baselines (rendered PDF + query aggregates). This eliminates the final manual step in the harvest-to-migrate pipeline, enabling fully automated parity checks for downstream consumers.

---

## Session 1: Aggregates from the report's RecordSource query
### Recommended orchestrator
`claude-opus-4-7 @ effort=medium`

### Rationale
This session establishes the core data-extraction logic and output contract for report aggregates. It involves database interaction via ACE OLEDB, data processing, and authoring hermetic tests, justifying a capable model for the initial implementation of the feature's data layer. This recommendation reflects the model already used for the session's boundary write.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1.1  | In a new Core-layer service, resolve each report's `RecordSource` (saved query or inline SQL) from the input IR. | `orchestrator` |
| 1.2  | Using the existing ACE OLEDB connector, execute the `RecordSource` query and load the result set. | `orchestrator` |
| 1.3  | Implement logic to compute aggregates from the result set: total row count, subtotals for each group level, and a grand total. | `orchestrator` |
| 1.4  | Emit an `aggregates.json` file containing the computed aggregates and provenance data (parameters used, harvester version, RecordSource shape). Explicitly include a note that aggregates reflect the RecordSource result set, not rendered footer controls. | `orchestrator` |
| 1.5  | Author hermetic xUnit tests that provide a fixture query result (`DataTable`) and assert the correctness of the aggregate computation logic. | `orchestrator` |
| 1.6  | Submit final changeset for verification. | `gpt-5-4` |

### Actuals (filled after the session)
- Orchestrator used: `claude-code opus-4.7 @ effort=medium`
- Total routed cost: `$1.05` — spec-finalization consensus (Gemini Pro + Opus + 2× GPT-5-4 verifiers) $0.218; ai-assignment.md authoring (Gemini Pro analysis) $0.020; session-verification R1+R2+R3+R4 (4× GPT-5-4) $0.235 + $0.201 + $0.179 + $0.199.
- Deviations from recommendation:
  - **Spec-finalization phase added before Session 1.** The spec was a DRAFT authored by a downstream consumer with four open maintainer decisions. Per the user's "finalize spec first, then start S1" direction, the maintainer resolved all four via cross-provider consensus (subcommand name = `report-baseline`; aggregate contract = query-result totals; parameter strategy = hybrid; scope = default-all-reports + `--reports` allowlist) before the boundary write. Spec, ROADMAP, and ai-assignment.md were updated to reflect resolutions; the DRAFT banner was removed.
  - **Steps 1.1–1.5 collapsed into a single orchestrator-driven implementation pass** (no per-step routed call) — Core-layer code is mechanical per the resolved contract and falls under Delegation Discipline's "orchestrator-direct" threshold.
  - **Step 1.6 took four rounds, not one.** R1 / R2 / R3 each returned ISSUES_FOUND with concrete must-fix items; every item was addressed in-session and re-verified. R4 returned VERIFIED with empty must_fix. The four-round trajectory was driven by the verifier's standards on determinism (canonical numeric Key, sorted Skipped order), API completeness (the Core-layer service was implicit in S1 scope but not exposed until R2 surfaced it), and edge cases (literal `(null)` string vs DBNull collision, PARAMETERS / TRANSFORM prefix coverage, typed-value bucket identity).
  - **R2 nice-to-have applied:** the inherited `samples/HomeHealthCare.accdb` working-tree drift (from a prior harvest run's COM-side metadata mutation, unrelated to S1) was reverted via `git checkout HEAD --` before R3 so it would not be staged in the closeout commit.
- Notes for next-session calibration:
  - The Core-layer service `ReportBaselineService` is the single API S2 wires from CLI code. It composes `RecordSourceResolver` → `IReportQueryExecutor` → `ReportAggregateComputer` → `ReportAggregatesWriter` and exposes `Capture` (returns the document) and `CaptureAndWrite` (writes `aggregates.json` to a caller-supplied directory).
  - `OleDbReportQueryExecutor` already implements `IReportQueryExecutor`. S2's CLI code constructs one executor per `--db` argument and passes it into the service.
  - The S2-scope items deferred from S1 are explicit and bounded: COM PDF render via `DoCmd.OutputTo`; CLI subcommand wiring (`--db`, `--out`, `--reports`, `--params`, `--json-status`); exit-code contract mirroring `run` (0/1/2/3); `baseline-manifest.json` index emission; PDF-binary determinism handling; `[SkippableFact]` integration test against a committed fixture DB.
  - Three minor nice-to-haves filed for future opportunism: a `ParametersUsed` mutation-after-Compute regression test; minor XML-doc cleanups; a future positional+provenance-bundled parameter input shape.

**Next-session orchestrator recommendation (Session 2):**
`claude opus-4.7 @ effort=high`
Rationale: S2 spans COM PDF render, CLI subcommand wiring, exit-code contract, the `baseline-manifest.json` index, and determinism-gate handling for binary PDFs — multiple coordinated concerns benefit from frontier-model synthesis.

---

## Session 2: COM PDF render + CLI subcommand wiring + determinism
### Recommended orchestrator
`opus @ effort=high`

### Rationale
This session integrates the new capability into the user-facing CLI and adds complex COM interop for PDF rendering. Wiring a new subcommand, handling its arguments, managing process exit codes, and ensuring determinism across outputs requires architectural synthesis and a deep understanding of the existing application structure.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| 2.1  | Integrate a `DoCmd.OutputTo acOutputReport` call into the existing COM session manager to render a specified report to a PDF file. | `orchestrator` |
| 2.2  | Wire the new `report-baseline` standalone subcommand, including flags: `--db`, `--out`, `--reports`, `--params`, `--json-status`. | `orchestrator` |
| 2.3  | Implement the specified process exit codes (0/1/2/3) to mirror the behavior of the `run` subcommand for batch-operation consistency. | `orchestrator` |
| 2.4  | Generate the top-level `baseline-manifest.json` index, iterating over all processed reports and recording their status, paths, and any skipped reason. | `orchestrator` |
| 2.5  | Author a `[SkippableFact]` integration test that runs the `report-baseline` command against a committed fixture database. | `orchestrator` |
| 2.6  | Update the determinism gate to exclude PDF binary content and timestamps from the byte-for-byte comparison, similar to how `ExportedAt` is handled. | `orchestrator` |
| 2.7  | Submit final changeset for verification. | `gpt-5-4` |

### Actuals (filled after the session)
- Orchestrator used: `claude-code opus-4.7 @ effort=high` (matches the recommendation).
- Total routed cost: `$0.45` — session-verification R1 (gpt-5-4) $0.320; session-verification R2 (gpt-5-4) $0.125. No routed calls for implementation work — orchestrator-direct per the Delegation Discipline Convention "When the orchestrator's model matches the routing target for a given task type, handle the task in-conversation rather than calling route()".
- Deviations from recommendation:
  - **All seven planned steps (2.1–2.7) handled by the orchestrator directly** (mechanical CLI/COM wiring + hermetic test authoring fall under orchestrator-direct per Delegation Discipline). Only step 2.7 (verification) was routed, matching the plan.
  - **Three additional pieces of work landed beyond the planned 2.1–2.7 set:**
    1. The S1-deferred-R4 `ParametersUsed` mutation regression test (an explicit "land in S2 alongside CLI tests" note from S1's Notes for Session 2 calibration).
    2. A drift-gate fix to `CliUsageTextTests.cs` (necessary regression — adding `report-baseline` to the dispatcher and exit-code legend broke a string-match assertion that pinned the prior heading verbatim; the fix updated the assertions to include the new subcommand).
    3. Convention-prescribed `[Collection("HomeHealthCareCom")]` applied to five pre-existing Integration test classes that drive Access COM against `samples/HomeHealthCare.accdb` (per project-guidance.md → Testing Conventions → "Do Not Chain Parallel xUnit Integration Tests"). Knocked the Integration project's failure count from 3 → 1; the remaining failure (`SanitizeCommandTests.HomeHealthCare_sanitize` timeout) reproduces in true isolation and is pre-existing, not introduced by S2 — logged as a follow-up.
  - **Step 2.7 took two rounds, not one.** R1 returned ISSUES_FOUND with two Majors: (1) positional parameter binding via `Dictionary<string, object?>` enumeration order — fragile contract; switched to `IReadOnlyList<KeyValuePair<string, object?>>` for explicit ordering, name-keyed dict derived from the list ONLY for provenance; (2) no stale-output cleanup on rerun — added `cleanRoot:true` default + `--no-clean` CLI opt-out, plus unconditional per-report dir recreate. Plus a both-fail coverage gap (added a test) and minor doc tightening (Reason XML doc mentions ReadSpecFailed; PrintUsage wording matches the actual JSON field names). R2 returned VERIFIED with empty issues.
  - **Integration test scoped via `--reports` to four `srpt*` sub-reports.** HomeHealthCare's eight `rpt*` top-level reports reference `Forms!`/`Reports!`/`PARAMETERS [X]` parameter bindings and `DoCmd.OutputTo` pops a modal parameter prompt for those when run standalone (confirmed empirically during the first integration-test run — 14-minute manual-cancellation slog). The spec's resolved decision routes required-param reports through `identify-usage` in Session 3 ("parameter elicitation"), explicitly NOT in S2 scope. Scoping the integration test to `srpt*` makes it run unattended in ~3 seconds while still exercising both `Captured` and `Failed/AggregateFailed` branches of the runner.
- Notes for next-session calibration:
  - **S2 produced the end-to-end pipeline through aggregates + PDF render + manifest.** S3 work is documentation (`install.md` CLI reference; interpretation guide), parameter elicitation design (see below), golden capture, and closeout.
  - **Parameter elicitation design.** Maintainer-validated this session: Access COM can resolve parameter-required reports without modifying the `.accdb` — via `TempVars` + hidden-form binding shimming, or via in-memory `RecordSource` substitution closed with `acSaveNo`. The `acSaveNo` literal-substitution approach is the universal fallback (works regardless of binding kind). S3's `identify-usage` integration should design around one of these.
  - **PDF necessity / opt-in design question (S3 maintainer call).** Maintainer raised whether PDFs are pulling their weight given the aggregate contract is RecordSourceResultSet, not footer-expression evaluation. Arguments for keeping PDFs: footer-expression values (not in aggregates.json), visual layout fidelity, hedge against metadata-extraction gaps, human-stakeholder validation artifact. Arguments against: cost, modal-prompt exposure, non-deterministic binary. Possible S3 spec revisit: default to aggregates-only with `--include-pdf` opt-in. Flagged here for S3 maintainer consideration.
  - **One minor nice-to-have from R2** (not in scope for S2 closeout): narrow the broad-catch in `AccessReportSpecReader` around `RecordSource` + `GroupLevel.Count` reads — currently flattens COM read failures into "unbound/no groups" rather than surfacing as a distinct failure. Opportunistic for S3 or a later set.

**Next-session orchestrator recommendation (Session 3):**
`gemini-pro @ effort=medium` (matches the original ai-assignment.md recommendation below; S3 is mostly documentation drafting + golden capture + mechanical closeout, which is the cost-effective profile for gemini-pro). The orchestrator should route docs drafting through `route(task_type="summarization")` (NOT `documentation`, per the convention) and handle file mechanics directly.

---

## Session 3: Goldens, docs, parameter elicitation, and closeout
### Recommended orchestrator
`gemini-pro @ effort=medium`

### Rationale
The primary work in this session is documentation drafting, running the now-complete tool to generate golden files, and performing mechanical project closeout tasks. A cost-effective model is well-suited for generating initial documentation drafts and handling checklist-driven file modifications, with the orchestrator handling the file I/O and tool execution.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 3.1  | Run the new `report-baseline` subcommand to capture golden artifacts for `NorthwindDevEdition` (rptInvoice, rptTotalsByOwner, rptTotalsByCategory) and all in-scope reports for `HomeHealthCare`. | `orchestrator` |
| 3.2  | Draft documentation for the new subcommand in `docs/harvester/install.md` (CLI reference) and `docs/harvester/interpretation-guide.md`. | `gemini-pro` |
| 3.3  | Ensure the documentation explicitly covers the parameter selection strategy (unfiltered default, `identify-usage` for required params) and the `--params` JSON file shape. | `gemini-pro` |
| 3.4  | Update the `READ-ME-FIRST.md` template to include the new `reports-baseline` artifact, ensuring downstream agents can discover it. | `orchestrator` |
| 3.5  | Perform project closeout: update the change-log, flip the ROADMAP status from *in-progress* to *complete*, and conduct the Step 9 reorganization review. | `orchestrator` |
| 3.6  | Submit final changeset for verification. | `gpt-5-4` |

### Actuals (filled after the session)
- Orchestrator used: `github-copilot gpt-5.4` for the successful recovery/closeout pass on a Session 3 run that had previously been started under `claude-code opus-4.8` and left in a bad state.
- Total routed cost: `$0.169` — Session-verification R1 (same-provider `gpt-5-4` via the repo's Claude-calibrated `session-verification` override) $0.1553; Session-verification R2 (forced `gemini-pro`, the counting cross-provider review for this OpenAI-orchestrated recovery) $0.0136.
- Deviations from recommendation:
  - **Session 3 became an implementation-recovery session before it could be a goldens/closeout session.** A prior closeout attempt left the set with a real correctness defect (`--include-pdf` could render before aggregate success was known) plus forged verification state. The recovered implementation keeps `report-baseline` aggregates-first, makes PDF capture opt-in, and suppresses rendering when aggregate capture fails.
  - **The originally planned golden corpus / regression-gate / `READ-ME-FIRST` propagation work did not ship here.** The maintained follow-up decision from the Session 3 addendum held: `AccessReportSpecReader.ReadSpec` opens reports in hidden design view, which collides with concurrent ACE OLE DB aggregate capture and reproduced as a hang. With maintainer approval, that work remains deferred to `harvester-report-baseline-golden-corpus`.
  - **Verification took two rounds, but only Round 2 is the valid cross-provider closeout review.** Round 1 used `route(task_type='session-verification')` and landed on `gpt-5-4` because `router-config.yaml` pins that task type for Claude orchestrators; for this GitHub Copilot / OpenAI recovery pass, Round 1 was same-provider and therefore informational only. Round 2 forced `query(model='gemini-pro', task_type='session-verification')` and returned `VERIFIED` with empty issues.
  - **Docs drafting was handled directly rather than routed.** The changed `install.md` / `interpretation-guide.md` text was part of the implementation-recovery diff and was tightened alongside the code/tests so the shipped behavior and docs stayed aligned.
- Notes for next-session calibration:
  - No further sessions remain in this set.
  - The named follow-up set `harvester-report-baseline-golden-corpus` owns the deferred lock-free aggregate capture work, committed `reports-baseline/` corpus, regression gate, and `READ-ME-FIRST` propagation.