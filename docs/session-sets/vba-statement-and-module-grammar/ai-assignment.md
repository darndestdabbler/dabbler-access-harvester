# AI Assignment Ledger - vba-statement-and-module-grammar

> **Authored:** 2026-04-26 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals"
> block under each session row once it completes, and updates the next
> session's row if the actuals reveal a miscalibration.

---

## Session 1 of 4: Statement Grammar

**Recommended orchestrator:** `opus-4.7` @ effort=`high`

**Rationale:** This session's core task is synthesizing a novel ANTLR4 statement grammar, a high-complexity task. Repo conventions mandate using Opus as the orchestrator for in-conversation authoring to avoid timeouts, mirroring the successful pattern from the prerequisite set.

**Estimated routed cost:** ~$0.65

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Elaborate step list | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Orchestrator must expand the spec's stub plan into a runnable sequence. |
| Synthesize statement grammar | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Core synthesis task; per repo constraints, this is done in-conversation. |
| Author targeted unit tests | Synthesis | n/a (in-conversation) | `opus-4.7` | `medium` | Test authoring requires reasoning about grammar edge cases and fixtures. |
| File/git/build/test ops | Mechanical | n/a (orchestrator) | `opus-4.7` | `low` | Standard mechanical work driven by the orchestrator. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used: Claude Code Opus 4.7 (1M context). Statement-grammar synthesis authored in-conversation per the "When the orchestrator's model matches the routing target..." Convention.
*   Total routed cost: ~$0.09 (analysis $0.023 authoring this ledger + Gemini Pro verification rounds 1+2 $0.07 + next-orchestrator analysis ~tbd). Two `gpt-5-4` `session-verification` calls timed out before fallback to Gemini Pro; no charge from the failures (httpx read timeout, no usage reported).
*   Deviations from plan:
    - **Verifier swapped to `gemini-pro`** mid-session after the configured cross-provider verifier (`gpt-5-4`) hit the router's 600s read timeout twice in succession. Per the human's escalation ladder: try the same provider twice, then switch to the remaining cross-provider verifier. Verification completed in two Gemini Pro rounds (R1: 3 Minor coverage gaps fixed; R2: 3 more Minor coverage gaps fixed and committed without R3 since the verifier was strictly enumerating additive test cases, not surfacing grammar-correctness issues).
    - Added `DEBUG # DebugObjectExpr` alternative to `primary` so `Debug.Print "..."` parses as a member-access call. The lexical-section `DEBUG` keyword token was authored in the prereq set; the expression-section primary list did not include it, which left fixture-07-style `Debug.Print` calls unable to start an l-expression chain.
    - Test entry point `statementListEof` added; the existing `module : moduleItem* EOF` placeholder is left untouched (Session 2 replaces it with real declarations).
*   Notes for next-session calibration:
    - Session 2's `opus-4.7` @ effort=`high` recommendation stands; module-grammar synthesis (procedure declarations with parameter lists, Type/Enum, class-level Implements/WithEvents/Public Event, Class_Initialize/Terminate, Form/Report event signatures, Option directives, Declare/PtrSafe) is comparable scope to this session and is the second of the two synthesis sessions called out in the spec.
    - **Verifier reliability lesson:** `gpt-5-4` at `reasoning_effort: high` on ~20K-token verification prompts is currently unreliable in this repo — Gemini Pro completed the same prompt in ~30s and produced sharp findings. Consider routing `session-verification` for the next Opus-orchestrated session through `gemini-pro` from the start (override `task_type_overrides.session-verification: gpt-5-4` -> `gemini-pro` for Session 2 only, OR keep gpt-5-4 with the documented two-strikes-then-fall-back rule).
    - The `statement` rule's call-vs-let disambiguation (`letStatement : LET? lExpression EQ expression` listed before `callStatement : lExpression bareCallArgumentList?`) parsed every fixture-witnessed form correctly, but the call statement vs. expression-statement collision is the single subtle area in the grammar — Session 3 (mechanical tightening) should run the harness and look for any false-positive parses (e.g., `Foo` parsing as a call when it's actually a typo for an undeclared variable assignment).

---

## Session 2 of 4: Module Grammar

**Recommended orchestrator:** `opus-4.7` @ effort=`high`

**Rationale:** Like Session 1, this session synthesizes a complex, novel ANTLR4 module grammar. The high-complexity synthesis work mandates using Opus as the orchestrator for in-conversation authoring, per established repo conventions.

**Estimated routed cost:** ~$0.65

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Elaborate step list | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Orchestrator must expand the spec's stub plan into a runnable sequence. |
| Synthesize module grammar | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Core synthesis task; per repo constraints, this is done in-conversation. |
| Author targeted unit tests | Synthesis | n/a (in-conversation) | `opus-4.7` | `medium` | Test authoring requires reasoning about module-level constructs and fixtures. |
| File/git/build/test ops | Mechanical | n/a (orchestrator) | `opus-4.7` | `low` | Standard mechanical work driven by the orchestrator. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used:
*   Total routed cost:
*   Deviations from plan:
*   Notes for next-session calibration:

---

## Session 3 of 4: Cross-Cut + Tightening

**Recommended orchestrator:** `sonnet-4.6` @ effort=`medium`

**Rationale:** The work is primarily mechanical: running a test harness, analyzing failures, and applying targeted grammar fixes. This workload is a proven fit for Sonnet, and the `schema-validation` task is explicitly pinned to Sonnet per repo constraints.

**Estimated routed cost:** ~$0.35

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Run harness & fix bugs | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `medium` | Orchestrator drives shell tools, parses output, and edits files to fix bugs. |
| Validate against matrix | Routed | `schema-validation` | `sonnet-4.6` | n/a | Mechanical correspondence check; pinned to Sonnet per constraints. |
| Land commits & log entry | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `low` | Standard file and git operations. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used:
*   Total routed cost:
*   Deviations from plan:
*   Notes for next-session calibration:

---

## Session 4 of 4: Verifier-Surfaced Cleanup

**Recommended orchestrator:** `sonnet-4.6` @ effort=`low`

**Rationale:** This session involves addressing specific, documented verifier findings and generating a change-log. These are low-complexity mechanical edits and a standard summarization task, making the cost-effective Sonnet a safe choice.

**Estimated routed cost:** ~$0.30

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Resolve verifier findings | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `medium` | Applying targeted fixes based on structured verifier feedback. |
| Generate change-log note | Routed | `summarization` | `gemini-pro` | n/a | Standard narrative generation; spec allows fallback to `max_tier=1`. |
| File/git/build/test ops | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `low` | Final mechanical cleanup, commit, and branch merge. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used:
*   Total routed cost:
*   Deviations from plan:
*   Notes for next-session calibration: