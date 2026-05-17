# Strategy — Building the Blazor/C# Replacement of Access Applications

> **Audience:** Any orchestrator picking up this work. Read this
> alongside `project-guidance.md`, `lessons-learned.md`, and
> `project-status.md`. The session-set specs and change-logs under
> `docs/session-sets/` are the operational answer to this strategy;
> this document is the strategic frame they sit inside.
> **Status:** Approved direction as of 2026-04-30; revised
> 2026-04-30 (Phase 1 + Phase 2 actuals) and 2026-05-17 (Phase 3
> reset based on cross-provider strategic review; Access-to-Blazor
> application work in this repo declared complete; finish line for
> the mechanical harvester named in Phase 4). See Revisions.
> **Landing window:** Phases 1 + 2 landed within one day on
> 2026-04-30. Phase 3 candidates 3a / 3b / 3e / 3f / 3g are deferred
> to a future migration repo per the 2026-05-17 review; 3c landed
> 2026-05-15. The repo's remaining work is now Phase 4 (mechanical
> harvester finish line) only.

---

## The Ultimate Goal

Build a **Blazor/C# system that has equivalent functionality** as
existing Microsoft Access applications. Target databases:
`NorthwindDevEdition.accdb` (primary proving ground), plus
`MiniComplex.accdb` and `HomeHealthCare.accdb` as additional corpus
for generalization.

The new system must:

1. Solve the same user problems as the Access app.
2. Use patterns natural to Blazor/C# — component-based UI,
   server/client state separation, async I/O, EF Core or similar
   modern data access.
3. **Not** be a literal port. The new system's code should look
   like Blazor written by someone who understood the requirements,
   not like translated VBA wrapped in Razor markup.

The work will be executed primarily by AI orchestrators (Claude
Code, GitHub Copilot/Codex, or similar), guided by a human
architect who sets direction and adjudicates ambiguity.

## What Supports the Goal (Outside This Repo)

Two adjacent in-progress investments shape the strategy:

- **A custom Blazor component library.** Provides opinionated
  components — master-detail (filter row + results grid +
  collapsible accordion + details form), a robust security
  framework with delegated role assignment and a baked-in role
  taxonomy (`SystemAdmin`, `UserAdmin`, `CrossOrgUserAdmin`,
  `CrossOrgMonitor`, `User`), and other domain-aligned patterns.
  In active development. The Blazor implementations of migrated
  Access workflows will use this library; in its absence, generic
  MudBlazor is the placeholder.
- **A VS Code extension for AI orchestration.** Supports two
  modes: outsource-first (route everything externally first) and
  outsource-last (suppress outsourcing except for cross-provider
  verification). In active development. The pilot work in this
  repo is designed to fit cleanly into the outsource-last mode.

Neither blocks the harvester pilot work. The strategy is designed
so that each lands when ready and the work picks up the benefits in
flight without restructuring.

## The UAT DSL

A sibling repo (`../dabbler-uat-dsl`) defines a formal grammar for
expressing workflow operationalizations as ordered sequences of UI
actions. The DSL is one of two candidate formats for documenting
the source-side (Access) operationalization in workflow packages
(see *Definitions* below); the other is structured prose. The
choice is deferred to the active pilot's first session.

---

## Definitions

These terms are used throughout this strategy and the active
session-set specs. Use them consistently.

### Workflow

A user-facing slice that crosses one or more forms, queries, and
events to accomplish a single business goal.
Implementation-agnostic. Survives the rewrite. Examples: "Add an
Order," "Update a Product."

### Workflow operationalization

An at-least roughly (and sometimes strictly) ordered sequence of
user UI actions — some of which may be parameterized with things
like unique text or specific selections — that are repeated by users
for a target outcome, that can be formally expressed by the UAT DSL
(see `../dabbler-uat-dsl`), that typically have a parallel but
sparsely aligned set of parameterized CRUD operations, and that
accomplish the business purpose of the associated workflow. A
workflow operationalization can be specific to the software in
which it is implemented.

The **source operationalization** is how the Access app does it.
The **target operationalization** is how the Blazor implementation
will do it (authored later by the AI builder, not by this strategy
or the harvester pilot).

### Behavior-bearing procedure

A VBA procedure whose body contains business logic that survives
the rewrite. Concretely: validators, calculations, state-change
logic, save/delete gating, business-rule-heavy query shaping.

Roughly **10-20% of procedures** in a typical Access codebase are
behavior-bearing; the rest are pure UI choreography, navigation,
trivial wrappers, or dead code. The strategy invests
characterization-test effort only in the behavior-bearing minority.

### Workflow package

The deliverable shape per workflow. A single markdown document
plus associated characterization tests, containing seven
sections:

1. **Workflow summary** — what users do, why; implementation-agnostic.
2. **Actors and entities** — who initiates, what's primary, what's related.
3. **Business rules with evidence and confidence tags** —
   bulleted rules with citations to harvester output and a
   `confirmed` / `inferred` / `unknown` tag.
4. **Behavioral invariants → characterization tests** — pinned by
   xUnit tests in `tests/AccessHarvester.CharacterizationTests/`,
   asserting behavior independently of the operationalization.
5. **Source operationalization reference** — the Access click-sequence,
   in UAT DSL or structured prose (decided per pilot).
6. **Component / role mapping hints** — advisory mapping to the
   Blazor library's components and role taxonomy.
7. **Open questions / ambiguity log** — explicit unknowns with
   recommended resolution paths.

A package is sufficient when an AI builder can implement a Blazor
slice that satisfies the same business problem, passes the
characterization tests unchanged, uses Blazor patterns natural to
the component library, and surfaces the open-questions list as
known limitations rather than silent assumptions.

---

## How We Got Here (the Strategic Inflection Points)

The strategy is the synthesis of two completed experiments and one
deliberate strategic-review round. An orchestrator picking up this
work should understand the inflections, not just the conclusions.

### Inflection 1 — `vba-symbol-db` (PIVOT, ~$24 routed)

The first attempt to make the symbol DB consumable by downstream AI
treated it as input to **AI interpretation**: routed per-symbol
cross-provider AI calls populated `Purpose`, `Justification`,
`Confidence` columns on every symbol row. A calibration session
showed that self-rated `Confidence` correlates **+0.004 (Pearson)
with cross-provider agreement**, and **-0.18 with justification
agreement**. The rigor model was uncalibrated. Verdict: PIVOT to a
different rigor mechanism.

