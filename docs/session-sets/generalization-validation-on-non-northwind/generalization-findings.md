# Generalization Validation Findings — HomeHealthCare `Add a Client`

> **Set:** `generalization-validation-on-non-northwind`
> **Authored:** 2026-05-15 (Session 2 of 2)
> **Reviewer:** Claude Opus 4.7 (cross-provider peer review against the
> Session 1 / Codex GPT-5 draft)
> **Open Validation in strategy.md:** #2 — "Does the package shape
> generalize to non-Northwind databases?"

---

## Verdict

**`generalizes-cleanly`**

The seven-section workflow-package template authored by
`workflow-package-pilot` against Northwind's `Add an Order` handled
HomeHealthCare's `Add a Client` workflow without structural revision.
Section structure carried across cleanly; the divergent HHC patterns
(unbound listbox-cascade search, `OpenArgs`-driven popup detail
launch, form-code-plus-table-rule enforcement instead of data macros,
mixed DAO + ADODB persistence) all expressed naturally within the
existing section taxonomy. Open Validation #2 in `strategy.md` flips
from `OPEN` to `RESOLVED` for the HHC structural-divergence axis;
`MiniComplex` remains as a separate follow-up validation behind its
own error-audit gate.

---

## Template Fit (Section-By-Section)

| Section | Fit | Evidence |
|---|---|---|
| 1. Workflow summary | **Clean** | One paragraph captures HHC's search-launch-detail flow alongside the Northwind contrast (bound list/detail) without forcing terminology that doesn't apply. |
| 2. Actors and entities | **Clean** | "Internal home-healthcare staff user; no user/role model or login gate observed" sits naturally in the existing slot. The single-user nature of HHC vs Northwind's role-shape was not a structural issue. |
| 3. Business rules with evidence + confidence tags | **Clean (after Session 2 additions)** | Eight rules captured in Session 1; Session 2 added three: phone normalization, state-driven city auto-fill, and the deactivation-confirmation-with-rollback policy. The "evidence + confidence" shape was the natural way to express each. The phone-normalization rule's exact mask was initially flagged as `inferred` pending a read of `basUtilities::FormatPhone`; after the read it landed as `confirmed`, leaving every Session-2 rule at `confirmed`. No rule had to be omitted or distorted to fit. |
| 4. Behavioral invariants → characterization tests | **Clean** | Session 1 pinned four invariants with concrete C# stub tests. Session 2 added five falsification entries (future-enrollment date gating, phone normalization determinism, state-city auto-fill exact-one-match policy, deactivation-cancel reversibility, deactivation-confirm-with-warning), still expressed within the section's evidence + anti-coupling slot. No invariant required a new sub-structure. |
| 5. Source operationalization reference | **Clean** | The structured-prose default (per `workflow-package-pilot` Decision 1) accommodated HHC's listbox-cascade plus popup `OpenArgs` launch without strain. The numbered step list captured both add-mode entry points (synthetic add row vs. `btnAddNew`) and the `OnClose` requery-of-`frmClientSearch` round trip. State-machine work would benefit from UAT DSL per the strategy review, but `Add a Client` is not state-machine work. |
| 6. Component / role mapping hints | **Clean** | Generic-now (per `workflow-package-pilot` Decision 2) expressed HHC's missing role taxonomy naturally as "no role gating observed; treat as single-user/internal workflow." The "library-specific upgrade" path remains a separate Phase 3e move and was not needed to keep the section honest. |
| 7. Open questions / ambiguity log | **Clean** | Three questions in Session 1 (canonical add-entry-point choice; deliberate-or-oversight on `DateOfBirth` tag; whether the standalone `mcrValidation` macro object is intended for `frmClientDetail` reuse); Session 2 added one more (deliberate-or-defensive on state-city auto-fill) after the in-flight `FormatPhone` open-mask question was resolved by reading `basUtilities`. All four sit comfortably in the existing slot; no new categories of ambiguity were required. |

**Bottom line:** zero sections required structural revision. The
template that worked for Northwind's bound master-detail order flow
worked for HomeHealthCare's unbound listbox-cascade intake flow.

