# Workflow Package: Update an Order

> **Slug:** `update-an-order` (filename: `packages/update-an-order.md`)
> **Pilot session:** Session 4 of 7
> **Author orchestrator:** Codex GPT-5.4
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

Sales, fulfillment, and back-office staff update existing orders as
those orders move from draft fulfillment through invoicing, shipping,
payment, closure, or deletion. The workflow preserves a single
status-driven order record while enforcing that downstream transitions
only happen when the required upstream data is already present. The
business outcome is an order whose header fields, line-item states, and
allowed edits stay consistent with its current lifecycle stage.

## 2. Actors and entities

- **Actors:** Authenticated order-management staff working from the
  order list and the order-detail form; the source evidence does not
  show per-action privilege gates inside this workflow.
- **Primary entity:** Order.
- **Related entities:** Order line items, products/inventory
  allocation, shipper, payment details, invoice output, customer,
  employee, tax status, and order / order-detail status lookups.

## 3. Business rules with evidence + confidence tags

- **Rule:** Existing orders are opened from the order list by clicking
  `OrderID`, and the detail form loads a single-order record source for
  that selected `OrderID`.
  - **Evidence:** `Forms/frmOrderList.ir.json` -> `OrderID_Click`;
    `ModulesAndClasses/modOrders.ir.json` -> `OpenOrderDetailsForm`;
    `Forms/frmOrderDetails.ir.json` -> `Form_Load`.
  - **Confidence:** confirmed

- **Rule:** Edited orders still cannot be saved while required bound
  order fields are missing.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `Form_BeforeUpdate`; `ModulesAndClasses/modValidation.ir.json` ->
    `ValidateForm`, `IsValidForm`, `IsBoundToRequiredField`;
    `Tables/Orders.ir.json` marks `EmployeeID`, `CustomerID`,
    `OrderDate`, `TaxRate`, `TaxStatusID`, and `OrderStatusID` as
    required.
  - **Confidence:** confirmed

- **Rule:** Only orders in `New` status can be invoiced.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdCreateInvoice_Click`; `ModulesAndClasses/modGlobal.ir.json` ->
    `enumOrderStatus`.
  - **Confidence:** confirmed

- **Rule:** Invoicing requires at least one line item, all line items
  already `Allocated`, and a filled `ShippingFee`; partial invoicing is
  not allowed.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdCreateInvoice_Click`, `AllInvoiceLineItemsHaveStatus`,
    `ShippingFeeFilledOut`; `ModulesAndClasses/modGlobal.ir.json` ->
    `enumOrderDetailStatus`.
  - **Confidence:** confirmed

- **Rule:** Successfully creating an invoice advances the order to
  `Invoiced`, stamps `InvoiceDate = Now()`, and sets every line item on
  the order to `Invoiced`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdCreateInvoice_Click`, `SetLineItemsStatus`;
    `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderStatus`,
    `enumOrderDetailStatus`; `Tables/Orders.ir.json` ->
    `InvoiceDate`; `Tables/OrderDetails.ir.json` ->
    `OrderDetailStatusID`.
  - **Confidence:** confirmed

- **Rule:** Only orders in `Invoiced` status can be shipped, and
  shipping requires `ShippedDate`, `ShipperID`, and `ShippingFee`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdShipOrder_Click`, `ShippingFieldsFilledOut`;
    `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderStatus`;
    `Tables/Orders.ir.json` -> `ShippedDate`, `ShipperID`,
    `ShippingFee`.
  - **Confidence:** confirmed

- **Rule:** Successfully shipping an order advances the order to
  `Shipped` and sets every line item on the order to `Shipped`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdShipOrder_Click`, `SetLineItemsStatus`;
    `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderStatus`,
    `enumOrderDetailStatus`.
  - **Confidence:** confirmed

- **Rule:** Only orders in `Shipped` status can receive payment, and
  payment requires `PaymentMethod` and `PaidDate`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdReceivePayment_Click`, `PaidFieldsFilledOut`;
    `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderStatus`;
    `Tables/Orders.ir.json` -> `PaymentMethod`, `PaidDate`.
  - **Confidence:** confirmed

