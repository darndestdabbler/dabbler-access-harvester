# AI Assignment Ledger -- vba-parser-session-set-generator

> **Authored:** 2026-04-25 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals" block under each session row once it completes, and updates the *next* session's row if the actuals reveal a miscalibration.

## Session 1 of 4: Harvester-Import Retrospective

**Recommended orchestrator:** Claude Sonnet 4.6 @ effort=medium
**Rationale:** This session's work is primarily reading artifacts and marshalling them for a single, routed `analysis` call. A mid-tier model is cost-effective for the mechanical I/O and capable enough to manage the context for the synthesis and verification steps.
**Estimated routed cost:** $0.50 - $1.50

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1-2 | mechanical | — | — | — | Direct file reads; no AI reasoning required. |
| 3-4 | reasoning | analysis | sonnet/gemini-pro | medium | Synthesis of findings from multiple docs; complexity 65 routes to Tier 2. |
| 5 | mechanical | — | — | — | Write final markdown file from synthesized content. |
| 6 | reasoning | session-verification | gpt-5-4 | high | Per `task_type_overrides`; mandatory cross-provider gate. |

**Actuals (filled after the session runs):**
- Orchestrator used: Claude Opus 4.7 @ effort=high (1M context).
- Total routed cost: $0.5651 (6 calls). By model: gemini-pro 3 calls $0.0665 (Step 3.5 ai-assignment + Step 4 retrospective synthesis + Step 8 next-orchestrator recommendation); gpt-5-4 3 calls $0.4986 (3 verification rounds).
- Deviations from recommendation: Opus orchestrator used where Sonnet@medium was recommended. Synthesis itself was routed (correctly), so the orchestrator handled only artifact reads + structured drafting + edits — work Sonnet would have handled. The deviation matches the import set's Session 2/4/6 pattern of over-spending on mechanical orchestrator work.
- Notes for next-session calibration: Verification took 3 rounds, all on evidence-grounding tightness (not content gaps). Round 1 found 5 Major (cut 4 plan-observation rows; moved 1 to Session 4; rebucketed 1 to no-op; tightened 4 evidence pointers; added 1 missing finding). Round 2 found 2 Major + 1 Medium (residual non-retrospective row #17 cut; row-1 evidence still inferential; row-2 set-wide claim narrowed). Round 3 found 2 Medium (row-1 verification-concentration framing softened; row-2 ranking-claim phrasing softened) — applied via direct edit at retry cap. Final retrospective: 17 rows (16 adopt-now + 1 no-op). Session 2 (decomposition) is a document-authoring synthesis session; verifier on it will be similarly strict on evidence — *the verifier will expect plan-content evidence, NOT import-set evidence*, since decomposition is plan-derived.

**Next session orchestrator recommendation (Session 2):**
Claude Opus 4.7 @ effort=medium

**Rationale:** Session 1 revealed that the verifier is exceptionally strict on evidence-grounding, leading to expensive multi-round fixes. Using Opus as the orchestrator allows the core `planning` task to be handled in-conversation, which better preserves context and is more likely to produce a well-grounded initial draft that satisfies the verifier.

## Session 2 of 4: Plan Decomposition

**Recommended orchestrator:** Claude Opus 4.7 @ effort=medium *(updated from Session 1's actuals — original recommendation was Sonnet 4.6 @ medium; see Session 1's "Next session orchestrator recommendation" block above.)*
**Rationale:** The core cognitive work is a high-complexity `planning` task that under `router-config.yaml` `task_type_overrides` pins to Opus. With an Opus orchestrator, that work can be handled in-conversation rather than via a same-model `route()` call (per `project-guidance.md` Convention "When the orchestrator's model matches the routing target..."). Session 1 demonstrated the verifier is strict on evidence-grounding; Opus context fidelity helps the initial draft survive verifier rounds.
**Estimated routed cost:** $1.00 - $2.00

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1-4 | mechanical | — | — | — | Read artifacts, build test-case coverage table; preparative work. |
| 5-6 | reasoning | planning | opus | high | Core decomposition work; complexity 80 routes to Tier 3. |
| 7 | mechanical | — | — | — | Write final markdown tree from the planned decomposition. |
| 8 | reasoning | session-verification | gpt-5-4 | high | Per `task_type_overrides`; mandatory cross-provider gate. |

**Actuals (filled after the session runs):**
- Orchestrator used: TBD
- Total routed cost: TBD
- Deviations from recommendation: TBD
- Notes for next-session calibration: TBD

## Session 3 of 4: Stub Specs

**Recommended orchestrator:** Claude Sonnet 4.6 @ effort=medium
**Rationale:** This session is a mechanical loop that orchestrates 6-12 independent, routed `planning` calls. A mid-tier model is the most cost-effective choice for managing this straightforward, repetitive workflow.
**Estimated routed cost:** $2.00 - $4.00

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Read tree and retrospective files to set up the loop. |
| 2 | reasoning | planning | opus | high | Repeated `route()` call per child; complexity 70 routes to Tier 3. |
| 3 | mechanical | — | — | — | Not applicable (no code). |
| 4 | reasoning | session-verification | gpt-5-4 | high | Per `task_type_overrides`; mandatory cross-provider gate for the set of stubs. |

**Actuals (filled after the session runs):**
- Orchestrator used: TBD
- Total routed cost: TBD
- Deviations from recommendation: TBD
- Notes for next-session calibration: TBD

## Session 4 of 4: Routing Playbook + Change Log

**Recommended orchestrator:** Claude Sonnet 4.6 @ effort=medium
**Rationale:** The primary deliverable is generated via a single, high-stakes `architecture` call that is pinned to Opus. A mid-tier orchestrator is sufficient to manage this call and the subsequent low-complexity `summarization` task.
**Estimated routed cost:** $1.00 - $2.00

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1-2 | mechanical | — | — | — | Read inputs to prepare context for the main routed call. |
| 3-4 | reasoning | architecture | opus | xhigh | Per `task_type_overrides`; playbook synthesis is high-stakes design work. |
| 5 | mechanical | — | — | — | Write playbook file. |
| 6 | reasoning | summarization | sonnet/gemini-pro | low | Standard last-session change-log generation; low complexity. |
| 7-10 | mechanical | — | — | — | Final verification, commit, and post-session review steps. |
| 8 | reasoning | session-verification | gpt-5-4 | high | Per `task_type_overrides`; mandatory cross-provider gate. |

**Actuals (filled after the session runs):**
- Orchestrator used: TBD
- Total routed cost: TBD
- Deviations from recommendation: TBD
- Notes for next-session calibration: TBD
