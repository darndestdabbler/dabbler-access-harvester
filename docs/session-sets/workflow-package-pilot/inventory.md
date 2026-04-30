# NorthwindDevEdition Pilot-Workflow Inventory

> **Purpose:** Confirm the four pilot workflows from `spec.md` map cleanly
> to identifiable forms, queries, event handlers, behavior-bearing
> procedures, and tables in the existing NorthwindDevEdition harvester
> output.
> **Source:** `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/`
> (locally rebuilt IR from `samples/NorthwindDevEdition.accdb`; the
> committed `tools/symbol-db/symbol-db.sqlite` was left untouched per
> the path-b-probe pattern).
> **Authored:** 2026-04-30, Session 1 of 7.
> **Scope:** Identification only. This document does **not** infer
> business rules or behavior; that is sessions 2-6's work. Entries
> here are pure citations into harvester output.

---

## 1. Add an Order

| Aspect | Artifact |
|---|---|
| Primary form | `Forms/frmOrderDetails.ir.json` (RecordSource: `qryOrder`) |
| Caller / list | `Forms/frmOrderList.ir.json` (RecordSource: `qryOrderList`) — `cmdAddOrder_Click` opens the detail form |
| Subform | `Forms/sfrmOrderLineItems.ir.json` (RecordSource: `qryOrderLineItems`) |

### Bound queries

- `Queries/qryOrder.ir.json` — `SELECT Orders.*, OrderStatus.OrderStatusName FROM OrderStatus INNER JOIN Orders ON OrderStatus.OrderStatusID = Orders.OrderStatusID;`
- `Queries/qryOrderList.ir.json` — joins `Orders`, `OrderStatus`, `qryCustomers`, `qrycboEmployees`, `qryOrderTotal`, `qryOrderList_DetailStatus`.
- `Queries/qryOrderLineItems.ir.json` — joins `OrderDetails`, `Products`, `ProductCategories`.

### Underlying tables

- `Tables/Orders.ir.json`
- `Tables/OrderDetails.ir.json`
- `Tables/OrderStatus.ir.json` (lookup)
- `Tables/OrderDetailStatus.ir.json` (lookup)
- `Tables/Customers` is the conceptual customer source — physically `Companies` filtered by `CompanyTypeID` via `qryCustomers`. See `Tables/Companies.ir.json`, `Tables/CompanyTypes.ir.json`.
- `Tables/Products.ir.json`, `Tables/ProductCategories.ir.json` (line-item product lookup).
- `Tables/Employees.ir.json` (assigned-to / cbo source).

### Event handlers (form-event properties present in IR)

- `frmOrderDetails`: `OnLoad`, `OnOpen`, `OnClose`, `OnCurrent`, `OnDelete`, `BeforeInsert`, `AfterInsert`, `BeforeUpdate`, `AfterUpdate`, `BeforeDelConfirm`. Implementing procedures live on the form (e.g. `Form_Load`, `Form_BeforeUpdate`).
- `sfrmOrderLineItems`: `OnCurrent`, `OnDelete`, `OnError`, `BeforeInsert`, `BeforeUpdate`, `AfterUpdate`, `BeforeDelConfirm`, `AfterDelConfirm`.

### Candidate behavior-bearing procedures (form-local)

From `Forms/frmOrderDetails.ir.json` Procedures:

- `AllInvoiceLineItemsHaveStatus(ods: enumOrderDetailStatus) -> Boolean`
- `PaidFieldsFilledOut() -> Boolean`
- `ShippingFeeFilledOut() -> Boolean`
- `ShippingFieldsFilledOut() -> Boolean`
- `LockControls()`
- `SetFormStatus()`
- `SetLineItemsStatus()`
- `cmdAddOrder_Click`, `cmdShipOrder_Click`, `cmdCloseOrder_Click`,
  `cmdCreateInvoice_Click`, `cmdReceivePayment_Click`,
  `cmdPrintInvoice_Click`, `cmdDeleteOrder_Click`
- `Form_BeforeInsert`, `Form_BeforeUpdate`, `Form_AfterInsert`,
  `Form_AfterUpdate`, `Form_Load`, `Form_Open`, `Form_Current`,
  `Form_Delete`, `Form_BeforeDelConfirm`, `Form_Close`
- `CustomerID_AfterUpdate`

From `Forms/sfrmOrderLineItems.ir.json` Procedures:

- `ReallocateInventory()`
- `SetOrderDetailStatus()`
- `Form_BeforeInsert`, `Form_BeforeUpdate`, `Form_AfterUpdate`,
  `Form_Current`, `Form_Delete`, `Form_BeforeDelConfirm`,
  `Form_AfterDelConfirm`, `Form_Error`
