# AI assignment — 016-ribbon-navigation-surface

Orchestrator recommendations authored via routed analysis (`route(task_type="analysis")`),
not self-opined, per the project convention. Cross-provider `session-verification` is
mandatory and routed to a different provider (gpt-5-4) regardless of orchestrator.

## Session 1: RibbonStructure facet — parse USysRibbons into NavigationGraph.Ribbon

### Recommended orchestrator
**Claude Opus 4.8** (engine `claude-code`, provider `anthropic`), effort **medium→high**.

### Rationale
New data surface + a customUI parser + a model-contract decision (how far to extend the
flat `RibbonStructure` for a real ribbon) — synthesis-grade design work where Opus earns
its keep. The model-shape fork was resolved by cross-provider consensus (gpt-5-4 +
gemini-pro), and the parser/shape-gate seam by routed verification (gpt-5-4).

### Actuals (filled after the session)
- Orchestrator: Claude Opus 4.8 (`claude-opus-4-8[1m]`), effort medium.
- Routed calls: model-design consensus (gpt-5-4 + gemini-pro, `architecture`); S2-orchestrator
  recommendation (`analysis`, max_tier 1); session-verification ×3 rounds (gpt-5-4) → VERIFIED.
- Outcome: VERIFIED; HHC golden byte-neutral; Northwind validated locally; 942 hermetic
  Pipeline + 7 detector pins green; DW-20/DW-21 filed.

## Session 2: Ribbon entry points (onAction → callback) + closeout

### Recommended orchestrator
**Claude Opus 4.8** (engine `claude-code`, provider `anthropic`), effort **high**.

### Rationale (routed)
Routed analysis (`C:/temp/s2-orch-rec.md`): the final session is closeout-grade — `change-log.md`,
the routed next-session-set recommendation, the set-wide devil's-advocate verification with
cross-session evidence, and DW-13 discharge — which favours a strong synthesis model; and
continuity with S1's faithful-mechanical ribbon design carries directly into the onAction→VBA
entry-point resolution. `continue-current-trajectory`.

### Estimated routed cost
1 closeout-grade `session-verification` (cross-provider, gpt-5-4) + 1 routed
`next-session-set` recommendation; small.

### Actuals (filled after the session)
_TBD — Session 2._