- **Rule:** Successfully receiving payment advances the order to
  `Paid`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdReceivePayment_Click`; `ModulesAndClasses/modGlobal.ir.json` ->
    `enumOrderStatus`.
  - **Confidence:** confirmed

- **Rule:** Only orders in `Paid` status can be closed; once the user
  confirms closure, the order status becomes `Closed`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdCloseOrder_Click`; `ModulesAndClasses/modGlobal.ir.json` ->
    `enumOrderStatus`.
  - **Confidence:** confirmed

- **Rule:** Orders may be deleted only while still `New` or
  `Invoiced`; when deletion is confirmed, the form deletes each line
  item and reallocates inventory per product before the cascading order
  delete completes.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` ->
    `cmdDeleteOrder_Click`, `Form_Delete`;
    `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderStatus`;
    `ModulesAndClasses/modInventory.ir.json` -> `AllocateInventory`.
  - **Confidence:** confirmed

- **Rule:** Existing-order line items can only be added, edited, or
  deleted while the order remains in `New` status.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `SetFormStatus`,
    `Form_Current`; `ModulesAndClasses/modGlobal.ir.json` ->
    `enumOrderStatus`.
  - **Confidence:** confirmed

- **Rule:** Header-field editability tightens as order status advances:
  customer, employee, and order-date fields lock immediately after the
  order is created; tax settings and shipping fee lock once invoiced;
  shipment fields lock once shipped; payment fields lock once paid; and
  even `Notes` locks once the order is closed.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `LockControls`.
  - **Confidence:** confirmed

## 4. Behavioral invariants -> characterization tests

- **Invariant:** Edited orders still reject saves when required order
  fields are missing.
  - **Source procedure:** `frmOrderDetails::Form_BeforeUpdate`,
    `modValidation::ValidateForm`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderEditPolicyBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins persistence
    preconditions, not highlight colors or message-box behavior.

- **Invariant:** Invoicing is allowed only for `New` orders that have
  at least one line item, all line items already `Allocated`, and a
  shipping fee.
  - **Source procedure:** `frmOrderDetails::cmdCreateInvoice_Click`,
    `frmOrderDetails::AllInvoiceLineItemsHaveStatus`,
    `frmOrderDetails::ShippingFeeFilledOut`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderStatusTransitionBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains domain
    transition eligibility, not report-preview focus or button clicks.

- **Invariant:** Successful invoicing advances the order to
  `Invoiced`, stamps `InvoiceDate`, and marks all line items
  `Invoiced`.
  - **Source procedure:** `frmOrderDetails::cmdCreateInvoice_Click`,
    `frmOrderDetails::SetLineItemsStatus`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderStatusTransitionBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins durable order and
    line-item state, not the Access report-opening mechanics.

- **Invariant:** Shipping is allowed only for `Invoiced` orders with
  shipment fields filled, and success advances the order and all line
  items to `Shipped`.
  - **Source procedure:** `frmOrderDetails::cmdShipOrder_Click`,
    `frmOrderDetails::ShippingFieldsFilledOut`,
    `frmOrderDetails::SetLineItemsStatus`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderStatusTransitionBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is about lifecycle
    transitions and fulfillment state, not command-button choreography.

- **Invariant:** Payment is allowed only for `Shipped` orders with
  payment fields filled, and success advances the order to `Paid`.
  - **Source procedure:** `frmOrderDetails::cmdReceivePayment_Click`,
    `frmOrderDetails::PaidFieldsFilledOut`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderStatusTransitionBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains business-stage
    advancement, not message-box display.

- **Invariant:** Only `Paid` orders can close, and closure only takes
  effect when the user confirms it.
  - **Source procedure:** `frmOrderDetails::cmdCloseOrder_Click`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderStatusTransitionBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins the confirmation-gated
    state transition, not the particular Yes/No dialog API.

- **Invariant:** Orders are deletable only in `New` or `Invoiced`
  status, and a confirmed delete triggers per-line-item inventory
  reallocation before the order disappears.
  - **Source procedure:** `frmOrderDetails::cmdDeleteOrder_Click`,
    `frmOrderDetails::Form_Delete`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderEditPolicyBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is about durable delete
    eligibility and inventory side effects, not recordset-clone
    iteration details.

- **Invariant:** Line-item editing is allowed only while the order is
  still `New`, and the set of editable header fields narrows as the
  order advances through `Invoiced`, `Shipped`, `Paid`, and `Closed`.
  - **Source procedure:** `frmOrderDetails::SetFormStatus`,
    `frmOrderDetails::LockControls`, `frmOrderDetails::Form_Current`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderEditPolicyBehaviorTests.cs`
  - **Anti-coupling check:** The invariant captures the stateful edit
    policy, not button shade values or control-backcolor styling.

