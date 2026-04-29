# Session 2 Translation Rationale

## Translation Shape

The probe translates `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`
into a POCO-style class, [`FrmCompanyDetailTranslationProbe`](../../../../tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbe.cs),
with one public method:

- `CboCompanyTypeIdBeforeUpdate(IFormContext form) -> bool`

The returned `bool` is the C# stand-in for the VBA `Cancel` ByRef
parameter. `true` means "cancel the pending edit"; `false` means "allow
the edit to continue."

## What Stayed Close To VBA

- The translation preserves the same gate condition: block the update
  when `CompanyIsActive()` is true or when `m_lngVendorProductCount > 0`.
- The helper `CompanyIsActive()` was translated too, because the target
  procedure's behavior depends on the cached counts it populates.
- The dialog `openArgs` string keeps the same four-field contract:
  `Header`, `Message`, `Actions`, `FormCaption`.
- `Undo()` on the combobox and the cancel signal both happen only on the
  blocked path.
- Error handling follows the VBA posture: catch, delegate to
  `HandleError`, and return without rethrowing.

## Design Calls

- `DoCmd.OpenForm` / `DoCmd.Close` / `IsFormOpen` collapsed into
  `IDialogService`. The probe is evaluating translatability and
  testability, not preserving Access UI primitives byte-for-byte.
- `GetString` stays a service boundary (`IStringRepository`) instead of
  translating the `Strings` table lookup. The packet identifies the
  helper and its signature, but the actual `Strings` table contents are
  not part of the probe target.
- `StringFormat` and `StringFormatSQL` became small local helpers because
  they are deterministic string mechanics and easy to unit-test in C#.
- `clsErrorHandler` became `IErrorHandler` for the same reason: behavior
  preserved, concrete Access/VBA plumbing deferred.

## Test-Coverage Rationale

Session 2's tests focus on observable behavior that would falsify a weak
translation:

- Active-company path cancels, opens the dialog, closes it if present,
  and undoes the combobox change.
- Fully inactive path leaves the edit alone.
- Vendor-product-only path still blocks the change, covering the
  right-hand side of the VBA `Or`.
- Error path delegates to `HandleError(Me.Name, "cboCompanyTypeID_BeforeUpdate", True)`.
- DAO criteria formatting uses the current `CompanyID`, proving the
  translated `CompanyIsActive()` helper is consulting the form record.