**Lesson promoted to convention:** routed cross-provider rigor that
proves uncalibrated should be replaced with a *cheaper* rigor model
(subscription-paid in-IDE orchestrator pairs), not abandoned.
Action-detail reference in `lessons-learned.md`.

### Inflection 2 — `vba-symbol-db-path-b-probe` (Hybrid verdict, ~$0.23 routed)

A three-session probe tested a different consumption model:
**translation + tests** instead of interpretation. One in-IDE AI
(Codex) translated one VBA procedure into testable C#; a different
in-IDE AI (Claude) independently peer-reviewed and added tests
pinning subtle invariants. The probe verdict was **Hybrid at the
procedure level** — Path B (translation + tests) for procedures
with falsifiable behavior; Path A (AI interpretation) for the rest.

The probe surfaced three concrete examples of invariants that the
peer-review tests would catch but a prose interpretation would
miss (eager `Or` semantics, inner-vs-outer error-handler parameter
distinction, defensive `IsFormOpen` guard before `DoCmd.Close`).
Decision document at
`docs/session-sets/vba-symbol-db-path-b-probe/decision.md`.

**Lesson promoted to convention:** subscription-paid in-IDE
orchestrator pairs (Codex + Claude in different VS Code windows)
produce cross-provider falsification at marginal-zero per-call
cost. The trade is wall-clock time for routed-call cost.

### Inflection 3 — Strategic review, manager-level reframing (2026-04-30)

After the path-b-probe verdict, an architectural proposal was
drafted to build a "JIT mechanical translator" with AI-fallback for
unsupported VBA constructs. Two cross-provider reviewers (Gemini
Pro and GPT-5.4) pushed back hard: **the probe proved tests matter,
not that translator infrastructure matters.** The reviewers were
right. The proposal was retracted.

The human then reframed the question at the manager level: what
**intermediate objective** best serves the ultimate goal? A second
strategic-review round (also Gemini Pro + GPT-5.4) converged with
high confidence on **Option 4 / scoped Option 5: workflow packages
containing requirements + characterization tests + design-spec
reference + ambiguity log**, scoped to 3-5 workflows at a time
rather than full-app coverage.

This document captures that synthesis as the operating strategy.
Review packages preserved at `tmp/feedback/` for audit.

---

## The Strategy in Phases

Three phases, sequential. Each phase has a clear success criterion
that gates the next.

### Phase 1 — Workflow Package Pilot (COMPLETE, 2026-04-30)

**Spec:** [`docs/session-sets/workflow-package-pilot/spec.md`](../session-sets/workflow-package-pilot/spec.md)
**Change log:** [`docs/session-sets/workflow-package-pilot/change-log.md`](../session-sets/workflow-package-pilot/change-log.md)
**Cross-workflow audit:** [`docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`](../session-sets/workflow-package-pilot/cross-workflow-audit.md)
**Handoff:** [`docs/session-sets/workflow-package-pilot/handoff.md`](../session-sets/workflow-package-pilot/handoff.md)

**Scope:** Produced four workflow packages from
NorthwindDevEdition.accdb (Add an Order, Update an Order, Update a
Product, Update an Employee). Each package follows the seven-section
template plus a characterization-test suite at
`tests/AccessHarvester.CharacterizationTests/`.

**Sessions actuals:** 7 sessions, all VERIFIED. Add an Order took
two sessions (draft + peer review per spec); the three update
workflows took one session each; setup, audit, and handoff took
one session each.

**Cost actuals:** ~$3.20 routed total. **Sessions 1-5 and 7
came in close to the original ~$0.35 estimate** (Gemini Pro
per-session verification, ~$0.05-$0.10 each). **Session 6 was a
$2.83 outlier** — the verifier was switched to Claude Opus per
session-local human direction, and the verification took four
rounds of grounded refinements (privilege-editor anti-coupling
phrasing, exclusion-list precision, MRU-cleanup naming fidelity).
The four rounds were grounded refinements, not corrections to
substantive findings.

**Outputs delivered:**

- **Four workflow packages** at
  `docs/session-sets/workflow-package-pilot/packages/` covering all
  four pilot workflows. Total open-questions count: 18 (5 + 3 + 5
  + 5 across the four packages).
- **107 characterization tests** in
  `tests/AccessHarvester.CharacterizationTests/`, organized into
  per-workflow folders (AddAnOrder ~890 LOC; UpdateAnOrder ~705 LOC;
  UpdateAProduct ~1119 LOC; UpdateAnEmployee ~480 LOC). All pass.
- **Cross-workflow audit** confirming the four packages form an
  internally coherent dataset; no UI-coupled tests; no systematic
  invented rationale; consistent confidence-tag calibration.
- **Reusable workflow-package template** at
  `docs/session-sets/workflow-package-pilot/_template.md`.

**Stop rules:** None triggered. The pilot landed cleanly across all
seven sessions.

