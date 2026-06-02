# Harvester Upstream Feedback Followups Spec

> **Purpose:** Address the remaining upstream consumer feedback after the
> cleanup-output narrowing landed: classify switchboards and other launcher/menu
> surfaces more explicitly, model direct or out-of-band entry as first-class
> uncertainty, and surface parameterized-query / parameter-dialog behavior more
> clearly in usage outputs. Verify `object-use-checklist.json` casing against a
> fresh run, but only change code if current upstream output still drifts.
> **Created:** 2026-06-02
> **Session Set:** `docs/session-sets/001-harvester-upstream-feedback-followups/`
> **Prerequisites:** `harvester-object-use-identifier`,
> `harvester-object-cleanup-identifier`
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.
> **Status:** Draft

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites:
  - slug: harvester-object-use-identifier
    condition: complete
  - slug: harvester-object-cleanup-identifier
    condition: complete
totalSessions: 3
```

> Rationale: this set only changes KB/IR interpretation and generated review
> artifacts. It does not require Access COM, a UI harness, or separate E2E
> automation when exercised against committed sample output and hermetic tests.

---

## Why This Set, Why Now

The cleanup false-positive problem has already been narrowed at the source:
`object-cleanup.json` is now intended to contain only positive stale-like
signals. That still leaves the main downstream interpretation gaps that surfaced
while consuming HomeHealthCare output:

1. switchboards, menu forms, helper macros, and helper tables are not being
   explained strongly enough as navigation surfaces
2. missing harvested openers are sensed today but not modeled as explicit
   direct-navigation or out-of-band uncertainty
3. parameterized queries and likely parameter-dialog flows are not yet promoted
   to first-class usage signals

Those gaps cause downstream AI consumers to spend time re-interpreting objects
that already look understandable to an Access operator. This set tightens the
usage-side explanation layer without reopening the cleanup scope that was just
corrected.

## Done Looks Like

- Switchboard-like forms and report-menu surfaces can be classified as
  launcher/navigation shells even with no record source.
- Helper artifacts such as switchboard macros and switchboard tables are tied to
  launcher behavior instead of reading as generic unexplained objects.
- Usage artifacts can say "no harvested opener found; may still be reachable by
  startup, direct navigation, ribbon, or other out-of-band access" rather than
  implying likely staleness.
- Parameterized queries emit explicit parameter-related signals, and likely
  parameter-dialog flows are distinguishable from generic unknown forms.
- `object-use-checklist.json` casing is verified against a fresh run; code only
  changes if drift still exists.

## In Scope

1. Usage-classification improvements for switchboards, menus, startup forms,
   ribbon-driven navigation, and related launcher helpers.
2. Explicit out-of-band entry uncertainty in usage output and checklist
   questions.
3. Query-parameter metadata plumbing into the usage-side IR and classifiers.
4. Heuristics for likely parameter-collection forms and report-open/query-open
   flows when the evidence is present.
5. Regression tests for launcher/menu patterns and prompt-parameter patterns.
6. Fresh-output verification of `object-use-checklist.json` casing.

## Out Of Scope

- Reopening cleanup semantics that were already narrowed to positive stale-like
  signals.
- Duplicate-table-shape detection for cleanup candidates.
- Obsolete-module-header detection from leading VBA comments.
- New UI tooling or checklist-editor changes.

---

## Sessions

### Session 1 of 3: Launcher Surfaces + Out-Of-Band Entry Uncertainty

**Steps:**

1. Read `docs/planning/harvester-upstream-feedback.md` and
   `docs/planning/access-domain-notes.md`, then inspect the current usage-side
   seams (`IrDatabase`, `IrLoader`, classifier/question writer surfaces) and the
   navigation/startup inputs already available under
   `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs`.
2. Define the explanation contract for launcher/menu shells and for missing
   harvested opener evidence. The core rule is: lack of an opener is an
   uncertainty to explain, not a cleanup hint.
3. Thread the relevant navigation/startup signals into usage classification so
   switchboards, report menus, startup forms, and related helper artifacts can
   be recognized as navigation surfaces.
4. Add explicit output signals and question text for direct navigation,
   startup-entry, ribbon-entry, and other out-of-band access paths.
5. Add regression tests for a switchboard-style form plus helper macro/table and
   for a modal report-menu surface.
6. Run the narrowest relevant tests first, then `dotnet build` and `dotnet
   test` sequentially if broader validation is still needed.
7. Cross-provider verification.
8. Closeout: update `session-state.json`, append actuals to `ai-assignment.md`.

**Likely Touches:**

- `AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs`
- `AccessHarvester.Pipeline/UsageIdentification/IrLoader.cs`
- `AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs`
- `AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs`
- `AccessHarvester.Pipeline/UsageIdentification/UsageChecklistWriter.cs`
- tests under `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/`

**Ends With:** usage output can classify launcher/menu shells and can describe
missing opener evidence as out-of-band uncertainty instead of unexplained likely
unused behavior.

---

### Session 2 of 3: Parameterized Queries + Parameter-Dialog Flows

**Steps:**

1. Read the Session 1 output and inspect the existing Access-layer parameter
   model in `AccessHarvester.Access/QueryMetadata.cs` plus the usage-side query
   model/loader.
2. Widen the usage-side IR so saved-query parameter metadata survives into the
   classifier instead of being dropped during load.
3. Add explicit signals for prompt-style/bracketed parameters, declared
   `PARAMETERS` blocks, and other evidence that a query/report expects runtime
   input.
4. Add heuristics for likely parameter-collection forms or report-open flows
   when the harvested evidence supports that interpretation.
5. Teach the question generator and checklist writer to express parameter
   uncertainty distinctly from generic unknown intent.
6. Add regression tests covering a query with bracketed prompt parameters and a
   likely parameter-dialog flow.
7. Run the narrowest relevant tests first, then `dotnet build` and `dotnet
   test` sequentially if broader validation is still needed.
8. Cross-provider verification.
9. Closeout: update `session-state.json`, append actuals.

**Likely Touches:**

- `AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs`
- `AccessHarvester.Pipeline/UsageIdentification/IrLoader.cs`
- `AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs`
- `AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs`
- tests under `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/`

**Ends With:** parameterized queries and likely parameter-dialog flows surface
as explicit usage signals instead of being lumped into generic uncertainty.

---

### Session 3 of 3: Pilot, Fresh-Output Verification, And Closeout

**Steps:**

1. Run `identify-usage` against committed sample or golden output that exercises
   launcher/menu and parameterized-query cases, including HomeHealthCare if the
   fixture is already present in-repo.
2. Review the regenerated `object-use.json` and `object-use-checklist.json` for
   whether launcher explanations and parameter flows now read clearly.
3. Verify `object-use-checklist.json` casing against the fresh run. If the file
   already emits canonical PascalCase, record the verification and leave the
   writer alone. Only patch code if a current run still shows drift.
4. Calibrate wording and thresholds if the pilot exposes local defects; keep the
   scope constrained to the launcher/parameter slices from Sessions 1 and 2.
5. Update the relevant planning or consumer docs if the generated artifacts now
   expose new concepts that operators should understand.
6. Run `dotnet build` and `dotnet test` sequentially.
7. Cross-provider verification of the output quality and the "verification-only
   unless broken" handling of checklist casing.
8. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, update `session-state.json`, and notify
   the maintainer.

**Ends With:** the remaining upstream feedback items are either implemented and
verified or explicitly deferred with fresh evidence, and the checklist-casing
question is closed based on a current upstream run rather than stale output.

---

## Stop Rules

- Do not fold duplicate-table-shape or obsolete-module-header cleanup heuristics
  into this set unless the maintainer explicitly expands scope.
- Do not treat missing inbound references as deletability evidence.
- Do not change `object-use-checklist.json` casing code unless a fresh run shows
  that the current upstream writer still drifts.