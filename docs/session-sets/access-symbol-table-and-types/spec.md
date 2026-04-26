# Access Symbol Table and Type Library Spec

> **Purpose:** Inject Access metadata (forms, reports, queries) into the VBA resolver as synthetic declarations so VBA references to controls/forms/reports resolve correctly, and curate the type-library registry covering the most common DAO/ADO/Office members.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/access-symbol-table-and-types/`
> **Prerequisite:** docs/session-sets/resolver-default-members-and-inference/, docs/session-sets/access-metadata-extraction/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #11. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `access-symbol-table-and-types` |
| **Plan-phase coverage (column 3)** | Phase 4.4-4.5 (symbol-table integration + type library registry) |
| **Prereqs (column 4)** | #9, #10 |
| **Sessions (rough) (column 5)** | 3-4 |
| **Synthesis count (column 6)** | 2 (synthetic-declaration injection design, type-library registry curation) |
| **Outputs (column 7)** | Resolver extensions to consume Access metadata under `AccessHarvester.Vba.Resolver/AccessIntegration/`; `AccessHarvester.Access/TypeLibraryRegistry.cs` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 4.4–4.5** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. This is the bridge between the VBA resolver (#7–#9) and the Access metadata extractor (#10): the resolver alone cannot resolve form/report/control references because those declarations don't exist in VBA source — they exist in `.accdb` design metadata. This set populates the resolver's symbol table with synthetic declarations derived from extracted metadata, and lands the type-library registry covering DAO, ADO, Office, and Access object model members not already covered by Feature 1.

Test-case anchors per the parent tree's coverage table: `01`, `02`, `03` (require form metadata to resolve fully). Inherits `.accdb` fixtures from #10.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #11.

---

## Feature 1: Synthetic Declaration Injection (Synthesis)

### Scope

Per Phase 4.4:
- Each form module's class scope is populated with synthetic declarations for every control on the form (one declaration per control with type derived from the control's class).
- The project scope is populated with synthetic declarations for every form and report (via `Forms` and `Reports` collections).
- The class hierarchy reflects subform navigation: `subformControl.Form` resolves to the contained form's class.

Output: `AccessHarvester.Vba.Resolver/AccessIntegration/` with the injection visitor, the synthetic-declaration types, and the resolver hooks #8 and #9 already call into.

### Standards

- Synthetic declarations carry provenance — `{ source: "synthetic-from-access", artifact: "<form-name>" }` — so downstream consumers can distinguish them from VBA-source declarations.
- Subform control's `.Form` property navigation is data, not code branching. The metadata extractor records the subform-to-form binding; the resolver dereferences it.

## Feature 2: Type Library Registry (Synthesis)

### Scope

Per Phase 4.5 ("pragmatic approach"): hand-curated registry of the most common members for DAO (Recordset, Field, Database, QueryDef), ADO (Recordset, Connection, Command), and Office (FileDialog, etc.). For types not in the registry, references are marked "external — type unknown" rather than failing.

Output: `AccessHarvester.Access/TypeLibraryRegistry.cs`.

### Standards

- Per the plan: "Defer full type-library introspection to a later iteration if needed." This set ships a curated seed registry; expansion is post-v1.
- Graceful degradation is the contract — unknown-type references resolve with `{ source: "external-unknown", typeLibrary: "<name>" }` rather than emitting an unresolved.

---

## Session Plan

### Session 1 of 3-4: Synthetic Declaration Injection (Synthesis)

- Read Phase 4.4 + #9's resolver output + #10's metadata schema + fixtures `01`, `02`, `03`'s `.accdb` from #10.
- Route an `architecture` synthesis call producing the injection visitor + integration with the resolver pipeline (1 of 2 synthesis sessions).
- Land injection under `AccessHarvester.Vba.Resolver/AccessIntegration/`.

### Session 2 of 3-4: Type Library Registry Curation (Synthesis)

- Read Phase 4.5 + plan Appendix A's "Recordset patterns" plus Access object model surfaces.
- Route an `architecture` synthesis call producing the registry seed (2 of 2 synthesis sessions).
- Land `AccessHarvester.Access/TypeLibraryRegistry.cs`.

### Session 3 of 3-4: Cross-Cut + Mechanical Tightening

- Run resolver against fixtures `01`, `02`, `03` with synthetic declarations injected. Confirm previously-unresolved control references now resolve.
- Route `schema-validation` for resolver output vs. `expected.json` correspondence.
- Land tightening commits.

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Synthetic declaration injection (Session 1) + type-library registry (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=75)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Resolver-extension design + curation.
- **Mechanical tightening (Session 3):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the resolver pipeline, the `.accdb` fixtures from #10, and the type-library registry — verifier needs the cross-product to assess unresolved-vs-degraded handling.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — full type-library introspection is deferred per Phase 4.5.
