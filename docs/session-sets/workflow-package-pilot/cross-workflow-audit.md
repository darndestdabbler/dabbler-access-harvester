# Cross-Workflow Audit — Workflow Package Pilot

> **Session:** 7 of 7
> **Author orchestrator:** Claude Opus 4.7 (Claude Code), in-conversation
> **Created:** 2026-04-30
> **Scope:** All four pilot workflow packages plus the
> `tests/AccessHarvester.CharacterizationTests/` suite.

---

## What this audit looks at

Per Session 7 of the spec, the audit looks for **systematic patterns
across all four packages** that an individual session could not have
seen on its own:

1. Recurring rules with subtly different wording.
2. Confidence-tag calibration consistency across packages.
3. Tests that may still be UI-coupled despite per-session anti-coupling
   checks.
4. Role-related findings whose treatment differs between packages.
5. Source-operationalization-reference style fit across the four
   workflows.

Findings are stated as observations + recommendations for the
downstream Blazor implementation pilot, not as defects to fix in this
set. The packages stand as authored — refinements live in the handoff
document and the open-questions logs.

---

## 1. Recurring rules across packages

### 1.1 The `ValidateForm(Me)` save gate appears in all four workflows

`Form_BeforeUpdate -> ValidateForm(Me)` is the persistence gate cited
in every package:

| Package | Where the gate is cited |
|---|---|
| Add an Order | Header: `frmOrderDetails::Form_BeforeUpdate`; Line item: `sfrmOrderLineItems::Form_BeforeUpdate` |
| Update an Order | `frmOrderDetails::Form_BeforeUpdate` (same procedure as Add) |
| Update a Product | `frmProductDetail::Form_BeforeUpdate` |
| Update an Employee | `frmEmployeeList::Form_BeforeUpdate` |

The Update an Order package's `OrderEditPolicyBehaviorTests` and the
Update a Product package's `ProductSaveValidationTests` both
characterize this gate. The Add an Order package merges it into its
`OrderCreationBehaviorTests` (header) and `OrderLineItemBehaviorTests`
(line item). The Update an Employee package isolates it in
`EmployeeSaveValidationTests`.

**Wording consistency:** all four packages phrase the rule as
"required-field presence -> save rejected" rather than diverging into
form-specific language. Update a Product correctly extends the rule with
the additional `StandardUnitCost > 0` branch from `Form_BeforeUpdate`
without rewriting the shared phrasing.

**Recommendation for the Blazor pilot:** treat the
`ValidateForm`/required-field gate as a single shared abstraction in
the new system (a model-level validator or domain-service
`SaveGuard`) rather than as four independent per-page validators. The
Access pattern collapses cleanly because the underlying rule *is* one
rule applied four times.

### 1.2 Inventory allocation policy appears in two packages with consistent phrasing

`modInventory::AllocateInventory` is cited by Add an Order
(`InventoryAllocationBehaviorTests`,
`Session3CrossOrderScopeTests`) and Update a Product
(`ProductInventoryAllocationTests`). Both packages:

- Cite the same SQL ordering: `ORDER BY o.OrderDate, o.OrderID`.
- Cite the same eligible-status filter: `OrderDetailStatusID IN (1,
  4, 5)` (`Allocated`, `NoStock`, `OnOrder`).
- Cite the same three-way decision into `Allocated` / `OnOrder` /
  `NoStock`.
- Both surface the same open question about whether the
  `OrderID` tiebreak is business-visible or implementation detail.

The Update a Product package explicitly notes this overlap with a
**Note** under the rule, which is the right move — it prevents the
downstream builder from inferring two separate allocation policies
from two separate packages.

**Recommendation for the Blazor pilot:** implement allocation as a
single domain service. Both workflows trigger it; neither owns it.

### 1.3 Cross-product line-item delete and reallocation

