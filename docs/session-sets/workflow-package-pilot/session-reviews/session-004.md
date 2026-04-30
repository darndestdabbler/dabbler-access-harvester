## Analysis

### 1. Business rules grounded in evidence

All 13 business rules in Section 3 trace directly to the cited source procedures. I verified each against the provided VBA evidence:

- Status enum values match exactly: `osClosed=1, osInvoiced=2, osNew=3, osShipped=4, osPaid=5` → model enums ✓
- `cmdCreateInvoice_Click` guards (status=New, count>0, all Allocated, ShippingFee) → `CanCreateInvoice` ✓
- `cmdShipOrder_Click` guard (status=Invoiced) + `ShippingFieldsFilledOut` (ShippedDate, ShipperID, ShippingFee) → `CanShip` ✓
- `cmdReceivePayment_Click` guard (status=Shipped) + `PaidFieldsFilledOut` (PaymentMethod, PaidDate) → `CanReceivePayment` ✓
- `cmdCloseOrder_Click` guard (status=Paid) + MsgBox confirmation → `CloseOrder` confirmed parameter ✓
- `cmdDeleteOrder_Click` guard (status=New OR Invoiced) + `Form_Delete` per-line-item `AllocateInventory` → `DeleteOrder` returning product IDs ✓
- `LockControls` Select Case arrays: I verified all six branches (New, Invoiced, Shipped, Paid, Closed, null) field-by-field against source — exact match ✓

Confidence tags are all "confirmed" and all warranted by the evidence.

### 2. Tests pin implementation-independent invariants

Tests exercise the state-machine transitions, validation preconditions, delete eligibility, and lock-field policy. No test references Access MsgBox calls, DoCmd, report preview, control backcolors, or ForeShade styling. The anti-coupling checks in Section 4 are appropriate.

The `LockedFieldsFor` tests do reference Access control names (`txtTaxAmount`, `txtTotal`), but these are direct characterization of the source behavior, and Open Question #2 already flags the "business requirement vs. UX convenience" ambiguity for downstream resolution.

### 3. Missing invariants

- **`Paid` lock set not tested.** The `Shipped` test asserts PaymentMethod/PaidDate are *unlocked*, and the test name says "Until Paid," but no test verifies they *lock* at `Paid`. The transition from editable→locked for payment fields is a meaningful business characterization point.
- **`ReceivePayment` not advancing line items.** The source doesn't call `SetLineItemsStatus` for payment — correctly reflected by the model's API not accepting line items. However, no test explicitly asserts this negative invariant. Acceptable since it's enforced structurally by the method signature.

### 4. Policy overstatement

None found. Every rule maps to explicit source guards. The model's `CanShip` includes the `ShippingFee.HasValue` check that's technically redundant (ShippingFee is locked after invoicing and was required to invoice), but the source's `ShippingFieldsFilledOut` does check it, so preserving it is correct characterization.

### 5. Blocking assessment

No issues are blocking. The missing `Paid` lock-set test is a minor coverage gap — the implementation is a direct transcription from the source VBA and is validated indirectly by the consistent pattern across the other five status tests.

```json
{
  "verdict": "VERIFIED",
  "issues": [
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderEditPolicyBehaviorTests.cs",
      "summary": "No test for the Paid status lock set. PaymentMethod and PaidDate transition from unlocked (Shipped) to locked (Paid) is asserted only from the Shipped side, not verified from the Paid side.",
      "fix": "Add a test LockedFieldsFor_PaidOrderLocksPaymentFields that asserts Contains(\"PaymentMethod\") and Contains(\"PaidDate\") for OrderStatus.Paid, and optionally DoesNotContain(\"Notes\") to confirm Notes remains editable until Closed."
    }
  ]
}
```

---

# Verification Round 2

## Analysis

### Gap resolution check

The Round 1 gap requested a test verifying that `PaymentMethod` and `PaidDate` lock at `Paid`, with `Notes` still editable. The following test was added:

```csharp
[Fact]
public void LockedFieldsFor_PaidOrderLocksPaymentFieldsButNotNotes()
{
    var lockedFields = UpdateOrderWorkflow.LockedFieldsFor(OrderStatus.Paid);

    Assert.Contains("PaymentMethod", lockedFields);
    Assert.Contains("PaidDate", lockedFields);
    Assert.DoesNotContain("Notes", lockedFields);
}
```

This matches the suggested fix exactly — all three assertions are present. The lock-field progression across the full lifecycle is now covered without gaps:

| Status | Test | Key assertions |
|---|---|---|
| `null` | `NullStatusMatchesPreInsertLockSet` | exact sorted set ✓ |
| `New` | `NewOrderLocksIdentityDatesAndPaymentFieldsButNotShippingFeeOrTax` | ✓ |
| `Invoiced` | `InvoicedOrderLocksShippingFeeAndTaxSettings` | ✓ |
| `Shipped` | `ShippedOrderLeavesPaymentFieldsEditableUntilPaid` | ✓ |
| **`Paid`** | **`PaidOrderLocksPaymentFieldsButNotNotes`** | **✓ (new)** |
| `Closed` | `ClosedOrderAlsoLocksNotes` | ✓ |

### New-issue scan

- No existing tests were modified or removed.
- The new test follows the established naming convention and assertion style.
- The `Paid` → `Closed` boundary for `Notes` is now pinned from both sides (`DoesNotContain` at `Paid`, `Contains` at `Closed`).
- Status-transition tests, save-validation tests, delete-eligibility tests, and line-item-edit-policy tests are all unchanged and remain correct.
- No new coupling to Access UI mechanics was introduced.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
