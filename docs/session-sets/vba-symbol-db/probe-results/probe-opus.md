# Comprehension Probe — Opus 4.7 (in-conversation)

> Authored in-conversation by the Session 7 orchestrator (claude-opus-4-7,
> effort=high) per the project-guidance convention "When the orchestrator's
> model matches the routing target for a given task type, handle the task
> in-conversation rather than calling `route()`." Responding to the same
> probe prompt and the same `comprehension-packet.md` that gemini-pro and
> gpt-5-4 received.

## 1. What does the procedure do?

`cboCompanyTypeID_BeforeUpdate` is the BeforeUpdate event handler for the
Company Type combobox on `frmCompanyDetail`. It enforces the business
rule (stated verbatim in the comment block) that a company's type may not
be changed while the company is still tied to live transactional data.
It reads two pre-computed signals — `CompanyIsActive` (a function on the
same form whose `Interpretation1` says it returns True when any of four
related-record counts is non-zero) and `m_lngVendorProductCount` (a
form-level cache populated elsewhere, presumably during `Form_Load` or
when the bound record changes) — and if either is true it cancels the
edit, shows a localized "not allowed" dialog through the
`frmGenericDialog` modal-dialog form, and reverts the combobox to its
prior value via `.Undo`.

## 2. What are its preconditions?

- `frmCompanyDetail` is open on a current company record and the user has
  attempted to change `cboCompanyTypeID`'s value (which is bound to
  `CompanyTypes.CompanyTypeID` per the FormControls row in the packet).
- `m_lngCustomerOrderCount`, `m_lngShipperOrderCount`,
  `m_lngVendorPurchaseOrderCount`, `m_lngVendorProductCount`, and
  `m_strMsgCaption` have been initialized for the current record.
  `CompanyIsActive`'s `Interpretation1` confirms it both reads and
  *populates* those counters, so the precondition is really "either the
  load path that populated them has already run, or `CompanyIsActive`
  itself populates them on first call".
- The packet shows `frmGenericDialog`, `GetString`, `StringFormat`, and
  `IsFormOpen` are referenced but not present in the symbol DB — the
  procedure assumes they exist at runtime in the wider Access project.
  The same applies to the `clsErrorHandler` static-class reference and
  the enum value `sCompanyDelete_CompanyTypeChange`.

## 3. What side effects does it have?

- Calls `CompanyIsActive`, which (per its `Interpretation1` in the packet)
  re-runs `DCount` against `Orders`, `PurchaseOrders`, `ProductVendors`,
  and `Contacts` and writes the results into the form's `m_lng*`
  module-level caches. So this handler indirectly mutates form-level
  state on every entry.
- If the gating condition holds, builds a localized message via
  `GetString(sCompanyDelete_CompanyTypeChange, ...)` and an
  `OpenArgs`-style header/message/actions/caption string via
  `StringFormat(...)`.
- Opens `frmGenericDialog` via `DoCmd.OpenForm` in `acFormReadOnly +
  acDialog` mode — modal, blocks until the dialog is dismissed.
- Defensively closes `frmGenericDialog` afterwards if `IsFormOpen`
  reports it is still open (without saving).
- Calls `Me.cboCompanyTypeID.Undo`, reverting the in-progress edit on
  the bound combobox.
- Sets the `ByRef Cancel As Integer` parameter to `True`, which causes
  Access to abort the BeforeUpdate event and refuse to commit the new
  value to `CompanyTypes.CompanyTypeID`.
- On any unhandled error inside the `If` block, branches to
  `Err_Handler:` which delegates to
  `clsErrorHandler.HandleError(Me.Name, "cboCompanyTypeID_BeforeUpdate", True)`.
  The third argument is presumably "rethrow / log severity" but the
  packet does not contain `clsErrorHandler` so that is inference.

## 4. What does the user see?

