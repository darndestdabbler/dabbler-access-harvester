# Bootstrap: New Repo for `ai-orchestration-project-template` Work

> ## ⚠️ Largely superseded by the canonical `Dabbler: Install ai-router` command
>
> This document was authored 2026-04-27 when the AI router lived as a
> copy-into-your-project `ai-router/` directory loaded via an
> importlib shim. As of canonical dabbler-ai-orchestration **Set 010
> (2026-05-02)** the router is on PyPI as `dabbler-ai-router` and is
> installed via:
>
> - `pip install dabbler-ai-router` (CLI), or
> - `Dabbler: Install ai-router` from the bundled VS Code extension
>   (handles venv creation, materializes
>   `ai_router/router-config.yaml` into the workspace, preserves any
>   prior local tuning, and writes a `.dabbler/install-method` marker
>   so subsequent `Dabbler: Update ai-router` runs re-pull from the
>   same source).
>
> The bulk of this document — Path A (fork-and-strip) and Path B
> (empty repo + manual copy) — was a workaround for the missing PyPI
> package and the absence of the install command. Both still work
> mechanically but produce a more drift-prone setup. The canonical
> bootstrap is now: create the directory, install the extension VSIX
> (`dabbler-ai-orchestration-0.12.0.vsix` from the canonical repo),
> open the workspace in VS Code, run **Dabbler: Install ai-router**.
>
> Specific steps below that have been overhauled rather than
> wholesale-superseded:
>
> - **Step 9 (Install the VS Code Extension)** — bumped from VSIX
>   v0.7.1 to v0.12.0 and pointed at the canonical repo.
> - **Step 10 (Sanity-Check the Bootstrap)** — rewritten to use bare
>   `from ai_router import ...` (no importlib shim).
>
> The original 2026-04-27 walkthrough follows below for historical
> reference.

> **Audience:** Claude Code (or Codex / Gemini CLI) running in a
> brand-new, otherwise-empty repository whose job is to build the
> reusable AI-orchestration template.
> **Source-of-truth pattern lives at:** `../dabbler-access-harvester`
> (assumed sibling of this new repo on disk).
> **Created:** 2026-04-27, in `dabbler-access-harvester` for export.
> **Banner added:** 2026-05-02, after canonical Set 010 published the
> router on PyPI and shipped the install command.

This file walks you through bootstrapping a new repo from zero so it can
run the `ai-orchestration-project-template` session set under the same
AI-led workflow that produced it. After bootstrapping, you (the human)
say "Start the next session of `ai-orchestration-project-template`" and
the agent picks up the workflow as documented at
`docs/ai-led-session-workflow.md` (which gets copied in during step 2).

There are two reasonable starting points. Pick one.

- **Path A — Fork the harvester and strip it.** Lower friction, higher
  blast radius if you forget to delete something domain-specific. Good
  if you want every line of scaffolding present without manually
  enumerating it.
- **Path B — Empty repo + manual copy.** Slower, more deliberate,
  cleaner result. Good if you want a tight understanding of *exactly*
  what the template needs (which is, after all, the whole point of the
  session set you are about to run).

Path B is recommended — it doubles as the first inventory pass for the
session set itself.

---

## Path B — Empty Repo + Manual Copy (Recommended)

### Step 0: Pre-flight Check

Confirm `../dabbler-access-harvester` exists as a sibling directory of
this new repo. The remaining steps copy from there. If it lives
elsewhere, adjust paths.

```bash
ls ../dabbler-access-harvester/CLAUDE.md && echo "OK"
```

### Step 1: Initialize Git + .gitignore

```bash
git init
git branch -M main
```

Create a minimal `.gitignore` that keeps the noisy artifacts out (these
match what the harvester gitignores):

```
# .gitignore
.venv/
__pycache__/
*.pyc
ai-router/router-metrics.jsonl.bak
node_modules/
.vs/
bin/
obj/
```

> **Note on `router-metrics.jsonl`:** the harvester does NOT gitignore
> this file (the metrics history is part of the project record and
> survives merges as predictable noise — see lessons-learned). Match
> that: do not gitignore it here either.

### Step 2: Copy the Project-Agnostic Scaffolding

Copy these from `../dabbler-access-harvester/` verbatim — they are the
core of the workflow and contain no harvester-specific content:

```bash
mkdir -p docs/planning ai-router tools
cp ../dabbler-access-harvester/docs/ai-led-session-workflow.md         docs/
cp -r ../dabbler-access-harvester/ai-router/                           ./
cp -r ../dabbler-access-harvester/tools/vscode-session-sets/           tools/
```

After copying:

- Delete `ai-router/router-metrics.jsonl` and `ai-router/__pycache__/`
  (re-created on first run; do NOT inherit the harvester's metrics
  history).
- Delete `ai-router/router-config.yaml.bak` if present.

### Step 3: Copy + Trim the Planning Docs

`project-guidance.md` and `lessons-learned.md` carry both
project-agnostic discipline (workflow rules, routing patterns, verifier
discipline) and project-specific content (Access / VBA / parser
conventions). Copy and trim.

