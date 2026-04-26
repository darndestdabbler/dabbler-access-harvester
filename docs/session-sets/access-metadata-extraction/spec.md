# Access Metadata Extraction Spec

> **Purpose:** Extract form, report, query, and table metadata from `.accdb` files via DAO/ACE — and author the database-level fixtures Phase 4.1–4.3 requires (which the existing 12 module-level test cases do not provide). Runs in parallel with the entire `#3-#9` resolver branch since it depends only on `parser-foundations` (#1).
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/access-metadata-extraction/`
> **Prerequisite:** docs/session-sets/parser-foundations/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #10. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `access-metadata-extraction` |
| **Plan-phase coverage (column 3)** | Phase 4.1-4.3 (form, report, query, table metadata); authors `.accdb` fixtures |
| **Prereqs (column 4)** | #1 (independent of resolver work above; runs in parallel with #2-#9) |
| **Sessions (rough) (column 5)** | 5-6 |
| **Synthesis count (column 6)** | 2 (metadata schema for forms/reports, query+table extractor) |
| **Outputs (column 7)** | `AccessHarvester.Access/FormMetadata.cs`, `ReportMetadata.cs`, `QueryMetadata.cs`, `TableMetadata.cs`; >=7 paired `.accdb` fixtures under `tests/AccessHarvester.Access.Tests/SyntheticDbs/` built by `test-builder/` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 4.1–4.3** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. Produces `AccessHarvester.Access/{Form,Report,Query,Table}Metadata.cs` plus ≥7 paired `.accdb` fixtures under `tests/AccessHarvester.Access.Tests/SyntheticDbs/` built by the existing `test-builder/` Python tool committed by `accessexportertemp1-import`. Minimum coverage: one fixture per case `01`/`02`/`03`/`09`/`10`/`11`/`12` that has a form-or-report dependency. Phase 4.4–4.5 (symbol-table + type-library integration) lives in the next child set, `access-symbol-table-and-types` (#11).

The COM-based extractor requires Windows + Microsoft Access installed. Per `project-guidance.md` → Platform And Runtime, the extractor is gated `[SupportedOSPlatform("windows")]` at the assembly level; integration tests against committed `.accdb` fixtures use `[SkippableFact]`.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #10.

---

## Feature 1: Form + Report Metadata Schema (Synthesis)

### Scope

Form and report metadata per plan Phase 4.1–4.2; per-field enumeration is Session 1 elaboration. The shape commitment is "one designed-once schema covering both because they share most structure" plus reuse of the existing tier-output conventions established by `accessexportertemp1-import` (geometry, provenance, only-emit-what-Access-wrote).

`FormMetadata.cs` and `ReportMetadata.cs` under `AccessHarvester.Access/`.

### Standards

- Output JSON conventions follow `project-guidance.md` → Output JSON Conventions verbatim (provenance shape, only-emit-what-Access-wrote, geometry units in field names). Stub does not re-state them; the elaborating orchestrator reads them from project-guidance.

## Feature 2: Query + Table Metadata Schema

### Scope

Per query (Phase 4.3): name, SQL text, query type (SELECT, MAKE TABLE, UPDATE, DELETE, INSERT, CROSSTAB), parameters (declared via `PARAMETERS` clause or implicit `[Forms]![X]` references), output column metadata when available.

Per table (Phase 4.3): name, columns (types/sizes/nullability), primary key, indexes, relationships (foreign keys), validation rules (table-level + column-level), default values.

`QueryMetadata.cs` and `TableMetadata.cs` under `AccessHarvester.Access/`.

### Standards

- SQL text is captured verbatim; structural parsing of SQL is `jet-sql-grammar-and-parser` (#12) work, not this set's.
- Relationships emit a structured graph; downstream enrichment consumes that, not raw FK strings.

## Feature 3: Database-Level Fixture Authoring

### Scope

Use `test-builder/` to author ≥7 `.accdb` fixtures committed under `tests/AccessHarvester.Access.Tests/SyntheticDbs/`. Each fixture corresponds to one of the existing module-level cases (`01`/`02`/`03`/`09`/`10`/`11`/`12`) with the forms/reports/queries/tables that case's VBA references.

### Standards

- Synthetic DBs are built by `test-builder/`'s Python (pywin32 + Access COM), committed as `.accdb` under `tests/.../SyntheticDbs/` per `project-guidance.md` → Platform And Runtime. Integration runs do not depend on a rebuild step.
- Each fixture's expected metadata extraction lands as a paired `expected.json` next to the `.accdb`.

---

## Session Plan

### Session 1 of 5-6: Form + Report Metadata Schema (Synthesis)

- Read Phase 4.1–4.2 + the existing tier output for `FormDef`/`ReportDef` from `accessexportertemp1-import`.
- Route an `architecture` synthesis call producing `FormMetadata.cs` + `ReportMetadata.cs` (1 of 2 synthesis sessions).
- Land schema types under `AccessHarvester.Access/`.

### Session 2 of 5-6: Query + Table Extractor (Synthesis)

- Read Phase 4.3 + tier output for `QueryDef`/`TableDef` from `accessexportertemp1-import`.
- Route an `architecture` synthesis call producing `QueryMetadata.cs` + `TableMetadata.cs` + extractor logic (2 of 2 synthesis sessions).
- Land schema types + extractor under `AccessHarvester.Access/`.

### Session 3 of 5-6: Database-Level Fixture Authoring (≥7 Fixtures)

- Identify the 7 anchor cases requiring forms/reports.
- Use `test-builder/` to build each fixture; commit `.accdb` files under `tests/AccessHarvester.Access.Tests/SyntheticDbs/`.
- Pair each with `expected.json` capturing extracted metadata.

### Session 4 of 5-6: Form + Report Extractor Wiring

- Run extractor against the 7 fixtures; confirm output matches `expected.json` for forms/reports.
- Route `schema-validation` for extractor output vs. `expected.json` (mechanical correspondence; integration tests are `[SkippableFact]`).

### Session 5 of 5-6: Query + Table Extractor Wiring + Cross-Cut

- Run query/table extractor against the 7 fixtures.
- Land tightening commits + `[SkippableFact]` integration tests.

### Session 6 of 5-6 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Form/report schema (Session 1) + query/table extractor (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=75)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Schema design.
- **Fixture authoring (Session 3):** Mechanical Python work via `test-builder/`. May routed `test-generation` for the per-fixture VBA-references-to-Access-objects mapping.
- **Mechanical tightening (Sessions 4 + 5):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the metadata schema types, the fixtures' `expected.json`, and the existing tier-schema deltas from `parser-foundations` so the verifier can check the schemas align.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — Phase 0.1 corpus expansion is deferred to `integration-testing-and-acceptance` (#18) per the parent tree's OQ 1(a).
