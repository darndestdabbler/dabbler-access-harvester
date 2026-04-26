# AI-Led Session-Set Workflow — Dabbler Access Harvester

This document describes the orchestration pattern used to develop features in
this repository. An AI coding agent (Claude Code, Codex, or a Gemini-based
tool) acts as the **orchestrator**, executing a predefined session plan one
session at a time. A separate **AI Router** Python module routes reasoning
tasks to cheaper external models and enforces cross-provider verification.

This document is the single source of truth for the session-set workflow
itself — procedure, rules, router usage, verification, and session-set
close-out. `CLAUDE.md`, `AGENTS.md`, and `GEMINI.md` provide only
agent-specific bootstrap (API key export, router import snippet) and point
here for everything else.

The orchestrator can change from session to session at the human's discretion.
All three orchestrators follow the same workflow — only the instruction file
they read differs.

Read `docs/planning/project-guidance.md` and
`docs/planning/lessons-learned.md` before every session and before changing
architecture, parser strategy, output schema, or workflow assets.

> **Note on UAT.** This project does **not** use a human-UAT checklist or a
> Playwright/E2E layer. There is no interactive UI; deliverables are
> command-line tools, a parser, and JSON outputs that can be validated
> programmatically. The end-of-session quality gate is **cross-provider AI
> verification** (Step 6 below), backed by automated builds, unit tests,
> and schema validation. Workflow lifted from sibling repos that include
> UAT-related task types (`uat-plan-generation`, `uat-coverage-review`)
> intentionally omits them here.

## Overview

```
Human
  |
  | "Start the next session"
  v
Orchestrator (Claude / Codex / Gemini)
  |
  |-- reads instruction file (CLAUDE.md / AGENTS.md / GEMINI.md)
  |-- reads project-guidance.md and lessons-learned.md
  |-- reads spec.md in the active session set
  |-- reads activity-log.json for prior progress
  |-- on first session of a set: authors ai-assignment.md
  |   (route(task_type='analysis')); on later sessions: reads it
  |
  |  For each step in the session plan:
  |    |-- file work: creates/edits files directly
  |    |-- reasoning: calls route() via ai-router Python module
  |    |     |-- router selects cheapest capable model
  |    |     |-- Gemini Flash (tier 1), Gemini Pro (tier 2),
  |    |     |   Sonnet (tier 2), or Opus (tier 3)
  |    |     +-- auto-verification for code-review/security-review
  |    |-- logs every step to activity-log.json
  |
  |-- runs dotnet build && dotnet test
  |
  |-- MANDATORY: end-of-session verification
  |     |-- sends all work to a DIFFERENT AI provider
  |     |-- saves raw verifier output (never edited)
  |     +-- fixes issues if found (max 2 retries)
  |
  |-- on last session: generates change-log.md (part of the same commit)
  |-- prints cost report
  |-- on every session except the last: routes a next-orchestrator
  |   recommendation (route(task_type='analysis')); appended to
  |   ai-assignment.md and surfaced in the final printout + notify body
  |-- commits and pushes
  |-- sends session-complete notification (if configured)
  |-- on last session ONLY (after notify):
  |                    proposes reorganization candidates for
  |                    project-guidance.md / lessons-learned.md, then
  |                    commits and pushes any accepted changes separately
  +-- STOPS (one session per conversation)
```

## Key Concepts

### Session Set

A session set is a planned body of work broken into sequential sessions. Each
session set lives in `docs/session-sets/<name>/` and contains:

| File | Purpose |
|---|---|
| `spec.md` | The full plan: goals, features, and per-session step lists |
| `ai-assignment.md` | Per-session AI-assignment ledger: recommended orchestrator (engine + model + thinking/effort) and per-step routed-task model/effort, with rationale. Authored by Session 1 of the set via `route(task_type='analysis')`; appended-to at the end of each subsequent session with that session's actuals + the recommendation for the next |
| `activity-log.json` | Machine-readable log of every step across all sessions |
| `session-reviews/session-NNN.md` | Raw verifier output for each session (never edited) |
| `session-reviews/issues-NNN.json` | Issue log if verification found problems |
| `change-log.md` | Generated after the final session; marks the set as complete |

### Session

One unit of work within a session set. Sessions are numbered (Session 1 of 5,
Session 2 of 5, etc.). Each session:

- Has a defined list of steps in `spec.md`
- Is executed by exactly one orchestrator in one conversation
- Ends with a mandatory cross-provider verification
- Produces a commit on completion

### AI Router

A Python module in `ai-router/` that routes reasoning tasks to external AI
models. The orchestrator calls `route()` instead of performing analysis,
review, or documentation itself. The router:

- Estimates task complexity (1-100 score)
- Selects the cheapest model capable of handling the complexity
- Escalates to a higher-tier model if the response is poor
- Auto-verifies certain task types using a different AI provider
- Tracks costs per session set

