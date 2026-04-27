# Session 1 Synthesis — Synthetic Declaration Injection

> **Scope:** `access-resolver-symbol-table-integration` Session 1 of 2-3.
> **Phase reference:** Plan §4.4 (symbol-table integration).
> **Prereqs read:** `resolver-default-members-and-inference` (#9) →
> `ReferenceResolver` shape, `IAccessObjectModelRegistry` /
> `NullAccessObjectModelRegistry`, `Declaration` taxonomy, scope-tree
> shape; `access-metadata-extraction` (#10) → `FormMetadata`,
> `ReportMetadata`, `AccessControl`, fixtures `01`/`02`/`03`;
> `access-type-library-registry` (#11a) → `ITypeLibraryRegistry`,
> `TypeInfo`, `MemberInfo`, the `Access.Form`/`Access.Report`/
> `Access.Control` family.
> **Authored by:** Claude Opus 4.7 (orchestrator-as-synthesizer per
> project-guidance Convention "When the orchestrator's model matches
> the routing target…"; trigger phrase included `— maxout Claude`).

## 1. Where this layer sits

The resolver was authored against the contract already declared at
`AccessHarvester.Vba.Resolver/References/IAccessObjectModelRegistry.cs`
with three lookup methods plus a `NullAccessObjectModelRegistry`
default. The existing call sites in `ReferenceResolver` are:

- `ResolveIdentifier(name)` step 2 → `TryResolveTopLevelMember(name)`
  for bare-name identifiers like `Forms`, `Reports`, `DoCmd`,
  `frmCustomers`.
- `MeRegistryLookup(owningClass, member)` →
  `TryResolveControlOnForm(owningClass.Name, member)` /
  `TryResolveControlOnReport(...)` for `Me.<control>` inside a form /
  report module.
- `SubChildLookup(parentDecl, member)` — currently a stub that always
  returns `null`. The session-1 comment marks this as #11's
  responsibility.

`AccessIntegration/` is the implementation half of those hooks. It
consumes `FormMetadata` / `ReportMetadata` (Phase 4.1 / 4.2) and the
`ITypeLibraryRegistry` (Phase 4.5) and exposes a backed
`IAccessObjectModelRegistry`.

## 2. Boundary decisions

1. **One project reference is added.**
   `AccessHarvester.Vba.Resolver` already references
   `AccessHarvester.Vba.Parser`. We add a reference to
   `AccessHarvester.Access` (the metadata + registry library). The
   reverse-direction reference does not exist and stays absent — the
   metadata library never sees the resolver. Keeps the build DAG
   acyclic.

2. **Symbol-table injection is an external builder, not a mutation
   of `ScopeBuildingVisitor`.** The visitor stays purely-source-driven.
   Synthetic declarations are surfaced to the resolver through the
   `IAccessObjectModelRegistry` hooks, which is exactly the shape #8
   was designed for. Two consequences:
   - We do **not** have to reach into a form module's already-built
     `ModuleScope.Declarations` and append synthetic controls.
     `Me.<control>` resolves through the registry path
     (`MeRegistryLookup`), and bare `<control>` references inside a
     form module's procedures resolve via the same path through the
     scope-chain → `TryResolveTopLevelMember` fallback **after** we
     extend that fallback to consult an enclosing form/report context
     when the current module is one. (See §6.2.)
   - Forms and reports without code-behind (the entire 01/02/03
     fixture set) still get full coverage: the registry is queryable
     by bare name regardless of whether a class module exists.

3. **Member resolution against synthetic-declared types flows through
   one new interface method.** We extend `IAccessObjectModelRegistry`
   with one method:

   ```csharp
   Declaration? TryResolveMemberOf(Declaration parent, string memberName);
   ```

   `ReferenceResolver.SubChildLookup` is rewritten to call it. The
   `NullAccessObjectModelRegistry` returns `null`. The Access-backed
   implementation handles three cases:
   - `parent` is a `SyntheticControlDeclaration` carrying a
     type-library type name — call
     `ITypeLibraryRegistry.TryGetMember(parent.TypeLibraryTypeName,
     memberName, …)` and wrap the hit in a synthetic
     `MemberDeclaration`.
   - `parent` is a `SyntheticSubformControlDeclaration` (a control
     whose Access class is `Subform` / `Subreport`) and `memberName`
     is `Form` / `Report` — return the contained form/report's
     synthetic declaration.
   - Otherwise — `null`. Lets the existing
     `default-member-not-yet-resolved` fallback fire as before.

4. **`SubChildLookup`'s contract widens but its callers don't change.**
   The existing call sites in `DecideMemberOutcome` still pass any
   resolved declaration through `SubChildLookup` — the only change is
   that the lookup now does work instead of always returning `null`.
   Outcomes that aren't relevant (built-in functions, source-defined
   procedures, etc.) keep falling through to the
   `default-member-not-yet-resolved` reason. This is additive.

## 3. Synthetic declaration taxonomy

All synthetic types live under
`AccessHarvester.Vba.Resolver.AccessIntegration`. They subclass
`Declaration` so they slot into the existing `ResolutionOutcome`
shape (`Resolved` / `ResolvedToObjectModel` carry a `Declaration`).

| Type | Represents | Key fields |
|---|---|---|
| `SyntheticAccessDeclarationProvenance : DeclarationProvenance` | Provenance for any synthetic decl. `Range` is always null (no source location). | `Artifact` (form/report name or `"Application"` for the host root), `Source = "synthetic-from-access"`. |
| `SyntheticFormsCollectionDeclaration : Declaration` | The host-owned `Forms` (or `Reports`) collection. | `Kind` (Forms / Reports). Default-member access into `Forms!frmName` is handled by `TryResolveMemberOf` returning the matching form decl. |
| `SyntheticFormDeclaration : Declaration` | One Access form. | `FormName`, `ModuleName?`, `TypeLibraryTypeName = "Access.Form"`, `Controls` (case-insensitive dict of synthetic control decls), `Provenance.Artifact = FormName`. |
| `SyntheticReportDeclaration : Declaration` | One Access report. | `ReportName`, `ModuleName?`, `TypeLibraryTypeName = "Access.Report"`, `Controls`, `Provenance.Artifact = ReportName`. |
| `SyntheticControlDeclaration : Declaration` | One control on a form/report. | `ControlName`, `AccessClassName` (e.g. `"TextBox"`), `TypeLibraryTypeName?` (e.g. `"Access.TextBox"`, null when class-name → type-library map has no entry), `OwnerArtifact` (the form/report name for provenance). |
| `SyntheticSubformControlDeclaration : SyntheticControlDeclaration` | Subform / Subreport control with binding. | adds `ContainedFormName?` / `ContainedReportName?` (mutually exclusive) — populated from the control's `SourceObject` property. |
| `SyntheticTypeLibraryMemberDeclaration : Declaration` | A property/method/event/field hit returned from `ITypeLibraryRegistry`. | `MemberInfo` (the registry's record), `OwnerTypeName` (the type the member lives on, walking the inheritance chain), `TypeLibraryTypeName?` of the member's return type — null for void / events / unpinned. |

Equality: all synthetic declarations are `class`es and use reference
equality, matching the rest of the `Declaration` hierarchy. The
registry caches one instance per logical entity so identity matches
across repeated lookups.

## 4. Class-name → type-library mapping

Authored as a static helper class
`AccessIntegration.AccessControlClassMap`. Entries:

| Access class name | Type-library type |
|---|---|
| `TextBox` | `Access.TextBox` |
| `ComboBox` | `Access.ComboBox` |
| `ListBox` | `Access.ListBox` |
| `CommandButton` | `Access.CommandButton` |
| `Label` | `Access.Label` |
| `Subform` | `Access.SubForm` *(see note)* |
| `Subreport` | `Access.SubReport` *(see note)* |
| anything else | `Access.Control` (fallback — universal control surface) |

> **Note on Subform / Subreport.** #11a's `TypeLibraryRegistry`
> registers `Access.Form`, `Access.Report`, `Access.Control` and the
> common subtypes, but does **not** currently register
> `Access.SubForm` / `Access.SubReport`. We map to those names anyway
> for forward-compatibility with #11a, and the registry returns
> "not registered" today; the subform-navigation path (`subform.Form`
> → contained form) is handled directly in
> `TryResolveMemberOf` rather than going through
> `ITypeLibraryRegistry`, so the mapping miss does not block resolution
> for the real navigation case. A later session set may add these to
> #11a; that addition is purely additive.

The fallback to `Access.Control` is intentional. Access has dozens of
control classes (PageBreak, Rectangle, Image, Line, Tab, Page,
NavigationButton, NavigationControl, ToggleButton, OptionButton,
OptionGroup, OleObject, ChartSpace, …) that share most of their
surface with `Access.Control`. Returning `Access.Control` lets `.Name`,
`.Visible`, `.Width`, `.Tag`, etc. resolve uniformly without
exhaustively enumerating every class. Curators add specific subtypes
to #11a only when there is a member-set that's worth distinguishing.

## 5. Tree walk: extracting controls

`AccessSymbolTableBuilder` walks each `FormMetadata.Children` /
`ReportMetadata.Children` recursively. Rules:

- **Skip wrappers.** `Section`, `FormHeader`, `FormFooter`,
  `PageHeader`, `PageFooter`, `GroupLevel0Header`,
  `GroupLevel<N>Header`/`Footer`, the anonymous `Begin` wrapper —
  none of these are name-resolution targets in VBA, so we don't emit
  declarations for them; we recurse into their `Children`.
- **Emit one declaration per leaf control with a non-null `Name`.**
  `Type` becomes `AccessClassName`; `AccessControlClassMap` resolves
  the type-library name. Anonymous nodes (Name == null, e.g. a `Begin`
  wrapper or an Access node Access didn't name) are skipped silently.
- **Subforms / Subreports get the subform decl variant.** For a node
  with `Type ∈ {"Subform", "Subreport"}`, look up the `SourceObject`
  property in `Properties` (a JSON string scalar); when present and a
  bare identifier, populate `ContainedFormName` /
  `ContainedReportName` on the synthetic decl. SourceObject values
  like `"Report.<name>"` (Access uses this prefix to disambiguate when
  a subreport hosts a report-class object inside a Subform control)
  are stripped to the bare name. Unparseable values produce a control
  decl with binding null — the navigation hook then returns null at
  resolution time, which is the deferred-default-member fallback.
- **Duplicate names within one form/report.** Access disallows
  duplicates so we treat the second occurrence as a fixture defect:
  the first wins, the duplicate is silently dropped. We do **not**
  throw — synthetic declarations have to be best-effort because the
  metadata extractor's job is to faithfully record what Access wrote,
  including pathological cases.
- **Case-insensitive control lookup.** The control dictionary uses
  `StringComparer.OrdinalIgnoreCase` (matching `Declaration.NameComparer`),
  so `Me.txtFoo`, `Me.TxtFoo`, `Me.TXTFOO` all hit the same decl.

## 6. Registry semantics

### 6.1 `TryResolveTopLevelMember(name)`

In order of precedence:

1. `name` equals `"Forms"` (case-insensitive) → return the cached
   `SyntheticFormsCollectionDeclaration` (Kind = Forms).
2. `name` equals `"Reports"` → return the cached collection decl
   (Kind = Reports).
3. `name` matches a form's `Name` (case-insensitive) → return that
   form's `SyntheticFormDeclaration`. Bare-name access resolves like
   `frmCustomers!txtFoo` — VBA allows naming a form directly.
4. `name` matches a report's `Name` → return that report's
   `SyntheticReportDeclaration`.
5. Otherwise → `null`. Other host-only top-levels (`DoCmd`,
   `Application`, `Screen`, `CurrentProject`, …) are handled by the
   built-in / type-library path; future sessions may add them to the
   registry as additional cached top-level entries, but they are out
   of scope for #11b Session 1 — the spec calls out forms / reports /
   controls explicitly.

If a form's bare `Name` collides with a report's bare `Name` (rare but
legal in Access), forms win — Access's own `Forms!Foo` parsing has
the same precedence. Both are still queryable through the
`Forms`/`Reports` collection paths.

### 6.2 `TryResolveControlOnForm` / `TryResolveControlOnReport`

The resolver passes the form/report's identifier from the
`OwningClass.Name`. Per `MeRegistryLookup`, that name is whatever
`ScopeBuildingVisitor`'s caller used when constructing the
`ClassDeclaration` — typically the **module name** (e.g.
`Form_Customers`), not the bare form name (`Customers`).

For the 01/02/03 fixtures none of the forms have a code-behind module
(`ModuleName == null`), so the `Me`-via-class path is structurally
unreachable in those fixtures — that path will be exercised in
fixtures with code-behind. But we still need a robust resolution
strategy:

1. Lookup by `ModuleName` (case-insensitive) against
   `_formsByModuleName` first. Hits when `OwningClass.Name` is the
   module name `Form_Customers`.
2. Fall back to `Name` lookup against `_formsByName`. Hits when a
   caller supplies the bare form name (some host code paths and most
   tests pass the bare name).

This dual-key index is the cheapest robustness layer; the alternative
(forcing the harvester to canonicalise to one form) would tightly
couple #11b to choices made in #7 / #10.

### 6.3 `TryResolveMemberOf(parent, member)`

Dispatch order:

1. `parent is SyntheticSubformControlDeclaration sfc`:
   - if `member` equals `"Form"` (case-insensitive) and
     `sfc.ContainedFormName` is non-null → return that form's
     `SyntheticFormDeclaration`.
   - if `member` equals `"Report"` and `sfc.ContainedReportName` is
     non-null → return that report's `SyntheticReportDeclaration`.
   - otherwise fall through to step 2 (subform controls also have
     the universal `Access.SubForm` / fallback `Access.Control`
     surface — `.Name`, `.Visible`, etc.).
2. `parent is SyntheticControlDeclaration sc` and
   `sc.TypeLibraryTypeName` is non-null:
   - call `_typeLib.TryGetMember(sc.TypeLibraryTypeName, member, …)`;
     if hit, wrap the returned `MemberInfo` in a
     `SyntheticTypeLibraryMemberDeclaration`. (Cached per
     (typeName, memberName) pair so identity remains stable across
     repeated lookups.)
3. `parent is SyntheticFormDeclaration sfd`:
   - if `_typeLib.TryGetMember("Access.Form", member, …)` hits,
     wrap and return — covers `Me.RecordSource`, `Me.Refresh`, etc.
     when a form has a class module.
   - else if `sfd.Controls` contains a key matching `member`
     case-insensitively → return that synthetic control decl.
     Covers `Forms!frmCustomers!txtFoo` — `Forms!frmCustomers`
     resolves to the form decl (collection-default-member access),
     `!txtFoo` lands here.
4. `parent is SyntheticReportDeclaration srd`: same shape, with
   `Access.Report` instead of `Access.Form`.
5. `parent is SyntheticFormsCollectionDeclaration coll`:
   - if `member` matches a known form (Kind == Forms) /
     report (Kind == Reports) → return that form/report decl.
     Implements the `Forms!frmFoo` default-member shape that
     `BangAccessExpression` lowers into.
6. `parent is SyntheticTypeLibraryMemberDeclaration mem`:
   - if the member's `ReturnType` is itself a registered type, walk
     into it. Lets chained lookups like
     `Me.RecordsetClone.MoveNext` work — `RecordsetClone` returns
     `DAO.Recordset`; the second hop calls
     `_typeLib.TryGetMember("DAO.Recordset", "MoveNext", …)`.
7. Anything else → `null`. Source-defined declarations stay on the
   in-source resolution path; the existing
   `default-member-not-yet-resolved` fallback handles them as
   before.

This dispatch handles all four cases the spec calls out under
Feature 1 → Scope:

- form-module class-scope synthetic decls (case 2/3 above);
- project-scope `Forms` / `Reports` (case 5 / 1 of `TryResolveTopLevel`);
- subform navigation (case 1);
- type-library member resolution (case 2/3/4/6).

## 7. ReferenceResolver hook change

`ReferenceResolver.SubChildLookup` is rewritten from the stub to:

```csharp
Declaration? SubChildLookup(Declaration parentDecl, string member)
    => _registry.TryResolveMemberOf(parentDecl, member);
```

That is the only line of `ReferenceResolver` that changes.
`NullAccessObjectModelRegistry.TryResolveMemberOf` returns `null`,
preserving the existing default-behaviour path. All callers of the
single-line `SubChildLookup` are unchanged.

## 8. What stays out of Session 1

- **Live `Application` / `DoCmd` / `Screen` / `CurrentProject` decls.**
  Adding cached top-level entries for these would be additive and
  uncontroversial, but the spec calls out forms/reports/controls
  explicitly; Session 2 (cross-cut + tightening) is the place to
  decide whether to absorb them based on what the fixtures exercise.
- **Cross-database / linked-table-style hosts.** Not relevant at this
  layer.
- **Late-bound `CallByName` / `Eval` / dynamic SQL.** The deferred
  Phase 5 work — these stay `Unresolved` per project-guidance's "VBA
  Parser Strategy → unresolvable constructs are explicit" principle.

## 9. Test surface

Hermetic unit tests under
`tests/AccessHarvester.Vba.Resolver.Tests/AccessIntegration/`. End-to-end
fixture replay (running the resolver against `01`/`02`/`03`'s VBA with
synthetic decls injected) is Session 2's job. Session 1 tests pin the
contract:

1. `AccessControlClassMapTests` — known classes resolve, fallback to
   `Access.Control` for unmapped classes, case-insensitivity.
2. `AccessSymbolTableBuilderTests` — walks a hand-built
   `FormMetadata` with nested sections + a Begin wrapper + a subform
   pointing at a sibling form. Asserts the resulting
   `SyntheticFormDeclaration.Controls` keys, the
   `SyntheticSubformControlDeclaration.ContainedFormName`, and that
   anonymous nodes are skipped.
3. `AccessObjectModelRegistryTests` — covers all 7 dispatch cases of
   `TryResolveMemberOf` and the dual-key index of
   `TryResolveControlOnForm` (lookup by ModuleName and by Name).
4. `ReferenceResolverAccessIntegrationTests` — feeds a hand-built
   `ModuleScope` + AST through `ReferenceResolver` with a populated
   `AccessObjectModelRegistry`, asserts that:
   - `Forms` resolves to the Forms-collection decl;
   - `Forms!frmCustomers` resolves to the form decl;
   - `Me.txtFoo` (inside a form module with `OwningClass`) resolves
     to the synthetic control decl;
   - `Me.txtFoo.Value` resolves to the type-library member;
   - `Me.sfrmOpenCases.Form.txtChildField` resolves through the
     subform-navigation hop.

`NullAccessObjectModelRegistry.TryResolveMemberOf` returning null is
covered by the existing `ReferenceResolver` tests (no change in
behaviour for the null-registry path).

## 10. Surface stability for Sessions 2-3

Session 2 will run the resolver against `01`/`02`/`03` end-to-end and
schema-validate the output. Adjustments expected at that boundary are
pure plumbing (constructor wiring in `AccessHarvester.Cli` /
`AccessHarvester.Core`) plus possibly refining the
`SourceObject`-string parser if a fixture reveals a format we didn't
anticipate. The interface and synthetic-decl shapes declared here are
the contract; Session 2 should not reshape them.
