# AI Assignment - 007-integration-test-rebase

> Authored on Session 1 (2026-06-12) via route(task_type="analysis") per
> docs/ai-led-session-workflow.md Step 3.5. Actuals appended at each close.

## Session 1: Inventory, Fixture Strategy, And Hermetic Re-Base

### Recommended orchestrator
Claude Code claude-fable-5 @ effort=medium (fait accompli)

### Rationale
This session mixes code analysis, strategic decision-making, and mechanical refactoring. Claude Code excels at both understanding test intent for analysis and executing precise, constrained code modifications.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Read and synthesize project docs, specs, and change-logs. | Route (gemini-pro) |
| 2    | Enumerate failing tests and analyze their assertions across five files. | Route (opus) |
| 3    | Decide fixture strategy (copy vs. ref) under hard constraints, including preservation of v1.0 compatibility test intent. | Route (opus) |
| 4    | Implement re-base: modify five test files to use new HomeHealthCare-derived fixtures, updating assertions. **Constraints:** Test code/fixtures only, no weakening assertions. | Direct |
| 5    | Run `dotnet build` and the re-based non-COM tests sequentially and in the foreground. | Direct |
| 6    | Cross-provider verification of the session's diff and analysis artifacts (inventory, fixture rationale). | Route (gpt-5-4) |
| 7    | Finalize artifacts (`disposition.json`), commit, push, notify. | Direct |

### Actuals (filled after the session)
- Orchestrator used: `<engine + model + effort>`
- Total routed cost: $X.XX
- Deviations from recommendation: `<any>`
- Notes for next-session calibration: `<any>`

**Next-session orchestrator recommendation (Session 2):**
Gemini Code Assist gemini-pro @ effort=high
Rationale: Session 2's primary AI-leveraged task is authoring a new, complex session set spec, a task well-suited to Gemini's long-context and structured-prose generation capabilities.

---

## Session 2: COM-Tier Validation, Flake Disposition, And Next-Set Authoring

### Recommended orchestrator
Gemini Code Assist gemini-pro @ effort=high

### Rationale
This session's work is dominated by authoring a new long-form specification from multiple sources, a task that benefits from a strong prose and synthesis model. Gemini Code Assist is also adept at the secondary task of analyzing test output for the intermittent flake investigation.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | On the Access-equipped host, run the full integration suite sequentially and in the foreground, logging to TRX. **Constraint:** Foreground, sequential COM-tier runs only. | Direct |
| 2    | Analyze TRX output for the `SanitizeCommandTests` flake and, if it fired, propose a bounded mitigation (e.g., quarantine with `Skip.If`). **Constraint:** Time-boxed to characterization + one mechanical mitigation; no open-ended debugging. | Route (gemini-pro) |
| 3    | Run `git checkout -- samples/*.accdb` to discard any spurious diffs before staging. | Direct |
| 4    | Author the `docs/session-sets/008.../spec.md` and stub `session-state.json` based on `006`'s review findings. | Route (opus) |
| 5    | Cross-provider verification of the TRX evidence, any code changes, and the new `008` spec. | Route (gpt-5-4) |
| 6    | Finalize artifacts, including `next-session-set-recommendation.md` (routed analysis). **Constraint:** Carry forward the never-regenerate-northwind rule into the next set's spec. Commit, push, notify. | Route (sonnet) for analysis; Direct for mechanics. |

### Actuals (filled after the session)
- Orchestrator used: `<engine + model + effort>`
- Total routed cost: $X.XX
- Deviations from recommendation: `<any>`
- Notes for next-session calibration: `<any>`