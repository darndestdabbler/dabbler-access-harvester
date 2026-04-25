# VBA Statement and Module Grammar Spec

> **Purpose:** Author the statement and module sections of the ANTLR4 `Vba.g4` grammar — control flow, error handling, declarations, special statements, procedure/property triplets, classes, `Implements`, `WithEvents`, events — extending the lexical and expression grammar from `vba-lexical-and-expression-grammar` (#3) to cover the remaining 1.4 ground.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/vba-statement-and-module-grammar/`
> **Prerequisite:** docs/session-sets/vba-lexical-and-expression-grammar/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #4. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `vba-statement-and-module-grammar` |
| **Plan-phase coverage (column 3)** | Phase 1.4 statement + module rules |
| **Prereqs (column 4)** | #3 |
| **Sessions (rough) (column 5)** | 4 |
| **Synthesis count (column 6)** | 2 (statement grammar, module grammar) |
| **Outputs (column 7)** | `AccessHarvester.Vba.Grammar/Vba.g4` (statement + module sections, edits in place), unit tests under `tests/AccessHarvester.Vba.Grammar.Tests/` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements the statement and module portions of **Phase 1.4** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. Edits `AccessHarvester.Vba.Grammar/Vba.g4` in place (statement + module sections) plus extends unit tests under `tests/AccessHarvester.Vba.Grammar.Tests/`. Output paths are the same as #3 because the grammar lives in one file; statement and module rules are a follow-on edit, not a separate file.

Test-case anchors (per the parent tree's coverage table): `01`, `05`, `07` (GoSub/Return), `09`, `11`, `12` for statements; `10` (class with Implements, WithEvents, Property triplet, Initialize/Terminate) for module structure.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #4.

---

## Feature 1: Statement Grammar

### Scope

Control flow (`If`/`Else`, `Select Case`, `For`/`Next`, `For Each`/`Next`, `Do`/`Loop`, `While`/`Wend`), error handling (`On Error Goto`/`Resume Next`/`Resume`/`Resume <label>`), declarations (`Dim`, `Public`, `Private`, `Static`, `Const`, `ReDim`), assignment (`Let`/`Set` distinction), special statements (`GoTo`, `GoSub`/`Return` for legacy), `With`/`End With` blocks (parsed structurally; flattening lives in #8), `RaiseEvent`, `Stop`, `End`.

Test-case anchors: `01` (validation patterns), `05` (dynamic SQL VBA-side), `07` (GoSub/Return), `09` (report events), `11`, `12`.

### Standards

- `With`/`End With` is parsed as a structural block; semantic flattening is `resolver-references-and-with-blocks` (#8).
- `Set` vs implicit Let assignment distinction is preserved in the AST (`Set` is its own statement node, not a flag on assignment).

## Feature 2: Module Grammar

### Scope

Module-level structure: option directives (`Option Explicit`, `Option Compare`, `Option Base`), procedure declarations (`Sub`/`Function`/`Property Get`/`Let`/`Set`), parameter lists (`ByRef`/`ByVal`/`Optional`/`ParamArray`/named args), user-defined types (`Type ... End Type`), enums (`Enum ... End Enum`), class-level constructs (`Implements`, `WithEvents`, `Public Event`, `Initialize`/`Terminate` lifecycle events), declared API imports (`Declare`/`Declare PtrSafe`).

Test-case anchor: `10-clsAuditLogger.cls`.

### Standards

- Property triplets are parsed as three independent procedure declarations sharing a name; pairing logic (Get + Let, Get + Set, etc.) is resolver work, not grammar work.
- `Declare`/`Declare PtrSafe` is parsed but not validated against an OS — that's a downstream concern.

---

## Session Plan

### Session 1 of 4: Statement Grammar

- Read coverage matrix statement rows + hazard list statement entries + `01`, `05`, `07`, `09`, `11`, `12` fixtures.
- Route an `architecture` synthesis call producing statement rules (1 of 2 synthesis sessions).
- Edit `Vba.g4` in place; add tests under `tests/AccessHarvester.Vba.Grammar.Tests/Statements/`.

### Session 2 of 4: Module Grammar

- Read coverage matrix module-structure rows + hazard list module entries + `10-clsAuditLogger.cls` fixture.
- Route an `architecture` synthesis call producing module rules (2 of 2 synthesis sessions).
- Edit `Vba.g4` in place; add tests under `tests/AccessHarvester.Vba.Grammar.Tests/Modules/`.

### Session 3 of 4: Cross-Cut + Tightening

- Run `parser-foundations` harness against all 12 fixtures; classify failures; fix grammar bugs in priority order.
- Route `schema-validation` for grammar-vs-matrix correspondence on statement + module rows.
- Land tightening commits.

### Session 4 of 4: Verifier-Surfaced Cleanup

- Resolve any cross-provider verifier findings deferred from prior sessions.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Statement (Session 1) and module (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Grammar authoring at xhigh effort.
- **Mechanical tightening (Session 3):** `route(task_type="schema-validation")` for grammar-vs-matrix correspondence. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the relevant test-case fixtures plus the coverage matrix per the verifier-context discipline.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None.
