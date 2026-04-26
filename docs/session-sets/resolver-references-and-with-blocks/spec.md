# Resolver — References and With-Blocks Spec

> **Purpose:** Land the reference-resolution algorithm — for each identifier reference in the AST, walk the scope tree to find the matching declaration — plus the With-block flattening pass that resolves `.Member` and `!Member` inside `With expr ... End With`.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/resolver-references-and-with-blocks/`
> **Prerequisite:** docs/session-sets/resolver-declarations-and-scopes/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #8. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `resolver-references-and-with-blocks` |
| **Plan-phase coverage (column 3)** | Phase 3.3-3.4 (reference resolution + with-block flattening) |
| **Prereqs (column 4)** | #7 |
| **Sessions (rough) (column 5)** | 4-5 |
| **Synthesis count (column 6)** | 2 (resolution algorithm, with-block flattening) |
| **Outputs (column 7)** | `AccessHarvester.Vba.Resolver/References/`, `AccessHarvester.Vba.Resolver/Scopes/WithBlockScope.cs`, resolved-AST annotations |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 3.3–3.4** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. The output of this set is a `ResolvedModule` containing the AST plus annotations linking each reference to its declaration (or marking it unresolved). With-block flattening is the second feature because its resolution depends on the same scope-walking infrastructure references use.

Test-case anchors per the parent tree's coverage table: `02`, `03`, `09`.

Per `project-guidance.md` → VBA Parser Strategy: "When the parser cannot resolve a name, an SQL parameter, a dynamic SQL fragment, or a late-bound call, it emits a node with provenance metadata (e.g., `{ source: 'unresolved', reason: '<string>' }`) rather than swallowing the construct." Test case `08-unresolvable.bas` is the canonical example.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #8.

---

## Feature 1: Reference Resolution

### Scope

Reference-resolution algorithm per Phase 3.3 of the plan; algorithm specifics are Session 1 elaboration. Shape commitment: the resolver consumes #7's scope tree and emits a `ResolvedModule` (AST + reference→declaration annotations) with explicit unresolved provenance for unresolvable cases. Access object-model and built-in-function fallbacks are wired to registries — synthetic Access declarations come from #11; built-ins are a static list landed in this set. Special cases the plan calls out (`Me`, bang operator, subform/subreport navigation) are Session 1 elaboration.

### Standards

- Unresolved references are explicit AST annotations, never silent. Every unresolved node carries `{ source: "unresolved", reason: "<string>" }` per the project guidance.
- Built-in function recognition uses a curated list (Phase 3.3 mentions Format, IIf, Nz). The list lives in source as a static registry, not loaded at runtime.

## Feature 2: With-Block Flattening

### Scope

Algorithm (per Phase 3.4):
- During scope construction (#7), `With` blocks introduce a `WithBlockScope` carrying a "this expression" reference.
- During resolution (this set), when a reference begins with `.` or `!` inside a `With` scope, prepend the `With` expression and resolve the synthesized full reference.
- Nested `With` blocks: innermost wins for `.` references.

Output: `WithBlockScope.cs` under `AccessHarvester.Vba.Resolver/Scopes/`, plus resolved-AST annotations recording the synthesized reference for each flattened `.`/`!` access.

### Standards

- Flattening is non-destructive: the original `.`/`!` access node is preserved with an annotation pointing at the synthesized full reference. The downstream consumer can render either form.
- Test fixtures must include nested With blocks and With blocks containing form references (already present in fixtures `02`, `03`).

---

## Session Plan

### Session 1 of 4-5: Resolution Algorithm (Synthesis)

- Read Phase 3.3 + #7's declaration/scope output + fixtures `02`, `03`, `09`.
- Route an `architecture` synthesis call producing the resolution algorithm (1 of 2 synthesis sessions).
- Land resolver under `AccessHarvester.Vba.Resolver/References/`.

### Session 2 of 4-5: With-Block Flattening (Synthesis)

- Read Phase 3.4 + With-block patterns from `02`, `03`.
- Route an `architecture` synthesis call producing flattening pass (2 of 2 synthesis sessions).
- Land `AccessHarvester.Vba.Resolver/Scopes/WithBlockScope.cs` + annotation logic.

### Session 3 of 4-5: ResolvedModule Annotations + Provenance

- Define the `ResolvedModule` shape: AST + reference→declaration map + unresolved-list + diagnostics.
- Land annotation infrastructure; integrate flattening output into resolved AST.

### Session 4 of 4-5: Cross-Cut + Mechanical Tightening

- Run resolver over fixtures `02`, `03`, `09` plus `08-unresolvable.bas`. Confirm unresolved provenance is correct on `08`.
- Route `schema-validation` for resolved-AST-vs-fixture-`expected.json` correspondence.
- Land tightening commits.

### Session 5 of 4-5 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Resolution algorithm (Session 1) + With-block flattening (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Resolver algorithm design — Opus xhigh per retrospective `#13`.
- **ResolvedModule shape (Session 3):** May be in-conversation if the orchestrator is Opus (per `project-guidance.md` Convention "When the orchestrator's model matches the routing target..."), or routed `architecture` otherwise. If routed, `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`.
- **Mechanical tightening (Session 4):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the resolver code + fixtures `02`, `03`, `09`, `08`.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — Access object model integration timing is fixed by the tree (synthetic declarations land in #11, the algorithm here just calls into a registry that does not yet exist; #11 fills it).
