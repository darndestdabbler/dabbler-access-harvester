# Interpretation Guide — Reading A Harvester Run Output

> **Status:** Authored by Session 2 of
> `harvester-finish-operations-and-interpretation-docs` against the
> outline at
> [`./interpretation-guide-outline.md`](./interpretation-guide-outline.md).
> Section ordering and per-section JSON-snippet requirements are
> decisions of record from Session 1 (audience-check probe) and
> are reflected here.
>
> **Audience:** A downstream AI consumer reading a harvester run
> output (Blazor-migration agent, documentation generator, search
> index, ad-hoc analysis script). **Not** a human onboarding to the
> harvester project itself — that audience reads
> [`README.md`](../../README.md) and the schema docs at
> [`docs/schema/`](../schema/).
>
> **Length:** ~4,200 words within the outline's 3,500–5,000-word
> budget. The lean-by-default Principle in
> [`docs/planning/project-guidance.md`](../planning/project-guidance.md)
> applies: enough prose to interpret the JSON, no more.

---

## 1. Audience and purpose

The Access Harvester's job is to lift the structural and behavioural
content out of a Microsoft Access database — forms, reports, queries,
modules, tables, relationships, macros, validation rules, and the
resolved-where-possible references between them — into a tree of JSON
files a downstream AI agent can read deterministically. The harvester
performs mechanical extraction and rule-based enrichment only;
interpretive judgement is explicitly out of scope and is left to the
AI consumer that reads this output.

This document is the prose annotation layer over that JSON contract.
The harvester's `ir/` directory tree is the contract surface; the
schema docs at [`docs/schema/v0-vs-tiers.md`](../schema/v0-vs-tiers.md)
and [`docs/schema/v1-adr.md`](../schema/v1-adr.md) define the
field-by-field shape; this guide explains the *conventions* a consumer
needs to read those fields correctly — what `null` means versus what
omission means, how to traverse from a form to its row source's
underlying table, how to interpret the `UnresolvedItems` array, and
what the harvester does and does not promise.

If you are a downstream AI consumer reading a harvester run output
for the first time, start at §3 (Reading order) and let the typed
links in §4–§5 carry you to the per-category detail you need.

---

## 2. What the harvester produces (output map)

A complete harvester run writes four kinds of artefact into the
output directory:

- **`raw/`** — Access-native artefacts the harvester captured before
  any enrichment: `SaveAsText` output for forms, reports, modules, and
  macros, plus DAO JSON dumps for tables, queries, and relationships.
  Useful for debugging the harvester itself; **not** intended for
  downstream interpretation.
- **`kb/`** — clean parsed JSON, one file per Access object, organised
  by category (`kb/Forms/<name>.kb.json`,
  `kb/Queries/<name>.kb.json`, etc.). This is the parsed-but-not-yet-
  enriched view: the form's section tree is a structured `AccessNode`
  rather than text; queries and tables pass through largely as-is.
- **`ir/`** — the consumer-facing tier. `kb/` plus mechanical
  enrichments: geometry decoded to inches, labels resolved to attached-
  label text where Access wrote one, property values decoded from the
  property-sheet wire format, VBA procedure signatures lifted out of
  module text, embedded macro actions correlated with their host
  controls. Per-category file layout mirrors `kb/` — for every
  `kb/Forms/<name>.kb.json` there is a corresponding
  `ir/Forms/<name>.ir.json`. Consult `kb/` only when an `ir/` field's
  provenance is in question; the enrichment is additive, so a missing
  field in `ir/` is also missing in `kb/`.
- **Top-level envelope files.** Alongside the tier directories, every
  run produces a `manifest.json` (the inventory and the cross-cutting
  enrichment arrays — see §4) and an `unresolved-items.json` (the
  flat list of references the resolver could not fully bind — see §6).
  Both filenames are stable: a consumer can rely on
  `<output-dir>/manifest.json` and `<output-dir>/unresolved-items.json`
  as the canonical entry points without scanning the directory.

The **canonical golden corpus** at
[`samples/GoldenOutput/<fixture>/`](../../samples/GoldenOutput/)
shows what a complete run looks like for the two fixtures the
harvester tests against:
[`HomeHealthCare`](../../samples/GoldenOutput/HomeHealthCare/) (the
primary fixture; unbound forms, ADODB, validation-only data macros,
Hungarian naming) and
[`NorthwindDevEdition`](../../samples/GoldenOutput/NorthwindDevEdition/)
(secondary; bound split forms, subforms, audit data macros, AutoExec
startup). `raw/` is deliberately excluded from the golden corpus per
the decisions in
[`harvester-finish-canonical-golden-output`](../session-sets/harvester-finish-canonical-golden-output/change-log.md);
the golden corpus carries `kb/`, `ir/`, `manifest.json`, and
`unresolved-items.json`.

---

## 3. Reading order: where to start

A consumer that walks the output top-down sees an inventory before
any detail:

1. Open `manifest.json` for the per-category counts and the
   envelope-level cross-cutting arrays (`RowSourceSpecs`,
   `FunctionClassifications`, `EventHandlerClassifications`,
   `ValidationRules`, `UnresolvedItems`, `Diagnostics`).
2. For each form or report of interest, open the corresponding
   `ir/Forms/<name>.ir.json` or `ir/Reports/<name>.ir.json` for the
   structural detail the manifest only summarises.
3. For queries referenced by those forms or reports — typically
   surfaced as control `RowSource` values or as a form's
   `RecordSource` — open `ir/Queries/<name>.ir.json` for the SQL,
   then consult the manifest's `RowSourceSpecs` array for dependency
   facts (which tables and other queries the query reads). The
   per-query `ir/` file carries the SQL string; the manifest's
   `RowSourceSpecs` carries the structured dependency record. See §5.3
   for the joining shape.
4. For modules invoked from form / report events, open
   `ir/ModulesAndClasses/<name>.ir.json` for the procedures' source
   and the per-procedure metadata, and consult the manifest's
   `FunctionClassifications` and `EventHandlerClassifications` for
   the caller / callee graph and the side-effect classification a
   migration agent reads to pick a translation strategy. See §5.4 for
   the joining shape.
5. After interpreting the records, consult `unresolved-items.json`
   for anything the harvester flagged as unresolvable. The full
   per-reason narrative at
   [`docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`](../session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md)
   is the authority for what each reason code means; §6 summarises
   the shape and links into it.

**Provenance fallback.** If an `ir/` field's value surprises the
consumer — a label resolved differently than expected, a property
that looks decoded incorrectly — open the matching `kb/` file. The
`kb/` tier mirrors the `ir/` per-category subdirectory structure
(`kb/Forms/<name>.kb.json` corresponds to `ir/Forms/<name>.ir.json`)
and carries the same record minus the enrichment-added fields
(`GeometryIn`, `NonDefaultProperties`, `Procedures`,
`AttachedLabel`-style resolved labels). Diffing the `ir/` value
against `kb/` shows the consumer whether the field came from Access
or from the enricher.

**The dependency DAG.** Top-down traversal is the typical read path
because the inter-object reference graph is a DAG rooted at the
forms and reports the user opens at runtime:

```
Form → Query (via RowSource / RecordSource) → Table
Form → Module (via VBA event binding) → other Module / built-in
Report → Query → Table
Report → Group-level binding → Field
Module → other Module (via Call edge)
```

