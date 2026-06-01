# AI Assignment — `harvester-report-displayed-total-parity`

Per-session ledger of the cheapest-capable orchestrator for each step, plus a
forward-looking recommendation for the next session. Recommendations are
produced via `route(task_type="analysis")` (Gemini Pro, cross-provider from a
Claude orchestrator) so they are independent of the orchestrator's own model —
never self-opined (workflow Rule 17).

---

## Session 1: Add `DisplayTotals` to the report-baseline capture

### Recommended orchestrator
Codex GPT-5.5 @ effort=high

### Rationale
Codex excels at greenfield algorithm implementation and reasoning through
complex, platform-specific APIs like COM interop. This session's mix of parser
design and Windows-specific C# extensions makes it a strong choice for a
correct, robust implementation.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1.0 | Inventory existing `ReportBaseline` C# pipeline | orchestrator (mechanical reads) |
| 2.0 | Survey footer controls in the 5 target reports | orchestrator (read IR extraction, no route needed) |
| 3.0 | Design the recursive-descent Sum-aggregate evaluator | `route(analysis)` → gemini-pro (cross-provider design read) |
| 4.0 | Implement tokenizer, parser, evaluator in C# | orchestrator (deterministic single-feature C#) |
| 5.0 | Extend C# records for `DisplayTotals` capture | orchestrator (mechanical, <50 lines/file) |
| 6.0 | Extend Windows COM reader for footer metadata | orchestrator (mechanical COM read) |
| 7.0 | Write hermetic xUnit tests for the evaluator | orchestrator (test authoring against in-context types) |
| 8.0 | Live COM capture against `rptInvoice` | orchestrator (single CLI run, no parallel dotnet) |
| 9.0 | End-of-session cross-provider verification | `route(session-verification)` → gpt-5-4 (mandatory) |

### Actuals (filled after the session)
- Orchestrator used: **Claude Opus 4.8 @ effort=high** (human-selected via
  "Start the next session" — Rule 7; deviates from the routed Codex GPT-5.5
  recommendation).
- Total routed cost: see Step 8 cost report (design-fork `analysis` +
  `ai-assignment` `analysis` + final `session-verification`).
- Deviations from recommendation: orchestrator engine (Claude, not Codex) per
  the human's session-start choice. The implementation was handled
  in-conversation rather than routed — the work was deterministic
  single-feature C# under the Delegation-Discipline "mechanical, owns
  mechanics" bar; the one genuine design fork (focused evaluator vs. reuse the
  ANTLR VBA parser) WAS routed to cross-provider `analysis` (gemini-pro chose
  the focused evaluator, Option A).
- Notes for next-session calibration: the spec premise "reuse the repo's
  existing Access-expression parser" was wrong — no standalone Access-expression
  parser exists; a focused closed-set (`Sum` only) recursive-descent evaluator
  was written instead. Session 2 is corpus regen + docs + a small CLI flag; it
  does not need a frontier coder.

**Next-session orchestrator recommendation (Session 2):** Claude Sonnet 4.6 @
effort=low — Rationale: the work is primarily documentation (narrative prose),
simple CLI wiring, and mechanical byte-diff parity checks, which Sonnet handles
cost-effectively; the mandatory cross-provider session-verification stays on
GPT-5.4.
