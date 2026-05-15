# Blazor Update Workflows Spec

> **Purpose:** Implement the three remaining Northwind pilot workflows
> — `Update an Order`, `Update a Product`, and `Update an Employee` —
> against the same Blazor host established by `blazor-add-an-order`.
> Success criterion (inherited from Phase 2's Option D resolution):
> the unmodified `tests/AccessHarvester.CharacterizationTests/`
> tests in the `UpdateAnOrder/`, `UpdateAProduct/`, and
> `UpdateAnEmployee/` folders continue to pass against the Blazor
> app's parallel domain services. The Blazor app gains three new
> vertical slices reachable from the UI; the existing
> `Add an Order` slice keeps working unchanged.
> **Created:** 2026-05-15
> **Session Set:** `docs/session-sets/blazor-update-workflows/`
> **Prerequisite:** `workflow-package-pilot` complete (the four
> packages and the four characterization-test suites are the inputs
> consumed by this set); `blazor-add-an-order` complete (the
> existing Blazor host at `output-tests/blazor-add-an-order/` is
> extended in-place); `generalization-validation-on-non-northwind`
> complete (verdict `generalizes-cleanly` resolved the Phase-3a-vs-3c
> sequencing question in favor of 3a).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification.
> Per-session cost target ~$0.05–$0.15 routed (cross-provider
> session-verification only); in-IDE for everything else.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: This set ships three additional vertical UI slices on
> top of the `blazor-add-an-order` host, matching that set's
> deferred-UAT pattern. Phase 3b in `docs/planning/strategy.md` is
> explicitly named as the UAT/E2E formalization step and pairs
> naturally with 3a once all four workflows are shipped — a single
> later set can add UAT to all four together. **This is an explicit
> deviation from the When-UAT-Is-Required heuristic; revisit at the
> Step 9 reorganization review if absence of UAT causes friction
> during implementation.** Quality bar for this set: the harvester's
> existing characterization tests (which the implementation must
> keep passing unmodified) plus build + cross-provider AI
> verification per session, plus a Session-4 cross-workflow smoke.

---

## Why This Set, Why Now

`blazor-add-an-order` (closed 2026-04-30) proved the workflow-package
+ MudBlazor + EF Core / SQLite Blazor host shape on the richest
behavioral surface (`Add an Order`). All four workflow packages
already exist from `workflow-package-pilot`; all four
characterization-test folders already exist and the
`UpdateAnOrder/`, `UpdateAProduct/`, and `UpdateAnEmployee/`
suites pass against the Blazor app's parallel domain services
(per the Option D resolution). What's missing is the UI + service +
persistence wiring for those three workflows.

`generalization-validation-on-non-northwind` (closed 2026-05-15)
resolved Open Validation #2 — the package shape generalizes
cleanly to HHC — which removes the structural-risk argument for
sequencing 3c (more generalization probes) before 3a (this set).
Strategy doc's advisory sequencing is now unambiguously
3a → 3b/3c → 3e → 3d → 3f → 3g.

This set is Phase 3a per `docs/planning/strategy.md`. Cheapest /
highest-value next move per the strategy doc's own framing:
"likely the cheapest / highest-value next move if the team wants
Northwind to feel like a real working application end-to-end."

---

## Project Overview

### What this set delivers

1. **Three new vertical slices** in the existing
   `output-tests/blazor-add-an-order/` project:
   - **Update an Order** — order-list landing + detail/edit page.
     Status-driven edits (`New` → `Invoiced` → `Shipped` → `Closed`,
     plus `Cancelled`/`Deleted`), invoice creation gate, shipping
     fee + payment + closure transitions, line-item allocation
     rebalancing on edit.
   - **Update a Product** — product-list landing + detail/edit page.
     Reorder-level / discontinue toggling, inventory-affecting
     edits.
   - **Update an Employee** — employee-list landing + detail/edit
     page. Simplest single-entity edit form; minimal state machine.

2. **Persistence + DI extensions** in the existing
   `BlazorAddAnOrder.Services.Persistence` namespace:
   - `OrdersDbContext` gains `Products`, `Employees`, `Invoices`,
     `Payments`, `Shipments` DbSets as each workflow lands.
   - `DatabaseSeeder` extended with idempotent seed data for the
     new entities sufficient to demonstrate each update workflow.
   - New `*Store` classes per workflow (mirroring `OrdersStore`).

3. **Domain services** in
   `BlazorAddAnOrder.Services.Domain` per the Option D pattern:
   - `OrderUpdateService` honoring `update-an-order.md`'s rules and
     invariants; reuses `InventoryAllocationService`.
   - `ProductUpdateService` honoring `update-a-product.md`'s rules.
   - `EmployeeUpdateService` honoring `update-an-employee.md`'s
     rules.
   - The harvester's characterization tests stay unmodified;
     parallel-domain-logic-honoring-the-same-invariants pattern
     from `blazor-add-an-order`.

4. **Routes and navigation hooks**:
   - `/orders` — order-list landing page used by both Add and
     Update flows.
   - `/orders/{id}/edit` — Update an Order detail page.
   - `/products` and `/products/{id}/edit`.
   - `/employees` and `/employees/{id}/edit`.
   - `MainLayout.razor` / `Routes.razor` gain navigation entries.

5. **A change-log + Step 9 reorganization review** per the standard
   workflow.

### What this set does NOT do

- Does not author any new workflow packages — `update-an-order.md`,
  `update-a-product.md`, and `update-an-employee.md` already exist
  and are the inputs.
- Does not modify the existing characterization-test files. New
  test code can be added if a Session 4 cross-workflow smoke
  test needs end-to-end harness coverage, but the existing tests
  are kept unmodified per the Option D pilot success criterion.
- Does not add UAT or E2E coverage. That is Phase 3b's scope.
- Does not extract shared workflow code between the test
  fixtures and the Blazor service layer. The duplication is a
  deliberate Option D choice; revisiting it is Phase 3f.
- Does not rename `output-tests/blazor-add-an-order/` or its
  namespaces. The project is now logically "the Blazor pilot
  host" rather than "the Add an Order pilot"; a rename can land
  later if naming becomes misleading.
- Does not change the in-process HttpClient boundary discipline
  established by `blazor-add-an-order`. UI must talk to services
  only through HTTP.
- Does not add Authentication / Authorization. Per
  `update-an-employee.md`, no per-action privilege gates were
  observed in the source; this set inherits that posture.

### Inputs available

- `docs/session-sets/workflow-package-pilot/packages/update-an-order.md`
  — primary input for Session 1 (richest update workflow).
- `docs/session-sets/workflow-package-pilot/packages/update-a-product.md`
  — primary input for Session 2.
- `docs/session-sets/workflow-package-pilot/packages/update-an-employee.md`
  — primary input for Session 3.
- `docs/session-sets/blazor-add-an-order/change-log.md` and `spec.md`
  — the established project structure, Option D resolution rationale,
  and namespace discipline.
- `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/`,
  `UpdateAProduct/`, `UpdateAnEmployee/` — the test suites that must
  continue passing unmodified.
- `output-tests/blazor-add-an-order/` — the existing Blazor host
  (Ui/, Services/, Contracts/, Program.cs, BlazorAddAnOrder.csproj).
- `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/` — the
  Northwind harvester output if any session needs to cross-check the
  workflow package against the source IR.

---

## Stop Rules and Pivot Signals

| Stop rule | Signal | Pivot |
|---|---|---|
| **Characterization tests fail unmodified after wiring** | A `UpdateAnOrder/`, `UpdateAProduct/`, or `UpdateAnEmployee/` test fails when the new service is registered, even though the Blazor domain service implements the package's invariants | Stop. Surface to human. The Option D contract from `blazor-add-an-order` says the tests stay unmodified; if a test fails, the Blazor service is wrong, not the test. Fix the service, do NOT edit the test. |
| **Package rule contradicts existing host pattern** | A rule in the update package implies a code shape that breaks the existing `Ui/Services/Contracts` namespace discipline or the HttpClient boundary | Stop. Surface to human. The boundary discipline is non-negotiable; the spec may need a revision rather than a workaround. |
| **EF Core migration cycle exceeds one session** | The persistence extension for a single workflow requires a schema-altering migration that takes more than one session to design + apply + reseed | Stop. Surface to human. Either the workflow's persistence is genuinely complex enough to justify its own session-set (split), or the migration approach is over-engineered (use `EnsureCreated` like `blazor-add-an-order` rather than full EF migrations). |
| **MudBlazor pattern doesn't fit the update flow** | The update flow exercises a pattern (e.g., status-driven conditional disable, in-row inline edit) that MudBlazor doesn't support cleanly | Stop. Surface to human. Document the gap and recommend either a workaround within MudBlazor or a Phase-3e (library swap) follow-up. Do NOT silently invent a component. |
| **Single workflow session blows context budget** | A workflow's implementation can't fit in one session (Blazor page + service + persistence + characterization-test re-run + cross-provider verification) | Stop. Surface to human. Either split the workflow across two sessions (extend the set) or descope (e.g., ship the update page but defer some status transitions to a follow-up). |

If any stop rule fires, the orchestrator surfaces to the human and
waits for direction; does not auto-pivot.

---

## Session Plan

> Four sessions. Per-session cost target: ~$0.05–$0.15 routed
> (cross-provider session-verification only). In-IDE for everything
> else — orchestrator authors code, runs the build, runs the
> characterization-test re-run, and the routed call is just the
> cross-provider review at the end. **Mandatory build + test and
> Step 6 cross-provider session-verification per
> `docs/ai-led-session-workflow.md` apply to every session.**

### Session 1 of 4: Update an Order

**Orchestrator note:** richest workflow; recommended orchestrator
= Claude Opus 4.7 @ effort=high (or Codex GPT-5 @ effort=high
when the human wants to burn the Copilot allotment). Routed
verification still mandatory.

**Steps:**

1. Required reading:
   `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`,
   `docs/planning/session-set-authoring-guide.md`,
   `docs/session-sets/workflow-package-pilot/packages/update-an-order.md`
   (end-to-end),
   `docs/session-sets/blazor-add-an-order/change-log.md` (the
   established project shape and Option D pattern),
   `docs/session-sets/blazor-add-an-order/spec.md` (Section 5
   namespace discipline, Section 6 HTTP boundary discipline).
2. Verify the prior session set committed (`generalization-validation-on-non-northwind`).
3. **Persistence extension.** Extend `OrdersDbContext` with the
   entities the update workflow needs that `blazor-add-an-order`
   didn't ship: `Invoice`, `Payment`, `Shipment` plus
   `Shippers`/`PaymentMethods` lookups if not already present.
   Extend `DatabaseSeeder` to seed enough data to demonstrate the
   workflow's state transitions (one `New` order, one
   `Invoiced` order, etc.). Use `EnsureCreated` per the prior
   set's pattern — no EF migrations.
