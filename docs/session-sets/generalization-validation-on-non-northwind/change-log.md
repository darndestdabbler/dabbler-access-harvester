# Change Log — `generalization-validation-on-non-northwind`

> **Verdict:** `generalizes-cleanly` — the workflow-package shape
> proven on Northwind's `Add an Order` handled HomeHealthCare's
> `Add a Client` without structural template revision.
> **Closed:** 2026-05-15
> **Sessions:** 2 of 2
> **Routed cost (set total):** see Session 8 cost report (target was
> ~$0.20–$0.40 across the set; both sessions stayed inside the
> outsource-last envelope).

---

## What this set delivered

1. **Inventory of HHC `Add a Client` artifacts** —
   `inventory.md`. Located forms (`frmClientSearch`,
   `frmClientDetail`), helpers (`basListHelpers`, `basUtilities`,
   `basDataOperations`), bound table (`tblClients`), related
   listbox/popup detail flow, and behavior-bearing procedures.
   Confirmed empirically (correcting an early assumption) that
   `tblClients` has no data macro — the workflow is enforced by
   form code plus table-level validation rules.

2. **`Add a Client` workflow package** —
   `packages/add-a-client.md`. Authored in Session 1 by Codex
   GPT-5 against the seven-section template established by
   `workflow-package-pilot`. Refined in Session 2 (Claude Opus
   4.7 peer review) with three additional business rules (phone
   normalization, state-driven city auto-fill, deactivation
   confirmation-with-rollback), five additional invariants
   covered by falsification tests, two additional ambiguity-log
   entries, and tightened operationalization prose.

3. **Characterization tests** —
   `tests/AccessHarvester.CharacterizationTests/AddAClient/`.
   Session 1 added `ClientCreationBehaviorTests`,
   `ClientValidationBehaviorTests`, and
   `ClientLifecyclePolicyTests` (plus `AddAClientWorkflowModels`
   as the shared `ClientDraft` DTO) using the path-b-probe
   pattern. Session 2 added `Session2FalsificationTests` with
   nine tests covering future-enrollment-date gating, phone
   normalization determinism, state-city auto-fill exact-match
   policy, deactivation-cancel reversibility,
   deactivation-confirm-with-warning policy, and a tightened
   delete-blocking check across multiple positive-budget-row
   counts.

4. **Generalization-validation report** —
   `generalization-findings.md`. The headline output of this
   set. Verdict `generalizes-cleanly` with section-by-section
   fit analysis, pattern-divergence findings (five
   Northwind-absent HHC patterns, all captured cleanly), three
   low-priority harvester-coverage extensions identified for a
   future set, and an explicit recommended-next-move pointing
   at Phase 3a (Blazor update workflows) as primary plus one or
   two HHC follow-up packages as optional widening.

5. **Closeout artifacts** — this change log,
   `next-session-set-recommendation.md`, `disposition.json`.

## Strategic significance

- **Open Validation #2 in `docs/planning/strategy.md`** ("Does the
  package shape generalize to non-Northwind databases?") flips from
  `OPEN` to `RESOLVED` for the HHC structural-divergence axis. The
  strategy doc's next strategic-review pass should record this.
- **`MiniComplex.accdb` remains an open generalization probe** —
  explicitly out of scope for this set per its prerequisite
  exclusion (error-audit gate not yet cleared per
  `project-guidance.md` → Canonical Test Fixtures). When that
  fixture is unblocked, a small follow-up set can validate the
  remaining axis of #2.
- **Phase 3 sequencing pressure relieved.** The strategy doc's
  Phase 3a-vs-3c "which first" tension is now decisively in favor
  of 3a; 3c's structural risk has been answered.

## Process findings

- The cross-provider in-IDE peer-review pattern
  (`workflow-package-pilot` Sessions 2-3 path-b-probe) continued to
  earn its keep. Session 2 surfaced four invariants Session 1
  missed — same order of magnitude as the `Add an Order`
  peer-review pass. Single-pass package authoring leaves
  predictable gaps; cross-provider review reliably catches them.
- The `OpenArgs` contract — the most Northwind-divergent pattern
  in HHC — was the pattern most likely to surface a template
  misfit, and didn't. It sits naturally in operationalization
  (Section 5) plus open-questions (Section 7) without needing its
  own structural slot.
- The `tblClients`-has-no-data-macro finding (initial assumption
  vs. source-grounded reality) is a useful calibration signal:
  authoritative-looking comparison docs can be over-general about a
  fixture, and the per-table check belongs in every package's
  inventory step.

## What this set did NOT do

- Did not package other HHC workflows. Two natural follow-up
  candidates (`Add a Service Need`, `Copy from Prior FY`) are
  named in `next-session-set-recommendation.md` for the human's
  consideration.
- Did not touch `MiniComplex.accdb` or any other non-HHC fixture.
- Did not implement any Blazor code; the package is an input to a
  downstream session set, not implementation work.
- Did not extend or repopulate the AI-interpretation columns
  (`Confidence*`, `Justification*`, `Interpretation*`) on the
  symbol DB — same throwaway-under-Hybrid rule as
  `workflow-package-pilot`.
- Did not patch the harvester. Three low-priority coverage gaps
  are surfaced in `generalization-findings.md` for a future
  harvester-extension set; no in-session harvester edits were
  made.
- Did not modify `docs/planning/strategy.md`. That doc owns its
  own update cadence; this set's verdict is the input, not the
  edit.

## Cost discipline

Both sessions stayed within the outsource-last envelope:
in-IDE work for analysis, authoring, and peer review;
mandatory cross-provider session-verification per session as
the only routed cost. No `route(architecture)` calls and no
synthesis-document routing (per the
"orchestrator-matches-routing-target → handle in-conversation"
convention, applicable to both the Codex orchestrator on
Session 1 and the Claude orchestrator on Session 2).
