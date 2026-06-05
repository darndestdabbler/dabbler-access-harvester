# HomeHealthCare `Add a Client` Inventory

## Reading Surface

- Live harvester run succeeded in this session against `samples/HomeHealthCare.accdb`.
- Active session reading surface:
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/raw/`
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/kb/`
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/`
- Cross-check source for code-behind readability:
  - `samples/DatabaseYaml/HomeHealthCare.accdb/`

## Workflow Surface

### `frmClientSearch`

- Role in workflow: unbound search-and-launch surface for client records.
- Evidence:
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/Forms/frmClientSearch.ir.json`
  - `samples/DatabaseYaml/HomeHealthCare.accdb/Forms/frmClientSearch.yaml`
  - `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientSearch.yaml`
- Observed behavior:
  - `RecordSource` is blank, so the form is a search host rather than a bound list form.
  - `OnLoad` embedded macro seeds `cboFilterStatus = "Active"` and focuses `txtFilterName`.
  - `PopulateClientList` builds a live `SELECT ... FROM tblClients` and feeds it through `basListHelpers.BuildAddableRowSource(...)`, which prepends a synthetic add row.
  - `lstClientList_DblClick` routes through `basListHelpers.OpenDetailFromList Me.lstClientList, "frmClientDetail"`.
  - `btnAddNew_Click` is a second add entry point and opens `frmClientDetail` with `acFormAdd`, not through the shared helper.
- Northwind analogue:
  - Closest analogue is `frmRecordList` plus `qryRecordList`, but the shape is materially different.
  - Northwind uses a bound split form and built-in record navigation.
  - HHC uses an unbound listbox-cascade search form with explicit filter widgets and launch handlers.

### `frmClientDetail`

- Role in workflow: bound detail form for creating or editing a single client.
- Evidence:
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/Forms/frmClientDetail.ir.json`
  - `samples/DatabaseYaml/HomeHealthCare.accdb/Forms/frmClientDetail.yaml`
  - `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/Form_frmClientDetail.yaml`
- Observed behavior:
  - `RecordSource` is `tblClients`.
  - `Form_Load` interprets `OpenArgs`:
    - `"ADD"` -> moves to a new record.
    - numeric value -> applies `Filter = "ClientID=<id>"` and `FilterOn = True`.
  - `Form_Current` sets caption, initializes the fiscal-year filter, refreshes the child listboxes, and disables `btnSave` until the form becomes dirty.
  - `Form_BeforeUpdate` enforces tag-driven required-field checks.
  - Field-level handlers normalize names and validate email, date-of-birth, SSN, and state/city interactions.
  - `OnClose` embedded macro requeries `frmClientSearch` if it is open.
- Northwind analogue:
  - Closest analogue is `frmRecordDetail`, but HHC does not use subforms.
  - HHC uses child listboxes (`lstClientServiceNeeds`, `lstClientBudgets`) plus popup detail forms rather than always-visible bound subforms.

### `basListHelpers`

- Role in workflow: reusable launch and `OpenArgs` helper for the listbox-plus-detail pattern.
- Evidence:
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/ModulesAndClasses/basListHelpers.ir.json`
  - `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/basListHelpers.yaml`
- Observed behavior:
  - `BuildAddableRowSource(...)` prepends a synthetic add row with sentinel PK `-1`.
  - `OpenDetailFromList(...)` maps the synthetic row to `OpenArgs = "ADD"` and real selections to numeric PK `OpenArgs`.
  - `BuildContextArgs(...)`, `IsAddMode(...)`, and `GetContextLong(...)` encode and decode pipe-delimited `OpenArgs` payloads.
- Northwind analogue:
  - No close analogue in the pilot packages.
  - This is a genuine HHC-specific substrate shape the Northwind packages did not need.

## Data Surface

### `tblClients`

