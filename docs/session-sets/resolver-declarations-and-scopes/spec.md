# Resolver — Declarations and Scopes Spec

> **Purpose:** Land the declaration model and scope-tree visitor that every later resolver child set builds on — every kind of VBA declaration represented as a typed declaration node, every kind of VBA scope represented as a node in a parent-pointing scope tree.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/resolver-declarations-and-scopes/`
> **Prerequisite:** docs/session-sets/parser-infrastructure/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #7. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `resolver-declarations-and-scopes` |
| **Plan-phase coverage (column 3)** | Phase 3.1-3.2 (declaration model + scope tree) |
| **Prereqs (column 4)** | #6 |
| **Sessions (rough) (column 5)** | 3-4 |
| **Synthesis count (column 6)** | 2 (declaration model, scope tree visitor) |
| **Outputs (column 7)** | `AccessHarvester.Vba.Resolver/Declarations/`, `AccessHarvester.Vba.Resolver/Scopes/` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 3.1–3.2** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. This is the first resolver child set; reference resolution itself lives in #8 (`resolver-references-and-with-blocks`). Splitting the resolver across three child sets (#7 declarations + scopes, #8 references + with-blocks, #9 default members + inference + xref) is a direct application of retrospective adopt-now `#1` (≤2 synthesis sessions per child) — the original Appendix E grouped all four resolver tasks into a single Claude Code session.

Test-case anchors per the parent tree's coverage table: `01`, `02`, `09`, `10`, `11`, `12`.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #7.

---

## Feature 1: Declaration Model

### Scope

Types representing every VBA declaration kind: `ProcedureDeclaration` (Sub / Function / Property Get/Let/Set), `VariableDeclaration` (Dim / Public / Private / Static / Const), `ParameterDeclaration` (with ByRef/ByVal/Optional/ParamArray modifiers), `TypeDeclaration` (user-defined Type), `EnumDeclaration`, `ClassDeclaration`, `EventDeclaration`, `ImplicitDeclaration` (Variant variables created by use). Each declaration carries: name (case-preserved, compared case-insensitively), scope reference, source location, type signature, modifiers, and a references-to-this-declaration collection (populated later in #8).

### Standards

- Names compare case-insensitively but are stored case-preserved for round-tripping back to source.
- Implicit declarations are represented explicitly (never silent) — first use of an undeclared variable creates an `ImplicitDeclaration` node with provenance.

## Feature 2: Scope Tree

### Scope

Scope kinds: `ProjectScope`, `ModuleScope` (one per module — standard, class, form, report), `ProcedureScope`, `WithBlockScope` (structural; flattening lives in #8), `ForEachScope`. Each scope carries: parent reference, ordered child scopes, declarations defined in scope, source range. A scope-construction visitor walks the AST and builds the tree.

### Standards

- The scope tree's shape is the input #8 (reference resolution) consumes; bias toward shape stability.
- Property triplets (Get + Let, Get + Set) are pairing-deferred — at this layer each is its own `ProcedureDeclaration` in the module scope; pairing belongs to #9 default-member resolution.

---

## Session Plan

### Session 1 of 3-4: Declaration Model (Synthesis)

- Read Phase 3.1 of the plan + the 6 anchor fixtures' modules.
- Route an `architecture` synthesis call producing the declaration type hierarchy (1 of 2 synthesis sessions).
- Land types under `AccessHarvester.Vba.Resolver/Declarations/`.

### Session 2 of 3-4: Scope Tree Visitor (Synthesis)

- Read Phase 3.2 of the plan + #6's AST visitor infrastructure.
- Route an `architecture` synthesis call producing the scope-construction visitor (2 of 2 synthesis sessions).
- Land visitor + scope types under `AccessHarvester.Vba.Resolver/Scopes/`.

### Session 3 of 3-4: Cross-Cut + Mechanical Tightening

- Run scope construction over the 6 anchor fixtures; confirm scope nesting matches expected structure.
- Route `schema-validation` for declaration-coverage-vs-fixture-AST correspondence.
- Land tightening commits + tests under `tests/AccessHarvester.Vba.Resolver.Tests/`.

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Declaration model (Session 1) + scope tree (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Resolver algorithm design — Opus xhigh per retrospective `#13`.
- **Mechanical tightening (Session 3):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the AST shape from #6 plus the 6 anchor fixtures and their `expected.json` so the verifier can check declaration/scope round-tripping.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None.