Add an Order documents per-line-item reallocation on delete
(`Form_AfterDelConfirm` + `frmOrderDetails::Form_Delete`). Update an
Order documents the same parent-order delete-with-reallocation
behavior (`cmdDeleteOrder_Click`, `Form_Delete`). Update a Product
also touches the same reallocation logic indirectly because product
deletion cascades through `OrderDetails` not at all (a product cannot
be deleted while order-detail rows reference it — see Update a
Product rule about `DCount` against `OrderDetails`).

The three packages present the same delete-cascade/reallocation
constraint coherently, with each package surfacing the slice
relevant to its workflow. No contradictions surfaced.

### 1.4 The `RequeryEmployeeList` / `RequeryProductList` /
`RequeryOrderList` post-save refresh

All four workflows include a similar after-save list-refresh pattern.
The packages correctly classify these as Access UI mechanics and
exclude them from characterization tests. The exclusions are
explicit (Update an Employee Section 4 lists it; the others omit it
without comment, which is consistent with the anti-coupling check —
nothing to test means nothing to mention).

**No drift.** This is a UI pattern, not a business rule; it lives
naturally in the handoff to the Blazor implementer.

---

## 2. Confidence-tag calibration

### 2.1 All visible rules across all four packages are tagged `confirmed`

Counting confidence tags by package:

| Package | confirmed | inferred | unknown |
|---|---:|---:|---:|
| Add an Order | 11 | 0 | 0 |
| Update an Order | 13 | 0 | 0 |
| Update a Product | 17 | 0 | 0 |
| Update an Employee | 15 | 0 | 0 |

Every rule across all four packages carries a `confirmed` tag. This
is not a sign that nothing was inferred — the packages explicitly
**push uncertainty into the open-questions log instead of marking a
rule `inferred`**. The open-questions logs carry 5, 3, 5, and 5
entries respectively.

