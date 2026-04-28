# VBA Symbol Resolution and Enrichment

> **Purpose:** Build symbol resolution infrastructure to annotate VBA code with semantic metadata (resolved references, dependency graphs, inferred functionality), bridging syntactic IR to semantic IR for downstream AI-comprehension work.
> **Created:** 2026-04-28
> **Session Set:** `docs/session-sets/vba-symbol-resolution-and-enrichment/`
> **Prerequisite:** `integration-testing-and-acceptance` completed; gold-standard DB fixtures available
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

| Field | Value |
|---|---|
| **Slug (column 2)** | `vba-symbol-resolution-and-enrichment` |
| **Plan-phase coverage (column 3)** | Symbol resolution PoC (Session 1–2), pipeline integration (Session 3+, if PoC succeeds) |
| **Prereqs (column 4)** | `integration-testing-and-acceptance` committed; gold-standard DBs with extracted VBA available |
| **Sessions (rough) (column 5)** | 2–4 (PoC + integration + validation) |
| **Synthesis count (column 6)** | 1 (symbol resolution algorithm design) |
| **Outputs (column 7)** | `tools/symbol-resolver/`, enriched IR schema extension, cross-module dependency graph spec |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference.

---

## Project Overview

Symbol resolution bridges the gap between syntactic IR (procedure signatures, events, form structure) and semantic IR (what code does, how identifiers resolve, dependency chains). Current IR captures event mappings and procedure signatures but not what identifiers resolve to, what procedures actually do, or cross-module dependencies.

**Impact on downstream AI-comprehension work:** Without symbol resolution, questions can only address structure; with it, questions can address behavior and intent.

The PoC (Sessions 1–2) will establish the symbol resolution algorithm and IR schema extension on two target modules (`frmCompanyDetail.cls` and `modStrings.bas`). Phase 2 (Session 3+, if PoC succeeds) will integrate into the harvester pipeline and scale to all gold-standard modules.

## Current State

IR captures:
- Event → Procedure mapping (e.g., `BeforeUpdate` → `cboCompanyTypeID_BeforeUpdate`)
- Procedure signatures (parameters, access modifiers)
- Form/control structure

IR *does not* capture:
- What identifiers resolve to (e.g., `CompanyIsActive` → Function in same module)
- What procedures actually do (business logic, validation rules, side effects)
- Cross-module dependencies (e.g., `GetString()` → `modStrings`)
- Unresolved references (e.g., enum values, external libraries)

## Phase Scope

### Phase 1: Symbol Resolution PoC (Sessions 1–2)

**In scope:** `frmCompanyDetail.cls` and `modStrings.bas`

**Deliverables:**
- Symbol table JSON for both modules
- Dependency graph (call chains, cross-module references)
- Annotated unresolved identifiers with reason (enum, external library, late-bound)
- Proposed IR schema extension for enriched events
- PoC report assessing feasibility for Phase 2

**Known challenges:**
- Enum value resolution (requires enum declaration parsing)
- External library refs (VBA built-ins like `DoCmd.*` need type-library mapping)
- Late-bound calls (`Variant`, `CallByName` — can't always resolve)
- Module-level state (static variables affect behavior)
- Recursive dependencies (cycle detection needed)

### Phase 2: Pipeline Integration (Session 3+, if PoC succeeds)

- Integrate symbol resolution into harvester pipeline (`translate` → `enrich` phases)
- Apply to all form/module pairs in gold-standard DBs
- Validate against test suite
- Performance benchmarks

## Proposed IR Schema Extension

**Current event representation:**
```json
{
  "OnClick": "[Event Procedure]"
}
```

**Proposed enriched representation:**
```json
{
  "OnClick": {
    "ValueType": "MeSub",
    "ValueIdentifier": "cmdCancel_Click",
    "Parameters": [
      {"Name": "Cancel", "DataType": "Integer", "PassingMechanism": "ByRef"}
    ],
    "InferredFunctionality": "Closes the form without saving changes. Reverts any pending edits.",
    "ReferencedIdentifiers": [
      {"Name": "Me.Undo", "Type": "Form method", "Status": "resolved"},
      {"Name": "DoCmd.Close", "Type": "VBA built-in", "Status": "resolved"}
    ],
    "UnresolvedIdentifiers": []
  }
}
```

## Success Criteria (PoC)

- [ ] Symbol table generated for both modules with ≥95% identifier coverage
- [ ] Cross-module references (frmCompanyDetail → modStrings) correctly resolved
- [ ] Unresolved identifiers clearly flagged with reason (enum, external library, late-bound)
- [ ] Enriched event schema demonstrates semantic richness
- [ ] PoC report and algorithm spec ready for Phase 2 planning

---

## Session Plan

### Session 1 of 2–4: Symbol Resolution Algorithm + PoC Scaffolding

- Read target VBA: `raw/Forms/frmCompanyDetail.txt` and `raw/ModulesAndClasses/modStrings.txt`
- Route `architecture` design call for symbol resolver algorithm (scope: identifier extraction, type binding, cross-module reference tracking, unresolved classification)
- Land `tools/symbol-resolver/` scaffolding (symbol table builder, reference resolver, dependency graph generator)
- Generate baseline symbol tables and unresolved report for both modules

### Session 2 of 2–4: Schema Extension + PoC Report

- Apply symbol resolution to sample procedures from both modules
- Draft enriched IR schema extension (ref the proposed JSON above; confirm or revise)
- Produce PoC report: what resolved where, what couldn't and why, dependency graph samples
- Assess feasibility and document blockers for Phase 2

### Session 3+ (optional, if PoC succeeds): Pipeline Integration + Scaling

- Integrate symbol resolver into harvester pipeline
- Apply to all form/module pairs in gold-standard DBs
- Validate and performance-test

> Step lists above are bullet hints. The orchestrator running each session must elaborate them — including the mandatory Step 6 (build + test) and Step 8 (verification) per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Symbol resolver algorithm design (Session 1):** `route(task_type="architecture", complexity_hint=75)`. Synthesis session; auto-verified cross-provider.
- **PoC execution and schema extension (Session 2):** Mechanical if algorithm is sound; may route `schema-design` if extension requires significant reasoning.
- **Session verification:** `route(task_type="session-verification", complexity_hint=70)` per `task_type_overrides`.

## Open questions for the human

None at present.
