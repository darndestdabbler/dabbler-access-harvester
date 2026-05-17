# Workflow Package Pilot Spec

> **Purpose:** Prove a repeatable harvest → workflow-package pipeline
> on four NorthwindDevEdition workflows. Each workflow package
> contains business requirements, characterization tests, source
> operationalization reference, component/role mapping hints, and an
> open-questions log — sufficient for an AI builder to implement a
> Blazor/C# slice that satisfies the same user needs without copying
> Access mechanics.
> **Created:** 2026-04-30
> **Session Set:** `docs/session-sets/workflow-package-pilot/`
> **Prerequisite:** `vba-symbol-db-path-b-probe` complete (decision:
> Hybrid). Harvester parser, resolver, symbol DB, and form/event
> enrichers operational. `samples/NorthwindDevEdition.accdb`
> available.
> **Workflow:** Orchestrator → in-IDE pair (Claude Code + Codex,
> alternating per workflow) → cross-provider session verification
> per session.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: This set produces documentation packages (markdown) plus
> characterization tests (xUnit) under `docs/session-sets/<slug>/`
> and `tests/`. No UI surface; no end-user feature ships from this
> set. Quality bar is build + tests + cross-provider AI verification
> per session, plus a final cross-workflow peer review session that
> audits all four packages together. The Blazor implementation pilot
> that consumes these packages is a downstream session set with its
> own UAT considerations.

---

## Open Decisions Awaiting Human Input (Resolve Before Session 2)

These two decisions shape the per-workflow output. Session 1 surfaces
them; Sessions 2-onward apply whichever choice is made.

### Decision 1: Source-operationalization format