Walking from an entry-point form to the tables it ultimately reads
is the canonical "what does this UI surface depend on" answer; the
manifest's `RowSourceSpecs` (for combo / list controls) and the
per-query `ir/` file (for the form's main `RecordSource`) carry the
edges.

---

## 4. The DatabaseManifest envelope

`manifest.json` is a single JSON object. Its top-level keys, in the
on-disk order the writer emits, are:

- **`SchemaType`** — always `"DatabaseManifest"`. A consumer that
  loads JSON without a path may switch on this to identify the file.
- **`SchemaVersion`** — currently `"1.0"`. A consumer should treat
  this as load-time gating: `"1.0"` and any later `"1.x"` are
  expected to read with the same field set.
- **`HarvesterVersion`** — semantic version of the harvester binary
  that produced the run. Normalised to a sentinel in the golden
  corpus so test fixtures stay diff-clean across releases.
- **`Database`** — `{ Name, FilePath, ExportedAt }`. The run's source
  `.accdb` and the harvest timestamp.
- **`Summary`** — per-category counts: `FormCount`, `ReportCount`,
  `QueryCount`, `ModuleCount`, `TableCount`, and
  `UnresolvedItemCount`. Use these to size the read job before
  walking the arrays.
- **`Forms`, `Reports`, `Queries`, `Modules`, `Tables`,
  `Relationships`** — per-category stub entries. Each entry is a
  small `{ Name, ... }` record naming the object; the full
  structural detail lives in the corresponding
  `ir/<Category>/<Name>.ir.json` file.
- **`ValidationRules`** — flat consumer-friendly index over both
  table-level and field-level validation rules from across the
  database. Pre-flattened so a consumer that wants "all rules" does
  not have to walk every table.
- **`RowSourceSpecs`** — per-combo / per-list-control row-source
  records (one entry per combo box or list box on any form). The
  shape and joining keys are documented in §5.3.
- **`FunctionClassifications`** — per-procedure side-effect
  classifications across all modules (Pure, DataAccess, UIBound,
  ExternalIO, LateBound). Shape in §5.4.
- **`EventHandlerClassifications`** — per-event-handler effect-class
  classifications across all forms and reports. Shape in §5.4.
- **`UnresolvedItems`** — the flat list of references the resolver
  could not fully bind. See §6 for the shape and the per-reason
  narrative pointer.
- **`Diagnostics`** — harvester run diagnostics. Each entry carries
  a severity (`Info` / `Warning` / `Error`), a source component
  (e.g., the extractor that emitted it), a location (file path,
  object name, or line), and a message string. Recommended
  downstream-AI action class: surface `Error`-severity entries to
  the human before consuming the affected object's `ir/` record;
  treat `Warning` and `Info` as advisory unless the consumer is
  doing a strict-mode review.

A run on an empty fixture looks like this (snipped from
[`samples/GoldenOutput/HomeHealthCare/manifest.json`](../../samples/GoldenOutput/HomeHealthCare/manifest.json)
with normalisation sentinels for the volatile fields):

```json
{
  "SchemaType": "DatabaseManifest",
  "SchemaVersion": "1.0",
  "HarvesterVersion": "<NORMALIZED:HarvesterVersion>",
  "Database": {
    "Name": "HomeHealthCare.accdb",
    "FilePath": "<NORMALIZED:Database.FilePath>",
    "ExportedAt": "<NORMALIZED:Database.ExportedAt>"
  },
  "Summary": { "FormCount": 0, "ReportCount": 0, "QueryCount": 0,
               "ModuleCount": 0, "TableCount": 0, "UnresolvedItemCount": 0 },
  "Forms": [], "Reports": [], "Queries": [], "Modules": [],
  "Tables": [], "Relationships": [],
  "ValidationRules": [], "RowSourceSpecs": [],
  "FunctionClassifications": [], "EventHandlerClassifications": [],
  "UnresolvedItems": [], "Diagnostics": []
}
```

A populated run has the arrays filled in; the per-category stubs
keep the consumer's iteration shape constant across empty and
populated cases.

### Required record snippets

The audience-check probe identified three manifest-level arrays
whose record shape a consumer must see to make use of the array.
The snippets below illustrate the shape; the C# record definitions
in
[`AccessHarvester.Pipeline/Enrichment/`](../../AccessHarvester.Pipeline/Enrichment/)
are the authoritative source.

**`RowSourceSpec`** (one record per combo / list control;
documented in `RowSourceSpec.cs`):

```json
{
  "SourceFormName": "frmCaseManagement",
  "ControlName": "cboCaseworker",
  "ControlType": "ComboBox",
  "Kind": "TableOrQueryNamed",
  "RowSourceText": "qryCaseworkerList",
  "RowSourceType": "Table/Query",
  "TargetName": "qryCaseworkerList",
  "InlineSql": null,
  "ValueListItems": null,
  "FieldListSourceTable": null,
  "BoundColumn": 1,
  "ColumnCount": 2,
  "Columns": [
    { "Index": 1, "WidthTwips": 0,    "WidthInches": 0.0, "Hidden": true  },
    { "Index": 2, "WidthTwips": 2160, "WidthInches": 1.5, "Hidden": false }
  ],
  "LimitToList": true,
  "CascadingDrivers": null,
  "QueryAbstractionRef": null,
  "Purpose": null,
  "Notes": null
}
```

The joining key is `(SourceFormName, ControlName)` — match these
against the corresponding `ir/Forms/<name>.ir.json` file (by
`SourceFormName`) and against a control's `Name` inside that file's
`Children` tree. `Kind` is the discriminator: `TableOrQueryNamed`
puts the target query / table name in `TargetName`,
`TableOrQueryInlineSql` puts the verbatim SQL in `InlineSql`,
`ValueList` populates `ValueListItems`, `FieldList` populates
`FieldListSourceTable`, `Empty` means the row source is filled at
runtime by VBA (look at `CascadingDrivers` for the driver
controls), `Unknown` means the row source could not be classified.

**`FunctionClassification`** (one record per user-defined procedure;
documented in `FunctionClassification.cs`):

```json
{
  "ModuleName": "Module_PaymentLogic",
  "ProcedureName": "ApplyPayment",
  "Kind": "Function",
  "IsPublic": true,
  "IsStatic": false,
  "ParameterCount": 3,
  "HasReturnValue": true,
  "Categories": ["DataAccess"],
  "PrimaryCategory": "DataAccess",
  "IsMixed": false,
  "Metrics": { "CyclomaticComplexity": 5, "LinesOfCode": 32, "StatementCount": 21 },
  "DirectDependencies": [
    { "ModuleName": "Module_AuditTrail", "ProcedureName": "LogChange", "Kind": "Sub" }
  ],
  "TransitiveDependencies": [
    { "ModuleName": "Module_AuditTrail", "ProcedureName": "LogChange",       "Kind": "Sub" },
    { "ModuleName": "Module_AuditTrail", "ProcedureName": "FormatTimestamp", "Kind": "Function" }
  ],
  "Callers": [
    { "ModuleName": "Form_frmInvoice", "ProcedureName": "btnPay_Click", "Kind": "Sub" }
  ],
  "UnresolvedReferences": null
}
```

The joining key is `(ModuleName, ProcedureName)` — match these
against the per-module `ir/ModulesAndClasses/<ModuleName>.ir.json`
file's `Procedures` list. `PrimaryCategory` is the single-bucket
answer for consumers that need one (precedence: `UIBound` >
`DataAccess` > `ExternalIO` > `LateBound` > `Pure`); `Categories`
is the full set the body exercises. `IsMixed=true` plus a
`LateBound` category in `Categories` is the signal that the static
classification is incomplete and the consumer should examine
`UnresolvedReferences` (member-access chains on Object-typed locals,
typically). The dependency edges are deduplicated and sorted
case-insensitively for stable diffing across runs.

**`EventHandlerClassification`** (one record per form / report event
handler; documented in `EventHandlerClassification.cs`):

```json
{
  "ProcedureName": "cboCaseworker_AfterUpdate",
  "Categories": ["CascadingUpdate", "FilterSearch"],
  "Mixed": true,
  "Signals": [
    {
      "Category": "CascadingUpdate",
      "Kind": "ControlAssignment",
      "Line": 47,
      "Column": 4,
      "TargetExpression": "Me!cboClient.RowSource"
    },
    {
      "Category": "FilterSearch",
      "Kind": "FormRequeryCall",
      "Line": 49,
      "Column": 4,
      "TargetExpression": "Me.Requery"
    }
  ]
}
```

The joining key is `ProcedureName` plus the host form / report
identified by source-name convention: handlers are named
`<control>_<event>` for control handlers (the `<control>` segment
is the form-control name) or `Form_<event>` / `Report_<event>` for
form-level / report-level handlers. To locate the host form, scan
the per-form `ir/Forms/<name>.ir.json` files for a control whose
`Name` matches the `<control>` segment; for `Form_*` /
`Report_*` handlers, the host is named by the procedure's containing
module (`Form_frmCaseManagement` and so on). The `Signals` array is
the per-pattern evidence trail — `Categories` deduplicates and
orders them; `Mixed` is the convenience flag for "this handler does
more than one thing."

---

## 5. Per-category interpretation

Each subsection covers one category of `ir/` record. The prose is
short — the JSON shape is mostly self-documenting, and the schema
docs at [`docs/schema/v0-vs-tiers.md`](../schema/v0-vs-tiers.md)
carry the field-by-field detail. What follows is the conventions a
consumer needs that the schema does not state outright.

### 5.1 Forms (`ir/Forms/*.ir.json`)

A form record has the shape `{ Type: "Form", Properties: {...},
Children: [...], VbaCode: "...", ... }`. The interesting bits:

- **`Properties`** is the verbatim Access property-sheet bag — strings
  exactly as Access stored them. Decoded numeric units land in
  separate sibling fields rather than overwriting `Properties`; for
  example, a form's `Properties.Width` is the raw twip count, while
  `GeometryIn` (when present) holds the decoded `{ X, Y, W, H }` in
  inches. The convention scales to every Access dimension property.
  Some properties carry hex-encoded binary blobs (`GUID`, `NameMap`,
  `PrtDevMode`, `PrtDevNames`, `PrtMip`) — printer settings, internal
  name maps, GUIDs, and similar Access-private formats. Unless the
  consumer specifically needs these (e.g., a migration tool preserving
  print-driver settings), they can be safely ignored.
- **`Children`** is the form's section / control tree, recursive.
  Each child has the same `{ Type, Properties, Children, ... }`
  shape; section nodes (`Detail`, `FormHeader`, `FormFooter`) wrap
  controls; controls' `Children` are typically empty.
- **`VbaCode`** is the form's class-module source as a string, with
  Windows line endings (`\r\n`) preserved as Access stored them. For
  form and report class modules, the string begins with a non-VBA
  `CodeBehindForm` header line (added by Access's `SaveAsText`
  export), followed by the standard `Attribute VB_*` headers, then
  the actual code. A consumer feeding the body to a VBA parser should
  strip the `CodeBehindForm` line first. A consumer should prefer the
  structured `Procedures` list (below) over re-parsing `VbaCode`.
- **`Procedures`** is the enricher-extracted procedure list: one
  entry per `Sub`, `Function`, or `Property *` declaration in
  `VbaCode`, each carrying `Name`, `ProcedureType`, `AccessModifier`,
  `Parameters`, and `StartLine`. Use this to locate handlers without
  re-parsing.
- **`NonDefaultProperties`** is the enricher's "interesting fields"
  hint: a list of property names that diverge from Access's defaults
  for the control class. Filter the property bag by this list when
  summarising a control to skip boilerplate (every form has
  `Properties.AutoCenter` regardless of whether the user changed it;
  only forms where the user changed it appear in
  `NonDefaultProperties`).
- **Event-binding shape.** A property like
  `"OnLoad": "[Event Procedure]"` is Access's signal that the form
  has a VBA handler bound to that event. The handler's body is in
  `VbaCode`; its signature is in `Procedures`; its effect-class
  classification is in the manifest's
  `EventHandlerClassifications`. Other event property values:
  `=ExpressionFunction()` for an inline expression call, `"Macro
  Name"` for a saved-macro reference, `[Embedded Macro]` for an
  embedded UI macro (see *Embedded-macro correlation* below), or
  the raw macro text inline. **Inline-macro `On...EmMacro`
  pattern.** Access also emits a sibling property whose name is
  the event name suffixed `EmMacro` — `OnLoadEmMacro`,
  `OnClickEmMacro`, etc. — whose value is the raw text dump of the
  embedded macro the host control fires on that event. The text is
  the same payload that appears `_AXL:`-encoded inside the
  `Children` tree; the harvester surfaces it on the host's
  `Properties` bag as a convenience. Consumers should prefer the
  structured `EventMacros` object below over parsing the raw text.
- **`EventMacros`** is the enricher's structured representation of
  every embedded UI macro bound to one of the host's event slots.
  The shape is `{ "<EventName>": { "Actions": [...],
  "InferredFunctionality": ... }, ... }` — keyed by event name
  (`OnLoad`, `OnClick`, `OnCurrent`, etc.). Each action entry
  carries `Action` (the Access macro action name, e.g. `SetValue`,
  `OpenForm`, `MessageBox`), `Arguments` (a name-keyed bag —
  `Item`, `Expression`, `FormName`, etc.), and optional `Condition`
  / `ConditionBranch` / `Comment` / `InferredFunctionality` slots.
  This is the **consumer-friendly** form of an embedded macro;
  prefer it over the lower-level `_AXL:` / `On...EmMacro` raw text.
  The two raw forms remain in the output for debugging and
  round-tripping. **Omission semantics:** `EventMacros` is omitted
  from the record entirely when the host has no embedded UI macros;
  absence means "no embedded macros," not "data not captured."
- **`TextCapacity`** (per-control, enricher-added on text-bearing
  controls) — the estimated display capacity for a text control,
  computed from its geometry and font. Either populated as
  `{ "charsPerLine": <int>, "visibleLines": <int>, "font": { "name":
  "<font>", "size": <int>, "resolution": "inherited" | "fallback" } }`
  (plus possibly a few extra metric fields) or, when the harvester
  cannot compute it, the explicit absence form
  `{ "unavailable": true, "reason": "<short reason>" }` — typical
  reasons are `"no geometry"` (the control has no `Width`/`Height`
  to measure against) or a missing font. A consumer doing layout
  preservation or migration sizing reads the populated form; a
  consumer just needing structure can ignore the field.

**Embedded-macro correlation.** Embedded UI macros — the macros
Access stores inside the form rather than as standalone objects —
appear in three places in the `ir/` record. **Prefer the structured
`EventMacros` object documented above for downstream consumption**;
the raw forms are available for debugging and round-tripping:

1. **`EventMacros`** (consumer-friendly, top-level) — the parsed,
   per-event structured object documented in the bullet above.
2. **`Properties.On<event>EmMacro`** (raw text, host-bag-attached) —
   the verbatim text dump on the host's `Properties` bag.
3. **`_AXL:` comments inside the `Children` tree** (raw XML
   fragments) — the legacy in-tree form preserved for the
   round-trip case described below.

The `_AXL:` reassembly recipe (only needed when the consumer is
specifically working with the in-tree XML form): walk the `Children`
tree, collect every `Begin` node whose `Comment` string starts with
`_AXL:`, and concatenate the comment payloads in source order. The
result is a fragment of XML in the
`http://schemas.microsoft.com/office/accessservices/2009/11/application`
namespace. The shape is roughly:

```xml
<UserInterfaceMacro xmlns="...accessservices/2009/11/application">
  <Statements>
    <ConditionalBlock>
      <If Expression="[Status] = &quot;Open&quot;">
        <Statements>
          <Action Name="SetProperty">
            <Argument Name="ControlName">txtAmount</Argument>
            <Argument Name="Property">Enabled</Argument>
            <Argument Name="Value">True</Argument>
          </Action>
        </Statements>
      </If>
    </ConditionalBlock>
    <Action Name="OpenForm">
      <Argument Name="FormName">frmDetail</Argument>
      <Argument Name="View">Form</Argument>
    </Action>
  </Statements>
</UserInterfaceMacro>
```

The element-to-Access-action mapping is direct: `<Action Name="X">`
corresponds to the Access macro action named `X` in the Access UI
(`OpenForm`, `SetProperty`, `MessageBox`, `RunMacro`, `RunCode`,
`StopMacro`, and so on). Action arguments appear as `<Argument
Name="...">value</Argument>` children. Control-flow constructs
appear as `<ConditionalBlock>` (with nested `<If>` / `<ElseIf>` /
`<Else>` branches) and `<SubMacro Name="...">` blocks. A consumer
that wants the action list without semantics can read every
`<Action Name>` value as the macro's verb sequence; a consumer that
wants the full semantics walks the structured tree.

### 5.2 Reports (`ir/Reports/*.ir.json`)

A report record has the same overall shape as a form (`Type:
"Report"`, `Properties`, `Children`, `VbaCode`, `Procedures`,
`NonDefaultProperties`) with one structural addition: reports have
**group levels** in the section tree. The `Children` array contains
section nodes named `GroupLevel0`, `GroupLevel1`, ... up to four
levels, each with `GroupHeader` / `GroupFooter` sub-sections plus
the level's sort fields, break-on conditions, and grouping
expression. The report's `RowSource` / `RecordSource` is the source
of the row data; report-bound queries surface in the manifest's
`RowSourceSpecs` like form-bound queries do.

### 5.3 Queries (`ir/Queries/*.ir.json`)

A query record carries:

- **`Sql`** — the verbatim Jet SQL as Access stored it. Whitespace
  and case are preserved.
- **`QueryTypeCode`** / **`QueryTypeName`** — discriminates Select,
  Make-Table, Update, Append, Delete, Crosstab, Pass-through, Union,
  and DDL. Switch on `QueryTypeName` rather than `QueryTypeCode` for
  readability.
- **`Connect`** — non-empty for pass-through queries; the ODBC /
  OLE-DB connection string Access stored. For non-pass-through
  queries this is `null`.
- **Parameters** — declared `PARAMETERS` clause entries, decoded
  into `{ Name, Type }` records. Inline parameter references in the
  SQL body are left in the SQL string itself.

Dependency facts — which tables and which other queries this query
reads — do **not** live on the per-query `ir/` file. They surface
in the manifest's `RowSourceSpecs` array (the same array that
serves form combo / list controls; queries that are the target of
a control's `RowSource` show up here with the control as the
"source"). For queries that are not referenced by any control —
free-standing queries called by VBA, for example — the dependency
graph is reachable through the SQL itself: parse it against the
Jet grammar (the parser at
[`AccessHarvester.Sql.Parser/`](../../AccessHarvester.Sql.Parser/))
or use the table-name mentions in the SQL text as a heuristic.

