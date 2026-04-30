# Workflow Package: Update an Employee

> **Slug:** `update-an-employee` (filename: `packages/update-an-employee.md`)
> **Pilot session:** Session 6 of 7
> **Author orchestrator:** Codex / GPT-5.4
> **Reviewer (when applicable):** none; single-pass package with
> cross-provider verification only
> **Source DB:** `samples/NorthwindDevEdition.accdb`
> **Source IR:** `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/`
>
> Open Decisions in effect for this set
> (resolved Session 1, 2026-04-30):
>
> - **Source operationalization format:** structured prose
>   (numbered click-sequence in markdown). See Section 5.
> - **Component / role mapping format:** generic-now
>   (implementation-agnostic hints). See Section 6.

---

## 1. Workflow summary

Office administrators maintain the employee roster in a single split
form where they update names, titles, contact information, supervisor
links, and login identity, delete employees who have no dependent
business history, and assign elevated privileges immediately after a new
employee is created. The business outcome is an employee record that is
internally consistent, uniquely identifiable, and safely connected to
its downstream responsibilities such as orders, purchase orders, and
privilege assignments.

## 2. Actors and entities

- **Actors:** Authenticated staff with access to the employee
  maintenance surface. The workflow manages employee and privilege data,
  but no explicit `HasPrivilege(...)` gate was confirmed inside
  `frmEmployeeList` or `sfrmEmployee_Privileges`.
- **Primary entity:** Employee.
- **Related entities:** EmployeePrivileges, Privileges, Orders,
  PurchaseOrders, supervisor relationships to other Employees, and
  Titles.

## 3. Business rules with evidence + confidence tags

- **Rule:** The workflow is a list-and-edit employee-maintenance surface
  bound directly to `qryEmployees`; existing rows are edited in place
  rather than opened in a separate detail form.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `RecordSource = qryEmployees`, split-form comments in `VbaCode`;
    `Queries/qryEmployees.ir.json`.
  - **Confidence:** confirmed

- **Rule:** Employee saves are rejected when any required employee field
  is missing; the shared validation helper is the save gate for the
  form.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `Form_BeforeUpdate` (`Cancel = ValidateForm(Me)`);
    `ModulesAndClasses/modValidation.ir.json` -> `ValidateForm`,
    `IsValidForm`, `IsBoundToRequiredField`;
    `Tables/Employees.ir.json` marks `FirstName`, `LastName`, and
    `JobTitle` as required.
  - **Confidence:** confirmed

- **Rule:** Employee identity is unique both by full name and by
  `WindowsUserName` when one is assigned.
  - **Evidence:** `Tables/Employees.ir.json` -> unique indexes
    `uidxFNLN` (`FirstName`, `LastName`), `uidxLNFN`
    (`LastName`, `FirstName`), and `WindowsUserName`.
  - **Confidence:** confirmed

- **Rule:** The supervisor picker is refreshed when the current row
  changes or when the supervisor control is entered so the candidate
  list can stay record-specific.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `Form_Current` (`.cboSupervisorID.Requery`) and
    `cboSupervisorID_Enter` (comment + `Me.cboSupervisorID.Requery`).
  - **Confidence:** confirmed

- **Rule:** Entering a supervisor value that is not already in the list
  suppresses Access's default invalid-list error, clears the attempted
  supervisor selection, and can pivot directly into creating a new
  employee record for that missing supervisor.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `cboSupervisorID_NotInList`
    (`Response = acDataErrContinue`, `.cboSupervisorID.Undo`,
    optional `.Dirty = False` then `.Recordset.AddNew`).
  - **Confidence:** confirmed

- **Rule:** An employee can be deleted only when there are no related
  Orders, no related PurchaseOrders, no subordinate employees pointing
  at that employee as supervisor, and no privilege assignments for that
  employee.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `EmployeeCanBeDeleted` (`DCount` checks against `Orders`,
    `Employees`, `EmployeePrivileges`, and `PurchaseOrders`; return
    condition is the sum of those counts equals zero).
  - **Confidence:** confirmed

