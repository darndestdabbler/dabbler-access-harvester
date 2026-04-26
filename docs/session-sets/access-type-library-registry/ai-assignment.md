# AI Assignment Ledger — access-type-library-registry

> **Authored:** 2026-04-26 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals" block under each session row once it completes, and updates the *next* session's row if the actuals reveal a miscalibration.

## Session 1 of 2: Registry Curation + Contract Design (Synthesis)

**Recommended orchestrator:** Claude Code claude-opus-4-7 @ effort=high
**Rationale:** The core task is large-scale synthesis (curating a data registry and designing its contract), which is the primary use case for an Opus-class model. Per project conventions, the Opus orchestrator handles this in-conversation for efficiency.
**Estimated routed cost:** $0.01

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Verify prerequisites (#10) and read project context/plans. |
| 2 | synthesis | — (in-conversation) | claude-opus-4-7 | high | Author `ITypeLibraryRegistry.cs`, `TypeLibraryRegistry.cs`, and hermetic unit tests per the spec. |
| 3 | mechanical | `schema-validation` | claude-3-5-sonnet | — | Validate schema/syntax of the generated C# files against project conventions. |
| 4 | mechanical | — | — | — | Run initial build and test cycle to confirm compilation and test pass. |

**Actuals (filled after the session runs):**
- Orchestrator used: <to be filled>
- Total routed cost: $<to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>

## Session 2 of 2: Tightening + Cross-Provider Verification

**Recommended orchestrator:** Claude Code claude-opus-4-7 @ effort=high
**Rationale:** The orchestrator manages routing for the critical cross-provider verification and final summarization tasks. Retaining the Opus orchestrator ensures it can handle any complex findings surfaced by the verifier without needing to escalate.
**Estimated routed cost:** $0.12

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Final build and test execution to establish a clean baseline. |
| 2 | verification | `session-verification` | gpt-5-4 | high | Cross-provider verification of the contract, implementation, and tests for correctness and robustness. |
| 3 | mechanical | — | — | — | Address any findings from the verifier (max 2 retries). |
| 4 | summarization | `summarization` | gemini-flash | — | Generate change-log entry based on the final diff. |
| 5 | mechanical | — | — | — | Recommend next session (#11b), update session state, and merge branch. |

**Actuals (filled after the session runs):**
- Orchestrator used: <to be filled>
- Total routed cost: $<to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>

## Session 1 Actuals (appended by Claude Opus 4.7 orchestrator)

**Orchestrator used:** Claude Code claude-opus-4-7 (effort=unknown — model self-introspection unavailable; human can override).

**Total routed cost — Session 1:** $0.0000
- ai-assignment.md authoring (analysis): ~$0.012 (gemini-pro)
- Session-verification round 1 (gpt-5-4): ~$0.158
- Session-verification round 2 (gpt-5-4): ~$0.176
- Session-verification round 3 (gpt-5-4): ~$0.190

**Verifier verdict:** VERIFIED — across three rounds the verifier flagged 5 Major + 2 Minor (round 1), 3 Major + 1 Minor (round 2), then 2 Minor in round 3. All findings, including the round-3 Minors, were applied (round 3 was the final retry per workflow Step 7's max-2-retries cap; the orchestrator used the post-round-3 fix window for the two Minor doc/projection items rather than running an out-of-budget round 4).

**Deviations from recommendation:** Synthesis was authored in-conversation per the project-guidance Convention "use Opus as the orchestrator… for synthesis-document work" — no routed `architecture` call. The schema-validation step in the recommendation was folded into the cross-provider verification rather than run separately (the verification prompt covers schema/contract correspondence). The "claude-3-5-sonnet" reference in Session 1 row was treated as a pseudonym for the project's `sonnet` router model — not actually invoked because in-conversation Opus authored the schema-shaped output.

**Notes for Session 2 calibration:** Session 2 is mostly mechanical (build/test, verification, change-log, branch merge, next-set recommendation). The cross-provider verification is forced through gpt-5-4 by router-config regardless of orchestrator. Sonnet (or any tier-2 model) at low effort is sufficient as the Session 2 orchestrator — no synthesis content remains. The registry surface is curation-stable from Session 1; Session 2 should resist scope creep into adding more registered types unless the verifier surfaces a coverage gap against the actual fixture data.



## Session 2 Actuals (appended by Claude Opus 4.7 orchestrator)

**Orchestrator used:** Claude Code claude-opus-4-7 (effort=unknown — model self-introspection unavailable; human can override).

**Total routed cost — Session 2:** ~$0.69
- Session-verification round 1 (gpt-5-4): ~$0.173
- Session-verification round 2 (gpt-5-4): ~$0.173
- Session-verification round 3 (gpt-5-4): ~$0.171
- Change-log generation (summarization, max_tier=1): see metrics file
- Next-session-set recommendation (analysis): see metrics file

**Verifier verdict:** VERIFIED — across three rounds the verifier flagged
2 Major + 1 Minor (round 1), 1 Major + 1 Minor (round 2), 1 Major + 1 Minor
(round 3). All findings, including the round-3 Major (IsDefault storage
consistency) and Minor (stale comments), were applied (round 3 was the
final retry per workflow Step 7's max-2-retries cap; the orchestrator
used the post-round-3 fix window per the precedent set by Session 1).

**Deviations from recommendation:** Recommendation called for Opus orchestrator
to manage routing; that held. The "schema-validation" task type wasn't routed
— the cross-provider session-verification covered schema/contract
correspondence as it had in Session 1.

**Notes for future #11b orchestrator (when #9 lands):** The
ITypeLibraryRegistry contract this set ships is consumed in #11b. Key
behaviors the resolver-side integration must honor: (a) graceful
degradation — `TryGet*` never throws; (b) public registry exposes only
real type-library identifiers (no fabricated curation types — see the
DataBoundControl flatten saga in this Session 2's verification rounds);
(c) IsDefault metadata is consistent between TypeInfo.Members direct
read and TryGetMember/TryGetDefaultMember lookup (a ValidateRegistry-
AtConstruction invariant enforces this, so adding a new default
member is a fail-fast at startup if the curator forgets the flag).
