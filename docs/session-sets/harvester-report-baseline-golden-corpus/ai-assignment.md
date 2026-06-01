## Session 1: Deterministic Northwind Date-Helper Support + Capture Proof

### Recommended orchestrator
claude-code claude-sonnet-4-6 @ effort=medium

### Rationale
The scope has narrowed from complex lock investigation to a deterministic SQL substitution, which is a bounded implementation task. A lean, cost-effective orchestrator is sufficient for the well-defined coding and testing work required.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Implement narrow SQL rewrite for `reportParameterStartDate()` / `reportParameterEndDate()` helpers. | `claude-code / claude-sonnet-4-6` |
| 2    | Add hermetic unit tests for the date-helper rewrite logic. | `claude-code / claude-sonnet-4-6` |
| 3    | Integrate the rewrite into the aggregate capture path and add a validation test slice. | `claude-code / claude-sonnet-4-6` |
| 4    | Verify final implementation and test suite with a cross-provider review. | `github-copilot / gpt-5.4` |

### Actuals (filled after the session)
- Orchestrator used: github-copilot gpt-5.4 @ effort=high
- Total routed cost: ~$0.0327 across three routed calls: Step 3.5 ai-assignment authoring on gemini-pro ($0.009531), Session 1 cross-provider verification on gemini-pro ($0.0173), and Session 2 next-orchestrator recommendation on gemini-pro ($0.0059).
- Deviations from recommendation: Session 1 was started under GitHub Copilot / GPT-5.4 before the routed recommendation was written; the delivered work stayed bounded and verified cleanly, so no mid-session orchestrator swap was needed.
- Notes for next-session calibration: The core blocker was narrower than the deferred stub assumed. Saved-query SQL preload plus deterministic substitution of the documented Northwind helper pair was sufficient; Session 2 should stay focused on committing the corpus and wiring regression coverage, with the authoritative next-session recommendation in `next-orchestrator-recommendation-session-2.md`.

## Session 2: Commit Corpus + Regression Gate + READ-ME-FIRST + Closeout

Recommendation superseded at Session 1 closeout. The authoritative Session 2 recommendation now lives in `next-orchestrator-recommendation-session-2.md`, authored via `route(task_type='analysis')` per Step 8 / Rule #17.

### Recommended orchestrator
claude-code claude-sonnet-4-6 @ effort=low

### Rationale
This session consists of mechanical tasks like extending an existing test harness and updating documentation. The work follows established patterns and does not require a high-end orchestrator, prioritizing low cost and fast turnaround.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Extend the golden-file normalizer for report-baseline manifest and aggregate files. | `claude-code / claude-sonnet-4-6` |
| 2    | Wire the report-baseline artifacts into the canonical golden regression test harness. | `claude-code / claude-sonnet-4-6` |
| 3    | Draft updates for the fixture `READ-ME-FIRST.md` files to document the new artifacts. | `claude-code / claude-sonnet-4-6` |
| 4    | Verify regression test code and review documentation updates with a cross-provider agent. | `gemini-cli / gemini-2.5-pro` |

### Actuals (filled after the session)
- Orchestrator used: ...
- Total routed cost: ...
- Deviations from recommendation: ...
- Notes for next-session calibration: ...