# Enrichment — Functions, Navigation, Manual Review Spec

> **Purpose:** Classify every user-defined VBA procedure by translation strategy (pure / data-access / UI-bound / external I/O / mixed) with cyclomatic + dependency metrics; reconstruct the application's navigation graph from ribbons, switchboards, autoexec, and `DoCmd.OpenForm`/`OpenReport` calls; and produce the structured `UnresolvedItem` records for everything the harvester cannot fully process. Sibling-parallel with `enrichment-events-and-validation` (#15) under shared parent #14.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/enrichment-functions-navigation-review/`
> **Prerequisite:** docs/session-sets/enrichment-queries-and-forms/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #16. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `enrichment-functions-navigation-review` |
| **Plan-phase coverage (column 3)** | Phase 6.6-6.8 (VBA function classification, navigation extraction, manual review routing) |
| **Prereqs (column 4)** | #14 (parallel with #15) |
| **Sessions (rough) (column 5)** | 4-5 |
| **Synthesis count (column 6)** | 2 (function classification, navigation graph reconstruction) |
| **Outputs (column 7)** | `AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs`, `NavigationGraph.cs`, `UnresolvedItem.cs` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 6.6–6.8** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. Three deliverables that share infrastructure (the resolved AST, cross-reference index from #9, form metadata from #10): VBA function classification (#6.6), navigation/workflow extraction (#6.7), manual review routing (#6.8). The `UnresolvedItems` array surfaces in every harvester output run (Phase 0.3 output schema), so this set's `UnresolvedItem` shape is the union point for unresolved-construct provenance from every prior child set.

Test-case anchors per the parent tree's coverage table: `07` (legacy mixed-return → mixed classification), `10` (audit-logger class → pure/utility classification), `11` (AutoExec startup chain), `12` (switchboard navigation), `08` (manual-review routing — `Eval()` / dynamic form names / `Application.Run`).

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #16.

---

## Feature 1: VBA Function Classification (Synthesis)

### Scope

Per Phase 6.6, classify each user-defined function/sub by:
- **Pure** — no side effects, no external references; direct C# translation
- **Data access** — reads tables/queries, no UI; translates to repository or service method
- **UI-bound** — manipulates forms/controls; becomes Blazor component logic
- **External I/O** — file system, network, COM
- **Mixed** — multiple of the above; flagged for decomposition

Per function, additionally compute: cyclomatic complexity, lines of code, direct dependencies (called procedures, referenced controls), transitive dependency closure (via #9's cross-reference index), caller list.

Output: `AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs`.

### Standards

- Classification is deterministic; metrics are mechanical.
- Per Lean By Default, do not pre-decompose mixed functions; emit component classifications + a `mixed: true` marker.

## Feature 2: Navigation Graph Reconstruction (Synthesis)

### Scope

Per Phase 6.7, reconstruct the navigation graph from:
- Custom ribbon XML (`USysRibbons` table — surfaced via #10's table extractor)
- Switchboard items (`Switchboard Items` table if present)
- Navigation forms / autoexec macros
- `DoCmd.OpenForm`, `DoCmd.OpenReport` calls in event handlers
- Form `OnLoad`/`OnOpen` event navigations

Output: `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs` — top-level navigation structure (ribbon tabs, switchboard menus), form-to-form navigation graph with triggers, entry points (autoexec, default form).

### Standards

- The graph is structured (nodes + edges + trigger metadata), not a flat list.
- Entry points are explicit and ordered (AutoExec → default form → switchboard root).

## Feature 3: Manual Review Routing

### Scope

Per Phase 6.8, every construct the harvester cannot fully process produces a structured `UnresolvedItem` record: source location, construct description, reason, surrounding context, suggested next steps. The output `UnresolvedItems` array is the union of every prior child set's unresolved emissions; this set defines the canonical shape and lands the aggregator.

Output: `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs` plus the aggregator producing the run-wide `UnresolvedItems` array.

### Standards

- Every prior child set's unresolved-emission shape (resolver, SQL form-reference, dynamic-SQL analyzer, validation extractor) maps to `UnresolvedItem` without information loss.
- Per the project guidance's "unresolvable constructs are explicit, not silent" — `UnresolvedItem` records are the explicit form.

---

## Session Plan

### Session 1 of 4-5: Function Classification (Synthesis)

- Read Phase 6.6 + fixtures `07` (mixed-return), `10` (utility class).
- Route an `architecture` synthesis call producing classifier + metrics (1 of 2 synthesis sessions).
- Land under `AccessHarvester.Pipeline/Enrichment/`.

### Session 2 of 4-5: Navigation Graph (Synthesis)

- Read Phase 6.7 + fixtures `11` (AutoExec), `12` (switchboard) + #10's ribbon/switchboard metadata.
- Route an `architecture` synthesis call producing graph reconstruction (2 of 2 synthesis sessions).
- Land under `AccessHarvester.Pipeline/Enrichment/`.

### Session 3 of 4-5: Manual Review Routing + Aggregator

- Read Phase 6.8 + every prior child set's unresolved-emission shapes.
- Land `UnresolvedItem.cs` + aggregator. May be in-conversation if the orchestrator is Opus, otherwise routed `analysis`.

### Session 4 of 4-5: End-to-End + Mechanical Tightening

- Run full pipeline (#1 through this set) against `07`, `08`, `10`, `11`, `12`. Confirm classification + navigation + unresolved aggregation in run output.
- Route `schema-validation` for output vs. `harvester-schema.v0.json`.
- Land tightening commits.

### Session 5 of 4-5 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Function classification (Session 1) + navigation graph (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=75)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Novel taxonomy + graph reconstruction.
- **Manual review aggregator (Session 3):** `route(task_type="analysis", complexity_hint=55)` — well-trodden aggregation shape, but unification of every prior set's unresolved provenance warrants careful design.
- **Mechanical tightening (Session 4):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the resolved AST, cross-reference index, form metadata, and the 5 anchor fixtures.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — taxonomy is fixed by Phase 6.6; navigation sources are enumerated in Phase 6.7; `UnresolvedItem` shape is fixed by Phase 6.8.
