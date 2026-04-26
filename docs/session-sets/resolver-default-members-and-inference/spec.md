# Resolver — Default Members, Type Inference, Cross-Reference Index Spec

> **Purpose:** Land the default-member registry that resolves implicit `.Value` (and analogues) on Access object-model and VBA standard types, the lightweight type-inference pass that annotates `Variant` declarations with confidence-scored inferred types, and the `GetReferencesTo()` cross-reference index that downstream enrichment consumes.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/resolver-default-members-and-inference/`
> **Prerequisite:** docs/session-sets/resolver-references-and-with-blocks/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #9. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `resolver-default-members-and-inference` |
| **Plan-phase coverage (column 3)** | Phase 3.5-3.7 (default member resolution + type inference + cross-reference index) |
| **Prereqs (column 4)** | #8 |
| **Sessions (rough) (column 5)** | 4-5 |
| **Synthesis count (column 6)** | 2 (default-member registry design, type inference) |
| **Outputs (column 7)** | `AccessHarvester.Vba.Resolver/DefaultMembers/`, `AccessHarvester.Vba.Resolver/Types/` (inference pass), `GetReferencesTo()` API |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 3.5–3.7** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. Three features that share infrastructure (the resolver's reference→declaration map from #8) but are distinct deliverables: default-member resolution, type inference, cross-reference index. Splitting these from references + with-blocks (#8) is a direct application of retrospective adopt-now `#1` (≤2 synthesis per child).

Test-case anchors per the parent tree's coverage table: `02`, `03`, `09`, `11`.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #9.

---

## Feature 1: Default-Member Registry

### Scope

Curated registry of types and their default members. Initial coverage (per Phase 3.5):
- Access controls (TextBox, ComboBox, ListBox, Label, Subform, Subreport): default to `.Value` (or `.Form`/`.Report` for sub-controls).
- DAO types (Recordset's `Fields(name)` default-member returns the field's value).
- VBA collections.

When the resolver sees a member-less reference on an expression of a typed value, it inserts the implicit default member into the resolved AST as an annotation (preserving the original syntax).

### Standards

- The registry is data, not code branching. Adding a new type means adding a row, not editing the resolver.
- Property triplet pairing (Get + Let, Get + Set) lands here too — the registry maps a triplet name to its three procedure declarations so downstream consumers can pick the right reader/writer.

## Feature 2: Type Inference

### Scope

Lightweight inference on `Variant` declarations:
- Look at assignment patterns. String-literal/string-typed → infer string. Numeric-only → infer numeric. `Set rs = ...` where `rs` is consistently a `Recordset` → infer Recordset.
- Confidence scoring: high (consistent type), medium (mostly one type with some Variant uses), low (true Variant).
- Inferred types annotate the AST but never replace the original declaration.

### Standards

- Per Phase 3.6: "Don't overinvest here; AI-assisted translation downstream can use these hints without depending on them being exact." Inference is helpful but not load-bearing.
- The default-member registry from Feature 1 feeds inference: `Set rs = CurrentDb.OpenRecordset(...)` → `rs` inferred as Recordset because `OpenRecordset` is registered as returning Recordset.

## Feature 3: Cross-Reference Index

### Scope

Once resolution is complete, invert the reference→declaration map: `GetReferencesTo(declaration) -> IEnumerable<Reference>`. Used extensively by downstream enrichment (`enrichment-functions-navigation-review`, #16) to compute call-graph closures and impact analysis.

### Standards

- Index construction is a single pass; downstream callers query via the API, never reconstruct.

---

## Session Plan

### Session 1 of 4-5: Default-Member Registry (Synthesis)

- Read Phase 3.5 + the Access object model surfaces in plan Appendix A + fixtures `02`, `03`, `11`.
- Route an `architecture` synthesis call producing the registry shape and seed coverage (1 of 2 synthesis sessions).
- Land registry under `AccessHarvester.Vba.Resolver/DefaultMembers/`.

### Session 2 of 4-5: Type Inference (Synthesis)

- Read Phase 3.6 + the inference patterns in fixtures `09`, `11`.
- Route an `architecture` synthesis call producing the inference algorithm and confidence model (2 of 2 synthesis sessions).
- Land inference pass under `AccessHarvester.Vba.Resolver/Types/`.

### Session 3 of 4-5: Cross-Reference Index

- Land `GetReferencesTo()` API + index construction.
- Build out under `AccessHarvester.Vba.Resolver/CrossReference/`.

### Session 4 of 4-5: Cross-Cut + Mechanical Tightening

- Run resolver over `02`, `03`, `09`, `11`. Confirm default-member annotations + inferred types appear in the resolved AST.
- Route `schema-validation` for resolver-output-vs-`expected.json` correspondence.
- Land tightening commits.

### Session 5 of 4-5 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Default-member registry (Session 1) + type inference (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Resolver design.
- **Cross-reference index (Session 3):** May be in-conversation if the orchestrator is Opus, otherwise routed `analysis` or `architecture`. If routed `architecture`, the task type is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`.
- **Mechanical tightening (Session 4):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include resolver code + fixtures `02`, `03`, `09`, `11`.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — type-inference depth is fixed by the plan ("Don't overinvest here").
