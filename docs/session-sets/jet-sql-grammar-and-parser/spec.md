# JET SQL Grammar and Parser Spec

> **Purpose:** Author the ANTLR4 grammar for the JET/ACE SQL dialect and the parser infrastructure (error recovery, AST, position tracking) that downstream SQL analysis depends on. Runs in parallel with #7–#11 since it depends only on `parser-infrastructure` (#6).
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/jet-sql-grammar-and-parser/`
> **Prerequisite:** docs/session-sets/parser-infrastructure/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #12. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `jet-sql-grammar-and-parser` |
| **Plan-phase coverage (column 3)** | Phase 5.1-5.2 (JET SQL grammar + parser infrastructure) |
| **Prereqs (column 4)** | #6 |
| **Sessions (rough) (column 5)** | 4-5 |
| **Synthesis count (column 6)** | 2 (JET SQL grammar, error recovery + AST for SQL) |
| **Outputs (column 7)** | `AccessHarvester.Sql.Grammar/AccessHarvester.Sql.Grammar.csproj`, `AccessHarvester.Sql.Grammar/JetSql.g4`, `AccessHarvester.Sql.Parser/` (AST + error recovery), tests under `tests/AccessHarvester.Sql.Grammar.Tests/` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 5.1–5.2** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. JET/ACE SQL is a distinct dialect with its own quirks (SQL-89-ish JOIN syntax, parameter brackets `[Forms]![X]![Y]` and named `[Enter ID]`, IIf/Switch/Choose in SELECT lists, domain functions DLookup/DSum/etc., TRANSFORM ... PIVOT crosstab, identifier escaping with brackets `[Field With Spaces]`, date literals `#1/1/2024#`, `&` for string concatenation).

Test-case anchors per the parent tree's coverage table: `04` (PARAMETERS, joins, IIf, date math), `05` (dynamic SQL fragments embedded in VBA strings).

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #12.

---

## Feature 1: JET SQL Grammar (Synthesis)

### Scope

ANTLR4 grammar `JetSql.g4` covering: SELECT (with WHERE, GROUP BY, HAVING, ORDER BY), JOIN variants (INNER, LEFT, RIGHT — SQL-89-ish), subqueries, parameter brackets (`[Forms]![frmX]![ctlY]` and named parameters like `[Enter ID]`), IIf/Switch/Choose in SELECT lists, domain functions (DLookup/DSum/DCount/DMax/DMin/DAvg/DFirst/DLast), TRANSFORM ... PIVOT, identifier escaping with brackets, date literals, `&` for string concatenation. Limited UPDATE/INSERT/DELETE coverage (smaller than VBA grammar).

Output: `AccessHarvester.Sql.Grammar/AccessHarvester.Sql.Grammar.csproj` + `AccessHarvester.Sql.Grammar/JetSql.g4`. Build wiring follows the same ANTLR4.Build.Tasks pattern `parser-foundations` (#1) established for VBA.

### Standards

- The grammar is a separate `.g4` from `Vba.g4`, but the lexical layer can reuse VBA tokens where they overlap (e.g., date literals).
- Domain functions are recognized as a closed list from plan Appendix A; new ones land via registry update.

## Feature 2: SQL Parser Infrastructure (Synthesis)

### Scope

Same shape as `parser-infrastructure` (#6) for VBA but smaller in scope: error recovery (synchronization at statement boundaries), AST layer (immutable nodes for SELECT/UPDATE/INSERT/DELETE/TRANSFORM), position tracking (every node carries `SourceLocation`).

Output: `AccessHarvester.Sql.Parser/` with AST + error recovery.

### Standards

- AST shape mirrors VBA AST conventions (immutable, visitor-walked, source-located).
- Error recovery is robust on real-database SQL strings (which include malformed concatenation residue).

---

## Session Plan

### Session 1 of 4-5: JET SQL Grammar (Synthesis)

- Read Phase 5.1 + plan Appendix A SQL surfaces + fixtures `04`, `05`.
- Route an `architecture` synthesis call producing `JetSql.g4` (1 of 2 synthesis sessions).
- Land grammar + ANTLR4 build wiring under `AccessHarvester.Sql.Grammar/`.

### Session 2 of 4-5: SQL Parser Infrastructure (Synthesis)

- Read Phase 5.2 + #6's VBA parser infrastructure for shape reuse.
- Route an `architecture` synthesis call producing AST + error recovery (2 of 2 synthesis sessions).
- Land parser under `AccessHarvester.Sql.Parser/`.

### Session 3 of 4-5: Test Coverage + Mechanical Tightening

- Run the parser harness against `04` and the embedded SQL fragments in `05`.
- Route `schema-validation` for parser output vs. `expected.json` correspondence.
- Land tests under `tests/AccessHarvester.Sql.Grammar.Tests/`.

### Session 4 of 4-5: Cross-Cut + Edge-Case Fixtures

- Author additional small SQL-only fixtures if the corpus reveals dialect surfaces fixtures `04`/`05` do not exercise (e.g., TRANSFORM ... PIVOT) via `route(task_type="test-generation")`.
- Land tightening commits.

### Session 5 of 4-5 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **JET SQL grammar (Session 1) + SQL parser infrastructure (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Grammar authoring at xhigh effort.
- **Mechanical tightening (Session 3):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Edge fixtures (Session 4):** `route(task_type="test-generation", complexity_hint=40)` if needed.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the grammar, AST, and fixtures `04`/`05`.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — JET SQL scope is fixed by Phase 5.1 of the plan and fixture coverage.
