# Workflow Package: Update a Product

> **Slug:** `update-a-product` (filename: `packages/update-a-product.md`)
> **Pilot session:** Session 5 of 7
> **Author orchestrator:** Claude Opus 4.7
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

Catalogue, purchasing, and warehouse staff update existing products to
keep pricing, packaging, category assignment, target inventory levels,
and discontinued-status accurate, and use the same form to trigger
reorders or to delete products that no longer have any order or
purchase-order history. The business outcome is a product record whose
business attributes stay consistent with current operations and whose
cascading inventory and purchasing effects (allocation, reorder
quantity, vendor-to-PO routing) follow a single set of rules.

## 2. Actors and entities

- **Actors:** Authenticated catalogue / purchasing staff working from
  the product list and the product-detail form. The source evidence
  does not show per-action privilege gates inside this workflow
  (`HasPrivilege` is not referenced in `frmProductDetail`).
- **Primary entity:** Product.
- **Related entities:** Product categories, vendors (companies of
  type Vendor), stock takes, order line items, purchase order line
  items, purchase orders.

## 3. Business rules with evidence + confidence tags

- **Rule:** Existing products are opened from the product list; the
  detail form binds to a single-product record source.
  - **Evidence:** `Forms/frmProductList.ir.json` -> `ProductID_Click`;
    `Forms/frmProductDetail.ir.json` -> `Form_Load`,
    `RecordSource = qryProductDetail`;
    `Queries/qryProductDetail.ir.json`.
  - **Confidence:** confirmed

- **Rule:** A product save is rejected when any required column is
  missing, when `StandardUnitCost <= 0`, or when the product fails
  the shared form-level validation.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `Form_BeforeUpdate` (`Cancel = ValidateForm(Me)` followed by an
    explicit `Me.txtStandardUnitCost <= 0` cancel branch);
    `ModulesAndClasses/modValidation.ir.json` -> `ValidateForm`,
    `IsValidForm`, `IsBoundToRequiredField`;
    `Tables/Products.ir.json` marks `ProductCode`, `ProductName`,
    `StandardUnitCost`, `UnitPrice`, `ProductCategoryID`, and
    `Discontinued` as required.
  - **Confidence:** confirmed

- **Rule:** `ProductCode` and `ProductName` are unique across the
  product catalogue.
  - **Evidence:** `Tables/Products.ir.json` -> `Indexes` includes
    unique indexes named `ProductCode` (over `ProductCode`) and
    `ProductName` (over `ProductName`).
  - **Confidence:** confirmed

- **Rule:** When a product is saved with a category assigned and an
  existing `ProductID`, its `ProductCode` is regenerated as
  `"NWT" + <ProductCategoryCode> + "-" + ProductID`.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cboProductCategoryID_AfterUpdate` calls `SetProductCode`;
    `SetProductCode` builds the code via `DLookup` against
    `ProductCategories.ProductCategoryCode`.
  - **Confidence:** confirmed

- **Rule:** A product can be deleted only when no `OrderDetails` row
  and no `PurchaseOrderDetails` row references it.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cmdDeleteProduct_Click` (the `Select Case True` block runs
    `DCount` against both tables and short-circuits with a
    "Cannot Delete" message when either count is greater than zero).
  - **Confidence:** confirmed

- **Rule:** A confirmed product delete cascades by removing the
  product's `StockTake` rows and `ProductVendors` rows first, then
  deleting the `Products` row.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cmdDeleteProduct_Click` (two `g_dbApp().Execute "DELETE * FROM
    StockTake ..."` and `"DELETE * FROM ProductVendors ..."`
    statements, then `DoCmd.RunCommand acCmdDeleteRecord` for the
    Product row itself).
  - **Confidence:** confirmed

- **Rule:** Reordering is blocked while a non-closed purchase order
  for the same product is already outstanding (statuses
  `posApprove`, `posNew`, or `posSubmitted`); the existing purchase
  order is opened instead of creating a new one.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cmdReorderProduct_Click` (`If Me.txtQtyOnOrder <> 0 ...
    StatusID in (posApprove, posNew, posSubmitted) ...
    OpenPurchaseOrderDetailsForm varRelatedID`);
    `ModulesAndClasses/modGlobal.ir.json` ->
    `enumPurchaseOrderStatus`.
  - **Confidence:** confirmed

- **Rule:** A product must have at least one defined vendor before a
  purchase order can be created from the reorder action.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cmdReorderProduct_Click` (the `DLookup("Count(ProductVendorID)
    & "|" & First(VendorID)", "ProductVendors", ...)` branch shows
    the "At least one product vendor must be defined ..." message
    when the count is 0).
  - **Confidence:** confirmed