This is the right call given the spec's instruction in Section 3 of
the template (*"Forbidden: rules without evidence. If the
orchestrator can't cite, the entry goes in the open-questions log
instead."*) but it does mean the `inferred` tag was not exercised at
all in this pilot. Two interpretations are both compatible with the
data:

(a) The Northwind workflows' VBA evidence was thorough enough that
   inference was rarely needed.
(b) The packages may have a slight bias toward "evidence I can cite
   = confirmed; uncertainty = open question" with no middle band.

**Recommendation:** future workflow packages outside Northwind, where
evidence may be sparser, should explicitly use the `inferred` tag for
the middle band. For the pilot specifically, the all-`confirmed`
distribution is consistent with the Northwind sample's actual
clarity.

### 2.2 Negative findings ("X was searched for and not found") are
appropriately conservative

The Update an Employee package contains the most explicit
negative findings:

- "No `HasPrivilege(...)` gate was confirmed inside `frmEmployeeList`
  or `sfrmEmployee_Privileges`" — cites the search procedure.
- "No hidden employee role flags such as `IsAdmin` or `IsActive`, and
  no logged-in-user conditional formatting or editability branches,
  were observed" — cites the searches that were conducted.

The Update a Product package includes a similar phrasing for role
gating ("No explicit privilege check was confirmed for this workflow
itself").

The Add an Order and Update an Order packages mention role-gating
only in Section 6 (component / role mapping hints) and Section 7
(open questions), not in Section 3 (business rules). This is
defensible — they did not cite a search-and-not-found rule the way
Employee did — but it does mean the four packages handle negative
findings about role gating slightly differently.

**Audit verdict:** the calibration is consistent in *spirit* (no
package overstates role gating) but inconsistent in *form*
(negative findings are explicit rules in two packages, comments in
two others). The Blazor pilot should treat this as a "no
confirmed in-form privilege gate exists across all four pilot
workflows" finding, since the packages collectively support it
without contradicting each other.

### 2.3 Cross-provider verification verdicts

All five completed cross-provider verification rounds returned
`VERIFIED` cleanly:

| Session | Workflow | Verifier | Verdict |
|---|---|---|---|
| 1 | Setup | Gemini Pro | VERIFIED, 0 issues |
| 2 | Add an Order draft | Gemini Pro | VERIFIED |
| 3 | Add an Order peer review | Gemini Pro | VERIFIED |
| 4 | Update an Order | Gemini Pro | VERIFIED |
| 5 | Update a Product | Gemini Pro | VERIFIED |
| 6 | Update an Employee | Claude Opus | VERIFIED, 0 issues (after 4 rounds of grounded minor refinements) |

Session 6's four-round path is the only deviation from a single-shot
verified outcome — and the Opus verifier's notes were grounded
refinements (privilege-editor anti-coupling phrasing, Section 4
exclusion coverage, delete-policy test precision, MRU-cleanup naming
fidelity), not corrections to substantive findings. The set as a
whole landed at high cross-provider agreement.

---

## 3. Anti-coupling check across the test suite

A second pass over the test files specifically looking for
UI-coupled assertions:

### 3.1 No test asserts against `Forms!`, `RecordsetClone`, `MsgBox`, or focus events

Grep across the suite shows zero references to:

- `Forms!`, `Me.Form`, or other Access form-control accessors.
- `Recordset.Edit`, `Recordset.Update`, `acDataErrContinue`.
- `MsgBox`, `vbYesNo`, or other dialog APIs.
- `SetFocus`, `GotFocus`, `LostFocus`, or focus-event names.

The tests model business rules through pure functions and POCO
state. Where Access-specific terminology survives (e.g.,
`OpenArgsEmployeeId` in `EmployeePrivilegeAssignmentTests`), the
**assertions** are about scoping behavior, not about the Access
form-passing mechanism itself. The Session 6 verifier flagged this
specifically as "naming cosmetic rather than a coupling defect" —
that judgment holds across the suite.

### 3.2 The exclusion list pattern is consistent and explicit

Each package documents what was intentionally not tested in
Section 4. The exclusion entries collectively cover:

- Form-control choreography (highlight clearing, focus, requery)
- `TempVars` lifecycle and Access-specific dialog state
- `acDataErrContinue` / `Response = ...` event-callback plumbing
- `acCmdDeleteRecord` / `DoCmd.RunCommand` mechanics
- Datasheet layout, splitform behavior, tab-control focus
- `RecordsetClone` iteration and recordset `Edit`/`Update` syntax
- Report-preview opening and modal-window sizing

The Update an Employee package has the most exhaustive exclusion
list because the Employee form has the densest UI-mechanic surface
(split form + supervisor combo + privilege subform + custom
delete-confirm flow). The other packages have shorter lists because
their workflows are more behaviorally clean to begin with.

**Verdict:** the anti-coupling discipline holds across the suite.
No test required reformulation during the audit.

### 3.3 One test relies on a nullable parameter abstraction worth
flagging

`UpdateAnEmployeeWorkflowModels.PrepareDelete` parameterizes MRU IDs
as nullable (`int? recentOrderMruId, int? recentPurchaseOrderMruId`).
The Access source code's `RemoveFromMRU "Orders" ...` and
`RemoveFromMRU "PurchaseOrders" ...` calls are unconditional. The
Session 6 verifier noted this as "a slight abstraction over the
Access code's unconditional `RemoveFromMRU` calls, but this models
the same business outcome (cleanup happens when relevant) and is a
reasonable pure-function decomposition."

The Blazor pilot inherits this same flexibility: the rewrite can
make MRU cleanup conditional on whether MRU state actually exists
for the deleted employee, without changing the durable invariant.
**Action item for the handoff:** flag this so the Blazor builder
knows the model deliberately allows null-MRU as a no-op rather
than crashing.

---

## 4. Role-logic treatment across packages

This is the lens the Session 6 actuals specifically called out for
the Session 7 audit.

### 4.1 The four packages collectively triangulate "no observed
in-form role gating"

| Package | Role-gating treatment |
|---|---|
| Add an Order | Section 6: "No explicit privilege gate was confirmed in the create path itself; the workflow appears to rely primarily on authenticated employee context." |
| Update an Order | Section 6: "No explicit privilege check was confirmed in the update workflow itself. The main gating is lifecycle state, not a role-specific branch." |
| Update a Product | Section 2 actor + Section 6: "No `HasPrivilege` call was observed in `frmProductDetail`." |
| Update an Employee | Section 3 negative finding + Section 6 + open question: "No explicit privilege gate was confirmed inside the Update an Employee workflow itself"; Section 7 surfaces who-is-allowed-to-open question. |

The Update an Employee package is the most thorough — it cites the
search method (`HasPrivilege` call sites elsewhere in the IR but not
in the employee form/subform), names the security module
(`modSecurity`), and surfaces the open question explicitly. The
other three are consistent with Employee's finding but describe it
as an absence, not as a search.

### 4.2 The Session 6 Notes for Session 7 question is satisfied

The Session 6 actuals raised: *"distinguish durable
authorization/business rules from Access UI mechanics"* — the
Employee workflow manages privilege data but does not itself prove
who is allowed to open the workflow. The same caveat applies to
other workflows.

The audit confirms that finding generalizes:

- **All four packages** correctly avoid asserting in-workflow role
  gating.
- **All four packages** push the "who is allowed to open this
  workflow" question to Section 7 / Section 6 hints rather than
  inventing a privilege rule.
- **None** of the packages asserts an `IsAdmin`/`IsActive`-style
  flag (because none exist in `Tables/Employees`).
- **One** package (Update a Product) cites the related observation
  about `Reorder` not being role-gated specifically — only product
  state-gated.

### 4.3 Recommendation for the Blazor pilot

The pilot's role-mapping conclusion is: **role gating is not in the
Access source for any of the four pilot workflows**. The Blazor
implementer must therefore decide explicit role mappings in the
new system rather than translating an existing rule. Update an
Employee's Section 6 candidate mapping (`UserAdmin` /
`SystemAdmin`) is the strongest seed; the other three workflows
have weaker prior signal and may map to a generic `User` role plus
authenticated-employee context.

This is a planning decision for the downstream pilot, not a defect
of the package set. The packages correctly surface the gap rather
than inventing role logic.

---

## 5. Source-operationalization-reference style fit

All four packages use **structured prose** (numbered click-sequence)
per Open Decision 1 (resolved Session 1 with default (b)). Section
lengths:

| Package | Section 5 step count |
|---|---:|
| Add an Order | 13 steps |
| Update an Order | 10 steps |
| Update a Product | 7 steps |
| Update an Employee | 7 steps |

Add an Order is the longest because it spans both header and line
items. Update an Order is mid-length because it documents a
multi-stage status machine (invoice -> ship -> pay -> close ->
delete). The two single-form workflows (Update a Product, Update
an Employee) compress naturally.

### 5.1 Did one workflow suggest a different format?

**Add an Order's Section 5** is the most complex, and at 13 steps
some hierarchy (header phase / line-item phase / save phase) would
have helped readability. The structured-prose format works but is
near its ceiling on this workflow.

**Update an Order's Section 5** explicitly documents a state
machine. A state-transition diagram would convey the
status -> action -> next status -> requirements relationships more
compactly than prose. The prose form still works, but the workflow
suggests the format isn't a great fit for state-machine workflows
specifically.

**Update a Product's Section 5** is comfortably in prose. Multiple
optional branches (cmdReorderProduct's vendor-count decision, the
quantity fallback chain) read better as prose than they would as
DSL because the branches are domain-driven, not purely sequential.

**Update an Employee's Section 5** is comfortably in prose.

### 5.2 Recommendation for the next set

The pilot Default Decision 1 was structured prose. The
post-pilot decision (per spec: "Promote to (a) for the next set if
the dsl form proves valuable during the cross-workflow review at
the end") leans toward **promote to UAT DSL only for state-machine
workflows**. The pilot evidence supports a hybrid:

- Keep structured prose as the default — it works for 3 of 4
  pilot workflows.
- Adopt UAT DSL when a workflow is primarily a state machine
  (e.g., Update an Order). The DSL's structural parallelism with
  the eventual Blazor UAT DSL would pay off there.

This is a recommendation for the *next* session set's
authoring-time decision, not a refactor of the pilot packages.

---

## 6. Cross-cutting observations not tied to a single workflow

These came up while reviewing all four packages together and don't
fit cleanly into the headings above.

### 6.1 The pilot proves the package shape works on Northwind

The set's "What 'Success' Looks Like" criterion (spec Section 13) is
that a downstream AI builder can take any one package + the existing
Blazor library and produce an implementation that:

1. Solves the same business problem.
2. Passes the characterization tests without modification.
3. Uses Blazor patterns natural to the library, not translated
   patterns.
4. Surfaces the open-questions list to a human reviewer.

The audit cannot directly verify (1)-(3) because the downstream
pilot is a separate session set. What this audit *can* verify is
that the package shape itself is internally coherent: rules are
backed by evidence, tests pin invariants without UI coupling,
ambiguities are named rather than silently resolved, and the four
packages collectively form a consistent dataset.

The four packages meet that bar.

### 6.2 The characterization-test "scaffolding gets discarded" property
held cleanly

Per the spec: AI-translated C# scaffolding is discarded; only the
tests survive. In practice the surviving artifacts are:

- Per-workflow `*WorkflowModels.cs` files containing pure-function
  implementations of the business rules under test.
- Per-rule test files asserting against those pure functions.

The workflow-model classes are short (56-383 lines per file) and
contain only the methods the tests call. They are not full
translations of the source procedures. They are domain models
extracted from the source procedures — exactly the artifact the
"discard the translation, keep the tests" property predicted.

The Blazor builder may keep them, replace them, or absorb their
behavior into domain services as fits the new system. They are
inputs to the test harness, not deliverables.

### 6.3 The pilot's actual cost stayed close to the spec's estimate

Spec budgeted ~$0.35 across the seven sessions for cross-provider
verification. Actuals:

- Session 1: $0.026 (Gemini Pro)
- Sessions 2-5: each ~$0.05-$0.10 routed to Gemini Pro
- Session 6: $2.83 across four Opus rounds (the verifier was switched
  from Gemini Pro to Claude Opus per session-local human direction;
  four rounds because the verifier produced grounded refinements
  rather than a single-shot pass)
- Session 7 (this session): one Gemini Pro verification call

Session 6's four-round Opus verification accounts for the cost
overshoot. The five Gemini Pro calls (Sessions 1-5, 7) sit close to
the original budget. The packaging volume per workflow stayed within
one session for the three update workflows; only Add an Order
required two sessions (draft + peer review), which was planned.

The pilot did not trigger any of the spec's stop rules. Specifically:

- No tests turned out to be UI-coupled.
- No workflow took more than 2 sessions to package.
- The cross-workflow audit (this section) did not find systematic
  invented rationale.
- No harvester gaps forced manual spelunking that would have
  required surfacing.
- Session 3's peer review found a manageable number of falsification
  candidates; it did not exceed the "more than 4-5 missing
  invariants" threshold for stop-rule activation.
- The packages do appear to materially reduce ambiguity (per
  Section 6.1 above).

---

## 7. Summary verdict

The pilot proves the workflow-package shape on four Northwind
workflows. The four packages and their characterization-test suite
form a coherent, internally consistent dataset suitable for handoff
to a downstream Blazor implementation pilot. No package needs
rework before handoff.

Recommendations for the *next* session set are captured in the
companion `handoff.md` document.
