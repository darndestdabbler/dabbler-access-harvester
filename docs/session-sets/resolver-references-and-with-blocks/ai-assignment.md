# AI Assignment Ledger — resolver-references-and-with-blocks

> **Authored:** 2026-04-27 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals" block under each session row once it completes, and updates the *next* session's row if the actuals reveal a miscalibration.

## Session 1 of 4-5: Resolution Algorithm (Synthesis)

**Recommended orchestrator:** claude-3 Opus 4.7 @ effort=high
**Rationale:** Session 1 is a primary synthesis task (resolution algorithm), requiring a top-tier model to generate the core logic and handle complex special cases correctly.
**Estimated routed cost:** $0.35

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Read project-guidance, plan docs, and relevant fixtures. |
| 2 | synthesis | architecture | opus | max | Resolution-algorithm synthesis, including special cases and built-in function registry. Per convention, authored in-conversation since orchestrator is Opus. |
| 3 | mechanical | — | — | — | Land resolver files, build, and run initial tests. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Mandatory cross-provider gate to ensure correctness and adherence to project conventions. |

**Actuals (filled after the session runs):**
- Orchestrator used: claude-code Opus 4.7 (1M context)
- Total routed cost: $0.5359 — 4 calls
  - 1 × `analysis` @ gemini-pro (ai-assignment authoring): $0.0187
  - 3 × `session-verification` @ gpt-5-4 (rounds 1, 2, 3): $0.5172
- Deviations from recommendation: synthesis was authored in-conversation per the "When the orchestrator's model matches the routing target..." Convention rather than via `route(architecture)`. Verification took 3 rounds (Round 1 = 3 Major + 2 Minor; Round 2 = 1 Major + 2 Minor; Round 3 = VERIFIED). All findings were real and fixed; the iteration consumed ~$0.20 more than the $0.35 estimate.
- Notes for next-session calibration: a synthesis-doc-first workflow caught most of the algorithmic bugs (good); the verifier's coverage-gap findings (registry-precedence, parenthesized-target unwrap) suggest pre-routing the test list itself through the verifier before starting Round 1, or expanding the synthesis-doc §9 test plan with concrete edge cases.

**Next session orchestrator recommendation (Session 2):**
claude-code Opus 4.7 @ effort=high

**Rationale:** Session 2's scope is a surgical implementation of a deterministic flattening rule within two pre-defined visitor methods, a narrower task than the pre-recommendation's `max` effort implies. Session 1 successfully established all prerequisite scaffolding, making this a focused implementation task that warrants a high-precision model but not one requiring novel architectural design.

## Session 2 of 4-5: With-Block Flattening (Synthesis)

**Recommended orchestrator:** claude-3 Opus 4.7 @ effort=high
**Rationale:** Session 2 is the second primary synthesis task (with-block flattening), requiring a top-tier model to correctly analyze scope and transform the AST without data loss.
**Estimated routed cost:** $0.30

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Read plan docs and `WithBlockScope.cs` from prerequisite session #7. |
| 2 | synthesis | architecture | opus | max | With-block flattening synthesis. Per convention, authored in-conversation since orchestrator is Opus. |
| 3 | mechanical | — | — | — | Land file changes, build, and run initial tests. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Mandatory cross-provider gate to ensure correctness. |

**Actuals (session 2 — completed 2026-04-27):**
- Orchestrator used: claude-code Opus 4.7 (1M context).
- Total routed cost: $0.3282 across 3 calls.
  - 2 × `session-verification` @ gpt-5-4: Round 1 $0.1436 (ISSUES_FOUND, 1 Major + 1 Minor); Round 2 $0.1827 (VERIFIED).
  - 1 × `analysis` @ gemini-pro (next-orchestrator recommendation): $0.0019.
- Deviations from recommendation: synthesis was authored in-conversation per the "When the orchestrator's model matches the routing target..." Convention rather than via `route(architecture)`. Verification took 2 rounds; both Round-1 findings (target-expansion bug in `EmitImplicitWithFlattening`; non-recursive InnerOutcome variant check in fixture test) were real and fixed.
- Notes for next-session calibration: synthesis-doc-first kept the implementation tight (~80 lines + tests), but Round-1 still surfaced two real defects the doc had not pre-empted — verifier-driven QA remains worth its cost. Resolver suite landed at 151 passing tests (Session 1 left it at 144; Session 2 net +7 including 1 Round-1 regression test).

**Next session orchestrator recommendation (Session 3):**
claude-3 Opus 4.7 @ effort=medium

**Rationale:** This session defines a core architectural data structure (`ResolvedModule`), a task well-suited to a top-tier model. Session 2's actuals revealed that even small integrations in this codebase can contain subtle defects, justifying retaining Opus to manage the design nuance and reduce implementation risk.

## Session 3 of 4-5: ResolvedModule Annotations + Provenance

**Recommended orchestrator:** claude-3 Opus 4.7 @ effort=medium
**Rationale:** This session involves moderate synthesis (defining the `ResolvedModule` shape) and integrating the outputs of two prior synthesis sessions. Using a top-tier orchestrator minimizes risk of misinterpretation and rework.
**Estimated routed cost:** $0.25

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Read outputs from S1 and S2. |
| 2 | synthesis | architecture | opus | high | Define `ResolvedModule` shape and annotation infrastructure; integrate S1/S2 logic. Per convention, authored in-conversation. |
| 3 | mechanical | — | — | — | Land files, build, and test. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Mandatory cross-provider gate. |

**Actuals (filled after the session runs):**
- Orchestrator used: <to be filled>
- Total routed cost: <to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>

## Session 4 of 4-5: Cross-Cut + Mechanical Tightening

**Recommended orchestrator:** claude-3.5 Sonnet 4.6 @ effort=medium
**Rationale:** This session is purely mechanical, involving running existing code against fixtures and performing schema validation. A mid-tier model is sufficient and cost-effective.
**Estimated routed cost:** $0.15

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Run resolver over fixtures 02, 03, 09, 08-unresolvable.bas. |
| 2 | reasoning | schema-validation | sonnet | dynamic | Mechanical correspondence check of resolved AST vs fixture `expected.json`. Pinned to Sonnet per convention. |
| 3 | mechanical | — | — | — | Land tightening commits based on validation results. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Mandatory cross-provider gate. |

**Actuals (filled after the session runs):**
- Orchestrator used: <to be filled>
- Total routed cost: <to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>

## Session 5 of 4-5 (optional buffer): Verifier-Surfaced Cleanup

**Recommended orchestrator:** Contingent; claude-3.5 Sonnet 4.6 @ medium (default) or claude-3 Opus 4.7 @ high (if rework is architectural).
**Rationale:** This is an optional buffer session. The orchestrator choice depends on the nature of the verifier-surfaced issues, defaulting to a mechanical-tier model unless significant rework is needed.
**Estimated routed cost:** $0.10 - $0.40

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | reasoning | analysis | gemini-pro | dynamic | Triage verifier findings from S4 to determine scope of work. |
| 2 | mechanical/synthesis | — | — | — | Implement fixes. The model and task type depend entirely on the triage results. |
| 3 | reasoning | session-verification | gpt-5-4 | high | Mandatory cross-provider gate on any fixes implemented. |

**Actuals (filled after the session runs):**
- Orchestrator used: <to be filled>
- Total routed cost: <to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>