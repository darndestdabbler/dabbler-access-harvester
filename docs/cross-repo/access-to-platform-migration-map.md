# Access-to-Platform Migration Map

> **Snapshot of:** `../dabbler-platform` @ commit `aa77020e1ca0` on `2026-05-16`
> **Captured on:** `2026-05-17`
> **Regenerate when:** the platform's component library changes
> meaningfully, or when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.

## Overview

This document maps Microsoft Access constructs to their natural migration targets within the `dabbler-platform`. It uses the component inventory and RDF schema reference to provide concrete file paths and API surfaces. The goal is to guide a migration engineer or AI agent in translating Access artifacts into a modern Blazor application using the platform's components and patterns.

| Access Construct | Primary Platform Target(s) |
|---|---|
| Form / Subform | Composition of MudBlazor inputs, `DirtyAwareActionRow`, and security wrappers. |
| Report / Subreport | `DabblerReportPage` component consuming a `report.rdf.json` file. |
| Query (SELECT) | `DeclarativeFilterPanel` for UI; data-source for components/reports. |
| Query (Action) | C# service methods invoked by component event handlers. |
| Macro | C# event handlers, Blazor lifecycle methods, or `NavigationManager` calls. |
| VBA Event Handler | C# methods handling Blazor component events (e.g., `@onclick`). |

### Form (bound and unbound)

-   **Access semantics (one paragraph):** Access forms provide the primary user interface for data entry and display. Bound forms are directly linked to a table or query, managing data state automatically. Unbound forms act as dialogs or main menus. The migration intent is to replicate the UI layout, data binding, and user interactions (save, delete, navigate) in a web context.
-   **Primary platform target:** A composition of standard MudBlazor components (e.g., `MudForm`, `MudTextField`, `MudSelect`) and platform-provided primitives for actions and security.
    -   `DirtyAwareActionRow` (`src/Libs/Dabbler.Components/MasterDetail/DirtyAwareActionRow.razor`) for Save/Cancel/Delete buttons.
    -   `RestrictedFormField` (`src/Libs/Dabbler.Security.Blazor/Components/Restriction/RestrictedFormField.razor`) to wrap individual input controls for declarative security.
    -   `DabblerSecured` (`src/Libs/Dabbler.Security.Blazor/Components/DabblerSecured.razor`) for securing entire sections of a form.
