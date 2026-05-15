# Workflow Package: Add a Client

> **Slug:** `add-a-client` (filename: `packages/add-a-client.md`)
> **Pilot session:** Session 1 of 2
> **Author orchestrator:** Codex GPT-5
> **Reviewer (when applicable):** Claude / other-provider Session 2 peer review
> **Source DB:** `samples/HomeHealthCare.accdb`
> **Source IR:** `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/`
>
> Open Decisions inherited from `workflow-package-pilot`:
>
> - **Source operationalization format:** structured prose
> - **Component / role mapping format:** generic-now

---

## 1. Workflow summary

Staff add a new client from an unbound search-and-list surface that launches a bound client-detail form. The workflow's business outcome is a persisted `tblClients` row with required identity and intake fields present, defaults applied from the data model, and basic validation enforced before save. Compared with Northwind's bound list/detail pattern, this workflow leans much more heavily on explicit search filters, `OpenArgs`-driven detail launch, and field-level input hygiene on the detail form itself.

## 2. Actors and entities

- **Actors:** Internal home-healthcare staff user; no user/role model or explicit login gate was observed in this path.
- **Primary entity:** Client.
- **Related entities:** Client budgets, client service needs, state lookup values, and downstream service-plan rows that depend on the client existing first.

## 3. Business rules with evidence + confidence tags

- **Rule:** A new client record inherits table-model defaults of `Gender = "X"`, `EnrollmentDate = Date()`, and `IsActive = True` before the row is first committed.
  - **Evidence:** `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/Tables/tblClients.ir.json`; `samples/DatabaseYaml/HomeHealthCare.accdb/Tables/tblClients.yaml` -> field defaults for `Gender`, `EnrollmentDate`, and `IsActive`.
  - **Confidence:** confirmed

- **Rule:** The client cannot be saved unless the detail form has values for its tag-marked required controls and the table-required fields are valid.
  - **Evidence:** `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/Forms/frmClientDetail.ir.json` -> `Form_BeforeUpdate`; `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml` -> `Form_BeforeUpdate`; `samples/DatabaseYaml/HomeHealthCare.accdb/Forms/frmClientDetail.yaml` shows `Tag: "Required"` on the first-name, last-name, and enrollment-date controls; `tblClients` marks `DateOfBirth` as required at the table level.
  - **Confidence:** confirmed

- **Rule:** Date consistency is enforced in two layers: `DateOfBirth` must be in the past, and `EnrollmentDate` cannot be before `DateOfBirth` or in the future.
  - **Evidence:** `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml` -> `txtDateOfBirth_BeforeUpdate`; `samples/DatabaseYaml/HomeHealthCare.accdb/Tables/tblClients.yaml` -> field validation for `DateOfBirth`, field validation for `EnrollmentDate`, and table validation rule `[EnrollmentDate] >= [DateOfBirth]`.
  - **Confidence:** confirmed

- **Rule:** The detail form applies input hygiene before save by trimming and proper-casing first and last names rather than persisting raw textbox text verbatim.
  - **Evidence:** `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml` -> `txtFirstName_BeforeUpdate`, `txtLastName_BeforeUpdate`.
  - **Confidence:** confirmed

- **Rule:** Optional contact data still has format guards: email must either be blank or resemble `*@*.*`, and `SSNLast4` must be exactly four digits when supplied.
  - **Evidence:** `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml` -> `txtEmail_BeforeUpdate`, `txtSSNLast4_Exit`; `samples/DatabaseYaml/HomeHealthCare.accdb/Tables/tblClients.yaml` -> `Email` validation rule and `SSNLast4` input-mask shape.
  - **Confidence:** confirmed

- **Rule:** The detail form applies an additional business-age rule on intake: the client must be at least 18 years old.
  - **Evidence:** `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml` -> `txtDateOfBirth_BeforeUpdate` checks `DateAdd("yyyy", -18, Date)`.
  - **Confidence:** confirmed

- **Rule:** Deleting a client is blocked when dependent budget rows exist, so the client header cannot be removed while budget children remain.
  - **Evidence:** `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml` -> `Form_Delete`; `samples/DatabaseYaml/HomeHealthCare.accdb/Relationships.yaml` -> `rel_Client_Budgets`.
  - **Confidence:** confirmed

- **Rule:** Deactivating a client is allowed but emits a warning when approved service-plan rows still exist for that client.
  - **Evidence:** `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml` -> `chkIsActive_AfterUpdate`; query over `tblClientServicePlan` + `tblClientServiceNeeds`.
  - **Confidence:** confirmed

## 4. Behavioral invariants -> characterization tests

- **Invariant:** Beginning a new client intake applies the table-model defaults for gender, active status, and enrollment date.
  - **Source procedure:** `tblClients` field defaults (`Gender`, `EnrollmentDate`, `IsActive`)
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins persisted starting state for a new client record, not which Access event happened to surface that state.

