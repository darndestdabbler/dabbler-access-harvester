# Reference Description — `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`

> **Status:** Draft authored by the Session 7 orchestrator (Opus 4.7) from
> the source body in the symbol DB plus the surrounding form/module text
> in `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/`. The
> human (a former Access developer who knows this procedure cold) is the
> authority and may adjust this draft before adjudication; treat the
> orchestrator's draft as a starting point, not as ground truth.

## What the procedure does (definitive)

`cboCompanyTypeID_BeforeUpdate` is the BeforeUpdate event handler for
the **Company Type** combobox on the **Company Detail** form. It
enforces the business rule that a company's type may not be changed
once the company has live transactional data in the system — at any of
four points of entanglement:

1. Customer orders against this company (this company is a Customer)
2. Shipper orders against this company (this company is a Shipper)
3. Vendor purchase orders against this company (this company is a
   Vendor)
4. Product–Vendor mapping rows that name this company as the supplier
   of one or more products (this company is also a Vendor in the
   `ProductVendors` cross-table)

If *any* of those conditions hold, the procedure cancels the user's
edit, displays a localized "not allowed" dialog through the generic
modal-dialog form `frmGenericDialog`, undoes the in-progress combobox
edit, and prevents Access from committing the new `CompanyTypeID`
value. If none hold, it returns silently and the new selection is
allowed to commit through the normal Access binding path.

The handler does NOT decide *whether* the company is entangled — it
delegates that to two pre-computed signals, both of which are populated
elsewhere on the form:

- `CompanyIsActive` (a same-form function) returns True when any of
  the customer-order / shipper-order / vendor-PO / contact counts is
  non-zero. It also re-runs the `DCount` queries that populate the
  `m_lng*` cache variables — so calling it has the side effect of
  refreshing those caches.
- `m_lngVendorProductCount` is a form-level cache variable holding the
  count of `ProductVendors` rows naming this company. The
  `Form_Load` / record-change path is responsible for populating it.

The handler reads both: `If CompanyIsActive = True Or
m_lngVendorProductCount > 0 Then`. The `CompanyIsActive` call is
short-circuited first, so the vendor-product check only runs when the
order/contact counts are all zero.

## Source-grounded preconditions

- `frmCompanyDetail` is open on a current (saved) company record. The
  combobox is bound to `CompanyTypes.CompanyTypeID` (per the
  `FormControls` row in the packet) and its `BeforeUpdate` event has
  fired because the user attempted to change its value.
- `m_lngCustomerOrderCount`, `m_lngShipperOrderCount`,
  `m_lngVendorPurchaseOrderCount`, `m_lngVendorProductCount`, and
  `m_strMsgCaption` are populated. `CompanyIsActive`'s
  `Interpretation1` confirms the first three (plus contact count) are
  populated by `CompanyIsActive` itself; `m_lngVendorProductCount` and
  `m_strMsgCaption` are populated by the form's load / record-change
  path.
- `modStrings.GetString`, `modStrings.StringFormat`, and the enum value
  `modStrings.enumStrings.sCompanyDelete_CompanyTypeChange` exist and
  are callable. (They do exist in the symbol DB — the resolver did not
  bind these call sites; that is a separate substrate observation.)
- The "globals" `frmGenericDialog`, `IsFormOpen`, and `clsErrorHandler`
  exist in the wider Access project but outside the two probe modules.

## Source-grounded side effects

- **Indirect mutation of form state:** the `CompanyIsActive` short-
  circuit causes the form's `m_lng*` cache variables to be refreshed
  on every BeforeUpdate, even on the happy path where the change is
  allowed.
- **Localized dialog text:** if the rule fires, builds the dialog body
  via `GetString(sCompanyDelete_CompanyTypeChange, "change the
  Company Type", m_lngCustomerOrderCount, m_lngShipperOrderCount,
  m_lngVendorPurchaseOrderCount, "Vendor Products (" &
  m_lngVendorProductCount & ")")`. The substituted parameters
  surface the actual counts to the user inside the localized message
  template.
- **OpenArgs-style payload:** packs `Header / Message / Actions /
  FormCaption` (in that order) via `StringFormat` with the literal
  values `"Company Type Change Not Allowed"`, the dialog body,
  `"vbOK"`, and `m_strMsgCaption`.
- **Modal dialog:** opens `frmGenericDialog` via `DoCmd.OpenForm` with
  `acFormReadOnly + acDialog`, blocking the form thread until the
  dialog is dismissed.
- **Defensive close:** if `IsFormOpen("frmGenericDialog")` returns
  True after the dialog returns control, calls `DoCmd.Close acForm,
  "frmGenericDialog", acSaveNo`. (Belt-and-braces — `acDialog` should
  have closed it already, but the dialog might in some flows leave
  itself open.)
- **Reverts the combobox:** `Me.cboCompanyTypeID.Undo` rolls back the
  in-progress edit so the displayed value matches the bound record's
  saved value.
- **Cancels the BeforeUpdate event:** `Cancel = True` instructs Access
  to refuse the new `CompanyTypeID` value.
- **On unhandled error:** branches to `Err_Handler:` which delegates to
  `clsErrorHandler.HandleError(Me.Name, "cboCompanyTypeID_BeforeUpdate",
  True)` and resumes at `Exit_Handler:`. The third argument is a
  rethrow / severity flag whose semantics live outside this packet.

## What the user sees

If the company is entangled in transactional data, the user attempting
to change Company Type sees a modal dialog appear titled "Company Type
Change Not Allowed" with a localized body message that names the
specific kinds of records that exist (orders, shipper orders, vendor
purchase orders, vendor products). After dismissing the dialog, the
combobox snaps back to its previous value and the change is not saved.

If the company has no such entanglements, the user sees nothing
unusual — the new Company Type commits through the normal Access
binding path and the form's bound record updates as if the BeforeUpdate
handler never fired.

## Notes for adjudication

The orchestrator drafted this from source plus the symbol DB content;
the human is the adjudication authority. Adjustments worth considering
before grading the three probe responses:

- The exact behavior of `clsErrorHandler.HandleError` (does the third
  argument suppress, log, or rethrow?) is opaque from this packet; the
  draft says "rethrow / severity flag" as a name-shape inference.
- Whether `Form_Load` / record-change path populates `m_strMsgCaption`
  is asserted by the draft on the basis that it is referenced read-only
  here; the human knows whether the assertion holds.
- The draft attributes responsibility for populating
  `m_lngVendorProductCount` to the form-load/record-change path. Worth
  confirming against the actual `Form_Load` body, which lives in the
  same module but was not drawn into this packet.