```bash
cp ../dabbler-access-harvester/docs/planning/project-guidance.md       docs/planning/
cp ../dabbler-access-harvester/docs/planning/lessons-learned.md        docs/planning/
```

Then edit each:

- **`project-guidance.md`** — keep the *Principles → Lean By Default*,
  *Principles → Testing Hierarchy*, *Principles → Precedence*, every
  *Conventions → Workflow Expectations* entry, and *Conventions →
  Token-Window Budget Overrides*. Delete *Project Scope And Audience*,
  *Output Architecture*, *VBA Parser Strategy*, *Platform And Runtime*,
  *Code Style*, *Output JSON Conventions*, *Cross-Repo Coordination*,
  and the harvester-specific tuning notes embedded in the AI Router
  section. Replace with a one-paragraph project-overview placeholder
  the new project fills in.

- **`lessons-learned.md`** — keep the *How To Use This File*, the
  *Entry Template*, and every *Seeded Lessons* entry (those are
  workflow discipline, not Access-specific). Delete the entire
  *Harvester-Specific Lessons* section and replace with a single line:
  `<!-- Project-specific lessons will accrue here over time. -->`

### Step 4: Copy the Spec for the Work You Are About To Do

```bash
mkdir -p docs/session-sets
cp -r ../dabbler-access-harvester/docs/session-sets/ai-orchestration-project-template/ \
      docs/session-sets/
```

The spec at `docs/session-sets/ai-orchestration-project-template/spec.md`
is the work plan for this repo. The session-set workflow expects it.

### Step 5: Author Project-Specific Instruction Files

Create `CLAUDE.md`, `AGENTS.md`, and `GEMINI.md` at the repo root. The
harvester's versions are good models — copy them and substantially
rewrite the *Project Overview* and *Required Reading* sections, but
keep the *Claude-specific bootstrap* (API key export, `load_ai_router`
import snippet) and *Delegation Discipline (pointer)* sections nearly
verbatim — they are agent-runtime infrastructure, not project content.

Replace the harvester's "Building & Testing" section with whatever fits
this template repo. For an in-progress template repo with no .NET
projects yet, a placeholder is fine:

```markdown
## Building & Testing

There is no build / test infrastructure in this repo yet. The session
set's first session decides whether the produced template needs one;
update this section then.
```

Example new `CLAUDE.md` opener (replace harvester-specific phrasing
throughout):

```markdown
# CLAUDE.md — AI Orchestration Project Template

## Project Overview

This repo authors a reusable bootstrap template for new projects that
want to adopt the AI-led session-set workflow developed in
`../dabbler-access-harvester`. The template ships as scaffolding
(instruction files, ai-router, planning docs, workflow doc, VS Code
extension) plus a setup guide. See
`docs/session-sets/ai-orchestration-project-template/spec.md` for the
full work plan.

## Required reading before every session
- `docs/ai-led-session-workflow.md`
- `docs/planning/project-guidance.md`
- `docs/planning/lessons-learned.md`
- `docs/session-sets/ai-orchestration-project-template/spec.md`

## Claude-specific bootstrap

[copy verbatim from ../dabbler-access-harvester/CLAUDE.md, including the
API key export block and the `load_ai_router` import snippet]
```

Keep `AGENTS.md` and `GEMINI.md` symmetric — same content, agent-name
substitutions only.

### Step 6: Set Up the Python venv

```bash
python -m venv .venv
.venv/Scripts/pip install pyyaml google-genai anthropic httpx   # Windows
# or: .venv/bin/pip install pyyaml google-genai anthropic httpx  # macOS/Linux
```

The runtime only needs `pyyaml` and `httpx`; the SDK packages are
listed for compatibility with sibling tooling. See
`docs/ai-led-session-workflow.md` → "Create the Python Virtual
Environment" for the rationale.

### Step 7: Confirm the Required Environment Variables Are Set

```bash
.venv/Scripts/python.exe -c "
import os
required = ('ANTHROPIC_API_KEY', 'GEMINI_API_KEY', 'OPENAI_API_KEY')
missing = [k for k in required if not os.environ.get(k)]
if missing:
    raise SystemExit(f'Missing: {missing}. Set them as Windows User env vars.')
print('API keys OK')
"
```

Optional but recommended:

```
PUSHOVER_API_KEY    # for end-of-session phone notifications
PUSHOVER_USER_KEY
```

### Step 8: Tune `router-config.yaml`

Open `ai-router/router-config.yaml` and verify:

- The `task_type_overrides.session-verification` entry maps to a model
  from a **different provider than your intended orchestrator**.
  Default in the harvester: `gpt-5-4`, calibrated for a Claude
  orchestrator. If your default orchestrator here is also Claude, leave
  it. If you plan to default to Codex / Gemini, swap to a Claude model.
  See `docs/planning/lessons-learned.md` →
  `session-verification` Needs A `task_type_overrides` Entry...

- All `is_enabled` and `is_enabled_as_verifier` flags reflect what you
  actually want available. The harvester's defaults are a reasonable
  starting point.

You do NOT need to change anything else for the first session.

### Step 9: Install the VS Code Extension

