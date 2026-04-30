# AI Assignment — `blazor-add-an-order`

> **Authoring note (Session 1):** The human directed at session start
> that **no work this set route externally except for cross-provider
> session-verification, which must go to Gemini Pro**. That directive
> overrides Rule #17 (route the `ai-assignment.md` analysis instead of
> self-opining) for this set. The recommendations below are therefore
> the orchestrator's own opinion — disclosed here so the verifier and
> any future orchestrator can weigh that bias. The `routed cost`
> estimates correspondingly drop to "verifier-only" since no other
> calls hit the router.

---

## Session 1: Two-Layer Scaffold + Smoke Endpoint

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=high

### Rationale
This session is mostly mechanical scaffold work (project creation,
DI wiring, three small files), but the layer-separation discipline
and the `HttpClient`-only cross-layer rule reward an orchestrator
that can reason about namespace dependencies as it writes. Opus 4.7
on Claude Code handles both: keystroke-cheap on the scaffold,
attentive on the discipline.

### Estimated routed cost
Verifier-only (per the human's session-start directive). Expected
routed spend: one cross-provider session-verification call to
Gemini Pro at the end of the session — typically $0.05–$0.15.

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Required reading | In-conversation (mechanical reads) |
| 2 | Confirm Blazor Server + MudBlazor on .NET 10.0; latest MudBlazor NuGet version | In-conversation per human directive (would normally be a quick `analysis` route or a WebFetch); use WebFetch against `https://mudblazor.com/api#components` and the MudBlazor NuGet listing. |
| 3 | Create the project at `output-tests/blazor-add-an-order/` and add it to `AccessHarvester.sln` | In-conversation (mechanical scaffold) |
| 4 | Wire `Program.cs` (Razor Components + Controllers + MudBlazor + HttpClient + endpoint mapping) | In-conversation (mechanical, single file under ~50 lines once scaffolded) |
| 5 | Author the three namespaces (`Ui`, `Services`, `Contracts`) with the smoke endpoint and a placeholder `Home.razor` | In-conversation (mechanical scaffold across small files) |
| 6 | Code-review layer separation discipline | In-conversation per human directive (would normally route `code-review`); the cross-provider verifier at Step 6 of the workflow doc covers the cross-provider check. |
| 7 | `dotnet build && dotnet test` (sequential) | In-conversation (shell command) |
| 8 | Document manual smoke-check steps for the human | In-conversation (short prose) |
| 9 | Cross-provider session verification | **Routed** — `route(task_type="session-verification")` to Gemini Pro per the human's directive (overrides any task-type override that would have selected GPT-5.4). |
| 10 | Step 8: commit + push + notify | In-conversation (shell + helper) |

### Actuals (filled after the session)
- Orchestrator used: claude-code claude-opus-4-7 @ effort=high
- Total routed cost: $0.0105 (one Gemini Pro session-verification call,
  4,983 input / 424 output tokens — well below the spec's
  $0.05–$0.15 per-session expectation because the verification was
  the only routed work).
- Deviations from recommendation: None. The session followed the spec's
  step list verbatim. Two procedural deviations from the workflow doc
  were carried as **pre-disclosed by the human's session-start
  directive**: (1) `ai-assignment.md` authored in-conversation rather
  than via routed `analysis` (Rule #17); (2) layer-separation code
  review performed in-conversation rather than via routed
  `code-review`. Both disclosed in the verifier prompt; verdict was
  VERIFIED, no issues.
- Notes for next-session calibration:
  - The spec's Session 1 cost target ($0.05–$0.15) is now overshoot
    for this set — the actual lower-bound is whatever a single Gemini
    Pro session-verification call costs ($0.01 here at ~5K input
    tokens).
  - MudBlazor 9.4.0 confirmed compatible with net10.0; pin this
    version going into Session 2 to avoid drift.
  - One Razor-template fix landed during build (`@rendermode="InteractiveServer"`
    → `@rendermode="RenderMode.InteractiveServer"`); the .NET 10
    runtime requires the qualified form. Worth flagging to Session 2's
    orchestrator if it authors more Razor pages.

## Session 2: Add an Order Vertical Slice (Stubbed Services)

**Authoring note:** Per the human's set-wide directive, this
recommendation was authored in-conversation rather than via
`route(task_type="analysis")`. The orchestrator-bias caveat from
Session 1 applies here too — disclosed for the next session's
verifier and any reader.

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=high

### Rationale
Session 2 is the largest of the three: it authors the contracts,
the controllers, the in-memory stub catalog, the Razor page with
MudForm + MudDataGrid + validators, the embedded line-item editor,
and the wiring that makes the harvester's existing characterization
tests pass without modification. The wiring decision is non-trivial
because the test fixtures' workflow classes are `private sealed`
inside the test files — meaning the only **shared** types are in
`AddAnOrderWorkflowModels.cs` (`OrderDraft`, `OrderLineItemDraft`,
`OrderStatus`, `OrderDetailStatus`, `PendingOrderDetail`). The spec's
"reference workflow models so tests pass unmodified" phrasing assumes
the workflow classes are shared, which the actual test code does not
support. **The Session 2 orchestrator must surface this to the human
before authoring** — it is the spec's binding constraint and may
require resolution per the spec's first Stop Rule. Opus 4.7 on Claude
Code is the right fit because it can hold the constraint reasoning,
the multi-file Razor authoring, and the in-IDE shell loop together
without losing thread; Codex would also work but the constraint
discussion is easier in the same window as the file authoring.

### Estimated routed cost
Verifier-only (the human's session-start directive remains in force
across the set unless the human pivots). Expected: one cross-provider
session-verification call to Gemini Pro at the end of the session,
likely $0.05–$0.20 because the session-2 verifier prompt will be
substantially larger (more files, more code, more invariants to
audit) than Session 1's $0.01.

### First step for Session 2
**Open the spec's Stop Rule #1 conversation with the human before
writing code.** The spec instructs the orchestrator to "reference
`AddAnOrderWorkflowModels.cs` so its types …
`OrderCreationWorkflow`, `OrderLineItemWorkflow`,
`InventoryAllocationService` … are available without re-authoring,"
but those workflow classes are `private sealed` inside the test files
and cannot be shared with the Blazor project as written. Two
candidate resolutions:

1. **Reauthor the workflow classes inside `BlazorAddAnOrder.Services`**
   (or a new `BlazorAddAnOrder.Domain` namespace) and have the test
   files reference them rather than duplicating them. This requires
   modifying the test files' `using` statements — a tiny edit, but
   the spec's Section "What this set does NOT do" forbids modifying
   the tests. Surface to the human whether this counts as
   modification or as a wiring change.
2. **Promote the workflow classes from `private sealed` to `internal`
   types in a shared file** (e.g., a new
   `tests/AccessHarvester.CharacterizationTests/AddAnOrder/AddAnOrderWorkflowImplementations.cs`)
   that both the test project and the Blazor project compile via
   `<Compile Include="…">`. Same caveat — the test files would lose
   their nested classes, which is a behavioral test-file change.

The Stop Rule says: stop and surface to the human. Do that.

**Next-session orchestrator recommendation (Session 2):**
claude-code claude-opus-4-7 @ effort=high
Rationale: see above — the constraint reasoning + Razor authoring
mix favors Opus on Claude Code, and the Session 1 directive against
external routing remains in force.

### Stop Rule #1 resolution (Session 2 first action)

The Session 2 orchestrator (claude-code claude-opus-4-7 @ effort=high)
re-confirmed the binding constraint at session start:

- `AddAnOrderWorkflowModels.cs` declares only `internal sealed`
  data/state types (`OrderStatus`, `OrderDetailStatus`, `OrderDraft`,
  `OrderLineItemDraft`, `PendingOrderDetail`) — not workflow classes.
  Even those are `internal` (assembly-scoped).
- The workflow *classes* the spec names — `OrderCreationWorkflow`,
  `OrderLineItemWorkflow`, `InventoryAllocator` (the actual name; the
  spec said `InventoryAllocationService`), plus `LineItemDeletionWorkflow`
  and `OrderDeletionWorkflow` from Session3 tests — are each
  `private sealed class` nested inside one test file. They are
  unsharable as written, even within the test assembly.
- `AccessHarvester.CharacterizationTests.csproj` has no project
  references and is by design a self-contained pure-function fixture.

Four resolution options were surfaced to the human:

- **A:** Treat tests-already-pass as the success criterion; Blazor
  authors its own service-layer logic that honors the same invariants;
  no shared workflow code.
- **B:** Promote test-file workflow classes to a shared `internal`
  file in the test project + `InternalsVisibleTo` to the Blazor project,
  edit test files to remove their nested copies. Violates "do not
  modify tests."
- **C:** Same as B but workflow classes own-by-Blazor; test project
  takes a project reference. Stronger violation of "do not refactor
  the CharacterizationTests project."
- **D:** Option A's code shape, with explicit documentation that the
  spec's Section "What 'Success' Looks Like" #3 is clarified — the
  pilot proves *invariant correspondence* between the package + tests
  and the Blazor service layer, not *single-source workflow code*.

**Human chose Option D** in-conversation at Session 2 start.

### Clarified success criterion for the rest of this set (Option D)

The pilot's success criterion #2 ("characterization tests pass without
modification against the Blazor app's wiring") is preserved in letter:
the existing tests pass, untouched. The deeper interpretation
("Blazor service layer and test fixtures share workflow code") is
explicitly **not** the criterion. Instead:

- The Blazor service layer authors its own `OrderCreationService`,
  `OrderLineItemService`, and `InventoryAllocationService` inside the
  `BlazorAddAnOrder.Services.Domain` namespace, implementing the same
  invariants the package's Section 3 rules and Section 4 invariants pin.
- The CharacterizationTests/AddAnOrder/ folder is consumed unchanged
  and continues to validate the package shape against its own private
  workflow fixtures.
- The Blazor project's behavioral correctness against the same rules
  is validated by hand-test (the spec's `requiresUAT: false` rationale
  applies — human hand-tests as user) plus cross-provider session
  verification per the workflow.
- A future session set may revisit shared workflow code if the
  duplicated invariant logic becomes a maintenance pain point. For
  this set the duplication is a deliberate trade for letter-perfect
  test-file preservation.

This clarification is logged here in `ai-assignment.md` and will be
captured again in Session 3's `change-log.md` when the set closes.

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Required reading | In-conversation |
| 2 | Stop Rule #1 conversation with human | In-conversation (already happened — see above) |
| 3 | Author `BlazorAddAnOrder.Services.Domain` types (own enums, drafts, services implementing the package's Section 3 rules / Section 4 invariants) | In-conversation (multi-file but mechanical against the package) |
| 4 | Author `BlazorAddAnOrder.Contracts` DTOs | In-conversation (mechanical) |
| 5 | Author `BlazorAddAnOrder.Services` controllers + `InMemoryCatalog` stub | In-conversation (mechanical) |
| 6 | Author UI vertical slice (`OrderEntryPage.razor` + helpers) | In-conversation (multi-file Razor authoring) |
| 7 | Wire DI in `Program.cs` | In-conversation (mechanical) |
| 8 | `dotnet build && dotnet test` (sequential) | In-conversation (shell) |
| 9 | Cross-provider session verification | **Routed** — `route(task_type="session-verification")` to Gemini Pro per the human's set-wide directive (overrides any task-type override). |
| 10 | Step 8: commit + push + mark complete + notify | In-conversation |

### Actuals (filled after the session)
- Orchestrator used: claude-code claude-opus-4-7 @ effort=high
- Total routed cost: $0.0503 (one Gemini Pro session-verification call,
  30,353 input / 1,231 output tokens). The verification cost ran above
  Session 1's $0.01 because the prompt now carries spec + ai-assignment +
  workflow package + four characterization-test files + 16 Blazor source
  files; this matches the spec's "$0.05–$0.20 expected" estimate.
- Deviations from recommendation: None on routing. Two procedural
  deviations from the workflow doc remain pre-disclosed by the human's
  set-wide directive: (1) `ai-assignment.md` Session 2 update authored
  in-conversation rather than via routed `analysis` (Rule #17); (2)
  layer-separation code review and test-correspondence review performed
  in-conversation rather than via routed `code-review`. The Stop Rule
  #1 conversation at session start is logged in this file.
- Notes for next-session calibration:
  - **First Gemini Pro call returned 503 Service Unavailable** after
    three internal retries; a 90s wait + manual retry succeeded. This
    matches the verifier-failure escalation ladder's step 1 (try same
    provider twice). No code change required; flag for Session 3 as a
    transient-availability hint — if Gemini Pro is unreliable on the
    morning of Session 3, fall back per the ladder rather than fight it.
  - **Verification prompt size: ~30K input tokens.** Session 3's
    verification will likely be similar (persistence + change-log + the
    same Blazor surface), well within Gemini Pro's window.
  - **Option D resolution worked cleanly** — the verifier explicitly
    confirmed invariant correspondence between the Blazor service layer
    and the test fixtures' private workflow logic, and accepted that no
    shared workflow code exists. Session 3 can lean on the same model:
    re-verify invariant correspondence after persistence rewires the
    services to call EF Core instead of the in-memory stub. The pure
    domain logic should be unchanged.
  - **InMemoryCatalog seed data lives in `Services/Stubs/InMemoryCatalog.cs`.**
    Session 3's persistence layer should match this seed exactly so the
    UI experience and invariant-corresponding tests stay green across
    the in-memory → SQLite swap.

**Next-session orchestrator recommendation (Session 3):**
claude-code claude-opus-4-7 @ effort=high

Rationale (authored in-conversation per the human's set-wide directive
against external routing for analysis/recommendation tasks; the
orchestrator-bias caveat applies here too): Session 3 swaps the
in-memory stub for EF Core + SQLite, threads the persistence boundary
through the existing controllers without changing the domain services,
generates the change-log, runs the Step 9 reorganization review, and
must keep the characterization tests green plus the invariant
correspondence intact. Opus 4.7 on Claude Code is the right fit because
it can hold the EF Core entity-mapping work, the controller-rewiring
diff, and the change-log authoring together in one window. Codex/GPT
would also work; the only differentiator is the human's preference for
the same orchestrator across the set so Session 3 inherits Session 2's
context for the Step 9 review.

The set-wide directive (no external routing except cross-provider
session-verification, which goes to Gemini Pro) remains in force for
Session 3 unless the human pivots. Expected routed cost: ~$0.05–$0.10
for the Session 3 verification (the prompt grows by an EF Core entity
file and a change-log, but the domain code is largely unchanged).

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Required reading | In-conversation |
| 2 | Choose persistence path (default SQLite per spec) | In-conversation |
| 3 | Author `Services/Persistence/OrdersDbContext.cs` + entities + mapping | In-conversation |
| 4 | Update controllers to call persistence instead of stubs | In-conversation |
| 5 | Confirm characterization tests still pass | In-conversation (shell) |
| 6 | Manual smoke check end-to-end | In-conversation (shell + prose) |
| 7 | `dotnet build && dotnet test` | In-conversation (shell) |
| 8 | Author `change-log.md` | In-conversation per `task_type="summarization"` substitution — the Convention says when the orchestrator's model matches the routing target, handle in-conversation. This applies cleanly here because the orchestrator IS the recommended summarization-tier when the human set-wide directive forbids routing to a tier-1 Gemini summarization model. |
| 9 | Update next-session-set recommendation in `ai-assignment.md` | In-conversation per directive |
| 10 | Cross-provider session verification | **Routed** — `query("gemini-pro", task_type="session-verification", ...)` per the directive |
| 11 | Step 8: commit + push + mark complete + notify | In-conversation |
| 12 | Step 9 reorganization review post-notify | In-conversation |
