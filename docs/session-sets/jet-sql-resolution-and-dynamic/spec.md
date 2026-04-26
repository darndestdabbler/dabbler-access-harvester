# JET SQL — Form-Reference Resolution and Dynamic SQL Spec

> **Purpose:** Detect form-control references inside SQL ASTs (`[Forms]![X]![Y]` and friends) and link them to resolved control declarations from the Access symbol table; perform intra-procedural data-flow analysis on VBA-constructed SQL strings to enumerate candidate query templates.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/jet-sql-resolution-and-dynamic/`
> **Prerequisite:** docs/session-sets/access-symbol-table-and-types/, docs/session-sets/jet-sql-grammar-and-parser/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #13. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `jet-sql-resolution-and-dynamic` |
| **Plan-phase coverage (column 3)** | Phase 5.3-5.4 (form-reference detection in SQL + dynamic SQL analysis) |
| **Prereqs (column 4)** | #11, #12 |
| **Sessions (rough) (column 5)** | 3-4 |
| **Synthesis count (column 6)** | 2 (form-reference detection design, dynamic SQL data-flow analyzer) |
| **Outputs (column 7)** | `AccessHarvester.Sql.Parser/FormReferences/`, `AccessHarvester.Sql.Parser/DynamicSql/` data-flow analyzer |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 5.3–5.4** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. Bridges the VBA resolver (from #11) and the SQL parser (from #12). Two distinct deliverables share infrastructure: form-reference detection (#5.3, structural — find `[Forms]![X]![Y]` patterns and annotate AST nodes with resolved control declarations) and dynamic SQL analysis (#5.4, data-flow — walk backward through VBA procedures tracking string-typed variables, resolving constants and form-control references in concatenations, marking unknowns as wildcards).

Test-case anchors per the parent tree's coverage table: `04` (form-reference patterns), `05` (canonical dynamic SQL — 32 possible forms enumerated as 5 templates).

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #13.

---

## Feature 1: Form-Reference Detection in SQL (Synthesis)

### Scope

Identify `[Forms]![X]![Y]` and similar (`[Reports]!`, `[Forms]![X].Form.<Property>`) patterns inside SQL ASTs from #12, and link them to resolved control declarations via the Access metadata symbol table from #11.

Output: `AccessHarvester.Sql.Parser/FormReferences/` — a visitor over SQL ASTs that emits annotated AST nodes carrying the resolved declaration (or marking unresolved with provenance per the project guidance).

### Standards

- Detection is structural; resolution is via the same symbol table the VBA resolver uses, not a separate registry.
- Unresolved form references emit `{ source: "unresolved", reason: "form-not-in-metadata", expression: "<original>" }` rather than silent fall-through.

## Feature 2: Dynamic SQL Data-Flow Analyzer (Synthesis)

### Scope

For each `strSql = ...` (or analogue) assignment in VBA procedures from `enrichment-events-and-validation`'s upstream input, walk backward through the procedure tracking string-typed variables. For each concatenation, resolve constants and form-control references; mark unknowns as wildcards. Output: a set of candidate query templates with parameters identified, or an "unanalyzable" marker for cases too dynamic to resolve.

Output: `AccessHarvester.Sql.Parser/DynamicSql/` — the data-flow analyzer + the candidate-template AST.

### Standards

- Per Phase 5.4: "Failures route to manual review queue rather than producing wrong results." The analyzer's output is candidate templates plus an `unanalyzable` marker; downstream `enrichment-functions-navigation-review` (#16) consumes both.
- The 5-template canonical example from fixture `05` is the contract: the analyzer must produce those 5 templates from `05`'s 32 possible runtime forms.

---

## Session Plan

### Session 1 of 3-4: Form-Reference Detection (Synthesis)

- Read Phase 5.3 + #11's symbol table + #12's SQL AST + fixture `04`.
- Route an `architecture` synthesis call producing the form-reference visitor (1 of 2 synthesis sessions).
- Land detection under `AccessHarvester.Sql.Parser/FormReferences/`.

### Session 2 of 3-4: Dynamic SQL Analyzer (Synthesis)

- Read Phase 5.4 + fixture `05`'s `expected.json` (5 templates from 32 runtime forms).
- Route an `architecture` synthesis call producing the data-flow algorithm (2 of 2 synthesis sessions).
- Land analyzer under `AccessHarvester.Sql.Parser/DynamicSql/`.

### Session 3 of 3-4: Cross-Cut + Mechanical Tightening

- Run end-to-end pipeline on `04` and `05`. Confirm form-reference annotations on `04`; confirm 5 candidate templates on `05`.
- Route `schema-validation` for output vs. `expected.json`.
- Land tightening commits.

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Form-reference detection (Session 1) + dynamic SQL analyzer (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Resolver/analyzer design.
- **Mechanical tightening (Session 3):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the symbol table, the SQL AST, and fixtures `04`/`05`.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — the "candidate templates + unanalyzable marker" output shape is fixed by Phase 5.4 of the plan.
