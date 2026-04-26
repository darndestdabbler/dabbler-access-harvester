# AI Assignment Ledger - vba-lexical-and-expression-grammar

> **Authored:** 2026-04-25 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals"
> block under each session row once it completes, and updates the next
> session's row if the actuals reveal a miscalibration.

---

## Session 1 of 4: Lexical Grammar

**Recommended orchestrator:** `opus-4.7` @ effort=`high`

**Rationale:** The session's core task is synthesizing a novel ANTLR4 grammar from scratch, a high-complexity synthesis task. Repo constraints mandate using Opus as the orchestrator for in-conversation authoring to avoid provider timeouts on large synthesis payloads.

**Estimated routed cost:** ~$0.50 (driven entirely by the cross-provider `session-verification` call to `gpt-5-4`).

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Elaborate step list | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Orchestrator must expand the spec's stub plan into a runnable sequence. |
| Synthesize lexical grammar | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Core synthesis task; per constraints, this is done in-conversation. |
| Author targeted unit tests | Synthesis | n/a (in-conversation) | `opus-4.7` | `medium` | Test authoring requires reasoning about grammar edge cases. |
| File/git/build/test ops | Mechanical | n/a (orchestrator) | `opus-4.7` | `low` | Standard mechanical work driven by the orchestrator. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used: Claude Code Opus 4.7 (1M context). Lexical synthesis authored in-conversation per the "When the orchestrator's model matches the routing target..." Convention.
*   Total routed cost: ~$0.68 (analysis $0.02 authoring this ledger + 3 cross-provider verification rounds with gpt-5-4 totalling $0.65 + next-orchestrator analysis $0.006).
*   Deviations from plan:
    - Switched `AccessHarvester.Vba.Grammar.csproj` from `Antlr4.Runtime` 4.6.6 (placeholder echo target) to `Antlr4BuildTasks` 12.8.0 + `Antlr4.Runtime.Standard` 4.13.1 to wire actual ANTLR code generation. This implements the parser-foundations Session 3 deferred item (`ANTLR4 Code Generation: To #3`) and is required for the Session 3 harness run later in this set. Disclosed in the verification prompt.
    - Authored TYPED_IDENTIFIER lexer rule per Round-1 verifier finding R1-5 instead of the original separate-token approach for type-decl suffixes; bang stays as separate BANG token because of the bang/Single-suffix collision (Hazard H-A02).
