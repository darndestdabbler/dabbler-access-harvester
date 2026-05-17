# dabbler-platform Components Inventory

> **Snapshot of:** `../dabbler-platform` @ commit `aa77020e1ca0` on `2026-05-16`
> **Captured on:** `2026-05-17`
> **Regenerate when:** the platform's component library changes meaningfully, or when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.

## Overview

The `dabbler-platform` component surface provides MudBlazor-native UI primitives for Blazor Server applications. Two posture rules govern adoption:

1. The platform does NOT ship a comprehensive general-purpose form-control library. Consumers build forms by composing standard MudBlazor inputs (`MudTextField`, `MudSelect`, `MudDatePicker`, etc.) directly.
2. The platform DOES ship higher-order primitives for: dirty-aware action rows, declarative QBE filtering, reporting (definition → render), the authorization-aware shell, and the security-administration surface.

This inventory documents every public component as a potential migration target from Microsoft Access. Each entry cites a source file path under `../dabbler-platform/` and lists `[Parameter]` / `[Parameter, EditorRequired]` surfaces verified against the `.razor` or `.razor.cs` source.

**Stability convention used below:**

- `stable` — public contract is documented in a project README and exercised by tests; consumers can rely on the parameter surface and contract.
- `in-flight` — implementation exists but is gated by an in-progress workstream, deprecation notice, or test scaffold (i.e., consumers should expect breaking changes).
- `deprecated` — explicitly marked `[Obsolete]` or part of a package the platform README marks `DEPRECATED — slated for removal`.

## Categories

### Master/Detail and core CRUD primitives

Components from `Dabbler.Components` for building bound query and detail pages.

#### `DirtyAwareActionRow`

- **Source:** `src/Libs/Dabbler.Components/MasterDetail/DirtyAwareActionRow.razor` (markup-only).
- **Purpose:** Renders a Save / Cancel / Delete action button row whose enabled state is driven by an external `IsDirty` flag.
- **Required parameters:**
  - `IsDirty` : `bool` — controls the enabled state of the Save button and toggles the Cancel/Close button label.
  - `OnSave` : `EventCallback` — invoked when the user clicks Save.
- **Optional parameters:**
  - `OnCancel` : `EventCallback` — invoked when the user clicks Cancel/Close.
  - `OnDelete` : `EventCallback` — when set, renders a Delete button; omitted callback hides the button.
  - `Saving` : `bool` (default `false`) — when true, disables all buttons.
  - `ShowCancel` : `bool` (default `true`) — toggles Cancel/Close visibility.
  - `CanDelete` : `bool` (default `true`) — controls Delete enabled state.
  - `CustomActions` : `RenderFragment` — slot for page-specific buttons.
  - `CustomActionsPosition` : `ActionRowSlot` (default `End`) — places `CustomActions` relative to standard buttons.
  - `Class` : `string?` — CSS class applied to the action-row container.
  - `TestId` : `string` (default `"action-row"`) — root `data-testid`.
  - `SaveLabel` : `string` (default `"Save"`), `CancelLabel` : `string` (default `"Cancel"`), `CloseLabel` : `string` (default `"Close"`), `DeleteLabel` : `string` (default `"Delete"`) — button labels.
  - `CancelDialogTitle` : `string` (default `"Discard changes?"`), `CancelDialogMessage` : `string` (default `"You have unsaved changes. Are you sure you want to discard them?"`), `CancelDialogConfirmLabel` : `string` (default `"Discard"`), `CancelDialogDismissLabel` : `string` (default `"Keep editing"`) — cancel-confirmation dialog copy.
  - `DeleteDialogTitle` : `string` (default `"Confirm delete"`), `DeleteDialogMessage` : `string` (default `"Are you sure you want to delete this record? This action cannot be undone."`), `DeleteDialogConfirmLabel` : `string` (default `"Delete"`), `DeleteDialogDismissLabel` : `string` (default `"Cancel"`) — delete-confirmation dialog copy.
  - `SaveTestId` : `string` (default `"save-button"`), `CancelTestId` : `string` (default `"cancel-button"`), `DeleteTestId` : `string` (default `"delete-button"`) — per-button `data-testid` overrides.
- **Stability tag:** `stable` — listed as a supported primitive in `src/Libs/Dabbler.Components/README.md`.
- **Common usage context:** Replaces the action button row of a bound Access form footer; centralizes dirty-tracking and confirmation dialogs for Cancel/Delete.

#### `DeclarativeFilterPanel<TEntity>`

- **Source:** `src/Libs/Dabbler.Components/MasterDetail/DeclarativeFilterPanel.razor` (markup-only).
- **Purpose:** Renders a Query-By-Example (QBE) filter panel from a declarative configuration object.
- **Required parameters:**
  - `Config` : `FilterConfig<TEntity>` — defines filterable properties, their types, and editor controls.
- **Optional parameters:**
  - `AriaLabel` : `string` — `aria-label` for the search region.
  - `TestId` : `string` — `data-testid` for the root element.
- **Stability tag:** `stable` — preferred filter panel per `src/Libs/Dabbler.Components/README.md`.
- **Common usage context:** Replaces an Access "search form" backing a continuous form or datasheet.

#### `FilterPanel<T>`

- **Source:** `src/Libs/Dabbler.Components/FilterPanel/FilterPanel.razor.cs`.
- **Purpose:** Renders a QBE filter panel from a flat list of `FilterColumnDefinition` objects (older, column-list-driven API; `DeclarativeFilterPanel` is the modern replacement).
- **Required parameters:**
  - `Columns` : `IReadOnlyList<FilterColumnDefinition>` — filterable column definitions.
- **Optional parameters:**
  - `FilterChanged` : `EventCallback<CompositeFilterDescriptor?>` — raised on each filter change with the latest descriptor.
  - `ShowAddGroup` : `bool` (default `true`) — show the "Add group" control for multi-group filters.
  - `ShowFilterChips` : `bool` (default `true`) — render an active-filter chip summary above the editors.
- **Stability tag:** `stable` — `src/Libs/Dabbler.Components/README.md` notes this is retained for legacy callers; new code should use `DeclarativeFilterPanel`.
- **Common usage context:** Older query pages built before the declarative configuration object existed.

### Reporting UI

Components from `Dabbler.Reports.Blazor` for the report viewing and parameter-prompting workflow.

#### `DabblerReportPage`

- **Source:** `src/Libs/Dabbler.Reports.Blazor/DabblerReportPage.razor.cs`.
- **Purpose:** Self-contained page that loads an RDF definition, prompts for parameters, runs the report, and renders the result in a `ReportViewer`.
- **Required parameters:**
  - `RdfPath` : `string` — path or resource name for the `.rdf.json` definition.
  - `DataProvider` : `IReportDataProvider` — data source used to execute the report query.
- **Optional parameters:**
  - `RdfLoader` : `Func<string, Task<string>>?` — custom RDF loader; defaults to filesystem read.
  - `SubreportRdfLoader` : `Func<string, string>?` — synchronous subreport-definition loader (required by `ReportDataEngine`).
  - `SubreportProviderFactory` : `Func<string, IReportDataProvider>?` — factory resolving a data provider per subreport connection name.
  - `Title` : `string?` — page title (defaults to the RDF metadata `name`).
  - `InitialParameterValues` : `Dictionary<string, object?>?` — pre-filled parameter values.
  - `AutoRunOnLoad` : `bool` — when all required params have defaults, run on load.
  - `CollapseParametersAfterRun` : `bool` (default `true`) — collapse the parameter form after first successful run.
  - `ShowPdfExport` / `ShowPrint` / `ShowZoom` : `bool` (each default `true`) — toggle viewer-toolbar actions.
  - `DefaultScale` : `double` (default `1.0`) — initial zoom scale.
  - `PdfFileName` : `string?` — PDF download filename; defaults to `"{ReportName}.pdf"`.
- **Stability tag:** `stable` — README-listed migration target and exercised by `Dabbler.Reports.Blazor.Tests`.
- **Common usage context:** Hosts a single Access report; replaces the "parameter prompt" dialog and subsequent preview window in a single Blazor page.

#### `ReportViewer`

- **Source:** `src/Libs/Dabbler.Reports.Blazor/ReportViewer.razor`.
- **Purpose:** Renders a collection of pre-composed report pages with zoom/scale support.
- **Required parameters:**
  - `Pages` : `IReadOnlyList<ComposedPage>` — composed pages to render.
- **Optional parameters:**
  - `Report` : `ReportDefinition` — used for ARIA labels.
  - `Scale` : `double` — zoom level.
- **Stability tag:** `stable`.
- **Common usage context:** Embedded inside `DabblerReportPage`, or used directly when a custom parameter-collection UI surrounds it.

