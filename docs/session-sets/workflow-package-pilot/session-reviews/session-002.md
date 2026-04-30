# Verification Round 1

## Cross-Provider Verification Report

### Analysis

I reviewed all provided source evidence (VBA, table schemas, queries, enums) against the package markdown and three test files. Below are the grounded findings.

---

### Issue Details

**1. `OrderDate` table-level default `Now()` is undocumented and untested**

`Tables/Orders.ir.json` declares `OrderDate` as `Required: true` with `DefaultValue: "Now()"`. The VBA `Form_BeforeInsert` does **not** set `OrderDate` — it only sets `EmployeeID`, `TaxRate`, and `OrderStatusID`. The package's Rule 1 and `BeginOrderEdit` test correctly omit OrderDate from the insert-seeding logic, but neither the package's business rules section nor any test captures that the system auto-defaults OrderDate to the current timestamp. A Blazor rewrite without this fact will produce orders that fail save validation (OrderDate is required) unless the developer independently discovers the DDL default.

**2. Line-item save validation is not tested**

`sfrmOrderLineItems::Form_BeforeUpdate` calls `ValidateForm(Me)` before `SetOrderDetailStatus`. The `OrderDetails` table marks `ProductID`, `Quantity`, `UnitPrice`, and `OrderDetailStatusID` as required, and `Quantity` has a validation rule `>0`. The order-header validation has a corresponding `CanSave` test pair, but `OrderLineItemBehaviorTests` has no equivalent; `PrepareForSave` skips directly to status-setting logic without gating on required-field validation.

**3. Unique constraint `(OrderID, ProductID)` omitted**

`Tables/OrderDetails.ir.json` contains the index `uidxOrderID_ProductID` (Unique: true) over `(OrderID, ProductID)`. This means each product can appear at most once per order — a data-integrity rule that shapes the UI (e.g., must edit quantity rather than add a duplicate row). Neither the package's business-rules section nor any test documents or pins this constraint.

**4. Cross-order rebalancing scope unstated**

The `modInventory::AllocateInventory` SQL is `select od.* from OrderDetails od inner join Orders o ... where od.ProductID = {0} and od.OrderDetailStatusID in (1, 4, 5) order by o.OrderDate, o.OrderID` — there is no `WHERE o.OrderID = <current>` filter. Allocation rebalances **all** pending demand for the product across every order. The package's Rule 6 says "processed oldest-first for the affected product" but does not explicitly state the cross-order scope. The test `AllocateInventory_ProcessesOldestDemandFirstWithoutPartialAllocation` uses multiple distinct `OrderId` values, implicitly modeling this, but the package prose could mislead a downstream implementer into scoping allocation per-order.

**5. Reallocation status filter `(1, 4, 5)` undocumented**

The `AllocateInventory` SQL restricts to `OrderDetailStatusID in (1, 4, 5)` — only rows in Allocated, NoStock, or OnOrder participate. Rows in New (3), Invoiced (2), or Shipped (6) are excluded. The package and tests do not document or enforce this filter. The C# `InventoryAllocator.AllocateInventory` accepts any `IEnumerable<PendingOrderDetail>` with no status precondition, meaning the test would pass even if Invoiced/Shipped rows were incorrectly included in the input set.

**6. Caching precondition (Allocated-only) not tested negatively**

`sfrmOrderLineItems::Form_Current` caches `ProductID` and `Quantity` only when `Not Me.NewRecord And Me.OrderDetailStatusID = enumOrderDetailStatus.odsAllocated`. The package's Section 4 Invariant 6 correctly says "saved allocated line item," but no test verifies that a NoStock or OnOrder line-item edit does **not** trigger prior-product reallocation. The `ReallocateAfterProductChange` test only covers the positive path.

---

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "packages/add-an-order.md — Section 3 (Business Rules)",
      "summary": "OrderDate's required table-level default `Now()` is not documented as a business rule or seeded in BeginOrderEdit, meaning a Blazor rewrite would not know to auto-default it and would fail save validation.",
      "fix": "Add a business rule: 'OrderDate defaults to the current date/time at the table level (DefaultValue: Now()); Form_BeforeInsert does not set it explicitly.' Add a characterization test or at minimum an open-question entry for how the new system should replicate this default."
    },
    {
      "severity": "Major",
      "location": "tests/…/AddAnOrder/OrderLineItemBehaviorTests.cs",
      "summary": "No characterization test for line-item required-field validation; sfrmOrderLineItems::Form_BeforeUpdate calls ValidateForm(Me) gating save on ProductID, Quantity, UnitPrice, OrderDetailStatusID (all required) and Quantity > 0, but no CanSave equivalent exists for line items.",
      "fix": "Add CanSave_ReturnsFalse/True test pair for OrderLineItemDraft mirroring the pattern in OrderCreationBehaviorTests, covering at minimum the required-field and Quantity > 0 constraints."
    },
    {
      "severity": "Major",
      "location": "packages/add-an-order.md — Section 3 (Business Rules)",
      "summary": "The unique index uidxOrderID_ProductID on OrderDetails (each product appears at most once per order) is not documented or tested, yet it constrains add/edit behavior.",
      "fix": "Add a business rule with confidence 'confirmed' citing Tables/OrderDetails.ir.json → uidxOrderID_ProductID, and add a characterization test asserting the one-product-per-order invariant."
    },
    {
      "severity": "Minor",
      "location": "packages/add-an-order.md — Section 3, Rule 6",
      "summary": "Package does not state that AllocateInventory rebalances across ALL orders for the product (the VBA SQL has no order-level filter), which could mislead a downstream implementer into per-order scoping.",
      "fix": "Amend Rule 6 to read: '…is processed oldest-first across all orders for the affected product…' and cite the unfiltered SQL in modInventory::AllocateInventory."
    },
    {
      "severity": "Minor",
      "location": "packages/add-an-order.md — Section 3, Rule 6 / InventoryAllocationBehaviorTests.cs",
      "summary": "The reallocation status filter (only rows in Allocated/NoStock/OnOrder participate; New/Invoiced/Shipped are excluded) is a business-bearing WHERE clause not documented in the package or enforced in the test model.",
      "fix": "Document the eligible-status precondition in Rule 6 and consider adding a test where a Shipped or Invoiced detail is in the input set and asserting it is excluded from rebalancing."
    },
    {
      "severity": "Minor",
      "location": "tests/…/AddAnOrder/InventoryAllocationBehaviorTests.cs",
      "summary": "No negative test for the caching precondition: prior-product reallocation should NOT fire when the edited line item was in NoStock or OnOrder status, per the Form_Current guard on odsAllocated.",
      "fix": "Add a test where cachedProductId is 0 (simulating a non-allocated row) and assert that previousProduct result is empty after ReallocateAfterProductChange."
    }
  ]
}
```

---

# Verification Round 2

## Round-2 Verification

### Issue-by-issue check

| # | Round-1 Issue | Status | Evidence |
|---|---|---|---|
| 1 | `OrderDate` default `Now()` not documented or seeded | ✅ Resolved | Section 3 adds explicit rule with evidence citing `Tables/Orders.ir.json -> DefaultValue: "Now()"`. Section 4 adds invariant. Section 5 step 4 documents it. `BeginOrderEdit_AppliesOrderDateDefaultFromDataModel` test exists and `BeginOrderEdit` now sets `order.OrderDate = _orderDateFactory()`. |
| 2 | No `CanSave` tests for line-item required-field validation | ✅ Resolved | `CanSave_ReturnsFalseWhenRequiredFieldsAreMissingOrQuantityIsNonPositive` and `CanSave_ReturnsTrueWhenLineItemIsComplete` added to `OrderLineItemBehaviorTests.cs`, mirroring the order-header pattern. Validator covers `ProductId`, `Quantity > 0`, `UnitPrice`, and `OrderId`. |
| 3 | Unique index `uidxOrderID_ProductID` not documented or tested | ✅ Resolved | Section 3 adds rule "Each product may appear at most once per order" with evidence. Section 4 invariant includes the uniqueness constraint. `CanAddProductToOrder_ReturnsFalseWhenSameProductAlreadyExistsForOrder` and `…ReturnsTrueWhenProductIsNotAlreadyOnOrder` tests added. |
| 4 | `AllocateInventory` scopes across ALL orders not documented | ✅ Resolved | Rule 9 now reads: "…is processed oldest-first **across all orders** for the affected product…" with evidence citing the unfiltered SQL. |
| 5 | Reallocation eligible-status filter not documented or tested | ✅ Resolved | Rule 9 now explicitly states: "only line items already in `Allocated`, `No Stock`, or `On Order` participate in rebalancing." `AllocateInventory_IgnoresStatusesOutsideAllocatedNoStockAndOnOrder` test added, passing through `Shipped` and `Invoiced` rows unchanged. The `InventoryAllocator` implementation filters on the correct `eligibleStatuses` set. |
| 6 | No negative test for caching precondition (`cachedProductId = 0`) | ✅ Resolved | `ReallocateInventory_DoesNotTouchPreviousProductWhenCurrentRowWasNotAllocated` test added with `cachedProductId: 0`, asserting `Assert.Empty(result.PreviousProduct)` while still verifying current-product reallocation proceeds. Matches VBA `Form_Current` guard: `Me.OrderDetailStatusID = enumOrderDetailStatus.odsAllocated`. |

### New issues scan

- **Model fidelity of `ReallocateAfterProductChange`:** Verified against VBA `ReallocateInventory` — current product is always reallocated (line 50), previous product only when cache ≠ 0 and differs from current (line 20). Test model matches.
- **`AllocateInventory` ordering:** Test model uses `.OrderBy(OrderDate).ThenBy(OrderId)`, matching VBA SQL `order by o.OrderDate, o.OrderID`. ✓
- **Status enum values:** `Allocated=1, Invoiced=2, New=3, NoStock=4, OnOrder=5, Shipped=6` — matches VBA `enumOrderDetailStatus` and the SQL `in (1, 4, 5)` filter. ✓
- **Section 3 ↔ Section 4 ↔ Tests alignment:** Every new rule in Section 3 has a corresponding invariant in Section 4 and at least one test. No orphaned invariants or untested rules.

No new issues found.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
