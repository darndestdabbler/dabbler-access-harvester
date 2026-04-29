# Decision — Path A vs. Path B at Scale

> **One-line verdict:** **Hybrid.** Path B (translated-and-testable
> C#) for any procedure that mutates observable state, branches on a
> business rule, or owns an event-handler boundary; Path A (the
> existing symbol DB substrate) for the bulk of metadata that has no
> falsifiable behavior to assert. Drive the boundary by procedure
> shape, not by file or by module.
>
> **Confidence:** Medium-high. One probe is one data point; the
> pattern that produced this verdict is consistent with the
> `vba-symbol-db` PIVOT outcome.

## What This Document Decides

The `vba-symbol-db` Session 7 PIVOT closed with two unresolved
direction questions:

- **Path A:** Continue scaling AI-interpretation passes against the
  symbol DB substrate (the ~$24, four-day arc of Sessions 4-7 was the
  pilot). Substrate is rich; the AI fills `Purpose` / `Intent` /
  `Notes` slots; downstream consumers read interpretations.
- **Path B:** Translate procedures into testable C# and let the
  test suite be the falsification mechanism for whether the
  understanding is correct. Substrate is whatever the translator
  needs; the AI authors translation + tests; downstream consumers
  read a passing test suite.

This set's three sessions probed Path B with one procedure
(`Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`) translated
by Codex (Session 2) and independently peer-reviewed plus
falsifiability-extended by Claude Opus 4.7 (Session 3). The
Session 1 cross-module resolver fix made the probe possible by
binding `GetString`, `StringFormat`, and the enum value to their
`modStrings` declarations.

## Probe Evidence

### Did Path B surface signal Path A would have missed? Yes — three concrete instances.

1. **The `Or`-eager-evaluation invariant**
   ([translation-review.md D1](peer-review/translation-review.md)).
   The translator chose a De Morgan rewrite
   (`if (!CompanyIsActive(form) && _vendorProductCount <= 0) return false`)
   that preserves VBA's eager-evaluation semantics by accident
   rather than by design. A future maintainer "simplifying" back to
   `if (CompanyIsActive(form) || _vendorProductCount > 0)` would
   silently change semantics. **Session 3 test
   `CompanyIsActive_PopulatesAllFiveCachedCounts_EvenWhenInactive`
   pins the eager-side-effect property** so the regression trips a
   test, not a customer.
   - **What Path A would have produced:** A prose `Purpose` field
     describing the procedure as "blocks edit if company has
     orders/POs or vendor products." Correct, useless for catching
     the eager-evaluation invariant.

2. **The inner-vs-outer error-handler distinction**
   ([translation-review.md U3](peer-review/translation-review.md)).
   VBA's two `clsErrorHandler.HandleError` call sites differ on the
   third argument (`True` for the outer handler, default `False`
   for the inner `CompanyIsActive` handler). **Session 3 test
   `CompanyIsActive_DelegatesErrorWithDefaultShowUserMessage_OnDaoFailure`
   catches** a hypothetical translator who hard-coded `true` to
   "match the outer call." All five Session 2 tests would still
   pass under that bug; the Session 3 test would fail.
   - **This is the closest the probe got to "a Session 3 test that
     would have forced a translation choice the Session 2 author did
     not make"** — the spec's stated goal for Session 3.
   - **What Path A would have produced:** No record of the
     parameter divergence at all. The interpretation column would
     not name the third-argument distinction; the symbol DB's
     `[References]` table records the call site but not the
     parameter values' semantic difference.

3. **The `IsFormOpen` defensive close**
   ([translation-review.md U2](peer-review/translation-review.md)).
   The VBA `If IsFormOpen("frmGenericDialog") Then DoCmd.Close ...
   End If` is preserved in the C# translation, but Session 2's
   tests do not assert "no Close when not open." A translator
   under "simplify the dialog dance" pressure would drop the
   guard. **Session 3 test
   `CboCompanyTypeIdBeforeUpdate_DoesNotCloseDialog_WhenAlreadyClosed`
   catches that simplification.**
   - **What Path A would have produced:** Likely a prose
     `Notes` field saying "checks if dialog is open before
     closing." Reads correctly, has no falsification mechanism.

The pattern: **Path B gives every behavioral invariant a
mechanically-checkable assertion. Path A gives every behavioral
invariant a paragraph of prose.** Prose is fine for navigation; it
is not a quality gate.

### What Path B did NOT surface that Path A might have

The probe did **not** produce richer documentation about *why* the
business rule exists. The translation faithfully encodes "block edit
if company is active OR has vendor products" but does not
illuminate why the business cares about that distinction. A
hypothetical Path A interpretation pass might have produced:
"Company Type changes are blocked when downstream order or
inventory records exist because changing the type would invalidate
foreign-key assumptions in the Orders / PurchaseOrders /
ProductVendors tables and break referential integrity." That kind
of WHY-summary is a real Path A asset that Path B does not
inherently produce.

**This is the strongest argument for hybrid, not pure Path B.**

## Cost Comparison

| Metric | `vba-symbol-db` (Path A pilot, Sessions 4-7) | This set (Path B probe, Sessions 1-3) |
|---|---|---|
| Routed AI cost | ~$24 (Sessions 4-7 only; full set ~$24) | ~$0.21 estimated (Session 1: $0.156; Session 2: $0.0267; Session 3: ~$0.03 verification only) |
| In-conversation token spend | Limited — most work was routed | Substantial — translation (Session 2) + peer review + tests + decision (Session 3) authored in-IDE under Anthropic Max + GitHub Copilot windows |
| Sessions | 7 (full set) | 3 |
| Procedures covered | Database-wide structural metadata; one comprehension probe procedure | One translated procedure |
| Falsifiability per procedure | Confidence column (Pearson +0.004 vs cross-provider agreement — Session 6 finding) | xUnit test suite (boolean pass/fail per asserted invariant) |

**Per-procedure cost projection at scale:**

- **Path A** (continue routed cross-provider interpretation):
  ~$0.10-$0.30 per procedure for the AI-interpretation pass
  (extrapolating from `vba-symbol-db` Session 5: 84
  re-interpretations at $7.98 ≈ $0.095 per call), plus the cost
  of the calibration sessions that proved the signal was
  uncalibrated (Session 6: $12.46 for the grading rubric that
  found the signal was uncalibrated). Net: rigor model is
  expensive AND its calibration evidence is "you are paying a lot
  for low-correlation signal."
- **Path B** (subscription-paid in-IDE orchestrator pair, one
  routed verification per session): ~$0.03 routed per procedure
  per session × ~2 sessions per procedure = **~$0.06 routed per
  procedure**, with the rest covered by the human's existing
  Anthropic Max and GitHub Copilot subscriptions which they are
  paying for whether or not these sessions consume them.

**The economics favor Path B by ~10x at the per-procedure level**
when subscription orchestrator windows are the binding constraint.
The trade-off is wall-clock time per procedure — Path B needs two
sequential in-IDE sessions plus one verification per procedure, so
a database with hundreds of procedures will take many sessions even
parallelized across two orchestrator windows.

## Recommendation: Hybrid

**Path B for procedures with falsifiable behavior:**

- Event handlers with business rules
  (`*_BeforeUpdate`, `*_BeforeInsert`, `*_BeforeDelClick`, etc.)
- State mutators (anything that writes back to the form, opens a
  dialog, mutates an underlying record)
- Validators (anything that branches on a count, a lookup, a
  user input)
- Procedures named in `EventBindings` that fire on user
  interaction

**Path A (existing symbol DB substrate) for procedures without
falsifiable behavior:**

- Pure property accessors (no branching, no side effects)
- Dead code (already flagged `is_dead = 1` by the resolver — no
  point translating)
- Simple constants/enum tables that are already structurally
  captured
- Macro actions whose behavior is already declarative in the
  macro XML

**The decision boundary at the symbol-DB level:** add a column
`is_path_b_candidate` (or compute it on demand) from
`signature_string` + `body_hash_md5` heuristics: any procedure
whose body contains a branch (`If`/`Select Case`/loop) AND has a
caller via `EventBindings` is a Path B candidate. Everything else
defaults to Path A.

**Estimated split for NorthwindDevEdition** (rough — would need an
inventory pass to confirm): ~40% of procedures are simple
accessors/wrappers (Path A is enough); ~50% are event-handler
business logic (Path B target); ~10% are dead code or pure
constants. Total Path B target: ~50 procedures × ~$0.06 =
~$3 routed for the entire database, plus subscription window
consumption that is already paid for.

## Open Follow-Ups (Hybrid)

1. **Path B pipeline session set.** Author a session set
   `path-b-translation-pipeline` that:
   - Adds `is_path_b_candidate` heuristics to the symbol-DB
     writer (or computes it on demand).
   - Builds a translation-packet generator that batches Path B
     candidates by module, so one in-IDE session can translate
     several related procedures at once instead of one per
     session.
   - Establishes the
     `tests/AccessHarvester.TranslationProbe.Tests/` project as
     the home for translated procedures (one folder per source
     module, one file per procedure), with a naming convention
     and a per-module faked-interface seam pattern.
   - Decides whether translated procedures live in the
     `tests/` tree (probe scope) or get promoted to a real
     `AccessHarvester.TranslationOutput/` project (production
     scope). This probe deliberately stayed in `tests/` to
     defer the decision.

2. **Faked-interface escalation policy.** This probe introduced
   `IFormContext`, `IComboBoxControl`, `IDaoCounts`,
   `IStringRepository`, `IDialogService`, `IErrorHandler`. A
   pipeline of translations will pile up faked interfaces across
   modules. Decide:
   - Are they a "translation surface" (one interface set per
     translated procedure, throwaway)?
   - Or a "translation contract" (a stable namespace
     `AccessHarvester.TranslationProbe.Contracts` other modules
     can depend on)?
   The probe deferred the decision because one procedure does not
   need a contract surface; ten procedures will.

3. **DAO-substitution policy.** This probe stubbed `IDaoCounts`
   with a recording fake. A pipeline of translations needs to
   decide whether to:
   - Keep faking DAO at the Path B layer and defer the real
     substitution to a downstream UI/runtime layer; or
   - Stand up an `AccessHarvester.Dao` project that wraps DAO
     `Database.OpenRecordset` / `DCount` / `DLookup` against a
     real Access connection (or a substitutable in-memory
     equivalent for tests).
   The first option keeps each Path B session cheap; the second
   builds the runtime that lets Path B output actually run.

4. **Path A pruning.** If Hybrid is the direction, the
   `Confidence*` / `Justification*` / `Interpretation*` columns
   that Sessions 4-7 of `vba-symbol-db` populated should be
   *narrowed* to the Path A scope (procedures the heuristic
   classifies as Path A candidates). Currently they exist for
   every symbol; under Hybrid most of them become noise for
   Path B candidates whose source of truth is a passing test,
   not an interpretation column. The `vba-symbol-db` PIVOT
   verdict already implies this; the Hybrid recommendation
   makes it actionable.

5. **Cross-module resolver follow-up gaps.** Session 1's resolver
   fix closed `GetString`, `StringFormat`, and the enum-value
   case. The probe surfaced two unresolved categories that are
   not strictly resolver gaps but become Path B blockers:
   - **Access object-model constants** (`acNormal`, `acDialog`,
     `acFormReadOnly`, `acForm`, `acSaveNo`). Path B has to map
     these to `IDialogService` parameters or constants somewhere;
     the symbol DB currently leaves them unresolved.
   - **`IsFormOpen` (project-wide)**. This is a standard Access
     helper that lives outside `modStrings`; the resolver fix
     does not bind it because it is genuinely outside the two
     probe modules. A full-DB extraction would resolve it.

6. **Decide who runs the Hybrid pipeline.** The probe used Codex
   for translation and Claude Opus 4.7 for peer review. The
   pipeline could:
   - Keep the same Codex-translates / Claude-reviews split (with
     verification routed to Gemini Pro per probe convention).
   - Alternate which orchestrator translates and which reviews
     across procedures (mitigates orchestrator-specific biases
     in batch).
   - Run a single orchestrator end-to-end per procedure when the
     procedure is small (skip the peer-review session). The
     probe's Session 3 finding that no test failed against
     Session 2's translation is *one* data point that the
     translator did not make a falsifiable mistake; one
     procedure is not enough to default to skipping the
     review.
   Recommend: keep the two-orchestrator split for the first ~10
   procedures, then revisit. The peer-review step's marginal cost
   is one Gemini Pro verification (~$0.03) — not worth removing
   prematurely.

## What Would Have Changed The Verdict

- If Session 3's tests had **caught a real bug** in Session 2's
  translation, the verdict would still be Hybrid but the
  recommendation would push harder on Path B (the falsifier paid
  for itself on procedure #1).
- If Session 3's tests had been **unable to find any
  under-pinned invariant** beyond what Session 2 already covered,
  the verdict would lean toward Path A (Path B's
  falsifiability premise would be weaker — if every translator
  catches every invariant on the first pass, the peer-review
  session is wasted spend).
- If the routed cost of Sessions 2 and 3 had been **comparable to
  `vba-symbol-db`'s ~$24** instead of ~$0.05, the verdict would
  lean toward Path A — Path B's main economic argument is the
  ~500x cost reduction at the per-procedure routed level, and
  losing that nullifies the recommendation.

None of those counterfactuals hit. The probe paid for itself by
producing four falsifiable test pins that the AI-interpretation
substrate does not contain, at ~1/500th the per-procedure routed
cost of the `vba-symbol-db` pilot.