*   Notes for next-session calibration:
    - Session 2 Opus orchestrator recommendation stands; expression-grammar synthesis (precedence climbing, bang-vs-dot chains, default-member access) is comparable scope.
    - The cross-provider verifier flagged 3 rounds of issues (1 Critical, 5 Major, 1 in-place fix at retry cap). The hazard list combined with concrete operator-vs-suffix collisions is sharp enough that gpt-5-4's verifier verdict is probably the right calibration; do not relax `complexity_hint` for Session 2.
    - The `LINE_CONT_FRAGMENT` and `IsRemBoundary()` predicate patterns landed in this session may benefit later child sets — surface to `parser-infrastructure` (#6) when preprocessor evaluation lands.

---

## Session 2 of 4: Expression Grammar

**Recommended orchestrator:** `opus-4.7` @ effort=`high`

**Rationale:** This session synthesizes a complex expression grammar with operator precedence, another high-complexity synthesis task. As with session 1, repo constraints mandate using Opus as the orchestrator for in-conversation authoring.

**Estimated routed cost:** ~$0.50 (driven entirely by the cross-provider `session-verification` call to `gpt-5-4`).

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Elaborate step list | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Orchestrator must expand the spec's stub plan into a runnable sequence. |
| Synthesize expression grammar | Synthesis | n/a (in-conversation) | `opus-4.7` | `high` | Core synthesis task accounting for precedence; done in-conversation. |
| Author targeted unit tests | Synthesis | n/a (in-conversation) | `opus-4.7` | `medium` | Test authoring requires reasoning about operator precedence and associativity. |
| File/git/build/test ops | Mechanical | n/a (orchestrator) | `opus-4.7` | `low` | Standard mechanical work driven by the orchestrator. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used: Claude Code Opus 4.7 (1M context). Expression-grammar synthesis authored in-conversation per the "When the orchestrator's model matches the routing target..." Convention.
*   Total routed cost: ~$0.57 (3 cross-provider verification rounds with gpt-5-4 totalling $0.55 + next-orchestrator analysis $0.02).
*   Deviations from plan:
    - Worktree-per-set setup (`session-set/vba-lexical-and-expression-grammar` branch + `../dabbler-access-harvester-vba-lexical-and-expression-grammar` worktree) per the 2026-04-26 lessons-learned entry. Session 1 ran on main pre-lesson; Session 2 onward runs in the worktree, which will be merged back on Session 4.
    - Round-1 verifier flagged a Major issue (`lExpression` too broad for `TypeOf` and `AddressOf` operands) that drove a real fix: introduced `procedureReference` and `referenceExpression` rules to enforce MS-VBAL §5.2.6 / §5.6.4 syntactic constraints. Round 2 surfaced a follow-on Minor (`AddressOf Me` parsed; `Me` alone is the instance, not a procedure pointer) — fixed by requiring `Me (DOT memberName)+`. Round 3 = VERIFIED.
*   Notes for next-session calibration:
    - Session 3's `sonnet-4.6` @ effort=`medium` recommendation is confirmed by the next-orchestrator analysis. Mechanical tightening (run harness, classify failures, fix grammar bugs) is a good Sonnet fit. The schema-validation route() call is already pinned to Sonnet per `task_type_overrides`.
    - The `procedureReference` and `referenceExpression` rule split is a useful pattern for #4 (statement-and-module grammar) — `Set` requires an l-value (l-expression form), `AddressOf` requires a procedure-pointer reference. Surface to #4 when assignment-statement parsing lands.
    - Cross-provider verifier is calibrated correctly for this kind of work — caught two real spec-fidelity issues that an in-conversation review would have missed. Do not relax `complexity_hint` for Session 3's verification; mechanical-tightening still benefits from the cross-provider check on the resulting grammar shape.

---

## Session 3 of 4: Mechanical Tightening

**Recommended orchestrator:** `sonnet-4.6` @ effort=`medium`

**Rationale:** The work is primarily mechanical: running a test harness, analyzing failures, and applying targeted grammar fixes. The single routed reasoning call, `schema-validation`, is explicitly pinned to the cheap/fast Sonnet model per repo constraints.

**Estimated routed cost:** ~$0.35 (driven by the cheap `schema-validation` call to `sonnet` and the more expensive cross-provider `session-verification` call to `gpt-5-4`).

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Run harness & fix bugs | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `medium` | Orchestrator drives shell tools, parses output, and edits files. |
| Validate against matrix | Routed | `schema-validation` | `sonnet-4.6` | n/a | Mechanical correspondence check; pinned to Sonnet per constraints. |
| Land commits & log entry | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `low` | Standard file and git operations. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used: Claude Sonnet 4.6 @ effort=medium (per ai-assignment.md recommendation).
*   Total routed cost: ~$0.31 (3 cross-provider verification rounds with gpt-5-4 totalling $0.31 + next-orchestrator analysis $0.01).
*   Deviations from plan:
    - The spec's "harness against 01/02/03/06/07 → classify failures → fix bugs" loop found ZERO failures: 12/12 parsed, 0 errors, 0 ERROR_TOKEN characters. No grammar bugs to fix.
    - schema-validation handled in-conversation per Convention (Sonnet orchestrator = routing target). No route() call needed.
    - The main work was adding the `parse` CLI subcommand (new, not foreseen in the spec as a step) because the harness requires a parser executable and none existed. This was a prerequisite for the harness run.
    - Verification caught 4 Minor issues across R1/R2/R3: bare-file heuristic, encoding, schema-table completeness, usage text. All resolved.
*   Notes for next-session calibration:
    - Session 4 recommendation: sonnet-4.6 @ effort=low (confirmed by next-orchestrator analysis). The work is resolving specific documented findings + generating a change-log — no synthesis needed.
    - The `parse` CLI subcommand is now the harness driver for all subsequent grammar sessions. Session 4 and vba-statement-and-module-grammar (#4) both inherit it.
    - The parse-failure-log.md baseline entry will be the "from" state for #4's tightening session. Make sure #4 references it.

---

## Session 4 of 4: Verifier-Surfaced Cleanup + Cross-Set Handoff

**Recommended orchestrator:** `sonnet-4.6` @ effort=`low` (updated from medium by next-orchestrator analysis after Session 3)

**Rationale:** This session involves addressing specific, documented issues and generating a brief change-log. These are mechanical edits and a standard summarization task, making the cost-effective Sonnet a safe choice for orchestration.

**Estimated routed cost:** ~$0.25 (driven by a cheap `summarization` call to `gemini-pro` and the cross-provider `session-verification` call to `gpt-5-4`).

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| Resolve verifier findings | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `medium` | Applying targeted fixes based on structured verifier feedback. |
| Generate change-log note | Routed | `summarization` | `gemini-pro` | n/a | Standard narrative generation; spec allows fallback to `max_tier=1`. |
| File/git/build/test ops | Mechanical | n/a (orchestrator) | `sonnet-4.6` | `low` | Final mechanical cleanup and commit. |
| Final session verification | Routed | `session-verification` | `gpt-5-4` | n/a | Non-negotiable cross-provider check per repo constraints. |

**Actuals (filled after the session runs):**
*   Orchestrator used:
*   Total routed cost:
*   Deviations from plan:
*   Notes for next-session calibration: