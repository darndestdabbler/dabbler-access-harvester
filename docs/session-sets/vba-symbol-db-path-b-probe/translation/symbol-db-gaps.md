# Session 2 Symbol-DB Gaps

The translation probe was workable, but the packet exposed a few places
where the symbol DB still leaves real migration decisions to the human or
to later sets.

## Gaps That Mattered

- `IsFormOpen("frmGenericDialog")` is still unresolved in the packet, so
  the translation had to model it as `IDialogService.IsFormOpen(...)`
  rather than bind it to a concrete helper method.
- Access UI constants (`acNormal`, `acFormReadOnly`, `acDialog`,
  `acForm`, `acSaveNo`) are unresolved. For the probe this was fine,
  because the behavior compresses into "open modal dialog" and "close
  dialog without save", but a larger Path-B program would need a stable
  abstraction map for these primitives.
- `GetString(...)` identifies the string-key helper, but the packet does
  not carry the underlying `Strings` table row for
  `sCompanyDelete_CompanyTypeChange`. That is enough for control-flow
  translation, not enough for fully faithful UX/text migration.
- `Me`-rooted member accesses remain unresolved at the reference-row
  level even though `FormControls` and bound columns give enough context
  for this probe. A broader translator would benefit from a cleaner
  "this member resolves to this form/control/property" projection.

## Gaps That Did Not Block This Probe

- `CompanyIsActive` was resolvable and had enough source to translate its
  count-query logic.
- The cached form fields (`m_lng*`, `m_strMsgCaption`) were present as
  symbol rows, which was enough to model them as private instance state.
- The host form's `FormControls`, `EventBindings`, and bound-table rows
  gave sufficient structure to understand why `cboCompanyTypeID` is the
  relevant control and why `CompanyTypes` is the affected lookup table.