#### `ParameterPromptForm`

- **Source:** `src/Libs/Dabbler.Reports.Blazor/ParameterPromptForm.razor`.
- **Purpose:** Renders a form with input controls for a report's parameters.
- **Required parameters:**
  - `Parameters` : `IReadOnlyList<ReportParameter>` — parameter definitions.
  - `OnRunReport` : `EventCallback<Dictionary<string, object>>` — fired when the user clicks "Run Report".
- **Optional parameters:**
  - `Title` : `string?` — optional title above the form.
  - `DataProvider` : `IReportDataProvider?` — used for parameter lookup queries (ComboBox / MultiSelectList).
  - `InitialValues` : `Dictionary<string, object>?` — default values per parameter.
  - `IsLoading` : `bool` — disables the form while a report is running.
- **Stability tag:** `stable`.
- **Common usage context:** The parameter-gathering UI used by `DabblerReportPage`. Can be used standalone when the caller wants to drive the run lifecycle manually.

### Application shell (compatibility)

Components from `Dabbler.Components.Compat`. Per `src/Libs/Dabbler.Components.Compat/README.md`, this package is **DEPRECATED — slated for removal in `0.4.0`**. New apps should compose `MudLayout` + `MudDrawer` + `MudNavMenu` + `MudDataGrid` directly. Stability tag for every component in this section is `deprecated`.

#### `DabblerShell`

- **Source:** `src/Libs/Dabbler.Components.Compat/Shell/DabblerShell.razor.cs`.
- **Purpose:** Top-level shell wiring `DabblerNavigationPanel` and `DabblerTabbedWorkspace`. Converts nav events into workspace tab operations and propagates the active tab's entity/record context back to the nav panel for relational smart-filter updates.
- **Required parameters:**
  - `MetadataProvider` : `IEntityMetadataProvider` — entity metadata source.
  - `NavigationDataProvider` : `INavigationDataProvider` — smart-filter data source for the nav panel.
  - `DetailFormFactory` : `Func<string, object, RenderFragment>` — produces a Detail Form for a given entity + primary key.
  - `SearchPageFactory` : `Func<string, SmartFilterDefinition?, RenderFragment>` — produces a Search/Query page (optionally pre-filtered).
  - `NewRecordFactory` : `Func<string, RenderFragment>` — produces a new-record Detail Form for a given entity.
- **Optional parameters:**
  - `ReportPageFactory` : `Func<string, RenderFragment>?` — produces a report page; required for report tabs.
  - `DashboardPageFactory` : `Func<DashboardConfiguration, RenderFragment>?` — produces a dashboard page (when null, dashboard tabs render a placeholder).
  - `DashboardConfigurationResolver` : `Func<string, DashboardConfiguration?>?` — resolves a `DashboardConfiguration` by id.
  - `HomePageConfiguration` : `HomePageConfiguration?` — initial workspace tab shown on login.
  - `UserRoles` : `IEnumerable<string>` — current user's roles, for per-role home-page overrides.
  - `Title` : `string?` — application title shown in the nav panel.
  - `MaxTabs` : `int` (default `15`) — max open workspace tabs.
  - `ReportItems` : `IReadOnlyList<NavigationReportItem>` — report items shown in the nav panel.
  - `EmptyStateTemplate` : `RenderFragment?` — custom empty-state content for the workspace.
  - `UsageTrackingProvider` : `IUsageTrackingProvider?` — when set with `CatalogProvider`, enables the top menu bar.
  - `CatalogProvider` : `IArtifactCatalogProvider?` — required (with `UsageTrackingProvider`) to render the top menu bar.
  - `UserId` : `string` (default `""`) — current user id, passed to the top menu bar for usage tracking.
  - `MenuBarTrailingContent` : `RenderFragment?` — right-side content of the menu bar (user profile, settings, etc.).
- **Common usage context:** The top-level `MainLayout.razor` for a migrated app using the legacy compatibility shell. New apps should not adopt this; compose MudBlazor primitives directly.

#### `DabblerNavigationPanel`

- **Source:** `src/Libs/Dabbler.Components.Compat/Shell/DabblerNavigationPanel.razor.cs`.
- **Purpose:** Dual-section nav: an entity list (top) plus a contextual smart-filter pill area (bottom) for the currently focused entity. Pills derive from `INavigationDataProvider` queries (top-N + count per smart filter).
- **Required parameters:**
  - `MetadataProvider` : `IEntityMetadataProvider`.
  - `NavigationDataProvider` : `INavigationDataProvider`.