### Cross-Provider Verification

Every session ends with an independent verification step. The orchestrator
sends its work to a model from a **different AI provider** than the one that
did the work. This catches provider-specific biases and blind spots and is
the primary quality gate for this repo (in lieu of human UAT):

- If the orchestrator is Claude and used Gemini for routing, verification
  goes to Opus or Sonnet (Anthropic) — but the **session-verification**
  task type always routes to a non-orchestrator provider
- If the orchestrator is Codex/Gemini, verification goes to an Anthropic model
- The verifier's raw output is saved and never edited

Because there is no human-UAT layer here, treat any verifier finding with
the seriousness a human reviewer's finding would have in a UI repo. Do not
suppress findings; if the orchestrator disagrees, follow the disagreement
procedure in Step 7 — humans adjudicate, not the orchestrator.

---

## Setting Up a New Session Set

### 1. Create the Directory Structure

```
docs/session-sets/<name>/
  spec.md
```

The `activity-log.json` and `session-reviews/` directory are created
automatically by the SessionLog class on first use.

### 2. Write spec.md

The spec defines the entire body of work. Structure:

```markdown
# <Feature Name> Spec

> **Purpose:** One-sentence description
> **Created:** YYYY-MM-DD
> **Session Set:** `docs/session-sets/<name>/`
> **Prerequisite:** What must be done before starting
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification

---

## Project Overview

Describe the goals and deliverables.

## Feature 1: <Name>

### Scope
What's included.

### Standards
Rules and conventions.

---

## Session Plan

### Session 1 of N: <Title>

**Steps:**
1. Step description
2. Step description (route documentation task)
3. Step description

**Creates:** List of files created
**Touches:** List of files modified
**Ends with:** Success criteria
**Progress keys:** `session-1/step-name`, `session-1/other-step`

---

### Session 2 of N: <Title>
...
```

Key rules for the spec:
- Each session must be completable in one conversation
- Steps should be specific and actionable
- "Creates" and "Touches" sections let subsequent sessions verify prerequisites
- Progress keys are used in `log.log_step()` for tracking

### 3. Configure the AI Router

The router is configured via `ai-router/router-config.yaml`. Prompt templates
and workflow utilities live under `ai-router/prompt-templates/`. Key sections:

**Models:** Define available models with tier, pricing, and context limits.
Each model may also declare `generation_params` controlling the API-level
reasoning knobs: `effort` and `thinking` for Anthropic (Sonnet/Opus), and
`thinking_budget` (Gemini 2.5) or `thinking_level` (Gemini 3.x) for Google.
These are sent on every call, so leaving `effort` unset on Sonnet 4.6
means the API defaults to `high` — which burns tokens unnecessarily on
simple tasks. Explicitly setting `effort: medium` (or lower per task
type) is the single biggest cost lever.

```yaml
models:
  gemini-flash:
    provider: google
    model_id: gemini-2.5-flash
    tier: 1
  gemini-pro:
    provider: google
    model_id: gemini-2.5-pro
    tier: 2
  sonnet:
    provider: anthropic
    model_id: claude-sonnet-4-6
    tier: 2
  opus:
    provider: anthropic
    model_id: claude-opus-4-6
    tier: 3
```

**Tier assignments:** Map complexity tiers to models.

```yaml
routing:
  tier1_max_complexity: 30    # score <= 30 -> tier 1
  tier2_max_complexity: 65    # score 31-65 -> tier 2
  tier_assignments:
    1: gemini-flash
    2: gemini-pro      # or sonnet -- see task_type_overrides
    3: opus
  task_type_overrides:
    code-review: sonnet   # force Sonnet for code review tasks
```

**Verification:** Verifier selection is rule-based. For each routed
call, `verification.py` picks a verifier that is from a different
provider than the generator, is enabled, is enabled as a verifier,
matches the generator's tier (or one tier higher), and — among the
survivors — has the cheapest output price. Every model entry carries
`is_enabled` (generator pool) and `is_enabled_as_verifier` (verifier
pool) flags; new models join with the verifier flag off and are
promoted only after calibration data supports it.

`preferred_pairings` is an optional advisory layer: if a listed
pairing survives the rules it is used, otherwise the rules decide. The
legacy key name `cross_provider_map` is still accepted for backward
compatibility with older branches.

```yaml
verification:
  preferred_pairings:
    sonnet:       gemini-pro
    gemini-flash: gpt-5-4-mini
    gemini-pro:   gpt-5-4-mini
    opus:         gpt-5-4
    gpt-5-4-mini: sonnet
    gpt-5-4:      opus
  auto_verify_task_types:
    - code-review
    - security-review
```