- **Invariant:** A client save is rejected when required identity/date fields are missing or when the enrollment and birth dates violate the underlying table rules.
  - **Source procedure:** `frmClientDetail::Form_BeforeUpdate`; `tblClients` field/table validation rules
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains durable persistence preconditions, not focus changes or message-box wording.

- **Invariant:** Client name normalization and field-level format guards are deterministic: first and last names are trimmed/proper-cased, malformed email is rejected, under-18 clients are rejected, and a partial SSN must be exactly four digits.
  - **Source procedure:** `frmClientDetail::txtFirstName_BeforeUpdate`; `txtLastName_BeforeUpdate`; `txtEmail_BeforeUpdate`; `txtDateOfBirth_BeforeUpdate`; `txtSSNLast4_Exit`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientValidationBehaviorTests.cs`
  - **Anti-coupling check:** These tests pin input-normalization and validation semantics rather than Access textbox event plumbing.

- **Invariant:** Client lifecycle policy on the detail form blocks delete when budget children exist and escalates deactivation with a warning when approved plans remain.
  - **Source procedure:** `frmClientDetail::Form_Delete`; `frmClientDetail::chkIsActive_AfterUpdate`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientLifecyclePolicyTests.cs`
  - **Anti-coupling check:** The invariant is about child-record safety and approval-state policy, not button clicks or dialog UI.

## 5. Source operationalization reference

1. The user opens `frmClientSearch` from the switchboard surface. The form's `OnLoad` embedded macro defaults the status filter to `Active` and places focus in the name filter textbox.
2. The user narrows the list with any combination of partial name, state, city, and active/inactive filters. `PopulateClientList` rebuilds the list over `tblClients` and prepends a synthetic add row through `basListHelpers.BuildAddableRowSource(...)`.
3. The user can reach add mode in two observed ways:
   - double-click the synthetic add row in `lstClientList`, which routes through `basListHelpers.OpenDetailFromList(...)` and opens `frmClientDetail` with `OpenArgs = "ADD"`;
   - click `btnAddNew`, which opens `frmClientDetail` with `acFormAdd`.
4. When `frmClientDetail` loads, `Form_Load` interprets `OpenArgs`. `"ADD"` moves the form to a new record; a numeric value filters the form to the selected `ClientID`.
5. `Form_Current` initializes the fiscal-year filter, updates the caption (`New Client` for add mode, `<First> <Last> - Client Detail` for edit mode), refreshes the child listboxes, and disables `btnSave` until the record becomes dirty.
6. The user enters client identity and contact data. Field handlers normalize names, validate email and birth date, enforce four-digit SSN shape, and optionally auto-fill city when a state has a single known city.
7. Clicking `btnSave` commits the dirty record. `Form_BeforeUpdate` enforces the tag-marked required controls, and the underlying `tblClients` defaults and validation rules provide the second line of defense.
8. When the detail form closes, the embedded `OnClose` macro requeries `frmClientSearch` if that form is loaded so the new or edited client is visible in the list surface.

## 6. Component / role mapping hints

- **UI pattern:** unbound search panel plus results list, launching a separate bound detail form for create/edit.
- **Role gating:** no role taxonomy or workflow-specific privilege gate was observed in the client add path; treat this as a single-user/internal workflow unless a downstream system introduces roles explicitly.
- **Component library mapping (optional):** likely maps to a searchable index/list component with a dedicated create/edit details surface rather than a Northwind-style split-form recreation.

## 7. Open questions / ambiguity log

- **Question:** Which add entry point is the canonical one the downstream builder should preserve: the synthetic add row (`OpenArgs = "ADD"` via `basListHelpers.OpenDetailFromList`) or the explicit `btnAddNew_Click` path (`acFormAdd`)?
  - **Source-of-uncertainty:** Both entry points exist in `frmClientSearch`, and they do not launch the detail form the same way.
  - **Recommended resolution path:** Human review of the running Access app to see which path users actually rely on; if both are used, preserve both in the downstream package consumer.

- **Question:** Was `DateOfBirth` intentionally left out of the form's tag-driven required-field loop even though the table requires it?
  - **Source-of-uncertainty:** `Form_BeforeUpdate` checks controls tagged `Required`, but the form metadata shows that tag on first name, last name, and enrollment date only; `DateOfBirth` is enforced by the table and field-specific handler instead.
  - **Recommended resolution path:** Human review of the Access UX to determine whether this is a deliberate split between form-level and table-level validation or an authoring oversight.

- **Question:** The broader HHC fixture is known for validation-heavy macros, but `tblClients` itself has no data macro. Is the standalone `mcrValidation` macro object meant to be reused by this form or simply available for other forms?
  - **Source-of-uncertainty:** `mcrValidation` exists as a macro object, but `tblClients` has `DataMacros: null` and `frmClientDetail`'s current save path is fully visible in form code plus table rules.
  - **Recommended resolution path:** Treat the current workflow package as form-code-plus-table-rule based. If a future harvester pass surfaces macro call sites from this form, revise the package then.