If the company has any orders, shipper orders, vendor purchase orders,
or product-vendor links, the user's attempt to change the Company Type
is rejected: a modal dialog (`frmGenericDialog`) appears with a
localized header "Company Type Change Not Allowed", a localized body
message that names the specific kinds of records that exist, and the
caption that `m_strMsgCaption` holds. After the user dismisses the
dialog, the combobox snaps back to its previous value and the form's
underlying record is *not* updated. If none of those signals fire, the
handler returns silently and the combobox commits its new value through
the normal Access binding path.

## 5. Where is the symbol DB MISSING information?

This is the part of the probe that matters most for the verdict.

**Blocking gaps (would prevent a downstream agent from interpreting this
correctly without re-reading source):**

- None for *this* procedure, given the strong `Interpretation1` already
  on `CompanyIsActive` and the m_lng* counters. The handler's logic is
  shallow enough that the packet plus callee summaries is sufficient.
  The "blocking" qualifier earned by gemini-pro for `Me`/`DoCmd` is
  overcalled — both are universally-known VBA built-ins and any
  downstream agent that cannot interpret them is not the right
  consumer for this DB.

**Useful gaps (would materially improve fidelity):**

- **Cross-module resolution gap — significant.** The packet's "Direct
  References" table lists `GetString`, `StringFormat`, `IsFormOpen`, and
  `sCompanyDelete_CompanyTypeChange` as `unresolved / not-in-scope`.
  But `modStrings.GetString`, `modStrings.StringFormat`, and
  `modStrings.enumStrings.sCompanyDelete_CompanyTypeChange` are *all*
  present in the same symbol DB (visible in `tools/symbol-db/symbol-db.sqlite`
  under `module_fqn='modStrings'`). The resolver did not bind cross-module
  references for these calls, so the comprehension probe was forced to
  rediscover what `GetString` does from its name and call shape rather
  than from the populated `Interpretation1`. **This is a real
  Session 1-2 resolver-pipeline finding, not a probe limitation.**
  A downstream consumer would land in the same blind spot at scale.
- **Missing `frmGenericDialog`.** The probe modules cover
  `Form_frmCompanyDetail` and `modStrings` only. `frmGenericDialog` is a
  full first-class form in NorthwindDevEdition but was not extracted into
  the symbol DB because Session 1 deliberately scoped the build to the
  two probe modules. For full-DB scaling this resolves naturally; it is
  flagged here only because the probe context highlights it.
- **Missing `IsFormOpen` and `clsErrorHandler`.** Same shape — they live
  outside the two probe modules. Same resolution path (full-DB scaling)
  applies.
- **Tables coverage is partial.** `Tables Bound by Controls on This
  Form` includes `CompanyTypes` (the lookup behind `cboCompanyTypeID`
  itself), but the four tables that `CompanyIsActive` actually queries
  (`Orders`, `PurchaseOrders`, `ProductVendors`, `Contacts`) appear only
  inside the `Interpretation1` prose and as `Tables` rows that are not
  joined into the packet because no control on this form binds to them
  directly. The relationship is *behavioral*, not *binding*. A
  comprehension probe at scale would benefit from a "tables referenced
  via DCount/DLookup/SQL inside this procedure or its callees"
  pre-computation. Not blocking for *this* procedure, but a useful
  follow-up signal for any DB that uses the same DCount-cache pattern.

## 6. Confidence

**0.92** — High. The handler's intent is stated in the comment block,
the control flow is shallow and linear, the resolved `m_lng*` caches and
the `CompanyIsActive` summary supply the gating semantics, and the
unresolved cross-module identifiers (`GetString`, `StringFormat`,
`sCompanyDelete_CompanyTypeChange`) are interpretable from name and
call shape with high fidelity even without the symbol-DB binding. The
remaining 0.08 of uncertainty is for the exact text of the localized
message (we know it talks about customer/shipper/vendor counts, but the
exact template lives in the unresolved enum value) and for the exact
behaviour of `clsErrorHandler.HandleError`'s third argument.