---

## Pattern Divergence Findings

HHC exercised five Northwind-absent patterns. Each is reported as
captured-cleanly, partially-captured, or missed.

1. **Unbound listbox-cascade search** (`frmClientSearch` →
   `PopulateClientList` over `tblClients` with synthetic add row).
   - **Captured:** cleanly, in Section 5 step 2 + step 3.
   - **Notes:** the synthetic add row (PK `-1`) and the
     `basListHelpers.BuildAddableRowSource` shape are documented in
     `inventory.md`; the package treats them as
     operationalization-bearing rather than business-rule-bearing.
     Correct call.

2. **`OpenArgs` contract for `Add a Client`** (`"ADD"` vs numeric
   PK).
   - **Captured:** cleanly, in Section 5 step 4 plus Section 7 open
     question on which add entry point is canonical.
   - **Notes:** this was the most structurally Northwind-divergent
     pattern in HHC and the template handled it natively. No section
     was forced to invent fields. The pipe-delimited
     `BuildContextArgs` payload variant exists in `basListHelpers`
     and is exercised by sibling workflows
     (`frmClientServiceNeedDetail`, `frmBudgetServicePlan`) but is
     **not** exercised by `Add a Client` itself — confirmation of
     the broader `OpenArgs` contract's generalization is a
     `Add a Service Need` (or similar) follow-up rather than a
     finding of this set.

3. **Form-code-plus-table-rule enforcement instead of data macros**
   (`Form_BeforeUpdate` tag-driven required-field loop; field-level
   `BeforeUpdate` handlers; table-level validation rules).
   - **Captured:** cleanly, with the additional Session 2 nuance
     that the `EnrollmentDate <= Date()` future-date check lives at
     the table level and was added explicitly to Section 3.
   - **Notes:** the broader HHC fixture is known for
     validation-heavy data macros (per
     `docs/homehealthcare-northwind-comparison.md`), but
     `tblClients` itself has `DataMacros: null`. Session 1
     correctly flagged this as a `tblClients`-specific finding and
     resisted generalizing. Session 2 confirmed it. This is the
     correct shape for the package.

4. **Mixed DAO + ADODB persistence** (DAO for the main read path;
   ADODB for `basListHelpers.PopulateAddableListBoxFromAdo` over
   listbox row sources).
   - **Captured:** partially, in `inventory.md`'s `basListHelpers`
     entry, but not surfaced as a rule in the package. **Acceptable
     omission** — this is a persistence-mechanism detail, not a
     business invariant. Downstream consumers do not need to know
     the source app mixed access libraries; they need to know what
     the workflow accomplishes.

5. **Hungarian naming throughout** (`frmClientDetail`,
   `txtFirstName`, `cboStateCode`, `chkIsActive`, `lstClientList`,
   `basListHelpers`, `tlkpStates`).
   - **Captured:** orthogonal to the template. The package
     references procedures by their real names; no section was
     distorted by the naming convention.

**No divergent pattern forced field invention or structural
template revision.** Pattern (4) was the only one with measurable
"the template doesn't explicitly track this" friction, and that
friction is the right answer — persistence mechanism is downstream
implementation detail, not workflow-package content.

---

## Harvester Coverage Findings

The Session 1 harvester run against `samples/HomeHealthCare.accdb`
succeeded cleanly. The peer review surfaces specific coverage
observations:

### Surfaced well

- **Forms** (`frmClientSearch`, `frmClientDetail`): `RecordSource`,
  event bindings, embedded `OnLoad` / `OnClose` macros, child
  listbox row sources, and navigation properties came through to
  `ir/` without manual recovery. The IR was directly usable for
  Section 5 operationalization authoring.
- **Tables** (`tblClients`): field-level `Required`, `DefaultValue`,
  `ValidationRule`, and the table-level
  `[EnrollmentDate] >= [DateOfBirth]` rule all came through cleanly.
  The `DataMacros: null` finding was visible in the YAML, which
  enabled the correct "no data macro on this table" assertion.
