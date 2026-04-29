## 1. What does the procedure do?

This is the `BeforeUpdate` event handler for the `cboCompanyTypeID` combo box on `frmCompanyDetail`, so it runs when the user tries to change the company's `CompanyTypeID`. It calls `CompanyIsActive`, which checks related transactional records for the current company and refreshes cached counts, then it also checks cached vendor-product count. If the company has any customer orders, shipper orders, vendor purchase orders, or vendor products, it shows a dialog saying the company type change is not allowed, undoes the combo-box edit, and cancels the update. If none of those related records exist, it does nothing and the edit is allowed to continue.

## 2. What are its preconditions?

- `frmCompanyDetail` is open on a current company record, and the user has triggered `cboCompanyTypeID`'s `BeforeUpdate` event by attempting to change that control.
- The current form record has a valid `CompanyID`, because `CompanyIsActive` uses `Me.CompanyID` to count related records.
- For the warning path to work, `frmGenericDialog`, `GetString`, `StringFormat`, and the assumed `IsFormOpen` helper must exist; their definitions are not in the packet.
- `m_strMsgCaption` is expected to have been initialized earlier if a nonblank dialog caption is desired; its initialization is not shown in this packet.

## 3. What side effects does it have?

- Calls `CompanyIsActive`, which queries related-record counts and caches them in form-level variables, including counts for contacts, customer orders, shipper orders, vendor purchase orders, and vendor products.
- If blocking conditions are met, builds a dialog message and open-args string for `frmGenericDialog`.
- Opens `frmGenericDialog` with `DoCmd.OpenForm` in read-only dialog mode; the header passed in open args is `"Company Type Change Not Allowed"`.
- If the assumed `IsFormOpen("frmGenericDialog")` check returns true after the dialog call, closes `frmGenericDialog` without saving.
- Calls `Me.cboCompanyTypeID.Undo`, reverting the pending edit in the `cboCompanyTypeID` control, which is bound to the `CompanyTypeID` field.
- Sets `Cancel = True`, preventing the attempted update from being committed.
- On error, calls `clsErrorHandler.HandleError Me.Name, "cboCompanyTypeID_BeforeUpdate", True`; the handler's downstream effects are no evidence in packet.

## 4. What does the user see?

If the user tries to change the company type for a company that already has related orders, purchase orders, or vendor-product rows, they see a dialog titled via open args as `"Company Type Change Not Allowed"`, and the attempted selection is rejected. After the dialog is dismissed or otherwise closed, the company-type combo box reverts to its prior value. If the company has no such related records, there is no special message from this procedure and the new selection is allowed.

## 5. Where is the symbol DB missing information?

### No blocking gaps for questions 1-4

The core control-flow and business rule are explicit in the handler plus the provided `CompanyIsActive` summary.

### Useful missing information

- **`GetString`** — useful. The packet does not show the implementation, so the exact dialog body text and formatting are not visible.
- **`sCompanyDelete_CompanyTypeChange`** — useful. This constant/template appears to drive the message content, but its actual text is no evidence in packet.
- **`frmGenericDialog`** — useful. The packet does not show that form's UI, buttons, or how it interprets `OpenArgs`, so exact user-visible presentation is not fully known.
- **`IsFormOpen`** — useful. Interpreting it as “check whether a form is open” depends on the name and call site; its actual implementation is no evidence in packet.
- **Relationships/tables behind `CompanyIsActive` (`Orders`, `PurchaseOrders`, `ProductVendors`, `Contacts`)** — useful. The callee summary tells us enough for the rule, but the actual schema/relationships would help confirm the precise business meaning of each count.
- **`clsErrorHandler`** — useful. Error-path behavior is referenced but not defined, so any user-visible or logging effects during exceptions are unknown.

## 6. Confidence

**0.91** — High confidence because the handler's gatekeeping logic, undo/cancel behavior, and dialog invocation are explicit in the source, with uncertainty limited to unresolved helper functions and the exact dialog text/UI.

```json
{
  "what_it_does": "This is the BeforeUpdate handler for the company-type combo box on frmCompanyDetail, so it runs when the user tries to change CompanyTypeID. It calls CompanyIsActive to refresh related-record counts and determine whether the current company has customer orders, shipper orders, or vendor purchase orders, and it also checks vendor-product count. If any of those related records exist, it shows a dialog saying the change is not allowed, undoes the combo-box edit, and cancels the update. If no such related records exist, it leaves the edit alone and the change can proceed.",
  "preconditions": [
    "frmCompanyDetail is open on a current company record and cboCompanyTypeID's BeforeUpdate event has fired because the user attempted to change that control.",
    "The current record has a valid CompanyID, because CompanyIsActive uses Me.CompanyID to count related records.",
    "For the warning path to work, frmGenericDialog, GetString, StringFormat, and the assumed IsFormOpen helper must exist; their definitions are not in the packet.",
    "m_strMsgCaption is expected to have been initialized earlier if a dialog caption is desired, but that initialization is not shown here."
  ],
  "side_effects": [
    "Calls CompanyIsActive, which queries and caches counts of related contacts, customer orders, shipper orders, vendor purchase orders, and vendor products in form-level variables.",
    "If blocking conditions are met, constructs a dialog message and open-args string.",
    "Opens frmGenericDialog in read-only dialog mode with open args including the header 'Company Type Change Not Allowed' and a caption value from m_strMsgCaption.",
    "If the assumed IsFormOpen('frmGenericDialog') check returns true after the dialog call, closes frmGenericDialog without saving.",
    "Calls Me.cboCompanyTypeID.Undo, reverting the pending change in the combo box bound to CompanyTypeID.",
    "Sets Cancel = True, preventing the attempted update from being committed.",
    "On error, calls clsErrorHandler.HandleError(Me.Name, 'cboCompanyTypeID_BeforeUpdate', True); exact error-path effects are no evidence in packet."
  ],
  "user_visible_behavior": "If the user tries to change the company type for a company that already has related orders, purchase orders, or vendor-product rows, they get a dialog indicating the change is not allowed and the combo box reverts to its previous value. If the company has no such related records, this procedure shows no special message and the new selection is allowed.",
  "missing_from_symbol_db": [
    {
      "item": "GetString",
      "severity": "useful",
      "why": "Its implementation is missing, so the exact dialog body text and formatting are not visible."
    },
    {
      "item": "sCompanyDelete_CompanyTypeChange",
      "severity": "useful",
      "why": "This message template or constant is referenced but not defined, so the exact wording shown to the user is no evidence in packet."
    },
    {
      "item": "frmGenericDialog",
      "severity": "useful",
      "why": "The form definition is absent, so the exact UI layout, buttons, and OpenArgs handling are unknown."
    },
    {
      "item": "IsFormOpen",
      "severity": "useful",
      "why": "Its implementation is not present; interpreting it as a form-open check requires assuming it matches its name."
    },
    {
      "item": "Relationships and schemas for Orders, PurchaseOrders, ProductVendors, and Contacts",
      "severity": "useful",
      "why": "The callee summary gives the rule, but the actual table relationships would better confirm the precise business meaning of each count."
    },
    {
      "item": "clsErrorHandler",
      "severity": "useful",
      "why": "The error handler is referenced but not defined, so any user-visible or logging behavior on exceptions is unknown."
    }
  ],
  "confidence": 0.91,
  "confidence_justification": "The handler's main control flow and business rule are explicit in the source and supported by the provided CompanyIsActive summary, with uncertainty only around unresolved helper functions and exact dialog presentation."
}
```