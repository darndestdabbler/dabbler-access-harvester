# Schema Finalization and Documentation Spec

> **Purpose:** Promote `harvester-schema.v0.json` to v1.0 with backward-compatibility plan, land example fixtures for every major schema construct, wire the output-validation pass that fails the harvester on schema-non-conforming output, and generate the human-readable schema documentation.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/schema-finalization-and-docs/`
> **Prerequisite:** docs/session-sets/enrichment-events-and-validation/, docs/session-sets/enrichment-functions-navigation-review/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #17. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `schema-finalization-and-docs` |
| **Plan-phase coverage (column 3)** | Phase 7.1-7.4 (JSON schema v1 finalization, examples, output validation, documentation generation) |
| **Prereqs (column 4)** | #15, #16 |
| **Sessions (rough) (column 5)** | 3-4 |
| **Synthesis count (column 6)** | 1 (schema v1 review pass; mostly mechanical against existing v0) |
| **Outputs (column 7)** | `docs/test-cases/harvester-schema.v1.0.json`, `examples/*.json`, output-validation pass under `AccessHarvester.Pipeline/Validation/`, generated markdown docs under `docs/schema/` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 7.1–7.4** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. The schema started in Phase 0.3 (`harvester-schema.v0.json`) has evolved through every parser/resolver/enrichment child set; this set finalizes for v1.0. The work is mostly mechanical against existing v0 (per the parent tree's synthesis count of 1) — confirming each enrichment output validates, deciding which fields are stable for v1, versioning, and writing documentation.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #17.

---

## Feature 1: Schema v1.0 Finalization (Synthesis)

### Scope

Per Phase 7.1, produce `docs/test-cases/harvester-schema.v1.0.json` (JSON Schema Draft 2020-12 or similar). Versioned. Backward-compatibility plan documented: schema changes increment version; downstream tools can request specific versions. Documentation describes every field's meaning and provenance.

The single synthesis task is the v0→v1 review pass: enumerate every field, decide stable / deprecated / removed, document rationale.

### Standards

- Per `project-guidance.md` → Output Architecture, "Interpretive slots remain null at the `ir/` tier" — v1 schema preserves interpretive slots (`purpose`, `notes`, `intent`) as nullable fields.
- Provenance fields use `{ source, ... }` shape per `project-guidance.md` → Output JSON Conventions.

## Feature 2: Schema Examples and Test Fixtures

### Scope

Per Phase 7.2, ship example fixtures: `examples/northwind-output.json` (full Northwind output), `examples/minimal-form.json` (smallest valid form output), `examples/complex-form.json` (form exercising every feature), per-construct example files for documentation.

### Standards

- Examples are generated outputs, not hand-edited. Run the harvester against fixtures and commit the result.
- Northwind example is regenerated whenever the schema changes; staleness is a build failure, not a docs problem.

## Feature 3: Output Validation Pass

### Scope

Per Phase 7.3, every harvester run validates its own output against the schema before writing. Failure on schema non-conformance is a hard error.

Output: validation pass under `AccessHarvester.Pipeline/Validation/`.

### Standards

- Validation is enforced, not aspirational. The harvester refuses to emit malformed output.

## Feature 4: Documentation Generation

### Scope

Per Phase 7.4, generate human-readable documentation alongside JSON: per-database markdown summary, form-by-form catalog, dependency diagrams (forms-and-reports level), manual review queue formatted for ticket creation. Lands under `docs/schema/`.

### Standards

- Documentation is generated; do not hand-write what the schema can produce.

---

## Session Plan

### Session 1 of 3-4: Schema v0→v1 Review Pass (Synthesis)

- Read v0 schema + every enrichment output shape + `docs/schema/v0-vs-tiers.md` from #1.
- Route an `architecture` synthesis call producing v1 schema + backward-compatibility plan (1 of 1 synthesis session for this set).
- Land `docs/test-cases/harvester-schema.v1.0.json` + ADR-style note documenting v0→v1 changes.

### Session 2 of 3-4: Examples + Output Validation Pass

- Generate `examples/*.json` from the harvester pipeline against Northwind + a minimal fixture + a complex form fixture.
- Land output-validation pass under `AccessHarvester.Pipeline/Validation/`.
- Route `schema-validation` for examples-vs-schema correspondence.

### Session 3 of 3-4: Documentation Generation

- Generate per-database markdown summary, form-by-form catalog, dependency diagrams under `docs/schema/`.
- Wire generation into the harvester run; documentation regenerates whenever output regenerates.

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Schema v1 finalization (Session 1):** `route(task_type="architecture", complexity_hint=70)`. The pass is mostly mechanical (per the tree's synthesis-count = 1) but versioning + back-compat decisions justify Opus once. `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`.
- **Examples + validation (Session 2):** `route(task_type="schema-validation", complexity_hint=40)` for examples-vs-schema correspondence; mechanical-correspondence, Sonnet-pinned per retrospective adopt-now `#10`. The validation pass code itself is mechanical and may be in-conversation.
- **Documentation (Session 3):** `route(task_type="documentation", complexity_hint=30)` only for code-doc generation (e.g., XML doc comments on the validation API). The per-database markdown summaries are generated outputs from the harvester pipeline, not routed.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include v0 schema, v1 schema, every enrichment output shape, and one example.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — schema versioning approach and back-compat are fixed by Phase 7.1.