The `RowSourceSpec` snippet in §4 illustrates the joining record
shape; the recipe for following a form → query → table chain is:

1. Open `ir/Forms/<formName>.ir.json`. Note the control names of
   interest (`cboCaseworker` in the example).
2. Scan `manifest.RowSourceSpecs` for an entry with
   `SourceFormName == "<formName>"` and `ControlName ==
   "cboCaseworker"`. Read `Kind`:
   - `TableOrQueryNamed` → `TargetName` is a saved-query or table
     name. Open `ir/Queries/<TargetName>.ir.json` (or
     `ir/Tables/<TargetName>.ir.json`).
   - `TableOrQueryInlineSql` → `InlineSql` is the SQL the control
     uses; there is no per-`ir/` file for it (the SQL lives only
     inside the control's row source).
   - `ValueList` / `FieldList` / `Empty` / `Unknown` → no query
     dependency to follow.
3. From the query, repeat for `RecordSource`-style dependencies on
   the form itself if the form is bound (`Properties.RecordSource`).

### 5.4 Modules and class modules (`ir/ModulesAndClasses/*.ir.json`)

A module record carries:

- **`Name`** and **`ModuleKind`** — the canonical name (matches the
  Access nav-pane name; `Form_<formName>` / `Report_<reportName>`
  for class modules behind forms / reports) and the discriminator
  (Standard, Class, Form, Report).
- **`VbaCode`** — verbatim source.
- **`Procedures`** — the same per-procedure list as on forms: one
  entry per declaration with `Name`, `ProcedureType`,
  `AccessModifier`, `Parameters`, and `StartLine`.
- **`Attributes`** — the `VB_Name` / `VB_GlobalNameSpace` / etc.
  attribute header values stripped from the top of the source.

The enrichment layer's classification arrays in the manifest are
where the structured per-procedure metadata lives.
`FunctionClassifications` carries the side-effect category, the
complexity metrics, and the call-graph edges (direct and transitive
dependencies, callers). `EventHandlerClassifications` carries the
effect-pattern signals for handler procedures. The §4 snippets show
the record shapes; the joining keys are documented above.

A consumer reconstructing the module call graph reads
`manifest.FunctionClassifications`, indexes by `(ModuleName,
ProcedureName)`, and follows each entry's `DirectDependencies` /
`Callers` arrays. `TransitiveDependencies` is the BFS closure
pre-computed by the enricher; use it when the consumer wants the
reachability set without re-walking the graph. Entries flagged
`IsMixed=true` with a `LateBound` category mean the static
classification is incomplete — examine the entry's
`UnresolvedReferences` array for the member-access chains the
resolver could not bind, and treat them as "may also exercise other
categories" rather than "exhaustively classified."

### 5.5 Tables and Relationships

Tables (`ir/Tables/*.ir.json`) carry column definitions
(`Name`, `Type`, `Size`, `Nullable`, `DefaultValue`,
`ValidationRule`, `ValidationText`), index definitions
(`Name`, `Fields`, `Unique`, `Primary`, `Foreign`), and
table-level `ValidationRule` / `ValidationText`. Relationships
(`ir/Relationships/*.ir.json`) carry the cross-table edges, each
with the two table names, the join field pairs, the cascade rules
(`UpdateCascade`, `DeleteCascade`), and the relationship
`Attributes` flags. Cardinality (one-to-one versus one-to-many) is
encoded via the relationship's index attributes — a relationship
whose foreign side has a `Unique` index is one-to-one; one-to-many
otherwise.

The manifest's `ValidationRules` array is the flat consumer-friendly
index over both table-level and field-level rules — when a consumer
wants "all validation rules in the database" the manifest array is
the answer; the per-table `ir/` file is the answer when the consumer
wants "all validation rules on this specific table."

### 5.6 Macros — standalone and embedded

Standalone macros (objects in the Access nav pane) live in
`ir/Macros/<name>.ir.json` with `{ Type: "Macro", Properties,
Children, ... }`. The macro's actions are recoverable from the
`Children` tree; the same `<Action Name="...">` convention from
§5.1 applies. Saved data macros (the validation macros attached to
tables) are emitted as separate macros under
`ir/DataMacros/<tableName>.<event>.ir.json`.

Embedded UI macros — the per-control event macros stored inside a
form or report rather than as standalone objects — surface in three
forms on the host record: the structured `EventMacros` object
(preferred for consumers), the `Properties.On<event>EmMacro` raw
text dump, and `_AXL:` comment payloads in the `Children` tree.
See §5.1 for the full description of all three forms and the
recommended consumption order, plus the `<UserInterfaceMacro>` XML
shape sketch that applies if a consumer is working with the raw
`_AXL:` form. The shape is identical for embedded macros regardless
of whether the host is a form or a report. The harvester does
**not** lift embedded macros into the standalone macros directory;
preserving the binding to the host control is the higher-value
property.

---

## 6. The `UnresolvedItems` array

`unresolved-items.json` is a flat list of `UnresolvedItem` records,
one per reference the harvester could not fully bind. The record
shape:

```json
{
  "Source": "Resolver",
  "Reason": "member-access-on-unresolved-target",
  "Severity": "Warning",
  "Location": {
    "ModuleName": "Form_frmCaseManagement",
    "ProcedureName": "cboClient_AfterUpdate",
    "Line": 23,
    "Column": 14
  },
  "SurroundingText": "    Set rs = db.OpenRecordset(strSql)",
  "SuggestedNextSteps": [
    "Resolve the underlying target reference first; the member access cannot be bound until then."
  ]
}
```

Fields:

- **`Source`** — which producer surfaced the entry. One of
  `Resolver`, `SqlFormReference`, `DynamicSqlSite`,
  `NavigationTarget`, `FunctionClassification`.
- **`Reason`** — a stable string from the producer's closed reason
  set. The full set of reason codes and their meanings is at
  [`docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`](../session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md).
  The narrative is the authoritative source — this guide summarises
  the shape only.
- **`Severity`** — `Info`, `Warning`, or `Error`.
- **`Location`** — module / procedure / line / column where the
  unresolved reference appears. For SQL-sourced entries this is the
  query file path; for navigation-target entries it is the host
  form / control.
- **`SurroundingText`** — a short string of source context (the line
  of VBA or the SQL fragment) to ease human review.
- **`SuggestedNextSteps`** — verbatim from
  `UnresolvedItemAggregator.SuggestedNextStepsLookup`. For stable
  reasons these are the per-reason recommendation strings; for the
  one dynamic-prefix family
  (`member-not-found-at-segment-<N>`), the next-steps fall back to
  the generic list.

**Reason-class summary.** The full per-reason narrative is the
authority — read it before acting on an unresolved entry. The
classes at the high level:

| Source class | Common reasons | Downstream-AI handling |
|---|---|---|
| `Resolver` | `not-in-scope`, `me-in-standard-module`, `default-member-not-yet-resolved`, `with-block-flattening-deferred`, `member-access-on-unresolved-target` | Name resolution gaps. Check the builtin registry before treating as dead code; cascading entries usually disappear when the upstream reference is fixed. |
| `SqlFormReference` | `bare-collection-reference`, `form-not-in-metadata`, `report-not-in-metadata`, `control-not-on-form`, `control-not-on-report`, `host-collection-not-registered`, `member-not-found-at-segment-<N>` (dynamic) | SQL-side `[Forms]![...]!...` chains. Typo-vs-dynamic-runtime-form is the most common ambiguity; check the migration project's dynamic-form registry before flagging. |
| `DynamicSqlSite` | concatenated-SQL build sites | The harvester cannot statically resolve string-built SQL. Surface the call site for human review or downstream symbolic execution. |
| `NavigationTarget` | unresolved hyperlink / `DoCmd.OpenForm` / `DoCmd.OpenReport` targets | The target name could not be resolved against the harvested metadata. Typically a dynamic name or a typo. |
| `FunctionClassification` | late-bound member chains the classifier could not statically bucket | Records carry a `LateBound` category. Consumer reads `UnresolvedReferences` on the matching `FunctionClassification` for the per-site detail. |

---

## 7. Provenance conventions

Some fields look like `{ source: "...", ... }` rather than carrying a
bare value. The shape signals that the harvester *looked* for the
value and recorded what it found — including the case where it
found nothing.

**Field-name casing on these provenance objects is lower-case
(`source`, `text`, `kind`) — *not* PascalCase.** The harvester's
top-level record-shape fields use PascalCase (`Properties`,
`Children`, `VbaCode`) but the small provenance wrappers documented
in this section use lower-case keys verbatim. Consumers should
match on the lower-case spelling.

```json
"Label": { "source": "attached", "text": "Customer Name" }
"Label": { "source": "unresolved" }
"Binding": { "kind": "Unbound" }
```

A consumer reading `Label.source` knows whether the label came from
an attached `Label` control (`attached`), from the control's caption
(`caption`), from the control's `Name` as a fallback
(`nameFallback`), or whether the label could not be resolved at all
(`unresolved`). Bare missing keys are reserved for "this property is
at its Access default"; an explicit `{ source: "unresolved" }` is
"the harvester looked here and found nothing the consumer should
trust."

### Null versus omission

A second convention governs the rest of the per-record bag:

- **Field omitted entirely from the JSON.** Access used its default
  for that property and the harvester did not need to record it. A
  consumer that wants the default value should consult
  [`samples/GoldenOutput/<fixture>/ir/Defaults.json`](../../samples/GoldenOutput/HomeHealthCare/ir/Defaults.json),
  the per-fixture defaults table — the harvester captures the
  per-Access-version defaults at harvest time so a consumer can
  reconstruct complete property state without depending on a live
  Access install.
- **Field present and `null`.** The harvester looked at this slot
  and Access did not provide a value. A consumer should read `null`
  as "explicitly absent" — meaningful — rather than as "missing data
  point the harvester forgot to check."

The distinction matters for downstream interpretation. An omitted
`AutoCenter` is "Access default behaviour" (whatever that default
is for the property); a `null` `Caption` on a control is "the
designer explicitly cleared the caption." A migration agent should
treat the two cases differently.

### Why some arrays are sorted

Several arrays are emitted with a stable case-insensitive sort —
`FunctionClassification.DirectDependencies`,
`TransitiveDependencies`, `Callers` on the same record, the per-
module `Procedures` list (in source order — not sorted, since
source order is the natural traversal), the manifest's per-category
stub lists. The stable ordering is the harvester's
diff-cleanliness commitment: re-running on the same input produces
byte-identical output, which keeps downstream AI consumers' diff
tooling honest.

---

## 8. Known limitations

What the harvester does **not** do, drawn from
[`docs/planning/strategy.md`](../planning/strategy.md) → *Frozen
Unless Reauthorized (2026-05-17)*:

- **No AI interpretation.** Fields like `Purpose` and `Notes` on
  enrichment records are reserved for the downstream AI consumer to
  fill. They are always `null` at the `ir/` tier.
- **No production IR codegen.** The probe scope under
  [`tools/AccessHarvester.IrCodegen/`](../../tools/AccessHarvester.IrCodegen/)
  is exploratory; production IR-to-Blazor / IR-to-C# codegen is
  deferred to the future migration repo, not this codebase.
- **No Blazor consumer integration.** The pilot Blazor host
  integration with `dabbler-platform` is deferred to the future
  migration repo per the 2026-05-17 strategy reset.
- **No `dabbler-platform-discovery` refresh.** The discovery probe is
  frozen at its current revision pending the migration-repo work.
- **No CRUD scaffolder expansion.** The scaffolder probe under
  `tools/` is frozen at its current scope.
- **No additional Northwind workflows.** The four existing pilot
  packages cover the in-scope workflows; further additions are
  deferred.
- **No symbolic execution of dynamic SQL.** Concatenated-SQL build
  sites surface as `UnresolvedItems[Source=DynamicSqlSite]` entries
  rather than getting resolved.
- **No late-binding type inference.** Member-access chains on
  Object-typed locals surface as `LateBound` classifications and
  `UnresolvedReferences` entries rather than getting resolved.

A consumer should not assume the harvester does any of the above —
the explicit `null` interpretive slots and the `UnresolvedItems`
array are how the harvester says "this is for you to figure out."

---

## 9. Examples gallery

Three small example files are committed under
[`examples/`](../../examples/) for consumers that want a complete
record to inspect without walking a full canonical corpus. Each is a
verbatim copy of a record from the canonical golden corpus described
in §2 — same schema, same conventions, same field names. The file
naming follows the original three-example convention; the content
shape is whatever the harvester actually writes today:

- [`examples/minimal-form.json`](../../examples/minimal-form.json) —
  a single `ir/Forms/*.ir.json` record for a small parameter-prompt
  form (`frmReportParam_ClientFY` from the HomeHealthCare canonical
  corpus). Two combo controls and one button, no VBA procedures, no
  subforms. Use this to confirm the consumer's per-form
  `{ Type, Properties, Children, ... }` walk works end-to-end before
  pointing at a richer record.
- [`examples/complex-form.json`](../../examples/complex-form.json) —
  a single `ir/Forms/*.ir.json` record for a richer detail form
  (`frmServiceDetail` from the HomeHealthCare canonical corpus).
  Demonstrates the `Properties` bag at scale, a deep `Children` tree
  with multiple sections, a `VbaCode` body, and the per-form
  `Procedures` and `NonDefaultProperties` enricher additions. Exhibits
  the structural conventions §5.1 documents on a single self-contained
  record.
- [`examples/northwind-output.json`](../../examples/northwind-output.json) —
  the `manifest.json` envelope from the NorthwindDevEdition canonical
  corpus. Shows every top-level key §4 documents (envelope
  identification, `Database`, `Summary`, the per-category stub arrays,
  and the cross-cutting enrichment arrays) in the order the writer
  emits them. The arrays are empty in this snapshot because the
  committed Northwind golden corpus normalises per-record content out
  of the manifest for diff-cleanliness — the per-record detail lives
  in `samples/GoldenOutput/NorthwindDevEdition/ir/<Category>/` (see
  below). The file is small by design; a consumer looking for a
  populated envelope should read a populated `manifest.json` from a
  live harvester run.

The example files are deliberately single-record (or single-envelope)
slices. They are not a substitute for the full canonical corpus —
they exist to let a consumer eyeball the per-record shape without
opening a multi-file tree. For the complete multi-file output the
harvester actually writes, including populated `ir/Forms/`,
`ir/Queries/`, `ir/ModulesAndClasses/`, `ir/Tables/`,
`ir/Relationships/`, `ir/Reports/`, `ir/Macros/`, `kb/`,
`unresolved-items.json`, and the per-fixture `Defaults.json`, see
the canonical corpora:

- [`samples/GoldenOutput/HomeHealthCare/`](../../samples/GoldenOutput/HomeHealthCare/) —
  primary fixture: unbound forms, ADODB, validation-only data macros,
  Hungarian naming.
- [`samples/GoldenOutput/NorthwindDevEdition/`](../../samples/GoldenOutput/NorthwindDevEdition/) —
  secondary: bound split forms, subforms, audit data macros, AutoExec
  startup.

The structural comparison at
[`docs/homehealthcare-northwind-comparison.md`](../homehealthcare-northwind-comparison.md)
documents which fixture exhibits which characteristic.

---

## 10. Where to look next

This section is intentionally a short version of the docs-index
pointer page Session 3 produces at
[`docs/README.md`](../README.md). When a consumer reaches the end of
this guide and needs the next stop without leaving the file:

- **Schema details (field-by-field).** [`docs/schema/v0-vs-tiers.md`](../schema/v0-vs-tiers.md)
  documents the v0 schema and the three-tier model; [`docs/schema/v1-adr.md`](../schema/v1-adr.md)
  carries the v1 architecture decision record.
- **IR shape for forms and reports specifically.** [`docs/ir-schema-forms-reports.md`](../ir-schema-forms-reports.md).
- **Unresolved-reason narrative (authoritative).** [`docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`](../session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md).
- **CLI usage and project orientation.** [`README.md`](../../README.md).
- **Parser test cases (canonical input / expected pairs).** [`docs/test-cases/`](../test-cases/).
- **Project guidance (principles + conventions).** [`docs/planning/project-guidance.md`](../planning/project-guidance.md).
- **Workflow doc (how harvester sessions are run).** [`docs/ai-led-session-workflow.md`](../ai-led-session-workflow.md).

The full docs index lives at [`docs/README.md`](../README.md).
