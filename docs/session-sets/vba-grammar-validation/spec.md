# VBA Grammar Validation Spec

> **Purpose:** Run the full `Vba.g4` grammar against the 12 module-level fixtures plus hazard-list-driven edge fixtures, classify and fix every failure, and produce the parse-failure log that downstream parser child sets treat as the regression contract.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/vba-grammar-validation/`
> **Prerequisite:** docs/session-sets/vba-statement-and-module-grammar/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #5. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `vba-grammar-validation` |
| **Plan-phase coverage (column 3)** | Phase 1.5-1.6 corpus validation + edge case coverage; authors edge-case fixtures |
| **Prereqs (column 4)** | #4 |
| **Sessions (rough) (column 5)** | 3-4 |
| **Synthesis count (column 6)** | 1 (edge-case-driven grammar fix loop) |
| **Outputs (column 7)** | Grammar bug-fix commits in `AccessHarvester.Vba.Grammar/Vba.g4`, `docs/spec-coverage/parse-failure-log.md`, edge-case fixtures under `docs/test-cases/edge/` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 1.5–1.6** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. The plan's "≥95% on representative corpus" target is reframed by the parent tree as **"100% on the 12 module-level cases plus all hazard-list edge fixtures"** — full `.accdb` corpus validation is deferred to `integration-testing-and-acceptance` (#18) per Open Question 1(a). This child set authors the edge-case fixtures the hazard list demands, drives the grammar to that target, and lands the parse-failure log.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #5.

---

## Feature 1: Edge-Case Fixture Authoring

### Scope

For each hazard in `docs/spec-coverage/hazard-list.md` not already covered by the 12 module-level fixtures, author a paired source/expected-output fixture under `docs/test-cases/edge/<NN>-<slug>.bas|.cls` + `<NN>-<slug>.expected.json`. Count is determined by the hazard list (typical: 5–15 fixtures).

### Standards

- Fixtures follow the same shape as the existing 12 cases — input file plus pinned `expected.json` per `project-guidance.md` → VBA Parser Strategy.
- Each edge fixture's expected JSON validates against `harvester-schema.v0.json`.
- Fixtures are minimal: one fixture exercises one hazard, not a kitchen sink.

## Feature 2: Grammar Fix Loop

### Scope

Iterative loop: run the harness on the full corpus (12 module-level + edge fixtures), classify every failure (grammar bug / missing construct / malformed input / ambiguous), fix in priority order. Continue until 100% parse on the corpus.

### Standards

- "Won't fix" is a real category but requires explicit justification logged in `docs/spec-coverage/parse-failure-log.md`.
- Grammar fixes commit to `Vba.g4` in place; no rewrites of #3 / #4 work without justification.

## Feature 3: Parse-Failure Log

### Scope

Land `docs/spec-coverage/parse-failure-log.md` — the running ledger of every failure encountered in this set, its classification, and its resolution. Downstream child sets append to this file when they discover new failures.

### Standards

- Every entry has: fixture, line/column, classification, resolution (fixed in commit X / deferred / won't-fix-justification).

---

## Session Plan

### Session 1 of 3-4: Edge-Case Fixture Authoring

- Read `docs/spec-coverage/hazard-list.md` and the 12 fixtures' coverage.
- Route `test-generation` for each non-covered hazard producing a fixture pair.
- Land fixtures under `docs/test-cases/edge/`.

### Session 2 of 3-4: Grammar Fix Loop (Synthesis Session)

- Run harness against full corpus including edge fixtures.
- Route an `architecture` synthesis call for each non-trivial grammar bug requiring a rule-shape decision (1 of 1 synthesis session for this set; bug-fix loops typically aggregate into a single routed call rather than per-bug).
- Land grammar fixes; iterate harness until 100%.

### Session 3 of 3-4: Parse-Failure Log + Cross-Cut

- Land `docs/spec-coverage/parse-failure-log.md` with every failure encountered, classification, and resolution.
- Route `schema-validation` over the log to verify every "fixed" entry maps to a real commit.

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for resolving cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Edge fixture authoring (Session 1):** `route(task_type="test-generation", complexity_hint=40)` per hazard. Each call is small; aggregate cost is low.
- **Grammar fix synthesis (Session 2):** `route(task_type="architecture", complexity_hint=80)` for the design-shape decisions emerging from the loop. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Per the playbook (forthcoming from #4 of generator) and retrospective `#13`.
- **Parse-failure log + cross-cut (Session 3):** `route(task_type="schema-validation")` for log-vs-commit correspondence. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the hazard list, edge fixtures, and the parse-failure log.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — the "100% on module-level + edge fixtures" target replaces the plan's `≥95% on representative corpus` per the parent tree's OQ 1(a) decision.
