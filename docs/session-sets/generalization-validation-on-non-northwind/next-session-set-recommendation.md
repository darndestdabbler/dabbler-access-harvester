# Next Session-Set Recommendation

> **Authoring set:** `generalization-validation-on-non-northwind`
> **Verdict driving this recommendation:** `generalizes-cleanly`
> **Authored:** 2026-05-15 (Session 2 of 2)
> **Plan-driven repo:** yes — see
> `docs/planning/strategy.md` → Phase 3 candidates 3a–3g.

---

## Primary recommendation

**Spec a new session set: `blazor-update-workflows`** (or similar
slug per `session-set-authoring-guide.md`).

This is **Phase 3a** in `docs/planning/strategy.md`. The strategy
doc's advisory sequencing was 3a → 3b/3c → 3e → 3d → 3f → 3g.
This set just completed 3c with a `generalizes-cleanly` verdict,
which resolves the only major risk that could have pushed 3a
back in the queue. 3a is now unambiguously the next move per the
plan.

**What 3a delivers:** ships `Update an Order`,
`Update a Product`, and `Update an Employee` against the same
Blazor host established in Phase 2 (`workflow-package-pilot`).
Reuses namespace structure, HttpClient boundary, EF Core +
SQLite persistence, MudBlazor patterns, and the
characterization-test wiring. Per the Phase 2 handoff: "Once Add
an Order ships, the three update workflows reuse most of its
infrastructure."

**Why not 3c continuation (MiniComplex):** explicitly out of scope
per this set's spec — `MiniComplex.accdb` sits behind an error-audit
gate (`docs/planning/project-guidance.md` → Canonical Test Fixtures).
Until that gate clears, MiniComplex generalization can't be a
session-set deliverable.

**Why not 3b (UAT/E2E for the Phase 2 slice) first:** the Phase 2
spec explicitly deferred UAT/E2E and flagged it for revisit at
Step 9. 3b is a natural pair with 3a (single later set can add
UAT to all four workflows together once they're shipped),
which is the cheaper sequence than running 3b before 3a's
workflows even exist.

**Sizing guidance for the next set's spec author:** the Phase 2
actuals (Add an Order: 2 sessions for draft + peer review; the
three update workflows: 1 session each per the strategy doc's
sequencing comment) suggest a 3–5 session set is appropriate.
Per `session-set-authoring-guide.md`: 5+ sessions is justified
when the work has well-defined synthesis points; the three update
workflows are independent enough to land in parallel session
plans inside a single set, with a final synthesis session for the
combined change-log and any cross-workflow regressions.

**Prerequisites for 3a:**
- `workflow-package-pilot` complete (yes, closed 2026-04-30 / 2026-05-01)
- This set complete (yes, closing now)
- The four Phase 2 packages and the Blazor host are the foundation;
  no new harvester work or symbol-DB work is required.

---

## Secondary recommendation (parallel-eligible, low cost)

**One or two more HHC packages, before or in parallel with 3a.**

The verdict `generalizes-cleanly` is supported by one HHC
workflow. Widening the evidence base is cheap (the harvester
output is already on disk; the in-IDE orchestrator-pair pattern
is already validated for HHC) and would meaningfully harden the
generalization claim if any concern remains.

Two natural candidates:

1. **`Add a Service Need` for a client.** Exercises
   `frmClientServiceNeedDetail` with `OpenArgs` carrying
   `ClientID` + `FiscalYear` (a different `OpenArgs` shape from
   `Add a Client`'s `"ADD"` / numeric PK). Same listbox-cascade
   parent pattern. Confirms the `OpenArgs` contract generalizes
   *within* HHC, not just across Northwind/HHC. A small set
   (2 sessions, mirroring this set's shape).

2. **`Copy from Prior FY`** on the client detail form.
   Exercises `basDataOperations.CopyClientNeedsAndPlans` — a
   richer business procedure with replace-all semantics,
   validation against provider-service availability, and
   explicit user-confirmation gates. Confirms the package shape
   handles deeper business procedures, not just intake forms.
   The closer HHC analog to Northwind's `Add an Order`
   complexity. A small set (2 sessions).

Either can run in parallel with 3a via the
bare-repo + flat-worktree layout
(`dabbler-ai-orchestration/docs/planning/repo-worktree-layout.md`) —
disk state stays separate, merges happen explicitly when each
set finishes.

**The human picks one of:**
- 3a only (fastest path to a real Northwind end-to-end app slice)
- 3a + one HHC follow-up (parallel; broader generalization
  evidence)
- 3a + both HHC follow-ups (parallel; full HHC `OpenArgs`-shape
  coverage + a deeper-procedure exemplar)

---

## Deviation from plan

None. This recommendation aligns with `strategy.md` Phase 3a as
the named primary next move, with the secondary recommendation
sitting in the "widen HHC evidence" gap that the strategy doc
acknowledges but did not enumerate. No re-prioritization away
from the plan; only an optional add-on the human can take or
leave.

---

## Strategy-doc updates implied (not made in this set)

- Open Validation #2 status should flip from `OPEN` to `RESOLVED
  for HHC; MiniComplex still open` in the next strategic-review
  pass.
- Phase 3 sequencing should be updated to reflect that 3a is now
  unambiguously next (3c-as-blocker is resolved).
- The "Phase 3 cost depends on scale and is not estimated here"
  caveat can stay; 3a's actuals will inform the next estimate.

These updates land in the strategy doc on its own cadence (per the
strategy doc's own update protocol — it documents its own update
schedule). This set is the input, not the edit.