- **Rule:** When an employee is deletable, the workflow removes that
  employee's Orders and PurchaseOrders entries from MRU tracking before
  delete confirmation proceeds.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `EmployeeCanBeDeleted` (`RemoveFromMRU "Orders" ...` and
    `RemoveFromMRU "PurchaseOrders" ...`);
    `ModulesAndClasses/modGlobal.ir.json` -> `RemoveFromMRU`.
  - **Confidence:** confirmed

- **Rule:** A successful delete queues the employee's display name into
  `TempVars!EmployeeToDelete`, and the later confirmation prompt reads
  that accumulated value so bulk deletes can confirm the specific names
  being removed.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `EmployeeCanBeDeleted` (`TempVars.Add Name:="EmployeeToDelete"...`);
    `Form_BeforeDelConfirm`
    (`MsgBox(GetString(sDeleteEmployee, TempVars!EmployeeToDelete), ...)`,
    then clears `EmployeeToDelete` and sets
    `Response = acDataErrContinue`).
  - **Confidence:** confirmed

- **Rule:** When an employee cannot be deleted, the workflow opens a
  read-only generic dialog that explains the blocking dependency
  categories instead of letting the default Access delete failure
  surface.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `EmployeeCanBeDeleted` (`DoCmd.OpenForm "frmGenericDialog", ...,
    acFormReadOnly, acDialog, strOpenArgs`).
  - **Confidence:** confirmed

- **Rule:** Saving an employee refreshes the list form back to the same
  employee and clears required-field highlighting; moving to a current
  employee row also clears any stale validation highlights.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `Form_AfterUpdate` (`RequeryEmployeeList(Me.EmployeeID)`,
    `ValidateForm_RemoveHighlights Me`);
    `Form_Current` (`ValidateForm_RemoveHighlights Me`);
    `RequeryEmployeeList` re-finds the same `EmployeeID`.
  - **Confidence:** confirmed

- **Rule:** Creating a new employee immediately launches the privilege
  assignment subform as a dialog scoped to that employee, so elevated
  privileges can be assigned as part of the create flow.
  - **Evidence:** `Forms/frmEmployeeList.ir.json` ->
    `Form_AfterInsert` (`DoCmd.OpenForm FormName:="sfrmEmployee_Privileges",
    WhereCondition:="EmployeeID = " & lngNewEmployeeID,
    DataMode:=acFormAdd, WindowMode:=acDialog, OpenArgs:=lngNewEmployeeID`).
  - **Confidence:** confirmed

- **Rule:** When the privileges subform is opened with an employee id in
  `OpenArgs`, it defaults new privilege rows to that employee id.
  - **Evidence:** `Forms/sfrmEmployee_Privileges.ir.json` ->
    `Form_Open` (`If Nz(.OpenArgs, 0) > 0 ... .cboEmployeeID.DefaultValue = .OpenArgs`).
  - **Confidence:** confirmed

- **Rule:** The privileges workflow currently maps to a single concrete
  privilege value, `pApprovePO`.
  - **Evidence:** `ModulesAndClasses/modGlobal.ir.json` ->
    `enumPrivileges` contains only `pApprovePO = 1`;
    `Tables/Privileges.ir.json` has `RecordCount = 1`.
  - **Confidence:** confirmed

- **Rule:** No explicit privilege gate was confirmed inside the Update
  an Employee workflow itself; the form manages privilege records, but
  access to the form appears to be controlled elsewhere.
  - **Evidence:** search across
    `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/Forms`
    shows `HasPrivilege(...)` call sites in other workflows (for
    example purchase-order approval) but none in
    `frmEmployeeList.ir.json` or `sfrmEmployee_Privileges.ir.json`;
    `ModulesAndClasses/modSecurity.ir.json` defines the shared gate.
  - **Confidence:** confirmed

- **Rule:** No hidden employee role flags such as `IsAdmin` or
  `IsActive`, and no logged-in-user conditional formatting or
  editability branches, were observed in the inspected employee
  workflow artifacts.
  - **Evidence:** `Tables/Employees.ir.json` fields do not include
    `IsAdmin` or `IsActive`; searches across
    `Forms/frmEmployeeList.ir.json`,
    `Forms/sfrmEmployee_Privileges.ir.json`, and the related employee
    queries/modules did not reveal user-specific formatting branches or
    `Get_UserID`-driven edit policy inside this workflow.
  - **Confidence:** confirmed

