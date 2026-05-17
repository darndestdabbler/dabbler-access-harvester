# Verification Round 1

For amendment-plausibility and consistency, the relevant platform surfaces implicated by this session are the ones already cited in the migration map snapshot: `src/Libs/Dabbler.Components/MasterDetail/DirtyAwareActionRow.razor`, `src/Libs/Dabbler.Security.Blazor/Components/Restriction/RestrictedFormField.razor`, `src/Libs/Dabbler.Security.Blazor/Components/DabblerSecured.razor`, `src/Libs/Dabbler.Security.Blazor/Components/Restriction/RestrictedGridColumn.razor`, `src/Libs/Dabbler.Reports.Blazor/DabblerReportPage.razor.cs`, `src/Libs/Dabbler.Components/MasterDetail/DeclarativeFilterPanel.razor`, and the deprecated compat shell path `src/Libs/Dabbler.Components.Compat/Shell/DabblerShell.razor.cs`. No fresh platform re-snapshot appears to have been performed in this session; this verification is of the document amendments against that already-pinned snapshot.

## 1. Gap coverage is complete

The six smoke-test gaps are all addressed by the amended map text.

- **Gap 1: Calculated controls not mapped**
  - Addressed by the new Form row:
    - `Calculated control whose ControlSource is an expression (=...)`
  - This directly closes the prior absence of any primary-table mapping for declarative `ControlSource` expressions.

- **Gap 2: Cross-component data references not explicitly mapped**
  - Addressed by the new Form row:
    - `Calculated control referencing a control on another (sub)form ([sfrm...].[Form].[field])`
  - This is the direct answer the smoke-test asked for, and it also aligns with the pre-existing cross-cutting `State Management` note.

- **Gap 3: UDFs in ControlSource not mapped**
  - Addressed by the new Form row:
    - `Calculated control invoking a UDF (=RecordModifiedString(...))`
  - This closes the earlier partial coverage where the VBA function itself had a destination, but its use inside a declarative control expression did not.

- **Gap 4: ComboBox RowSource types undifferentiated**
  - Addressed by two new Form rows:
    - `ComboBox with RowSourceType: "Table/Query"`
    - `ComboBox with RowSourceType: "Value List"`
  - Splitting these is the right fix, because the migration target differs materially between service-backed lookups and static lists/enums.

- **Gap 5: Button intent not differentiated**
  - Addressed by the amended `Command Buttons` row note.
  - The note now separates CRUD-style actions that fit `DirtyAwareActionRow` from domain workflow actions that should remain individual `MudButton`s with explicit handlers.

- **Gap 6: Control metadata dropped**
  - Addressed by the new Form row:
    - ``StatusBarText` property`
  - This gives the property a concrete migration disposition rather than leaving it implicit.

In addition, the session added a **Subform-container events** row for `OnEnter`/`OnExit`. That was not one of the six numbered gaps, but it was explicitly called out in the smoke-test coverage table as ambiguously covered, so adding it is a valid extra clarification rather than scope creep.

## 2. The amendments are accurate descriptions of the platform

The new targets and patterns are plausible and consistent with the platform posture already established in the document.

- **`MudSelect` for combo boxes** is accurate in context. The map already treats forms as compositions of MudBlazor primitives rather than proprietary input controls, so using `MudSelect` for Access combo boxes is consistent.
- **View-model-backed properties** are accurate. The map already uses “C# view model object, populated by a data service” for form record sources. Extending that to calculated read-only properties is a natural continuation, not a fabrication.
- **`EventCallback<T>` and shared/cascading state** are accurate Blazor patterns for parent/child coordination. The map is not restricted to platform-owned components; it already uses framework concepts such as lifecycle methods and `NavigationManager`. So these are valid migration targets.
- **`MudTooltip` for user-facing `StatusBarText`** is a reasonable analog. The row also correctly allows omission when the text is developer metadata rather than real UX help.
- **Domain-service handlers for workflow buttons** are accurate and more precise than the prior generic CRUD framing.
- **`@onfocusin` / `@onfocusout` for subform-container boundary events** is plausible as the nearest web/UI analogue for Access focus transitions at the embedded-subform boundary.

I do not see any fabricated component names, obviously mistyped platform paths, or invented platform features. The amendments stay within either:
1. components already named elsewhere in the map,
2. standard MudBlazor primitives, or
3. standard Blazor/.NET patterns.

## 3. Internal consistency is good

The amendments improve specificity without creating contradictions.

- The new **cross-subform calculated-control** row is consistent with the existing **Cross-cutting notes → State Management** section. In fact, it operationalizes that note by giving a direct table-level rule for one common Access pattern.
- The refined **Form open gaps** note is an improvement. Before the change, `ControlSource` expressions sat vaguely inside “open gaps.” After the change, the note is correctly narrowed to property-sheet-style tweaks such as masks, formats, and conditional formatting. That removes ambiguity rather than creating it.
- The split **ComboBox** rows do not contradict the generic `Form Controls` row; they simply refine one important subtype.
- The amended **Command Buttons** row remains consistent with the overview statement that forms are composed from MudBlazor inputs plus `DirtyAwareActionRow` and security wrappers.
- The new **Subform-container events** row is consistent with the overall Subform section. It explicitly distinguishes:
  - the embedded subform control boundary,
  - the inner form/grid behavior,
  - and the general master/detail synchronization pattern.
  That distinction is useful and not in tension with the existing `Subform control`, `Link Master Fields`, and `Link Child Fields` rows.

Net: the new text reads like a targeted elaboration of the existing map, not a second competing model.

## 4. The smoke-test artifact is appropriately scoped

The smoke-test does what Session 3 asked for.

The spec asked for:
- one concrete sample,
- walked through the migration-relevance map by hand,
- with noted gaps.

The artifact does exactly that:
- It uses **one real sample form** from `samples/NorthwindDevEdition.accdb`: `frmOrderDetails`, including its related subform `sfrmOrderLineItems`.
- It walks form pieces row-by-row through the migration map in a structured **coverage check** table.
- It identifies specific **gaps**, proposes map text, and distinguishes covered vs partially covered vs not covered.
- It also records **confirmed-precise rows**, which is useful because a smoke test should validate strengths as well as expose misses.

Including the subform is still within “one form” scope; it is part of the concrete sample under test, not a second independent specimen. So the artifact is neither too narrow nor too broad.

## 5. No spec-boundary violations are evident

I do not see evidence of the session crossing the stated boundaries.

- There is **no indication of edits to `../dabbler-platform`** itself.
- The deliverable is a **documentation amendment** inside this repo, plus the smoke-test writeup.
- The map continues to function as a **structured snapshot/reference**, not as copied source.
- The cited component file paths are references, not embedded source dumps.
- The build/test note is consistent with a docs-only change set: no C# code modified, no regressions introduced.

This stays within the allowed discovery-and-documentation boundary.

## 6. The currency header is intact and correctly unchanged

The migration map still pins:

- snapshot commit: `aa77020e1ca0`
- snapshot date: `2026-05-16`
- captured on: `2026-05-17`

That is correct for this session.

Session 3 was a **smoke test against the existing snapshot**, not a fresh inspection of the sibling platform repo. The amendments refine the migration guidance in response to a real Access-form walk-through; they do not claim that the underlying platform inventory changed. Therefore, leaving the pinned platform snapshot header unchanged is the right move.

Overall, this session looks complete and coherent: the smoke test surfaced concrete misses, the map was amended in the right places, the amendments are plausible against the documented platform surfaces, and the session stayed within spec.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
