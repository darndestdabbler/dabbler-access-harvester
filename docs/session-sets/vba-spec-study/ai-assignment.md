# AI Assignment Ledger — vba-spec-study

> **Authored:** 2026-04-25 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals" block under each session row once it completes, and updates the *next* session's row if the actuals reveal a miscalibration.

## Session 1 of 3: MS-VBAL Summary

**Recommended orchestrator:** Claude Code Sonnet 4.6 @ effort=medium
**Rationale:** The session is dispatch-heavy, involving multiple `route()` calls and file system operations. Sonnet is the cost-effective choice for orchestrating these file/shell-heavy tasks, reserving budget for the single high-complexity synthesis call.
**Estimated routed cost:** $1.25

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Orchestrator setup, file I/O | Orchestration | N/A (local) | Claude Code Sonnet 4.6 | low | Mechanical setup and file handling. |
| Synthesize MS-VBAL summary | Synthesis | `architecture` | Claude 3 Opus | max + adaptive | Per spec, this requires a top-tier model for high-fidelity technical prose generation. Will be broken into sub-calls to manage context size. |
| Run baseline build/test | Orchestration | N/A (local) | Claude Code Sonnet 4.6 | low | Mechanical shell command dispatch. |
| Verify session output | Verification | `session-verification` | gpt-5-4 | N/A | Task type is pinned to gpt-5-4 for maximum accuracy in verifying session outcomes against the plan. |

**Actuals (Session 1 — 2026-04-25):**
- Orchestrator used: Claude Code Sonnet 4.6 (this conversation)
- Total routed cost: $2.10 (ai-assignment $0.015 via gemini-pro; synthesis 3x $0.60 via opus; session-verification 3 rounds $0.28 via gpt-5-4)
- Deviations from recommendation: Spec called for 1 architecture synthesis call; split into 3 focused calls (~1000 input tokens each) per lessons-learned Opus timeout warning. No timeouts occurred. Cost ~$1.80 for synthesis (3x actual Opus calls) vs. estimated $1.25 total routed — over by $0.85, primarily due to 3-round verification needed for context-gap management.
- Notes for next-session calibration: Three-round verification was context-management overhead, not content quality. For large documents, structure the verification prompt to send one section at a time OR send the full document if under 30K chars. The 3x synthesis split worked well; no timeouts. Architecture synthesis at ~1000 input tokens = ~$0.60/call with Opus.

**Next session orchestrator recommendation (Session 2):**
Claude Code Sonnet 4.6 @ effort=low

**Rationale:** Session 2's primary task is large-context mechanical data transformation (CSV generation from the 84K summary + 12 fixtures), not creative synthesis. `effort=low` is sufficient; no Opus architecture calls needed this session.

## Session 2 of 3: Coverage Matrix

**Recommended orchestrator:** Claude Code Sonnet 4.6 @ effort=medium
**Rationale:** This session is almost entirely mechanical: reading structured data, transforming it into a CSV, and running a correspondence check. Sonnet is the ideal orchestrator and can also handle the routed validation task.
**Estimated routed cost:** $0.15

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Read inputs & build matrix | Orchestration | N/A (local) | Claude Code Sonnet 4.6 | medium | The orchestrator itself will perform the structured data transformation from markdown/fixtures to CSV. |
| Validate matrix coverage | Verification | `schema-validation` | Claude 3.5 Sonnet | N/A | Mechanical correspondence check is pinned to a cost-effective Sonnet-class model. |
| Land CSV & commit | Orchestration | N/A (local) | Claude Code Sonnet 4.6 | low | Mechanical file I/O and git operations. |
| Verify session output | Verification | `session-verification` | gpt-5-4 | N/A | Task type is pinned to gpt-5-4 for final session verification. |

**Actuals (filled after the session runs):**
- Orchestrator used: TBD
- Total routed cost: TBD
- Deviations from recommendation: TBD
- Notes for next-session calibration: TBD

## Session 3 of 3: Hazard List Synthesis

**Recommended orchestrator:** Claude Code Sonnet 4.6 @ effort=medium
**Rationale:** Similar to Session 1, this is a dispatch-heavy session with a single, critical synthesis call. Sonnet provides the most cost-effective orchestration.
**Estimated routed cost:** $0.95

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Read fixtures with hazard lens | Orchestration | N/A (local) | Claude Code Sonnet 4.6 | low | Mechanical file I/O. |
| Synthesize hazard list | Synthesis | `architecture` | Claude 3 Opus | max + adaptive | Per spec, identifying subtle edge cases and parsing hazards from code fixtures requires a top-tier model. |
| Generate change-log | Summarization | `summarization` | Claude 3.5 Sonnet | N/A | Standard summarization suitable for a cost-effective Tier 1 model (`max_tier=1`). |
| Verify session output | Verification | `session-verification` | gpt-5-4 | N/A | Task type is pinned to gpt-5-4 for final verification. |
| Recommend next session-set | Analysis | `analysis` | Claude 3.5 Sonnet | N/A | Strategic planning requires a capable model; Sonnet is a good balance of cost and capability. |

**Actuals (filled after the session runs):**
- Orchestrator used: TBD
- Total routed cost: TBD
- Deviations from recommendation: TBD
- Notes for next-session calibration: TBD

## Session 4 of 3 (optional buffer): Verifier-Surfaced Cleanup

**Recommended orchestrator:** Claude Code Sonnet 4.6 @ effort=medium
**Rationale:** This session is reactive. A Sonnet orchestrator is best suited to handle the likely mix of code/doc edits, re-running tasks, and final verification.
**Estimated routed cost:** Variable (dependent on findings)

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Address verifier findings | Corrective Action | Varies | Varies | Varies | Model and effort will depend on the specific finding. A simple doc fix might use Sonnet; a flawed synthesis would re-route to Opus. |
| Final session verification | Verification | `session-verification` | gpt-5-4 | N/A | Final check after all corrections are applied. |

**Actuals (filled after the session runs):**
- Orchestrator used: TBD
- Total routed cost: TBD
- Deviations from recommendation: TBD
- Notes for next-session calibration: TBD