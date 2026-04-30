# Workflow Package: Add an Order

> **Slug:** `add-an-order` (filename: `packages/add-an-order.md`)
> **Pilot sessions:** Session 2 (draft) + Session 3 (peer review &
> falsification) of 7
> **Author orchestrator (Session 2):** Codex GPT-5.4
> **Peer-review orchestrator (Session 3):** Claude Opus 4.7
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

Sales staff create a new customer order by opening the order-detail form,
selecting the customer, and adding one or more line items. The workflow
captures the commercial intent of a new sale while immediately classifying
each requested line item against current inventory so the organization knows
whether the order can proceed from stock or needs replenishment. The business
outcome is a persisted order in `New` status with enough defaults and
inventory signals in place for later invoice, shipping, and payment steps.

## 2. Actors and entities

- **Actors:** Sales employee or other authenticated order-entry user; the form seeds `EmployeeID` from the current logged-in user (`modGlobal::Get_UserID`).
- **Primary entity:** Order.
- **Related entities:** Customer/company, order line items, products, order status, order-detail status, inventory / stock-take / purchase-order inputs, and tax status.

## 3. Business rules with evidence + confidence tags

- **Rule:** Starting a new order seeds the order with the current user, the system tax rate, and `New` status before the first save.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `Form_BeforeInsert`; `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderStatus.osNew`; `Tables/Orders.ir.json` shows `EmployeeID`, `TaxRate`, and `OrderStatusID` are required.
  - **Confidence:** confirmed

- **Rule:** `OrderDate` defaults to the current timestamp at the table level, even though `Form_BeforeInsert` does not assign it explicitly.
  - **Evidence:** `Tables/Orders.ir.json` -> `OrderDate` has `Required: true` and `DefaultValue: "Now()"`; `Forms/frmOrderDetails.ir.json` -> `Form_BeforeInsert` sets `EmployeeID`, `TaxRate`, and `OrderStatusID` only.
  - **Confidence:** confirmed

- **Rule:** Selecting a customer defaults the order's tax status from that customer's `StandardTaxStatusID`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `CustomerID_AfterUpdate`; `Tables/Companies.ir.json` -> required `StandardTaxStatusID`; `Queries/qryCustomers.ir.json` limits the picker to customer-typed companies.
  - **Confidence:** confirmed

- **Rule:** The order cannot be saved while any required bound order field is still empty.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `Form_BeforeUpdate`; `ModulesAndClasses/modValidation.ir.json` -> `ValidateForm`, `IsValidForm`, `IsBoundToRequiredField`; `Tables/Orders.ir.json` marks `EmployeeID`, `CustomerID`, `OrderDate`, `TaxRate`, `TaxStatusID`, and `OrderStatusID` as required.
  - **Confidence:** confirmed

- **Rule:** A new line item starts in `New` status, then moves to `Allocated` only if current physical inventory can satisfy the entire requested quantity; otherwise it is preliminarily marked `No Stock`.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_BeforeInsert`, `SetOrderDetailStatus`, `Form_BeforeUpdate`; `ModulesAndClasses/modInventory.ir.json` -> `ProductAvailable`; `Tables/OrderDetails.ir.json` marks `ProductID`, `Quantity`, `UnitPrice`, and `OrderDetailStatusID` as required, with `Quantity > 0`.
  - **Confidence:** confirmed

- **Rule:** The line item cannot be saved while required fields are missing, and quantity must be greater than zero.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_BeforeUpdate`; `ModulesAndClasses/modValidation.ir.json` -> `ValidateForm`, `IsValidForm`; `Tables/OrderDetails.ir.json` marks `ProductID`, `Quantity`, `UnitPrice`, and `OrderDetailStatusID` as required and sets `Quantity` validation rule to `>0`.
  - **Confidence:** confirmed

- **Rule:** Choosing a product on a new line item copies that product's unit price into the order-detail row.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `ProductID_AfterUpdate`; `Tables/Products.ir.json` -> required `UnitPrice`; `Queries/qryOrderLineItems.ir.json` joins order details to products.
  - **Confidence:** confirmed

