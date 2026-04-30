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
