# Project Status — Dabbler Access Harvester

> **Purpose:** Capture everything a fresh orchestrator needs to know
> about the current state of the harvester repo so they can act
> confidently without re-reading conversation history. Treat the code
> as the source of truth; when this file and the code disagree, the
> code wins — and update this file.

---

## Project in one paragraph

A C# .NET 10 toolchain that harvests Microsoft Access (`.accdb` /
`.mdb`) metadata at sufficient structural fidelity that a downstream
AI agent can reliably interpret and document the database's forms,
reports, queries, macros, ribbons, tables, and modules. Two related
concerns share the repo: (1) raw extraction + mechanical enrichment
(COM/DAO pull → `raw/` → `kb/` → `ir/`), source material lives in the
sibling `../AccessExporterTemp1` repo and is being imported here; and
(2) a structured VBA / Access-expression parser whose plan and test
cases live under `docs/VBA-Parser-Migration-Harvester-Plan.md` and
`docs/test-cases/`. AI interpretation of the harvested metadata is
explicitly *not* part of this codebase. Quality gates are automated
build + tests + schema validation backed by mandatory cross-provider
AI verification (no human-UAT layer; no UI surface).

---

## Where durable content lives

| Topic | Authoritative file |
|---|---|
| Durable strategic commitments (principles, conventions) | `docs/planning/project-guidance.md` |
| Practical lessons from execution | `docs/planning/lessons-learned.md` |
| Session-set workflow — procedure, rules, router usage, verification, close-out | `docs/ai-led-session-workflow.md` |
| VBA parser plan (target schema, scope, milestones) | `docs/VBA-Parser-Migration-Harvester-Plan.md` |
| VBA parser test cases (input + expected output fixtures) | `docs/test-cases/` |
| Parser output schema (v0) | `docs/test-cases/harvester-schema.v0.json` |
| Orchestrator bootstrap (API keys, import snippets) | `CLAUDE.md`, `AGENTS.md`, `GEMINI.md` |
| Router design rationale (rules-first verifier selection, complexity-heuristic stopping point) | Comment blocks inline in `ai-router/router-config.yaml` |
| LLM instructions used for every routed task | `ai-router/prompt-templates/system-prompts.md`, `task-prompts.md`, `verification.md` |
| Source codebase being imported | sibling repo `../AccessExporterTemp1/` (and its `BATON.md`) |

If a fact is not in one of these files, treat it as unresolved rather
than reconstructing it from chat history.

---

## Current state (as of project bootstrap)

The repo was just bootstrapped from scratch. The only pre-existing
content was:

- `docs/VBA-Parser-Migration-Harvester-Plan.md` — full migration plan
  authored before this repo existed
- `docs/test-cases/` — 12 input fixtures (`.bas` / `.cls` / `.sql` /
  `.txt`) plus matching `<n>.expected.json` files, the v0 schema
  (`harvester-schema.v0.json`), and Python validators
  (`validate.py`, `validate_verbose.py`, `fix_locations.py`)

The bootstrap added:

- `ai-router/` — copied from the sibling `../dabbler-platform` repo
- `AGENTS.md`, `CLAUDE.md`, `GEMINI.md` — adapted from the sibling
  repo, stripped of UI/UAT/Blazor concerns
- `docs/ai-led-session-workflow.md` — adapted from the sibling repo,
  stripped of UAT-checklist machinery and Playwright/E2E references;
  cross-provider AI verification kept and elevated as the primary
  quality gate
- `docs/planning/project-guidance.md` — rewritten for the harvester
  domain (output architecture, parser strategy, lean-by-default,
  testing hierarchy without UI tiers)
- `docs/planning/lessons-learned.md` — seeded with workflow-discipline
  lessons from sibling-repo execution that apply here too;
  UI/MudBlazor/Playwright entries dropped
- `docs/planning/project-status.md` — this file

There is no C# solution yet. The first session set imports the
relevant projects from `../AccessExporterTemp1`.

---

## Planned session sets

Three session sets are authored at bootstrap. They are sequential:

1. **`accessexportertemp1-import`** — discovery + import of relevant
   code from `../AccessExporterTemp1`. Self-scoping: the first
   session decides what to import based on the BATON.md, the
   sibling's structure, and a routed planning task.
2. **`vba-parser-session-set-generator`** — meta session set whose
   deliverable is a tree of child session sets that implement the
   VBA parser plan, plus per-session-set routing metadata (which
   `task_type`, model, params, complexity hint per task).
   Pre-requisite: this can run in parallel with #1, but the child
   session sets it *generates* should not start executing until the
   import in #1 has stabilized.
3. **`remaining-work-planner`** — meta session set that, after #1
   and #2 are complete, plans the remaining work to reach a shippable
   harvester. Authored at bootstrap with explicit preconditions
   stated; do not start it until #1 and #2 have committed
   `change-log.md` files.

---

## Working voice (carried forward from sibling repo)

The discipline below was developed across sibling-repo sessions and is
worth keeping visible because it shapes how the orchestrator should
operate here too:

- **Don't over-engineer.** When in doubt, ship the simpler answer.
  The user's lean-by-default preference is a non-trivial constraint
  for LLM orchestrators that tend to over-engineer.
- **Don't propose new custom tools.** Extensions to existing files
  and existing tools are preferred over new surfaces.
- **Push back when warranted.** Real pushback with reasoning is more
  valuable than agreement. Cross-provider review itself sometimes
  surfaces incorrect findings; verifying before implementing is part
  of the discipline.
- **Budget tool calls carefully.** Multi-session work benefits from
  clean partial handoffs over rushed completion in one session.
- **Be precise about what's verified.** The harvester produces
  structured data downstream consumers diff across runs;
  approximations and "close enough" outputs are correctness defects,
  not stylistic ones.