**Success criterion (the spec's "What 'Success' Looks Like"):** The
audit could not directly verify because the downstream Blazor
implementation pilot is a separate set. **Phase 2 below now provides
that empirical verification.**

**Key cross-workflow findings worth carrying forward:**

- The `ValidateForm` save gate appears in all four workflows with
  consistent wording — implement once as a shared abstraction, not
  four per-page validators.
- Inventory allocation is shared between Add an Order and Update a
  Product with consistent wording — implement once as a domain
  service.
- All visible rules across all four packages are tagged
  `confirmed`; uncertainty went into the open-questions logs. The
  `inferred` middle band was not exercised — future packages with
  sparser evidence should use it.
- **No in-form role gating was confirmed in any of the four
  workflows.** The Blazor implementation must decide role mappings
  explicitly; the packages correctly surface this as an open
  question rather than inventing role logic.
- Structured prose worked for 3 of 4 workflows; Update an Order's
  state machine would benefit from UAT DSL in the next set.

**Lesson promoted to Convention during Phase 1's Step 9
reorganization:** *Verify the prior session actually committed and
pushed before starting a new one.* Originally proposed during the
`vba-symbol-db-path-b-probe` Step 9 review, validated on the
recurrence in this set when Session 6's deliverables were
completed but commit was skipped (Session 7 bundled them).
Mechanical promotion criterion met. Action-detail reference
preserved in `lessons-learned.md`.

### Phase 2 — Blazor Add an Order Pilot (COMPLETE, 2026-04-30)

**Spec:** [`docs/session-sets/blazor-add-an-order/spec.md`](../session-sets/blazor-add-an-order/spec.md)
**Change log:** [`docs/session-sets/blazor-add-an-order/change-log.md`](../session-sets/blazor-add-an-order/change-log.md)

**Scope:** Implemented Add an Order end-to-end as a Blazor Server +
MudBlazor UI calling a Web API service layer over an in-process
HTTP boundary, all in a single ASP.NET Core 10.0 host with three
namespace-distinct layers (Ui / Services / Contracts). Real
persistence via EF Core 10.0 + SQLite. Reachable from
`/orders/new`; orders survive process restart.

**Sessions actuals:** 3 sessions, all VERIFIED. Session 1 scaffold
+ smoke endpoint; Session 2 vertical slice with stubbed services;
Session 3 real persistence + closeout.

**Cost actuals:** ~$0.11 routed total ($0.0105 + $0.0503 + $0.0458
across three Gemini Pro verifications). Well under the spec's
$0.20-$0.40 estimate.

**Tooling decisions made:**

- **Generic MudBlazor used as placeholder** — the in-progress
  custom Blazor component library was not used in this pilot.
  Substitution into the library when it lands is a follow-up pass.
- **Single-host architecture** — the layer separation is enforced
  by namespace + HttpClient boundary, not by separate processes.
  Real two-process deployment is a follow-up refactor, not a
  failure of the pilot.
- **EF Core + SQLite** chosen over EF Core InMemory for
  persistence-across-restart realism.
- **Authentication and role gating deliberately not implemented** —
  the workflow package's Section 6 finding (no in-form role gating
  exists in the Access source) means the new system must decide
  roles explicitly rather than translate them. Pilot deferred that
  decision.

**Success criterion: PROVEN, with one explicit nuance.**

The pilot's success criterion was that the
`tests/AccessHarvester.CharacterizationTests/AddAnOrder/` tests
(and their UpdateAnOrder/UpdateAProduct/UpdateAnEmployee siblings)
pass without modification against the Blazor app's wiring. **All
111 characterization tests pass unmodified.**

The nuance: **Stop Rule #1 fired in Session 2.** The
characterization-test workflow classes (`OrderCreationWorkflow`,
`OrderLineItemWorkflow`, `InventoryAllocator`) are `private sealed`
inside their respective test files and could not be shared with
the Blazor project as written. The orchestrator surfaced four
resolution options to the human; the human chose **Option D**:
clarify the success criterion to *"invariant correspondence"* —
the Blazor service layer authors its own domain services that
honor the same package invariants, and the unmodified
characterization tests continue to validate the package shape
against their own private fixtures.

This is a real strategic refinement of what "the package shape
proves out" means:

- **Letter-perfect interpretation** ("the test files survive
  unmodified"): satisfied. No characterization-test file was
  edited.
- **Deeper interpretation** ("Blazor + tests share the workflow
  domain code"): NOT satisfied. The Blazor service layer
  duplicates the invariant logic in its own domain services.

The duplication is a deliberate choice for letter-perfect test
preservation. Whether to revisit and share workflow code is an
open question for a future set; the Blazor pilot did not block on
it. This nuance is documented in the pilot's change log.

**Outputs delivered:**

- **Single ASP.NET Core 10.0 host** at
  `output-tests/blazor-add-an-order/` with three namespaces
  (`BlazorAddAnOrder.Ui`, `BlazorAddAnOrder.Services`,
  `BlazorAddAnOrder.Contracts`).
- **Add-an-Order vertical slice** reachable at `/orders/new` —
  master-detail entry form, customer/product lookup, line items
  with inventory allocation, save with validation, persistence to
  SQLite.
- **All 111 characterization tests pass unmodified** (count grew
  from 107 in Phase 1 to 111 across the suite, reflecting test
  refactoring during Phase 2).
- **Layer-separation discipline holds** — `BlazorAddAnOrder.Ui`
  does not directly reference `BlazorAddAnOrder.Services` types;
  all cross-layer communication goes through the named
  `HttpClient` and the shared `Contracts` DTOs.

**Stop rules:** Rule #1 fired and was resolved as Option D
(documented above). No other stop rules triggered.

### Phase 3 — Next-Set Selection (RESOLVED 2026-05-17)

Phase 2's success — both letter and Option-D-nuanced — opened a
menu of next moves. The 2026-05-17 cross-provider strategic
review (Gemini 3.1 Pro + GPT-5.5; see
`docs/strategic-reviews/2026-05-17-platform-tooling-fit/`) resolved
the menu. Both reviewers independently concluded that Phases 1
and 2 had already proved what they needed to prove, and that
further Access-to-Blazor *application implementation* work
belongs in a future migration repo rather than continuing here.
The harvester repo should shift from "start the next promising
session" to "finish or freeze."

The candidate statuses below reflect that resolution.

**3a. Implement the three remaining pilot workflows in Blazor.**
**DEFERRED (2026-05-17) to the future migration repo.** Session
1 of `blazor-update-workflows` started on 2026-05-17 and was
paused mid-Step-4a when the orchestrator noticed
`../dabbler-platform/crud-scaffolding` had shipped CRUD templates
two days earlier (after this strategy doc's last revision). The
cross-provider review concluded that the platform's CRUD
templates are correctly scoped to single-entity admin slices and
*not* a fit for workflow-driven update flows; further hand-
authored Blazor work in this repo would dilute the harvester's
scope. The partial Session 1 work (persistence extensions,
`ShipperEntity`, `IShipperCatalog`) was dropped from the stash
on 2026-05-17. The spec at `docs/session-sets/blazor-update-
workflows/spec.md` is preserved as input the future migration
repo can adapt; this repo will not execute the set.

**3b. Add UAT and E2E coverage to the Blazor pilot.**
**DEFERRED to the future migration repo.** UAT/E2E is the right
gate for Blazor application work; with 3a deferred, 3b deferred
naturally with it.

**3c. Validate generalization on a non-Northwind database.**
**COMPLETE 2026-05-15.** `generalization-validation-on-non-
northwind` shipped the HHC `Add a Client` package + falsification
tests; verdict `generalizes-cleanly` resolves Open Validation
#2. See that set's `change-log.md` and `generalization-findings.md`.

**3d. Resolve open questions with a domain reviewer.**
**STILL OPEN** but lower priority. The 18 open questions from
Phase 1 are not blocking; resolution naturally happens when the
future migration repo consumes the packages and a domain
reviewer adjudicates per-question.

**3e. Substitute the custom Blazor component library.**
**DEFERRED to the future migration repo.** The 2026-05-17
strategic review concluded that swapping component libraries in
this repo's pilot host is not the right place to test platform-
library fit. The future migration repo should consume the
platform fresh.

**3f. Revisit shared workflow code.** **DEFERRED to the future
migration repo.** With 3a deferred, the Option D duplication
question doesn't recur in this repo.

**3g. Harden the layer boundary to a real two-process
deployment.** **DEFERRED to the future migration repo.** Layer
hardening only matters when application work continues.

**Net Phase 3 outcome:** 3c shipped (one open validation
resolved); 3a / 3b / 3e / 3f / 3g deferred to the future
migration repo; 3d remains open as advisory. The harvester
pilot host at `output-tests/blazor-add-an-order/` is preserved
as-is for the future migration repo to reference, but no
further code will land in it from this repo.

### Phase 4 — Mechanical Harvester Finish Line (ACTIVE 2026-05-17)

> Introduced 2026-05-17 per the cross-provider strategic review.
> Both reviewers (Gemini 3.1 Pro and GPT-5.5) recommended
> explicitly naming a finish line for the harvester proper
> distinct from the deferred-to-future-migration-repo Blazor
> application work.

**Goal:** the mechanical harvester (parser, extractor, enrichers,
output schemas, CLI) can produce stable, documented output for
the canonical fixtures with parser/extraction tests green and
explicit `UnresolvedItem` records for constructs it cannot
process.

**Planned-sequence actuals (planner shipped 2026-05-17):**
`remaining-work-planner` (revised 2026-05-17 — scoped to the
harvester CLI; explicitly excludes migration-app implementation
work) audited the remaining mechanical-harvester gaps against the
seven Phase 4 criteria and produced a named sequence of follow-on
session sets. The full plan lives at
[`docs/session-sets/ROADMAP.md`](../session-sets/ROADMAP.md);
the per-feature prerequisite analysis lives at
[`docs/session-sets/remaining-work-planner/remaining-work-tree.md`](../session-sets/remaining-work-planner/remaining-work-tree.md).

Phase 4 closes in **5 session sets, 13 sessions total**, at a
routed-cost rollup of **~$1.00–$3.20**:

1. [`harvester-finish-canonical-parse-success-report`](../session-sets/harvester-finish-canonical-parse-success-report/spec.md)
   (F1, 2 sessions, small) — Criterion #1 gate.
2. [`harvester-finish-canonical-golden-output`](../session-sets/harvester-finish-canonical-golden-output/spec.md)
   (F2, 4 sessions, medium) — Criteria #3 + #5 (UnresolvedItems
   wiring folded in). Central deliverable.
3. [`harvester-finish-cli-e2e-test-audit`](../session-sets/harvester-finish-cli-e2e-test-audit/spec.md)
   (F3, 2 sessions, small) — Criterion #4 gate.
4. [`harvester-finish-operations-and-interpretation-docs`](../session-sets/harvester-finish-operations-and-interpretation-docs/spec.md)
   (F4, 3 sessions, small-to-medium) — Criterion #6
   (downstream-AI-consumer-facing portion).
5. [`harvester-finish-cli-packaging`](../session-sets/harvester-finish-cli-packaging/spec.md)
   (F5, 2 sessions, small) — Criterion #7. Final Phase 4 gate.

Criterion #2 (schema finality) is **COVERED** by the already-shipped
[`schema-finalization-and-docs`](../session-sets/schema-finalization-and-docs/change-log.md);
no F-set is needed. The five Frozen items below were each re-examined
by the gap audit and confirmed frozen — no Phase 4 candidate required
reauthorization.

F1 and F3's "black-box CLI tests" feature are parallel-eligible from
the start; F3 and F4 are parallel-eligible after F2 closes. See the
ROADMAP's "Phase 4" section for the parallelism advisory.

**Out of scope for this repo:**

- New Blazor application slices or workflows.
- Component-library swaps in the pilot host.
- Cross-repo discovery work beyond refreshing the existing
  `docs/cross-repo/` snapshot when the future migration repo
  actively consumes it.
- Production IR generators beyond the `lowdefy-ir-foundation`
  probe (reclassified to "probe complete; production generator
  not authorized" — see Frozen list below).

**Done when:** the harvester's `output-tests/` (or equivalent)
contains golden output for the two canonical fixtures
(`NorthwindDevEdition.accdb`, `HomeHealthCare.accdb`);
the parser surfaces explicit `UnresolvedItem` records for any
construct it cannot handle; the CLI is documented; and the test
suite is green for the mechanical pipeline (parser, resolver,
enrichers, IR serialization). `MiniComplex.accdb` was resolved
out of Phase 4 by `remaining-work-planner` Session 1 Open Question
1 (the fixture is excluded from automated coverage pending an
error-audit pass per `project-guidance.md` → Canonical Test
Fixtures); it can stay in `samples/` for ad-hoc inspection but is
not a Phase 4 gate.

### Frozen Unless Reauthorized (2026-05-17)

The following work-streams are explicitly frozen as of the
2026-05-17 strategic review. Reauthorization requires either
the future migration repo's needs to surface them, or an
explicit human decision to reopen.

- **Production IR generator** beyond
  `tools/AccessHarvester.IrCodegen/` probe scope. Probe verdict
  recorded; runtime usefulness not yet demonstrated by a
  consuming workflow.
- **Further `dabbler-platform-discovery` refreshes** beyond the
  existing dated snapshot at platform commit `aa77020e1ca0`
  (2026-05-16). Refresh only when the future migration repo
  actually consumes the platform.
- **CRUD-scaffolder expansion to cover workflow-driven flows.**
  Both reviewers explicitly recommended against reshaping the
  platform's CRUD templates around Access migration. The
  recommendation lives in
  `docs/strategic-reviews/2026-05-17-platform-tooling-fit/`.
- **Additional Northwind workflows in this repo beyond
  `blazor-add-an-order`.** The four-workflow pilot proved the
  package shape; running more workflows here is incremental
  scale proof, not new strategic ground.
- **Old stale enrichment sessions** — none currently in
  flight after the 2026-05-17 housekeeping pass; any reopen
  requires a fresh spec scoped to a specific harvester gap.

### Recommendations for the future migration repo

Captured here as input for whichever orchestrator scopes that
repo. Authoritative source is
`docs/strategic-reviews/2026-05-17-platform-tooling-fit/`.

- **Inherit the workflow-package shape** from Phase 1's four
  packages and `_template.md`.
- **Consume `docs/cross-repo/access-to-platform-migration-map.md`**
  fresh — the snapshot is dated 2026-05-16; verify against
  current platform source before relying on specific component
  names.
- **Use the platform's CRUD templates tactically** for the
  single-entity admin/lookup surfaces (shippers, payment
  methods, simple product/employee maintenance). Hand-author
  workflow-driven pages and domain services for the lifecycle
  flows.
- **Inherit the Option D pattern** from
  `output-tests/blazor-add-an-order/` — parallel domain
  services that honor the same invariants as the
  characterization tests; unmodified test files in the
  harvester repo as the validation gate.
- **Add UAT + E2E coverage from the start** rather than
  deferring as Phase 2 did. The deferral was acceptable for the
  pilot; the migration repo should not inherit it.

---

## Contributions of Existing Artifacts

What in the repo (and adjacent repos) feeds the strategy, and what
doesn't. This list is the operationalization of the
"keep / partial / discard" verdict from the strategic review.

### Load-bearing — keep

| Artifact | Contribution |
|---|---|
| `AccessHarvester.Vba.Parser` (ANTLR-driven AST) | Source body extraction; procedure slicing; foundation for all downstream extraction |
| `AccessHarvester.Vba.Resolver` (declarations, scope tree, cross-references, default-member resolution, type inference) | Identifies behavior-bearing procedures; reduces AI misreads of references; supports characterization-test generation |
| `AccessHarvester.Pipeline` form/control/event-binding enrichers | High-value for workflow identification (form ↔ event ↔ procedure mapping) |
| Event-handler classification | Critical for picking which procedures get characterization tests |
| Translation-probe pattern (one AI translates, another peer-reviews and adds falsification tests) | The core engine for producing characterization tests; proven in `vba-symbol-db-path-b-probe` |
| Symbol DB (SQLite) as queryable substrate | Keep IF retrieval stays easy; do NOT keep evolving its schema unless directly required by package authoring |
| The harvester's existing 1600+ tests | Protect the extraction substrate; do not turn into broader platform investment |
| Cross-provider session verification (Gemini Pro per-session) | Mandatory quality gate, preserved across all phases |

### Partial — keep where they directly feed the four package outputs

A harvester component is load-bearing only if it helps produce one
of: a requirement, a characterization test, a reference evidence
item, or an ambiguity log entry. Components that pass this filter
stay; components that don't, freeze.

| Artifact | Status | Note |
|---|---|---|
| Query abstraction | Partial | Keep where it identifies entities, joins, filters, business rules; do not chase complete semantic reconstruction |
| Navigation graphs | Partial | Useful for workflow prioritization; not worth refining deeply |
| Type inference | Partial | Useful where it materially improves rule extraction or test synthesis; do not over-invest |

### Throw-away — discard or freeze

| Artifact | Why it's throw-away |
|---|---|
| AI-interpretation columns from `vba-symbol-db` (`Purpose`, `Justification`, `Confidence`, etc.) | The probe was useful; the column-population output is not. Regenerate workflow-relevant interpretation per workflow on demand. Do not write back to the symbol DB. |
| Translated C# procedures themselves (from `path-b-probe` and forward) | Throwaway scaffolding. Tests are the deliverable. Do not accumulate translated procedures into a pseudo-port. |
| Generalized translator infrastructure beyond the current probe | Not aligned with the strategy. Stays buried per the second cross-provider review. |
| Mechanical enrichers with no path to requirements / tests / reference / ambiguity log | Stop expanding. The four-output filter is the test. |

### Adjacent — outside this repo, contributes when ready

| Artifact | Contribution | Status |
|---|---|---|
| Custom Blazor component library | Target patterns for AI-built Blazor implementations; defines master-detail, role taxonomy, etc. | In active development. Not blocking this repo's pilot. |
| VS Code extension for AI orchestration | Outsource-last mode aligns with the strategy's cost-consciousness pattern | In active development. Not blocking this repo's pilot. |
| UAT DSL (`../dabbler-uat-dsl`) | Candidate format for source operationalization in workflow packages | Decision deferred to pilot Session 1 |

---

## Open Validations — Status

Updated post-Phase 3-reset (2026-05-17). Items #1, #2, #3, #5,
#6, #7 (partial), #8, #9 are resolved. Items #4 and #10 are
re-classified from OPEN to DEFERRED-TO-FUTURE-MIGRATION-REPO —
they remain valid questions but will be answered by that repo's
work rather than additional sessions here.

| # | Question | Status | Resolution / next opportunity |
|---|---|---|---|
| 1 | Does the workflow-package shape disambiguate enough for AI-driven Blazor implementation? | **RESOLVED — yes, with one nuance** | Phase 2 shipped Add an Order end-to-end with all 111 characterization tests passing unmodified. Nuance: the "shared workflow code" interpretation didn't hold (private sealed test types blocked it); the "invariant correspondence" interpretation did (Blazor service layer authors parallel domain logic that honors the same invariants). See Phase 2 above. |
| 2 | Does the package shape generalize to non-Northwind databases? | **RESOLVED 2026-05-15 — yes** | `generalization-validation-on-non-northwind` shipped the HHC `Add a Client` package + falsification tests; verdict `generalizes-cleanly`. See that set's `generalization-findings.md`. |
| 3 | Is four-workflow pilot diversity enough, or do we need more variety (queries, macros, reports)? | **RESOLVED — yes, for Northwind** | Cross-workflow audit confirmed the four packages form an internally coherent dataset. Diversity for non-Northwind databases is a separate question (#2). |
| 4 | Will the Blazor library support all the patterns these packages need? | **DEFERRED to future migration repo** | Phase 3e deferred per 2026-05-17 strategic review. The platform's CRUD scaffolding shipped 2026-05-16 with single-entity scope; library-pattern fit for workflow-driven flows will be tested by the future migration repo, not by additional Phase-3 sessions here. |
| 5 | Should source operationalization use UAT DSL or structured prose? | **RESOLVED — hybrid** | Phase 1 used structured prose for all four (default per Decision 1). Audit found prose works for 3 of 4; Update an Order's state machine would benefit from UAT DSL. Recommendation for next set: prose for most workflows, DSL for state-machine workflows. |
| 6 | Should component/role hints be generic-now or library-specific? | **RESOLVED — generic was right** | Phase 1 used generic-now (default per Decision 2). The hints worked fine for Phase 2 with MudBlazor. Library-specific upgrade is part of Phase 3e. |
| 7 | Does the Hybrid procedure-level decision hold for non-event-handler procedures (queries, macros, class-module methods)? | **PARTIALLY RESOLVED** | The four pilot workflows exercised event handlers, save validators, calculation logic, state machines, and cascading-effect rules. They did NOT exercise macros, queries-as-first-class workflows, or report-bound logic. Hybrid holds for what was tested; report/macro/query-heavy workflows still untested. |
| 8 | Does the in-IDE orchestrator pair (Codex + Claude) hold up for package authoring, not just translation? | **RESOLVED — yes** | Phase 1 Sessions 2-3 (Codex drafted Add an Order; Claude peer-reviewed) produced a clean package + falsification tests. Pattern works for synthesis tasks beyond translation. |
| 9 | What's the actual per-package authoring time? | **RESOLVED — close to estimate** | Add an Order: 2 sessions (draft + peer review); the three update workflows: 1 session each; setup/audit/handoff: 1 session each. Total 7 sessions matched the spec exactly. |
| 10 | At what Northwind workflow count does the package-authoring overhead exceed the build savings? | **DEFERRED to future migration repo** | The scale calibration only matters once full-application implementation is underway. The future migration repo's first one or two workflows will surface the right answer; running additional Phase-3a workflows in this repo for the sole purpose of answering this validation isn't worth the scope cost (per the 2026-05-17 strategic review). |

**Net (post-2026-05-17 reset):** Validations #1, #2, #3, #5, #6,
#7 (partial), #8, and #9 are resolved. Validations #4 and #10
are formally deferred to the future migration repo; that repo's
work will answer them in passing. No open validation now gates
work in this repo. The harvester's remaining objective is the
mechanical finish line (Phase 4) — not additional pilot
extension.

---

## Cost Posture

- **Routed cost target across all phases:** under $5 total through
  the end of Phase 2. Phase 3 cost depends on scale and is not
  estimated here.
- **Routing pattern:** outsource-last. Per-session cross-provider
  verification (Gemini Pro) is the only routed cost; everything
  else runs in-IDE under subscription windows (Anthropic Max for
  Claude; GitHub Copilot for Codex).
- **No routed analysis** for `ai-assignment.md` authoring,
  next-session recommendations, change logs, or strategic
  documents like this one. All in-conversation per the
  outsource-last pattern established in `vba-symbol-db-path-b-probe`.
- This posture matches the in-progress VS Code extension's
  outsource-last mode and the cost-consciousness constraint
  established during the strategic-review round on 2026-04-30.

The historical cost reference: `vba-symbol-db` cost ~$24 routed
across 7 sessions (the calibration arc that proved the routed-rigor
model wasn't paying off). The path-b-probe cost ~$0.23 routed
across 3 sessions producing a defensible direction verdict.
Phase 1 (workflow-package-pilot) targets ~$0.35 routed across 7
sessions. The order-of-magnitude cost reduction per session is
the in-IDE-pair pattern earning its keep.

---

## Landing Plan — Actuals

The original target was one week for Phase 1 alone. **Phase 1 AND
Phase 2 both landed within one day** (2026-04-30).

| Originally planned | Actuals |
|---|---|
| Day 1: Phase 1 Session 1 (setup) | Done same day |
| Day 2: Phase 1 Sessions 2-3 (Add an Order draft + peer review) | Done same day |
| Day 3: Phase 1 Sessions 4-5 (Update an Order + Update a Product) | Done same day |
| Day 4: Phase 1 Session 6 (Update an Employee) | Done same day |
| Day 5: Phase 1 Session 7 (audit + handoff + change log) | Done same day |
| Days 6-7: Buffer / Phase 2 spec drafting | Phase 2 specced AND fully shipped same day |

**Net pace: ~10 sessions across both pilots in 24 hours.**
Vindicates the human's calibration note that AI engines
systematically overestimate complex-task duration. The 7-day plan
was off by 6 days on the long side.

**Adjacent investments status:**

- **Custom Blazor component library:** still in progress as of
  2026-04-30 close-of-day. Phase 2 used generic MudBlazor as
  placeholder per the original plan. Substitution is a Phase 3
  candidate (3e).
- **VS Code extension for AI orchestration:** still in progress.
  The pilot work followed the outsource-last pattern manually
  (no routed analysis except per-session verification), which
  matches what the extension's outsource-last mode will eventually
  automate.

The original strategy doc's day-by-day landing plan is preserved
above as a calibration data point for future planning. The actual
ship pace is the more useful reference going forward: a single
session set the size of `workflow-package-pilot` (7 sessions of
mixed complexity) is plausibly a one-day effort with two
orchestrators alternating, not a one-week effort.

---

## What the Next Strategic-Review Session Should Look At

A separate orchestrator session reads this strategy alongside the
session-set artifacts to advise on next steps. To make that
session productive, the materials it should have in front of it:

1. **This document** (`docs/planning/strategy.md`) — including
   the Revisions section at the bottom.
2. **The completed Phase 1 pilot:**
   - [`docs/session-sets/workflow-package-pilot/spec.md`](../session-sets/workflow-package-pilot/spec.md)
   - [`docs/session-sets/workflow-package-pilot/change-log.md`](../session-sets/workflow-package-pilot/change-log.md)
   - [`docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`](../session-sets/workflow-package-pilot/cross-workflow-audit.md)
   - [`docs/session-sets/workflow-package-pilot/handoff.md`](../session-sets/workflow-package-pilot/handoff.md)
   - The four packages under `packages/` and the
     characterization tests under
     `tests/AccessHarvester.CharacterizationTests/`
3. **The completed Phase 2 pilot:**
   - [`docs/session-sets/blazor-add-an-order/spec.md`](../session-sets/blazor-add-an-order/spec.md)
   - [`docs/session-sets/blazor-add-an-order/change-log.md`](../session-sets/blazor-add-an-order/change-log.md)
   - The shipped slice at `output-tests/blazor-add-an-order/`
4. **The strategic inflections that produced the current
   direction:**
   - [`docs/session-sets/vba-symbol-db/`](../session-sets/vba-symbol-db/)
     (`verdict.md` and `change-log.md`)
   - [`docs/session-sets/vba-symbol-db-path-b-probe/`](../session-sets/vba-symbol-db-path-b-probe/)
     (`decision.md`, `decision-plain-english.md`, `change-log.md`)
5. **Cross-provider review packages from the strategic review**
   that produced this strategy. Originally written to
   `tmp/feedback/`; if `tmp/` has been cleaned since then, the
   artifacts are gone. **A real action item for the next session
   is to decide whether these should be promoted to a permanent
   location** (e.g.,
   `docs/strategic-reviews/2026-04-30-workflow-packages/`) so
   future revisions of this strategy can cite them. Filenames
   were:
   - `path-b-foundations-proposal.md` + responses from Gemini Pro
     and GPT-5.4 (the rejected JIT-translator proposal)
   - `strategy-question.md` + responses from Gemini Pro and
     GPT-5.4 (the proposal that produced the workflow-package
     direction)

**Specific questions worth asking the next session:**

- Of the seven Phase 3 candidates (3a-3g), which is the right
  next set? The advisory sequencing puts 3a first; is that
  defensible?
- Is the "Option D / invariant correspondence" resolution from
  Phase 2 a long-term acceptable shape, or should 3f (revisit
  shared workflow code) be elevated above its current low
  priority?
- The all-`confirmed` confidence-tag distribution from Phase 1
  — is that a structural bias to fix in the next package set, or
  is the open-questions log doing the right work?
- The pilot did NOT exercise macros, queries-as-first-class
  workflows, or report-bound logic (open validation #7 partial
  resolution). Should one of the Phase 3 candidates explicitly
  cover one of those shapes, or is event-handler-shaped work the
  realistic 80% of Northwind's surface area?
- Anything missing from the strategy doc that the next session
  should raise before any Phase 3 work starts?

The next session is NOT authorized to start any session set — its
job is to advise on next steps, not to execute. Authorization
remains with the human.

---

## Document Maintenance

This strategy document is updated when:

1. A pilot phase lands (update the relevant section with actuals).
2. A pivot trigger fires and the strategy changes direction.
3. A new strategic-review round produces a recommendation that
   shifts the plan.
4. An open validation (in the table above) gets resolved.

The document is NOT updated for tactical changes within a session
set (those go in the session set's own change log) or for routine
session-by-session progress (that lives in `activity-log.json`).

A `## Revisions` section is added to the bottom of the document
when the first revision lands, noting date / what changed / why.

---

## Revisions

### 2026-04-30 (same-day) — Phase 1 + Phase 2 actuals

Both `workflow-package-pilot` (7 sessions) and
`blazor-add-an-order` (3 sessions) landed within hours of this
document's original draft. Updates:

- **Phase 1 section:** rewritten from "active session set" to
  "COMPLETE" with full actuals (cost, outputs, key
  cross-workflow findings, the lesson promoted to Convention).
  Session 6's $2.83 cost outlier called out (Claude Opus
  4-round verification per session-local human direction; the
  other 6 sessions stayed close to the original ~$0.05-each
  estimate).
- **Phase 2 section:** rewritten from "next session set
  (provisional)" to "COMPLETE" with the empirical proof of the
  package-shape success criterion AND the explicit Option D
  nuance (invariant correspondence, not shared workflow code).
  Architectural decisions documented: single-host ASP.NET Core
  10.0, MudBlazor placeholder, EF Core + SQLite, namespace-only
  layer separation.
- **Phase 3 section:** rewritten from binary "Scale or Pivot"
  to seven concrete next-set candidates (3a-3g) with an
  advisory sequencing recommendation. The Phase 2 actuals
  opened up a structured next-step menu rather than a single
  decision point.
- **Open Validations table:** updated status for all 10 items.
  Seven resolved (1, 3, 5, 6, 7 partially, 8, 9), three open
  (2, 4, 10) and mapped to specific Phase 3 candidates.
- **Landing plan section:** replaced the Day 1-7 forward plan
  with the day-by-day actuals showing both pilots landed in
  one day. Original 7-day plan preserved as a calibration
  reference; new pace is the better forward planning input.
- **Header status line:** updated date and added pointer to
  this Revisions section.

No content was deleted from the doc — earlier framing is
preserved where it remains useful (the journey-so-far inflections,
definitions, the contributions table, cost posture, and the
"what the next strategic-review session should look at" pointer
list). All changes are additive or rewrite-with-equivalent-intent.

The contribution table itself is now empirically validated rather
than projected: the parser, resolver, enrichers, translation-probe
pattern, and symbol DB all proved load-bearing; the AI-interpretation
columns from `vba-symbol-db` remained throw-away as predicted.

Next planned revision: when Phase 3's first selected candidate
lands. If the next selected work happens to be 3a (the three
update workflows in Blazor), this doc gets updated with that
actuals block. If it's 3c (non-Northwind generalization), the
resolution of open validation #2 lands. Etc.

### 2026-05-17 — Phase 3 reset; Phase 4 finish line declared

Three Phase-3 candidates and one new probe set landed (or
attempted) since the 2026-04-30 revision. The 2026-05-17
cross-provider strategic review (Gemini 3.1 Pro + GPT-5.5) then
adjudicated the remaining menu and recommended an explicit
stop-or-finish reset rather than continuing to start new
candidates by default. This revision lands that reset.

**Sets that landed between 2026-04-30 and 2026-05-17:**

- **`generalization-validation-on-non-northwind`** — COMPLETE
  2026-05-15. 2 sessions. Shipped the HHC `Add a Client` package
  + falsification tests; verdict `generalizes-cleanly`. Resolved
  Open Validation #2. Was originally Phase 3c.
- **`lowdefy-ir-foundation`** — COMPLETE 2026-05-17. 2 sessions.
  Probed Lowdefy-as-IR shape; produced a ModalDetailForm JSON
  Schema, a hand-authored Northwind sample, and a standalone
  `tools/AccessHarvester.IrCodegen/` console-exe slice generator
  whose generated Blazor pair compiled clean into the pilot
  csproj. Probe verdict only — production generator not
  authorized (see Frozen list above).
- **`dabbler-platform-discovery`** — COMPLETE 2026-05-17. 3
  sessions. Produced four authoritative cross-repo docs at
  `docs/cross-repo/` pinned to platform commit `aa77020e1ca0`
  (2026-05-16). Treat as a dated snapshot; re-check current
  platform source before relying on specific component names.

**`blazor-update-workflows` (Phase 3a) — paused and dropped:**

- Session 1 started 2026-05-17 with Claude Opus 4.7 @ effort=high.
  Mid-Step-4a the orchestrator noticed that
  `../dabbler-platform/crud-scaffolding` had shipped two CRUD
  templates (`dabbler-crud-service`, `dabbler-crud-page`) on
  2026-05-16 — after this strategy doc's last revision. The
  orchestrator stopped and surfaced the inventory miss before
  continuing.
- Cross-provider strategic review (Codex / GPT-5.5 and Gemini
  3.1 Pro, both driven by the human via in-IDE access to both
  repos) concluded that the platform CRUD templates are
  correctly scoped to single-entity admin slices and **not** a
  fit for workflow-driven update flows. Both reviewers
  independently recommended deferring further Blazor application
  work in this repo to the future migration repo.
- The partial Session 1 work (persistence extensions for
  Invoice/Shipping/Payment fields on the Order entity, a new
  `ShipperEntity`, an `IShipperCatalog` interface) was dropped
  from the stash on 2026-05-17. The spec at
  `docs/session-sets/blazor-update-workflows/spec.md` is
  preserved as input the future migration repo can adapt.
- Net cost of the aborted session: $0.40 (the routed
  `ai-assignment.md` analysis call, which per the orchestrator-
  matches-target convention should have been done in-conversation
  — noted as a deviation for the next Step 9 review).

**Strategic-review outputs preserved:**
`docs/strategic-reviews/2026-05-17-platform-tooling-fit/`
contains the raw Gemini 3.1 Pro and GPT-5.5 recommendations
plus an index `README.md`. Both reviews + the index are the
audit trail for this revision.

**Updates landed in this revision:**

- **Header status line:** updated to reference the 2026-05-17
  Phase 3 reset and the Phase 4 finish-line declaration.
- **Phase 3 section:** each candidate (3a-3g) updated with its
  current status. 3c marked COMPLETE; 3a / 3b / 3e / 3f / 3g
  marked DEFERRED to future migration repo; 3d remains open as
  advisory.
- **Phase 4 (new):** "Mechanical Harvester Finish Line"
  introduced as the active phase. Names the
  `remaining-work-planner` set as the executable next step
  once its spec is revised. Defines "done" for the harvester
  proper.
- **Frozen Unless Reauthorized (new):** explicit list of
  work-streams frozen as of 2026-05-17. Production IR
  generator, further platform-discovery refreshes, CRUD-
  scaffolder expansion around Access migration, additional
  Northwind workflows in this repo, old stale enrichment
  sessions.
- **Recommendations for the future migration repo (new):**
  captured as a forward pointer so whichever orchestrator
  scopes that repo can inherit the workflow-package shape, the
  Option D pattern, and tactical use of the platform's CRUD
  templates.
- **Open Validations table:** #2 marked RESOLVED 2026-05-15;
  #4 and #10 marked DEFERRED to future migration repo. Net
  comment rewritten — no open validation now gates work in
  this repo.

**No content was deleted** from the prior revision. The Phase 1
and Phase 2 sections, the inflections, the definitions, the
contributions table, and the cost posture all remain intact;
this revision is additive plus per-candidate status updates.

**Concurrent housekeeping commit:** this revision lands with
- `enrichment-functions-navigation-review/session-state.json`
  flipped to `complete` (the change-log was committed on
  2026-04-28; only the state file was stale);
- `lowdefy-ir-foundation/change-log.md` authored from the rich
  notes already in that set's `session-state.json`;
- `remaining-work-planner/spec.md` revised to scope the planner
  to mechanical harvester gaps only;
- the two strategic-review reports filed under
  `docs/strategic-reviews/2026-05-17-platform-tooling-fit/`.

Next planned revision: see the 2026-05-17 (planner-shipped)
revision entry below.

### 2026-05-17 (same-day) — `remaining-work-planner` shipped Phase 4 sequence

The `remaining-work-planner` set completed all three sessions
(gap audit → stub specs + tree → ROADMAP) and produced a named,
sequenced execution plan for Phase 4. This revision lands the
planner's output into the Phase 4 section above.

**Outputs the planner produced:**

- [`docs/session-sets/remaining-work-planner/gap-audit.md`](../session-sets/remaining-work-planner/gap-audit.md)
  — criterion-by-criterion classification (1 COVERED, 5 GAPs
  small/fold-in, 1 GAP central, 5 frozen items confirmed
  frozen). Three open questions surfaced and resolved within
  Session 1's close (MiniComplex out; macros/property-sheet
  hybrid inventory-driven; packaging shape self-contained
  binary primary).
- Five stub specs at
  `docs/session-sets/harvester-finish-*/spec.md` (F1–F5),
  authored at acceptance-criteria level per the
  Session-1-vs-Session-2 boundary discipline.
- [`docs/session-sets/remaining-work-planner/remaining-work-tree.md`](../session-sets/remaining-work-planner/remaining-work-tree.md)
  — row-per-set index + per-feature prerequisite table + GAP #5
  fold-in rationale. Authoritative for Phase 4 dependencies and
  execution order.
- [`docs/session-sets/ROADMAP.md`](../session-sets/ROADMAP.md)
  — top-level pointer for every session set in the project
  (already-complete, Phase 4 planned, deferred/cancelled/retired).
  The artifact a fresh orchestrator reads first to know what to
  start.

**Phase 4 section updated above** with the planned-sequence
actuals: F1 → F5 ordering, the parallel-eligibility advisory, the
~$1.00–$3.20 routed-cost rollup, and the link to the ROADMAP.

**Frozen list unchanged.** The audit considered each frozen item
against Phase 4 needs and confirmed all five should remain frozen.
No reauthorization recommended.

**No content was deleted** from prior revisions. The 2026-05-17
(initial) Phase 3 reset entry above remains intact; this entry is
additive and updates only the Phase 4 section's planned-sequence
block.

Next planned revision: when the first Phase 4 set (F1) ships.

### Next planned revision

When the first Phase 4 set ships (F1
`harvester-finish-canonical-parse-success-report`), update Phase
4 with its actuals and tick the F1 row on the ROADMAP.
