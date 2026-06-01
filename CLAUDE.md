# CLAUDE.md — Dabbler Access Harvester

## Project Overview

A C# .NET 10 toolchain that harvests Microsoft Access (`.accdb` / `.mdb`)
metadata at sufficient structural fidelity that a downstream AI can
reliably interpret and document the database's forms, reports, queries,
macros, ribbons, tables, and modules. Two related concerns share this
repo:

1. **Raw extraction + mechanical enrichment** — COM- and DAO-driven
   metadata pull, three-tier output (`raw/` → `kb/` → `ir/`), property
   decoding, geometry, label resolution, etc. Source material lives in
   the sibling `../AccessExporterTemp1` repo and is imported here.
2. **VBA parser** — a structured parser for VBA modules and Access
   expressions (validation rules, query SQL, calc expressions) that
   downstream AI can read more cleanly than raw text. Plan and test
   cases live under `docs/VBA-Parser-Migration-Harvester-Plan.md` and
   `docs/test-cases/`.

There is **no UI surface** in this repo. End-of-session quality is
guarded by automated build/test plus mandatory cross-provider AI
verification — there is no human-UAT layer.

The released binary ships on
[GitHub Releases](https://github.com/darndestdabbler/dabbler-access-harvester/releases)
(zip + `.sha256` per release, tagged `v<semver>`); see
[`docs/harvester/install.md`](docs/harvester/install.md) for the
consumer download + checksum-verify procedure.

## Required reading before every session

- `README.md` — repo layout and build entry points (when present)
- `docs/planning/project-guidance.md` — durable principles + conventions
- `docs/planning/lessons-learned.md` — repo-specific failure patterns
  and tactics learned from prior sessions
- [`session-set-authoring-guide.md`](https://raw.githubusercontent.com/darndestdabbler/dabbler-ai-orchestration/master/docs/planning/session-set-authoring-guide.md)
  (canonical, lives in `dabbler-ai-orchestration`) — how to author or
  update a session-set spec (slug naming, sizing, Session Set
  Configuration block including `requiresUAT` / `requiresE2E` / `tier`
  / `prerequisites`)
- [`ai-led-session-workflow.md`](https://raw.githubusercontent.com/darndestdabbler/dabbler-ai-orchestration/master/docs/ai-led-session-workflow.md)
  (canonical, lives in `dabbler-ai-orchestration`) — the authoritative
  session-set workflow. Single source of truth for the step-by-step
  procedure, the AI router usage, cross-provider verification, **the
  Step 7 verifier-disagreement resolution procedure (four human options,
  disagreement logging)**, the Step 9 reorganization review (run after
  the Step 8 notify so the human is not blocking the completion
  signal), **the Delegation Discipline rules (when to route vs. handle
  directly)**, and the mandatory rules list. Do not duplicate that
  content here.
- [`docs/session-state-schema.md`](https://raw.githubusercontent.com/darndestdabbler/dabbler-ai-orchestration/master/docs/session-state-schema.md)
  (canonical, lives in `dabbler-ai-orchestration`) — the authoritative
  schema for `session-state.json`. Required reading before hand-editing
  any state file. Touching a state file without having read this doc is
  the most common cause of N−1/N drift in the Session Set Explorer.
  (Links are **raw** GitHub URLs so a fetch returns the doc itself, not
  the HTML page.)

### State-file rules every orchestrator must follow (v4 shape; Set 047)

These are inlined here so the orchestrator does not need to fetch the
canonical doc to get the must-knows. Read the linked schema for the
full invariant and worked examples.

- `sessions: [{ number, title, status, startedAt, completedAt, ... }, ...]`
  is the canonical per-session ledger. Legacy top-level fields
  (`currentSession`, `totalSessions`, `completedSessions`,
  `lifecycleState`, `startedAt`, `completedAt`) are NOT written to
  disk in v4 — readers derive them at read time from `sessions[]`
  via the `normalize_to_v4_shape` shim. Without per-session entries
  carrying accurate `status` + `completedAt`, the extension cannot
  reliably derive completion count and will display N−1/N.
- On **Full tier**, `close_session` writes the per-session entry
  (`status: "complete"` + `completedAt`) automatically — the
  orchestrator never edits the array directly.
- On **Lightweight tier**, the orchestrator (or human) maintains
  `sessions[i]` **by hand** on every close: set the matching entry's
  `status` to `"complete"` and populate its `completedAt`. There is
  no router writer; this per-session ledger is the only authoritative
  completion signal.
- Canonical per-session `status` values are `"not-started"`,
  `"in-progress"`, `"complete"`, `"cancelled"`. Never `"completed"`
  or `"done"` on a new write. The set's top-level `status` flips to
  `"complete"` on the final close (Full: `close_session`; Lightweight:
  the orchestrator).
- The "in flight" predicate is: any `sessions[i].status` is
  `"in-progress"`. The extension uses this to distinguish a fresh-start
  row from a between-sessions row — keep `sessions[]` accurate.
- **Omit-null (Set 049)**: when creating a new session-set stub or
  per-session entry, omit fields that are not yet known (`startedAt`,
  `completedAt`, `orchestrator`, `verificationVerdict`) rather than
  writing `null`. Readers tolerate missing keys; `null` on disk is
  never correct.
- **Stamp `"schemaVersion": 4` on every write.** The current canonical
  schema is v4. A file written without this stamp (or stamped `< 4`) is
  flagged "needs migration" by the Session Set Explorer — this is exactly
  what happened to two sets on 2026-05-29 when they were hand-authored
  from stale memory as `v2`. If you are hand-authoring a state file and
  are not certain these inlined rules are current, **fetch the canonical
  schema doc (raw URL above) first** rather than writing from memory; the
  linked doc is authoritative if the canonical version has advanced past 4.

The human starts a session with one of the trigger phrases listed in
`docs/ai-led-session-workflow.md` → **Trigger Phrases** (sequential,
named-set, parallel-worktree, optional `— maxout <engine>` suffix).
That section is the canonical list; do not re-list trigger phrases here.

When the human says **"Start the next session"**, follow the workflow
in `docs/ai-led-session-workflow.md` exactly. What follows below is
only the Claude-specific bootstrap the workflow needs.

## Claude-specific bootstrap

### API key export (Windows)

```bash
export GEMINI_API_KEY=$(powershell -Command "[System.Environment]::GetEnvironmentVariable('GEMINI_API_KEY', 'User')" | tr -d '\r')
export ANTHROPIC_API_KEY=$(powershell -Command "[System.Environment]::GetEnvironmentVariable('ANTHROPIC_API_KEY', 'User')" | tr -d '\r')
export OPENAI_API_KEY=$(powershell -Command "[System.Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'User')" | tr -d '\r')
.venv/Scripts/python.exe -c "
import os
missing = [k for k in ('ANTHROPIC_API_KEY', 'GEMINI_API_KEY', 'OPENAI_API_KEY') if not os.environ.get(k)]
if missing:
    raise SystemExit(f'Missing environment variables: {missing}')
print('API keys OK')
"
```

If keys are missing, stop and tell the human.

> **`No module named ai_router…` is NOT a missing-keys problem — do not
> stop and ask the human for it.** It means the router CLI was launched
> with the wrong interpreter (a bare `python` resolving to a system
> interpreter without `ai_router`; the config-only `ai_router/` folder in
> the repo root then shadows as an empty namespace package, so the error
> names the submodule, e.g. `No module named ai_router.close_session`).
> The keys can be perfectly present. **Always run the router CLIs through
> the workspace venv** — `.venv/Scripts/python.exe -m ai_router.<module>`
> — never a bare `python -m ai_router.<module>`. Only stop for missing
> keys once the venv interpreter (the key check above) actually reports a
> key absent.

### AI router import

The router is the `dabbler-ai-router` PyPI package (canonical
dabbler-ai-orchestration repo, Set 010 onwards). It is declared in
`requirements.txt` and lives in the workspace `.venv`. Bare imports
work:

```python
from ai_router import route, send_session_complete_notification
from ai_router.session_log import SessionLog
```

The operator-tuned config lives at `ai_router/router-config.yaml`
(workspace-relative, not inside the venv). Set the env var so
`route()` reads it:

```bash
export AI_ROUTER_CONFIG=ai_router/router-config.yaml
export AI_ROUTER_METRICS_PATH=ai_router/router-metrics.jsonl
```

`send_session_complete_notification()` reads `PUSHOVER_API_KEY` and
`PUSHOVER_USER_KEY` from the environment or Windows User environment.

If `pip install` of the router needs to be re-run (fresh clone, dropped
venv, etc.), the canonical extension's **Dabbler: Install ai-router**
command does it from VS Code: PyPI install (default) or GitHub
sparse-checkout fallback for offline / pre-release / fork scenarios.

### Delegation Discipline (pointer)

Your role is orchestrator, not solo coder. Reasoning tasks — code
review, security review, architecture, analysis, documentation, test
generation, parser-grammar design, session verification — **always** go
through `route()`. Only handle work directly when it is mechanical,
single-file, and under ~50 lines.

This is not cost paranoia; it is the design of the workflow. The router
already picks the cheapest capable model and applies the correct
reasoning depth from `router-config.yaml`. Your job is to pass the
right `task_type` and let the router handle the rest.

See `docs/ai-led-session-workflow.md` → **Delegation Discipline** for
the full criteria, the human-tunable thresholds (in
`ai_router/router-config.yaml` under `delegation:`), and the rationale.

### Decision-time consensus (pointer)

When you hit an in-session design / architecture / process question
that has more than one plausible answer, route it through cross-
provider consensus *before* falling back to `AskUserQuestion`. The
opt-in (`delegation.decision_consensus.enabled`), category gates,
journal format, and the human-only vs consensus-eligible split are
documented in `docs/ai-led-session-workflow.md` → **Decision-time
consensus**.

## Building & Testing

The solution is C# / .NET 10. Build and test commands once the solution
exists (Session Set #1 imports the relevant projects from
`../AccessExporterTemp1`):

```bash
dotnet build
dotnet test
```

Run build and test **sequentially**, not in parallel — file-lock
contention against the same worktree on Windows obscures real failures.

The harvester's COM-based extractor only runs on Windows with Microsoft
Access installed. Hermetic unit tests (parser, enrichers, schema
validators) run anywhere; integration tests against committed
`.accdb` fixtures are gated as `[SkippableFact]` when Access is absent.

COM-driven test and extraction runs terminate the **exact** Access
instance they create — graceful `Quit(acQuitSaveNone)` + doubled
`GC.Collect()` / `WaitForPendingFinalizers()`, then a bounded wait and an
exact-PID force-kill failsafe — so a hung run no longer leaves an
orphaned `msaccess.exe` (or the `testhost` / `dotnet` waiting on it)
behind. The single COM chokepoint that implements this is
[`AccessHarvester.Core/Com/AccessComSession.cs`](AccessHarvester.Core/Com/AccessComSession.cs);
the pattern, the `hWndAccessApp` → `GetWindowThreadProcessId` PID capture,
and the force-kill / lock-file caveats are documented in
[`docs/access-com-zombie-cleanup.md`](docs/access-com-zombie-cleanup.md).
Do not reintroduce a blunt by-name `taskkill` of `msaccess.exe` in test
teardown — it risks unrelated Access instances; rely on the chokepoint's
exact-PID self-cleanup instead.

## Solution Structure

The intended layout (filled in by Session Set #1):

- `AccessHarvester.Cli/` — thin CLI exe (subcommands: extract,
  translate, run, generate-docs, validate, parse)
- `AccessHarvester.Core/` — extractors, translators, enrichers, COM
  session, parser, model
- `tests/AccessHarvester.Tests/` — xUnit hermetic + `[SkippableFact]`
  integration tests
- `tools/` — developer utilities (Python scripts for resource
  generation, validation, etc.)
- `test-builder/` — Python synthetic-DB builder (pywin32 + Access COM)
- `docs/` — workflow docs, planning docs, VBA parser plan, test cases,
  session sets
- `ai_router/` — operator-tuned `router-config.yaml` and the
  append-only `router-metrics.jsonl` cost telemetry; the router
  itself is `pip install dabbler-ai-router` (declared in
  `requirements.txt`)
- `samples/` — committed sample `.accdb` inputs for integration tests
  (Windows-only)

Code style conventions (namespaces, casing, nullable, `Async` suffix)
live in `docs/planning/project-guidance.md` → Conventions → Code Style.

This repo uses the bare-repo + flat-worktree layout — the working tree
is at `<container>/main/` and parallel session-set worktrees live at
`<container>/<slug>/`. Run all build, test, and Python commands from
inside `main/` (or the session-set worktree), never from the container
root. See
`../../dabbler-ai-orchestration/docs/planning/repo-worktree-layout.md`
for the layout, recipe, and gotchas.

If a file-system operation (typically `mv` or `git worktree remove`)
fails with a Windows file-lock error, run
`.venv/Scripts/python.exe -m ai_router.utils cleanup-dev-orphans`
from inside `main/` first — it shuts down dotnet build servers, kills
stale Claude Code background polling loops, and clears orphan
`conhost.exe`. Pass `--match-path dabbler-access-harvester` to scope
the polling-loop kill to this container only.