4. **Domain service.** Author
   `BlazorAddAnOrder.Services.Domain.OrderUpdateService` (or
   extend `OrderCreationService` if the human prefers; surface
   the choice at first authoring). Implement Section 3 rules and
   Section 4 invariants from `update-an-order.md`:
   - Status-driven edits (`New` / `Invoiced` / `Shipped` /
     `Closed` / `Cancelled` / `Deleted`)
   - Invoice creation gate (all-line-items-allocated +
     shipping-fee-filled-out)
   - Shipping fee + payment + closure transitions
   - Line-item reallocation on edit (reuses
     `InventoryAllocationService` from `blazor-add-an-order`).
5. **Controller + DTOs.** Extend `OrdersController` with
   `GET /api/orders` (list), `GET /api/orders/{id}`, and
   `PUT /api/orders/{id}` plus action endpoints (`POST
   /api/orders/{id}/invoice`, `POST /api/orders/{id}/ship`,
   etc., per the package's Section 5 operationalization). Add
   request/response DTOs in `Contracts/`. The HTTP boundary
   stays real.
6. **UI.** Author `Pages/OrderListPage.razor` (route `/orders`)
   and `Pages/OrderEditPage.razor` (route
   `/orders/{id}/edit`). MudBlazor components throughout
   (`MudTable`, `MudForm`, `MudButton`, `MudSelect`,
   `MudNumericField`, `MudSnackbar`, validation alerts).
   Status-driven conditional disable.
7. **Navigation.** Add `/orders` to `MainLayout.razor` or
   `Routes.razor` so the new pages are reachable from the app
   shell.
8. **Smoke check.** Run the app locally, navigate to `/orders`,
   open one seeded order, perform one state transition (e.g.,
   New → Invoiced), confirm persistence-across-restart.
9. `dotnet build && dotnet test` (sequential). The
   `UpdateAnOrder/` characterization tests must continue passing
   unmodified.
10. Cross-provider session-verification per Step 6.

**Creates:**
- `output-tests/blazor-add-an-order/Services/Domain/OrderUpdateService.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/` — new
  entity classes, mapping helpers, store classes as needed for
  Invoice/Payment/Shipment
- `output-tests/blazor-add-an-order/Contracts/` — Update / list
  / action DTOs
- `output-tests/blazor-add-an-order/Ui/Pages/OrderListPage.razor`
- `output-tests/blazor-add-an-order/Ui/Pages/OrderEditPage.razor`
- `docs/session-sets/blazor-update-workflows/ai-assignment.md`
- `docs/session-sets/blazor-update-workflows/disposition.json` (in-progress)
- `docs/session-sets/blazor-update-workflows/session-reviews/session-001.md`

**Touches:**
- `output-tests/blazor-add-an-order/Services/Persistence/OrdersDbContext.cs` (new DbSets)
- `output-tests/blazor-add-an-order/Services/Persistence/DatabaseSeeder.cs` (seed extensions)
- `output-tests/blazor-add-an-order/Services/Controllers/OrdersController.cs` (list + update + action endpoints)
- `output-tests/blazor-add-an-order/Ui/Routes.razor` or `MainLayout.razor` (nav entries)
- `output-tests/blazor-add-an-order/Program.cs` (DI registration for new service + store)

**Ends with:** `/orders` reachable; one seeded order survives a
status transition + restart; `UpdateAnOrder/` characterization
tests green; Session 1 verified.

**Progress keys:** `session-1/required-reading`,
`session-1/prior-session-committed-check`,
`session-1/persistence-extension`,
`session-1/domain-service`,
`session-1/controller-dtos`,
`session-1/ui-pages`,
`session-1/navigation`,
`session-1/smoke-check`,
`session-1/build-test`,
`session-1/verification`.

---

### Session 2 of 4: Update a Product

**Orchestrator note:** lighter workflow than Session 1; can use
Claude Opus or Codex GPT-5 at moderate effort. Same routed
verification mandatory.

**Steps:**

1. Required reading: `update-a-product.md` end-to-end, plus
   Session 1's `OrderUpdateService` for the established
   service-shape pattern.
2. Verify Session 1 committed.
3. **Persistence extension.** Extend `OrdersDbContext` with
   `Product`-related entities if they aren't already wired (the
   Add an Order seed already includes `Products`; this session
   likely just adds new fields or stores). Extend seed data
   with products spanning discontinued / reorder-level
   variations.