- **Optional parameters:**
  - `ReportItems` : `IReadOnlyList<NavigationReportItem>` — flat report items.
  - `Title` : `string?` / `TitleTemplate` : `RenderFragment?` — application-title display (template wins).
  - `OnRecordSelected` : `EventCallback<(string EntityName, object PrimaryKey)>` — pill click.
  - `OnSearchRequested` : `EventCallback<(string EntityName, SmartFilterDefinition? PreFilter)>` — "all(N)" link click.
  - `OnNewRequested` : `EventCallback<string>` — new-record action.
  - `OnEntitySelected` : `EventCallback<string>` — entity-list click (opens Search tab).
  - `OnReportSelected` : `EventCallback<string>` — report-item click.
  - `IsCollapsed` : `bool` + `IsCollapsedChanged` — two-way binding for collapsed/icon-strip state.
  - `FocusedEntityName` : `string?` / `FocusedRecordKey` : `object?` — drive the contextual pill area.
  - `Width` : `string` (default `"280px"`).
  - `SplitPercent` : `double` (default `40`) — clamped to `[15, 85]`; entity-list share of vertical space.
  - `SplitRatioChanged` : `EventCallback<double>` — fired after the user finishes dragging the divider, for persistence.
- **Common usage context:** Composed inside `DabblerShell`; not generally used standalone.

#### `DabblerTabbedWorkspace`

- **Source:** `src/Libs/Dabbler.Components.Compat/Shell/DabblerTabbedWorkspace.razor.cs`.
- **Purpose:** Tab manager enforcing no-duplicate-record, dirty-close confirmation, max-tab limits, drag-reorder, middle-click close, `Ctrl+W` / `Ctrl+Tab` keyboard, and ARIA tabs pattern.
- **Required parameters:** none — all parameters are optional, but `Tabs` and the event callbacks must be wired by a parent for the workspace to do anything useful.
- **Optional parameters:**
  - `Tabs` : `IReadOnlyList<WorkspaceTab>` — current open tabs.
  - `ActiveTabId` : `string?` + `ActiveTabIdChanged` — two-way binding.
  - `MaxTabs` : `int` (default `15`) — when exceeded, the oldest non-dirty tab is auto-closed.
  - `OnTabClose` : `EventCallback<WorkspaceTab>` — fired after dirty-check passes.
  - `OnTabReorder` : `EventCallback<(int OldIndex, int NewIndex)>`.
  - `OnDirtyTabCloseConfirmed` : `EventCallback<WorkspaceTab>` — fired when user confirms closing a dirty tab.
  - `OnTabOpen` : `EventCallback<WorkspaceTab>` — parent must add the tab to `Tabs` and set `ActiveTabId`.
  - `EmptyStateTemplate` : `RenderFragment?` — shown when no tabs are open.
- **Common usage context:** Composed inside `DabblerShell`. Exposes `OpenTabAsync`, `CloseTabAsync`, `ActivateTab`, and `FindTabByRecord` as public methods for the shell.

#### `DabblerTopMenuBar`

- **Source:** `src/Libs/Dabbler.Components.Compat/Shell/DabblerTopMenuBar.razor.cs`.
- **Purpose:** Horizontal menu bar with Forms / Queries / Reports / Dashboards dropdowns. Each dropdown shows pinned, recent, and "All [Type]" entries fed by `IUsageTrackingProvider` + `IArtifactCatalogProvider`.
- **Required parameters:**
  - `UsageTrackingProvider` : `IUsageTrackingProvider`.
  - `CatalogProvider` : `IArtifactCatalogProvider`.
- **Optional parameters:**
  - `UserId` : `string` (default `""`).
  - `AppTitle` : `string?` / `AppTitleTemplate` : `RenderFragment?`.
  - `TrailingContent` : `RenderFragment?` — right-side content.
  - `OnArtifactSelected` : `EventCallback<(ArtifactType ArtifactType, string ArtifactId, string Name)>`.
  - `OnCatalogRequested` : `EventCallback<ArtifactType>` — fired by "All [Type]".
- **Common usage context:** Composed inside `DabblerShell`. Renders only when both `UsageTrackingProvider` and `CatalogProvider` are supplied.

#### `DabblerCatalogPage`

- **Source:** `src/Libs/Dabbler.Components.Compat/Pages/DabblerCatalogPage.razor.cs`.
- **Purpose:** Catalog search page listing all artifacts of a given type (Form / Query / Report / Dashboard). Reached from the "All [Type]" link in `DabblerTopMenuBar`. Supports search, pin/unpin, row-click navigation.
- **Required parameters:**
  - `ArtifactType` : `ArtifactType`.
  - `CatalogProvider` : `IArtifactCatalogProvider`.
  - `UsageTrackingProvider` : `IUsageTrackingProvider`.
- **Optional parameters:**
  - `UserId` : `string` (default `""`).
  - `OnArtifactSelected` : `EventCallback<(ArtifactType, string ArtifactId, string Name)>`.
- **Stability tag:** `deprecated` — type itself is `[Obsolete]` *and* `[EditorBrowsable(EditorBrowsableState.Never)]` in source. The README's deprecation also applies.
- **Common usage context:** Migrated apps using the compat shell render this from the `DabblerTopMenuBar` "All [Type]" link to give users a searchable catalog of an artifact kind. New apps should compose a MudDataGrid against `IArtifactCatalogProvider` directly.

### Security UI

Components from `Dabbler.Security.Blazor`. Source-of-record: `src/Libs/Dabbler.Security.Blazor/README.md`, which documents the supported public surface listed below.

#### Authentication pages

These page components are routed; routes come from `@page` directives in the `.razor` file. Neither page exposes `[Parameter]` properties; both are service-driven via `AddDabblerSecurityBlazor()` registration.

##### `LoginPage`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Pages/Auth/LoginPage.razor` (route: `/login`).
- **Purpose:** Magic-link login with optional external OIDC/OAuth entry points.
- **Required parameters:** none.
- **Optional parameters:** none — page is service-driven.
- **Stability tag:** `stable`.
- **Common usage context:** Replaces the Access login form in a migrated app. Mounted as a public route; redirects authenticated users to the configured post-login landing page.

##### `MfaPage`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Pages/Auth/MfaPage.razor` (route: `/mfa`).
- **Purpose:** TOTP setup and verification when product policy enables MFA.
- **Required parameters:** none.
- **Optional parameters:** none — page is service-driven.
- **Stability tag:** `stable`.
- **Common usage context:** Reached automatically after a successful primary-factor login when the active policy requires a second factor. No Access counterpart — this is a net-new capability the platform adds to migrated apps.

#### Admin pages

Routed pages composing the security-admin surface. All five are listed under "Admin pages" in the package README and exercised by `tests/Dabbler.Security.Blazor.Tests/`. Routes come from `@page` directives; route-segment values are populated by the router and are listed as Required parameters where present.

##### `UserListPage`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Pages/Admin/UserListPage.razor` (route: `/admin/users`).
- **Purpose:** List and manage application users (per package README).
- **Required parameters:** none.
- **Optional parameters:** none — page is service-driven.
- **Stability tag:** `stable`.
- **Common usage context:** Index page for the user-management workflow; reached from `DabblerSecurityNav` or a direct `/admin/users` link. Replaces the Access "Users" maintenance form.

##### `UserEditPage`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Pages/Admin/UserEditPage.razor` (route: `/admin/users/{id}`).
- **Purpose:** Edit user details and metadata for a specific user (per package README).
- **Required parameters:**
  - `Id` : `int?` — route segment; the user-id whose details are being edited.
- **Optional parameters:** none.
- **Stability tag:** `stable`.
- **Common usage context:** Row-click target from `UserListPage`. Replaces the Access "User Detail" subform.

##### `RoleListPage`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Pages/Admin/RoleListPage.razor` (route: `/admin/roles`).
- **Purpose:** View and manage security roles (per package README).
- **Required parameters:** none.
- **Optional parameters:** none — page is service-driven.
- **Stability tag:** `stable`.
- **Common usage context:** Index page for role management; reached from `DabblerSecurityNav` or a direct `/admin/roles` link. Replaces the Access "Roles" maintenance form.

##### `RoleAssignmentPage`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Pages/Admin/RoleAssignmentPage.razor` (route: `/admin/users/{UserId:int}/roles` — note: the package README's "Admin pages" table abbreviates this as `/admin/roles/assign`, but the file's `@page` directive is the route the router actually matches).
- **Purpose:** Assign and remove roles from a specific user (per package README).
- **Required parameters:**
  - `UserId` : `int` — route segment from `{UserId:int}`; the user whose role assignments are being edited.