**Per-task-type parameter overrides:** `task_type_params` maps each task
type to per-model parameter overrides, layered on top of each model's
`generation_params`. This is where cheap tasks like `formatting` and
`summarization` get `effort: low` and `thinking_budget: 0`, while
`architecture` and `session-verification` get deeper reasoning.

```yaml
task_type_params:
  formatting:
    sonnet:       { effort: low, thinking: { enabled: false } }
    gemini-flash: { thinking_budget: 0 }
  session-verification:
    sonnet:       { effort: high, thinking: { enabled: true, type: adaptive } }
    gemini-pro:   { thinking_budget: -1 }
```

Precedence (low → high): model-level `generation_params` →
`task_type_params` override.

> **Harvester-specific tuning notes.** Several task types in this repo are
> mechanical-correspondence checks rather than synthesis (schema
> validation of harvester output, parse-tree shape comparison against an
> expected fixture, AST-vs-source diffing). Pin those to Sonnet rather
> than Opus when added to `task_type_overrides` — see the
> `lessons-learned.md` entry on mechanical-gate calibration. Synthesis
> tasks (architecture, parser-grammar design, novel test-case authoring)
> earn Opus.

### 4. Set Environment Variables

The router requires API keys as environment variables:

```
ANTHROPIC_API_KEY   (for Claude Sonnet/Opus)
GEMINI_API_KEY      (for Gemini Flash/Pro)
OPENAI_API_KEY      (for GPT-5.4 and GPT-5.4 Mini)
PUSHOVER_API_KEY    (optional, for end-of-session phone notifications)
PUSHOVER_USER_KEY   (optional, for end-of-session phone notifications)
```

On Windows, set these as User environment variables. The orchestrator
instruction files include the commands to export them into the shell. The
notification helper also falls back to the Windows User/Machine environment
if the current process environment does not already contain the Pushover
keys.

### 5. Create the Python Virtual Environment

```bash
python -m venv .venv
.venv/Scripts/pip install pyyaml google-genai anthropic httpx
```

The router uses `httpx` directly — it does NOT require the `openai`,
`anthropic`, or `google-genai` SDKs at runtime (those are listed for
compatibility with other tools in the repo). OpenAI calls go through
the Responses API via plain HTTP.

---

## Executing a Session

When the human says **"Start the next session"**, the orchestrator follows
this exact procedure.

### Step 0: Verify API Keys And Read Guidance

Before doing anything else:

1. Read `docs/planning/project-guidance.md`
2. Read `docs/planning/lessons-learned.md`
3. Then load keys from the environment and confirm they are present

If keys are missing, stop and tell the human.

### Step 1: Identify the Active Session Set

The `find_active_session_set()` function auto-detects:
- Folder with `spec.md` + `activity-log.json` but no `change-log.md` = in-progress
- Folder with `spec.md` only = not-started (use if no in-progress exists)
- Folder with `change-log.md` = complete (skip)

```python
from ai_router.session_log import find_active_session_set, SessionLog

SESSION_SET = find_active_session_set("docs/session-sets")
log = SessionLog(SESSION_SET)
next_session = log.get_next_session_number()
```

### Step 2: Read the Spec

Open `spec.md` and find the plan for the current session number.

### Step 3: Verify Prerequisites

Confirm that files listed in prior sessions' "Creates" and "Touches" exist.

### Step 3.5: First Session Of A Set — Author `ai-assignment.md` (If Missing)

**Skip this step on Session 2+ of a set.** If
`docs/session-sets/<name>/ai-assignment.md` already exists, read it for
context and proceed to Step 4.

**On the first session of a new set, if the file does not yet exist,
produce it before executing any steps.** This is the AI-assignment
ledger: a per-session table of the cheapest AI capable of safely
handling each piece of work, including the orchestrator itself.
Authoring it via `route()` (rather than self-opining) keeps the
calibration honest — the current orchestrator is the wrong judge of
whether it could have been a smaller orchestrator.

```python
result = route(
    content=ai_assignment_prompt,   # see schema below
    task_type="analysis",
    complexity_hint=55,             # mid-tier reasoning, cross-provider verifier kicks in
    session_set=str(SESSION_SET),
    session_number=N,
)
# write the result to docs/session-sets/<name>/ai-assignment.md
log.log_step(N, step_num, "session-N/author-ai-assignment", ...)
```

**Calibration philosophy** the prompt should encode:

- **Mechanical work** (file copy, namespace rewrite, csproj boilerplate,
  git, build/test, dispatch + logging) → lowest-tier orchestrator that
  can drive file/shell tools reliably. Sonnet 4.6 at low/medium effort
  is usually sufficient. Opus 4.7 is over-spend on mechanical sessions.
- **Routed reasoning** → use the `routing.task_type_overrides` and
  `task_type_params` already in `router-config.yaml`. Override via
  `complexity_hint` only when the spec describes work materially
  larger or smaller than the task type's default.