4. **Domain service.** Author
   `BlazorAddAnOrder.Services.Domain.ProductUpdateService`
   honoring `update-a-product.md`'s rules and invariants.
5. **Controller + DTOs.** Extend or add `ProductsController`
   with list/get/update endpoints.
6. **UI.** Author `Pages/ProductListPage.razor` and
   `Pages/ProductEditPage.razor`. MudBlazor throughout.
7. **Navigation.** Add `/products` to the app shell.
8. **Smoke check.** Run app, edit a product (e.g., toggle
   discontinued, change reorder level), confirm persistence.
9. `dotnet build && dotnet test` (sequential).
   `UpdateAProduct/` tests pass unmodified.
10. Cross-provider session-verification.

**Creates:**
- `output-tests/blazor-add-an-order/Services/Domain/ProductUpdateService.cs`
- `output-tests/blazor-add-an-order/Contracts/` — Product DTOs
- `output-tests/blazor-add-an-order/Ui/Pages/ProductListPage.razor`
- `output-tests/blazor-add-an-order/Ui/Pages/ProductEditPage.razor`
- `docs/session-sets/blazor-update-workflows/session-reviews/session-002.md`

**Touches:**
- `output-tests/blazor-add-an-order/Services/Persistence/OrdersDbContext.cs` (if Product entity needs new fields)
- `output-tests/blazor-add-an-order/Services/Persistence/DatabaseSeeder.cs`
- `output-tests/blazor-add-an-order/Services/Controllers/ProductsController.cs` (likely new file)
- `output-tests/blazor-add-an-order/Ui/Routes.razor` or `MainLayout.razor`
- `output-tests/blazor-add-an-order/Program.cs`
- `docs/session-sets/blazor-update-workflows/ai-assignment.md`
- `docs/session-sets/blazor-update-workflows/disposition.json`