## 4. Behavioral invariants -> characterization tests

- **Invariant:** Employee saves fail until `FirstName`, `LastName`, and
  `JobTitle` are all present.
  - **Source procedure:** `frmEmployeeList::Form_BeforeUpdate`,
    `modValidation::ValidateForm`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeeSaveValidationTests.cs`
  - **Anti-coupling check:** The invariant pins the durable save
    precondition, not yellow highlighting or message-box text.

- **Invariant:** Employee identity remains unique by full name and by
  `WindowsUserName` when one is assigned.
  - **Source procedure / artifact:** `Tables/Employees.ir.json`
    unique indexes `uidxFNLN`, `uidxLNFN`, and `WindowsUserName`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeeIdentityConstraintTests.cs`
  - **Anti-coupling check:** The invariant pins database-level identity
    rules, not datasheet sorting or split-form layout.

- **Invariant:** Employee deletion is allowed only when order history,
  purchase-order history, subordinate-employee references, and
  privilege assignments are all absent; a deletable employee triggers
  MRU cleanup before confirmation.
  - **Source procedure:** `frmEmployeeList::EmployeeCanBeDeleted`,
    `frmEmployeeList::Form_Delete`,
    `frmEmployeeList::Form_BeforeDelConfirm`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeeDeletePolicyTests.cs`
  - **Anti-coupling check:** The invariant captures the durable delete
    gate and cleanup side effects, not the generic-dialog UI or Access
    confirmation plumbing.

- **Invariant:** Creating a new employee immediately transitions into a
  privilege-assignment flow scoped to that employee, and the privilege
  editor defaults new rows to that employee id.
  - **Source procedure:** `frmEmployeeList::Form_AfterInsert`,
    `sfrmEmployee_Privileges::Form_Open`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeePrivilegeAssignmentTests.cs`
  - **Anti-coupling check:** The invariant pins the workflow handoff and
  scoping rule, not Access form coordinates or modal-window sizing.

Excluded from characterization tests:
`frmEmployeeList::cboSupervisorID_NotInList` is intentionally not
modeled as a pure-function invariant because the confirmed behavior is
mostly Access invalid-list suppression and form-navigation plumbing
(`acDataErrContinue`, `.Undo`, optional `.Recordset.AddNew`). The
rewrite decision about whether to preserve that inline create pivot is
tracked in Section 7 instead. Other confirmed-but-untested rules are
also intentionally excluded because they are UI mechanics rather than
durable business logic: `Form_Current` and `Form_AfterUpdate`
highlight-clearing, `RequeryEmployeeList` row re-selection,
`cboSupervisorID_Enter` / `Form_Current` supervisor-combo requeries,
the generic-dialog presentation details for blocked deletes, and the
specific `TempVars` lifecycle used to feed the custom delete
confirmation prompt.

## 5. Source operationalization reference

1. The user opens `frmEmployeeList`, which shows the current employee in
   detail view and the roster in the list pane because the form runs as
   a split form over `qryEmployees`.
2. As the current row changes, `Form_Current` clears stale validation
   highlights, toggles the New Employee button based on whether the row
   is new, refreshes the supervisor combo, and updates the "Recent
   Orders for ..." label for the selected employee.
3. The user edits employee fields directly on the bound row. If they
   tab into the supervisor combo, `cboSupervisorID_Enter` requeries the
   row source so the list can stay current. If they type a supervisor
   not in the list, `cboSupervisorID_NotInList` suppresses the default
   Access error and optionally pivots into a new employee record for the
   missing supervisor.
4. On save, `Form_BeforeUpdate` calls `ValidateForm(Me)`. If required
   fields are missing, the save is cancelled. On a clean save,
   `Form_AfterUpdate` re-queries the form and re-selects the saved
   employee row via `RequeryEmployeeList`.
5. When the user creates a brand-new employee, `Form_AfterInsert`
   immediately opens `sfrmEmployee_Privileges` in dialog mode filtered
   to that employee. The subform's `Form_Open` seeds
   `cboEmployeeID.DefaultValue` from `OpenArgs` so new privilege rows
   attach to the correct employee.