- **Synthesis** (architecture, parser-grammar, novel test-case
  authoring, plan generation) → top tier; cheaper models burn budget
  in re-routes and verifier ping-pong.

**Schema** (the produced markdown):

```markdown
# AI Assignment Ledger — <session-set-name>

> **Authored:** <date> by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals"
> block under each session row once it completes, and updates the
> *next* session's row if the actuals reveal a miscalibration.

## Session N of M: <title>

**Recommended orchestrator:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2-sentence justification grounded in the spec's step list>
**Estimated routed cost:** $<X.XX>

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | mechanical | — | — | — | <e.g. verify prereqs> |
| 2 | reasoning | analysis | gemini-pro | dynamic | <why this fit> |
| ... | reasoning | session-verification | gpt-5-4 | high | Cross-provider gate |

**Actuals (filled after the session runs):**
- Orchestrator used: <engine + model + thinking>
- Total routed cost: $<X.XX>
- Deviations from recommendation: <any>
- Notes for next-session calibration: <any>
```

A single `analysis` route() call — typically Gemini Pro per the
default tier mapping — produces the whole ledger at once. The cost
is small (~$0.10–0.30 for a 6-session set) and the savings on later
sessions tend to be 5–20× that.

For each step in the session plan:

**File work** — create or edit files directly:
```python
log.log_step(
    session_number=N,
    step_number=step_num,
    step_key="session-N/step-name",
    description="What was done",
    status="complete",
    api_calls=[]
)
```

**Reasoning tasks** — delegate to the router:
```python
from ai_router import route

result = route(
    content="<the text to analyze>",
    task_type="code-review",       # or documentation, analysis, etc.
    context="<optional background>",
    complexity_hint=None,          # optional 1-100 override
    session_set=str(SESSION_SET),  # enables per-set metrics grouping
    session_number=N,              # enables per-session metrics grouping
)

# Use result.content, then log:
log.log_step(N, step_num, "session-N/step-name", "description", "complete", [{
    "model": result.model_name,
    "taskType": "code-review",
    "inputTokens": result.input_tokens,
    "outputTokens": result.output_tokens,
    "costUsd": result.cost_usd
}])
```

During execution:

- if the human gives a decision or instruction that looks durable enough to
  guide future sessions or future projects, ask whether it should be added
  to `docs/planning/project-guidance.md`
- if a failure or avoidable friction suggests a reusable tactic, recommend
  an update to `docs/planning/lessons-learned.md`

### Step 5: Build and Test

```bash
dotnet build
dotnet test
```

Build and test should run **sequentially**, not in parallel — file-lock
contention against the same worktree on Windows obscures real failures.
Log the result with `log.log_step()`.

For sessions that produce harvester output (JSON), also run any
schema-validation step the spec specifies (e.g., validating against
`docs/test-cases/harvester-schema.v0.json` or a future combined schema).

### Step 6: End-of-Session Verification (MANDATORY)

**The orchestrator must not verify its own work.** The `route()` function
dispatches to a different AI provider for independent review.

1. Collect all files created or modified during the session.
2. Build a verification prompt with: spec excerpt + file contents + build
   results + (if applicable) a sample of the produced JSON output and the
   schema it should satisfy.
3. Execute:
   ```python
   result = route(
       content=verification_prompt,
       task_type="session-verification",
       complexity_hint=70,
       session_set=str(SESSION_SET),
       session_number=N,
   )
   log.save_session_review(
       session_number=N,
       review_text=result.content,
       round_number=1
   )
   ```
4. **Never edit the saved review file.**
5. Log the verification step.

Because this repo has no human-UAT backstop, the cross-provider review is
the last line of defense before commit. Treat it accordingly: do not
suppress, summarize, or paraphrase the verifier's output.

### Step 7: Handle Verification Result

**VERIFIED:** Proceed to commit.

**ISSUES_FOUND:**
1. Parse issues from the verifier's response.
2. Fix each issue. Update status to "fixed" or "deferred".
3. Save: `log.save_issue_log(session_number=N, issues=issues)`
4. Re-run verification (max 2 retries). Use `complexity_hint=85` if any
   issue is Major or Critical.

#### Disagreement With A Verifier Finding

If the orchestrator disagrees with a specific finding rather than
accepting it, the orchestrator does **not** dismiss the finding on its
own authority and does **not** appeal to another AI provider for a
consensus vote. The authority model is: verifiers flag, humans
adjudicate.

Most orchestrator-vs-verifier disagreements turn out to be context
problems rather than who-is-right problems. The common failure modes
are:

- **Context gap** — the verifier flagged something that's actually
  handled elsewhere in code the verifier wasn't shown. Most common case.
- **Genuine split** — the verifier and orchestrator both have the same
  context and disagree on the call.
- **Orchestrator error** — the orchestrator is wrong and should accept
  the finding.