- Role in workflow: primary persisted entity for the add flow.
- Evidence:
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/Tables/tblClients.ir.json`
  - `samples/DatabaseYaml/HomeHealthCare.accdb/Tables/tblClients.yaml`
- Important defaults and constraints:
  - Required fields: `FirstName`, `LastName`, `DateOfBirth`, `EnrollmentDate`.
  - Defaults: `Gender = "X"`, `EnrollmentDate = Date()`, `IsActive = True`.
  - Field validations:
    - `DateOfBirth < Date()`
    - `Email Is Null Or Like "*@*.*"`
    - `Gender In ("M","F","X")`
  - Table validation:
    - `[EnrollmentDate] >= [DateOfBirth]`
- Northwind analogue:
  - Closest analogue is the customer/company header entity used in `Add an Order`, but HHC's client detail is a direct table-bound intake surface rather than a multi-entity order header.

### Related tables and relationships

- `tlkpStates` -> `tblClients` (`rel_State_Client`) supplies the state lookup.
- `tblClientBudgets` and `tblClientServiceNeeds` depend on `tblClients` and become relevant only after the client exists.
- Northwind analogue:
  - Similar to Northwind child collections appearing only after the parent header exists, but HHC implements them with listboxes + popup detail forms rather than embedded subforms.

### `qryClientList`

- Role in workflow: nearest committed query analogue for the client list surface.
- Evidence:
  - `tmp/generalization-validation-on-non-northwind/AccessMetaData/HomeHealthCare/ir/Queries/qryClientList.ir.json`
  - `samples/DatabaseYaml/HomeHealthCare.accdb/Queries/qryClientList.yaml`
- Note:
  - `frmClientSearch` does not bind directly to `qryClientList`; it builds its own SQL over `tblClients`.
  - The query is still useful as a structural cross-check because it exposes the intended list columns and ordering.
- Northwind analogue:
  - `qryRecordList`, with the same caveat that Northwind actually binds the query into a split-form UI while HHC composes the list dynamically.

## Behavior-Bearing Procedures

### Package- and test-worthy

- `Form_frmClientDetail::Form_BeforeUpdate`
  - Why it matters: primary save gate for required controls on the detail form.
  - Testability: yes, as an implementation-independent save-precondition model.
- `Form_frmClientDetail::txtFirstName_BeforeUpdate`
  - Why it matters: trims and proper-cases first name before save.
  - Testability: yes, as deterministic normalization.
- `Form_frmClientDetail::txtLastName_BeforeUpdate`
  - Why it matters: trims and proper-cases last name before save.
  - Testability: yes, as deterministic normalization.
- `Form_frmClientDetail::txtEmail_BeforeUpdate`
  - Why it matters: blocks malformed email values.
  - Testability: yes, as input validation.
- `Form_frmClientDetail::txtDateOfBirth_BeforeUpdate`
  - Why it matters: enforces adult-only intake on top of the table's "must be in the past" rule.
  - Testability: yes, as input validation.
- `Form_frmClientDetail::Form_Delete`
  - Why it matters: blocks delete when dependent budget rows exist.
  - Testability: yes, as deletion-policy logic.
- `Form_frmClientDetail::chkIsActive_AfterUpdate`
  - Why it matters: warns when deactivating a client with approved plan rows.
  - Testability: yes, as a warning-condition policy, not as UI message-box plumbing.

### Operationalization-bearing, not characterization-worthy

- `Form_frmClientSearch::PopulateClientList`
  - Valuable for describing the workflow, but mostly UI/query composition rather than a durable domain invariant.
- `Form_frmClientSearch::lstClientList_DblClick`
  - Important operationalization link between list and detail, but tightly coupled to Access listbox interaction.
- `basListHelpers::OpenDetailFromList`
  - Critical for the HHC pattern description, but still UI-launch plumbing rather than business logic.
- `Form_frmClientDetail::Form_Load` / `Form_Current`
  - Important for understanding `OpenArgs`, captions, and child-list refresh, but too UI-lifecycle-shaped for first-pass characterization tests.

## Harvester Coverage Notes

- The live harvester run surfaced all expected forms, queries, tables, modules, and relationships for the workflow with zero extraction, translation, or enrichment failures.
- `frmClientSearch` and `frmClientDetail` form properties came through cleanly in `ir/`, including `RecordSource`, event bindings, navigation-button settings, and the `OnLoad` / `OnClose` embedded macros.
- Standard modules (`basListHelpers`, `basUtilities`, `basDataOperations`) translated cleanly into `ModulesAndClasses/*.ir.json`.
- Form code-behind is still easier to audit from the committed YAML module dumps than from the enriched form JSON alone; for this session the YAML mirror is the most readable source for VBA bodies.
- `tblClients` does **not** carry a data macro. This matters because the broader HHC fixture comparison emphasizes validation-heavy macros, but the `Add a Client` workflow is enforced here by a mix of form code and table validation rules instead.
- The standalone macro object `mcrValidation` exists, but it behaves like a reusable validation helper library rather than a `tblClients`-attached enforcement layer for this workflow.
