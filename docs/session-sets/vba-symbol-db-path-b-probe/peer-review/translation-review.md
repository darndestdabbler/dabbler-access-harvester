# Session 3 Translation Review (Independent Peer Review)

**Reviewer:** Claude Opus 4.7 (Claude Code), Session 3 of 3.
**Subject:** [`FrmCompanyDetailTranslationProbe.cs`](../../../../tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbe.cs)
authored by Codex / GPT-5.5 in Session 2.
**Source of truth:** the `cboCompanyTypeID_BeforeUpdate` and
`CompanyIsActive` source bodies captured in
[`translation-packet.md`](../translation-packet.md).
**Independence:** I did not see the Session 2 author's reasoning
beyond what is documented in
[`translation-rationale.md`](../translation/translation-rationale.md);
my findings below were arrived at by reading the C# against the VBA
source body line by line before consulting Session 2's rationale.

## Verdict (one line)

**Translation is faithful at the observable-behavior level for the
inputs Session 2's tests cover.** Three real-but-acceptable
divergences and four under-tested invariants documented below.
Session 3 tests target the under-tested invariants; one of them
demonstrably forces a translation choice that Session 2's tests did
not pin.

---

## Divergences From The VBA — Acceptable

### D1. `If A Or B` translated via De Morgan as `if (!A && !B) early-return`

The VBA gate is line 20:

```vba
If CompanyIsActive = True Or m_lngVendorProductCount > 0 Then ... End If
```

The translator rewrote this as the negated guard:

```csharp
if (!CompanyIsActive(form) && _vendorProductCount <= 0)
{
    return false;
}
```

This is the De Morgan transform of `Or`-then-block into
`Nor`-then-skip. **Behaviorally equivalent for this procedure** because:

1. `CompanyIsActive(form)` is the LEFT operand of `&&` and therefore
   always evaluated, so the field-population side effect (the five
   `_daoCounts.Count(...)` assignments) always runs — matching VBA
   `Or`'s eager-evaluation semantics.
2. `_vendorProductCount` is read after `CompanyIsActive(form)` returned,
   so the read sees the value just populated.

**Why I flag it anyway.** The correctness of the C# form depends on
both operands of `&&` being evaluated when needed and on the order in
which they're evaluated. A future maintainer who "simplifies" by
inverting back to `if (CompanyIsActive(form) || _vendorProductCount > 0)`
would silently introduce a bug only when `CompanyIsActive` returns
true: short-circuit `||` would then skip reading `_vendorProductCount`
— harmless here because the dialog still opens, but the test suite
would not detect a parallel refactor that moved the side-effect out of
`CompanyIsActive`. Session 3 test
`Session3RegressionTests.CompanyIsActive_PopulatesAllFiveCachedCounts_EvenWhenInactive`
locks the eager-side-effect property explicitly so a regression here
trips a test rather than a customer.

### D2. `IDaoCounts.Count(table, criteria)` drops the VBA `DCount` "expression" argument

VBA `DCount("*", "Contacts", "CompanyID=42")` takes three arguments:
the expression to count, the domain, the criteria. The C# faked
interface drops the expression argument and pins the operation to
`Count(*)`-equivalent semantics:

```csharp
public interface IDaoCounts
{
    long Count(string tableName, string criteria);
}
```

The translation is correct for THIS procedure because every call site
in the source body uses `"*"`. Path B at scale would need to expose
the expression argument (DCount on a non-PK column behaves differently
when the column is nullable). Worth flagging as an interface-shape
debt rather than a behavioral defect in this probe.

### D3. `DoCmd.OpenForm "frmGenericDialog", acNormal, , , acFormReadOnly, acDialog, ...` collapsed into `IDialogService.OpenForm(name, openArgs)`

VBA passes seven positional arguments; the relevant ones are
`acFormReadOnly` (data mode = read-only) and `acDialog` (window mode
= modal). The C# seam loses the read-only/modal flags entirely and
treats those as implementation details of the IDialogService
adapter.

Defensible for this probe — the spec was explicit that "translated C#
uses POCO methods plus *faked* form/control/DAO interfaces" and that
no UI-framework commitment is in scope — but the lost flags are real
behavioral signal that downstream Path B would need to recover. The
`symbol-db-gaps.md` note about Access UI constants being unresolved
is the same gap viewed from the symbol-DB side.

---

## Behavioral Invariants Session 2's Tests Did Not Pin

These are properties of the VBA behavior the translation appears to
preserve but Session 2's five tests do not exercise. Each is the seed
for a Session 3 test below.

### U1. `CompanyIsActive` populates ALL five cached counts (Contacts, three Orders, ProductVendors), not just the ones used in the gate

VBA lines 20-60 of `CompanyIsActive` populate five module-level fields
unconditionally. The gate only consults two of them
(`m_lngCustomerOrderCount + m_lngShipperOrderCount + m_lngVendorPurchaseOrderCount`
for the active determination, plus `m_lngVendorProductCount` in the
caller). The other count, `m_lngCompanyContactCount`, is computed but
not read in this procedure — yet it is observable downstream (other
event handlers on the form rely on it). Session 2's
test #5 (`...FormatsDaoCriteriaFromCompanyId`) verifies five DAO calls
are made but does not snapshot the resulting count fields.