**Ends with:** `/products` reachable; product edits persist;
`UpdateAProduct/` characterization tests green; Session 2
verified.

**Progress keys:** `session-2/required-reading`,
`session-2/prior-session-committed-check`,
`session-2/persistence-extension`,
`session-2/domain-service`,
`session-2/controller-dtos`,
`session-2/ui-pages`,
`session-2/navigation`,
`session-2/smoke-check`,
`session-2/build-test`,
`session-2/verification`.

---

### Session 3 of 4: Update an Employee

**Orchestrator note:** simplest workflow; routed verification mandatory.

**Steps:**

1. Required reading: `update-an-employee.md` end-to-end, plus
   Session 2's `ProductUpdateService` for the established
   simpler-update pattern.
2. Verify Session 2 committed.
3. **Persistence extension.** Add `Employee` entity to
   `OrdersDbContext` if not already present. Extend seed data
   with a few employees.
4. **Domain service.** Author
   `BlazorAddAnOrder.Services.Domain.EmployeeUpdateService`.
5. **Controller + DTOs.** Add `EmployeesController` with
   list/get/update.
6. **UI.** Author `Pages/EmployeeListPage.razor` and
   `Pages/EmployeeEditPage.razor`.
7. **Navigation.** Add `/employees` to the app shell.
8. **Smoke check.** Run app, edit an employee, confirm
   persistence.
