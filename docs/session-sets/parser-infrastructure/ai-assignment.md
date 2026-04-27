# AI Assignment Ledger — parser-infrastructure

> **Authored:** 2026-04-26 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals"
> block under each session row once it completes, and updates the
> *next* session's row if the actuals reveal a miscalibration.

## Session 1 of 4: Error-Recovery Strategy (Synthesis)

**Recommended orchestrator:** claude-code Opus 4.7 @ effort=max
**Rationale:** This session's core is a large `architecture` synthesis call. Using an Opus orchestrator allows this to be handled reliably in-conversation, avoiding potential timeouts when routing large contexts from Sonnet.
**Estimated routed cost:** $0.40

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Read plan, parse-failure log, and sample fixtures. |
| 2 | reasoning | architecture | in-conversation | max | Synthesize the error recovery strategy and lossy-parse-mode design. |
| 3 | mechanical | — | — | — | Author and land the error listener and sync-token recovery code. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Non-negotiable cross-provider gate for session integrity. |

**Actuals (filled after the session runs):**
- Orchestrator used: claude-code Opus 4.7 (effort: unknown — cannot self-introspect).
- Total routed cost: $0.2796 (vs. $0.40 estimate — under budget).
  - ai-assignment authoring (gemini-pro): $0.0180
  - session-verification round 1 (gpt-5-4): $0.1442
  - session-verification round 2 (gpt-5-4): $0.1285
  - tiebreaker (gemini-pro): $0.0069
  - next-orchestrator recommendation (gemini-pro): $0.0020 (approx)
- Verifier verdicts: round-1 ISSUES_FOUND (3 Major + 2 Minor); round-2 ISSUES_FOUND
  (1 Major outstanding: R1-I3 STATIC sync); tiebreaker (gemini-pro)
  SIDE_WITH_ORCHESTRATOR; R1-I3 deferred per synthesis doc §6.
- Deviations from recommendation: none. Architecture synthesis was authored
  in-conversation per the convention "When the orchestrator's model matches the
  routing target..." and stayed under the timeout window.
- Notes for next-session calibration: Session 1 needed 1 disagreement-procedure
  cycle (tiebreaker route to gemini-pro). For Session 2's AST shape work, expect
  the in-conversation synthesis to be larger (~12-18K output tokens vs. ~10K here).
  Recommend attaching the synthesis doc directly to the session-verification prompt
  (not just code + step list) to avoid context-gap repeats of the round-2 dispute.

**Next session orchestrator recommendation (Session 2):**
claude-code Opus 4.7 @ effort=max

**Rationale:** Session 2's scope is a single, large architecture synthesis for the AST, which is even larger than the pipeline from Session 1. This session confirmed an Opus-class model is required to reliably handle the token scale of these foundational synthesis calls.

## Session 2 of 4: AST Shape (Synthesis)

**Recommended orchestrator:** claude-code Opus 4.7 @ effort=max
**Rationale:** Like Session 1, this session centers on a large `architecture` synthesis call for the AST hierarchy. An Opus orchestrator is required for reliable in-conversation synthesis of this scope.
**Estimated routed cost:** $0.40

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Read plan and 12 existing `expected.json` fixture shapes. |
| 2 | reasoning | architecture | in-conversation | max | Synthesize the AST node hierarchy and visitor infrastructure. |
| 3 | mechanical | — | — | — | Author and land the AST types and visitor infrastructure code. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Non-negotiable cross-provider gate for session integrity. |

**Actuals (filled after the session runs):**
- Orchestrator used: claude-code Opus 4.7 (effort: unknown — cannot self-introspect).
- Total routed cost: ~$0.0791
  - session-verification round 1 (gemini-pro fallback after gpt-5-4 timed out 3x): $0.0588
  - session-verification round 2 (gemini-pro): $0.0188
  - next-orchestrator recommendation (gemini-pro): $0.0015
- Verifier verdicts: round-1 ISSUES_FOUND (1 Major MAJ-2.1 + 1 Minor MIN-2.1 + 1 Nit);
  round-2 VERIFIED.