- **Rule:** Each product may appear at most once per order.
  - **Evidence:** `Tables/OrderDetails.ir.json` -> unique index `uidxOrderID_ProductID` over `(OrderID, ProductID)`.
  - **Confidence:** confirmed

- **Rule:** Inventory allocation is all-or-nothing per line item and is processed oldest-first across all orders for the affected product; only line items already in `Allocated`, `No Stock`, or `On Order` participate in rebalancing, and eligible lines become `Allocated`, `On Order`, or `No Stock` depending on available stock plus approved inbound stock.
  - **Evidence:** `ModulesAndClasses/modInventory.ir.json` -> `AllocateInventory` SQL filters `OrderDetailStatusID in (1, 4, 5)` and orders by `o.OrderDate, o.OrderID`; `Forms/sfrmOrderLineItems.ir.json` -> `ReallocateInventory`, `Form_AfterUpdate`, `Form_AfterDelConfirm`; `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderDetailStatus` values.
  - **Confidence:** confirmed

- **Rule:** Editing a saved line item can trigger reallocation for both the previously selected product and the currently selected product.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_Current` caches allocated rows, `ReallocateInventory` reallocates the cached prior product when the product changes, then reallocates the current product.
  - **Confidence:** confirmed

- **Rule:** Deleting a line item is a fulfillment-side event, not just a UI removal: when a line-item delete is confirmed, inventory is reallocated for that product so that downstream demand for the same product can pick up the freed capacity. Deleting the parent order itself walks each line item, deletes it, and reallocates per-product before the cascading order delete completes.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_AfterDelConfirm` calls `AllocateInventory m_RowCache.ProductID` when `Status = acDeleteOK`; `Forms/frmOrderDetails.ir.json` -> `Form_Delete` walks `sfrmOrderLineItems.Form.RecordsetClone`, deletes each row, and calls `AllocateInventory(lngProductID)` before the cascading order delete.
  - **Confidence:** confirmed

## 4. Behavioral invariants -> characterization tests

- **Invariant:** Beginning a new order stamps the responsible employee, tax rate, and `New` order status before save.
  - **Source procedure:** `frmOrderDetails::Form_BeforeInsert`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is about default business state on order creation, not about Access event timing or control focus.

- **Invariant:** New orders receive an `OrderDate` default from the data model even though the insert-seeding VBA does not assign it.
  - **Source procedure:** `Tables/Orders::OrderDate DefaultValue`, `frmOrderDetails::Form_BeforeInsert`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins persisted order state, not the Access form event used to reach that state.

- **Invariant:** Selecting a customer causes the order's tax status to follow the customer's default tax-status record.
  - **Source procedure:** `frmOrderDetails::CustomerID_AfterUpdate`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains derived order data, not the specific combo-box event or UI widget.

- **Invariant:** Saving an order with missing required fields is rejected.
  - **Source procedure:** `frmOrderDetails::Form_BeforeUpdate`, `modValidation::ValidateForm`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins persistence preconditions, not Access highlighting or message-box behavior.

- **Invariant:** A new order line item starts as `New`, copies the product price, and resolves to `Allocated` versus `No Stock` based on whether full quantity is available.
  - **Source procedure:** `sfrmOrderLineItems::Form_BeforeInsert`, `sfrmOrderLineItems::ProductID_AfterUpdate`, `sfrmOrderLineItems::SetOrderDetailStatus`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderLineItemBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is expressed as domain-state transitions for an order detail, not as datasheet or combo-box mechanics.

- **Invariant:** A line item cannot be saved unless required fields are present, quantity is positive, and the `(OrderID, ProductID)` pair stays unique inside the order.
  - **Source procedure:** `sfrmOrderLineItems::Form_BeforeUpdate`, `modValidation::ValidateForm`, `Tables/OrderDetails::uidxOrderID_ProductID`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderLineItemBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains durable persistence rules, not message boxes, highlights, or datasheet interactions.

