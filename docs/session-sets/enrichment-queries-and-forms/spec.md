# Enrichment — Queries and Forms Spec

> **Purpose:** Produce the structured query-abstraction and form-component specifications downstream Blazor migration tooling consumes — converting parsed SQL ASTs and Access form metadata into method-shaped query specs and Blazor-component-shaped form specs.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/enrichment-queries-and-forms/`
> **Prerequisite:** docs/session-sets/jet-sql-resolution-and-dynamic/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #14. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `enrichment-queries-and-forms` |
| **Plan-phase coverage (column 3)** | Phase 6.1-6.2 (query abstraction + form component spec) |
| **Prereqs (column 4)** | #13 |
| **Sessions (rough) (column 5)** | 4 |
| **Synthesis count (column 6)** | 2 (query-abstraction structure, form-spec structure) |
| **Outputs (column 7)** | `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs`, `AccessHarvester.Pipeline/Enrichment/FormComponentSpec.cs` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 6.1–6.2** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. The plan calls Phase 6 "where the harvester's value is created for the downstream consumer": parsed ASTs become structured specifications a Blazor-migration AI can translate. This set covers the two foundational enrichment outputs — query abstraction and form component spec — that the next two enrichment sets (#15 events + validation, #16 functions + navigation + review) build on.

Test-case anchors per the parent tree's coverage table: `04` (query abstraction), `02` + `12` for form-spec generation; full form-spec generation requires `.accdb` fixtures from #10.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #14.

---

## Feature 1: Query Abstraction Generation (Synthesis)

### Scope

Per Phase 6.1, for each query (saved or inline), produce a structured specification:
- Method name (derived from query name or context)
- Parameters (with types resolved from form-control references or table column types)
- Return shape (columns from SELECT list with inferred types)
- Filter predicates (WHERE clause as structured tree)
- Sort order
- JOINs as structured navigation
- Special markers: aggregations, grouping, distinct, top N

Output: `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs` plus the visitor producing it from #12's SQL AST and #11's symbol table.

### Standards

- The query abstraction is a translation-ready specification, not an opinion about translation. Translation lives downstream.
- Interpretive slots (`purpose`, `intent`, `notes`) remain null per `project-guidance.md` → Output Architecture ("Interpretive slots remain null at the `ir/` tier").

## Feature 2: Form Component Specification (Synthesis)

### Scope

Per Phase 6.2, for each form, produce a Blazor-component-shaped specification:
- Component name (derived from form name)
- Data source binding (record source as a query reference — points at a Feature 1 output)
- Control specifications (each control with type, binding, properties relevant to UI generation)
- Event handler references (each event handler classified — classification itself is #15's work; this set lands the reference shape)
- Subform relationships
- Layout hints (control positions, sizes, sections)
- Validation rules (extracted from BeforeUpdate handlers and table validation rules — extraction is #15's work; this set lands the reference shape)

Output: `AccessHarvester.Pipeline/Enrichment/FormComponentSpec.cs`.

### Standards

- Per Lean By Default, do not pre-compute every Blazor-rendering decision; capture the structure and let the consumer decide.
- Geometry hints use the harvester's existing inch + twip conventions per `project-guidance.md` → Output JSON Conventions.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
```

## Sessions
### Session 1 of 4: Query Abstraction Structure (Synthesis)

- Read Phase 6.1 + fixture `04`'s `expected.json` + #12/#13 SQL AST output.
- Route an `architecture` synthesis call producing `QueryAbstraction.cs` + visitor (1 of 2 synthesis sessions).
- Land under `AccessHarvester.Pipeline/Enrichment/`.

### Session 2 of 4: Form Component Spec Structure (Synthesis)

- Read Phase 6.2 + fixtures `02`, `12` + #10 form-metadata schema.
- Route an `architecture` synthesis call producing `FormComponentSpec.cs` (2 of 2 synthesis sessions).
- Land under `AccessHarvester.Pipeline/Enrichment/`.

### Session 3 of 4: End-to-End Pipeline + Mechanical Tightening

- Run pipeline against `04` (query) and `02`/`12` (form). Confirm output shape matches Phase 6.1/6.2.
- Route `schema-validation` for enrichment output vs. `harvester-schema.v0.json`.
- Land tightening commits.

### Session 4 of 4: Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Query abstraction (Session 1) + form-spec (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=75)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Per the playbook (forthcoming from #4 of generator), enrichment-pipeline classification is `architecture` when novel and `analysis` when well-trodden; query+form abstractions are novel design surfaces.
- **Mechanical tightening (Session 3):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the SQL AST, form metadata, and fixtures `04`/`02`/`12`.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None.
