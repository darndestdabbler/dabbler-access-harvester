# Handoff — Workflow Package Pilot to Downstream Blazor Implementation Pilot

> **Session:** 7 of 7
> **Author orchestrator:** Claude Opus 4.7 (Claude Code), in-conversation
> **Created:** 2026-04-30
> **Audience:** Whoever scopes and runs the downstream
> "blazor-implementation-pilot" session set (or its renamed equivalent).

---

## 1. What is ready for handoff

Four workflow packages plus a characterization-test suite, all
landed in the repo:

```
docs/session-sets/workflow-package-pilot/
  packages/
    add-an-order.md
    update-an-order.md
    update-a-product.md
    update-an-employee.md
  cross-workflow-audit.md
  handoff.md          (this document)
  change-log.md

tests/AccessHarvester.CharacterizationTests/
  AddAnOrder/        (~890 LOC, 6 test files + 1 workflow-model file)
  UpdateAnOrder/     (~705 LOC, 2 test files + 1 workflow-model file)
  UpdateAProduct/    (~1119 LOC, 7 test files + 1 workflow-model file)
  UpdateAnEmployee/  (~480 LOC, 4 test files + 1 workflow-model file)
```

All tests pass (107 in CharacterizationTests; the broader suite stays
green). All four packages were cross-provider verified during the
authoring sessions — see
`docs/session-sets/workflow-package-pilot/session-reviews/`.

The cross-workflow audit at
`docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`
confirms the four packages form a coherent dataset and exposes
audit-level findings the individual sessions could not have seen.

---

## 2. The success criterion this set delivers against

From the pilot spec ("What 'Success' Looks Like"):

> A downstream AI builder, given any one of the four workflow packages
> plus the existing Blazor component library, should be able to
> implement a Blazor slice that:
>
> 1. Solves the same business problem as the Access workflow.
> 2. Passes the characterization tests without modification.
> 3. Uses Blazor patterns natural to the component library, not
>    patterns translated from Access.
> 4. Surfaces, to a human reviewer, the package's open-questions
>    list as known limitations (not as silent assumptions).

The pilot delivers the **inputs** for that test (packages + tests).
The downstream pilot is the actual proof.

---

## 3. Recommended downstream-pilot scope

### 3.1 Implement Add an Order first

Add an Order is the richest behavioral surface — it exercises:

- Multi-entity create with required-field validation
- Customer-driven default propagation (`TaxStatusID`)
- Inventory allocation (the core fulfillment side effect)
- Per-line-item state transitions (`New` -> `Allocated` /
  `OnOrder` / `NoStock`)
- Cross-order rebalancing on save and on delete

If the Blazor library plus the package together can produce a
working Add an Order slice, the pilot has its proof. If they
can't, the failure tells the team where the package shape needs
revision before scaling.

### 3.2 Defer the three update workflows behind Add an Order

Once Add an Order ships, the three update workflows reuse most of
its infrastructure:

- Update an Order shares the order detail form / state machine
- Update a Product shares the inventory allocation service
- Update an Employee adds a roster + privilege editor surface

A single update-workflow follow-up set could carry all three at
once, since they have less behavioral surface per workflow than
Add an Order.

### 3.3 Blazor library prerequisites

The handoff assumes the Blazor library will provide (or grow to
provide):

- A master-detail page pattern (Add an Order header + line items)
- A grid-with-details admin page pattern (Update an Employee)
- A detail-with-tabs page pattern (Update a Product)
- A status-driven action rail / state-machine workflow pattern
  (Update an Order)
- A confirm-dialog pattern (delete confirmations across all four)
- A modal / nested editor pattern (Update an Employee privilege
  editor)
- Required-field validation primitives (all four workflows share
  the `ValidateForm` save-gate pattern)
- Authorization plumbing — though see Section 4.3 below; the pilot
  did not surface confirmed in-form role gates from Access, so the
  Blazor implementation must decide role mappings explicitly.

If the library lacks any of these, that gap is a downstream-pilot
prerequisite, not a failure of the workflow packages.

---

## 4. Known limitations and open questions to carry forward

### 4.1 Open-questions logs are explicit and inherited

Each package has its own Section 7 open-questions log. The Blazor
pilot inherits all of them. Total open-question count:

| Package | Open questions |
|---|---:|
| Add an Order | 5 |
| Update an Order | 3 |
| Update a Product | 5 |
| Update an Employee | 5 |
| **Total** | **18** |

These are not blockers — they are explicit "the package surfaces
this; the rewrite must decide" items. The Blazor pilot's
session-set author should review them at scoping time and decide
which ones need pre-implementation domain-reviewer input vs. which
ones can be answered during implementation.

### 4.2 Tests are pure-function characterization tests, not
end-to-end tests

The characterization tests assert business invariants against
pure-function workflow models. They do **not**:

- Execute against a real database
- Drive a UI
- Verify the Blazor components render as designed

The Blazor pilot's E2E coverage (Playwright per
`docs/ai-led-session-workflow.md` UAT/E2E rules) is independent and
needs to be authored fresh. The characterization tests guarantee
that the *business invariants* survive the rewrite; the E2E tests
prove that the *user workflow* survives the rewrite.

### 4.3 No confirmed in-form role gating exists in the Access source

The cross-workflow audit (Section 4) confirmed that none of the
four pilot workflows contain a confirmed `HasPrivilege(...)` gate.
The Update an Employee package surfaced the strongest candidate
mapping (`UserAdmin` / `SystemAdmin`); the others have weaker
prior signal.

