# Generalization Validation on Non-Northwind Spec

> **Purpose:** Validate that the workflow-package shape established by
> `workflow-package-pilot` (NorthwindDevEdition) generalizes to a
> structurally different Access application. Author one workflow package
> for HomeHealthCare's `Add a Client` workflow, peer-review it against
> the Northwind `Add an Order` package, and produce a verdict — pass,
> revise-and-retry, or pivot — for the package-shape strategy as it
> scales beyond Northwind.
> **Created:** 2026-05-15
> **Session Set:** `docs/session-sets/generalization-validation-on-non-northwind/`
> **Prerequisite:** `workflow-package-pilot` complete (the
> `Add an Order` package + characterization-test pattern are the
> baseline this set validates against). `form-report-code-and-grouping`
> complete (the typed `FormMetadata` / `ReportMetadata` schema is the
> reading surface for this work). `samples/HomeHealthCare.accdb`
> committed (yes); YAML mirror at
> `samples/DatabaseYaml/HomeHealthCare.accdb/` committed (yes).
> **Workflow:** Orchestrator → in-IDE pair (Claude Code + Codex,
> alternating per session) → cross-provider session verification per
> session.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: This set produces a documentation package (markdown) plus
> characterization tests (xUnit) under `docs/session-sets/<slug>/` and
> `tests/AccessHarvester.CharacterizationTests/`. No UI surface; no
> end-user feature ships. Quality bar is build + tests +
> cross-provider AI verification per session, plus a peer-review
> session that audits the package against the Northwind baseline. The
> Blazor implementation pilot that consumes the resulting package is a
> downstream session set with its own UAT considerations.

---

## Why This Set, Why Now

The `workflow-package-pilot` (closed 2026-04-30 / 2026-05-01) shipped
four workflow packages for `NorthwindDevEdition.accdb` and proved the
package shape works on **Northwind**. The Phase-2 strategic review
(`docs/planning/strategy.md` → Phase 3c) explicitly identified
**generalization validation** as the only Phase-3 work item that
naturally lives in this repo, and named `MiniComplex.accdb` and
`HomeHealthCare.accdb` as the candidate target databases.

Open Validation #2 from the strategy doc — *"Does the package shape
generalize to non-Northwind databases?"* — is one of the three
remaining open validations. A negative answer means the
workflow-package template needs revision before scaling the Blazor
migration effort to additional Access apps; a positive answer
de-risks the strategy and lets Phase 3a (Blazor update workflows)
proceed with confidence.

`HomeHealthCare.accdb` is chosen over `MiniComplex.accdb` because:

1. HHC is the **primary canonical fixture** per
   `docs/planning/project-guidance.md` → Canonical Test Fixtures.
   `MiniComplex.accdb` is explicitly excluded from automated test
   coverage pending an error-audit pass and may only be used in
   narrow, explicitly-scoped experiments.
2. HHC has a **committed YAML mirror** under
   `samples/DatabaseYaml/HomeHealthCare.accdb/` that the orchestrator
   can read without needing Access COM to re-extract. The harvester
   pipeline can also run against the committed `samples/HomeHealthCare.accdb`
   when Access is present.
3. HHC is **structurally complementary to Northwind**
   (`docs/homehealthcare-northwind-comparison.md`): unbound
   listbox-cascade search instead of bound split forms,
   `OpenForm ... acDialog` popup detail instead of bound nav,
   validation-only data macros instead of audit-field macros, mixed
   DAO + ADODB instead of DAO-only, Hungarian naming throughout. A
   package shape that works for HHC has a much higher prior
   probability of working for arbitrary Access apps than one that
   only handles Northwind's mainstream patterns.

The "Add a Client" workflow is the chosen pilot for this set because
it exercises the HHC patterns that diverge most from Northwind: the
unbound `frmClientSearch` listbox-cascade + the `frmClientDetail`
popup opened with `OpenForm ... acDialog, "ADD"`. If the package
shape can describe THIS workflow cleanly, it has cleared the
strategically significant generalization bar; if it can't, the
template needs revision in ways that lighter workflows would not
have surfaced.