- `ProductID_AfterUpdate`, `cboProductCategories_AfterUpdate`

### Module-level helpers referenced from these forms

(Identified by word-boundary match against the form's `VbaCode` body.)

- `frmOrderDetails`: `ValidateForm` (modValidation), `HighlightControl`
  (modValidation), `AllocateInventory` (modInventory),
  `CloseOrderDetailsForm` (modOrders).
- `sfrmOrderLineItems`: `ValidateForm`, `AllocateInventory`,
  `ProductAvailable` (modInventory).
- `frmOrderList`: `OpenOrderDetailsForm` (modOrders).

### Notes

- The line-item subform's `ReallocateInventory` and the parent form's
  `AllocateInventory` call into `modInventory.AllocateInventory` —
  the calculation surface for the workflow extends into that module
  even though the form is the entry point.
- Customer-lookup binding goes through `qryCustomers`, which filters
  `Companies` to customer-typed rows. The harvester output captures
  the join chain; the package author for this workflow will need to
  cite both the form-level and the query-level evidence.

---

## 2. Update an Order

| Aspect | Artifact |
|---|---|
| Primary form | `Forms/frmOrderDetails.ir.json` (same as Add an Order) |
| Caller / list | `Forms/frmOrderList.ir.json` — `OrderID_Click` opens existing record |
| Subform | `Forms/sfrmOrderLineItems.ir.json` |

This workflow shares the form, queries, tables, event handlers, and
form-local behavior-bearing procedures with **Add an Order**. The
distinction lives in:

- The state-dependent command-button branches:
  - `cmdShipOrder_Click` — relevant only when status permits shipping
  - `cmdCloseOrder_Click`
  - `cmdReceivePayment_Click`
  - `cmdCreateInvoice_Click`, `cmdPrintInvoice_Click`
- `SetFormStatus`, `LockControls` — gating control enablement based
  on the current `OrderStatus` / `OrderDetailStatus`.
- `AllInvoiceLineItemsHaveStatus`, `PaidFieldsFilledOut`,
  `ShippingFeeFilledOut`, `ShippingFieldsFilledOut` — predicates on
  whether the order is in a state that permits the next transition.
- `Form_BeforeUpdate`, `Form_AfterUpdate`, `Form_Current` — fire on
  edit to an existing record.

### Lookup tables relevant to state transitions

- `Tables/OrderStatus.ir.json`, `Tables/OrderDetailStatus.ir.json`.

### Notes

- The package authors for **Add** and **Update** will read the same
  form. The split is in which subset of procedures a workflow's
  business invariants pin: Add focuses on `Form_BeforeInsert` /
  `CustomerID_AfterUpdate` paths; Update focuses on the status-aware
  command buttons and `Form_BeforeUpdate`.

---

## 3. Update a Product

| Aspect | Artifact |
|---|---|
| Primary form | `Forms/frmProductDetail.ir.json` (RecordSource: `qryProductDetail`) |
| Caller / list | `Forms/frmProductList.ir.json` (RecordSource: `qryProductList`) — `ProductID_Click` opens detail |
| Subforms (tabbed) | `sfrmProductDetail_Vendors`, `sfrmProductDetail_StockTake`, `sfrmProductDetail_Orders`, `sfrmProductDetail_PurchaseOrders` |

### Bound queries

- `Queries/qryProductDetail.ir.json` — selects from `Products` only.
- `Queries/qryProductList.ir.json` — joins `Products`,
  `ProductCategories`; calls module helpers (`ProductNoStock`,
  `ProductAllocated`, …) inline as SELECT-list expressions.
- `Queries/qryProductVendors.ir.json` — `ProductVendors` only.
- `Queries/qryStockTake.ir.json` — `StockTake` only.
- `Queries/qryProductOrders.ir.json` — joins `OrderDetails`,
  `Products`, `Orders`, `OrderStatus`, `OrderDetailStatus`. The join
  surface is what makes "cascading effect" tangible for this
  workflow.
- `Queries/qryProductPurchaseOrder.ir.json` — joins
  `PurchaseOrderDetails`, `Products`, `PurchaseOrders`,
  `PurchaseOrderStatus`.

### Underlying tables

- `Tables/Products.ir.json`, `Tables/ProductCategories.ir.json`,
  `Tables/ProductVendors.ir.json`, `Tables/StockTake.ir.json`.
- Cascade surface: `Tables/OrderDetails.ir.json`,
  `Tables/Orders.ir.json`, `Tables/OrderDetailStatus.ir.json`,
  `Tables/PurchaseOrderDetails.ir.json`,
  `Tables/PurchaseOrders.ir.json`,
  `Tables/PurchaseOrderStatus.ir.json`.

### Event handlers

- `frmProductDetail`: `OnLoad`, `OnActivate`, `OnClose`, `OnCurrent`,
  `OnDelete`, `BeforeUpdate`, `AfterUpdate`, `AfterInsert`,
  `AfterDelConfirm`.
- `sfrmProductDetail_StockTake`: `OnCurrent`, `BeforeUpdate`,
  `AfterUpdate`, `AfterInsert`.
- `sfrmProductDetail_Vendors`: `BeforeUpdate`.

### Candidate behavior-bearing procedures (form-local)

From `Forms/frmProductDetail.ir.json`:

- `CalculateLevels()`, `ControlStates()`, `SetProductCode()`
- `cboProductCategoryID_AfterUpdate`,
  `cboFindProduct_AfterUpdate`, `txtTargetLevel_AfterUpdate`,
  `chkDiscontinued_AfterUpdate`
- `cmdAddProduct_Click`, `cmdDeleteProduct_Click`,
  `cmdReorderProduct_Click`
- `tabProducts_Change`
- `Form_Load`, `Form_Activate`, `Form_Current`, `Form_BeforeUpdate`,
  `Form_AfterUpdate`, `Form_AfterInsert`, `Form_AfterDelConfirm`,
  `Form_Delete`, `Form_Close`

From `Forms/sfrmProductDetail_StockTake.ir.json`:

- `RequeryQuantities()`, `QuantityOnHand_BeforeUpdate`,
  `QuantityOnHand_AfterUpdate`, `Form_BeforeUpdate`,
  `Form_AfterUpdate`, `Form_AfterInsert`, `Form_Current`.

### Module-level helpers referenced

- `frmProductDetail`: `ValidateForm`, `ProductAllocated`,
  `ProductLastStockTakeDate`, `ProductNoStock`, `ProductOnOrder`,
  `ProductReorderQuantity`, `ProductToSell`, `AddPurchaseOrderDetail`,
  `ReorderProduct`, `OpenPurchaseOrderDetailsForm` (modInventory,
  modPurchaseOrders).
- `sfrmProductDetail_StockTake`: `ValidateForm`, `AllocateInventory`,
  `ProductAvailable`.

### Notes

- The cascading-effect surface is broad. Module `modInventory`
  contributes 12 public functions (`AllocateInventory`,
  `OrderQuantity_ByStatus`, `ProductAllocated`, `ProductAvailable`,
  `ProductBought`, `ProductLastStockTakeDate`,
  `ProductLastStockTakeQuantity`, `ProductNoStock`, `ProductOnOrder`,
  `ProductReorderQuantity`, `ProductSold`, `ProductToSell`) and
  `modPurchaseOrders` contributes `ReorderProduct`,
  `AddPurchaseOrderDetail`. These are the natural homes for cascading
  business rules that the package's confidence-tagged invariants will
  cite.
- `qryProductList` invokes `ProductNoStock`, `ProductAllocated`,
  `ProductOnOrder`, etc. as SELECT-list expressions — meaning the
  inventory cascade reaches into list-binding behavior, not just the
  detail form.

---

## 4. Update an Employee

| Aspect | Artifact |
|---|---|
| Primary form | `Forms/frmEmployeeList.ir.json` (RecordSource: `qryEmployees`) — list-and-edit form (no separate detail form for employees) |
| Privileges subform | `Forms/sfrmEmployee_Privileges.ir.json` (RecordSource: `qryEmployeePrivileges`) |
| Title lookup form | `Forms/frmEmployeeTitles.ir.json` (RecordSource: `Titles`) — manages the list of valid titles |

### Bound queries

- `Queries/qryEmployees.ir.json` — `SELECT Employees.*, [FirstName] & " " & [LastName] AS FullNameFNLN, [LastName] & ", " & [FirstName] AS FullNameLNFN FROM Employees ORDER BY ...`.
- `Queries/qryEmployeePrivileges.ir.json` — joins `Employees` and
  `EmployeePrivileges`.

### Underlying tables

- `Tables/Employees.ir.json`
- `Tables/EmployeePrivileges.ir.json`
- `Tables/Privileges.ir.json` (the privilege catalogue)
- `Tables/Titles.ir.json` (employee titles)

### Event handlers

- `frmEmployeeList`: `OnCurrent`, `OnDelete`, `BeforeUpdate`,
  `AfterUpdate`, `BeforeDelConfirm`, `AfterDelConfirm`, `AfterInsert`.
- `sfrmEmployee_Privileges`: `OnOpen`, `AfterInsert`,
  `AfterDelConfirm`.

### Candidate behavior-bearing procedures (form-local)

From `Forms/frmEmployeeList.ir.json`:

- `EmployeeCanBeDeleted()` — gating predicate.
- `RequeryEmployeeList()`
- `cboSupervisorID_NotInList`, `cboSupervisorID_Enter`
- `cmdEmailEmployee_Click`, `cmdNewEmployee_Click`
- `Form_BeforeUpdate`, `Form_AfterUpdate`, `Form_AfterInsert`,
  `Form_BeforeDelConfirm`, `Form_AfterDelConfirm`, `Form_Delete`,
  `Form_Current`.

From `Forms/sfrmEmployee_Privileges.ir.json`:

- `Form_AfterInsert`, `Form_AfterDelConfirm`, `Form_Open`,
  `frmLoadedAsSubform()` (likely a parent-context probe).

### Module-level helpers referenced

- `frmEmployeeList`: `ValidateForm`.
- Note: `modSecurity.HasPrivilege(p: enumPrivileges) -> Boolean` is
  the role-gating primitive in the codebase. It is referenced from
  many places in the harvester output (most notably the Admin form
  and command-button gating); whether it gates **Update an Employee**
  itself is a question for Session 6's package author to resolve
  with evidence.

### Notes

- This workflow lives on a list form rather than a separate detail
  form. Edits happen in the bound rows of `frmEmployeeList`; the
  privileges subform manages the per-employee privilege grid; the
  titles list is managed via a separate small form (`frmEmployeeTitles`).
- The role-taxonomy mapping called out in `spec.md` Session 6 is the
  concrete intersection of `EmployeePrivileges` rows × the
  `enumPrivileges` enum × `modSecurity.HasPrivilege` calls scattered
  across the codebase. Session 6 will inventory those call sites
  during its package authoring.

---

## Cross-workflow assets used as evidence

These appear in the citation chain of more than one pilot package and
are inventoried here once for reference:

| Artifact | Where it shows up |
|---|---|
| `ModulesAndClasses/modValidation.ir.json` (`ValidateForm`, `IsValidForm`, `IsBoundToRequiredField`, `HighlightControl`) | `frmOrderDetails`, `sfrmOrderLineItems`, `frmProductDetail`, `sfrmProductDetail_StockTake`, `frmEmployeeList` |
| `ModulesAndClasses/modSecurity.ir.json` (`HasPrivilege`) | Searched for in pilot forms; whether each pilot workflow gates on it is per-workflow evidence |
| `ModulesAndClasses/modInventory.ir.json` (12 public functions) | `frmOrderDetails`, `sfrmOrderLineItems`, `frmProductDetail`, `sfrmProductDetail_StockTake`, plus inline use inside `qryProductList` |
| `ModulesAndClasses/modOrders.ir.json` (`OpenOrderDetailsForm`, `CloseOrderDetailsForm`, `CloseAllOrderDetailsForms`, `SetDatesToCurrent`, `CreateRandomOrders`, `GetRandomProductID`) | `frmOrderList`, `frmOrderDetails`, `sfrmProductDetail_Orders` |
| `ModulesAndClasses/modPurchaseOrders.ir.json` (`AddPurchaseOrderDetail`, `ReorderProduct`, `OpenPurchaseOrderDetailsForm`, `ClosePurchaseOrderDetailsForm`) | `frmProductDetail`, `sfrmProductDetail_PurchaseOrders` |

---

## Identification verdict

All four pilot workflows map cleanly to identifiable harvester
artifacts:

| Workflow | Form? | Queries? | Tables? | Event handlers? | Behavior-bearing procs? |
|---|---|---|---|---|---|
| Add an Order | yes | yes | yes | yes | yes |
| Update an Order | yes (shared with Add) | yes | yes | yes | yes |
| Update a Product | yes | yes | yes | yes | yes |
| Update an Employee | yes | yes | yes | yes | yes |

No pilot workflow is blocked by a harvester gap at the
identification level. Session 2's package draft can begin against
`frmOrderDetails` + the modules above without additional extraction
work.

The **gaps the harvester does not resolve** (and therefore the
ambiguity log of each package will need to surface) are interpretive,
not structural:

- The harvester does not declare which of the form-local procedures
  are behavior-bearing vs. UI choreography. The package author
  judges this per the spec's definition (validators / calculations /
  state-change logic / save-delete gating / business-rule-heavy
  query shaping).
- The harvester does not classify the role-taxonomy intent of
  `modSecurity.HasPrivilege` call sites. Session 6 surfaces those.
- The harvester does not reconcile the cascade behavior of
  `modInventory` functions with their callers — that is what the
  characterization tests will pin.

This matches the spec's expectation that interpretation work belongs
in the package, not in the harvester.