- **Invariant:** Allocation reorders pending demand oldest-first and never partially allocates an individual line item.
  - **Source procedure:** `modInventory::AllocateInventory`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/InventoryAllocationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains fulfillment semantics and ordering policy, which survive a rewrite into a different UI architecture.

- **Invariant:** When a saved allocated line item changes product, inventory is reallocated for the prior product first and then for the current product.
  - **Source procedure:** `sfrmOrderLineItems::ReallocateInventory`, `sfrmOrderLineItems::Form_Current`, `sfrmOrderLineItems::Form_AfterUpdate`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/InventoryAllocationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is about inventory side effects of editing a line item, not about Access subform lifecycle details.

- **Invariant:** When a confirmed line-item delete completes (the cached pre-edit row was Allocated), the deleted row's product gets a reallocation pass; a cancelled delete leaves inventory untouched. Deleting the parent order reallocates each child line item's product before the cascading delete fires.
  - **Source procedure:** `sfrmOrderLineItems::Form_AfterDelConfirm`, `sfrmOrderLineItems::Form_Current` (cache precondition), `frmOrderDetails::Form_Delete`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3DeletionReallocationTests.cs`
  - **Anti-coupling check:** The invariant is about fulfillment-side side effects of removing demand, not about acDeleteOK return-code mechanics or recordset-clone iteration.

- **Invariant:** Allocation rebalancing is cross-order in scope for a given product (no per-order filter), and date ties are broken by `OrderID` ascending so rebalancing is order-independent given a fixed clock.
  - **Source procedure:** `modInventory::AllocateInventory`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3CrossOrderScopeTests.cs`
  - **Anti-coupling check:** The invariant constrains demand-pool scope and tiebreak policy, both of which survive a rewrite into any UI architecture.

## 5. Source operationalization reference

1. The user opens the order workflow from `frmOrderList`, where `cmdAddOrder_Click` calls `modOrders::OpenOrderDetailsForm` with no `OrderID`, creating a new `frmOrderDetails` instance.
2. `frmOrderDetails::Form_Load` sees no `TempVars!OpenArgs` and navigates to a new record if needed.
3. The user begins typing into the new order; `frmOrderDetails::Form_BeforeInsert` assigns the current employee, current system tax rate, and `OrderStatusID = osNew`.
4. The underlying `Orders` table contributes `OrderDate = Now()` as a data default when the new record is created.
5. The user chooses a customer in `CustomerID`; `CustomerID_AfterUpdate` looks up `Companies.StandardTaxStatusID` and writes that value into `TaxStatusID`.
6. The user fills the remaining order header fields required by the bound `Orders` record and saves; `Form_BeforeUpdate` calls `ValidateForm(Me)` and refuses the save if any required bound field is still null.
7. The user moves into `sfrmOrderLineItems`; if the parent order has not been started yet, `sfrmOrderLineItems_Enter` / `Form_Error` warn that the order must exist before line items.
8. On a new line item, `sfrmOrderLineItems::Form_BeforeInsert` seeds `OrderDetailStatusID = odsNew`.
9. The user optionally filters by category through `cboProductCategories_AfterUpdate`, which requeries the product list and then unconditionally selects the first matching product (`Me.ProductID = Me.ProductID.Column(0, 0)`) and invokes `ProductID_AfterUpdate` programmatically — so a category change always seeds a product and unit price.
10. The user selects a product; `ProductID_AfterUpdate` copies the hidden unit-price column into `UnitPrice`.
11. The user enters quantity and saves the line; `Form_BeforeUpdate` validates required fields, enforces table constraints such as positive quantity and unique `(OrderID, ProductID)`, and calls `SetOrderDetailStatus`, which marks the row `Allocated` when `ProductAvailable(ProductID) >= Quantity` and `No Stock` otherwise.
12. After save, `Form_AfterUpdate` runs `ReallocateInventory`, which recalculates allocation across all orders for the current product and, if the product changed on an already allocated row, for the previous product as well.
13. The order remains in `New` status, with line items carrying the immediate allocation signal that later invoice / shipping steps will consume.