9. `dotnet build && dotnet test` (sequential).
   `UpdateAnEmployee/` tests pass unmodified.
10. Cross-provider session-verification.

**Creates:**
- `output-tests/blazor-add-an-order/Services/Domain/EmployeeUpdateService.cs`
- `output-tests/blazor-add-an-order/Services/Controllers/EmployeesController.cs`
- `output-tests/blazor-add-an-order/Contracts/` — Employee DTOs
- `output-tests/blazor-add-an-order/Ui/Pages/EmployeeListPage.razor`
- `output-tests/blazor-add-an-order/Ui/Pages/EmployeeEditPage.razor`
- `docs/session-sets/blazor-update-workflows/session-reviews/session-003.md`

**Touches:**
- `output-tests/blazor-add-an-order/Services/Persistence/OrdersDbContext.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/DatabaseSeeder.cs`
- `output-tests/blazor-add-an-order/Ui/Routes.razor` or `MainLayout.razor`
- `output-tests/blazor-add-an-order/Program.cs`
- `docs/session-sets/blazor-update-workflows/ai-assignment.md`
- `docs/session-sets/blazor-update-workflows/disposition.json`

**Ends with:** `/employees` reachable; employee edits persist;
`UpdateAnEmployee/` characterization tests green; Session 3
verified.

**Progress keys:** `session-3/required-reading`,
`session-3/prior-session-committed-check`,
`session-3/persistence-extension`,
`session-3/domain-service`,
`session-3/controller-dtos`,
`session-3/ui-pages`,
`session-3/navigation`,
`session-3/smoke-check`,
`session-3/build-test`,
`session-3/verification`.

---

### Session 4 of 4: Cross-Workflow Smoke + Change Log + Close

**Orchestrator note:** synthesis-heavy capstone; recommended
Claude Opus 4.7 @ high. Authors closeout artifacts in-conversation
per the orchestrator-matches-routing-target convention.

**Steps:**

1. Required reading: Sessions 1-3 change-log entries, the four
   workflow packages in pairwise cross-reference (look for any
   pattern that landed inconsistently across the four slices).
2. Verify Session 3 committed.
3. **Cross-workflow smoke pass.** Run the Blazor app locally and
   exercise all four workflows end-to-end in one browser session:
   create an order, update that order through its status
   transitions, edit a product, edit an employee, confirm
   navigation between pages works and the SQLite database
   survives restart with all four workflows' state intact.
   Capture findings inline in this session's review section.
4. **Cross-workflow regression check.** Re-run the full
   `dotnet test` suite. Confirm all 111+ characterization tests
   (now spanning AddAnOrder + the three update folders) pass
   unmodified.
5. **Author the change-log.** In-conversation per the
   in-conversation summarization convention. Headline: the four
   workflows now form a Northwind end-to-end working application
   slice. Reference each session's per-workflow deliverables and
   the Option D pattern carrying across.