- **Optional parameters:** none.
- **Stability tag:** `stable`.
- **Common usage context:** Reached from `UserEditPage` or `UserListPage` as the role-assignment workflow. Replaces the Access "Assign Roles" dialog.

##### `AuditLogPage`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Pages/Admin/AuditLogPage.razor` (route: `/admin/audit`).
- **Purpose:** View a chronological audit trail of security events (per package README).
- **Required parameters:** none.
- **Optional parameters:** none — page is service-driven.
- **Stability tag:** `stable`.
- **Common usage context:** Compliance/forensics view for administrators. No direct Access counterpart — this surfaces events emitted by `Dabbler.Security`'s audit pipeline.

#### Core authorization-UI components

##### `DabblerSecured`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Components/DabblerSecured.razor`.
- **Purpose:** Declarative wrapper enforcing page-, field-, or control-level permissions on `ChildContent`. Evaluates the active role's `UiRestriction` list for the given `(PageKey, ControlKey)`. Baked-in roles always see the content; user-type roles are default-permissive when no restriction applies.
- **Required parameters:**
  - `PageKey` : `string` — page or view key.
  - `ControlKey` : `string` — control key within the page; the literal `"*"` matches a page-wide restriction.
- **Optional parameters:**
  - `ChildContent` : `RenderFragment?` — content to be secured.
- **Render contract:** `Hidden` → renders nothing; `Disabled` → wraps content in `aria-disabled="true"` with reduced opacity and `pointer-events:none`; `Enabled` → renders content as-is.
- **Stability tag:** `stable` — README-listed as the declarative-security primitive.
- **Common usage context:** Wraps individual buttons, sections, or pages whose visibility/enablement depends on the active role. Replaces ad-hoc `If User.IsInRole(...)` checks in Access VBA — typically wrapped around `MudButton`, an `EditForm` section, or a custom block within a Detail Form.

##### `DabblerRoleSwitcher`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Components/DabblerRoleSwitcher.razor`.
- **Purpose:** Dropdown allowing users in configured roles to temporarily switch their effective role. Useful for administrators verifying behavior under another role's permissions.
- **Required parameters:** none.
- **Optional parameters:** none — service-driven. Visibility is gated by `SecurityConfigurationManager.Current.RoleSwitching.AllowedRoles`; the component renders nothing when the current user has no matching role.
- **Stability tag:** `stable`.
- **Common usage context:** Embedded in the app shell's `MudAppBar` (typically `MainLayout.razor`) so multi-role users — and admins testing role behavior — can switch their effective role without re-login. No Access counterpart.

##### `DabblerSecurityNav`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Components/DabblerSecurityNav.razor`.
- **Purpose:** Navigation menu item exposing the admin pages; auto-visible to users in a baked-in role with security-admin privileges.
- **Required parameters:** none.
- **Optional parameters:** none — service-driven.
- **Stability tag:** `stable`.
- **Common usage context:** Dropped into the app's primary `MudNavMenu` to give security administrators a single nav target for the admin/dashboard surface. Replaces an Access app's manually-curated "Admin" menu section.

#### Dashboard

##### `DabblerSecurityDashboard`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Components/Admin/DabblerSecurityDashboard.razor`.
- **Purpose:** Central admin panel with five tabs (Role Master Details, Default Masks, Organization Master Details, Users and Role Assignment, Role Documentation). Each tab is gated by the active baked-in role.
- **Required parameters:** none.
- **Optional parameters:** none — service-driven.
- **Stability tag:** `stable` — README-listed as the supported dashboard surface.
- **Common usage context:** Mounted as a single-page admin destination (typically at `/admin/security`) and used as the unified replacement for an Access app's collection of role/permission/audit maintenance forms.
- **Note on internals:** The dashboard composes a tree of internal admin tab/sub-tab/dialog components under `Components/Admin/{ComplianceReview,Documentation,Masks,Organization,Role,Shared,Users}/` (e.g., `RoleMasterDetailsTab`, `DefaultMasksTab`, `OrganizationDetailsForm`, `ColumnRestrictionsSubTab`, `RoleMasterGrid`, `PickOrAddDialog`, `AddUserTypeRoleDialog`, `UsersAndRoleAssignmentTab`). These are implementation details of `DabblerSecurityDashboard` and are **not independent migration targets** — they are not part of the package's public migration surface. Migrating consumers should adopt `<DabblerSecurityDashboard />` as a single unit. If a consumer needs to customize a sub-tab, the supported path is configuration via `Dabbler.Security` services, not direct embedding of an internal admin component.

