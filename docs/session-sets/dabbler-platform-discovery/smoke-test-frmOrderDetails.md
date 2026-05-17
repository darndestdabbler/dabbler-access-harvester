# Smoke Test: frmOrderDetails Through the Migration Map

> Date: 2026-05-17 | Form: frmOrderDetails (+ sfrmOrderLineItems) from samples/NorthwindDevEdition.accdb | Map under test: docs/cross-repo/access-to-platform-migration-map.md

## 1. Coverage check

| Access Form Piece | Migration Map Row(s) | Coverage Assessment |
| :--- | :--- | :--- |
| `frmOrderDetails` RecordSource | **Form:** `Form Record Source` | **Covered.** The map correctly identifies this translates to a C# view model. |
| Bound controls (e.g., `OrderID`) | **Form:** `Bound Control` | **Covered.** The mapping to a MudBlazor input with `@bind-Value` is correct. |
| ComboBox (`CustomerID`) with `Table/Query` RowSource | **Form:** `Form Controls` | **Partially Covered.** The map covers the control type but is silent on how `Table/Query` sources are populated. |
| ComboBox (`PaymentMethod`) with `Value List` RowSource | **Form:** `Form Controls` | **Partially Covered.** The map is silent on the distinction between `Value List` and `Table/Query` sources. |
| Subform control (`sfrmOrderLineItems`) | **Subform:** `Subform control` | **Covered.** Maps correctly to a Blazor component containing a `MudDataGrid`. |
| Subform linking (`Link Master/Child Fields`) | **Subform:** `Link Master Fields`, `Link Child Fields` | **Covered.** Maps correctly to parameter passing and data retrieval logic. |
| Calculated control (`txtTaxAmount`) | **Form:** `Open gaps` | **Not Covered in Table.** The primary mapping table lacks a row for this. It is only mentioned as a known gap. |
| Calculated control with cross-form reference (`txtTaxAmount` referencing `sfrmOrderLineItems`) | **Cross-cutting notes:** `State Management` | **Indirectly Covered.** Handled by a general architectural note, not a direct mapping rule in the `Form` section. |
| Calculated control with UDF (`txtModified` referencing `RecordModifiedString`) | **VBA Event Handler:** `Public Sub/Function...` and **Form:** `Open gaps` | **Partially Covered.** The map covers migrating the UDF itself to a C# service, but not its declarative use in a `ControlSource`. |
| CRUD command buttons (`cmdAddOrder`, `cmdDeleteOrder`) | **Form:** `Command Buttons` | **Covered.** The map suggests `DirtyAwareActionRow` for Delete; "Add" is a reasonable fit for a standard `MudButton`. |
| Workflow command buttons (`cmdCreateInvoice`, `cmdShipOrder`) | **Form:** `Command Buttons` | **Partially Covered.** The map's guidance focuses on CRUD, not domain-specific actions. |
| Form/Control events (`OnCurrent`, `OnClick`, `AfterUpdate`) | **VBA Event Handler** section | **Covered.** The mapping from VBA event procedures to C# methods is clear and correct. |
| Subform container events (`OnEnter`/`OnExit` on `sfrmOrderLineItems` control) | **VBA Event Handler** section | **Ambiguously Covered.** The general VBA guidance applies, but the `Subform` section lacks specific guidance for container events. |
| Control metadata (`StatusBarText`) | (none) | **Not Covered.** There is no mapping for this property. |

## 2. Gap inventory

1.  **Calculated Controls Are Not Mapped.**
    *   **Form element:** `txtTaxAmount.ControlSource` = `=IIf([TaxStatusID]=1,[TaxRate]*[sfrmOrderLineItems].[Form].[txtSubTotal],0)`
    *   **Map rows involved:** Form (no applicable row)
    *   **Proposed map text:** Add a row to the Form mapping table: "| Calculated Control | Read-only C# property on the view model. | `ControlSource` expressions are migrated to a property getter in the component's view model."