The Session-Set VS Code extension is a substantial productivity
multiplier. As of canonical Set 010 (2026-05-02) the extension is
distributed from the **canonical `dabbler-ai-orchestration` repo**,
not vendored per-project. Install it manually via the VS Code UI (the
`code --install-extension` CLI flag does NOT work in this setup —
confirmed lesson):

1. In VS Code: `View → Extensions` (or `Ctrl+Shift+X`).
2. Click the `…` menu in the Extensions panel header → `Install from
   VSIX…`.
3. Navigate to the canonical repo's
   `tools/dabbler-ai-orchestration/` directory and pick
   `dabbler-ai-orchestration-0.12.0.vsix` (or the highest-numbered
   `dabbler-ai-orchestration-*.vsix` present).
4. Reload VS Code if prompted.

After install, run **Dabbler: Install ai-router** from the command
palette to install the router into this repo's `.venv` and
materialize `ai_router/router-config.yaml`.

After install, the extension's *Session Sets* sidebar should show
`ai-orchestration-project-template` as the only entry, in the
"not-started" group. The "Copy: Start next session of <slug>" /
"Copy: Start next parallel session of <slug>" context-menu items on
that entry produce the trigger phrases the workflow doc accepts.

### Step 10: Sanity-Check the Bootstrap

```bash
.venv/Scripts/python.exe -c "
import ai_router
print(f'ai_router {ai_router.__version__} import OK from {ai_router.__file__}')

from ai_router.session_log import find_active_session_set
ss = find_active_session_set('docs/session-sets')
print(f'Active session set: {ss}')
"
```

Expected output:

```
ai_router 0.1.0 import OK from .../site-packages/ai_router/__init__.py
Active session set: docs/session-sets/ai-orchestration-project-template
```

If both lines appear, bootstrap is complete.

### Step 11: First Commit

```bash
git add -A
git commit -m "Bootstrap: orchestration scaffolding copied from dabbler-access-harvester"
```

Push to your remote when convenient. (No CI required at this stage.)

### Step 12: Start The First Session

Tell your AI agent:

> Start the next session of `ai-orchestration-project-template`

The agent reads `CLAUDE.md` (or `AGENTS.md` / `GEMINI.md`), which points
at `docs/ai-led-session-workflow.md`, and proceeds with Session 1 of
the spec.

---

## Path A — Fork-and-Strip (Faster But Riskier)

If you really want to start from a copy:

1. `git clone ../dabbler-access-harvester this-new-repo` (or fork on
   GitHub then clone).
2. Inside the new repo: `git remote remove origin`, then add the new
   remote.
3. Delete everything that is NOT scaffolding:
   - All `AccessHarvester.*` directories and `AccessHarvester.sln`
   - `samples/`, `tests/`, `test-builder/`
   - `tools/parser-harness/`, `tools/session-runner/`, all top-level
     Python scripts under `tools/` that are harvester-specific
   - All session-set directories under `docs/session-sets/` *except*
     `ai-orchestration-project-template/`
   - `docs/test-cases/`, `docs/VBA-Parser-Migration-Harvester-Plan.md`,
     and any other harvester-specific doc
   - `docs/cross-repo/` if present (will be re-created if relevant)
   - The `tmp/` directory if present
4. Apply Steps 3, 5, 6, 7, 8, 9, 10, 11, 12 above (the trim/edit/setup
   steps that don't depend on having an empty starting point).
5. Beware: `git log` will still show the harvester's full history. If
   that is undesirable, run `git checkout --orphan main && git add -A
   && git commit -m "Initial bootstrap"` to flatten.

The trim step is error-prone. Path B is recommended for a reason.

---

## Open Questions Before Session 1

The spec at `docs/session-sets/ai-orchestration-project-template/spec.md`
asks four open questions of the human. Have answers ready before you
say "Start the next session" — the orchestrator will ask if you do not
volunteer them, and waiting on a human mid-session is friction the
workflow tries to avoid:

1. Reference UI repos for Feature 3 (the UI variant). **Per the human's
   2026-04-27 clarification, the harvester ecosystem currently has no
   sibling repo using this exact orchestration pattern with E2E/UAT
   integration.** The recommended response is to descope Feature 3 from
   the first run of this set — ship the headless template now, defer
   the UI variant to whichever real UI project becomes the first
   adopter. Update the spec accordingly in Session 1, Step 1.

2. Where the template lives long-term — sibling repo (THIS repo, named
   to your taste), in-tree subdirectory of the harvester, or a
   cookiecutter-style hosted template.

3. Ship all three orchestrator instruction files, or only your
   preferred subset. Recommended: all three (consistent with the
   harvester practice).

4. Whether the VS Code extension's hard-coded `docs/session-sets/`
   scan root needs parameterization. Recommended: no, it's a
   convention worth standardizing across template adopters.

---

## After You Finish

When `change-log.md` lands in
`docs/session-sets/ai-orchestration-project-template/`, this repo IS
the template — or, more precisely, this repo *holds* the template at
whatever path Session 1 chose for it (per open question 2). At that
point you can publish, fork-as-template, or extract per the chosen
distribution mechanism.

Future template adopters skip Path A / Path B entirely and use whatever
distribution mechanism this set lands on.