When the orchestrator wants to challenge a finding it must present to
the human:

1. **The exact finding** (verbatim from the saved review file).
2. **The dismissal reason** — why the orchestrator believes the finding
   does not apply.
3. **The context that went to the verifier** — the list of files and
   excerpts included in the verification prompt.
4. **Self-assessment of context** — whether relevant context may have
   been missing or whether the verifier may have been distracted by
   irrelevant material.

The human then chooses one of four options:

- **(a) Accept verifier finding** — treat as an issue and fix.
- **(b) Accept orchestrator's dismissal** — close without changes.
- **(c) Re-verify with reshaped context** — same verifier, adjusted
  input (add missing files, trim irrelevant ones). Resolves the
  context-gap case cleanly.
- **(d) Second opinion from a different provider** — route the same
  content to a tiebreaker model from a different provider than the
  original verifier. This reuses the existing
  `verification.settings.on_disagreement` / `tiebreaker_model`
  configuration in `router-config.yaml`; no new code path is required.

Whichever option the human picks, log the disagreement so the ratio
is visible in the manager report over time. The router exposes
`record_adjudication` for this — it writes one JSON line to
`router-metrics.jsonl` alongside the route/verify records so the same
report pass aggregates all of it:

```python
from ai_router import record_adjudication

record_adjudication(
    config,                         # already loaded router config
    task_type="architecture",       # same task_type as the verify call
    cause="context-gap",            # context-gap | genuine-split | orchestrator-error
    resolution="reverify-reshaped", # accept-finding | accept-dismissal
                                    # | reverify-reshaped | second-opinion
    session_set=str(SESSION_SET),
    session_number=N,
    generator_model=route_result.model_name,
    verifier_model=route_result.verification.verifier_model,
    finding_summary="<one-line summary of the finding>",
    dismissal_reason="<why the orchestrator challenged it>",
)
```

### Step 8: Cost Report, Next-Orchestrator Recommendation, Change Log (Last Session), Commit, Push, And Notify

Print the cost report:

```python
from ai_router import print_cost_report
print_cost_report(SESSION_SET)
```

**Every session except the last — produce a next-session orchestrator
recommendation.** This is the forward-looking complement to the
backward-looking cost report. Inputs: the next session's spec excerpt,
the next session's `ai-assignment.md` row, and this session's actuals
(routed cost, escalations, verifier verdict, any orchestrator
self-assessment of "harder/easier than expected"). Output: a
recommended orchestrator (engine + model + thinking/effort) plus a
two-sentence rationale. Routed via `route(task_type='analysis')` so
the current orchestrator does not self-opine.

```python
result = route(
    content=next_orchestrator_prompt,   # see schema below
    task_type="analysis",
    complexity_hint=40,                 # smaller than the ai-assignment authoring call
    session_set=str(SESSION_SET),
    session_number=N,
)
log.log_step(N, step_num, "session-N/next-orchestrator-recommendation",
             description="Routed analysis recommending <engine+model+effort> for Session N+1",
             status="complete",
             api_calls=[{...}])
```

**Recommendation schema** (one short markdown block, also appended to
the *Actuals* block of this session's row in `ai-assignment.md` AND
to the *Recommended orchestrator* line of the next session's row if
the recommendation differs from the original):

```markdown
**Next session orchestrator recommendation (Session N+1):**
<engine> <model> @ effort=<low|medium|high>

**Rationale:** <one to two sentences grounded in (a) the next
session's actual scope and (b) what this session revealed about
how the codebase is shaped>
```

The recommendation is **advisory** — the human chooses whether to
honor it when they invoke the next session. Surface it in the final
end-of-session printout AND include a one-line summary in the
Pushover notification body so the human sees it on their phone.

**Last session only — generate the change log before committing.** It must
land in the same commit as the final session's code so the session set is
marked complete in a single push.

```python
result = route(content=change_log_prompt, task_type="summarization")
# write docs/session-sets/<name>/change-log.md
```

> Use `task_type="summarization"` for the change-log prompt (and any
> other narrative-prose output). `task_type="documentation"` is reserved
> for code documentation; its prompt template wraps content in a
> "document this code" frame that misfits free-form prose and produces
> Gemini 400s on prompts above ~2KB.

**Last session only — produce a next-session-set recommendation.**
This is the cross-set complement to the per-session next-orchestrator recommendation.
It tells the human which child session set (or sets, if the DAG permits parallel
starts) to execute next, and with what orchestrator. Routed via
`route(task_type='analysis', complexity_hint=40)`.

Prompt inputs: the completed set's row from `session-set-tree.md` (shows which
sets it unblocks), this session's actuals, and the `ai-assignment.md` rows for the
unblocked candidate next sets.

