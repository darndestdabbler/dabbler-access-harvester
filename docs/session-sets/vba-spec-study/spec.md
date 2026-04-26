# VBA Spec Study Spec

> **Purpose:** Read [MS-VBAL] (Microsoft's VBA Language Specification) end-to-end and produce the three artifacts every later grammar/resolver child set treats as input: a topic-organized prose summary, a coverage matrix mapping every grammar production to its in-scope/in-corpus/priority disposition, and a hazard list of edge cases discovered against the existing 12 test fixtures.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/vba-spec-study/`
> **Prerequisite:** None
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #2. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `vba-spec-study` |
| **Plan-phase coverage (column 3)** | Phase 1.1-1.3 (MS-VBAL summary, coverage matrix, hazard list) |
| **Prereqs (column 4)** | none -- runs in parallel with #1 |
| **Sessions (rough) (column 5)** | 3-4 |
| **Synthesis count (column 6)** | 2 (MS-VBAL summary, hazard list synthesis) |
| **Outputs (column 7)** | `docs/spec-study/ms-vbal-summary.md`, `docs/spec-coverage/ms-vbal-coverage.csv`, `docs/spec-coverage/hazard-list.md` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 1.1–1.3** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. The MS-VBAL spec is dense and the parser is clean-room from it ("no derivation from Rubberduck or other GPL-licensed sources" per the plan preamble), so a structured study artifact is the foundation every grammar session reads. The coverage matrix is the checklist grammar sessions check off; the hazard list is the running edge-case ledger that drives the `vba-grammar-validation` (#5) edge-case fixtures.

This set produces documents only. No grammar code is authored here.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #2.

---

## Feature 1: MS-VBAL Summary

### Scope

Produce `docs/spec-study/ms-vbal-summary.md` — prose summary organized by spec topic (lexical structure, modules, procedures, statements, expressions, type system, object model, preprocessor) with explicit MS-VBAL section citations. Each entry notes syntactic complexity, semantic subtlety, and likely frequency in Access codebases. The summary covers the 14 critical constructs the plan calls out (bang operator, default property access, `Me`, `Set` vs Let, line continuation, line numbers, type-decl chars, date literals, ByRef, `Optional`/`ParamArray`/named args, Property Get/Let/Set triplets, events + `WithEvents`, `Implements`, `On Error` + `Resume`, `GoTo`/`GoSub`).

### Standards

- Cite MS-VBAL section numbers directly. Statements like "MS-VBAL covers expressions" without a section number are insufficient.
- Note where the plan's Appendix A (Access-specific constructs) extends MS-VBAL.

## Feature 2: Coverage Matrix

### Scope

Produce `docs/spec-coverage/ms-vbal-coverage.csv` (or markdown table). Columns: Section / Construct / In Corpus? / In Scope? / Priority (must/should/can-defer/wont) / Difficulty / Notes. Drives grammar session checklists.

### Standards

- "In Corpus?" cross-references the 12 fixtures under `docs/test-cases/` plus Northwind committed by `accessexportertemp1-import`.
- Priority assignments respect `project-guidance.md` → Lean By Default — won't-handle is acceptable when the construct does not appear in migration-scope databases.

## Feature 3: Hazard List

### Scope

Produce `docs/spec-coverage/hazard-list.md` — a living document of edge cases the 12 fixtures already exhibit plus suspected hazards from the spec. Each hazard tagged with file location (if from corpus), construct description, MS-VBAL section, parsing difficulty estimate. Drives `vba-grammar-validation` (#5) edge-case fixture authoring.

### Standards

- Hazards must point at concrete syntax, not abstract concerns. "Operator precedence is hard" is not a hazard; "the bang operator binding tighter than `.` in `Forms!frm!ctl.Property` is" is.
- Hazards discovered later (during grammar work) get appended; this child set seeds the document, it does not finalize it.

---

## Session Plan

### Session 1 of 3-4: MS-VBAL Summary

- Read MS-VBAL spec (or a prepared local copy/excerpt) topic by topic.
- Route an `architecture` synthesis call producing the summary in topic-organized form (1 of 2 synthesis sessions).
- Land `docs/spec-study/ms-vbal-summary.md`.

### Session 2 of 3-4: Coverage Matrix

- Read the summary from Session 1 plus the 12 test-case fixtures.
- Build the coverage matrix mechanically: each MS-VBAL grammar production → row, populate corpus/scope/priority/difficulty.
- Route `schema-validation` (mechanical-correspondence) to verify every must/should construct in the summary appears in the matrix.
- Land `docs/spec-coverage/ms-vbal-coverage.csv`.

### Session 3 of 3-4: Hazard List Synthesis

- Read the 12 fixtures with hazard lens.
- Route an `architecture` synthesis call producing the hazard list (2 of 2 synthesis sessions).
- Land `docs/spec-coverage/hazard-list.md`.

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for resolving any cross-provider verifier findings (e.g., missing must-handle constructs in the matrix, missing MS-VBAL sections in the summary).

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Summary synthesis (Session 1) and hazard list (Session 3):** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Synthesis-heavy with MS-VBAL spec context.
- **Coverage matrix (Session 2):** `route(task_type="schema-validation", complexity_hint=40)`. Mechanical-correspondence between summary and matrix; Sonnet-pinned per retrospective adopt-now `#10`. (Task type to be added by the routing-playbook child set #4 of `vba-parser-session-set-generator`.)
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)` per `task_type_overrides`.
- **Change-log (last session):** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — clean-room sourcing is fixed by the plan preamble. If the human wants `ms-vbal-summary.md` published as Markdown vs. structured JSON, that's a Session-1 elaboration decision, not a stub-level fork.