## 5. Source operationalization reference

1. The user opens `frmOrderList`, whose default filter shows open
   orders (`OrderStatusID <> 1`, meaning not `Closed`).
2. The user clicks an existing `OrderID`; `frmOrderList::OrderID_Click`
   calls `modOrders::OpenOrderDetailsForm Me.OrderID`.
3. `frmOrderDetails::Form_Load` reads `TempVars!OpenArgs`, switches the
   form to a single-record `qryOrder` record source for that `OrderID`,
   and opens the selected order.
4. `Form_Current` calls `SetFormStatus` and `LockControls`, which apply
   the order-status-specific edit policy to header fields and the line
   item subform.
5. While the order is still `New`, the user may keep editing header
   data and line items; once the status advances, the subform becomes
   read-only and more header fields lock.
6. To invoice the order, the user clicks **Create Invoice**. The code
   saves the order, requires status `New`, requires at least one line
   item, requires all line items to be `Allocated`, and requires a
   shipping fee. On success, Access opens the invoice report, sets
   `OrderStatusID = osInvoiced`, stamps `InvoiceDate = Now()`, and
   updates all line items to `odsInvoiced`.
7. To ship the order, the user fills `ShippedDate`, `ShipperID`, and
   `ShippingFee`, then clicks **Ship Order**. This is allowed only from
   `Invoiced`; success sets the order to `Shipped` and all line items to
   `odsShipped`.
8. To receive payment, the user fills `PaymentMethod` and `PaidDate`,
   then clicks **Receive Payment**. This is allowed only from
   `Shipped`; success sets the order to `Paid`.
9. To close the order, the user clicks **Close Order** on a `Paid`
   order and confirms the prompt. Success sets the order to `Closed`.
10. To delete the order, the user clicks **Delete Order** while the
   order is still `New` or `Invoiced` and confirms the prompt. The form
   deletes each line item first, calling `AllocateInventory(ProductID)`
   for each one, and then lets the order delete complete.

## 6. Component / role mapping hints

- **UI pattern:** Detail-form workflow with a status-driven command
  rail. A natural target is a read-write order detail page that
  progressively narrows the editable surface as the order advances.
- **Role gating:** No explicit privilege check was confirmed in the
  update workflow itself. The main gating is lifecycle state, not a
  role-specific branch.
- **Component library mapping (optional):** Likely maps to a status
  timeline / action bar plus an order-detail editor whose sections can
  switch between editable and read-only modes based on order state.

## 7. Open questions / ambiguity log

- **Question:** Are there intended chronological or range rules for
  `InvoiceDate`, `ShippedDate`, and `PaidDate`, or are the current
  fields intentionally unconstrained beyond presence checks?
  - **Source-of-uncertainty:** The comment block at the top of
    `frmOrderDetails` explicitly says these rules have not been
    implemented and lists examples such as future-date limits and
    whether dates may be entered before earlier lifecycle stages.
  - **Recommended resolution path:** Treat chronology validation as an
    explicit downstream design decision; confirm with a domain reviewer
    before the Blazor implementation hard-codes date rules.

- **Question:** Is the exact `LockControls` field matrix a business
  requirement, or partly an Access UX convenience?
  - **Source-of-uncertainty:** The code comment says these are
    Northwind-specific business rules, but the mechanism is a
    form-control lock matrix and some choices, like locking `Notes` only
    when closed, are not separately justified elsewhere.
  - **Recommended resolution path:** Preserve the locked/unlocked policy
    initially, then confirm with a domain reviewer whether every field
    boundary is semantically important or whether some are just UI
    defaults that can be relaxed in the rewrite.

- **Question:** Should the new system preserve the source's tight
  coupling between invoice creation and report preview, or is the
  durable business requirement only the transition to `Invoiced` plus
  line-item status updates?
  - **Source-of-uncertainty:** `cmdCreateInvoice_Click` opens
    `rptInvoice` as part of the same command that advances order state,
    but Section 4 intentionally excludes report-preview mechanics from
    characterization tests.
  - **Recommended resolution path:** Preserve the state-machine
    semantics as the hard requirement; let the downstream builder choose
    a modern invoice UX unless a human reviewer says invoice-preview is
    mandatory at transition time.
