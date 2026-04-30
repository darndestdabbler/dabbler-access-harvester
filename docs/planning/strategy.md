# Strategy — Building the Blazor/C# Replacement of Access Applications

> **Audience:** Any orchestrator picking up this work. Read this
> alongside `project-guidance.md`, `lessons-learned.md`, and
> `project-status.md`. The session-set specs and change-logs under
> `docs/session-sets/` are the operational answer to this strategy;
> this document is the strategic frame they sit inside.
> **Status:** Approved direction as of 2026-04-30; revised
> 2026-04-30 with Phase 1 and Phase 2 actuals (both phases landed
> the same day they were specced — see Revisions at the bottom).
> **Landing window:** The original target was one week for Phase 1
> alone; Phases 1 AND 2 both landed within one day, well ahead of
> plan. Phase 3 is now concretely scoped with selection pending.

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

### Phase 3 — Next-Set Selection (concretely scoped, selection pending)

Phase 2's success — both letter and Option-D-nuanced — opens a
small set of well-defined next moves rather than a binary
scale/pivot decision. The next-set author picks from this list
against the master plan / open backlog (or against the human's
direction if they want to nominate directly).

**3a. Implement the three remaining pilot workflows in Blazor.**
Spec a follow-up set (suggested slug
`blazor-update-workflows`) that ships Update an Order, Update a
Product, and Update an Employee against the same Blazor host
established in Phase 2. Reuses everything already built:
namespace structure, HttpClient boundary, EF Core + SQLite
persistence, MudBlazor patterns, the characterization-test wiring.
Per the workflow-package handoff: "Once Add an Order ships, the
three update workflows reuse most of its infrastructure." Likely
the cheapest / highest-value next move if the team wants Northwind
to feel like a real working application end-to-end.

**3b. Add UAT and E2E coverage to the Blazor pilot.** Phase 2
explicitly deferred UAT/E2E (`requiresUAT: false`,
`requiresE2E: false`); the spec flagged this as an explicit
deviation from the When-UAT-Is-Required heuristic and noted it
should be revisited at the Step 9 review. The spec's own
`What "Success" Looks Like` section says the human can hand-test
the slice; UAT/E2E formalizes that. Pairs naturally with 3a — a
single later set can add UAT to all four workflows once they're
shipped, or each workflow's set can add its own UAT inline.

**3c. Validate generalization on a non-Northwind database.**
Run the workflow-package shape against one or two workflows from
`MiniComplex.accdb` or `HomeHealthCare.accdb`. Tests open
validation #2 (does the package shape generalize?). If it
generalizes cleanly, the Hybrid strategy holds for any Access
app; if it doesn't, the package template needs revision before
scaling beyond Northwind. Probably worth doing before 3a if
generalization risk is a concern; probably worth doing after 3a
if proving the full Northwind end-to-end is the higher priority.

**3d. Resolve open questions with a domain reviewer.** The four
packages collectively carry 18 explicit open questions (see the
handoff document's Section 4.1). Most are not blockers; some are
genuine ambiguities about business intent that only a domain
expert can settle. A short session set could batch these
questions, surface them to a domain reviewer, and update the
packages with the resolutions.

**3e. Substitute the custom Blazor component library.** When
the in-progress Blazor library lands, run a small set to swap
generic MudBlazor for the library's components in Phase 2's
Add an Order slice. Tests open validation #4 (does the library
support all the patterns the packages need?). Probably best to
do this AFTER 3a so the library swap covers all four workflows
in one pass.

**3f. Revisit shared workflow code.** Phase 2's Option D
resolution leaves invariant logic duplicated between the
characterization tests and the Blazor service layer. If the
duplication becomes a maintenance pain point as 3a adds more
workflows, a small set could promote the workflow models to a
shared library both projects reference. Lower priority unless
duplication actually hurts.

**3g. Harden the layer boundary to a real two-process
deployment.** Phase 2's HTTP boundary is in-process for testing
convenience; the spec acknowledged this as a later refactor.
This is real engineering work (separate hosts, deployment
configuration, possibly auth between them) and is probably the
LAST item on this list — only worth doing once the application
is otherwise close to production-shaped.

**Sequencing recommendation (advisory, not binding):** 3a → 3b
(or 3c) → 3e → 3d → 3f → 3g. The first three are about proving
the strategy at scale; the last three are about hardening once
proof is established.

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

Updated post-Phase 2. Items #1, #3, #5, #6, #7, #8, #9 are
resolved; #2, #4, #10 remain open and map to specific Phase 3
candidates.

| # | Question | Status | Resolution / next opportunity |
|---|---|---|---|
| 1 | Does the workflow-package shape disambiguate enough for AI-driven Blazor implementation? | **RESOLVED — yes, with one nuance** | Phase 2 shipped Add an Order end-to-end with all 111 characterization tests passing unmodified. Nuance: the "shared workflow code" interpretation didn't hold (private sealed test types blocked it); the "invariant correspondence" interpretation did (Blazor service layer authors parallel domain logic that honors the same invariants). See Phase 2 above. |
| 2 | Does the package shape generalize to non-Northwind databases? | OPEN | Phase 3c — `MiniComplex` and `HomeHealthCare` validation |
| 3 | Is four-workflow pilot diversity enough, or do we need more variety (queries, macros, reports)? | **RESOLVED — yes, for Northwind** | Cross-workflow audit confirmed the four packages form an internally coherent dataset. Diversity for non-Northwind databases is a separate question (#2). |
| 4 | Will the Blazor library support all the patterns these packages need? | OPEN | Phase 3e — substitute the library when it lands, see what gaps surface |
| 5 | Should source operationalization use UAT DSL or structured prose? | **RESOLVED — hybrid** | Phase 1 used structured prose for all four (default per Decision 1). Audit found prose works for 3 of 4; Update an Order's state machine would benefit from UAT DSL. Recommendation for next set: prose for most workflows, DSL for state-machine workflows. |
| 6 | Should component/role hints be generic-now or library-specific? | **RESOLVED — generic was right** | Phase 1 used generic-now (default per Decision 2). The hints worked fine for Phase 2 with MudBlazor. Library-specific upgrade is part of Phase 3e. |
| 7 | Does the Hybrid procedure-level decision hold for non-event-handler procedures (queries, macros, class-module methods)? | **PARTIALLY RESOLVED** | The four pilot workflows exercised event handlers, save validators, calculation logic, state machines, and cascading-effect rules. They did NOT exercise macros, queries-as-first-class workflows, or report-bound logic. Hybrid holds for what was tested; report/macro/query-heavy workflows still untested. |
| 8 | Does the in-IDE orchestrator pair (Codex + Claude) hold up for package authoring, not just translation? | **RESOLVED — yes** | Phase 1 Sessions 2-3 (Codex drafted Add an Order; Claude peer-reviewed) produced a clean package + falsification tests. Pattern works for synthesis tasks beyond translation. |
| 9 | What's the actual per-package authoring time? | **RESOLVED — close to estimate** | Add an Order: 2 sessions (draft + peer review); the three update workflows: 1 session each; setup/audit/handoff: 1 session each. Total 7 sessions matched the spec exactly. |
| 10 | At what Northwind workflow count does the package-authoring overhead exceed the build savings? | OPEN | Needs at least one full app's worth of data; only knowable late in Phase 3a |

**Net:** the strategic foundation is much firmer than it was 24
hours ago. The package shape is empirically proven on the richest
behavioral surface (Phase 2). The remaining open validations are
about generalization (#2), tooling integration (#4), and scale
calibration (#10) — all important, none gating the next concrete
step.

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