**Session 3 test:** `CompanyIsActive_PopulatesAllFiveCachedCounts_EvenWhenInactive`
asserts the snapshot after a no-orders / no-vendor-products call,
ensuring all five fields land the right values from the DAO.

### U2. The dialog's `CloseForm` is NOT called when the dialog is already closed

VBA line 60-80:

```vba
If IsFormOpen("frmGenericDialog") Then
    DoCmd.Close acForm, "frmGenericDialog", acSaveNo
End If
```

The C# preserves the guard:

```csharp
if (_dialogs.IsFormOpen("frmGenericDialog"))
{
    _dialogs.CloseForm("frmGenericDialog");
}
```

Session 2's blocking-path test sets `IsOpen = true` and confirms
`CloseForm` is called. There is no test confirming that when the
dialog already closed itself (e.g., user dismissed it modally),
`CloseForm` is NOT called. A translator who dropped the `IsFormOpen`
guard — easy mistake under "simplify the dialog dance" pressure —
would pass Session 2's tests.

**Session 3 test:** `CboCompanyTypeIdBeforeUpdate_DoesNotCloseDialog_WhenAlreadyClosed`
asserts `LastClosedFormName` stays null in the active-company,
dialog-already-dismissed case while still confirming
`Cancel == true` and `Undo` was called.

### U3. Inner `CompanyIsActive` error path uses `showUserMessage = false` (default), distinct from outer's `showUserMessage = true`

VBA distinguishes two error sites:

- Outer (line 130): `clsErrorHandler.HandleError Me.Name, "cboCompanyTypeID_BeforeUpdate", True`
- Inner (`CompanyIsActive` line 140): `clsErrorHandler.HandleError Me.Name, "CompanyIsActive"` — only two args, third defaults to whatever the called signature defaults to.

The C# `IErrorHandler.HandleError(string, string, bool)` has
`bool showUserMessage = false` default, so the inner C# call
correctly inherits `false`. Session 2's test #4 verifies the outer
call passes `true`, but no test verifies the inner call passes the
default `false`. A translator who mistakenly hard-coded `true` in the
inner catch would pass all five Session 2 tests.

**Session 3 test:** `CompanyIsActive_DelegatesErrorWithDefaultShowUserMessage_OnDaoFailure`
configures `IDaoCounts` to throw, then asserts that the single
recorded `HandleError` call has `ProcedureName == "CompanyIsActive"`
and `ShowUserMessage == false`.

### U4. The active-company-with-zero-vendor-products path still blocks (left side of `Or`)

Session 2's test #1 has `vendorProductCount = 4` AND active orders;
test #3 isolates the right side of `Or` (vendor-products-only). Neither
test isolates the left side: active orders with zero vendor products.
A translator who somehow conflated the two conditions (e.g., wrote
`if (!Active || vendorCount > 0)`) would pass tests #1 and #3 but
might still fail this isolated case.

**Session 3 test:** `CboCompanyTypeIdBeforeUpdate_Cancels_WhenActiveButNoVendorProducts`
sets order counts > 0 and `vendorProductCount = 0`, asserts
`Cancel == true`, dialog opened, `Undo` called.

---

## Things I Looked For And Did Not Find Wrong

- **Operator precedence & short-circuit semantics** — Section D1
  above. The De Morgan rewrite is the only non-literal transform of
  the gate; it preserves the eager-evaluation invariant by accident
  rather than design, but it does preserve it.
- **`Cancel As Integer` → `bool`** — documented in the rationale and
  test contract; reasonable.
- **Error swallowing** — both VBA and C# catch and log; no rethrow.
- **`Undo` only on the blocked path** — preserved.
- **`Vendor Products (n)` interpolation** — `$"Vendor Products ({_vendorProductCount})"`
  matches the VBA `"Vendor Products (" & m_lngVendorProductCount & ")"`.
- **`StringFormat` semantics** — the translator's local
  `ProbeVbaFormatting.StringFormat` mimics the VBA `modStrings.StringFormat`
  positional `{0}`-replacement (with `InvariantCulture` numeric
  conversion). Not byte-identical to VBA's behavior on edge cases
  (e.g., VBA's nested-array unwrapping of `params(0)`), but those
  edge cases do not arise in this probe's call sites.
- **`StringFormatSql` quoting** — inferred (the source body for
  `StringFormatSQL` was not in the packet); the translator's
  inference (single-quote strings, `#`-wrap dates, raw numerics) is
  reasonable for the `CompanyID={0}` case actually exercised. **Real
  but acceptable inference**; flagged as a Path-B-at-scale interface
  debt similar to D2.

## What This Review Did NOT Cover

- The `tools/symbol-db/probe/build_packet.py` extension Session 2
  authored — out of scope for the translation review (build_packet is
  packet-generation code, not behavioral translation).
- The `AccessHarvester.TranslationProbe.Tests.csproj` file — also out
  of scope; reviewed only as needed to confirm the project builds.
- Performance, allocations, memory shape, async behavior — none of
  these are signals the probe is designed to surface.