-   **Mapping notes:** An Access form translates to a `.razor` component.

    | Access Piece | Platform Target | Notes |
    | :--- | :--- | :--- |
    | Form Record Source | C# view model object, populated by a data service. | The platform does not bind directly to SQL. |
    | Form Controls | MudBlazor input components (`MudTextField`, etc.). | The platform does not ship its own input library. |
    | Form Header/Footer | `MudToolBar` or similar layout component. | The `DirtyAwareActionRow` is typically placed in the footer area. |
    | Bound Control | A MudBlazor input with `@bind-Value` to a view model property. | Wrapped with `RestrictedFormField` for column-level security. |
    | Form `OnCurrent` event | Blazor's `OnParametersSetAsync` lifecycle method. | Used to load data for the current record. |
    | Command Buttons | `MudButton` with `@onclick` handlers. | For Save/Cancel/Delete, use `DirtyAwareActionRow`. For domain-workflow actions (e.g., `cmdCreateInvoice`, `cmdShipOrder`, `cmdReceivePayment`, `cmdCloseOrder` in `frmOrderDetails`), use individual `MudButton`s whose handlers invoke a domain service method, not a CRUD primitive. |
    | ComboBox with `RowSourceType: "Table/Query"` | `MudSelect` populated from a C# data-service call (one call per dependency, cached as appropriate). | `RowSource` is a saved query (e.g., `qrycboCustomers`); the service method encapsulates the SQL. |
    | ComboBox with `RowSourceType: "Value List"` | `MudSelect` populated from a `static readonly` list (or enum) in C#. | The list is fixed in code (e.g., `PaymentMethod` = `"Credit Card;Check;Cash"`); no service call. |
    | Calculated control whose `ControlSource` is an expression (`=...`) | A read-only property on the view model whose getter evaluates the expression in C#. | Access expressions in `ControlSource` (e.g., `txtTaxAmount.ControlSource = "=IIf([TaxStatusID]=1,[TaxRate]*[sfrmOrderLineItems].[Form].[txtSubTotal],0)"`) translate to a getter on the view model. Re-evaluation is driven by Blazor's standard rerender cycle when bound inputs change. |
    | Calculated control referencing a control on another (sub)form (`[sfrm...].[Form].[field]`) | A read-only property on the parent view model whose getter reads parent-shared state populated by the subform component. | Subforms must publish their derived totals (e.g., `txtSubTotal`) to the parent via an `EventCallback<T>` parameter or a shared cascading state service. The parent's `MudDataGrid` host component handles the wiring. |
    | Calculated control invoking a UDF (`=RecordModifiedString(...)`) | A read-only property on the view model that calls a C# helper or injected service method. | VBA UDFs in `ControlSource` (e.g., `txtModified.ControlSource = "=RecordModifiedString([AddedBy],[AddedOn],[ModifiedBy],[ModifiedOn])"`) are migrated by porting the UDF to a C# method (per the VBA Event Handler section) and calling it from the view model. |
    | `StatusBarText` property | `MudTooltip` (user-facing help) or dropped to a code comment (developer metadata). | `StatusBarText` is sometimes a user-help hint and sometimes an audit annotation (e.g., `TaxRate.StatusBarText = "Copied from SystemSettings.TaxRate"`). Migrate the former as a tooltip on the input; record the latter alongside the view-model property as a comment so the provenance is preserved without surfacing it in the UI. |

-   **Stability / posture:** The target components (`DirtyAwareActionRow`, `RestrictedFormField`, `DabblerSecured`) are all `stable`. The pattern relies on the consumer composing standard, stable MudBlazor primitives.
-   **Open gaps:** Access's property sheet provides a low-code way to configure complex control behavior (e.g., input masks, format strings, conditional formatting rules). MudBlazor requires C# or different component properties to achieve this. There is no direct "property sheet" equivalent. (Control-source expressions, including UDF and cross-form references, are covered by the calculated-control rows in the table above; this open gap concerns the per-property tweaks that don't reach into expression-evaluation territory.)

### Subform

-   **Access semantics (one paragraph):** A subform displays data from a related table or query, typically in a datasheet (grid) or continuous form view, linked to the parent form's current record. The migration intent is to preserve this master-detail relationship, allowing users to view and edit related records within the context of a parent record.
-   **Primary platform target:** For datasheet/continuous subforms, the target is a `MudDataGrid` component. For single-record subforms, the target is the same composition pattern as a main Form, nested within the parent form's render tree.
    -   `MudDataGrid` (a MudBlazor primitive, not a platform component).
    -   `RestrictedGridColumn` (`src/Libs/Dabbler.Security.Blazor/Components/Restriction/RestrictedGridColumn.razor`) to wrap `PropertyColumn` definitions inside a `MudDataGrid` for declarative column security.
-   **Mapping notes:**

    | Access Piece | Platform Target | Notes |
    | :--- | :--- | :--- |
    | Subform control | A Blazor component containing a `MudDataGrid`. | The component receives the parent record's ID as a `[Parameter]`. |
    | `Link Master Fields` | The parent component passes its primary key to the subform component. | The subform component uses this key to fetch its data. |
    | `Link Child Fields` | The `WHERE` clause in the subform's data retrieval logic. | |
    | Datasheet view | `MudDataGrid`. | |
    | Continuous Form view | `MudDataGrid` or a custom `foreach` loop rendering item components. | `MudDataGrid` is the idiomatic choice for tabular data. |
    | Subform-container events (`OnEnter`, `OnExit` attached to the subform control itself, not to its internal form) | Blazor focus events on the wrapping component (`@onfocusin` / `@onfocusout`) or explicit imperative calls in the parent component's event flow. | These events fire when focus crosses the subform boundary in Access; they are distinct from the inner form's own events. The Blazor analog is rare — most Access uses can be re-expressed as `@bind` callbacks instead. |