```python
result = route(
    content=next_set_prompt,
    task_type="analysis",
    complexity_hint=40,
    session_set=str(SESSION_SET),
    session_number=N,
)
log.log_step(N, step_num, "session-N/next-session-set-recommendation", ...)
```

**Output schema (single next set):**
```markdown
**Next session-set recommendation (after <slug>):**
`<next-slug>`

**Recommended orchestrator for `<next-slug>`:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2 sentences grounded in (a) next set's synthesis count from session-set-tree.md and (b) what this session revealed>
```

**Output schema (parallel next sets — when DAG permits):**
```markdown
**Next session-set recommendation (after <slug>):**
`<slug-a>` and `<slug-b>` (run in parallel — both are DAG roots)

**Recommended orchestrator for `<slug-a>`:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2 sentences>

**Recommended orchestrator for `<slug-b>`:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2 sentences>
```

Include this block in the `change-log.md` header AND in the Pushover notification body.
If there is no `session-set-tree.md` (i.e., this session set was not produced by a
generator set), omit this sub-step.

**Last session only — prepend ✅ to the session-set directory name.**
Once `change-log.md` lands on the last session of a set, rename the
session-set directory to `✅<original-slug>` so the completed state
is visible at a glance in `docs/session-sets/`. The conceptual slug
itself does not change (it stays `<original-slug>` in
`session-set-tree.md`, in cross-set references, and in any
`Prerequisite:` lines); only the directory name carries the marker.
Update path references in this commit so spec.md headers, change-log
self-references, and any in-set scripts point at the renamed path.
The `find_active_session_set()` helper keys off `change-log.md`
presence, not directory name, so the marker has no functional effect
on auto-detection.

Commit and push:
```bash
git add -A
git commit -m "Session N of M: <description>"
git push
```

Then send a session-complete notification through the reusable helper in
`ai-router/`. **Critical ordering:** notify here — before the Step 9
reorganization review — so the human is not holding up the "session
complete" signal while they think about proposals.

```python
from ai_router import send_session_complete_notification

try:
    send_session_complete_notification(
        session_set=SESSION_SET,
        session_number=next_session,
        total_sessions=log.total_sessions,
        verification_verdict="VERIFIED",
        summary="<short description>"
    )
except Exception as exc:
    print(f"Session completed, but Pushover notification failed: {exc}")
```

If notification delivery fails, report it, but do not undo or fail an
otherwise successful session.

### Step 9: Last Session Only — Reorganization Proposals (Post-Notify)

**Non-last sessions skip this step and proceed to Step 10.**

On the last session, AFTER notifying in Step 8, review
`docs/planning/project-guidance.md` and `docs/planning/lessons-learned.md`
and propose any reorganization that this session set's experience
justifies. This step is run after notify because it blocks on a human
response; the human should already have received the completion
notification and can answer on their own schedule.

Candidate moves include:

- **Lesson → Convention.** A lesson in `lessons-learned.md` that has
  been applied as the right call in **at least two different contexts**
  (different session sets, features, or problem areas) is a promotion
  candidate. A single repeat is not enough — wait for the second context.
- **Lesson → Principle.** A lesson that has proven itself strategic in
  ≥2 contexts and is broader than a specific pattern may be promoted
  directly to Principles. Rare — most promotions go to Conventions first.
- **Convention → Principle.** A convention whose rationale has become
  clearly strategic (not just a rule, but a reason the rule exists).
- **Principle → Convention.** A principle that turns out to be a
  specific rule rather than durable strategy.
- **Relocation within a file.** Moving an item to a more fitting section.
- **Staleness flag.** An item whose driving context is gone may be
  flagged for the human — but do not delete. Only move, with a note
  about why.

Procedure:

1. Scan both files and identify candidates with reasoning.
2. Present each candidate to the human, one at a time.
3. Human accepts or rejects each proposal.
4. Apply accepted proposals. **Never delete content — only move.**
5. If no candidates qualify, output exactly:
   > No reorganization changes recommended for `project-guidance.md` or
   > `lessons-learned.md`.
6. If any proposal was accepted, commit and push those changes in a
   **separate follow-up commit**:
   ```bash
   git add docs/planning/project-guidance.md docs/planning/lessons-learned.md
   git commit -m "Session set <name>: Step 9 reorganization (<summary>)"
   git push
   ```

This step is mandatory even when the output is "no changes recommended" —
the review itself is the checkpoint.

### Step 10: Stop

Report: session number, verification verdict, deferred issues (if any),
cost summary, and sessions remaining.

**Do not start the next session. Wait for the human.**

---

## Orchestrator Instruction Files

Each AI coding agent reads a different instruction file. All three instruct
the orchestrator to follow this same workflow.

| Agent | Instruction File | Global Config |
|---|---|---|
| Claude Code | `CLAUDE.md` (repo root) | `~/.claude/CLAUDE.md` |
| Codex (OpenAI) | `AGENTS.md` (repo root) | `~/.codex/instructions.md` |
| Gemini | `GEMINI.md` (repo root) | Varies by tool |

