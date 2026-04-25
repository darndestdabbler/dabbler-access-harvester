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
- Orchestrator used: Claude Opus 4.7 @ effort=high (1M context). Matches recommendation.
- Total routed cost: $0.4421 (4 calls). By model: gpt-5-4 3 verification rounds $0.4395 ($0.1999 + $0.1824 + $0.0594 -- round 3 short-and-clean); gemini-pro 1 call $0.0026 (next-orchestrator recommendation).
- Deviations from recommendation: none on orchestrator. Per the "orchestrator matches routing target" convention, the `planning` synthesis (which would route to Opus) was handled in-conversation rather than via `route()`. No `route(planning)` call appears in this session's metrics; the cost saved is the single largest item.
- Notes for next-session calibration: Verification took 3 rounds; ALL findings (5 in round 1, 2 in round 2) were artifact-drift defects (table-vs-diagram, output-path inconsistency, count-vs-list mismatch, column-number reference) -- ZERO scope/coverage findings against the actual decomposition logic. Round 1: 3 Major + 2 Medium (fixture-gap ownership, case-08-overclaim, DAG vs table drift, column-number drift, jet-sql-parser-and-resolution should split). Round 2: 1 Major + 1 Medium (residual DAG drift, 3-vs-4 fixture instance count). Round 3: VERIFIED (clean). The dominant cost lever for document sessions on this generator set is **mechanical-correspondence between sections of the same document** (single source of truth; numbers; column references; diagram-vs-table edges). Session 3 generates 18 stub specs which all reference the tree by slug/edge/path -- this risk is amplified, not reduced, in Session 3.

**Next session orchestrator recommendation (Session 3):**
Claude Opus 4.7 @ effort=high

**Rationale:** This session orchestrates 18 distinct but related planning calls, creating a high risk for the same class of artifact-drift defects that required three verification rounds in Session 2. A high-tier orchestrator is required to maintain context across the entire generation loop, ensuring consistency between the source tree and all 18 generated stubs to minimize expensive verification cycles.

## Session 3 of 4: Stub Specs

**Recommended orchestrator:** Claude Opus 4.7 @ effort=high *(updated from Session 2's actuals -- original recommendation was Sonnet 4.6 @ medium; see Session 2's "Next session orchestrator recommendation" block above. The escalation reflects that the verifier on this generator set has been strict on artifact-drift defects in both prior sessions, and Session 3 multiplies the drift surface area 18×.)*
**Rationale:** This session orchestrates 18 routed `planning` calls (one per child session set named in `session-set-tree.md`). The actual planning task is repetitive but the orchestrator's job is non-trivial: each stub must reference the tree's slugs, edges, and output paths verbatim, and stubs must be internally consistent with each other (per Session 1 retrospective adopt-now `#2`/`#3`). Session 2 needed 3 verification rounds purely on artifact-drift -- Session 3 has 18× the surface area for that class of defect. Opus context fidelity is justified by the cost-of-defect, not by the per-call reasoning depth. Note: `planning` task_type pins to Opus, so per the "orchestrator matches routing target" convention, the per-stub work can be handled in-conversation rather than via 18 separate `route()` calls.
**Estimated routed cost:** $1.50 - $3.00 (assuming most stub generation is in-conversation; ~$0.50 for verification, ~$1-2.50 if any stubs need re-routing)

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
