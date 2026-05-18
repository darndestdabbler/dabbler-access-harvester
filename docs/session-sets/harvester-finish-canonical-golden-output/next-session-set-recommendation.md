# Next-Session-Set Recommendation

> **Authored:** 2026-05-18 at the close of `harvester-finish-canonical-golden-output` Session 4.
> **Method:** routed `route(task_type="analysis")` to `gemini-pro`.
> **Plan source of truth:** `docs/planning/strategy.md` → Phase 4
> (5-set sequence pinned 2026-05-17 by `remaining-work-planner` Session 2).

---

## Verdict: **CONFIRM**

**Next session set:** `harvester-finish-cli-e2e-test-audit`
**Reason code:** `continue-current-trajectory`

The Phase 4 plan sequences F2 → F3 with F3 as a Criterion #4 gate
("every harvester subcommand has black-box CLI tests; deterministic
output"). With F2 now closed and the golden corpus committed, F3 has
a natural input for its determinism check — run the CLI twice
against a canonical fixture and diff the output against the now-
committed golden. Confirming the plan is the cost-minimal move and
keeps the next gate aligned with the criterion sequence.

## Parallelism advisory

F3 and F4 (`harvester-finish-operations-and-interpretation-docs`) are
parallel-eligible per the Phase 4 plan. The routed analysis
recommends running them in **separate worktrees** under the bare-
repo + flat-worktree layout. The two efforts are mechanically
independent — F3 is integration-test work in
`tests/AccessHarvester.Integration/Cli/`; F4 is markdown authoring in
`docs/`. The single predictable merge-noise file is
`router-metrics.jsonl`; one reconciliation commit per completed set
is the expected merge tax. If running serially is preferred for any
non-throughput reason (window budget, attention bandwidth, etc.),
F3 first → F4 second matches the criterion numbering.

## Orchestrator recommendation for F3 Session 1

**Recommended:** Sonnet @ effort=high
**Rationale:** F3 Session 1's work is mechanical — auditing existing
CLI test coverage against the subcommand list, writing missing
black-box tests, and adding a determinism gate. Test authoring of
this shape is squarely in the "mechanical correspondence" bucket
the project-guidance Convention pins to Sonnet rather than Opus.
Cross-provider verification will route to `gemini-pro` per the
verifier-selection rules for Sonnet output.

If the F3 worktree is run in parallel with F4, the human may want
to drive one with Sonnet (F3) and one with Opus or Codex (F4) so
both subscription windows burn concurrently per the
`maxout`-pairing pattern in `project-guidance.md` →
Token-Window Budget Overrides.

## F2 actuals to inform calibration

- All 4 sessions completed VERIFIED on 2026-05-18 (single day).
- Total routed cost: ~$0.90 (vs the spec's $0.40–$0.80 estimate;
  overrun was Session 1's 5-round verification convergence on
  narrative-cross-reference drift, not code/test work).
- Sessions 2–4 each closed under the per-session estimate band.
- The orchestrator used Opus 4.7 throughout, including Session 4
  via human override despite Session 3's routed recommendation of
  Sonnet for Session 4. Cost remained well-contained because Opus's
  in-conversation work bypasses routed calls; spend was dominated
  by cross-provider verification (which is Sonnet/Opus-routed
  regardless of orchestrator).
