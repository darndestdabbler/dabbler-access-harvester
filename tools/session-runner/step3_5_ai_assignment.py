"""Step 3.5: Author ai-assignment.md for vba-grammar-validation Session 1."""
import os
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = "docs/session-sets/vba-grammar-validation"
log = SessionLog(SESSION_SET)

ai_assignment_prompt = """
# AI Assignment Ledger Authoring Request — vba-grammar-validation

Produce a complete AI Assignment Ledger markdown document for the session set `vba-grammar-validation`, Session Set #5 in the VBA Parser migration tree.

## Session Set Overview

**Slug:** vba-grammar-validation
**Plan phase:** Phase 1.5-1.6 corpus validation + edge case coverage; authors edge-case fixtures
**Prerequisites:** #4 vba-statement-and-module-grammar (COMPLETE)
**Sessions (rough):** 3-4
**Synthesis count:** 1 (edge-case-driven grammar fix loop)
**Outputs:** Grammar bug-fix commits in `AccessHarvester.Vba.Grammar/Vba.g4`, `docs/spec-coverage/parse-failure-log.md`, edge-case fixtures under `docs/test-cases/edge/`

## Session Plan

### Session 1 of 3-4: Edge-Case Fixture Authoring
**Steps:**
1. Register session start in session-state.json
2. Read hazard list and 12 fixture coverage to identify non-covered hazards
3. For each non-covered hazard (expected: 5-10 fixtures), route test-generation to produce a paired .bas + .expected.json fixture
4. Land fixtures under docs/test-cases/edge/
5. Build and test (dotnet build && dotnet test)
6. Route session-verification (cross-provider)
7. Next-orchestrator recommendation (routed analysis)
8. Commit, push, notify

**Creates:** docs/test-cases/edge/ directory + fixture files (5-10 pairs)
**Touches:** docs/session-sets/vba-grammar-validation/activity-log.json, session-state.json
**Ends with:** All hazard-listed uncovered constructs have a paired fixture; build and tests pass; verified by cross-provider.
**Progress keys:** session-1/author-ai-assignment, session-1/fixture-<hazard-id>, session-1/build-test, session-1/session-verification

### Session 2 of 3-4: Grammar Fix Loop (Synthesis Session)
**Steps:**
1. Run harness against full corpus (12 module-level + edge fixtures)
2. Classify every failure (grammar bug / missing construct / malformed / ambiguous)
3. For non-trivial grammar bugs requiring rule-shape decisions, route architecture synthesis (1 synthesis call aggregating all decision points)
4. Implement grammar fixes in Vba.g4
5. Re-run harness; iterate until 100% pass on all fixtures
6. Build and test
7. Route session-verification
8. Next-orchestrator recommendation
9. Commit, push, notify

**Creates:** (none new -- edits Vba.g4 in place)
**Touches:** AccessHarvester.Vba.Grammar/Vba.g4, docs/spec-coverage/parse-failure-log.md (initial pass)
**Ends with:** 100% parse on 12 module-level + all edge fixtures; verified.
**Progress keys:** session-2/harness-run, session-2/grammar-fix-synthesis, session-2/grammar-fix-impl, session-2/build-test, session-2/session-verification

### Session 3 of 3-4: Parse-Failure Log + Cross-Cut
**Steps:**
1. Finalize docs/spec-coverage/parse-failure-log.md with all failures, classifications, resolutions
2. Route schema-validation (Sonnet-pinned, mechanical correspondence) to verify every "fixed" entry maps to a real commit
3. Build and test (final verification)
4. Route session-verification
5. Last-session work: change-log, next-session-set recommendation
6. Commit, push, notify
7. Step 9: Reorganization review

**Creates:** change-log.md
**Touches:** docs/spec-coverage/parse-failure-log.md
**Ends with:** parse-failure-log.md complete; change-log.md committed; verified.
**Progress keys:** session-3/parse-failure-log, session-3/schema-validation, session-3/build-test, session-3/session-verification

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup
Reserved for resolving cross-provider verifier findings from Sessions 1-3.

## Routing Notes (from spec)

- Session 1 fixture authoring: route(task_type="test-generation", complexity_hint=40) per hazard -- mechanical, small calls
- Session 2 grammar fix synthesis: route(task_type="architecture", complexity_hint=80) for design-shape decisions
- Session 3 log validation: route(task_type="schema-validation") -- mechanical-correspondence; Sonnet-pinned per convention
- Session verification: route(task_type="session-verification", complexity_hint=70)
- Change-log: route(task_type="summarization") with max_tier=1 fallback

## Calibration Philosophy to Apply

- Mechanical work (file creation, fixture landing, dispatching, logging) -> lowest viable orchestrator. Sonnet 4.6 at low/medium effort is sufficient.
- Session 1 (fixture authoring) is primarily mechanical dispatch + file I/O -- Sonnet-level orchestrator is appropriate.
- Session 2 (synthesis) has 1 architecture call to Opus -- requires an orchestrator that can interpret and implement the architecture call output in Vba.g4. Sonnet can do this, but Opus reduces ping-pong risk per prior session-set convention. Prior completed set (vba-statement-and-module-grammar change-log) explicitly recommends Opus 4.7 @ effort=medium for this set.
- Session 3 (parse-failure log + change-log) is mechanical prose + routing -- Sonnet sufficient.
- Session 4 (buffer, if needed) is mechanical cleanup -- Sonnet sufficient.

## Prior Recommendation from vba-statement-and-module-grammar change-log

"Recommended orchestrator for vba-grammar-validation: claude-code Opus 4.7 @ effort=medium. Rationale: The vba-grammar-validation set has one synthesis session (edge-case-driven grammar fix loop) that requires strong analytical capability to identify corpus failures and design targeted fixtures. Sonnet can handle the mechanical sessions, but the synthesis session warrants Opus to avoid re-verification ping-pong on novel fixture design."

## Output Required

Produce the full AI Assignment Ledger markdown. For each session (1-4), include:
- Recommended orchestrator (engine, model, effort)
- 2-sentence rationale
- Estimated routed cost
- Full step table: step number, type (mechanical/reasoning), route() task_type (or --), model (or --), effort, rationale

Format exactly per the schema below:

# AI Assignment Ledger -- vba-grammar-validation

> **Authored:** 2026-04-26 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals"
> block under each session row once it completes, and updates the
> *next* session's row if the actuals reveal a miscalibration.

## Session 1 of 4: Edge-Case Fixture Authoring
...
## Session 2 of 4: Grammar Fix Loop (Synthesis)
...
## Session 3 of 4: Parse-Failure Log + Cross-Cut
...
## Session 4 of 4: Verifier-Surfaced Cleanup (optional buffer)
...
"""

print("Routing ai-assignment analysis...")
result = route(
    content=ai_assignment_prompt,
    task_type="analysis",
    complexity_hint=55,
    session_set=SESSION_SET,
    session_number=1,
)

output_path = "docs/session-sets/vba-grammar-validation/ai-assignment.md"
with open(output_path, "w", encoding="utf-8") as f:
    f.write(result.content)
print(f"ai-assignment.md written to {output_path}")
print(f"Model: {result.model_name}, Tokens: {result.input_tokens} in / {result.output_tokens} out, Cost: ${result.cost_usd:.4f}")

log.log_step(
    session_number=1,
    step_number=1,
    step_key="session-1/author-ai-assignment",
    description="Authored ai-assignment.md via route(task_type='analysis')",
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "analysis",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }]
)
print("Step logged to activity-log.json")
