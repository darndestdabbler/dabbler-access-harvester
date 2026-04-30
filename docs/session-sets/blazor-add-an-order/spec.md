# Blazor Add an Order Pilot Spec

> **Purpose:** First downstream consumer of the
> `workflow-package-pilot` set. Implement the **Add an Order**
> workflow as a Blazor Server (MudBlazor) UI calling a Web API
> service layer over HTTP, both hosted in a single ASP.NET Core
> process for testing convenience but kept as logically distinct
> namespaced layers that only communicate through the
> HttpClient/REST boundary. The pilot's success criterion: the
> existing `tests/AccessHarvester.CharacterizationTests/AddAnOrder/`
> tests pass against the Blazor app's domain models without
> modification (Option (a) from `handoff.md` Section 3.3).
> **Created:** 2026-04-30
> **Session Set:** `docs/session-sets/blazor-add-an-order/`
> **Prerequisite:** `workflow-package-pilot` complete (verdict: set
> closed 2026-04-30; the four packages and the
> `AccessHarvester.CharacterizationTests` suite are the inputs
> consumed by this set).
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: This pilot ships a UI surface, but the human is honing
> UAT in another project and will hand-test as a user during/after
> the set. UAT and E2E gates are deliberately deferred to a
> follow-up set once the pilot's package shape is proven and the
> Blazor app's UAT/E2E conventions stabilize. Quality bar for this
> set: the harvester's existing characterization tests (which the
> pilot must run unmodified) plus build + cross-provider AI
> verification per session. **This is an explicit deviation from
> the When-UAT-Is-Required heuristic; revisit at the Step 9
> reorganization review if the absence of UAT causes friction.**

---

## Project Overview

### What this set delivers