---

## Project Overview

### What this set delivers

1. **One workflow package** for HomeHealthCare's `Add a Client`
   workflow at
   `docs/session-sets/generalization-validation-on-non-northwind/packages/add-a-client.md`,
   following the seven-section template established by
   `workflow-package-pilot`.

2. **Characterization tests** for the behavior-bearing procedures
   involved in `Add a Client`, under
   `tests/AccessHarvester.CharacterizationTests/AddAClient/`.
   Tests pin business invariants, NOT operationalization mechanics —
   same anti-coupling rule as `workflow-package-pilot`.

3. **A generalization-validation report** at
   `docs/session-sets/generalization-validation-on-non-northwind/generalization-findings.md`
   that compares the HHC package side-by-side with the Northwind
   `Add an Order` package and produces an explicit verdict:
   - `generalizes-cleanly` — the same template handled HHC without
     structural revision; package strategy proceeds.
   - `revise-and-retry` — the template needs specific changes; the
     report names them, a follow-up set applies them.
   - `pivot` — the template fundamentally doesn't fit non-Northwind
     apps; strategy needs to be re-evaluated before proceeding.

4. **A change-log + Step 9 reorganization review** per the standard
   workflow.

### What this set does NOT do

- Does not author packages for other HHC workflows (only `Add a
  Client`). If the validation passes, scaling to additional HHC
  workflows is a follow-up set; if it surfaces template revisions,
  the next set applies the revisions and re-validates.
- Does not implement any Blazor code. The package is an input to a
  downstream session set, not implementation work.
- Does not touch `MiniComplex.accdb` (excluded fixture per
  project-guidance.md). MiniComplex generalization can return as a
  separate follow-up after HHC is validated and any necessary
  template revisions land.
- Does not refactor the harvester. Components like
  `AccessHarvester.Vba.Resolver` and the symbol DB are consumed
  as-is. If a harvester limitation blocks the workflow, surface to
  the human and document it in the generalization-findings report;
  do not patch the harvester inside this set.
- Does not extend or repopulate the AI-interpretation columns
  (`Confidence*`, `Justification*`, `Interpretation*`) on the symbol
  DB. Same throwaway-under-Hybrid rule as `workflow-package-pilot`.
- Does not modify `docs/planning/strategy.md`. The findings report
  feeds into the next strategic review; updates to the strategy doc
  happen there, not here.

### Inputs available

- `samples/HomeHealthCare.accdb` — committed Access DB.
- `samples/DatabaseYaml/HomeHealthCare.accdb/` — committed YAML
  mirror covering Forms, Reports, Queries, Tables, Macros, Modules,
  Relationships. Sufficient for inventory and most reading without
  Access COM.
- `docs/homehealthcare-northwind-comparison.md` — authoritative
  comparison of HHC vs Northwind on the dimensions that matter for
  this work. Read end-to-end before Session 1's inventory step.
- `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
  — the baseline package the HHC `Add a Client` package is compared
  against.
- `docs/session-sets/workflow-package-pilot/_template.md` — the
  copyable template (same seven-section structure).
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/` — the
  baseline characterization-test set the HHC tests are compared
  against (for shape, not content).
- The harvester pipeline. If Access COM is available, the
  orchestrator can run `AccessHarvester.Cli` against
  `samples/HomeHealthCare.accdb` into a working `tmp/<set-slug>/`
  directory. If Access COM is unavailable, the YAML mirror is the
  fallback reading surface; the orchestrator notes this in
  `ai-assignment.md` and the verifier prompt.

---

## Stop Rules and Pivot Signals

These triggers stop the set early and surface to the human rather
than press through. Each rule lists the signal and the recommended
pivot.