```json
{
  "what_it_does": "BeforeUpdate handler for the Company Type combobox on frmCompanyDetail. Enforces the rule that a company's type may not be changed while the company has live transactional data: customer orders, shipper orders, vendor purchase orders, or product-vendor links. When the rule fires, it shows a localized 'not allowed' modal dialog via frmGenericDialog, undoes the in-progress combobox edit, and cancels the BeforeUpdate event so Access does not commit the new CompanyTypeID.",
  "preconditions": [
    "frmCompanyDetail is open on a current company record and the user has attempted to change the cboCompanyTypeID combobox (bound to CompanyTypes.CompanyTypeID).",
    "The form-level cached counts (m_lngCustomerOrderCount, m_lngShipperOrderCount, m_lngVendorPurchaseOrderCount, m_lngVendorProductCount) and m_strMsgCaption are initialized; CompanyIsActive itself can populate them on first call per its Interpretation1.",
    "frmGenericDialog, GetString, StringFormat, IsFormOpen, sCompanyDelete_CompanyTypeChange, and clsErrorHandler exist at runtime in the wider Access project even though they are not in this two-module symbol DB."
  ],
  "side_effects": [
    "Calls CompanyIsActive, which re-runs DCount against Orders/PurchaseOrders/ProductVendors/Contacts and writes the results into the form-level m_lng* caches.",
    "Builds a localized message body via GetString(sCompanyDelete_CompanyTypeChange, ...) when the rule fires.",
    "Builds an OpenArgs string (Header/Message/Actions/FormCaption) via StringFormat(...).",
    "Opens frmGenericDialog via DoCmd.OpenForm in acFormReadOnly + acDialog mode — modal, blocks until dismissed.",
    "Defensively closes frmGenericDialog if IsFormOpen reports it still open afterwards (acSaveNo).",
    "Reverts the in-progress combobox edit via Me.cboCompanyTypeID.Undo.",
    "Sets the ByRef Cancel parameter to True, aborting the BeforeUpdate event so Access does not commit the new CompanyTypeID.",
    "On unhandled error, delegates to clsErrorHandler.HandleError(Me.Name, 'cboCompanyTypeID_BeforeUpdate', True); the third argument is presumably a rethrow/severity flag but its semantics are not in this packet."
  ],
  "user_visible_behavior": "If the company has any related orders/shipper-orders/POs/product-vendor links, attempting to change the Company Type pops a modal dialog titled 'Company Type Change Not Allowed' that explains which related records exist; after dismissal, the combobox snaps back to its previous value and the change is not saved. If no such records exist, the handler returns silently and the new selection commits normally.",
  "missing_from_symbol_db": [
    {
      "item": "Cross-module resolution of modStrings.GetString, modStrings.StringFormat, modStrings.enumStrings.sCompanyDelete_CompanyTypeChange",
      "severity": "blocking",
      "why": "These symbols exist in the same symbol DB but the resolver did not bind the call sites to them. The comprehension probe had to fall back to name-and-shape inference for symbols that should have been Interpretation1-backed. This is a Session 1-2 resolver-pipeline gap, not a probe limitation."
    },
    {
      "item": "frmGenericDialog (form), IsFormOpen (function), clsErrorHandler (class)",
      "severity": "useful",
      "why": "These live outside the two probe modules; full-DB scaling resolves them naturally. Flagged because the probe context highlights them."
    },
    {
      "item": "Tables transitively referenced via DCount inside CompanyIsActive (Orders, PurchaseOrders, ProductVendors, Contacts)",
      "severity": "useful",
      "why": "Tables join into the packet only through direct FormControls bindings. A 'tables referenced via DCount/DLookup/SQL anywhere in this procedure or its callees' pre-computation would help downstream consumers at scale; here the four tables show up only inside CompanyIsActive's prose."
    }
  ],
  "confidence": 0.92,
  "confidence_justification": "Handler intent is in the comment block, control flow is shallow and linear, callee Interpretation1 supplies the gating semantics, and the unresolved cross-module symbols are interpretable with high fidelity from name and call shape; the residual uncertainty is on the exact localized text and the third argument of HandleError."
}
```