##### `RestrictedFormField`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Components/Restriction/RestrictedFormField.razor`.
- **Purpose:** Wraps a single form field and applies the active user's restrictions for the given `(EntityKey, ColumnKey)`. Resolves on initialize and on `AuthorizationStateNotifier` notifications (role switches re-render in the same circuit).
- **Required parameters:**
  - `EntityKey` : `string` — entity (table) key.
  - `ColumnKey` : `string` — column (field) key.
- **Optional parameters:**
  - `EntityId` : `string?` — row identifier; when supplied with an exempted-unmasked resolution, the binder emits a `FieldUnmaskedRead` audit row keyed on `(CorrelationId, EntityKey, EntityId, ColumnKey)`.
  - `Value` : `string?` — raw stored value; passed to `IFieldMaskService.RenderMasked` when masked.
  - `ChildContent` : `RenderFragment<RestrictionResolution>?` — receives the resolved restriction so the consumer can apply `Disabled` / `ReadOnly` on the inner control.
  - `MaskedTemplate` : `RenderFragment<RestrictionResolution>?` — overrides the default `data-masked="true"` span rendering.
- **Render contract:** `Hidden` → nothing; `Masked` → `MaskedTemplate` or fallback `data-masked` span via `IFieldMaskService.RenderMasked`; otherwise invokes `ChildContent` with the resolution.
- **Stability tag:** `stable`.
- **Common usage context:** Wraps a single field control inside a Detail Form so the value is masked, hidden, or marked read-only per the current role's column restrictions. Replaces Access form `OnCurrent` / `OnGotFocus` VBA that conditionally disabled or formatted PII fields.

##### `RestrictedGridColumn`

- **Source:** `src/Libs/Dabbler.Security.Blazor/Components/Restriction/RestrictedGridColumn.razor`.
- **Purpose:** Wraps a MudDataGrid column definition and applies the active user's restrictions for the given `(EntityKey, ColumnKey)`.
- **Required parameters:**
  - `EntityKey` : `string`.
  - `ColumnKey` : `string`.
- **Optional parameters:**
  - `EntityId` : `string?` — for single-entity-key-value views; per-cell `FieldUnmaskedRead` emission in a real multi-row grid remains the `CellTemplate` consumer's responsibility.
  - `ChildContent` : `RenderFragment<RestrictionResolution>?` — receives the resolution so the consumer can wire `IsEditable` / `CellTemplate` / etc.
- **Render contract:** `Hidden` → emits no column (MudDataGrid drops it from header and rows); otherwise invokes `ChildContent`.
- **Stability tag:** `stable`.
- **Common usage context:** Used inside `MudDataGrid` definitions so restricted columns disappear or render masked per the active role. Replaces Access continuous-form code that hid/altered grid columns conditionally on user role.

### Stubs and testing

Classes from `Dabbler.Platform.Stubs.Components` and `Dabbler.Testing.Blazor`. **Not migration targets** — these support test scaffolding and isolated UI demos.

- **Surface:** `StubGridDataProvider`, `StubComboBoxItemProvider`, `StubEntityMetadataProvider`, `StubNavigationDataProvider`
  - **Source:** `src/Libs/Dabbler.Platform.Stubs.Components/`.
  - **Purpose:** In-memory implementations of core data-provider interfaces for bUnit tests or UI demos.
  - **Stability tag:** `in-flight` — test scaffolding subject to change.
- **Surface:** `TrackedComponentBase`
  - **Source:** `src/Libs/Dabbler.Testing.Blazor/Components/TrackedComponentBase.cs`.
  - **Purpose:** Base class for components under test that synchronizes Playwright execution with Blazor's render lifecycle.
  - **Stability tag:** `in-flight`.

## Components NOT in dabbler-platform

Two negative findings worth recording, since downstream consumers should not search for these:

- **No general-purpose form-input library.** The platform expects callers to use MudBlazor inputs directly. Migrating an Access form means composing `MudTextField`, `MudSelect`, `MudCheckBox`, `MudDatePicker`, etc., with `DirtyAwareActionRow` for action buttons and `RestrictedFormField` per security-sensitive field.
- **No global app-wrapper that replaces `DabblerShell` outside of Compat.** New apps build their own `MainLayout.razor` from MudBlazor primitives; there is no MudBlazor-native successor shell shipped today.