6. **Author the next-session-set recommendation.** Per the
   strategy doc, Phase 3b (UAT + E2E coverage on the Blazor
   pilot) is the natural next move now that all four workflows
   are shipped. The recommendation should also flag Phase 3e
   (component-library swap) as a parallel-eligible candidate if
   the dabbler component library has landed by then.
7. **Author `disposition.json`** before the closeout commit per
   the Convention.
8. `dotnet build && dotnet test` (sequential, final).
9. Cross-provider session-verification.
10. Per workflow Step 8: commit, push, `mark_session_complete()`,
    notify.
11. Per workflow Step 9: reorganization review of
    `docs/planning/project-guidance.md` and
    `docs/planning/lessons-learned.md` AFTER the notify.

**Creates:**
- `docs/session-sets/blazor-update-workflows/change-log.md`
- `docs/session-sets/blazor-update-workflows/next-session-set-recommendation.md`
- `docs/session-sets/blazor-update-workflows/session-reviews/session-004.md`

**Touches:**
- `docs/session-sets/blazor-update-workflows/ai-assignment.md` (Session 4 actuals)
- `docs/session-sets/blazor-update-workflows/disposition.json` (status -> complete)

**Ends with:** Four-workflow Blazor pilot complete; cross-workflow
smoke + regression pass green; next move (Phase 3b) clearly named.

**Progress keys:** `session-4/required-reading`,
`session-4/prior-session-committed-check`,
`session-4/cross-workflow-smoke`,
`session-4/regression-check`,
`session-4/change-log`,
`session-4/next-session-set-recommendation`,
`session-4/disposition`,
`session-4/build-test`,
`session-4/verification`,
`session-4/step-9-reorganization`.

---

## Deliverables (End of Set)

1. Three new Blazor vertical slices reachable from the running
   app (`/orders` + `/orders/{id}/edit`, `/products` +
   `/products/{id}/edit`, `/employees` + `/employees/{id}/edit`).
2. Three new domain services (`OrderUpdateService`,
   `ProductUpdateService`, `EmployeeUpdateService`) implementing
   the respective package's Section 3 rules and Section 4
   invariants.
3. Extended persistence layer (new entities, store classes,
   seeded data covering each workflow's state transitions).
4. Extended controllers + DTOs honoring the HTTP boundary
   discipline.
5. All four characterization-test suites (`AddAnOrder/`,
   `UpdateAnOrder/`, `UpdateAProduct/`, `UpdateAnEmployee/`)
   continuing to pass **unmodified**.
6. `change-log.md`, `next-session-set-recommendation.md`,
   `disposition.json` — standard closeout artifacts.
7. (If applicable) Step 9 reorganization commit, separately.

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.15 per session
  (cross-provider session-verification only); ~$0.20–$0.60
  total across the set.
- **In-conversation token spend:** Substantial — Blazor pages,
  domain services, persistence wiring, controller endpoints, and
  closeout authoring all happen in-IDE under Anthropic Max or
  GitHub Copilot windows. Matches the `blazor-add-an-order`
  pattern.

---

## What "Success" Looks Like at the End of the Set

A reviewer can launch the Blazor app locally, navigate from the
landing page to all four workflows, and exercise each end-to-end:

1. Create a new order, persist it, verify it lives across a
   process restart.
2. Open that order in the update flow, walk it through
   `New → Invoiced → Shipped → Closed` (or
   `New → Cancelled`), and confirm line-item allocation
   re-balances correctly when edits affect inventory.
3. Edit a product (toggle discontinued, change reorder level,
   change unit price) and confirm the update persists.
4. Edit an employee (change name, contact info, role) and
   confirm the update persists.
5. Confirm the full `dotnet test` suite is green with no test
   modifications.

If all five hold, the Blazor pilot has expanded from one
workflow to four — the Northwind end-to-end working application
that Phase 3a was designed to deliver. The next strategic review
flips Open Validation #10 from `OPEN` to `RESOLVED` (the four
workflows are the count Phase 3a tested), and Phase 3b becomes
the next named move.

If any workflow's package shape forces a session to deviate from
the existing host's pattern, that deviation is itself a
generalization finding worth surfacing — it would suggest the
workflow-package shape doesn't yet handle the Northwind update
variations as cleanly as it handles the create flow, and a
future template-revision pass may be warranted.