-   **Stability / posture:** `RestrictedGridColumn` is `stable`. The pattern relies on `MudDataGrid`, which is a stable MudBlazor primitive. The deprecated `DabblerShell` (`src/Libs/Dabbler.Components.Compat/Shell/DabblerShell.razor.cs`) provided a higher-level master-detail navigation pattern, but the inventory explicitly states new apps should compose MudBlazor primitives directly.
-   **Open gaps:** Access's built-in subform control automatically handles master/child data synchronization. In Blazor, this logic must be implemented manually by passing the master key and reloading the subform's data when the master record changes.

### Report

-   **Access semantics (one paragraph):** Access reports present data in a formatted, print-optimized layout. They support grouping, sorting, headers, footers, and calculated fields. The migration intent is to translate the report's data source, parameters, layout, and formatting into a web-renderable and exportable format.
-   **Primary platform target:** The `DabblerReportPage` component, which consumes a declarative `report.rdf.json` definition file. The Access report is not migrated to a component, but rather to this JSON metadata file.
    -   `DabblerReportPage` (`src/Libs/Dabbler.Reports.Blazor/DabblerReportPage.razor.cs`).
    -   RDF Schema (`docs/cross-repo/dabbler-platform-report-rdf-schema.md`).
-   **Mapping notes:** The Access report's structure maps directly to sections of the RDF schema.

    | Access Piece | RDF Schema Section(s) | Notes |
    | :--- | :--- | :--- |
    | Record Source | `dataSources` | The SQL query goes into the `query` property. |
    | Report Parameters | `parameters` | Access parameter prompts map to `ParameterDefinition` objects. |
    | Grouping & Sorting | `grouping`, `sorting` | Maps directly to Access's Group, Sort, and Total pane. |
    | Report/Page/Group Sections | `bands` | Each Access section (e.g., Page Header) becomes a `BandDefinition` of the corresponding `type`. |
    | Controls (TextBox, Label) | `elements` within a band. | Text Boxes map to `TextField`, Labels to `StaticText`. |
    | Control Source Expressions | `expressions`, `elements` | Simple bindings use `fieldName`; complex ones use the `expression` property. |
    | Formatting Properties | `styles` | Font, color, alignment, borders are defined in named styles and referenced by elements. |

-   **Stability / posture:** `DabblerReportPage` is `stable`, as is the underlying reporting engine and RDF schema.
-   **Open gaps:** The RDF schema does not support all of Access's report features. Notably, charts and custom graphics controls have no direct counterpart in the RDF `elements` schema. The rendering of OLE objects is also not supported.

### Subreport

-   **Access semantics (one paragraph):** A subreport is a report embedded within another report. It typically displays related data, similar to a subform, but in a read-only, print-oriented context. The migration intent is to maintain this compositional structure, where a master report can include and pass parameters to child reports.
-   **Primary platform target:** A `Subreport` element type within a parent report's `report.rdf.json` file. The parent `DabblerReportPage` handles loading and rendering the nested report.
    -   Element type `Subreport` in the RDF `elements` schema.
    -   Parameters `SubreportRdfLoader` and `SubreportProviderFactory` on the `DabblerReportPage` component.
-   **Mapping notes:**

    | Access Piece | RDF Schema Mapping |
    | :--- | :--- |
    | Subreport control | An `ElementDefinition` with `type: "Subreport"`. |
    | `Source Object` property | `subreportPath` property, pointing to the child `.rdf.json` file. |
    | `Link Master Fields` / `Link Child Fields` | `subreportParameters` object, mapping child parameter names to parent expressions. |

-   **Stability / posture:** The `Subreport` element is a `stable` feature of the reporting engine. The `DabblerReportPage` component that orchestrates subreport loading is also `stable`.
-   **Open gaps:** None. The platform's subreport mechanism is a direct analog to the Access feature.

### Query (saved SELECT/parameter queries; action queries)

-   **Access semantics (one paragraph):** Saved queries are central to Access. `SELECT` queries act as record sources for forms and reports, often with parameters. Action queries (`UPDATE`, `INSERT`, `DELETE`, `MAKE TABLE`) perform bulk data operations. The migration intent is to separate the data-retrieval and data-modification logic from the UI layer.
-   **Primary platform target:** There is no single component target. Queries are translated into different platform patterns depending on their use.
    -   **SELECT Queries (as UI filters):** `DeclarativeFilterPanel<TEntity>` (`src/Libs/Dabbler.Components/MasterDetail/DeclarativeFilterPanel.razor`).
    -   **SELECT Queries (as data sources):** C# methods in a data service layer that return `IEnumerable<T>`. The SQL is encapsulated in these methods. For reports, the SQL is placed in the `query` property of a `dataSources` entry in the `.rdf.json`.
    -   **Action Queries:** C# methods in a service layer that perform the database operation. These are invoked from component event handlers, such as the `OnSave` or `OnDelete` callbacks of `DirtyAwareActionRow`.
-   **Mapping notes:** The concept of a standalone "saved query" artifact disappears. Its logic is absorbed into either UI component configuration (for filters), C# data services, or RDF `dataSources`.
-   **Stability / posture:** `DeclarativeFilterPanel` is `stable`. The pattern of using C# services is a standard architectural practice and not specific to a platform component.
-   **Open gaps:** Access allows end-users (with appropriate permissions) to create and save their own queries. The platform provides no direct counterpart for this ad-hoc, user-driven query building and persistence. The deprecated `DabblerCatalogPage` provided a list of pre-defined queries, but not a user-facing query designer.

### Macro (AutoExec, validation/data macros, navigation macros, embedded macros on controls)

-   **Access semantics (one paragraph):** Macros provide a declarative, low-code way to automate tasks, respond to events, and control application flow without writing VBA. They handle everything from opening forms (`OpenForm`) to running queries (`RunSQL`) and validating data. The migration intent is to translate these declarative actions into equivalent C# logic within the Blazor application.
-   **Primary platform target:** There is no direct "macro runner" component. Macro actions are translated into C# code within Blazor event handlers or lifecycle methods.

    | Access Macro Type | Platform Target | Notes |
    | :--- | :--- | :--- |
    | `AutoExec` | Application startup logic in `Program.cs` or `OnInitializedAsync` in `MainLayout.razor`. | |
    | Data Macros (Before Change, After Update) | Logic in C# service methods called before or after saving data. | Can also be implemented as database triggers. |
    | UI Macros (embedded on controls) | C# methods handling Blazor events like `@onclick`. | The logic from the macro action (e.g., `MessageBox`) is rewritten in C#. |
    | Navigation Macros (`OpenForm`, `OpenReport`) | `@onclick` handlers calling `NavigationManager.NavigateTo("/my-page")`. | In the deprecated compat shell, this involved calls to `DabblerTabbedWorkspace`. |

-   **Stability / posture:** This is a pattern-based mapping, not a component mapping. The underlying Blazor and .NET primitives (`NavigationManager`, event handlers) are stable.
-   **Open gaps:** The platform lacks a visual, low-code automation builder analogous to the Access macro editor. All macro logic must be translated into imperative C# code, which represents a significant paradigm shift. There is no platform equivalent for the `DoCmd` object's wide range of actions.

### VBA event handler (form/control events; module-level public Subs/Functions called from events)