### Switching Orchestrators Between Sessions

The workflow is designed so that any orchestrator can pick up where another
left off. The `activity-log.json` and `spec.md` provide all the state
needed.

---

## AI Router Details

### Importing the Router

The module directory is `ai-router/` (hyphenated). Python cannot import
hyphenated package names directly, so use `importlib`:

```python
import importlib.util, sys

def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        'ai_router', 'ai-router/__init__.py',
        submodule_search_locations=['ai-router'])
    mod = importlib.util.module_from_spec(spec)
    sys.modules['ai_router'] = mod
    spec.loader.exec_module(mod)
    return mod

ar = load_ai_router()
route = ar.route
```

On Windows, use `.venv/Scripts/python.exe` to run Python.

### Task Types

| Task Type | Base Complexity | Typical Model |
|---|---|---|
| `formatting` | 10 | Gemini Flash |
| `summarization` | 20 | Gemini Flash |
| `documentation` | 25 | Gemini Flash/Pro |
| `test-generation` | 35 | Gemini Pro |
| `code-review` | 40 | Sonnet (if overridden) or Gemini Pro |
| `analysis` | 55 | Gemini Pro |
| `refactoring` | 65 | Gemini Pro |
| `session-verification` | 70 | Opus |
| `security-review` | 75 | Opus |
| `architecture` | 80 | Opus |
| `planning` | 70 | Opus |

UI-specific task types from sibling repos (`uat-plan-generation`,
`uat-coverage-review`) are intentionally not used in this project. If
they appear in inherited config, leave them defined but expect zero
calls against them.

### Model Tiers and Pricing

| Tier | Model | Provider | Input $/1M | Output $/1M | Use Case |
|---|---|---|---|---|---|
| 1 | Gemini Flash | Google | $0.15 | $0.60 | Simple formatting, boilerplate |
| 2 | Gemini Pro | Google | $1.25 | $10.00 | Documentation, medium analysis |
| 2 | Sonnet | Anthropic | $3.00 | $15.00 | Code review, mechanical gates |
| 2 | GPT-5.4 Mini | OpenAI | $0.75 | $4.50 | Cross-provider verification of Gemini output |
| 3 | Opus | Anthropic | $15.00 | $75.00 | Architecture, security, synthesis verification |
| 3 | GPT-5.4 | OpenAI | $2.50 | $15.00 | Frontier verification of Opus output |

### Escalation

If a model produces an empty, truncated, or suspiciously short response,
the router automatically escalates to the next tier (up to 2 escalations).

### Cross-Provider Verification

For auto-verified task types (code-review, security-review, and any
others listed in `verification.auto_verify_task_types`), the router
sends the initial response to a verifier from a different provider.
Verifier selection is rule-based: a candidate must be from a different
provider than the generator, be in the enabled model pool
(`is_enabled: true`), be trusted for verification
(`is_enabled_as_verifier: true`), and match the generator's tier or be
one tier higher. Among survivors, the cheapest output price wins.

If the first-choice verifier fails at the HTTPS layer (provider
outage, timeout), the router excludes that provider and re-picks once.
The fallback call is recorded in the metrics log with
`verifier_fallback: true`.

Swapping or retiring a model requires editing only its entry under
`models:` in the YAML.

### When To Use The Router

- Code review → `route(code, task_type="code-review")` ← auto-verified
- Security review → `route(code, task_type="security-review")` ← auto-verified
- Documentation → `route(code, task_type="documentation")`
- Analysis → `route(question, task_type="analysis")`
- Test generation → `route(code, task_type="test-generation")`
- Schema/AST mechanical gates → `route(content, task_type="code-review")`
  with the gate prompt — pin to Sonnet via `task_type_overrides` if
  empirically Opus is overkill for the mechanical correspondence.

Do NOT route file creation, shell commands, or anything needing the
filesystem.

---

## Metrics and Observability

Every routed call, verifier call, and tiebreaker call is appended to a
global log at `ai-router/router-metrics.jsonl`. The log is append-only
JSON lines (one record per line). It spans all session sets — it is
NOT per-session-set — so cross-project trends can be analyzed.

### What gets logged

For each call: timestamp, session set, session number, call type
(route/verify/tiebreaker), task type, model, provider, tier,
complexity score, effort / thinking setting, input and output tokens,
cost, elapsed time, escalation flag, stop reason. Verifier calls
additionally log the verdict (VERIFIED / ISSUES_FOUND) and issue count.

### Reading the log

Call `print_metrics_report()` for a terminal summary:

```python
from ai_router import print_metrics_report
print_metrics_report()
```