- **Rule:** When the product has multiple vendors, the reorder
  action prompts the user to choose one; with a single vendor it
  uses that vendor without prompting.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cmdReorderProduct_Click` (`If aryVendor(0) = 1 Then
    lngVendorID = aryVendor(1) Else DoCmd.OpenForm
    "frmSelectVendorDialog" ... acDialog`).
  - **Confidence:** confirmed

- **Rule:** The reorder quantity is taken from
  `txtQtyToReorder` when it is greater than zero, otherwise from the
  product's `MinimumReorderQuantity`, otherwise from a user-entered
  value; a quantity of zero cancels the reorder.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cmdReorderProduct_Click` (the
    `If Me.txtQtyToReorder = 0 Then intOrderQuantity =
    Nz(Me.txtMinimumReorderQuantity, 0) ... Else intOrderQuantity =
    Me.txtQtyToReorder` block, including the `If intOrderQuantity =
    0 Then GoTo Exit_Handler` short-circuit).
  - **Confidence:** confirmed

- **Rule:** When an existing purchase order in `posNew` status
  already exists for the chosen vendor, the reorder appends a line
  item to that purchase order; otherwise a new purchase order is
  created in `posNew` status with one line item.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `cmdReorderProduct_Click` (the
    `DMin("PurchaseOrderID", "PurchaseOrders", ...
    StatusID = posNew)` branch routes to `AddPurchaseOrderDetail`,
    while the absent-vendor-PO branch routes to `ReorderProduct`);
    `ModulesAndClasses/modPurchaseOrders.ir.json` ->
    `AddPurchaseOrderDetail`, `ReorderProduct`.
  - **Confidence:** confirmed

- **Rule:** When `AddPurchaseOrderDetail` finds an existing
  `(PurchaseOrderID, ProductID)` line, it adds the new quantity to
  the existing line's quantity rather than creating a duplicate
  line; new lines copy the product's `StandardUnitCost` as
  `UnitCost`.
  - **Evidence:** `ModulesAndClasses/modPurchaseOrders.ir.json` ->
    `AddPurchaseOrderDetail` (the `If rs.EOF Then ... AddNew ...
    Else rs.Edit / rs!Quantity = rs!Quantity + intQuantity`
    branch); `Forms/frmProductDetail.ir.json` ->
    `cmdReorderProduct_Click` passes `Me.StandardUnitCost` as the
    cost argument.
  - **Confidence:** confirmed

- **Rule:** Discontinued products may not be reordered.
  - **Evidence:** `Forms/frmProductDetail.ir.json` ->
    `ControlStates` (`If Me.chkDiscontinued = True Then
    Me.cmdReorderProduct.Visible = False`), which gates access to
    `cmdReorderProduct_Click` for any persisted record.
  - **Confidence:** confirmed

- **Rule:** Existing `StockTake` rows are read-only and cannot be
  deleted from the stock-take subform; only new stock-take rows can
  be entered, and the entered quantity is interactively confirmed
  when it differs from the system's expected on-hand quantity.
  - **Evidence:**
    `Forms/sfrmProductDetail_StockTake.ir.json` -> `Form_Current`
    (`Me.AllowAdditions = True`, `Me.AllowDeletions = False`,
    `Me.AllowEdits = Me.NewRecord`); `QuantityOnHand_BeforeUpdate`
    (compares `Me.QuantityOnHand` to `ProductAvailable(...)`,
    cancels with a Yes/No prompt when they disagree).
  - **Confidence:** confirmed

- **Rule:** Saving a stock-take row stamps `StockTakeDate = Now()`,
  refreshes the product's expected on-hand quantity, and re-runs
  inventory allocation for the product so any pending order line
  items get the new physical inventory.
  - **Evidence:**
    `Forms/sfrmProductDetail_StockTake.ir.json` ->
    `QuantityOnHand_AfterUpdate` (`Me.StockTakeDate = Now()`,
    `Me.ExpectedQuantity = ProductAvailable(Me.ProductID)`,
    forced save via `Me.Dirty = False`); `Form_AfterUpdate`
    (`AllocateInventory Me.ProductID`); the same allocation
    routine is referenced from `Form_AfterInsert`'s requery path.
  - **Confidence:** confirmed