6. To delete an employee, the user initiates delete on the current row.
   `Form_Delete` calls `EmployeeCanBeDeleted`, which checks for linked
   Orders, PurchaseOrders, subordinate employees, and EmployeePrivileges
   rows. If any exist, the workflow opens a generic read-only dialog and
   cancels deletion. If none exist, it removes relevant MRU entries and
   queues the employee's name into `TempVars!EmployeeToDelete`.
7. `Form_BeforeDelConfirm` then shows the custom delete confirmation
   prompt using the accumulated `TempVars!EmployeeToDelete` value and
   clears that temp variable afterward. `Form_AfterDelConfirm` toggles
   the New Employee button back based on whether the remaining current
   row is new.

## 6. Component / role mapping hints

- **UI pattern:** Employee-administration page with a roster grid plus a
  detail panel, not a separate create-vs-edit wizard. The privilege
  assignment step is a natural nested editor or side panel that opens
  immediately after create.
- **Role gating:** The workflow manages security-adjacent data
  (`EmployeePrivileges`) but no explicit in-form privilege gate was
  confirmed. In a rewrite, employee maintenance and privilege-assignment
  actions likely belong to an administrative role such as
  `UserAdmin` or `SystemAdmin`, but that mapping is inferred from domain
  semantics rather than enforced by the observed form code.
- **Component library mapping (optional):** A grid-with-details admin
  page, a filtered supervisor picker that excludes the current employee,
  and a subordinate privilege-editor component seeded from the newly
  created employee id. The delete flow should be implemented
  transactionally on the server because it depends on four dependency
  counts plus MRU cleanup.

## 7. Open questions / ambiguity log

- **Question:** Who is actually allowed to open and edit the employee
  maintenance workflow in Northwind?
  - **Source-of-uncertainty:** The workflow edits privilege data, but no
    `HasPrivilege(...)` call is present in `frmEmployeeList` or
    `sfrmEmployee_Privileges`. Access to the form may be gated by menu
    exposure, navigation structure, or trust in the user population
    rather than by an explicit code check.
  - **Recommended resolution path:** Review the live app's navigation
    and startup surface, or inspect command-button/ribbon call sites
    that open `frmEmployeeList`, before carrying the role mapping into
    the rewrite.

- **Question:** If the employee rewrite ultimately needs `IsAdmin` /
  `IsActive`-style flags or user-specific UI state, where should that
  logic come from given that it was not present in the inspected Session
  6 workflow artifacts?
  - **Source-of-uncertainty:** The session spec explicitly called out
    those patterns as things to look for, but the inspected employee
    table, forms, and queries did not expose them.
  - **Recommended resolution path:** Treat them as out-of-scope for this
    workflow package unless a broader authorization/session-management
    design in the downstream Blazor pilot introduces them deliberately.

- **Question:** Is the single observed privilege (`pApprovePO`) the
  intended long-term scope of the employee privilege model, or just the
  current minimum seed?
  - **Source-of-uncertainty:** `Privileges` currently contains one row,
    and `enumPrivileges` currently contains one member. The workflow is
    architected as a general employee-to-privilege join table, which
    suggests future expansion.
  - **Recommended resolution path:** Domain reviewer decides whether the
    Blazor rewrite should model a general privilege catalog or a single
    PO-approval capability.

- **Question:** Should the "unknown supervisor" path remain an inline
  create-new-employee pivot in the rewrite, or should missing
  supervisors be created in a dedicated employee flow first?
  - **Source-of-uncertainty:** `cboSupervisorID_NotInList` explicitly
    pivots into a new employee record, but that may reflect Access UI
    convenience more than a durable business requirement.
  - **Recommended resolution path:** User-experience review during the
    downstream implementation pilot; preserve the capability only if it
    materially helps administrators.

- **Question:** When deleting a deletable employee, should the new
  system preserve the MRU cleanup behavior, or should it treat recent
  items as ephemeral UI state that can simply expire?
  - **Source-of-uncertainty:** `EmployeeCanBeDeleted` explicitly removes
    order and purchase-order MRU rows before delete, but MRU state is an
    Access-specific convenience rather than business data.
  - **Recommended resolution path:** Preserve the deletion safety rule,
    but let the Blazor implementation choose a more natural recent-items
    strategy unless a reviewer says the current MRU cleanup is business
    visible.