For deeper analysis, read the JSONL file directly. The human or an
orchestrator can hand the file to a reasoning model and ask
questions like *"which task types are escalating most?"* or *"is GPT-5.4
a stricter or looser verifier than Opus?"*.

### Manager Report

For a governance-oriented markdown summary rather than the developer
text dump, run `report.py`:

```bash
python -m ai_router.report
python -m ai_router.report --output docs/reports/router-2026-Q2.md
python -m ai_router.report --since 2026-04-01 --until 2026-04-30
python -m ai_router.report --session-set docs/session-sets/<name>
```

The report contains the headline (total spend vs. Opus-only baseline),
per-task-type summary, outliers, and auto-generated action items for any
task type whose composite unreliability exceeds 20%.

### Threading session info

When the orchestrator calls `route()`, pass `session_set` and
`session_number` so the metrics can be grouped by session.

### Disabling metrics

Set `metrics.enabled: false` in `router-config.yaml` to stop writing.

---

## Delegation Discipline

The orchestrator's job is to plan, sequence, and dispatch — not to do
every piece of reasoning itself. Orchestrators tend to hoard work
because calling themselves "feels faster." In practice that means paying
the orchestrator's premium model rate for tasks a Gemini Flash or
Sonnet call at low effort would handle just as well.

### Default: Route Reasoning, Own Mechanics

The orchestrator does these **directly**, without calling `route()`:

- Reading the spec and the activity log
- Creating, editing, renaming, or deleting files
- Running shell commands (build, test, git)
- Dispatching work to `route()` and logging the result
- Single-file edits under ~50 lines that are mechanical (renames,
  imports, formatting, trivial boilerplate the spec dictates verbatim)
- Interpreting errors enough to decide which task to route next

The orchestrator **always** routes these through `route()`, never
performs them itself:

- Code review
- Security review
- Architecture decisions, pattern selection, design proposals
- Analysis of existing code or test results beyond a surface read
- Documentation writing (change logs, READMEs, doc comments on
  non-trivial APIs)
- Test generation beyond one-off smoke tests
- Session verification (the cross-provider check at end of session)
- Parser-grammar design and AST shape decisions
- Any task that requires producing more than ~50 lines of reasoned
  output

### When the orchestrator's model matches the routing target

When the orchestrator model matches the routing target for a given task
type, handle the task in-conversation rather than calling `route()`. A
`route()` call into the orchestrator's own model gives fresh context
(occasionally useful for stochastic independence) but also pays the
provider's API cost to summon another instance of the same judgment.
**The non-negotiable exception is `session-verification`, which MUST
go through the router regardless** — the whole point is a cross-provider
second opinion.

### Thresholds (human-tunable)

The thresholds above (~50 lines, single file) live in
`router-config.yaml` under `delegation.direct_work_max_lines` and
`delegation.direct_work_max_files`.

---

## Rules (Apply to All Orchestrators)

This is the authoritative rules list. Instruction files (`CLAUDE.md`,
`AGENTS.md`, `GEMINI.md`) reference this section rather than duplicating it.

1. **One session only.** Never execute more than the assigned session.
2. **Never skip verification.** Every session must be independently
   verified by a different AI provider via
   `route(task_type="session-verification")`. Cross-provider review is
   the last line of defense in this repo — there is no human-UAT
   backstop.
3. **Never edit session review files.** They are the verifier's raw
   output.
4. **Log every step** via `log.log_step()` — including build, test, and
   verification.
5. **Delegate reasoning** to `route()`. See the Delegation Discipline
   section above for the full criteria.
6. **Do not commit with unresolved Critical/Major issues.** Inform the
   human.
7. **The human controls orchestrator choice.** Any session can use any
   agent.
8. **Before every session, read both guidance files.**
   `docs/planning/project-guidance.md` and
   `docs/planning/lessons-learned.md` are mandatory pre-session context.
9. **Build and test sequentially, not in parallel** against the same
   worktree on Windows.
10. **Escalate durable new guidance.** If the human gives an instruction
    that looks like a future principle or convention, ask whether it
    should be added to `docs/planning/project-guidance.md`.
11. **Recommend lessons learned after failures.** When a failure
    suggests a reusable tactic, propose an update to
    `docs/planning/lessons-learned.md`.
12. **Run the Step 9 reorganization review on the last session of every
    set, after the notify.** Output "no changes recommended" if nothing
    qualifies — but do the review.
13. **Author the AI-assignment ledger on Session 1 of every new set
    (Step 3.5), the next-session orchestrator recommendation at
    the end of every session except the last (Step 8 sub-step), and the
    next-session-set recommendation on the last session of every set
    (Step 8 sub-step, when a `session-set-tree.md` exists).** All are
    routed `analysis` calls — never self-opine on which orchestrator
    or which model would have been the cheapest safe choice. The current
    orchestrator is the wrong judge of whether it could have been a
    smaller orchestrator.
