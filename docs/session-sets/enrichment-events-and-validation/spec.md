# Enrichment — Events, Validation, RowSource Spec

> **Purpose:** Classify every event handler by purpose (validation / cascading / filter / navigation / side-effect / calculation / formatting / mixed), extract validation rules into structured form, and produce structured RowSource specifications for combo/list-box lookups. Sibling-parallel with `enrichment-functions-navigation-review` (#16) under shared parent #14.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/enrichment-events-and-validation/`
> **Prerequisite:** docs/session-sets/enrichment-queries-and-forms/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #15. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `enrichment-events-and-validation` |
| **Plan-phase coverage (column 3)** | Phase 6.3-6.5 (event handler classification, validation rule extraction, RowSource spec) |
| **Prereqs (column 4)** | #14 |
| **Sessions (rough) (column 5)** | 4-5 |
| **Synthesis count (column 6)** | 2 (event-handler taxonomy, validation-rule extraction) |
| **Outputs (column 7)** | `AccessHarvester.Pipeline/Enrichment/EventHandlerClassification.cs`, `ValidationRule.cs`, `RowSourceSpec.cs` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 6.3–6.5** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. Three deliverables share infrastructure (the resolved AST + form metadata + `FormComponentSpec` from #14): event handler classification (#6.3), validation rule extraction (#6.4), RowSource specification (#6.5). Splitting these from #16 (functions + navigation + review) is a direct application of retrospective adopt-now `#1` (≤2 synthesis per child) and lets #15/#16 run in parallel as siblings.

Test-case anchors per the parent tree's coverage table: `01` (validation), `02` (cascading + RowSource), `09` (report events), `12` (switchboard navigation — though navigation belongs to #16), `06` (validation rule patterns).

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #15.

---

## Feature 1: Event Handler Classification (Synthesis)

### Scope

Per Phase 6.3, classify each event handler by purpose:
- **Validation** — modifies `Cancel` flag based on field values
- **Cascading update** — modifies other controls based on this control's value
- **Filter/search** — modifies form's RecordSource or Filter
- **Navigation** — opens other forms/reports
- **Side effect** — writes to database, calls external code
- **Calculation** — computes derived values
- **Formatting/display** — modifies appearance
- **Mixed** — multiple of the above; flagged for human review

Output: `AccessHarvester.Pipeline/Enrichment/EventHandlerClassification.cs` plus the classifier producing it.

### Standards

- Classification rules are deterministic given the same input — never AI-decided per `project-guidance.md` → VBA Parser Strategy.
- Mixed handlers are explicit, not silent. Each component classification appears in the output, plus a `mixed: true` marker. Per Lean By Default, do not pre-decompose mixed handlers; emit the full set of component classifications and let downstream tooling pick.

## Feature 2: Validation Rule Extraction (Synthesis)

### Scope

Per Phase 6.4, extract validation logic as structured rules from:
- Table-level + column-level validation rules (already structured in metadata)
- Form-level validation rules
- BeforeUpdate event handler patterns (`If IsNull(field) Then Cancel = True`)
- Combo box `LimitToList = True` constraints

Output format includes required-field rules, range constraints (min/max), pattern constraints (regex equivalents for input masks), custom predicate rules (with VBA expression preserved for human review).

Output: `AccessHarvester.Pipeline/Enrichment/ValidationRule.cs`.

### Standards

- Custom predicates preserve the original VBA expression verbatim — translation downstream may revise but never overwrite.
- Per the project guidance's "unresolvable constructs are explicit, not silent": validation rules the extractor cannot structurally interpret emit a `{ source: "unstructured", expression: "<verbatim>" }` rather than dropping.

## Feature 3: RowSource Specification

### Scope

Per Phase 6.5, for each combo / list box, produce a structured lookup specification:
- Source type (table / query / value list / field list)
- Source SQL (parsed) if applicable
- Bound column index + bound column field
- Display columns (with widths)
- Filtering predicates (especially form-control-driven cascading lookups)

Output: `AccessHarvester.Pipeline/Enrichment/RowSourceSpec.cs`.

### Standards

- RowSource SQL points at the parsed SQL AST from #12, never re-parses.

---

## Session Plan

### Session 1 of 4-5: Event Handler Taxonomy (Synthesis)

- Read Phase 6.3 + fixtures `01`, `02`, `09`, `12` for handler patterns.
- Route an `architecture` synthesis call producing the classifier + classification taxonomy (1 of 2 synthesis sessions).
- Land under `AccessHarvester.Pipeline/Enrichment/`.

### Session 2 of 4-5: Validation Rule Extraction (Synthesis)

- Read Phase 6.4 + fixtures `01`, `06` + table validation metadata from #10.
- Route an `architecture` synthesis call producing the extractor + rule shape (2 of 2 synthesis sessions).
- Land under `AccessHarvester.Pipeline/Enrichment/`.

### Session 3 of 4-5: RowSource Specification

- Read Phase 6.5 + fixture `02`'s combo box RowSource patterns.
- Land `RowSourceSpec.cs` plus the pipeline visitor — may be in-conversation if the orchestrator is Opus, otherwise routed `analysis`.

### Session 4 of 4-5: End-to-End + Mechanical Tightening

- Run pipeline against `01`, `02`, `06`, `09`, `12`. Confirm classification + validation + RowSource output.
- Route `schema-validation` for output vs. `harvester-schema.v0.json`.
- Land tightening commits.

### Session 5 of 4-5 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for cross-provider verifier findings.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Event taxonomy (Session 1) + validation extraction (Session 2) synthesis:** `route(task_type="architecture", complexity_hint=75)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Novel taxonomy + extractor design.
- **RowSource (Session 3):** `route(task_type="analysis", complexity_hint=55)` if routed; well-trodden enrichment shape.
- **Mechanical tightening (Session 4):** `route(task_type="schema-validation")`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the resolved AST, form metadata, and the 5 anchor fixtures.
- **Change-log:** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — classification taxonomy is fixed by Phase 6.3; validation rule output format follows the plan's enumeration.
