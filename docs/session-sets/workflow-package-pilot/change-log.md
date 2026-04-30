# Change Log — Workflow Package Pilot

> **Session set:** `workflow-package-pilot`
> **Started:** 2026-04-30
> **Completed:** 2026-04-30
> **Total sessions:** 7 of 7
> **Verdict:** complete; cross-provider verified.

---

## Set summary

This set proves a repeatable harvest -> workflow-package pipeline on
four NorthwindDevEdition workflows. Each workflow package contains
business requirements, characterization tests, source operationalization
reference, component/role mapping hints, and an open-questions log —
enough for a downstream AI builder to implement a Blazor/C# slice that
satisfies the same user needs without copying Access mechanics.

The set was an in-IDE pair (Claude Code + Codex, alternating per
session) with one routed cross-provider verification per session. Total
routed cost stayed close to the spec's ~$0.35 estimate; Session 6's
four-round Opus verification was the only meaningful overshoot.

---

## What this set delivered

1. **Four workflow packages** covering Add an Order, Update an Order,
   Update a Product, and Update an Employee, at
   `docs/session-sets/workflow-package-pilot/packages/`.
2. **One characterization-test project** at
   `tests/AccessHarvester.CharacterizationTests/`, with per-workflow
   folders containing pure-function workflow models and xUnit tests
   that pin durable business invariants.
3. **A cross-workflow audit document** at
   `docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`.
4. **A handoff document** at
   `docs/session-sets/workflow-package-pilot/handoff.md` for the
   downstream Blazor implementation pilot.
5. **A reusable workflow-package template** at
   `docs/session-sets/workflow-package-pilot/_template.md`.

All artifacts landed without modifying the harvester or the existing
`AccessHarvester.TranslationProbe.Tests` project (per spec scope rule).

---

## Per-session summary

### Session 1 — Setup, Inventory, Open Decisions

- Surfaced and resolved both Open Decisions (default (b) structured
  prose; default (a) generic-now mapping hints).
- Authored `inventory.md` mapping the four pilot workflows to their
  Access artifacts.
- Authored `_template.md` (the seven-section workflow-package
  template).
- Scaffolded `tests/AccessHarvester.CharacterizationTests/` and
  added it to the solution.
- Cross-provider verified by Gemini Pro: VERIFIED, 0 issues.

### Session 2 — Add an Order package draft (Codex)

- Authored `packages/add-an-order.md` per the seven-section template.
- Generated characterization tests in
  `tests/AccessHarvester.CharacterizationTests/AddAnOrder/`.
- Cross-provider verified by Gemini Pro: VERIFIED.

### Session 3 — Add an Order peer review and falsification (Claude)

- Independent semantic review of the Session 2 package and tests.
- Added falsification tests for missing invariants
  (`Session3CrossOrderScopeTests.cs`,
  `Session3DeletionReallocationTests.cs`).
- Refined the package's open-questions log.
- Cross-provider verified by Gemini Pro: VERIFIED.

### Session 4 — Update an Order single-pass package (Codex)

- Authored `packages/update-an-order.md` and
  `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/`.
- Captured the order lifecycle state machine
  (New -> Invoiced -> Shipped -> Paid -> Closed) plus the
  status-driven `LockControls` matrix.
- Cross-provider verified by Gemini Pro: VERIFIED.

### Session 5 — Update a Product single-pass package (Claude)

- Authored `packages/update-a-product.md` and
  `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/`.
- Captured cascading-effect rules: PO outstanding gating, vendor
  selection, reorder-quantity fallback chain, stock-take allocation.
- Cross-provider verified by Gemini Pro: VERIFIED.

### Session 6 — Update an Employee single-pass package (Codex)

- Authored `packages/update-an-employee.md` and
  `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/`.
- Captured `EmployeeCanBeDeleted` four-way dependency check, MRU
  cleanup, post-insert privilege-assignment launch, and the negative
  finding that no in-form `HasPrivilege` gate exists.
- Cross-provider verified by Claude Opus (per session-local human
  direction): VERIFIED, 0 issues after 4 rounds of grounded
  refinements.
- Note: Session 6 deliverables were authored cleanly but the commit
  step did not run; Session 7 bundled them with the Session 7 commit.

### Session 7 — Cross-workflow audit, handoff, change log (Claude — this session)

- Authored
  `docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`
  covering recurring rules, confidence-tag calibration, anti-coupling
  across the suite, role-logic treatment, and source-operationalization
  format fit.
- Authored
  `docs/session-sets/workflow-package-pilot/handoff.md` for the
  downstream Blazor implementation pilot.
- Authored this change-log.
- Authored Session 7 actuals and the next-session-set recommendation
  in `ai-assignment.md`.
- Bundled Session 6's untracked deliverables into the Session 7 commit.
- Cross-provider verified by Gemini Pro.

---

## What did not happen

- No Blazor implementation work. The packages are inputs to a
  downstream session set.
- No coverage of the rest of NorthwindDevEdition or of the other two
  sample databases. Only the four pilot workflows.
- No harvester refactor. Components were consumed as-is.
- No backfill of the `Confidence*` / `Justification*` /
  `Interpretation*` columns from the prior `vba-symbol-db` work.
  Those remain throw-away under the Hybrid strategy.
- No consolidation of `tests/AccessHarvester.TranslationProbe.Tests/`.
  It coexists with `AccessHarvester.CharacterizationTests/`.
- The pilot did not trigger any of the spec's stop rules. Specifically:
  - No tests turned out to be UI-coupled.
  - No workflow took more than 2 sessions to package.
  - The cross-workflow audit did not find systematic invented
    rationale.
  - No harvester gaps forced manual spelunking.
  - Session 3's peer review found a manageable number of falsification
    candidates; it did not exceed the "more than 4-5 missing
    invariants" threshold.

---

## Key cross-workflow findings

From the cross-workflow audit:

- **The `ValidateForm` save gate appears in all four workflows** with
  consistent wording. It is a single shared abstraction, not four
  per-page validators.
- **Inventory allocation** is shared between Add an Order and Update
  a Product with consistent wording. The Blazor pilot should
  implement it once as a domain service.
- **All confirmed-tagged rules** across all four packages are
  `confirmed`; uncertainty went into the open-questions logs (5, 3,
  5, and 5 entries respectively). The `inferred` middle band was
  not exercised in this pilot; future sets with sparser evidence
  should use it.
- **No in-form role gating** was confirmed in any of the four
  workflows. The Blazor pilot must decide role mappings explicitly.
- **Structured prose** worked for 3 of 4 workflows. Update an Order
  is a state machine and would benefit from UAT DSL in the next set.

The audit and handoff documents contain the full findings and
recommendations for the downstream Blazor pilot.

---

## Cost summary

- Routed verification calls only (per spec):
  - Sessions 1-5, 7: ~$0.05-$0.10 each (Gemini Pro)
  - Session 6: $2.83 across four Opus rounds
- All package authoring, test authoring, audit, handoff, change-log,
  and ai-assignment updates were in-conversation per the
  outsource-last pattern.

---

## Recommended next session set

Per the `handoff.md`'s Section 6: a Blazor implementation pilot for
Add an Order, contingent on the Blazor component library being
ready at the level the handoff describes. If the library is not yet
at that level, the next set should be a Blazor library extension
set, not the implementation pilot. Final selection is the next-set
author's call against the master plan / open backlog.
