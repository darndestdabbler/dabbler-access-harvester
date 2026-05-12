# Access Resolver Symbol-Table Integration Spec

> **Purpose:** Inject Access metadata (forms, reports, controls) into the VBA resolver as synthetic declarations so VBA references to forms, reports, and form controls resolve correctly through the same scope/lookup machinery the resolver applies to source-defined names.
> **Created:** 2026-04-26
> **Session Set:** `docs/session-sets/access-resolver-symbol-table-integration/`
> **Prerequisite:** docs/session-sets/resolver-default-members-and-inference/, docs/session-sets/access-metadata-extraction/, docs/session-sets/access-type-library-registry/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **DEFERRED.** Do not start until prereqs #9 and #11a are complete (per their respective `change-log.md` files). Until then, this folder contains only the spec stub.

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #11b. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug** | `access-resolver-symbol-table-integration` |
| **Plan-phase coverage** | Phase 4.4 (symbol-table integration: synthetic declarations from Access metadata) |
| **Prereqs** | #9, #10, #11a |
| **Sessions (rough)** | 2-3 |
| **Synthesis count** | 1 (synthetic-declaration injection design) |
| **Outputs** | `AccessHarvester.Vba.Resolver/AccessIntegration/` — injection visitor, synthetic-declaration types, resolver pipeline hooks. Consumes `ITypeLibraryRegistry` from #11a for member-resolution of Access-typed declarations. |

This set is the **downstream half** of the original combined `access-symbol-table-and-types` plan. Its companion upstream set is `access-type-library-registry` (tree row #11a), which is unblocked and authors the registry contract this set integrates with.

---

## Project Overview

Implements **Phase 4.4** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. This is the bridge between the VBA resolver (#7-#9) and the Access metadata extractor (#10): the resolver alone cannot resolve form / report / control references because those declarations don't exist in VBA source — they exist in `.accdb` design metadata. This set populates the resolver's symbol table with synthetic declarations derived from extracted metadata, and uses #11a's `ITypeLibraryRegistry` for member-level resolution against DAO/ADO/Office types referenced from Access-typed declarations.

Test-case anchors per the parent tree's coverage table: `01`, `02`, `03` (require form metadata to resolve fully). Inherits `.accdb` fixtures from #10.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #11b.

---

## Feature 1: Synthetic Declaration Injection (Synthesis)

### Scope

Per Phase 4.4:

- Each form module's class scope is populated with synthetic declarations for every control on the form (one declaration per control, with type derived from the control's class via the class-name → control-type mapping the metadata extractor already produces in #10).
- The project scope is populated with synthetic declarations for every form and report (via `Forms` and `Reports` collections).
- The class hierarchy reflects subform navigation: `subformControl.Form` resolves to the contained form's class.
- Member lookups against synthetic-declaration types (e.g., `txtFirstName.Value`, `Me.RecordSource`) flow through `ITypeLibraryRegistry` (from #11a) for the Office / Access / DAO surface; resolver-native lookups continue to handle source-defined types.

Output: `AccessHarvester.Vba.Resolver/AccessIntegration/` containing the injection visitor, the synthetic-declaration types, and the resolver hooks #8 and #9 already call into.

### Standards

- **Synthetic declarations carry provenance** — `{ source: "synthetic-from-access", artifact: "<form-name>" }` — so downstream consumers can distinguish them from VBA-source declarations.
- **Subform navigation is data, not code branching.** The metadata extractor records the subform-to-form binding; the resolver dereferences it.
- **Type-library membership flows through the contract.** The injection layer holds an `ITypeLibraryRegistry` reference and calls into it; it does not import `TypeLibraryRegistry.cs` directly. This keeps the resolver project's dependency on the registry one-directional through the interface.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
```

## Sessions
### Session 1 of 2-3: Synthetic Declaration Injection (Synthesis)

- Read Phase 4.4 + #9's resolver output + #10's metadata schema + #11a's `ITypeLibraryRegistry` contract + fixtures `01`, `02`, `03` from #10.
- Route an `architecture` synthesis call producing the injection visitor + integration with the resolver pipeline (or handle in-conversation if orchestrator is Opus).
- Land injection under `AccessHarvester.Vba.Resolver/AccessIntegration/`.

### Session 2 of 2-3: Cross-Cut + Mechanical Tightening

- Run resolver against fixtures `01`, `02`, `03` with synthetic declarations injected. Confirm previously-unresolved control references now resolve.
- Route `schema-validation` for resolver output vs. `expected.json` correspondence.
- Land tightening commits.

### Session 3 of 2-3 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Synthetic declaration injection (Session 1) synthesis:** `route(task_type="architecture", complexity_hint=75)`. If the orchestrator is Opus, handle in-conversation per project-guidance Convention.
- **Mechanical tightening (Session 2):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per the mechanical-gate Convention.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the resolver pipeline source, the `.accdb` fixtures from #10, the `ITypeLibraryRegistry` contract from #11a, and the type-library registry implementation file — verifier needs the cross-product to assess unresolved-vs-degraded handling.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None at split time. The full type-library introspection question is deferred per Phase 4.5 (handled in #11a).