- Deviations from recommendation:
  - Architecture synthesis authored in-conversation per the convention "When the
    orchestrator's model matches the routing target..." — same pattern as Session 1.
  - **Verifier failover:** gpt-5-4 timed out 3x in a row on the ~43K-token
    verification prompt; per `project-guidance.md` Verifier-failure escalation
    ladder Step 2, fell back to `gemini-pro` (still cross-provider). Worked
    cleanly. Same failover as `vba-statement-and-module-grammar` Session 1
    (2026-04-26) — second confirmed context.
- Notes for next-session calibration: Session 3's scope is largely mechanical
  (SourceLocation/SourceRange threading + preprocessor parsing rule) with one
  routed test-generation call. Sonnet 4.6 @ effort=medium is appropriate.
- Round-2 verifier output was unusually terse (gemini-pro returned just
  "VERIFIED" with 2 output tokens). Acceptable per the prompt's
  "Conclude with one of:" framing, but worth noting for calibration if
  the same pattern repeats.

**Next session orchestrator recommendation (Session 3):**
claude-code Sonnet 4.6 @ effort=medium

**Rationale:** The session's dominant task is a large-scale, mechanical
refactoring suitable for a mid-tier model, making Sonnet the most cost-
effective choice. The remaining feature work is well-scoped and does not
require a flagship model like Opus.

## Session 3 of 4: Position Tracking + Preprocessor Pass-Through

**Recommended orchestrator:** claude-code Sonnet 4.6 @ effort=medium
**Rationale:** This session is primarily mechanical code threading, with one routed reasoning call for test generation. Sonnet is the cheapest-safe orchestrator for mechanical work.
**Estimated routed cost:** $0.65

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Author and thread `SourceLocation`/`SourceRange` types through the AST. |
| 2 | reasoning | test-generation | gemini-pro | medium | Author preprocessor fixture pair; `complexity_hint=40` aligns with tier picker default. |
| 3 | mechanical | — | — | — | Land preprocessor directive AST node and associated parsing rule. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Non-negotiable cross-provider gate for session integrity. |

**Actuals (filled after the session runs):**

## Session 4 of 4: Cross-Cut + Mechanical Tightening

**Recommended orchestrator:** claude-code Sonnet 4.6 @ effort=medium
**Rationale:** This session is almost entirely mechanical (running tests, git ops) with routed checks. Sonnet is the appropriate and most cost-effective choice.
**Estimated routed cost:** $0.52

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Run test harness against the full 13-case corpus. |
| 2 | reasoning | code-review | sonnet | medium / adaptive | Per convention, `schema-validation` maps to this for mechanical correspondence check. |
| 3 | mechanical | — | — | — | Land tightening commits based on test and validation results. |
| 4 | reasoning | session-verification | gpt-5-4 | high | Non-negotiable cross-provider gate for session integrity. |
| 5 | reasoning | summarization | gemini-flash | low | Generate change log summary as final step. |

**Actuals (filled after the session runs):**

## Session 5 (optional buffer): Verifier-Surfaced Cleanup

**Recommended orchestrator:** claude-code Sonnet 4.6 @ effort=medium
**Rationale:** This session is contingent and involves implementing specific fixes from a verifier report. This is purely mechanical work, suited for the Sonnet workhorse.
**Estimated routed cost:** $0.40

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | Ingest and parse verifier feedback from Session 4. |
| 2 | mechanical | — | — | — | Implement code fixes based on the feedback. |
| 3 | reasoning | session-verification | gpt-5-4 | high | Final non-negotiable cross-provider gate on the fixes. |

**Actuals (filled after the session runs):**

## Set-wide notes

- **Synthesis sessions vs. mechanical sessions:** S1 and S2 are synthesis-heavy
  (architecture decisions). S3 mixes mechanical (position-tracking threading) and
  reasoning (test-generation for the preprocessor fixture). S4 is almost entirely
  mechanical.
- **Cross-provider verification posture:** `session-verification` always goes through
  `route()` regardless of orchestrator (the cross-provider second opinion is the only
  quality gate in this repo, no human UAT).
- **Known cost trap:** Opus@max + adaptive thinking on `architecture` prompts above
  ~3K input tokens is unreliable from a Sonnet orchestrator (timeouts). Either
  promote the orchestrator to Opus for those sessions and author in-conversation, or
  pre-decompose the architecture prompt.