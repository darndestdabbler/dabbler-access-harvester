# AI Orchestration Project Template Spec

> **Purpose:** Distill the AI-orchestration scaffolding pattern this repo
> uses — `CLAUDE.md` / `AGENTS.md` / `GEMINI.md`, `ai-router/`,
> `docs/planning/{project-guidance,lessons-learned}.md`,
> `docs/ai-led-session-workflow.md`, the session-set tree-generator
> pattern, the `tools/vscode-session-sets/` extension — into a reusable
> bootstrap template (and accompanying setup guide) for new projects.
> **Created:** 2026-04-26
> **Session Set:** `docs/session-sets/ai-orchestration-project-template/`
> **Prerequisite:** None — meta workstream, runs independently of the
> harvester's session-set DAG. Treats the harvester repo itself as the
> reference implementation.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this
> set must elaborate the step lists before running them. Several scope
> decisions below are deliberately left as open questions for the human.

---

## Project Overview

The harvester arrived at a coherent AI-orchestration pattern incrementally
across several session sets (`accessexportertemp1-import`,
`parser-foundations`, `vba-parser-session-set-generator`,
`vba-spec-study`, plus the in-flight metadata-extraction +
lexical-and-expression-grammar pair). Standing up that same pattern in a
new project today means hand-copying a dozen files and editing each one
for the new codebase — which is friction every time, and exactly the
kind of friction that loses fidelity over copies.

This set bundles the pattern into a **template** (a scaffold a new
project can clone or unpack) and a **setup guide** (the human-readable
how-to for filling in the project-specific blanks). It must support two
variants — a **headless** variant (like the harvester: CLI / library /
JSON output, no UI surface, cross-provider AI verification as the sole
end-of-session quality gate) and a **UI** variant (Blazor / web /
desktop apps, where session-set output includes a UAT checklist and
E2E / Playwright tests as the quality gate).

---

## Features (placeholder — Session 1 elaborates)

### Feature 1: Inventory and Classify the Scaffolding Artifacts

Walk the harvester's repo and inventory every file that belongs to the
orchestration scaffold rather than the harvester's domain logic.
Classify each as one of:

- **Project-agnostic** — copy verbatim (e.g., `ai-led-session-workflow.md`,
  `tools/vscode-session-sets/`, most of `ai-router/` apart from the
  task-type overrides).
- **Project-parameterized** — has a small number of project-specific
  knobs the template must expose (e.g., `CLAUDE.md` mentions the project
  name and required-reading paths; `router-config.yaml`'s
  `task_type_overrides` are calibrated per-orchestrator).
- **Project-specific seed** — needs a starting-point but the new project
  must own and grow it (e.g., `project-guidance.md` Principles section;
  `lessons-learned.md` starts seeded with the workflow-discipline
  lessons but project-specific lessons accrue).

### Feature 2: Headless Variant Template

The harvester is the reference. The template captures a headless
project's setup: solution file, ai-router import bootstrap, the three
orchestrator instruction files, the workflow doc, the planning docs,
the session-set generator pattern, the VS Code extension. End-of-
session quality gate: build + test + cross-provider AI verification.

### Feature 3: UI Variant Template (UAT Checklist + E2E Testing)

Mirror the headless variant but add:

- A **UAT checklist** template per session (consumed by
  `task_type="uat-plan-generation"` and `task_type="uat-coverage-review"`,
  which already exist in `router-config.yaml` but are unused in the
  harvester per the workflow doc's UAT note).
- **Playwright / E2E test scaffolding** with a representative golden-
  path test, a CI hook, and the project-guidance entry that pins
  "feature correctness is verified by playing through the UI; type
  checking and tests verify code correctness, not feature correctness."
- The session-verification prompt must include a UI-screenshot or
  recorded-walkthrough disclosure, so the cross-provider verifier
  audits the actual user-facing behavior rather than just code.

The two existing sibling repos the human has set up this pattern in
(name TBD — see open question 1) are the reference implementations.

### Feature 4: Setup Guide

A human-readable `BOOTSTRAP.md` (or similar) that walks a developer
through unpacking the template into a new project: required env vars,
API key onboarding, choice of headless vs UI variant, where to edit
the project-specific seeds. Should answer the two questions a new user
will hit first: "what are the cheapest models I can run this on?" and
"how do I know the cross-provider verification is actually firing?"

### Feature 5: Smoke-Test the Template Against a Fresh Project

Take the produced template and run a real "Session 0" against a brand-
new, empty repo. Confirm the entire setup flow works end-to-end. Use
the ouput of this dry-run to back-fill any gaps in the setup guide.

---

## Session Plan (placeholder — bullet hints only)

### Session 1 of N: Inventory + Classification

- Walk the harvester repo, list every scaffold artifact, classify
  each per Feature 1.
- Route an `analysis` call to produce the classification table.
- Land it as `inventory.md` under this session set.

### Session 2 of N: Headless Template Authoring (Synthesis)

- Take the project-agnostic + project-parameterized artifacts and
  produce the template skeleton with parameter placeholders.
- Author `BOOTSTRAP.md` for the headless variant.

### Session 3 of N: UI Variant Authoring (Synthesis)

- Add UAT checklist template + Playwright scaffold + UI-variant
  workflow-doc deltas.
- Pull the patterns from the human's sibling UI repos (open question 2).

### Session 4 of N: Smoke-Test on a Fresh Repo (Per Feature 5)

- Stand up an empty test repo, unpack the template, run Session 1 of
  a synthetic session set against it, audit every step.
- Capture gaps as ISSUES_FOUND and resolve in Session 5.

### Session 5 of N (buffer): Cleanup + Promotion

- Address smoke-test gaps.
- Decide where the template lives long-term (open question 3).

> Step lists above are bullet hints. The orchestrator running each
> session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes (placeholder)

- **Inventory + classification:** `route(task_type="analysis",
  complexity_hint=55)`.
- **Template authoring (Sessions 2 + 3):** `route(task_type="architecture",
  complexity_hint=70)` or in-conversation Opus per the
  synthesis-in-conversation Convention.
- **Smoke-test audit:** `route(task_type="session-verification",
  complexity_hint=70)`.

---

## Open questions for the human

1. **Reference UI repos.** Which sibling repos use the harvester-style
   AI orchestration pattern with UAT + E2E setup? The Session 3
   synthesis needs their patterns as input. Likely candidates from the
   environment's additional working directories: `chromatic-notation-configurator`,
   `issue-tracking-with-ai`, `nedss-port`, `essence-port`, `access-to-blazor`,
   `task-and-issue-handler`, `dabbler-platform`, `dabbler-pdf`,
   `dabbler-components`, `uat-checklist-editor`. Confirm which two or
   three are the canonical references.

2. **Where the template lives.** Three options:
   - (a) A new sibling repo (`dabbler-orchestration-template`) the user
     clones / forks for new projects.
   - (b) A subdirectory in this repo (`tools/orchestration-template/`)
     with extraction scripts.
   - (c) A `cookiecutter` / `degit` template hosted on GitHub.

3. **Should the template be opinionated about orchestrator choice?**
   The pattern accommodates Claude / Codex / Gemini interchangeably.
   The template could ship with all three instruction files (current
   harvester practice) or only the human's preferred subset. Recommend
   shipping all three — adds little weight, preserves cross-orchestrator
   handoff (which proved valuable in `access-metadata-extraction`
   Session 1).

4. **Does the VS Code extension need parameterization?** Currently it
   hard-codes `docs/session-sets/` as the scan root. Probably fine as a
   convention; flag here in case the template wants to expose it as a
   workspace setting.