**Action for the Blazor pilot's authoring step:** add explicit
role-mapping decisions to the implementation spec. Treat the
packages' Section 6 hints as starting points; route any
substantive role-mapping decision to a domain reviewer or the
human (the pilot's spec author chose not to mark these as
blockers because the Blazor library may dictate the right
mapping anyway).

### 4.4 The shared `AllocateInventory` policy must be implemented as
a single service

Cross-workflow audit Section 1.2 found that two packages (Add an
Order and Update a Product) cite the same allocation policy with
the same wording. The Blazor pilot must implement this **once**, as
a domain service, and call it from both workflows. Treating it as
two separate per-page concerns will silently diverge.

### 4.5 The `OrderID` tiebreak in `AllocateInventory` is open

Both Add an Order and Update a Product surface the same open
question about whether the `ORDER BY OrderDate, OrderID` tiebreak
is business-visible or implementation detail. The audit recommends
preserving the tiebreak in the initial Blazor slice and confirming
with a domain reviewer post-implementation. The
`Session3CrossOrderScopeTests` already pin the tiebreak; the
Blazor implementation can either honor it (tests pass) or
deliberately diverge (tests fail and surface the choice).

### 4.6 Format selection for source-operationalization references

Open Decision 1 was resolved with structured prose for the pilot.
The cross-workflow audit (Section 5) found that prose works for
3 of 4 workflows but is borderline for Update an Order's state
machine. The recommendation for the next set is to author state-
machine workflows in UAT DSL and the others in structured prose.
This is a planning-time decision for the next set's spec.

### 4.7 The pilot intentionally did not exercise the `inferred`
confidence tag

All visible rules across the four packages are tagged `confirmed`.
Uncertainty went into the open-questions logs instead. If the
Blazor library leads to friction implementing what the packages
characterize as confirmed, the pilot's structure does not
distinguish "Access source clearly says X" from "Access source
clearly says X *and* the policy is intended-permanent."
Future workflow-package authoring should consider exercising the
`inferred` tag for the latter case.

---

## 5. What the pilot does NOT deliver (deliberately)

Per the spec's "What this set does NOT do" section:

- No Blazor code. The packages are inputs to the Blazor pilot, not
  implementation work.
- No coverage of the rest of NorthwindDevEdition. Only four
  workflows. If the pilot succeeds, scaling is a follow-up set.
- No coverage of `MiniComplex.accdb` or `HomeHealthCare.accdb`.
  Those validate generalization *after* the Northwind pilot proves
  the package shape.
- No harvester refactoring. If the Blazor pilot needs richer
  harvester output, that's a separate harvester-extension set
  surfaced via this handoff.
- No backfill of the `Confidence*` / `Justification*` /
  `Interpretation*` columns from the prior `vba-symbol-db` work.
  Those are throw-away under the Hybrid strategy.
- No consolidation of the existing
  `tests/AccessHarvester.TranslationProbe.Tests/` project. It
  coexists with the new `AccessHarvester.CharacterizationTests`
  project.

---

## 6. Cross-set session-set handoff recommendation

Per Session 7 Step 6 of the spec and the
`feedback_next_session_set_recommendation` memory entry — this set
authors a recommendation for the next set, but final selection is
the next-set author's call against the master plan / open backlog.

**Proposed next set: `blazor-implementation-pilot-add-an-order`** (or
similar slug).

- **Prerequisites met:** all four pilot packages, the
  characterization-test suite, the cross-workflow audit, and this
  handoff.
- **Prerequisites still needed (verify before starting):** the
  in-progress Blazor component library at the level of patterns
  listed in Section 3.3. If the library is not yet at that level,
  the next set should be a Blazor library extension set, not the
  implementation pilot.
- **Recommended scope:** implement Add an Order end-to-end. Pass
  every test in `tests/AccessHarvester.CharacterizationTests/AddAnOrder/`.
  Author E2E (Playwright) coverage if the next set's spec declares
  `requiresE2E: true`. Surface every Section 7 open question to a
  human reviewer at the appropriate scoping checkpoint.
- **Recommended exit criterion:** Add an Order ships in Blazor with
  the existing characterization tests passing unmodified. If they
  pass, the package shape is proven and the three update workflows
  follow naturally. If they don't pass, the failure tells the team
  whether the gap is in the package, the tests, or the library.

If the human's master plan (when present) names a different next
set, defer to it. This recommendation is advisory.

---

## 7. Quick-reference index

For the next set's author:

| Need | Path |
|---|---|
| Workflow business rules + invariants | `docs/session-sets/workflow-package-pilot/packages/<workflow-slug>.md` |
| Characterization tests | `tests/AccessHarvester.CharacterizationTests/<WorkflowSlug>/*.cs` |
| Cross-workflow patterns | `docs/session-sets/workflow-package-pilot/cross-workflow-audit.md` |
| Original spec (for context) | `docs/session-sets/workflow-package-pilot/spec.md` |
| Per-session verification rounds | `docs/session-sets/workflow-package-pilot/session-reviews/session-NNN.md` |
| Per-session orchestrator + cost ledger | `docs/session-sets/workflow-package-pilot/ai-assignment.md` |
| Workflow + procedure inventory | `docs/session-sets/workflow-package-pilot/inventory.md` |
| Workflow-package template (reusable) | `docs/session-sets/workflow-package-pilot/_template.md` |

The pilot is closed. The next set picks up from this handoff.