1. **A single ASP.NET Core 10.0 host** at
   `output-tests/blazor-add-an-order/` containing two logically
   distinct layers in two namespaces:
   - **UI namespace** (`BlazorAddAnOrder.Ui`) — Blazor Server pages
     and components built with MudBlazor. The UI calls the service
     layer **only** through an `HttpClient` against the in-process
     REST endpoints.
   - **Service-layer namespace** (`BlazorAddAnOrder.Services`) — Web
     API controllers, request/response DTOs, and a persistence
     facade. The service layer does not reference UI types.

   Both layers live in the same project (per the human's session-2
   direction — "one project for both applications to make testing
   easier"), wired in `Program.cs` via
   `builder.Services.AddControllers()`. The HTTP boundary is real
   even though the listener is in-process.

2. **An Add-an-Order vertical slice** that satisfies the
   `add-an-order.md` workflow package: master-detail create flow
   for orders + line items, customer-derived tax-status default,
   product price copy on selection, save-validation, and inventory
   allocation.

3. **The harvester's existing characterization tests pass without
   modification.** The pilot's domain objects are wired so the
   pure-function tests in
   `tests/AccessHarvester.CharacterizationTests/AddAnOrder/` execute
   against the same `OrderCreationWorkflow`,
   `OrderLineItemWorkflow`, and `InventoryAllocationService`
   abstractions. The shared `AddAnOrderWorkflowModels.cs` and any
   per-test pure-function entry points are referenced from the new
   project rather than re-authored. (Option (a) from the
   `workflow-package-pilot` `handoff.md`.)

4. **Iterative service implementations:** stubbed in Session 2,
   replaced with a real persistence path (EF Core + SQLite or
   InMemory; orchestrator chooses) in Session 3.

### What this set does NOT do

- Does **not** ship the other three pilot workflows (Update an
  Order, Update a Product, Update an Employee). They follow as a
  separate set if the Add an Order pilot succeeds.
- Does **not** ship a UAT checklist or Playwright E2E tests. The
  human will hand-test. (See Configuration block rationale.)
- Does **not** ship authentication or role-gating. Per the
  workflow package's Section 6 finding, no in-form role gate
  exists in the source; the pilot assumes a single anonymous
  user.
- Does **not** refactor the harvester or the
  `AccessHarvester.CharacterizationTests` project. They are
  consumed as-is. If a test surfaces a real defect, the pilot
  changes the Blazor implementation, not the test.
- Does **not** decompose the layers into separate processes /
  ports. The HTTP boundary is in-process for this pilot; the
  spec acknowledges that a real two-process deployment is a
  later refactor (per the human's session-set scoping).
- Does **not** generalize beyond Add an Order. If the pilot's
  shape proves out, follow-up sets implement the other workflows
  and harden the layer boundary.

### Why this shape

The `workflow-package-pilot` set's "What 'Success' Looks Like"
criterion is that a downstream AI builder, given any one workflow
package + an existing component library, can implement a Blazor
slice that passes the characterization tests unmodified. This set
operationalizes that criterion on the richest behavioral surface
(Add an Order). If the package + the MudBlazor library + the
existing tests are sufficient to drive a correct implementation,
the pilot's package shape is proven. If they are not, the failure
is informative for the next iteration.

---

## Required reading

Every session must read the following before authoring code:

- `docs/planning/project-guidance.md`
- `docs/planning/lessons-learned.md`
- `docs/planning/session-set-authoring-guide.md`
- `docs/ai-led-session-workflow.md`
- This spec, end-to-end.
- `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
  (the canonical input)
- `docs/session-sets/workflow-package-pilot/handoff.md`
  (the cross-set handoff)
- `docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`
  (cross-cutting findings — especially Section 1.2 on shared
  inventory allocation)

The MudBlazor component reference for this set is
`https://mudblazor.com/api#components`. Suggested commonly-used
components for this slice: **MudDataGrid** (line-item subform),
**MudForm** + validators, **MudButton**, **MudTextField**,
**MudSelect**, **MudCheckBox**, **MudAppBar**, **MudDialog**.
Use best judgment — these are starting points, not constraints.

---

## Standards

- **.NET 10.0** for both the new project and any test wiring.
  Match the harvester's existing target framework.
- **Layer boundary discipline:**
  - `BlazorAddAnOrder.Ui` may not reference any
    `BlazorAddAnOrder.Services` types directly. The only allowed
    cross-layer reference is the shared **DTO contract**
    (request/response shapes), which lives in a third namespace
    `BlazorAddAnOrder.Contracts` so neither layer takes a
    project-level dependency on the other's internals.
  - The UI calls the service layer through `HttpClient`, even
    though the listener is in-process. No `IServiceProvider`
    shortcut, no `MediatR` in-process dispatch.
- **Routing config:** the in-process HTTP base address is
  configured in `appsettings.json` and read by the UI's
  `HttpClient` factory. Do not hard-code `localhost:5000` or
  similar in component code.
- **Service-layer testability:** controllers are thin; business
  logic lives in injectable services. The pure-function workflow
  models from
  `tests/AccessHarvester.CharacterizationTests/AddAnOrder/AddAnOrderWorkflowModels.cs`
  are the canonical domain types — the service layer **wraps**
  them with persistence, not **reimplements** them. This is what
  makes it possible for the existing characterization tests to
  run unmodified.
- **MudBlazor-specific:** use `MudThemeProvider`, `MudDialogProvider`,
  and `MudSnackbarProvider` at the layout level (the standard
  MudBlazor layout). Component selection within pages is
  orchestrator's best judgment; the package's Section 6 hints
  ("master-detail create flow", "embedded editable
  collection/grid for line items", "inline validation summaries")
  are the only constraints.
- **Lean by default** (per `project-guidance.md` Principles): no
  authentication infrastructure, no logging frameworks beyond the
  ASP.NET defaults, no abstractions without two consumers, no
  premature configuration knobs. The workflow package's
  open-questions log is the place for "what about X?" — not the
  Blazor code.

---

## Session Plan

> Three sessions. Mostly in-IDE per cost-consciousness; routed cost
> is one cross-provider session-verification per session plus the
> usual `ai-assignment.md` analysis routes when applicable. Total
> expected routed cost: ~$0.20-$0.40 across the set.
>
> Mandatory Step 6 (build + test) and Step 8 (cross-provider
> session-verification) per `docs/ai-led-session-workflow.md` apply
> to every session.

---

### Session 1 of 3: Two-Layer Scaffold + Smoke Endpoint

**Steps:**

1. Required reading per the list above. Re-read this spec.
2. Confirm Blazor Server + MudBlazor target on .NET 10.0 against
   the MudBlazor docs (`https://mudblazor.com/api#components`).
   Confirm the latest stable MudBlazor NuGet package version.
3. Create the project at `output-tests/blazor-add-an-order/` —
   one ASP.NET Core 10.0 web app project. Add it to the
   `AccessHarvester.sln` so `dotnet build` from the repo root
   covers it. (If the human prefers an isolated solution under
   `output-tests/`, surface that choice and proceed with the
   answer.)
4. Wire the two layers in `Program.cs`:
   - `builder.Services.AddRazorComponents().AddInteractiveServerComponents()`
     for Blazor Server.
   - `builder.Services.AddControllers()` for the Web API surface.
   - MudBlazor service registration (`AddMudServices()`).
   - HttpClient factory configured with the in-process base
     address from `appsettings.json` (or
     `IHttpClientFactory.CreateClient("services")` against a
     named client).
   - Map endpoints: `MapControllers()` and
     `MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
5. Author the three namespaces:
   - `BlazorAddAnOrder.Ui` — `App.razor`, `MainLayout.razor`,
     a placeholder `Home.razor` page that calls the smoke
     endpoint and renders the response.
   - `BlazorAddAnOrder.Services` — a single
     `HealthController` exposing `GET /api/health` returning
     `{"status":"ok"}`.
   - `BlazorAddAnOrder.Contracts` — a `HealthResponse` DTO.
6. Confirm layer separation by code review:
   - `Ui/*.cs|.razor` references nothing in `.Services`.
   - `Services/*.cs` references nothing in `.Ui`.
   - Both reference `.Contracts` only for the shared DTO shape.
7. `dotnet build && dotnet test` (sequential). The new project
   builds; existing tests still pass; no new tests required this
   session beyond what comes scaffolded.
8. Manual smoke check (orchestrator records the steps for the
   human's verification): `dotnet run` from the new project,
   open `https://localhost:<port>/`, confirm the placeholder
   `Home.razor` renders the `{"status":"ok"}` response. The
   orchestrator describes the verification steps; the human
   confirms during the next session if needed.
9. Cross-provider session verification (routed). The verifier
   should specifically confirm: layer separation discipline,
   `HttpClient`-only cross-layer communication, MudBlazor
   integration sanity, and that the `AccessHarvester.sln` still
   builds with the new project added.
10. Step 8: commit + push + notify.

**Creates:**

- `output-tests/blazor-add-an-order/BlazorAddAnOrder.csproj`
- `output-tests/blazor-add-an-order/Program.cs`
- `output-tests/blazor-add-an-order/appsettings.json`
- `output-tests/blazor-add-an-order/Ui/App.razor`,
  `MainLayout.razor`, `Home.razor`,
  `_Imports.razor`
- `output-tests/blazor-add-an-order/Services/HealthController.cs`
- `output-tests/blazor-add-an-order/Contracts/HealthResponse.cs`
- `docs/session-sets/blazor-add-an-order/ai-assignment.md`
- `docs/session-sets/blazor-add-an-order/session-reviews/session-001.md`

**Touches:**

- `AccessHarvester.sln` (adds the new project)

**Ends with:** Project compiles, layer separation enforced by
namespace/structure, smoke endpoint reachable from the UI through
HttpClient, harvester's existing tests still green, Session 1
verified.

**Progress keys:** `session-1/required-reading`,
`session-1/scaffold`, `session-1/layer-wiring`,
`session-1/smoke-endpoint`, `session-1/build-test`,
`session-1/verification`.

---

### Session 2 of 3: Add an Order Vertical Slice (Stubbed Services)

**Steps:**

1. Required reading. Re-read `add-an-order.md` and the relevant
   characterization tests in
   `tests/AccessHarvester.CharacterizationTests/AddAnOrder/`.
2. Reference `AddAnOrderWorkflowModels.cs` from the new project so
   its types (`OrderDraft`, `OrderCreationWorkflow`,
   `OrderLineItemWorkflow`, `InventoryAllocationService`,
   `OrderStatus`, `OrderDetailStatus`, etc.) are available without
   re-authoring. The `BlazorAddAnOrder.Services` layer wraps these
   pure-function types with persistence (stubbed this session).
3. Author the service-layer surface:
   - DTOs in `BlazorAddAnOrder.Contracts`:
     `OrderRequest`, `OrderLineItemRequest`,
     `OrderResponse`, `LineItemResponse`,
     `CustomerLookupResponse` (with `StandardTaxStatusId`),
     `ProductLookupResponse` (with `UnitPrice`).
   - Controller endpoints in `BlazorAddAnOrder.Services`:
     `POST /api/orders` (create), `POST /api/orders/{id}/lines`
     (add line item), `GET /api/customers` (list),
     `GET /api/products` (list), `GET /api/customers/{id}` and
     `GET /api/products/{id}` (lookup-with-defaults).
   - Stub implementations: in-memory backing dictionaries seeded
     with a small handful of fake customers and products.
   - The controller wraps `OrderCreationWorkflow.BeginOrderEdit`,
     `OrderCreationWorkflow.ApplyCustomerSelection`,
     `OrderLineItemWorkflow.OnProductSelected`, etc., delegating
     pure logic to the workflow models.
4. Author the UI vertical slice:
   - `OrderEntryPage.razor` (route: `/orders/new`) — master form
     with `MudForm` + validators, customer `MudSelect`, line-item
     `MudDataGrid` with inline editing, save button, validation
     summary. Components selected per orchestrator best judgment
     against the package's Section 6 hints.
   - The page calls only the service layer's REST endpoints via
     the named `HttpClient`. No direct service references.
5. Wire the harvester's existing characterization tests against
   the new project's domain types:
   - Add a project reference from
     `AccessHarvester.CharacterizationTests` to
     `output-tests/blazor-add-an-order/BlazorAddAnOrder.csproj`,
     **OR** (preferred) keep `AddAnOrderWorkflowModels.cs` as the
     shared file and have the Blazor project reference it via
     `<Compile Include="..\..\tests\AccessHarvester.CharacterizationTests\AddAnOrder\AddAnOrderWorkflowModels.cs" />`
     so neither project depends on the other. **The orchestrator
     chooses whichever wiring keeps the existing tests passing
     unmodified** — that is the binding constraint.
6. `dotnet build && dotnet test` (sequential). The
   `AccessHarvester.CharacterizationTests` AddAnOrder folder
   passes against the wired domain types. No tests are renamed,
   moved, or modified this session.
7. Manual smoke check: `dotnet run`, navigate to `/orders/new`,
   confirm the master-detail flow works end-to-end against the
   stubbed customer/product data. Order create + line-item add
   round-trip through the in-process REST boundary.
8. Cross-provider session verification (routed). Verifier
   confirms: tests pass unmodified, layer separation still
   intact, `MudBlazor` usage matches the suggested components or
   has documented justification for divergence, the package's
   Section 4 invariants are all reachable through the UI flow.
9. Step 8: commit + push + notify.

**Creates:**

- `output-tests/blazor-add-an-order/Ui/Pages/OrderEntryPage.razor`
- `output-tests/blazor-add-an-order/Ui/Components/*.razor`
  (line-item grid, validation summary, customer picker — as
  needed)
- `output-tests/blazor-add-an-order/Services/Controllers/OrdersController.cs`
- `output-tests/blazor-add-an-order/Services/Controllers/CustomersController.cs`
- `output-tests/blazor-add-an-order/Services/Controllers/ProductsController.cs`
- `output-tests/blazor-add-an-order/Services/Stubs/InMemoryCatalog.cs`
  (or equivalent stub backing store)
- `output-tests/blazor-add-an-order/Contracts/*.cs`
  (request / response DTOs)
- `docs/session-sets/blazor-add-an-order/session-reviews/session-002.md`

**Touches:**

- `tests/AccessHarvester.CharacterizationTests/AccessHarvester.CharacterizationTests.csproj`
  **only if** the chosen wiring requires a project reference. If
  the `<Compile Include="...">` approach is used, this csproj is
  not touched. Either way the test files themselves are not
  modified.
- `output-tests/blazor-add-an-order/Program.cs` (DI registration
  for the new services)

**Ends with:** The Add-an-Order vertical slice is reachable from
`/orders/new`, end-to-end through stubbed services. The
harvester's existing characterization tests pass unmodified
against the wired domain types. Session 2 verified.

**Progress keys:** `session-2/required-reading`,
`session-2/contracts-and-controllers`,
`session-2/ui-vertical-slice`, `session-2/test-wiring`,
`session-2/build-test`, `session-2/verification`.

---

### Session 3 of 3: Real Persistence + Set Closeout

**Steps:**

1. Required reading.
2. Replace stubbed services with a real persistence path. The
   orchestrator chooses **EF Core + SQLite** or **EF Core
   InMemory** based on simplicity:
   - SQLite: file-based, persists between runs, more realistic.
     Connection string in `appsettings.json`.
   - InMemory: zero setup, but loses data between runs. Acceptable
     for this pilot.
   - **Default to SQLite** unless the orchestrator surfaces a
     reason to prefer InMemory.
3. Author the persistence layer:
   - `BlazorAddAnOrder.Services.Persistence` namespace with a
     `OrdersDbContext` and entity configurations.
   - Entities mirror the workflow-model state (Order,
     OrderLineItem, Customer, Product, OrderStatus,
     OrderDetailStatus). The workflow models stay pure; entities
     are the persistence projection.
   - Migration / `EnsureCreated` on app start (lean — no
     migration history machinery for this pilot).
   - Seed data matches the Session 2 stub data so the UI and
     tests behave identically.
4. Update controllers to call the persistence layer instead of
   the in-memory stubs. The pure workflow-model logic stays
   unchanged; only the I/O boundary moves.
5. Confirm the characterization tests still pass unmodified.
   They run against the pure workflow models, so this should be
   automatic — but the orchestrator verifies as a checkpoint.
6. Manual smoke check end-to-end: `dotnet run`, create an order,
   restart the app, confirm the order persists (SQLite) or
   resets cleanly (InMemory).
7. `dotnet build && dotnet test` (sequential).
8. Author the set's `change-log.md` (in-conversation per the
   in-conversation convention; do not route `summarization` if
   the orchestrator's model can do it directly per the
   "When the orchestrator's model matches the routing target..."
   convention).
9. Update the next-session-set recommendation in
   `ai-assignment.md`. Candidates: hardening the layer boundary
   (real two-process deployment), adding the other three pilot
   workflows, adding UAT/E2E coverage, or surfacing
   open-questions resolutions back to a domain reviewer. The
   recommendation is advisory; the next-set author decides
   against the master plan / open backlog.
10. Cross-provider session verification (routed).
11. Step 8: commit + push + notify.
12. Step 9 reorganization review of
    `docs/planning/project-guidance.md` and
    `docs/planning/lessons-learned.md` after the notify.

**Creates:**

- `output-tests/blazor-add-an-order/Services/Persistence/OrdersDbContext.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/Entities/*.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/Mappings/*.cs`
  (entity-to-workflow-model mapping)
- `docs/session-sets/blazor-add-an-order/change-log.md`
- `docs/session-sets/blazor-add-an-order/session-reviews/session-003.md`

**Touches:**

- Service-layer controllers (now call persistence instead of
  stubs)
- `Program.cs` (DI registration for `OrdersDbContext`)
- `appsettings.json` (SQLite connection string if applicable)

**Ends with:** Add an Order ships against a real persistence
path, characterization tests still pass unmodified, change-log
authored, set closed.

**Progress keys:** `session-3/required-reading`,
`session-3/persistence-layer`, `session-3/controller-rewiring`,
`session-3/build-test`, `session-3/change-log`,
`session-3/next-set-recommendation`,
`session-3/verification`, `session-3/step-9-reorganization`.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Tests can't run unmodified** | The wiring required to make `AccessHarvester.CharacterizationTests/AddAnOrder/` pass forces edits to test files or to `AddAnOrderWorkflowModels.cs` | Stop. The package shape didn't survive consumption. Surface to the human. |
| **Layer separation requires breaking the HTTP boundary** | Session 2 or 3 finds that completing the workflow forces a direct service reference from the UI | Stop. Reassess whether the in-process REST boundary is sufficient. |
| **MudBlazor / .NET 10.0 incompatibility** | A target component doesn't exist on .NET 10.0 or the MudBlazor version pinned for .NET 10.0 lacks needed primitives | Stop. Surface to the human; consider downgrading .NET version or substituting components. |
| **Add an Order can't be expressed without one of the workflow package's open questions** | The implementation hits a Section 7 ambiguity that materially blocks coding | Surface the open question to the human, accept their resolution, log the decision in this set's change-log under "Resolved during implementation." |

---

## Cost Expectations

- **Routed cost target:** ~$0.05-$0.15 per cross-provider
  session-verification call. Three sessions. Expected total:
  ~$0.20-$0.40.
- **In-conversation token spend:** Substantial — the bulk of
  scaffolding, UI authoring, controller authoring, and persistence
  authoring happens in-IDE under Anthropic Max (Claude Code) or
  GitHub Copilot (Codex) windows.
- **No routed analysis** for `ai-assignment.md` authoring, the
  next-session recommendation, or the change log unless an
  individual session's Step 3.5 deems otherwise. Mandatory
  cross-provider verification per session is the only routed cost
  by default.

---

## Routing Notes

- **Session 1 (scaffold):** Either Codex or Claude. Codex is a
  natural fit for the .NET project scaffold + DI wiring. Claude
  for cross-provider independence on the layer-separation
  verification.
- **Session 2 (vertical slice):** Different orchestrator from
  Session 1 if practical. The vertical slice involves significant
  C# + Razor authoring, which both orchestrators handle equally
  well.
- **Session 3 (persistence + closeout):** Whichever orchestrator
  has not done Session 1 — or the human's choice. The Step 9
  reorganization review benefits from a fresh provider
  perspective.
- **Cross-provider verification stays mandatory** for every
  session per Rule #2 of the workflow doc. The orchestrator at
  session start picks the verifier per the current
  `task_type_overrides` in `router-config.yaml`.

---

## What "Success" Looks Like at the End of the Set

Three things must all be true for this pilot to count as a
success:

1. The `AccessHarvester.CharacterizationTests/AddAnOrder/` tests
   pass without modification against the Blazor app's wiring.
2. The human can hand-run the Add-an-Order vertical slice from
   `/orders/new` end-to-end (create order, add line items,
   save, see persistence) without diagnostic debugging.
3. The layer-separation discipline holds — code review
   confirms `BlazorAddAnOrder.Ui` does not directly reference
   `BlazorAddAnOrder.Services` types, all cross-layer
   communication goes through HttpClient + REST.

If all three hold, the workflow-package-pilot's "What 'Success'
Looks Like" criterion (workflow package + library produces a
slice whose existing tests pass unmodified) is empirically
proven. If any fail, the failure is informative — the
characterization tests, the package shape, or the chosen Blazor
patterns need revision before the other three workflows are
attempted.

The Step 9 reorganization review at the end of Session 3 is the
formal capture point for what we learned and what we'd change
about the package shape if we were authoring the next pilot.
