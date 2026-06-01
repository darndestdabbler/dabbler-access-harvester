## Session 2: Commit Corpus + Regression Gate + READ-ME-FIRST + Closeout

### Recommended orchestrator
claude-code / claude-sonnet-4-6 @ effort=medium

### Rationale
Session 1 solved the core SQL rewrite problem, leaving this session with mechanical artifact commits and a constrained extension to the existing regression harness. A cost-effective mid-tier model is sufficient for this well-defined integration and documentation work.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 8.1 | Commit golden corpus for `reports-baseline/` artifacts. | human-ops |
| 8.2 | Extend regression harness and normalizer for `reports-baseline/` diffs. | claude-code / claude-sonnet-4-6 |
| 8.3 | Update `READ-ME-FIRST` docs with new artifact details. | claude-code / claude-sonnet-4-6 |
| 8.4 | Author session closeout artifacts. | gemini-cli / gemini-2.5-pro |
| 8.5 | Verify Session 2 deliverables. | gemini-cli / gemini-2.5-pro |