## 6. Component / role mapping hints

- **UI pattern:** Master-detail create flow: order header plus editable line-item grid/subform, with immediate derived-field defaults and fulfillment-state feedback.
- **Role gating:** No explicit privilege gate was confirmed in the create path itself; the workflow appears to rely primarily on authenticated employee context rather than an Add-specific privilege check.
- **Component library mapping (optional):** Deferred to library-readiness pass; likely maps cleanly to a create form with an embedded editable collection/grid for line items and inline validation summaries.

## 7. Open questions / ambiguity log

- **Question:** Is the customer-derived tax status only a default the user may later override, or is the chosen customer's tax status intended to remain authoritative through save?
  - **Source-of-uncertainty:** `CustomerID_AfterUpdate` populates `TaxStatusID`, but the reviewed Add-an-Order evidence does not show a corresponding guard that prevents later manual edits.
  - **Recommended resolution path:** Human review of the running Access app, or deeper review during the downstream Blazor implementation pilot if tax-policy ownership becomes important.

- **Question:** Should the builder preserve the exact oldest-first tie-breaker (`OrderDate`, then `OrderID`) for allocation, or is "oldest demand first" sufficient if timestamps are modeled differently in the new system?
  - **Source-of-uncertainty:** `modInventory::AllocateInventory` encodes the precise SQL ordering, but the business intent behind the `OrderID` tiebreak is not documented separately.
  - **Recommended resolution path:** Preserve the policy in the initial Blazor slice, then confirm with a domain reviewer whether the exact secondary ordering is business-visible or merely an implementation choice.

- **Question:** Should deleting a `No Stock` or `On Order` line item also trigger reallocation for that product, or is the source's "Allocated-only" cache precondition the intended behavior?
  - **Source-of-uncertainty:** `sfrmOrderLineItems::Form_Current` only populates `m_RowCache` when the row is `Allocated`; therefore `Form_AfterDelConfirm`'s `AllocateInventory m_RowCache.ProductID` becomes a no-op (productId = 0) when a `No Stock` or `On Order` row is deleted. This may be a deliberate optimization (no allocated capacity to free) or an oversight that leaves freed `On Order` capacity un-rebalanced. Section 4's deletion invariant matches the source behavior, not necessarily the intended behavior.
  - **Recommended resolution path:** Confirm with a domain reviewer whether the Blazor builder should reallocate on every line-item delete (regardless of pre-delete status). If yes, the new system should diverge from the source here and the invariant test must be updated.

- **Question:** Should the new system propagate `SetOrderDetailStatus`'s "do nothing if Product or Quantity is missing" defensive exit, or is the table-level required-field validation sufficient on its own?
  - **Source-of-uncertainty:** `sfrmOrderLineItems::SetOrderDetailStatus` exits silently when `IsNull(Me.ProductID) Or IsNull(Me.Quantity)` rather than letting allocation logic see partial state. Combined with `Form_BeforeUpdate`'s required-field validation, the line item cannot persist in that state anyway, so this guard is defense-in-depth. Whether the Blazor builder needs an analogous guard depends on its allocation-trigger architecture.
  - **Recommended resolution path:** Defer to the implementation pilot — if allocation is invoked from a single save-point that runs after validation, the guard is redundant; if allocation can be invoked mid-edit (e.g., for live status indicators), the guard or its equivalent is required.

- **Question:** Should the `ProductID_AfterUpdate` self-correction (resetting `ProductID = 0` when the combo's hidden `UnitPrice` column is null) be reproduced in the new system, or is it an Access-specific workaround for the cascading combobox after a programmatic category change?
  - **Source-of-uncertainty:** The VBA comment says the null-column case "can happen when changing product category on a new row without entering any other data," indicating an Access widget-state quirk. The behavior preserves data integrity (no priceless line item) but does so via UI-mechanic side-effects.
  - **Recommended resolution path:** The Blazor builder probably solves this implicitly by not exposing a no-price intermediate state in the product picker. Flag the source behavior so the implementer recognizes it as a workaround rather than a business rule.