2.  **Cross-Component Data References Are Not Explicitly Mapped.**
    *   **Form element:** `txtTotal.ControlSource` = `=[sfrmOrderLineItems].[Form].[txtSubTotal]+[txtTaxAmount]+Nz([ShippingFee])`
    *   **Map rows involved:** Form (no applicable row); Cross-cutting notes: `State Management`
    *   **Proposed map text:** Amend the proposed "Calculated Control" note: "...Expressions referencing controls on subforms must be re-architected using component parameter callbacks or a shared state service."

3.  **User-Defined Functions (UDFs) in ControlSource Are Not Mapped.**
    *   **Form element:** `txtModified.ControlSource` = `=RecordModifiedString([AddedBy],[AddedOn],[ModifiedBy],[ModifiedOn])`
    *   **Map rows involved:** Form (no applicable row); VBA Event Handler
    *   **Proposed map text:** Add a row to the Form mapping table: "| Control with UDF Source | C# property calling a helper/service method. | VBA UDFs used in expressions are migrated to C# methods and called from view model property getters."

4.  **ComboBox RowSource Types Are Undifferentiated.**
    *   **Form element:** `PaymentMethod` (`RowSourceType: "Value List"`) vs. `CustomerID` (`RowSourceType: "Table/Query"`)
    *   **Map rows involved:** Form: `Form Controls`
    *   **Proposed map text:** The "Form Controls" row notes should state: "For combo boxes, `Value List` sources become a static list in C#, while `Table/Query` sources require a data service call."

5.  **Button Intent Is Not Differentiated.**
    *   **Form element:** `cmdDeleteOrder` (CRUD) vs. `cmdShipOrder` (domain workflow)
    *   **Map rows involved:** Form: `Command Buttons`
    *   **Proposed map text:** The "Command Buttons" row note should be amended: "For standard Save/Cancel/Delete, use `DirtyAwareActionRow`. For custom domain actions (e.g., 'Ship Order'), use individual `MudButton`s with specific `@onclick` handlers."

6.  **Control Metadata Is Dropped.**
    *   **Form element:** `TaxRate.StatusBarText` = `"Copied from SystemSettings.TaxRate"`
    *   **Map rows involved:** None
    *   **Proposed map text:** Add a row to the Form mapping table: "| `StatusBarText` Property | `MudTooltip` or dropped. | If the text provides user help, migrate to a tooltip; if it's developer metadata, it should be documented and omitted from the UI."

## 3. Confirmed-precise rows

-   **Subform Linking:** The map's description of translating `Link Master Fields` to a `[Parameter]` passed to the child component and `Link Child Fields` into the `WHERE` clause of the child's data fetch is precise and correct.
-   **VBA `OnCurrent` Event:** The specific mapping of `Form_Current` to Blazor's `OnParametersSetAsync` lifecycle method is an accurate and high-value translation.
-   **Declarative Security:** The cross-cutting note on replacing imperative VBA `control.Visible = False` logic with `DabblerSecured` and `RestrictedFormField` wrappers correctly identifies a key architectural shift and provides the right target.
-   **EventProcPrefix Noise:** The `EventProcPrefix` property on controls with no event attachments is correctly ignored by the map; it is developer-time metadata with no runtime behavior to migrate, and the map rightfully does not create a target for it.

## 4. Summary

-   **6 gaps surfaced;** 3 severity-Major (Calculated Controls, Cross-Component Refs, UDFs); 3 severity-Minor (ComboBox Sources, Button Intent, Metadata).
-   The map's biggest blindspot is **the migration of declarative `ControlSource` expressions**. This is a foundational feature of Access forms, and its absence from the primary mapping tables is a major omission.
-   The map's strongest section is its **guidance on Reports and Subreports**. The direct mapping from Access report structure to the declarative `report.rdf.json` schema is clear, comprehensive, and leaves little ambiguity.