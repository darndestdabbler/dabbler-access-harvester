# Access Domain Notes

## Purpose

Capture operator-provided Microsoft Access behavior patterns that future AI
engines should use when interpreting harvester output and planning migration
work.

These notes are durable interpretation guidance, not proof that a specific
object in a specific database is active, stale, or migration-worthy. Use them
to avoid naive inferences. For any named object, still ground claims in
harvested metadata or explicit operator confirmation.

## How To Use This Document

- Treat these notes as common Access patterns that can explain otherwise weak or
  ambiguous harvester signals.
- Do not turn a generic pattern into a database-specific claim unless the
  harvested metadata or the operator confirms it for the active corpus.
- When the operator provides additional durable Access-domain facts, append them
  here with enough context that a later engine can apply them safely.

## Operator-Provided Patterns

### 1. Switchboards as application entry points

- In Microsoft Access, switchboards are often used as the application's entry
  point, home screen, and main menu.
- They often open forms, reports, or queries, and sometimes execute macros or
  VBA code.
- They often migrate conceptually to an enterprise application's menu or
  navigation system rather than to a CRUD screen.
- They may rely on one or more macros and sometimes a table that stores
  navigation data.

Interpretation impact:

- A switchboard-like form or its helper macros/table should not be treated as
  stale merely because the harvested graph lacks an inbound opener.
- Objects like `frmSwitchboard`, `Switchboard`, `tblSwitchboardItems`, or
  associated macros are plausible application entry surfaces and require review,
  not deletion assumptions.

### 2. Menu forms beyond classic switchboards

- A form other than a classic switchboard may still serve as a menu of objects
  to open.
- This is especially common for reports and sometimes for standalone queries.

Interpretation impact:

- A form with little or no record-source evidence may still be an active menu,
  launcher, or utility surface.
- Reports and queries with weak inbound navigation evidence may still be used if
  a menu form or launcher surface was not recognized by the harvested graph.

### 3. Parameter collection and popup/modal support forms

- RecordSource or RowSource logic for a form, report, or query often uses one
  or more parameters.
- For reports and forms, parameters are sometimes collected by another form,
  often popup or modal, and that helper form may be launched from `Form_Open`,
  `Report_Open`, or a similar event.
- For queries, square-bracket tokens such as `[ClientId]` or `[Enter a ClientId]`
  often denote parameters when they do not resolve to a column in the current
  query scope.

Interpretation impact:

- A popup/modal form may be a live parameter dialog even if it has weak CRUD
  evidence or sparse record-source metadata.
- Parameterized queries should not be treated as inactive just because the
  parameter source is not obvious in the harvested graph.
- Query, form, or report flows may depend on parameter dialogs or prompt-style
  input rather than direct static navigation.

### 4. Direct user navigation can bypass openers

- Users are often able to navigate directly to forms, reports, and queries in
  Access.
- A form, report, or query may therefore be usable even when no macro or VBA
  procedure explicitly opens it.

Interpretation impact:

- Missing `NavigationEdge` or `VbaReference` evidence is not enough to call a
  form, report, or query stale.
- Cleanup/use reasoning based only on absent inbound references must remain
  low-confidence and review-oriented.

### 5. Dropdown and cascading filter controls

- Dropdown controls are often used to filter records on a form.
- Cascading dropdown controls are also common.

Interpretation impact:

- `RowSource` and control-event evidence may reflect filtering behavior rather
  than entity creation/editing.
- When planning migration targets, consider dependent filters and cascading
  selectors as likely UI behavior to preserve rather than incidental control
  wiring.

### 6. Ribbon-driven navigation

- The Office Ribbon is sometimes used to open objects.

Interpretation impact:

- Objects may be live even when no form, macro, or VBA opener is harvested.
- Cleanup/use interpretation should allow for ribbon-driven access paths that
  are outside the currently detected graph.