| Stop rule | Signal | Pivot |
|---|---|---|
| **Harvester pipeline fails on HHC** | The pipeline produces a non-trivial error or visibly garbled output for a form / query / module HHC uses (e.g. unbound forms surface as malformed, validation macros are mis-parsed) | Stop. Document the gap in `generalization-findings.md`. Recommend a harvester-extension follow-up set. Do NOT patch the harvester inside this set. |
| **Package template doesn't fit HHC's listbox-cascade + popup pattern** | The seven sections, as defined, force the orchestrator to either invent fields or leave the most important workflow detail unrepresented | Stop after Session 1. Surface the misfit to the human. Session 2 becomes a template-revision proposal rather than a peer review. |
| **Characterization tests are UI-coupled despite anti-coupling rule** | The HHC workflow's behavior-bearing procedures only make sense in the context of `Forms!frmClientDetail!cboCity.Value`, `OpenArgs` parsing, or `DoCmd.OpenForm` sequencing | Stop generating tests for the affected procedures. Surface to human. Fall back to requirements + reference only. |
| **Single workflow takes longer than 2 sessions worth of in-IDE time** | Session 1 ends with the package half-drafted and the orchestrator's runway depleted | Stop. Surface to human. This is itself a generalization-validation finding — if one HHC workflow can't fit in the same session budget as `Add an Order`, the package shape is over-tuned to Northwind. |
| **AI over-infers business intent** | Session 2 peer review finds systematic invented rationale (rules that look reasonable but aren't backed by the source) | Stop the set after Session 2's peer-review surfaces the pattern. The findings report names it explicitly as a generalization-validation negative result. |

If any stop rule fires, the orchestrator surfaces to the human and
waits for direction; does not auto-pivot.

---

## Session Plan

> Two sessions. Mostly in-IDE per the cost-consciousness
> constraint; routed cost limited to the mandatory cross-provider
> session-verification per session. Target routed cost: ~$0.20 total
> across the set (one Gemini Pro verification per session +
> incidental routed analysis for `ai-assignment.md` if needed).
>
> Mandatory build + test and Step 6 (cross-provider
> session-verification) per `docs/ai-led-session-workflow.md` apply
> to every session.

### Session 1 of 2: HHC Inventory + `Add a Client` Package Draft + Tests

**Orchestrator note:** Codex OR Claude. Recommended: Codex drafts
(matching the `Add an Order` draft pattern from
`workflow-package-pilot` Session 2), Claude peer-reviews in Session
2. Reverse is fine if preferred; the cross-provider independence is
what matters, not the specific assignment.

**Steps:**

1. Required reading: `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`,
   `docs/planning/session-set-authoring-guide.md`,
   `docs/planning/strategy.md` (Phase 3c context),
   `docs/homehealthcare-northwind-comparison.md` (end-to-end),
   `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
   (the baseline being compared against),
   `docs/session-sets/workflow-package-pilot/_template.md`.
2. Verify the prior session set committed. Run `git status --short`
   against `docs/session-sets/workflow-package-pilot/`,
   `docs/session-sets/form-report-code-and-grouping/`, and the
   `tests/AccessHarvester.CharacterizationTests/` test project.
   Surface any uncommitted state from prior sessions per the
   "Verify the prior session actually committed" convention.
3. Run the harvester pipeline against `samples/HomeHealthCare.accdb`
   if Access COM is available, writing output to
   `tmp/generalization-validation-on-non-northwind/kb/` and
   `tmp/generalization-validation-on-non-northwind/ir/`. If Access
   COM is unavailable, document the fallback (read directly from
   `samples/DatabaseYaml/HomeHealthCare.accdb/` plus the source
   `.accdb`) in `ai-assignment.md`. Either way, the inventory step
   must have a structured reading surface for HHC.
4. **HHC inventory pass.** Locate the artifacts that operationalize
   the `Add a Client` workflow:
   - The form(s) involved: `frmClientSearch` (the listbox-cascade
     entry point), `frmClientDetail` (the popup detail form),
     anything else that fires during the add path.
   - The event handlers: `frmClientSearch`'s
     "Add Client" button click handler (or whatever launches the
     add flow); `frmClientDetail`'s `Form_Load` / `Form_Open`
     handler that reads `OpenArgs`; the `Form_BeforeUpdate` /
     `Form_BeforeInsert` validation handler.
   - The bound queries (if any) and underlying tables
     (`tblClients` and any related tables — addresses,
     intake details, etc.).
   - The behavior-bearing VBA procedures per the
     `workflow-package-pilot` definition (validators,
     calculations, state-change logic, save/delete gating). Note
     where validation logic lives in HHC: data macros on
     `tblClients` (`mcrValidation` pattern) in addition to
     VBA-level handlers.
   - Save the inventory at
     `docs/session-sets/generalization-validation-on-non-northwind/inventory.md`
     with the located artifacts plus a note for each one about
     how it maps (or doesn't) to a Northwind analogue.
5. **Author the `Add a Client` workflow package** at
   `docs/session-sets/generalization-validation-on-non-northwind/packages/add-a-client.md`
   using the seven-section template:
   1. Workflow summary
   2. Actors and entities
   3. Business rules with evidence + confidence tags
   4. Behavioral invariants → characterization tests
   5. Source operationalization reference (structured prose, per
      `workflow-package-pilot` Decision 1 default)
   6. Component / role mapping hints (generic-now, per
      `workflow-package-pilot` Decision 2 default)
   7. Open questions / ambiguity log
   Apply the same anti-coupling and evidence rules from
   `workflow-package-pilot`. **Pay specific attention to HHC's
   `OpenArgs` contract** (`"ADD"`, `"ADD|key=val|..."`, numeric PK)
   — this is the pattern most likely to surface a
   template-vs-pattern mismatch. If the seven sections force you
   to invent fields, STOP and surface to the human per the stop
   rule.
6. **Generate characterization tests** in
   `tests/AccessHarvester.CharacterizationTests/AddAClient/` for
   the behavior-bearing procedures identified in step 4. Use the
   path-b-probe pattern: AI translates the procedure into testable
   C# scaffolding, AI writes xUnit tests, the translation is
   discarded after tests are stable. Tests stay; translation does
   not. Apply the anti-coupling rule strictly — if the procedure
   only makes sense in context of UI events, push to the
   open-questions log instead of authoring an UI-coupled test.
7. Record findings about the harvester's coverage of HHC inline in
   `inventory.md` (e.g., "VBA signatures for `basDataOperations`
   look correct"; "Form event-binding rows for `frmClientDetail`
   surface as expected"; "Data macros on `tblClients` are visible
   but the action list is sparse"). These notes feed Session 2's
   generalization-findings report.
8. `dotnet build && dotnet test` (sequential).
9. Cross-provider session verification via the routed
   `session-verification` task type (default-cross-provider per
   `task_type_overrides` in `router-config.yaml`).

**Creates:**

- `docs/session-sets/generalization-validation-on-non-northwind/inventory.md`
- `docs/session-sets/generalization-validation-on-non-northwind/packages/add-a-client.md`
- `tests/AccessHarvester.CharacterizationTests/AddAClient/*.cs` (one
  or more test files per behavior-bearing procedure identified)
- `docs/session-sets/generalization-validation-on-non-northwind/ai-assignment.md`
- `docs/session-sets/generalization-validation-on-non-northwind/session-reviews/session-001.md`
- (Conditional) `tmp/generalization-validation-on-non-northwind/kb/`
  and `tmp/.../ir/` — local harvester output (gitignored, regenerable)

**Touches:**

- `AccessHarvester.sln` if the new test folder needs adding (it
  shouldn't — `AccessHarvester.CharacterizationTests` was added by
  `workflow-package-pilot` Session 1; new test files just need to
  land under it).

**Ends with:** Inventory complete; `Add a Client` package authored to
the seven-section template; characterization tests green; Session 1
verified. Any stop-rule signals surfaced to the human before the
session closes.

**Progress keys:** `session-1/required-reading`,
`session-1/prior-session-committed-check`,
`session-1/harvester-pipeline-or-fallback`,
`session-1/inventory`,
`session-1/package-draft`,
`session-1/characterization-tests`,
`session-1/harvester-coverage-notes`,
`session-1/build-test`,
`session-1/verification`.

---

### Session 2 of 2: Peer Review + Generalization Findings + Handoff + Close

**Orchestrator note:** The OTHER orchestrator from Session 1.
Independent review per the `workflow-package-pilot` path-b-probe
pattern: read Session 1's package and tests against the original
harvester output and source bodies WITHOUT consulting Session 1's
reasoning beyond what's documented.

**Steps:**

1. Required reading. Read Session 1's deliverables
   (`inventory.md`, `packages/add-a-client.md`, the
   characterization-test files). Re-read this spec end-to-end and
   `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
   (the baseline being compared against).
2. Verify Session 1 committed (per the "Verify the prior session
   actually committed" convention).
3. **Independent semantic review of the package:**
   - Are the business rules backed by their cited evidence?
   - Are confidence tags appropriately conservative? Are
     inferences flagged as `inferred` rather than `confirmed`?
   - Is the source operationalization reference accurate to HHC's
     listbox-cascade + popup pattern? Does it correctly describe
     the `OpenArgs` contract?
   - Are the component/role mapping hints reasonable given HHC
     has no role taxonomy (single-user app per the comparison
     doc)? Is the absence of role gating documented as a
     finding, not glossed over?
   - Are the validation rules sourced from BOTH the
     `Form_BeforeUpdate` VBA path AND the table-level data macros
     (`mcrValidation`)? Missing the data-macro layer would mean
     the package can't describe the actual enforcement
     mechanism.
4. **Independent review of the characterization tests:**
   - Do the tests pin behavioral invariants or do they assert
     against UI mechanics? (Apply the anti-coupling check from
     section 4 of the template.)
   - Are there missing invariants? (The
     `workflow-package-pilot` Session 3 review of `Add an Order`
     found 4 missing invariants; expect similar gaps here.)
   - Are the tests genuinely falsifiable?
5. **Add falsification tests** for missing invariants in
   `tests/AccessHarvester.CharacterizationTests/AddAClient/Session2*Tests.cs`
   (filename convention from path-b-probe).
6. **Update the package:** tighten over-inferred rules, add
   ambiguity-log entries for items the reviewer can't confirm,
   leave Session 1's rules whose evidence holds up.
7. **Author the generalization-findings report** at
   `docs/session-sets/generalization-validation-on-non-northwind/generalization-findings.md`.
   Required structure:
   - **Verdict:** one of `generalizes-cleanly` /
     `revise-and-retry` / `pivot`. ONE LINE, conspicuous.
   - **Template fit:** for each of the seven sections, did the
     section fit HHC's `Add a Client` cleanly, fit with minor
     adaptation, or force structural compromise? Evidence: cite
     the specific subsection of the package.
   - **Pattern divergence findings:** what patterns did HHC
     exercise that Northwind didn't? (Listbox-cascade,
     `OpenArgs`, validation data macros, ADODB, etc.) For each,
     note whether the package shape captured it cleanly.
   - **Harvester coverage findings:** what did the harvester
     surface well for HHC? What didn't it surface (gaps that
     forced manual reading)? Specific file/procedure references.
   - **Recommended next move:**
     - If `generalizes-cleanly`: name 1-2 HHC follow-up
       workflows worth packaging next, and recommend the Phase
       3a (Blazor update workflows) move per strategy.md.
     - If `revise-and-retry`: name specific template revisions
       and recommend a follow-up set that applies them.
     - If `pivot`: explain what about the package shape doesn't
       generalize and recommend a strategy-doc revision.
8. **Author `change-log.md`** for the set in-conversation per the
   in-conversation convention (don't route `summarization`).
   Include the verdict as the headline.
9. **Author the next-session-set recommendation** in
   `next-session-set-recommendation.md`. The recommendation
   follows from the verdict (per step 7's "Recommended next move"
   bullets).
10. **Author `disposition.json`** before the closeout commit, per
    the new lesson from `form-report-code-and-grouping` Session 3.
11. `dotnet build && dotnet test` (sequential).
12. Cross-provider session verification.
13. Per workflow Step 8: commit, push, `mark_session_complete()`,
    notify.
14. Per workflow Step 9: reorganization review of
    `docs/planning/project-guidance.md` and
    `docs/planning/lessons-learned.md` AFTER the notify. Output
    "no changes recommended" if nothing qualifies — but do the
    review.

**Creates:**

- `tests/AccessHarvester.CharacterizationTests/AddAClient/Session2*Tests.cs`
  (falsification tests for missing invariants)
- `docs/session-sets/generalization-validation-on-non-northwind/generalization-findings.md`
- `docs/session-sets/generalization-validation-on-non-northwind/change-log.md`
- `docs/session-sets/generalization-validation-on-non-northwind/next-session-set-recommendation.md`
- `docs/session-sets/generalization-validation-on-non-northwind/disposition.json`
- `docs/session-sets/generalization-validation-on-non-northwind/session-reviews/session-002.md`

**Touches:**

- `docs/session-sets/generalization-validation-on-non-northwind/packages/add-a-client.md`
  (refinements to rules, evidence, and ambiguity log)
- `docs/session-sets/generalization-validation-on-non-northwind/ai-assignment.md`
  (Session 2 actuals)

**Ends with:** Package + tests peer-reviewed; generalization-findings
report with explicit verdict; set closed; next move clearly
recommended.

**Progress keys:** `session-2/required-reading`,
`session-2/prior-session-committed-check`,
`session-2/package-review`,
`session-2/test-review`,
`session-2/falsification-tests`,
`session-2/package-refinement`,
`session-2/generalization-findings`,
`session-2/change-log`,
`session-2/next-session-set-recommendation`,
`session-2/disposition`,
`session-2/build-test`,
`session-2/verification`,
`session-2/step-9-reorganization`.

---

## Deliverables (End of Set)

1. `docs/session-sets/generalization-validation-on-non-northwind/inventory.md`
   — what was located in HHC for `Add a Client`.
2. `docs/session-sets/generalization-validation-on-non-northwind/packages/add-a-client.md`
   — the workflow package itself (seven sections, post-peer-review).
3. `tests/AccessHarvester.CharacterizationTests/AddAClient/*.cs`
   — characterization tests pinning the behavioral invariants.
4. `docs/session-sets/generalization-validation-on-non-northwind/generalization-findings.md`
   — the validation report with explicit verdict.
5. `change-log.md`, `next-session-set-recommendation.md`,
   `disposition.json` — standard closeout artifacts.
6. (If applicable) Step 9 reorganization commit, separately.

---

## Cost Expectations

- **Routed cost target:** ~$0.10–$0.20 per session (one
  cross-provider session-verification + maybe one routed analysis
  for `ai-assignment.md`); ~$0.20–$0.40 total across the set.
- **In-conversation token spend:** Substantial — package
  authoring, test generation, peer review, and findings authoring
  happen in-IDE under Anthropic Max (Claude Code) and GitHub
  Copilot (Codex) windows. Matches the
  `workflow-package-pilot` pattern.

---

## What "Success" Looks Like at the End of the Set

The generalization-findings report contains an explicit verdict and
the evidence behind it. A downstream reader (the next session-set
author, or the human directing the broader strategy) can read the
findings and know:

1. Whether the workflow-package shape works for HHC's patterns or
   needs revision.
2. If it needs revision, what specifically to change.
3. If it works, what to package next (more HHC workflows, then
   MiniComplex once that fixture is unblocked, then the Phase 3a
   Blazor pilot per strategy.md).
4. What the harvester surfaced well for HHC and what it didn't —
   a focused list of harvester gaps that a future
   harvester-extension set could close.

If the verdict is `generalizes-cleanly`, Open Validation #2 in
`strategy.md` flips from OPEN to RESOLVED, and the strategy doc
needs an update in the next strategic review pass (not in this set
— this set produces the input, the strategy doc consumes it).

If the verdict is `revise-and-retry` or `pivot`, the recommended
next move is named explicitly so the human can authorize it without
re-deriving it.