- **Rule:** Inventory allocation for a product walks that product's
  pending order details (status `Allocated`, `NoStock`, or
  `OnOrder`) oldest-first by `OrderDate`, breaking ties by
  ascending `OrderID`, and assigns each line `Allocated` if
  physical stock can satisfy it, `OnOrder` if remaining
  physical-plus-on-order can satisfy it, or `NoStock` otherwise.
  - **Evidence:** `ModulesAndClasses/modInventory.ir.json` ->
    `AllocateInventory` (its SQL ordering is
    `order by o.OrderDate, o.OrderID`; the three-branch decision
    on `intAvailable` and `intQtyToAllocate` writes
    `odsAllocated`, `odsOnOrder`, or `odsNoStock`).
  - **Confidence:** confirmed
  - **Note:** This rule is shared with the Add an Order workflow
    package; the product workflow surfaces it because every product
    save and stock-take is the upstream cause of allocation
    transitions on order line items.

- **Rule:** A product's reorder quantity is the larger of its
  `MinimumReorderQuantity` and the calculated shortfall
  `(NoStock + TargetLevel) - (QtyAvailableToSell + OnOrder)`; when
  current available-plus-on-order already meets the target plus
  no-stock backlog, the reorder quantity falls back to the minimum
  reorder quantity rather than zero.
  - **Evidence:** `ModulesAndClasses/modInventory.ir.json` ->
    `ProductReorderQuantity` (the
    `If (intQtyAvailable + intOnOrder) >= (intNoStock +
    intTargetLevel) Then intReorderQty = intMinReorder Else ...`
    branches and the subsequent `If intMinReorder >
    intReorderQty Then intReorderQty = intMinReorder` floor).
  - **Confidence:** confirmed

## 4. Behavioral invariants -> characterization tests

- **Invariant:** Product saves are rejected when any required field
  is missing or when `StandardUnitCost` is non-positive.
  - **Source procedure:** `frmProductDetail::Form_BeforeUpdate`,
    `modValidation::ValidateForm`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductSaveValidationTests.cs`
  - **Anti-coupling check:** The invariant pins persistence
    preconditions, not yellow-highlight rendering or message-box
    plumbing.

- **Invariant:** A persisted product's `ProductCode` follows the
  `"NWT" + <category code> + "-" + <ProductID>` format whenever a
  category and product id are both set.
  - **Source procedure:** `frmProductDetail::SetProductCode`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductCodeFormattingTests.cs`
  - **Anti-coupling check:** The invariant pins the durable string
    derivation, not the AfterUpdate event timing or the focus model.

- **Invariant:** A product can be deleted only when it has zero
  `OrderDetails` rows and zero `PurchaseOrderDetails` rows, and a
  confirmed delete cascades through `StockTake` and `ProductVendors`
  before the product row is removed.
  - **Source procedure:** `frmProductDetail::cmdDeleteProduct_Click`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductDeletePolicyTests.cs`
  - **Anti-coupling check:** The invariant captures durable
    delete-eligibility and cascade order, not the message-box copy
    or the `acCmdDeleteRecord` mechanism.

- **Invariant:** Reorder is blocked when a non-closed purchase order
  is already outstanding for the product; otherwise it requires at
  least one defined vendor and a positive resolved quantity.
  - **Source procedure:**
    `frmProductDetail::cmdReorderProduct_Click`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductReorderPolicyTests.cs`
  - **Anti-coupling check:** The invariant pins the eligibility
    decision (and the open-PO short-circuit), not the dialog form
    or the tab-control focus that follows it.

- **Invariant:** Reorder routes to an existing `posNew` purchase
  order for the chosen vendor when one exists (appending a line
  item, summing quantities for duplicate products) and otherwise
  creates a new purchase order in `posNew` status with a single
  line item.
  - **Source procedure:**
    `frmProductDetail::cmdReorderProduct_Click`,
    `modPurchaseOrders::AddPurchaseOrderDetail`,
    `modPurchaseOrders::ReorderProduct`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductReorderRoutingTests.cs`
  - **Anti-coupling check:** The invariant is about the
    append-vs-create routing decision and the line-item summing
    rule, not about the PO-detail form opening or MRU-list updates.

- **Invariant:** A product save and a stock-take save both
  re-allocate the product's pending order line items oldest-first
  by `OrderDate` (then ascending `OrderID`) using only statuses
  `Allocated`, `NoStock`, and `OnOrder`, and write only those same
  three statuses based on physical and on-order availability.
  - **Source procedure:** `modInventory::AllocateInventory`,
    invoked from `sfrmProductDetail_StockTake::Form_AfterUpdate`.
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductInventoryAllocationTests.cs`
  - **Anti-coupling check:** The invariant pins the durable
    allocation rule against pending order line items, not the DAO
    recordset iteration or `Edit`/`Update` syntax.