-   **Access semantics (one paragraph):** VBA (Visual Basic for Applications) provides a full-featured programming language for handling events, implementing complex business logic, and manipulating the Access application object model. Event handlers are attached to forms, reports, and controls. The migration intent is to translate this imperative business logic into C#.
-   **Primary platform target:** C# methods within a `.razor.cs` code-behind file or in an injected service. There is no specific platform component for this; it is a direct translation of programming logic from one language/framework (VBA/Access) to another (C#/Blazor).
-   **Mapping notes:**

    | Access VBA Construct | Platform Target | Notes |
    | :--- | :--- | :--- |
    | Form/Control Event Handler (e.g., `Button_Click`) | A Blazor component event handler (e.g., `<MudButton @onclick="MyClickHandler">`). | The `MyClickHandler` method is written in C#. |
    | `Form_Current` event | `OnParametersSetAsync` lifecycle method. | Executes when the component's parameters change, such as when navigating to a new record. |
    | Public Sub/Function in a standard module | Public method in an injectable C# service class. | The service is injected into components that need to call the method. |
    | `DoCmd` object methods (`OpenForm`, etc.) | Calls to `NavigationManager`, `IDialogService`, or other Blazor/MudBlazor services. | |
    | DAO/ADO data access code | C# data access logic using EF Core or Dapper in a service layer. | |

-   **Stability / posture:** This is a pattern-based mapping. The underlying Blazor framework is stable.
-   **Open gaps:** The Access object model is comprehensive and synchronous. Blazor's component model is asynchronous and lacks a central "application" object like `DoCmd`. Common VBA tasks like `DoCmd.Requery` or manipulating a control's properties on another open form have different, often more complex, analogs in Blazor (e.g., state management, component references, event callbacks).

### Platform-extension candidates

-   **Access feature:** Control property sheet (e.g., for input masks).
    -   **Proposed extension:** A new platform component or helper for common input behaviors.
    -   **Justification:** This would simplify migrating forms with heavily configured controls by providing declarative properties instead of requiring custom C# code for each.
-   **Access feature:** Built-in master/child synchronization for subforms.
    -   **Proposed extension:** A higher-order component that wraps a parent form and a child grid, managing the data refresh lifecycle.
    -   **Justification:** This would reduce boilerplate C# code required to manually implement master-detail data loading and refresh logic.
-   **Access feature:** Report controls for charts and graphics.
    -   **Proposed extension:** Add `Chart` and `Shape` element types to the RDF `elements` schema and implement their rendering in the report engine.
    -   **Justification:** This would unblock the migration of visually rich reports that rely on charting to convey information.
-   **Access feature:** User-driven query builder.
    -   **Proposed extension:** A new platform page/component that allows users to select columns, define filters, and save/run custom queries.
    -   **Justification:** This would support power-user workflows common in Access where users create their own data extracts without developer intervention.
-   **Access feature:** Macro editor / `DoCmd` object.
    -   **Proposed extension:** A service that abstracts common UI actions (e.g., `IUiService.ShowMessage`, `IUiService.NavigateToEntity`).
    -   **Justification:** This would provide a clearer, more declarative migration path for navigation and simple UI interaction macros, reducing direct dependency on framework-level services like `NavigationManager`.

### Cross-cutting notes

-   **Declarative Security:** The platform's security-wrapper pattern (`DabblerSecured`, `RestrictedFormField`, `RestrictedGridColumn`) is a cross-cutting replacement for ad-hoc VBA code that would hide or disable UI elements based on user roles. This shifts security logic from imperative code to declarative markup.
-   **Data Access:** In Access, UI objects are often tightly coupled to data via SQL in `RecordSource` properties. The platform pattern is to decouple UI from data via a C# service layer, promoting better architecture and testability.
-   **State Management:** Access manages the state of open forms implicitly. In the Blazor Server model, state is managed within component instances. Logic that assumes access to another open form's state or controls must be re-architected using Blazor's state management patterns (e.g., cascading parameters, shared services).
-   **Navigation:** Access navigation is often imperative (`DoCmd.OpenForm`). Platform navigation is based on URL routing. All navigation logic must be translated to use the `NavigationManager` service to change routes.
-   **Compatibility Shell:** The `Dabbler.Components.Compat` package provides a high-fidelity, tabbed MDI-style shell (`DabblerShell`) that mimics the Access experience. The inventory marks this entire surface as `deprecated`, and new applications should compose shells directly from MudBlazor primitives. This is a critical consideration for migration strategy.
