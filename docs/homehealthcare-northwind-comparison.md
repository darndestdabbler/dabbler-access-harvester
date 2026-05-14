# HomeHealthCare.accdb vs NorthwindDevEdition.accdb

> **Fixture-selection rationale:** This document is the authoritative record
> of why these two databases are the canonical test fixtures for this repo.
> See `docs/planning/project-guidance.md` → **Canonical Test Fixtures** for
> the summary table and excluded-fixture policy.

`HomeHealthCare.accdb` (HHC) is a deliberately different test fixture for Access migration tooling, designed to complement Microsoft's `NorthwindDevEdition.accdb` (NW). Where Northwind exercises the patterns most Access apps use (bound split forms, subforms, audit data macros, an `AutoExec`-driven startup chain), HHC exercises a complementary set (unbound listbox-cascade search, popup detail forms opened with `acDialog`, validation-only data macros, ADODB for transactional bulk copy). A migration tool that handles both correctly is more likely to handle a real-world Access database it has never seen.

This document compares the two on the dimensions that matter for that tooling. The HHC side is read from the committed YAML mirror at `DatabaseYaml/HomeHealthCare.accdb/`. The Northwind side is drawn from a locally-generated YAML extract (`DatabaseYaml/NorthwindDevEdition.accdb/`) that is not committed to this repo — see the README for how to generate it.

## Object counts

| Category       | HomeHealthCare | Northwind |
|----------------|---------------:|----------:|
| Tables         | 13             | 29        |
| Queries        | 22             | 65        |
| Forms          | 19             | 40        |
| Reports        | 12             | 15        |
| Macros         | 4              | 1         |
| Modules        | 32             | 66        |
| Relationships  | 10             | 27        |

HHC is roughly half the size by every count except macros, which is intentional. A focused fixture surfaces problems faster than a sprawling one; the two together cover both ends.

## Search/select patterns

Northwind's list views are **bound split forms**. `frmCompanyList` has `RecordSource: qryCompanyList`, `DefaultView: 5` (Split Form), `SplitFormSize: 1290`, `NavigationButtons: True` — the user scrolls Access's built-in record navigator to pick a row, and the matching `frmCompanyDetail` is bound to `qryCompanies` and embeds four subforms (`sfrmCompanyDetail_CustomerOrders`, `sfrmCompanyDetail_VendorPurchaseOrders`, `sfrmCompanyDetail_ShipperOrders`, `sfrmCompanyDetail_Contacts`). Northwind has **17 `sfrm`-prefixed subforms** in total.

HHC's list views are **unbound listbox-cascade forms**. `frmClientSearch` has `RecordSource: ""`, `NavigationButtons: False`, and an `OnLoad` embedded macro that sets a status filter and focuses a name-filter textbox. The form is a host for filter controls plus a listbox; selecting a row opens `frmClientDetail` as a popup via `DoCmd.OpenForm ... acDialog`. The detail form is bound to `tblClients` and has `NavigationButtons: False` with a caption set dynamically per record (`"Margaret Sullivan — Client Detail"`). HHC has **0 `sfrm`-prefixed subforms**.

| Aspect                  | HomeHealthCare                          | Northwind                                  |
|-------------------------|-----------------------------------------|--------------------------------------------|
| List-form `RecordSource`| unbound (`""`)                          | bound (`qryCompanyList`)                   |
| List-form `DefaultView` | 0 (Single Form)                         | 5 (Split Form)                             |
| List-form navigation    | filter textbox + listbox → `OpenForm`   | Access nav buttons on split form           |
| Detail-form navigation  | `NavigationButtons: False`, popup       | `NavigationButtons: True`, bound nav       |
| Subforms (`sfrm*`)      | 0                                       | 17                                         |
| Detail open pattern     | `OpenForm name, , , , , acDialog, args` | record selection on bound list/detail pair |

For a migration tool, the difference matters: the HHC pattern relies on parsing `OpenArgs` strings (e.g. `"ADD"`, `"ADD|key=val|..."`, or a numeric PK) at the target side, while the Northwind pattern relies on faithfully reproducing split-form + subform layout and the record-context wiring between them.

## Macros

| Aspect             | HomeHealthCare                                       | Northwind                                  |
|--------------------|------------------------------------------------------|--------------------------------------------|
| Macro objects      | 4 (`AutoKeys`, `mcrValidation`, `mcrErrorHandler`, `mcrSwitchboardActions`) | 1 (`AutoExec`) |
| `AutoExec`         | none                                                 | yes — `RunCode "Startup()"` on trusted; else opens `frmStartup` |
| Switchboard wiring | `mcrSwitchboardActions` (named macro per button)     | code-driven from `frmStartup`              |
| Tables with data macros | 12 of 13                                        | 29 of 29                                   |
| Data-macro purpose | **Validation** (BeforeChange) — table-level enforcer that matches the form's `BeforeUpdate` messenger | **Audit fields** (BeforeChange) — populate/update `AddedBy`, `AddedOn`, `ModifiedBy`, `ModifiedOn` |

Both databases use BeforeChange data macros heavily, but for opposite reasons. Northwind's data macros are a **derivation** concern (write audit columns); HHC's are an **enforcement** concern (refuse bad rows). HHC explicitly does not have audit-log macros — that's an out-of-scope decision documented in [docs/planning/CLAUDE_CODE_BATON.md](docs/planning/CLAUDE_CODE_BATON.md). A migration tool that only ever saw Northwind data macros could easily assume they're skippable cosmetic noise; in HHC they're the *enforcer*, and stripping them would silently weaken the schema.

## VBA libraries

| Library reference | HomeHealthCare                                       | Northwind                                   |
|-------------------|------------------------------------------------------|---------------------------------------------|
| `DAO.*`           | 37 occurrences across 6 files                        | 24 occurrences across 10 files              |
| `ADODB.*`         | 15 occurrences across 3 files (`basDataOperations`, `basUtilities`, `basListHelpers`) | 0 |
| Error-handling style | `mcrErrorHandler` — macro object                  | `clsErrorHandler` — VBA class module        |
| Dedicated DAO helper module | none — DAO usage is co-located with feature code | `modDAO`                          |

HHC is unusual in being a single Access database that uses **both DAO and ADODB**. The ADODB block lives in `basDataOperations` and powers a transactional copy of `tblProviderServiceCatalog` rows from one fiscal year to the next (open `ADODB.Connection` against `CurrentProject.Connection`, begin transaction, copy fields, commit or roll back). A migration tool that only handles DAO will silently drop the transactional semantics of that operation; one that only handles ADODB will trip over Northwind's `DAO.Recordset` patterns and `modDAO` helpers.

The error-handling divergence is a smaller but real source of friction: lifting Northwind's `clsErrorHandler` to another platform is a class-translation problem; lifting HHC's `mcrErrorHandler` is a macro-object problem. They share no machinery.

## Notable differences (at a glance)

- **Startup:** HHC opens directly to `frmSwitchboard`; Northwind runs `AutoExec` → `Startup()` (or `frmStartup` on untrusted DBs) → optional `frmLogin` against `frmCredentials`.
- **Security:** Northwind ships a user/role model (`Employees`, `EmployeePrivileges`, `Privileges`, `frmLogin`); HHC has none — it's single-user by design.
- **Lookup tables:** HHC names them with a `tlkp` prefix (`tlkpFiscalYears`, `tlkpPlanStatuses`, `tlkpServiceCategories`, `tlkpStates`); Northwind uses plain names (`CompanyTypes`, `OrderStatus`, `TaxStatus`, `Titles`, `States`).
- **Reports:** Both use main-report + subreport composition. HHC's `rptClientProfile` embeds `srptClientCurrentNeeds`/`srptClientBudgets`/`srptServicePlanEntries`/`srptServiceShortfall`; Northwind's `rptInvoice` embeds `srptOrderForm` etc.
- **Domain:** HHC = home-healthcare agency (clients, providers, services, fiscal-year budgets, service plans, rate limits). Northwind = order entry (customers, employees, products, orders, purchase orders, inventory). The schemas exercise different join shapes — HHC has a four-way `Client × Service × Provider × FiscalYear` plan/budget relationship that has no direct Northwind analogue.
- **Object naming:** HHC uses Hungarian-ish prefixes throughout (`tbl`, `tlkp`, `qry`, `qxt` for crosstab, `frm`, `rpt`, `srpt`, `bas`, `mcr`). Northwind mixes prefixed and unprefixed names freely (`Companies` vs `qryCompanies`, `frmCompanyDetail` vs `Catalog_TableOfContents`).

The throughline: HHC is built around a single coherent set of conventions (listbox + popup, `OpenArgs` contract, prefixed names, validation data macros, mixed DAO/ADODB) maintained consistently across all forms. Northwind covers a wider surface (split forms, subforms, security, ribbons, startup chain, audit macros) at the cost of a less uniform internal style. For migration tooling, both are useful; for testing migration tooling, having both is better than having either alone.