- **Invariant:** `ProductReorderQuantity` returns the
  `MinimumReorderQuantity` when current
  available-plus-on-order already meets the target plus the
  no-stock backlog, and otherwise returns the larger of the
  shortfall and the minimum reorder quantity.
  - **Source procedure:** `modInventory::ProductReorderQuantity`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductReorderQuantityTests.cs`
  - **Anti-coupling check:** The invariant is a pure arithmetic
    rule; the test exercises it without any DAO access by feeding
    the inputs the procedure reads (`MinimumReorderQuantity`,
    `TargetLevel`, available, on-order, no-stock).

## 5. Source operationalization reference

1. The user opens `frmProductList`; `ProductID_Click` opens the
   detail form with the selected product.
2. `frmProductDetail::Form_Load` fires
   `tabProducts_Change` to lazy-bind the active tab; `Form_Current`
   immediately runs `ControlStates` to populate the calculated
   inventory text boxes (`txtQtyAvailable`, `txtQtyAllocated`,
   `txtQtyOnOrder`, `txtQtyToReorder`, `txtQtyNoStock`) and to
   show or hide the Delete and Reorder buttons.
3. The user edits header fields directly (name, description, unit
   price, target level, category, discontinued) on the bound form.
   `cboProductCategoryID_AfterUpdate` calls `SetProductCode` to
   regenerate `txtProductCode`. `chkDiscontinued_AfterUpdate` calls
   `ControlStates` so the Reorder button hides when discontinued.
   `txtTargetLevel_AfterUpdate` saves the record and re-runs
   `ControlStates` so the calculated reorder quantity reflects the
   new target.
4. To save, the user navigates off the row or presses save;
   `Form_BeforeUpdate` invokes `ValidateForm(Me)` and then enforces
   `Me.txtStandardUnitCost > 0` with a separate prompt. On a clean
   save, `Form_AfterUpdate` runs `RequeryProductList` and
   `ControlStates`.
5. To record a stock take, the user activates the Inventory tab and
   adds a row in `sfrmProductDetail_StockTake`. The subform's
   `Form_Current` keeps existing rows read-only
   (`AllowEdits = NewRecord`, `AllowDeletions = False`).
   `QuantityOnHand_BeforeUpdate` confirms the entered quantity
   against `ProductAvailable(Me.ProductID)` if they disagree;
   `QuantityOnHand_AfterUpdate` stamps `StockTakeDate = Now()` and
   forces a save. `Form_AfterUpdate` then calls
   `AllocateInventory Me.ProductID`, which walks the product's
   pending order line items in `OrderDate` (then `OrderID`) order
   and updates each one to `Allocated`, `OnOrder`, or `NoStock`.
6. To reorder, the user clicks **Reorder Product**. The handler
   saves the product if dirty. If `txtQtyOnOrder` is non-zero, it
   looks up an outstanding purchase order in `posApprove`,
   `posNew`, or `posSubmitted` status via
   `qryProductPurchaseOrder`; if found, it opens that PO and stops.
   Otherwise it requires at least one entry in `ProductVendors`,
   prompts for a vendor when there are several, and resolves the
   reorder quantity (`txtQtyToReorder` first, then
   `MinimumReorderQuantity`, then a user prompt). If a `posNew` PO
   already exists for the vendor it appends a line item via
   `AddPurchaseOrderDetail`; otherwise it creates a new PO via
   `ReorderProduct`. On success the PurchaseOrders tab gets focus
   and `OpenPurchaseOrderDetailsForm` opens the resulting PO.
7. To delete a product, the user clicks **Delete Product**. The
   handler runs `DCount` against `OrderDetails` and
   `PurchaseOrderDetails`; on any non-zero count it shows a
   "Cannot Delete" prompt and stops. Otherwise it executes
   `DELETE` statements against `StockTake` and `ProductVendors` for
   the product, runs `acCmdDeleteRecord`, and refreshes the product
   list.

## 6. Component / role mapping hints

- **UI pattern:** Detail-form workflow with a tabbed "Inventory /
  Orders / Purchase Orders / Vendors" surface where each tab is a
  filtered grid of a related entity, plus a status-driven action
  rail (Save, Reorder, Delete). A natural target is a product
  detail page that loads each tab on demand and surfaces the
  cascading inventory/PO state inline rather than as a modal flow.
- **Role gating:** No explicit privilege check was confirmed for
  this workflow itself. The action rail is gated by the product
  state (`NewRecord`, `chkDiscontinued`, presence of an open PO)
  rather than by a role. The `Reorder` action does require an
  authenticated session because of the audit columns, but no
  `HasPrivilege` call was observed in `frmProductDetail`.
- **Component library mapping (optional):** Likely maps to a
  detail-with-tabs page, a vendor-selection modal (when multiple
  vendors are defined), and an inline confirm for the
  expected-vs-entered stock-take prompt. The cascading delete is a
  natural fit for a server-side transaction rather than a UI
  affordance.

## 7. Open questions / ambiguity log

- **Question:** Is it intentional that `ProductCode` is regenerated
  every time the user changes the category, even on existing
  products that may have been referenced by `ProductCode` elsewhere
  in the business?
  - **Source-of-uncertainty:** `cboProductCategoryID_AfterUpdate`
    calls `SetProductCode` unconditionally for any product with a
    `ProductID`. The committed Northwind sample does not reveal
    whether external systems consume `ProductCode` as a stable
    identifier, but `ProductCode` carries a unique index and the
    pattern hard-codes the category prefix into the value.
  - **Recommended resolution path:** Domain reviewer / business
    confirms whether category-change-rebrands are intended, or
    whether `ProductCode` should be sticky once a product has been
    persisted.

- **Question:** Should the Northwind tie-break inside
  `AllocateInventory` (oldest `OrderDate`, then ascending `OrderID`)
  be a business-visible rule or just an implementation detail?
  - **Source-of-uncertainty:** Same ambiguity raised in the
    Add an Order package's open-questions list. The
    Update-a-Product workflow inherits it because every stock take
    and every product save can re-trigger allocation across
    multiple pending orders.
  - **Recommended resolution path:** Pin the deterministic order
    in tests so the new system reproduces the existing behavior;
    surface to a domain reviewer whether the tie-break is allowed
    to differ in the rewrite.

- **Question:** Is the `ProductReorderQuantity` fallback to
  `MinimumReorderQuantity` (rather than zero) when current supply
  already meets demand intentional business policy or a UI
  convenience to keep the Reorder button always actionable?
  - **Source-of-uncertainty:** `ProductReorderQuantity`'s "first
    branch returns `intMinReorder` when supply already meets
    demand" looks deliberate but is not separately commented; the
    behavior makes the Reorder button always produce a non-zero
    quantity, which the reorder handler later re-confirms via
    `MinimumReorderQuantity` and a prompt.
  - **Recommended resolution path:** Confirm with a domain reviewer
    whether the new system should preserve the "always order at
    least the minimum" behavior or whether it should return zero
    (and disable Reorder) when no shortfall exists.

- **Question:** Is the cascading delete of `StockTake` and
  `ProductVendors` the durable rule, or should the new system
  preserve historical stock-take rows (inventory audit trail) even
  after a product is removed?
  - **Source-of-uncertainty:** `cmdDeleteProduct_Click` deletes
    StockTake rows for the product before deleting the product
    record itself. There is no soft-delete or tombstone mechanism;
    once a product is deleted its inventory history is gone.
  - **Recommended resolution path:** Domain reviewer decides
    whether stock-take history should be preserved (e.g., via a
    soft delete / archive flag) in the new system.

- **Question:** When `cmdReorderProduct_Click` finds an existing
  open purchase order for the same product (via `txtQtyOnOrder <>
  0`), the handler opens the existing PO. The user's intent could
  also have been "I want to add another batch to a fresh PO";
  should the new system preserve the redirect-to-existing behavior
  or expose both options?
  - **Source-of-uncertainty:** Source code shows the redirect is
    unconditional whenever any non-closed PO already exists for
    the product. There is no comment justifying the choice.
  - **Recommended resolution path:** Domain reviewer; the
    open-questions answer determines whether the new system can
    issue parallel POs for the same product.