For the source operationalization reference (the "how Access did
it" section), choose one:

- **(a) UAT DSL.** Express the Access click-sequence using the
  formal grammar from `../dabbler-uat-dsl`. Pro: machine-readable,
  structurally parallel to the eventual Blazor UAT DSL the AI
  builder will write. Con: requires reading the dsl repo and
  authoring may take longer per workflow.
- **(b) Structured prose.** Numbered click-sequence in markdown
  (e.g., "1. User opens the Order form. 2. User selects a customer
  from cboCustomerID. 3. ..."). Pro: cheaper per workflow,
  human-readable. Con: not structurally parallel to Blazor target.

**Default if you don't say:** (b) structured prose for the pilot.
Promote to (a) for the next set if the dsl form proves valuable
during the cross-workflow review at the end.

### Decision 2: Component / role mapping hints

For the component/role mapping hints section, choose one:

- **(a) Generic-now.** Write hints in implementation-agnostic terms
  ("fits master-detail with filter+grid+details"; "needs role
  gating for delete operations"). Pro: works whether the Blazor
  component library is ready or not. Con: AI builder has to map
  generic hints to specific library components later.
- **(b) Library-specific.** Wait for the in-progress Blazor library
  before authoring this section, OR author hints that reference
  specific library components if the library is already documented
  enough to do so. Pro: the AI builder gets concrete pointers. Con:
  blocks the pilot on the library; risks library API changes.

**Default if you don't say:** (a) generic-now. The pilot's value
isn't in the hint specificity; it's in proving the package shape.
Library-specific hints can be added in a follow-up pass once the
library lands.

---

## Project Overview

### What this set delivers

1. **Four workflow packages** for NorthwindDevEdition.accdb, one
   per pilot workflow (Add an Order, Update an Order, Update a
   Product, Update an Employee). Each package is a single markdown
   document at
   `docs/session-sets/workflow-package-pilot/packages/<workflow-slug>.md`
   following the seven-section template defined below.

2. **A characterization-test project** at
   `tests/AccessHarvester.CharacterizationTests/` containing xUnit
   tests for the behavior-bearing minority (~10-20%) of VBA
   procedures involved in the four workflows. Tests pin business
   invariants, NOT operationalization mechanics. Translated C#
   scaffolding produced en route is discarded; only the tests
   survive (per `decision.md` and the strategic review's "throwaway
   scaffolding" property).

3. **A cross-workflow audit + handoff document** at
   `docs/session-sets/workflow-package-pilot/handoff.md` produced in
   the final session, summarizing what's ready for the
   downstream Blazor implementation pilot and what's not.

4. **A change-log + Step 9 reorganization review** per the standard
   workflow.

### What this set does NOT do

- Does not implement any Blazor code. The packages are inputs to a
  downstream session set, not implementation work.
- Does not produce workflow packages for the rest of
  NorthwindDevEdition (only the four pilot workflows). If the
  pilot succeeds, scaling to additional workflows is a follow-up
  set; if it fails, scaling is reconsidered.
- Does not touch `MiniComplex.accdb` or `HomeHealthCare.accdb`. Those
  databases validate generalization of the pipeline AFTER the
  Northwind pilot proves the package shape works.
- Does not refactor the harvester. Components like
  `AccessHarvester.Vba.Resolver` and the symbol DB are consumed
  as-is. If a harvester limitation blocks a workflow, surface to
  the human; do not patch the harvester inside this set.
- Does not extend or repopulate the AI-interpretation columns
  (`Confidence*`, `Justification*`, `Interpretation*`) from the
  prior `vba-symbol-db` work. Those are throw-away under the
  Hybrid strategy. Regenerate workflow-relevant interpretation
  on-demand per workflow; do not write back to the symbol DB.
- Does not delete the existing
  `tests/AccessHarvester.TranslationProbe.Tests/` project. That
  project is the path-b-probe artifact; restructuring or
  consolidating it with the new
  `AccessHarvester.CharacterizationTests` project is out of scope
  for this set. Both coexist.

### Why this shape

This pilot is the operational answer to the strategic question
asked of two cross-provider reviewers (Gemini Pro and GPT-5.4) on
2026-04-30. Both reviewers independently recommended **Option 4**
(business requirements + characterization tests + design-spec
reference) as the intermediate objective serving the ultimate goal
of building a Blazor/C# replacement, with **GPT-5.4's scoping
discipline** ("prove on 3-5 workflows, then start building; don't
document the whole app first") adopted as the binding constraint.

The full review packages and the orchestrator's synthesis are
preserved at
`tmp/feedback/strategy_response_gemini_pro.md`,
`tmp/feedback/strategy_response_gpt_5_4.md`, and the synthesis is
captured in this conversation's history.

The four workflows were picked by the human to span:

- A CRUD-create with multi-entity validation and calculation
  (Add an Order — likely the richest behavioral surface).
- A CRUD-update with state-dependent constraints
  (Update an Order — tests state-machine semantics).
- A CRUD-update with cascading-effect concerns
  (Update a Product — tests reference-integrity boundaries).
- A CRUD-update directly adjacent to the Blazor library's role
  taxonomy (Update an Employee — likely surfaces ad-hoc Access
  role logic that needs explicit mapping).

---

## Definitions

### Workflow

A user-facing slice that crosses one or more forms, queries, and
events to accomplish a single business goal. Implementation-agnostic.
Survives the rewrite. Examples: "Add an Order," "Update a Product."

### Workflow operationalization

An at-least roughly (and sometimes strictly) ordered sequence of
user UI actions — some of which may be parameterized with things
like unique text or specific selections — that are repeated by users
for a target outcome, that can be formally expressed by the UAT DSL
(see `../dabbler-uat-dsl`), that typically have a parallel but
sparsely aligned set of parameterized CRUD operations, and that
accomplish the business purpose of the associated workflow (e.g.,
"Add an Order"). A workflow operationalization can be specific to
the software in which it is implemented.

**Practical implication:** the source operationalization (how Access
does it) belongs in the design-spec reference section of the
package. The target operationalization (how Blazor will do it) is
NOT produced by this pilot; it's authored later by the AI builder
during the implementation pilot. Tests and requirements pin
operationalization-independent properties.

### Behavior-bearing procedure

A VBA procedure whose body contains business logic that survives
the rewrite. Concretely: validators, calculations, state-change
logic, save/delete gating, business-rule-heavy query shaping.

NOT behavior-bearing: pure UI choreography (focus changes, control
visibility toggles, message-box plumbing), navigation handlers,
trivial property-getter/setter wrappers, dead code.

The harvester's existing event-handler classification helps
identify candidates; the orchestrator confirms by reading the
source body.

---

## The Workflow-Package Template

Each pilot workflow produces one document at
`docs/session-sets/workflow-package-pilot/packages/<workflow-slug>.md`
with these seven sections in order:

### 1. Workflow summary

One paragraph: who does this, why, and what business outcome it
accomplishes. Implementation-agnostic. Two to four sentences.

### 2. Actors and entities

- **Actors:** who initiates the workflow (User, SystemAdmin,
  CrossOrgMonitor, etc., mapped to the role taxonomy where the
  Access app's roles map cleanly).
- **Primary entity:** the entity being created or updated (Order,
  Product, Employee).
- **Related entities:** what else is read or modified in service of
  the primary entity (Customer, OrderDetails, Inventory, etc.).

### 3. Business rules with evidence + confidence tags

A bulleted list of rules. Each rule:

- **Rule statement** (what must be true)
- **Evidence:** citation to harvester output (specific procedure
  name, query name, validation rule, table constraint)
- **Confidence:** `confirmed` (rule is explicit in the code) /
  `inferred` (rule is implied by the code's behavior but not
  spelled out) / `unknown` (looks like there might be a rule here
  but the orchestrator can't confirm)

**Forbidden:** rules without evidence. If the orchestrator can't
cite, the entry goes in the open-questions log instead.

### 4. Behavioral invariants → characterization tests

For each behavior-bearing procedure identified in this workflow:

- **Invariant statement:** what behavior the test pins
- **Test file path:**
  `tests/AccessHarvester.CharacterizationTests/<WorkflowSlug>/<TestClassName>.cs`
- **Anti-coupling check:** the invariant must be implementation-
  independent. Tests that assert against `Recordset.Edit`,
  `Forms!MyForm!MyControl.Value`, focus events, or message-box
  displays are operationalization-coupled and forbidden — push
  them to the open-questions log instead, or reformulate as
  invariant tests against pure functions.

### 5. Source operationalization reference

The Access click-sequence that operationalizes this workflow in the
existing app. Format per **Decision 1** above. Used for ambiguity
resolution; not a blueprint for the new system.

### 6. Component / role mapping hints

Advisory-only mapping hints for the AI builder. Format per
**Decision 2** above. Default form:

- **UI pattern:** "fits master-detail" / "needs separate
  create-form vs. edit-form" / "is a wizard with N steps" / etc.
- **Role gating:** "edit operations require <role>" / "delete
  operations require <higher role>" / "no role gating observed"
- **Component library mapping (optional):** specific component
  references when known.

### 7. Open questions / ambiguity log

Explicit unknowns. Each entry:

- **Question:** what couldn't be determined from harvester output
- **Source-of-uncertainty:** what evidence is missing
- **Recommended resolution path:** human review of running app /
  domain expert / specific harvester gap to address

**Required:** at least one entry per package, even if just "no
ambiguities found in the source body." The act of looking is the
checkpoint.

---

## Pilot Workflows

| # | Slug | Workflow | Why included |
|---|---|---|---|
| 1 | `add-an-order` | Add an Order | Richest behavioral surface; multi-entity validation; calculation logic |
| 2 | `update-an-order` | Update an Order | State-dependent constraints; tests state-machine semantics |
| 3 | `update-a-product` | Update a Product | Cascading-effect concerns; tests reference-integrity boundaries |
| 4 | `update-an-employee` | Update an Employee | Adjacent to role taxonomy; surfaces ad-hoc Access role logic |

The order above is the recommended session order. Add an Order
first because it's the most complex and exercises the package
template most thoroughly; the three update workflows follow with
reduced scope per workflow.

---

## Sessions
> Seven sessions. Mostly in-IDE per the cost-consciousness
> constraint; routed cost limited to one cross-provider
> session-verification per session. Total expected routed cost:
> ~$0.35 across the set.
>
> Mandatory Step 6 (build + test) and Step 8 (cross-provider
> session-verification) per `docs/ai-led-session-workflow.md` apply
> to every session.

### Session 1 of 7: Setup, Inventory, Prioritize, Resolve Open Decisions

**Steps:**

1. Required reading: `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`,
   `docs/planning/session-set-authoring-guide.md`. Re-read this
   spec end-to-end.
2. Surface the two **Open Decisions** above to the human and
   capture their answers. If the human is unavailable, apply the
   defaults and proceed (note in `ai-assignment.md`).
3. Inventory NorthwindDevEdition.accdb workflows from harvester
   metadata. Goal: confirm the four pilot workflows map cleanly
   to identifiable forms, queries, and event handlers in the
   existing harvester output. Specifically locate:
   - The form(s) that operationalize each workflow
   - The event handlers that fire during the workflow
   - The bound queries the form(s) use
   - The underlying tables (Orders, Order Details, Products,
     Employees, Customers, etc.)
   - The behavior-bearing VBA procedures (per the definition above)
4. Save the inventory at
   `docs/session-sets/workflow-package-pilot/inventory.md`. One
   section per pilot workflow with the located artifacts.
5. Author the workflow-package template formally as a copyable
   stub at
   `docs/session-sets/workflow-package-pilot/_template.md`. Use the
   seven-section structure above; pre-populate with section
   headers and instructional comments.
6. Scaffold the test project at
   `tests/AccessHarvester.CharacterizationTests/` (xUnit, .NET 10).
   Add to `AccessHarvester.sln`. One folder per workflow slug.
7. Confirm Open Decisions outcomes are recorded in
   `ai-assignment.md` Session 1 actuals.
8. `dotnet build && dotnet test` (sequential).
9. Cross-provider session verification via Gemini Pro (per the
   in-IDE-pair / outsource-last pattern established in
   `vba-symbol-db-path-b-probe`).

**Creates:**

- `docs/session-sets/workflow-package-pilot/inventory.md`
- `docs/session-sets/workflow-package-pilot/_template.md`
- `tests/AccessHarvester.CharacterizationTests/AccessHarvester.CharacterizationTests.csproj`
- `tests/AccessHarvester.CharacterizationTests/<workflow-slug>/` directories (4 empty folders, one per workflow)
- `docs/session-sets/workflow-package-pilot/ai-assignment.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-001.md`

**Touches:**

- `AccessHarvester.sln` (add the new test project)

**Ends with:** Inventory complete; template ready; test project
scaffold compiling; Open Decisions resolved (or defaults applied
with disclosure); Session 1 verified.

**Progress keys:** `session-1/required-reading`,
`session-1/open-decisions`, `session-1/inventory`,
`session-1/template`, `session-1/test-project-scaffold`,
`session-1/build-test`, `session-1/verification`.

---

### Session 2 of 7: Add an Order — Package Draft

**Orchestrator note:** Choose Codex OR Claude for this session;
the OTHER orchestrator must run Session 3 (peer review) for
cross-provider independence. Recommended: Codex drafts (since
Codex authored the path-b-probe translation), Claude peer-reviews
(matching the path-b-probe pattern).

**Steps:**

1. Required reading. Re-read Session 1's `inventory.md` for the
   `add-an-order` row.
2. Pull the relevant harvester output for Add an Order: form
   metadata, event-binding rows, behavior-bearing procedure
   source bodies, related queries and tables. Use a temporary
   rebuilt symbol DB if needed (per the path-b-probe pattern that
   doesn't overwrite the committed `tools/symbol-db/symbol-db.sqlite`).
3. Author the workflow package draft at
   `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
   following the seven-section template. Apply the Open Decisions'
   chosen formats (UAT DSL vs. structured prose; generic vs.
   library-specific hints).
4. For each behavior-bearing procedure identified, generate
   characterization tests in
   `tests/AccessHarvester.CharacterizationTests/AddAnOrder/`.
   Use the path-b-probe pattern: AI translates the procedure into
   testable C# scaffolding, AI writes xUnit tests, the
   translation is discarded after the tests are stable. The tests
   stay; the translation does not.
5. Record explicit confidence tags on every business rule. If
   unsure, push the rule to the open-questions section instead.
6. `dotnet build && dotnet test` (sequential).
7. Cross-provider session verification via Gemini Pro.

**Creates:**

- `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/*.cs`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-002.md`

**Touches:** None outside the new package and test files.

**Ends with:** Add-an-Order package draft complete; characterization
tests for behavior-bearing procedures green; Session 2 verified.

**Progress keys:** `session-2/required-reading`,
`session-2/harvester-pull`, `session-2/package-draft`,
`session-2/characterization-tests`, `session-2/build-test`,
`session-2/verification`.

---

### Session 3 of 7: Add an Order — Peer Review and Falsification

**Orchestrator note:** The OTHER orchestrator from Session 2.
Independent review per the path-b-probe pattern: read the package
and tests against the original harvester output and source bodies,
without consulting Session 2's reasoning beyond what's documented.

**Steps:**

1. Required reading. Read Session 2's deliverables (package + tests).
2. Independent semantic review of the package:
   - Are the business rules backed by their cited evidence?
   - Are confidence tags appropriately conservative?
   - Are inferences flagged as `inferred` rather than presented
     as `confirmed`?
   - Is the source operationalization reference accurate to the
     harvester's form/event metadata?
   - Are the component/role mapping hints reasonable?
3. Independent review of the characterization tests:
   - Do the tests pin behavioral invariants or do they assert
     against UI mechanics? (Apply the anti-coupling check from
     section 4 of the template.)
   - Are there missing invariants (the path-b-probe Session 3
     found 4 in Codex's translation; expect similar gaps here)?
   - Are the tests genuinely falsifiable?
4. Add falsification tests for missing invariants in
   `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3*Tests.cs`
   (filename convention from the path-b-probe).
5. Update the package: tighten over-inferred rules, add
   ambiguity-log entries for items the reviewer can't confirm,
   leave Session 2's rules whose evidence holds up.
6. `dotnet build && dotnet test` (sequential).
7. Cross-provider session verification via Gemini Pro.

**Creates:**

- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3*Tests.cs`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-003.md`

**Touches:**

- `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
  (refinements to rules and ambiguity log)

**Ends with:** Add-an-Order package + tests verified by a second
orchestrator; ambiguity log reflects honest unknowns; Session 3
verified.

**Progress keys:** `session-3/required-reading`,
`session-3/package-review`, `session-3/test-review`,
`session-3/falsification-tests`, `session-3/package-refinement`,
`session-3/build-test`, `session-3/verification`.

---

### Session 4 of 7: Update an Order — Single-Pass Package

**Orchestrator note:** Either Codex or Claude. Single-pass authoring
(no separate peer-review session) because the three update workflows
are simpler than Add-an-Order; the cross-workflow review in Session
7 is the cross-provider check for these three.

**Steps:**

1. Required reading. Re-read Session 1's `inventory.md` for the
   `update-an-order` row.
2. Pull harvester output, author package, generate
   characterization tests — same shape as Session 2 but compressed
   into one session. Apply the lessons learned from Session 3 about
   anti-coupling and confidence tagging.
3. `dotnet build && dotnet test`.
4. Cross-provider session verification.

**Creates:**

- `docs/session-sets/workflow-package-pilot/packages/update-an-order.md`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/*.cs`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-004.md`

**Touches:** None outside the new package and test files.

**Ends with:** Update-an-Order package + tests complete; Session 4
verified.

**Progress keys:** `session-4/required-reading`,
`session-4/package`, `session-4/characterization-tests`,
`session-4/build-test`, `session-4/verification`.

---

### Session 5 of 7: Update a Product — Single-Pass Package

Same shape as Session 4. Slug: `update-a-product`. Folder:
`tests/AccessHarvester.CharacterizationTests/UpdateAProduct/`.

Pay special attention to cascading-effect rules: changes to a
Product may affect open Orders, vendor relationships, inventory.
Capture each cascading rule explicitly with evidence + confidence
tags; if cascade behavior is genuinely ambiguous, push to the
open-questions log instead of guessing.

**Progress keys:** `session-5/required-reading`,
`session-5/package`, `session-5/characterization-tests`,
`session-5/build-test`, `session-5/verification`.

---

### Session 6 of 7: Update an Employee — Single-Pass Package

Same shape as Sessions 4-5. Slug: `update-an-employee`. Folder:
`tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/`.

Pay special attention to role-related logic: hidden flags
(`IsAdmin`, `IsActive`), role-gated visibility, conditional
formatting based on logged-in user. Surface candidate mappings to
the Blazor library's role taxonomy (SystemAdmin / UserAdmin /
CrossOrgUserAdmin / CrossOrgMonitor / User) in section 6 of the
package; flag uncertain mappings in the open-questions log.

**Progress keys:** `session-6/required-reading`,
`session-6/package`, `session-6/characterization-tests`,
`session-6/build-test`, `session-6/verification`.

---

### Session 7 of 7: Cross-Workflow Audit + Handoff + Change Log + Step 9

**Orchestrator note:** Whichever orchestrator HAS NOT done two of
the prior workflow sessions, ideally — to maximize cross-provider
independence on the audit. If both orchestrators have run two
sessions each, either is fine.

**Steps:**

1. Required reading. Read all four workflow packages and the
   characterization-test files end-to-end.
2. **Cross-workflow audit:** look for systematic patterns.
   - Are there business rules that appear in multiple packages
     with subtly different statements? (Indicator of inconsistent
     interpretation.)
   - Are the confidence tags applied consistently across packages,
     or is one package much more confident than others on similar
     evidence quality?
   - Do any tests look UI-coupled despite the anti-coupling check
     in individual sessions?
   - Are there role-related findings in `update-an-employee` that
     should also appear in the other update workflows?
   - Does the source operationalization reference style work
     equally well across the four workflows, or does one workflow
     suggest a different format?
3. Save audit findings to
   `docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`.
4. **Handoff document:** author
   `docs/session-sets/workflow-package-pilot/handoff.md` for the
   downstream Blazor implementation pilot. Include:
   - Summary of what's ready (packages, tests, audit)
   - Known limitations and open questions to carry forward
   - Recommended next-set scope (which workflow to implement first
     in Blazor; what infrastructure pieces of the Blazor library
     need to be ready)
5. Author `change-log.md` for the set in-conversation per the
   in-conversation convention (do not route `summarization`).
6. Update the next-session-set recommendation in `ai-assignment.md`.
7. `dotnet build && dotnet test`.
8. Cross-provider session verification via Gemini Pro.
9. (Per workflow Step 9) Reorganization review of
   `docs/planning/project-guidance.md` and
   `docs/planning/lessons-learned.md` after the notify.

**Creates:**

- `docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`
- `docs/session-sets/workflow-package-pilot/handoff.md`
- `docs/session-sets/workflow-package-pilot/change-log.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-007.md`

**Touches:** None outside the new files.

**Ends with:** All four packages audited; handoff document ready
for downstream Blazor pilot; set closed.

**Progress keys:** `session-7/required-reading`,
`session-7/cross-workflow-audit`, `session-7/handoff`,
`session-7/change-log`, `session-7/next-set-recommendation`,
`session-7/build-test`, `session-7/verification`,
`session-7/step-9-reorganization`.

---

## Stop Rules and Pivot Signals

These are explicit triggers to stop the set early and pivot rather
than press through. Each rule lists the signal and the
recommended pivot.

| Stop rule | Signal | Pivot |
|---|---|---|
| **Tests are UI-coupled despite the anti-coupling check** | A workflow's characterization tests require simulating Forms, recordsets, focus, or event sequencing | Stop generating tests for that workflow. Fall back to requirements + reference only. Surface to human at next checkpoint. |
| **Packaging takes longer than building would** | A single workflow takes more than 2 sessions worth of in-IDE time to package | Cut scope harder for the remaining workflows. Reduce to requirements + a smaller test set. |
| **AI over-infers business intent across multiple workflows** | Cross-workflow audit (Session 7) finds systematic invented rationale | Re-do the affected packages with stricter "evidence required" prompts. May add 1-2 sessions to the set. |
| **Harvester gaps force repeated manual spelunking** | A workflow's package can't be authored without opening Access manually multiple times | Stop enriching broadly; document the harvester gaps in the handoff for a future harvester-extension set. Continue with what's already extractable. |
| **Cross-provider disagreement stays high** | Session 3's peer review of Add an Order finds substantial disagreement with Session 2 (more than 4-5 missing invariants, OR rules that don't hold up) | The Add-an-Order workflow may not be harvestable enough. Stop the set after Session 3, surface to the human, decide whether to switch pilot workflows or pivot strategy. |
| **The packages don't materially reduce ambiguity for the AI builder** | Mid-stream review (after Session 3 or Session 5) shows the packages don't actually disambiguate enough for downstream Blazor work | Pivot to Option 2 (requirements + reference only) for the remaining workflows. Drop the characterization tests except for high-risk calculations/validators. |

If any stop rule fires, the orchestrator surfaces to the human and
waits for direction; does not auto-pivot.

---

## Cost Expectations

- **Routed cost target:** ~$0.05 per session (Gemini Pro
  cross-provider verification only); ~$0.35 total across the set.
- **In-conversation token spend:** Substantial — the bulk of
  package authoring, test generation, peer review, and audit
  happens in-IDE under Anthropic Max (Claude Code) and GitHub
  Copilot (Codex) windows.
- **No routed analysis** for `ai-assignment.md` authoring,
  next-session recommendations, or the change log. All
  in-conversation per the outsource-last pattern established in
  `vba-symbol-db-path-b-probe`. The mandatory Gemini Pro
  cross-provider verification per session is the only routed cost.

This matches the outsource-last mode of the in-progress VS Code
extension and the cost-consciousness constraint the human set
during strategy discussions on 2026-04-30.

---

## Routing Notes

- **Session 1 (setup):** Claude Opus 4.7 @ effort=high (Claude
  Code) for the inventory pass — needs to read across many
  harvester artifacts. Codex would also work; alternation
  preference is not strong here.
- **Sessions 2 + 3 (Add an Order draft + peer review):** Different
  orchestrators. Recommended: Codex drafts in Session 2, Claude
  peer-reviews in Session 3 (matching the path-b-probe pattern
  that the human is familiar with). Reverse is fine if preferred.
- **Sessions 4-6 (the three updates):** Alternate orchestrators
  for variety. No two consecutive sessions with the same
  orchestrator if avoidable.
- **Session 7 (audit):** Whichever orchestrator has done the
  fewest prior sessions, for maximum independence on the audit.
- **Cross-provider verification stays mandatory** for every
  session per Rule #2 of the workflow doc. Pinned to Gemini Pro
  per the special-session direction continuing from
  `vba-symbol-db-path-b-probe`.

---

## What "Success" Looks Like at the End of the Set

A downstream AI builder, given any one of the four workflow
packages plus the existing Blazor component library, should be able
to implement a Blazor slice that:

1. Solves the same business problem as the Access workflow.
2. Passes the characterization tests without modification.
3. Uses Blazor patterns natural to the component library, not
   patterns translated from Access.
4. Surfaces, to a human reviewer, the package's open-questions
   list as known limitations (not as silent assumptions).

If a builder can do this end-to-end on at least the first
workflow (Add an Order) within a small number of sessions and
without re-reading the source VBA, the pilot succeeded. If
multiple workflows require returning to the harvester or to the
original Access app for additional context, the package shape
needs revision before scaling.

The handoff document (Session 7) makes this success criterion
explicit and concrete for the downstream Blazor implementation
pilot to consume.
