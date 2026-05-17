# AI Assignment Ledger: lowdefy-ir-foundation

## Session 1 of 2: Schema + Hand-Authored Sample

### Recommended Orchestrator
- **Engine:** Claude Code (Anthropic)
- **Model:** Sonnet 4.6 
- **Effort:** medium
- **Rationale:** Schema design is structured mechanical work (analysis task type, tier 2) well-suited to Sonnet. Hand-authoring is direct file work. Cross-provider review is code-review (tier 2). Sonnet covers all three efficiently without over-spending on tier 3.

### Estimated Routed Cost
- **Cost:** Low
- **Justification:** Two small routing tasks (analysis + code-review), both tier 2, with moderate prompt sizes (schema definition and sample validation).

### Per-Step Routing Decisions

| Step | Action | Task Type | Model Tier | Notes |
|------|--------|-----------|------------|-------|
| 1. Define ModalDetailFormSchema.json | Route | analysis | Tier 2 (Sonnet) | Schema design based on Lowdefy consensus |
| 2. Hand-author frmBudgetServicePlan.ir.yaml | Direct | — | — | Orchestrator directly authors sample IR |
| 3. Route cross-provider review on schema | Route | code-review | Tier 2 (Sonnet) | Cross-provider validation of schema completeness |
| 4. Land both files with commit | Direct | — | — | Orchestrator stages and commits |

---

## Session 2 of 2: Generator + Validation

### Recommended Orchestrator (Preview)
- **Engine:** Claude Code (Anthropic)
- **Model:** Sonnet 4.6
- **Effort:** medium
- **Rationale:** T4 template design and generated Blazor review are code-generation and code-review tasks (tier 2). Generator output validation against pilot is also code-review. Sonnet tier 2 provides good balance.

### Estimated Routed Cost (Preview)
- **Cost:** Moderate
- **Justification:** Template design prompt is larger; cross-provider review on generated Blazor will include full component code.

### Per-Step Routing Decisions (Preview)

| Step | Action | Task Type | Model Tier | Notes |
|------|--------|-----------|------------|-------|
| 1. Build T4 generator | Direct | — | — | Orchestrator authors template based on schema |
| 2. Run generator + validate | Direct | — | — | Orchestrator executes and compiles |
| 3. Compare against pilot | Direct | — | — | Orchestrator manual comparison |
| 4. Capture friction + schema gaps | Route | code-review | Tier 2 (Sonnet) | Cross-provider validation of findings |
| 5. Land generator + notes | Direct | — | — | Orchestrator stages and commits |

---

## Actuals (To be filled after Session 1 completes)

- **Orchestrator used:** —
- **Total routed cost:** —
- **Deviations from recommendation:** —
- **Notes for next-session calibration:** —
