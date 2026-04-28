# Schema Finalization and Documentation Spec

> **Purpose:** Promote `harvester-schema.v0.json` to v1.0 with backward-compatibility plan, land example fixtures for every major schema construct, wire the output-validation pass that fails the harvester on schema-non-conforming output, and generate the human-readable schema documentation.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/schema-finalization-and-docs/`
> **Prerequisite:** docs/session-sets/enrichment-events-and-validation/, docs/session-sets/enrichment-functions-navigation-review/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: This repo has no UI surface. The deliverables (a JSON Schema file, a validation C# class, and generated markdown docs) are internal pipeline artifacts consumed by downstream AI tools, not human-operated UI. Automated build/test plus cross-provider AI verification is the complete quality bar.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #17. This is the authoritative record; the prose below interprets these fields, never overrides them.

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

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #17.

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

### Session 1 of 3: Schema v0→v1 Review Pass (Synthesis)

**Steps:**
1. Elaborate this spec (add Session Set Configuration block and full step lists). *(Session 1 only — stub expansion.)*
2. Route `ai-assignment.md` authoring: `route(task_type="analysis")` with the full spec + all session step lists. Save to `docs/session-sets/schema-finalization-and-docs/ai-assignment.md`.
3. Enumerate the v0→v1 design decisions by reading `docs/schema/v0-vs-tiers.md` §11–12 and identifying the two open decisions (D20 casing normalization strategy; D1/D2 DatabaseManifest envelope shape). Route focused architecture sub-calls, each under ~1500 input tokens, to resolve each decision.
4. Apply all pre-decided gap fixes (D3: add `DeclareFunction`/`DeclareSub` to `procedureKind` enum; D4: rename `visibility` → `accessModifier`; D8: add flat `startLine` shorthand on `procedureItem`; D10: migrate `byRef:boolean` → `passingMechanism` string enum; D9: add `lib`/`alias` to `procedureItem`; D13: add DAO fields to `queryItem`; D16: add `databaseManifest`, `tableSchemas[]`, `relationships[]` top-level sections; plus casing and envelope decisions from step 3) to produce `docs/test-cases/harvester-schema.v1.0.json`.
5. Write `docs/schema/v1-adr.md` — an ADR-style note enumerating every v0→v1 change with rationale, a back-compatibility plan (v0 outputs remain valid until a migration script is shipped; consumer must check `schemaVersion`), and a summary of deferred items (still post-v1).
6. Run `dotnet build && dotnet test` to confirm nothing broke (no C# changes in this session).
7. End-of-session verification: `route(task_type="session-verification", complexity_hint=70)`. Include v0 schema, v1 schema, v1-adr.md, and disclose all non-schema files (enrichment C# models, test cases) are out of scope for this session.
8. Commit and push.

**Creates:**
- `docs/test-cases/harvester-schema.v1.0.json`
- `docs/schema/v1-adr.md`
- `docs/session-sets/schema-finalization-and-docs/ai-assignment.md`
- `docs/session-sets/schema-finalization-and-docs/session-state.json`
- `docs/session-sets/schema-finalization-and-docs/session-reviews/session-001.md`

**Touches:**
- `docs/session-sets/schema-finalization-and-docs/spec.md` (this file — stub expansion)

**Ends with:** `harvester-schema.v1.0.json` committed and verified against v0-vs-tiers.md gap list; ADR committed.

**Progress keys:** `session-1/spec-elaborated`, `session-1/ai-assignment`, `session-1/architecture-decisions`, `session-1/v1-schema`, `session-1/v1-adr`, `session-1/build-test`, `session-1/verification`

---

### Session 2 of 3: Examples + Output Validation Pass

**Steps:**
1. Read `ai-assignment.md` actuals for Session 1 and route a fresh recommendation for Session 3.
2. Generate example fixtures by running the harvester pipeline against `samples/NorthwindDevEdition.accdb` (Windows COM) and the committed synthetic test-case `.bas` + `.expected.json` files. Target: `examples/northwind-output.json`, `examples/minimal-form.json`, `examples/complex-form.json`.
3. Route `route(task_type="code-review")` on the generated examples for schema conformance spot-check.
4. Author `AccessHarvester.Pipeline/Validation/SchemaValidator.cs` — a validator that loads `harvester-schema.v1.0.json` at runtime and validates each pipeline output document before writing. Use `JsonSchema.Net` or equivalent NuGet package. Hard-fail the pipeline on schema-non-conformance.
5. Author unit tests in `tests/AccessHarvester.Pipeline.Tests/Validation/SchemaValidatorTests.cs` covering: (a) a conformant document returns no errors; (b) a document with a missing required field returns an error; (c) a document with an unknown field is accepted (additionalProperties not forbidden at v1).
6. Route `route(task_type="schema-validation", complexity_hint=40)` for examples-vs-schema correspondence (mechanical, Sonnet-pinned per retrospective #10). Include each example file and the v1 schema.
7. Run `dotnet build && dotnet test`.
8. End-of-session verification.
9. Commit and push.

**Creates:**
- `examples/northwind-output.json`
- `examples/minimal-form.json`
- `examples/complex-form.json`
- `AccessHarvester.Pipeline/Validation/SchemaValidator.cs`
- `tests/AccessHarvester.Pipeline.Tests/Validation/SchemaValidatorTests.cs`

**Touches:**
- `AccessHarvester.Pipeline/AccessHarvester.Pipeline.csproj` (NuGet reference)
- `tests/AccessHarvester.Pipeline.Tests/AccessHarvester.Pipeline.Tests.csproj` (NuGet reference)

**Ends with:** Examples committed and validated; validator wired into pipeline; tests green.

**Progress keys:** `session-2/ai-assignment-update`, `session-2/examples-generated`, `session-2/schema-validation-routed`, `session-2/validator-authored`, `session-2/tests-authored`, `session-2/build-test`, `session-2/verification`

---

### Session 3 of 3: Documentation Generation

**Steps:**
1. Read `ai-assignment.md` actuals for Session 2.
2. Author `AccessHarvester.Pipeline/DocGen/SchemaDocGenerator.cs` — walks a pipeline output document and produces: (a) a per-database markdown summary (database name, form count, query count, module count, unresolved-item count); (b) a form-by-form catalog section for each `FormComponentSpec` (form name, record source, control list, event handler count); (c) a dependency section listing subform relationships. Output path: `docs/output/<database-slug>/schema-summary.md`.
3. Wire `SchemaDocGenerator` into the harvester CLI (`dotnet run -- generate-docs <output-dir>`) so docs regenerate whenever output regenerates.
4. Route `route(task_type="documentation", complexity_hint=30)` for XML doc comments on `SchemaDocGenerator`'s public API.
5. Author unit tests in `tests/AccessHarvester.Pipeline.Tests/DocGen/SchemaDocGeneratorTests.cs` covering the summary and catalog sections against a minimal synthetic document.
6. Run `dotnet build && dotnet test`.
7. Generate change-log: `route(task_type="summarization", max_tier=1)`.
8. Route next-session-set recommendation: `route(task_type="analysis")`.
9. End-of-session verification.
10. Commit and push.

**Creates:**
- `AccessHarvester.Pipeline/DocGen/SchemaDocGenerator.cs`
- `tests/AccessHarvester.Pipeline.Tests/DocGen/SchemaDocGeneratorTests.cs`
- `docs/session-sets/schema-finalization-and-docs/change-log.md`
- `docs/session-sets/schema-finalization-and-docs/next-session-set-recommendation.md`

**Touches:**
- `AccessHarvester.Cli/` (new `generate-docs` subcommand)
- `AccessHarvester.Pipeline/AccessHarvester.Pipeline.csproj`

**Ends with:** Doc-gen wired and tested; change-log committed; set marked complete.

**Progress keys:** `session-3/ai-assignment-update`, `session-3/docgen-authored`, `session-3/cli-wired`, `session-3/tests-authored`, `session-3/build-test`, `session-3/change-log`, `session-3/next-set-recommendation`, `session-3/verification`

---

## Routing notes

- **Schema v1 finalization (Session 1):** `route(task_type="architecture", complexity_hint=70)`. The pass is mostly mechanical (per the tree's synthesis-count = 1) but versioning + back-compat decisions justify Opus once. `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`.
- **Examples + validation (Session 2):** `route(task_type="schema-validation", complexity_hint=40)` for examples-vs-schema correspondence; mechanical-correspondence, Sonnet-pinned per retrospective adopt-now `#10`. The validation pass code itself is mechanical and may be in-conversation.
- **Documentation (Session 3):** `route(task_type="documentation", complexity_hint=30)` only for code-doc generation (e.g., XML doc comments on the validation API). The per-database markdown summaries are generated outputs from the harvester pipeline, not routed.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include v0 schema, v1 schema, every enrichment output shape, and one example.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — schema versioning approach and back-compat are fixed by Phase 7.1.