- **Standard modules** (`basListHelpers`, `basUtilities`,
  `basDataOperations`): translated cleanly into
  `ModulesAndClasses/*.ir.json`. Procedure signatures and bodies
  were readable.
- **Relationships** (`rel_State_Client`, `rel_Client_Budgets`):
  visible in `Relationships.yaml`; supported the delete-blocking
  invariant evidence.
- **Form code-behind**: visible inline in the form's `ir.json`
  `VbaCode` field. For long readability the YAML module dumps
  (`samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_*.yaml`)
  are easier to navigate, but the IR carries the same content.

### Required cross-referencing

- **Control-level metadata** (e.g., the `Tag = "Required"` markers
  driving `Form_BeforeUpdate`): present in the binary form YAML but
  not surfaced in a directly text-searchable form. The peer review
  had to read the IR's `VbaCode` to confirm the tag-driven
  validation loop, then cross-reference Session 1's package claim
  that the tag is on first-name, last-name, and enrollment-date
  controls. The claim is empirically supported by the form's
  observed behavior, but the tooling does not let a reader confirm
  the exact tagged-control set without opening the form in Access.
- **Helper module bodies** (e.g., `FormatPhone` in `basUtilities`):
  available in the IR `ModulesAndClasses/*.ir.json` files, but the
  call site (`txtPhone_AfterUpdate` in `Form_frmClientDetail`) does
  not provide a forward-reference link to the defining module. The
  Session 2 peer review had to grep for `FormatPhone` across the
  YAML mirror to confirm its body; a directly text-navigable symbol
  index would have made the upgrade from `inferred` to `confirmed`
  mechanical rather than manual.

### Gaps to consider for a future harvester-extension set

1. **Control-property surface area in IR.** Surfacing form
   `Controls[*].Tag` (and a small set of related properties:
   `ControlType`, `ControlSource`, `Visible`, `Enabled`,
   `ControlName`) in a text-friendly `ir.json` form would close the
   "manually open Access to confirm tag set" gap. This is the
   highest-value coverage extension surfaced by this set.
2. **Forward-reference index for helper procedures.** A
   `WhichProcedureDefines("FormatPhone")` answer in the IR (or a
   simple symbol-name → defining-module map) would let readers
   confirm helper-call provenance without grepping module bodies.
   Useful but lower priority than (1).
3. **Standalone macro object call-site inference.** `mcrValidation`
   exists as a standalone macro object; whether it is invoked from
   form / table / module code is not directly visible in `ir/`.
   Would matter more for workflows whose enforcement layer
   actually uses such macros; for `Add a Client` it's
   confirmed-not-relevant.

None of these gaps blocked Session 1's or Session 2's work. They
are "what a future harvester-extension set could close to make the
next non-Northwind validation faster," not "what blocked this
set."

---

## Recommended Next Move

Given the `generalizes-cleanly` verdict, the strategy doc's open
Validation #2 is resolved (for HHC structural divergence). The
recommended next move follows the strategy doc's Phase 3 candidates
under that resolution:

**Primary recommendation: Phase 3a — Implement the three remaining
Northwind pilot workflows in Blazor.**

Rationale: the package-shape generalization risk that 3c was
designed to surface has been answered. The advisory sequencing in
`strategy.md` (3a → 3b/3c → 3e → 3d → 3f → 3g) puts 3a first
precisely because Phase 2 proved the strategy on the richest
behavioral surface (Add an Order); 3c then probes generalization,
which has now passed. Phase 3a is the next natural move: ship
Update an Order, Update a Product, and Update an Employee against
the same Blazor host established in Phase 2. The infrastructure
reuse is high; the package-to-implementation pipeline is exercised
on more workflows; and the four-workflow Northwind end-to-end
becomes a real working application slice.

**Secondary recommendation (parallel-eligible, low cost): one or
two more HHC packages.**

If the human wants to widen the generalization evidence base
before fully committing to 3a, two HHC follow-up workflows are
natural candidates:

1. **`Add a Service Need` for a client.** Exercises the
   `frmClientServiceNeedDetail` popup with `OpenArgs` carrying
   `ClientID` + `FiscalYear` (vs. `Add a Client`'s `"ADD"` /
   numeric PK). Different `OpenArgs` shape, same listbox-cascade
   parent pattern. Confirms the `OpenArgs` contract generalizes
   within HHC, not just across Northwind/HHC.

2. **`Copy from Prior FY`** on the client detail form. Exercises
   `basDataOperations.CopyClientNeedsAndPlans` — a richer business
   procedure with replace-all semantics, validation against
   provider-service availability, and explicit user confirmation
   gates. Confirms the package shape handles deeper business
   procedures (not just intake forms). This is the closer HHC
   analog to Northwind's `Add an Order` complexity.

Both are cheap (the harvester output is already on disk from
Session 1; the analytical work is the in-IDE orchestrator-pair
pattern this set already used). The human can authorize one, both,
or neither depending on whether they want broader HHC evidence
before committing to 3a.

**Out of scope here, but worth flagging:** `MiniComplex.accdb`
generalization remains unaddressed by this set per the spec's
explicit non-goals. It can return as a separate set after the
fixture's error-audit gate clears. The strategy doc's Validation #2
status should note this — HHC-resolved, MiniComplex-still-open.

---

## What Did NOT Generalize Cleanly (Honest Negatives)

The verdict is `generalizes-cleanly` rather than `generalizes-perfectly`
because three honest caveats deserve to be on record:

1. **Session 1 missed four invariants.** Phone normalization,
   state-city auto-fill, future-enrollment-date gating, and the
   deactivation-cancel-restores-active policy were absent from the
   draft. Session 2 added them. This is a **process** finding
   (single-pass authorship has gaps that peer review catches), not
   a **template** finding (the template had a slot for each). The
   `workflow-package-pilot` Session 3 review of `Add an Order`
   found four missing invariants too — same magnitude, same kind.
   The cross-provider peer review pattern continues to earn its
   keep.

2. **`OpenArgs` parsing isn't natively expressible as an
   invariant.** The invariant "the detail form interprets
   `OpenArgs` as either `"ADD"` or a numeric PK" is captured in
   Section 5 (operationalization) and Section 7 (open question on
   which add entry point is canonical), not Section 4
   (invariants). This is the **right** call — `OpenArgs` is a
   launch-protocol detail, not a business invariant — but it
   means the package's invariant section doesn't help a downstream
   builder reproduce the launch-time contract. The downstream
   builder reads Section 5 for that. Acceptable, but worth
   knowing.

3. **`FormatPhone`'s exact mask required a Round-2 follow-up.**
   Session 2 initially pinned only "phone is deterministically
   normalized" and pushed the exact mask to Section 7 as an open
   question. The Round-1 cross-provider verifier correctly flagged
   that the falsification test hardcoded a mask the package did
   not claim to have read. The fix was straightforward — read
   `basUtilities::FormatPhone` (visible in the YAML mirror),
   upgrade the package rule to `confirmed` with the exact mask,
   align the test stub to the source, and drop the resolved
   ambiguity-log entry. The honest negative here is the **process**
   shortcut, not a template gap: a workflow-package author who is
   going to put a mask-specific test in the suite owes the package
   the same level of evidence the test asserts. The template
   already had the slot for it.

None of these caveats reach the threshold for
`revise-and-retry`. They are notes for whoever authors the next
non-Northwind package, plus a small calibration signal for any
future template-revision set.

---

## Summary

- **Verdict:** `generalizes-cleanly`.
- **Template:** zero sections required structural revision.
- **Pattern divergence:** five Northwind-absent HHC patterns,
  all captured; one omission (DAO+ADODB mix) is acceptable on
  workflow-content grounds.
- **Harvester:** clean extraction; three low-priority coverage
  extensions identified for a future harvester-extension set.
- **Process:** cross-provider peer review still earns its keep
  (four missing invariants per pass, consistent with Northwind
  experience).
- **Strategic next move:** Phase 3a (Blazor update workflows).
  Optional widening: one or two more HHC packages before
  committing to 3a.
- **`MiniComplex` remains unresolved** as a separate
  generalization probe behind its own error-audit gate.
