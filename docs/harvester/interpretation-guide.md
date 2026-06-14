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
for the first time, start at §3 (Workflow for AI consumers) — §3.1 is
the end-to-end tool sequence (`run` → `identify-usage` →
`identify-cleanup` → developer review), §3.2 the reading order within a
single run — and let the typed links in §4–§5 carry you to the
per-category detail you need.

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

Downstream subcommands write **outside** this `run` output map. The
feedback chain (`classify` / `sanitize`) writes `classification.json`
and CSV-per-table data; `generate-migration` consumes those plus `ir/`
to write a SQL Server migration (`schema.sql`, `data.sql`,
`migration-manifest.json`) under its own `--out` — see §5.5.

The **canonical golden corpus** at
[`samples/GoldenOutput/<fixture>/`](../../samples/GoldenOutput/)
shows what a complete run looks like for the committed golden fixture
the harvester tests against:
[`HomeHealthCare`](../../samples/GoldenOutput/HomeHealthCare/) (unbound
forms, ADODB, validation-only data macros, Hungarian naming). `raw/`
is deliberately excluded from the golden corpus per
the decisions in
[`harvester-finish-canonical-golden-output`](../session-sets/harvester-finish-canonical-golden-output/change-log.md);
the golden corpus carries `kb/`, `ir/`, `manifest.json`, and
`unresolved-items.json`.

---

## 3. Workflow for AI consumers

This section is the orientation an agent needs before reading any
individual record. §3.1 is the **end-to-end workflow** — the sequence
of harvester subcommands that turns a raw `run` into a classified,
human-reviewable picture of the database, and the one mistake (the
`--in` target) that silently produces empty output. §3.2 is the
**reading order** within a single run's output once you have it. §3.3 is
the **mandatory rule** for explaining the feedback artifacts to the
developer rather than handing over raw JSON.

### 3.1 The canonical consumer workflow: run → feedback tools → developer review

A consuming agent that has only the harvest output in front of it
should follow this sequence rather than re-deriving any of it by hand.
The harvester ships two **feedback tools** — `identify-usage` and
`identify-cleanup` — that already produce the per-object usage
classification (with confidence scores and auto-generated clarifying
questions) and the stale-object flags an agent would otherwise spend a
long time reconstructing from `RecordSource` values and naming
conventions. They were easy to miss in earlier outputs; the
auto-emitted `READ-ME-FIRST.md` now points a cold consumer at this
section, and this is the full workflow it points to.

**1. `run`, then read `manifest.json`.** Start from the per-database
manifest at `<out-root>/AccessMetaData/<dbName>/manifest.json`. Read
its `Summary` block for the per-category counts, use the `FileMap`
globs to locate the per-object `ir/` files, and **verify** that the
counts match what the globs resolve to on disk before trusting the run.
The manifest's full shape is §4; the within-run reading order is §3.2.

**2. `identify-usage` → `object-use.json`.** Run the usage classifier
over that same per-database directory. It classifies every object's
probable usage with a confidence score and writes `object-use.json`
(machine-readable, for the agent) plus `object-use-checklist.json` (one
review item per object, with a question wherever the classifier is
unsure). **Do not re-derive the object inventory by hand** — read
`object-use.json`. (On one real database this tool classified 167
objects and generated 50 clarifying questions; an agent that hand-built
the same inventory did the tool's work over again for nothing.)

**3. `identify-cleanup` → `object-cleanup.json`.** Run the cleanup
identifier over the same directory. It flags objects that look stale —
no activity within a date threshold — and writes `object-cleanup.json`
(machine-readable flags) plus `object-cleanup-checklist.json` (a
keep/remove review item per flagged object). The date signal comes from
Access via COM when available, falling back to the DAO `LastUpdated`
metadata carried in the IR.

**4. Present the `*-checklist.json` files to the developer.** The two
checklists — `object-use-checklist.json` and
`object-cleanup-checklist.json` — are the human-review surface. Present
them to the developer, collect the answers, and treat the annotated
result as the **authoritative statement of intent** for any downstream
migration or modernisation. Do not just hand over a raw JSON file:
§3.3 below is the mandatory instruction for how an agent explains each
artifact and points the developer at the editor that opens it, and
[`reviewing-the-feedback-artifacts.md`](reviewing-the-feedback-artifacts.md)
is the plain-language developer guide you direct them to.

```bash
# The per-database base dir produced by `run` is the dir that CONTAINS
# ir/ — NOT the --out root, and NOT the AccessMetaData/ parent. See the
# trap below.
DB=out/AccessMetaData/HomeHealthCare

AccessHarvester run --db HomeHealthCare.accdb --out out     # harvest -> DB/{manifest.json,ir/,kb/,raw/}
AccessHarvester identify-usage   --in "$DB"                 # -> DB/object-use.json + object-use-checklist.json
AccessHarvester identify-cleanup --in "$DB"                 # -> DB/object-cleanup.json + object-cleanup-checklist.json
```

Both feedback tools also accept `--manifest <DB>/manifest.json` as an
equivalent to `--in <DB>` (the output directory is inferred as the
manifest's parent), and both take `--json-status` for a machine-readable
summary on stdout. `identify-cleanup` additionally accepts
`--harvest-date <YYYY-MM-DD>` (default: today), `--threshold-days <N>`
(default: 90), and `--no-com` (skip COM date sampling, rely on the DAO
`LastUpdated` fallback). `identify-usage` accepts `--exclude-cleanup
<object-cleanup-checklist.json>` so objects the developer has already
confirmed for removal drop out of the usage classification — run
`identify-cleanup` first if you intend to use it.

> **The `--in` target is the per-database base dir, not the `--out`
> root.** `--in` must point at the directory that *directly contains*
> the `ir/` subdirectory — `<out-root>/AccessMetaData/<dbName>/`.
> Pointing it at the `--out` root (the directory that *contains*
> `AccessMetaData/`) or at the `AccessMetaData/` parent makes the loader
> find no `ir/` and classify **zero** objects, with no error raised.
> This is the single most common first-run mistake; a real consumer lost
> a run to it.

### 3.2 Reading order: where to start

A consumer that walks the output top-down sees an inventory before
any detail:

1. Open `manifest.json` for the per-category counts and the
   envelope-level cross-cutting arrays (`RowSourceSpecs`,
   `FunctionClassifications`, `EventHandlerClassifications`,
   `ValidationRules`, `UnresolvedItems`, `Diagnostics`) plus the
   `NavigationGraph` object.
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
5. To map how the application moves between screens — which event
   handler opens which form or report — read
   `manifest.NavigationGraph.Edges` (the VBA-derived form→form /
   form→report dispatches) and `.UnresolvedTargets` (dispatches whose
   target was a non-literal expression). See §5.7 for the consumer
   surface and the top-level facets that are not yet populated.
6. After interpreting the records, consult `unresolved-items.json`
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

### 3.3 Always explain the feedback artifacts to the developer (mandatory)

Whenever you run a feedback tool (`identify-usage`, `identify-cleanup`,
or `classify` / `sanitize`), you **must not** simply hand the developer
a raw JSON file. For each artifact you produce, tell the developer, in
plain language: **(1)** what the file is, **(2)** exactly what they do
with it, and **(3)** the URL or local path of the zero-install editor
that opens it. The full developer-facing reference is
[`reviewing-the-feedback-artifacts.md`](reviewing-the-feedback-artifacts.md);
point them at it and summarise the relevant rows.

The editors and the artifacts they open:

- **`object-use-checklist.json`** and **`object-cleanup-checklist.json`**
  → the **uat-checklist-editor**, hosted zero-install at
  <https://darndestdabbler.github.io/uat-checklist-editor/>. Both files
  share that editor and the same PascalCase
  `{ "ProjectName", "Review": [...] }` format. The developer ticks
  `Passes` / writes `Feedback` per item and saves; the answers travel
  back inside the same file.
- **`classification.json`** (from `classify` / `sanitize`) → the
  **classification-editor**, which is **not hosted** today: the
  developer downloads [`tools/classification-editor.html`](../../tools/classification-editor.html)
  and opens it locally (it runs fully client-side). Do not point them at
  a raw GitHub `.html` URL — it will not render as a running app.
- **`object-use.json`** and **`object-cleanup.json`** are the
  machine-readable findings for *you*, the agent — there is no developer
  editor for them; read them directly and do not ask the developer to.

A copy-paste script you can adapt when you deliver the checklists (fill
in the bracketed counts from the tool's own output):

> I ran the harvester's usage and cleanup classifiers on your database.
> They produced two review files for you — you don't need to read any
> JSON.
>
> 1. **`object-use-checklist.json`** — I classified how each of
>    [N] objects is used. For the [M] where I wasn't sure, I left you a
>    question. Open this file in the checklist editor at
>    <https://darndestdabbler.github.io/uat-checklist-editor/> (click
>    *Open JSON*), answer the questions, tick the ones you confirm, and
>    Save. In Chrome/Edge it saves in place; in Firefox it downloads a
>    copy you send back to me.
> 2. **`object-cleanup-checklist.json`** — [K] objects look possibly
>    unused. Open it in the **same** editor and, for each, confirm keep
>    or remove (tick *Passes* or write a note in *Feedback*), then Save.
>
> Whatever you mark is what I'll treat as the final word on intent.
> Anything left blank I treat as "not reviewed yet."

If you also ran `classify` / `sanitize`, add: *I produced
`classification.json` for column data-sensitivity. Download
`tools/classification-editor.html` from the repo, open it in your
browser, load that file, set the category per column, and Save.*

---

## 4. The DatabaseManifest envelope

`manifest.json` is a single JSON object. Its top-level keys, in the
on-disk order the writer emits, are:

- **`SchemaType`** — always `"DatabaseManifest"`. A consumer that
  loads JSON without a path may switch on this to identify the file.
- **`SchemaVersion`** — currently `"1.1"`. A consumer should treat
  this as load-time gating: `"1.0"` and `"1.1"` (the two values the
  CLI's `validate` accepts) and any later `"1.x"` are expected to read
  with the same field set. The `1.0 → 1.1` bump was additive (the
  `ResolutionReason` sub-document field, 008 Session 3); the
  `NavigationGraph` key documented below rides the same `"1.1"`
  envelope (it sits on an `additionalProperties`-tolerant envelope, so
  its addition did not require an envelope bump).
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
  `Relationships`** — per-category entries. **The entry shape and the
  field that carries the object name differ by category** — there is no
  uniform top-level `Name` on every category:
  - `Forms` and `Reports` entries always carry **`Spec`** and **may
    also carry `ParserOutput`** — present when the form/report has VBA
    code-behind, absent when it does not (so an entry is `{ Spec }` or
    `{ Spec, ParserOutput }` depending on the object; in the
    HomeHealthCare golden 18 of 19 forms and 10 of 12 reports carry
    `ParserOutput`). The object name is **`Spec.SourceFormName`**
    (reports reuse that same key, not a `Name` field), and
    `Spec.SuggestedComponentName` is the migration-friendly name.
  - `Modules` entries are `{ ModuleName, ModuleKind, ParserOutput }` —
    the name is **`ModuleName`**.
  - `Queries`, `Tables`, and `Relationships` entries do carry a
    top-level **`Name`**.

  The corresponding `ir/<Category>/<objectName>.ir.json` file (keyed by
  the object name above) always holds the per-object detail; for
  forms/reports/tables the manifest entry also carries substantial
  structural detail inline under `Spec` / the table fields.
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
- **`NavigationGraph`** — the application-wide navigation structure:
  the VBA-derived form→form / form→report edges, the non-literal
  dispatch targets that could not be resolved, and the (currently
  empty) top-level entry-point / ribbon / switchboard / macro facets.
  A single object, not an array. Emitted between
  `EventHandlerClassifications` and `UnresolvedItems`. See §5.7 for the
  full consumer surface and what is and is not populated today.
- **`UnresolvedItems`** — the flat list of references the resolver
  could not fully bind. See §6 for the shape and the per-reason
  narrative pointer.
- **`Diagnostics`** — harvester run diagnostics. Each entry carries
  a `Severity` (`Info` / `Warning` / `Error`), a `Message` string
  (the affected object name and any location detail are embedded in
  the message text), and an optional stable `Code` for filtering
  without parsing the message. The codes the manifest emitter
  produces today:
  - `VBA_PARSE_DEGRADED` — a module/form/report VBA parse succeeded
    but in a degraded mode (parse diagnostics present, or the lossy
    fallback engaged). The message reports `mode=` and the diagnostic
    count. The module still ships a `ParserOutput`.
  - `VBA_PARSE_FAILURE` — a module's VBA parse threw; the module
    ships as a stub `ParserOutput=null` rather than being dropped. A
    parse failure happens before scope-building, so the module
    contributes **no** symbols to cross-module resolution.
  - `VBA_RESOLVE_FAILURE` — a module parsed and built its scope but
    threw during reference resolution; it ships as a degraded
    stub (`ParserOutput=null` for standard modules) **yet its
    already-built public declarations remain visible to sibling
    modules** (they are genuine source-level symbols). Consequence: a
    sibling can carry a `ResolutionStatus: "Resolved"` reference whose
    target module shipped as a `ParserOutput=null` stub. A consumer
    must therefore **null-check a referenced module's `ParserOutput`
    before dereferencing it regardless of the reference's resolved
    status** — a `"Resolved"` reference is not a guarantee the target's
    body is present. (Not observed on the HomeHealthCare corpus — no
    module currently triggers a resolve-time throw — but the contract
    permits it; tracked as DW-12.)
  - `QUERY_SQL_PARSE_FAILURE` — a saved query's SQL failed to parse;
    the query still ships with a Raw-only `Sql.Parsed` (added 008
    Session 3, F-3-6 — previously this downgrade was silent).
  - `QUERY_EXPORT_FAILURE`, `FORM_SPEC_LOAD_FAILURE`,
    `TABLE_IR_LOAD_FAILURE`, `RELATIONSHIP_IR_LOAD_FAILURE` — a
    per-object record could not be loaded/exported; the object is
    skipped and the rest of the manifest is still written.
  Recommended downstream-AI action class: surface `Error`-severity
  entries to the human before consuming the affected object's `ir/`
  record; treat `Warning` and `Info` as advisory unless the consumer
  is doing a strict-mode review. A degraded/failed parse means the
  affected object's `ParserOutput` is reduced or absent — cross-check
  the `Code` against the object you are about to read.

A run on an empty fixture looks like this (snipped from
[`samples/GoldenOutput/HomeHealthCare/manifest.json`](../../samples/GoldenOutput/HomeHealthCare/manifest.json)
with normalisation sentinels for the volatile fields):

```json
{
  "SchemaType": "DatabaseManifest",
  "SchemaVersion": "1.1",
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
  "NavigationGraph": { "EntryPoints": [], "Edges": [],
                       "UnresolvedTargets": [], "Macros": [] },
  "UnresolvedItems": [], "Diagnostics": []
}
```

A populated run has the arrays filled in; the per-category entries
keep the consumer's iteration shape constant across empty and
populated cases.

> **Database name-form across surfaces (join carefully).** The
> database name is **not** spelled identically on every output surface,
> so a consumer joining records across files by database name must
> normalise first:
>
> | Surface | Field | Name-form | Example |
> |---|---|---|---|
> | `manifest.json` | `Database.Name` | **suffixed** (keeps `.accdb`/`.mdb`) | `HomeHealthCare.accdb` |
> | `object-use.json` / `object-use-checklist.json` | `database` | **stripped** (extension removed) | `HomeHealthCare` |
> | `object-cleanup.json` / `object-cleanup-checklist.json` | `database` | **stripped** | `HomeHealthCare` |
>
> The manifest keeps the on-disk file name (`Path.GetFileName`); the
> usage/cleanup classification surfaces use the extension-stripped form
> (`Path.GetFileNameWithoutExtension`, via the COM session's
> `DatabaseName`). To join usage/cleanup findings to the manifest,
> compare on the **stripped** form (strip the manifest's extension), or
> rely on the per-database output directory — every surface for one
> database lives under the same `<output>/<DatabaseName>/` folder, which
> is the unambiguous join key. (Documented 2026-06-13 per 006 O-1-2; the
> divergence itself is left as-is to avoid churning either contract.)

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

**Report parity baselines (separate from `ir/`).** The `ir/Reports/`
record describes a report; the `report-baseline` subcommand produces a
different artifact — **parity ground truth for verifying a *ported*
report** — under `<out>/AccessMetaData/<dbName>/reports-baseline/`. It is
not part of a normal `run`; capture it explicitly (see
[`install.md`](install.md)). To read it:

- **`baseline-manifest.json`** — start here: `{name, status, paths,
  reason?}` per report. `status` is `Captured`, `Skipped` (e.g.
  `reason: "UnboundReport"`), or `Failed` (e.g. `reason:
  "AggregateFailed: ..."` — usually a report whose query needs a
  parameter that was not supplied). Nothing is silently omitted.
- **`<report>/aggregates.json`** — the numbers. Two complementary
  parity signals live here:
  - **`GrandTotals` / `GroupLevels`** — the `RecordSource` **query
    result-set** aggregates (row count, group subtotals, per-column
    grand totals) from Access's own query engine, *not* the rendered
    footer controls. For a column-level parity check that is the right
    like-for-like comparison (same engine, same data).
  - **`DisplayTotals`** — the **number a human actually reads in the
    report footer**, for the supported closed set of footer aggregate
    expressions. Each entry is `{Control, Section, Expression,
    ComputedValue}`, where `Expression` is the footer text box's raw
    `=Sum(...)` `ControlSource` and `ComputedValue` is that aggregate
    evaluated over the same result set. Only whole-result-set
    **report-footer `Sum`** expressions over numeric result-set columns
    are computed (e.g. `rptInvoice`'s
    `=Sum((1-[Discount])*[Quantity]*[UnitPrice])` → the invoice
    subtotal). Every footer control that is *not* a pure function of
    the flat result set — group-footer aggregates (evaluated per group
    at runtime), `RunningSum` totals, unsupported functions like
    `IIf`, control references like `=[txtSubTotal]+[txtTax]`, bare
    column references — is recorded in `Skipped` with a grounded
    `DisplayTotal*` reason, never silently dropped. This is the parity
    artifact for the displayed total without depending on a rendered
    PDF.
  - `ParametersUsed` records the exact parameter values applied.
- **`reports-baseline-spot-check/<report>/output.pdf`** — present only
  when captured with `--include-pdf` and the report's aggregate capture
  succeeded; written to the SEPARATE, non-gated
  `reports-baseline-spot-check/` sibling (indexed by its own
  `spot-check-manifest.json`), never inside the byte-diffed
  `reports-baseline/` corpus. A non-deterministic binary for *human*
  visual review of layout/format/grouping only — never part of any
  automated gate. A render failure is non-blocking: it is recorded on
  the spot-check manifest and never changes the report's gated capture
  status, so the gated corpus is byte-identical with or without
  `--include-pdf`.

To *understand* a report, read its `ir/Reports/` node; to *check a
migration*, compare against these baselines: assert the ported report's
displayed total equals `DisplayTotals[].ComputedValue` and its
underlying query results match `GrandTotals` / `GroupLevels`. A committed
golden corpus for the canonical report subset is checked in under
`samples/GoldenOutput/<fixture>/reports-baseline/` and byte-diffed by the
`CanonicalGoldenRegressionTests` gate; capture fresh baselines for other
databases on demand (see [`install.md`](install.md)).

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

#### Per-field type semantics

The harvester is **metadata-only**: every column's type, size,
attribute bitmask, validation strings, and slot in the table's
reconstructed DDL are captured; row-level data is **never** harvested.
Attachment, OLE Object, and multi-valued field *child schemas* (the
hidden tables Access uses to store attachment blobs or multi-value
rows) are likewise not expanded — the parent column's type code plus
its `Attributes` bitmask is the full extent of what a consumer sees.

A column's type surfaces on each `Fields[]` entry of
`ir/Tables/<name>.ir.json` as the pair
`("TypeCode": <int>, "TypeName": "<string>")`, plus the raw DAO
`Attributes` bitmask integer and the column's slot in the table's
top-level `Ddl` string. The codes are the DAO `dbType*` constants.

| TypeCode | TypeName | DDL rendering | Notes |
|---|---|---|---|
| 1   | `Boolean`         | `BIT`                                  | Access "Yes/No". |
| 2   | `Byte`            | `BYTE`                                 |  |
| 3   | `Integer`         | `SHORT`                                | 16-bit. |
| 4   | `Long`            | `LONG` or `COUNTER`                    | DDL emits `COUNTER` when the column is an AutoNumber (`IsAutoIncrement: true`); otherwise `LONG`. |
| 5   | `Currency`        | `CURRENCY`                             | Fixed-precision decimal (4 fractional digits). |
| 6   | `Single`          | `SINGLE`                               |  |
| 7   | `Double`          | `DOUBLE`                               |  |
| 8   | `DateTime`        | `DATETIME`                             | Access "Date/Time". |
| 9   | `Binary`          | `BINARY(<size>)` or `BINARY`           | Fixed-width binary; rare in user databases. DDL omits the parenthesised size when `Size` is null. |
| 10  | `Text`            | `TEXT(<size>)` or `TEXT`               | Short text (up to 255 chars). `Size` is the declared maximum length; DDL omits the parens when `Size` is null. |
| 11  | `LongBinary`      | `LONGBINARY`                           | **OLE Object** — binary blob. Metadata only; payload not harvested. |
| 12  | `Memo`            | `LONGTEXT`                             | Access "Long Text". **Hyperlink columns also surface as TypeCode 12** — they are a Memo with the DAO `dbHyperlinkField` flag set in `Attributes`; see *Distinguishing Hyperlink from Memo* below. |
| 15  | `GUID`            | `GUID`                                 | 16-byte UUID. |
| 16  | `BigInt`          | `BIGINT`                               |  |
| 17  | `VarBinary`       | `VARBINARY(<size>)` or `VARBINARY`     | DDL omits the parens when `Size` is null. |
| 18  | `Char`            | `CHAR(<size>)` or `CHAR`               | DDL omits the parens when `Size` is null. |
| 19  | `Numeric`         | `NUMERIC`                              |  |
| 20  | `Decimal`         | `DECIMAL`                              |  |
| 21  | `Float`           | `FLOAT`                                |  |
| 22  | `Time`            | `UNKNOWN(22)`                          | DDL builder has no Jet spelling for this code. |
| 23  | `TimeStamp`       | `UNKNOWN(23)`                          | DDL builder has no Jet spelling for this code. |
| 101 | `AttachmentField` | `UNKNOWN(101)`                         | Access "Attachment" column. The column's presence is captured; the attached blobs themselves live in a hidden child table the harvester does **not** expand. |
| 102 | `ComplexByte`     | `UNKNOWN(102)`                         | Multi-valued Byte. |
| 103 | `ComplexInteger`  | `UNKNOWN(103)`                         | Multi-valued Integer. |
| 104 | `ComplexLong`     | `UNKNOWN(104)`                         | Multi-valued Long. |
| 105 | `ComplexSingle`   | `UNKNOWN(105)`                         | Multi-valued Single. |
| 106 | `ComplexDouble`   | `UNKNOWN(106)`                         | Multi-valued Double. |
| 107 | `ComplexGUID`     | `UNKNOWN(107)`                         | Multi-valued GUID. |
| 108 | `ComplexDecimal`  | `UNKNOWN(108)`                         | Multi-valued Decimal. |
| 109 | `ComplexText`     | `UNKNOWN(109)`                         | Multi-valued Text — the typical Lookup-driven multi-value column shape. |
| any other | `Unknown(<N>)` | `UNKNOWN(<N>)`                         | An unmapped DAO code; surface to a human before consuming. |

The `Ddl` column above shows the JET spelling the harvester's
`TableDdlBuilder` emits into the table's `Ddl` string. The string is
a reconstruction, not a verbatim Access export — DAO does not expose
a `TableDef.DDL` property; the harvester builds it from the `Fields`,
`Indexes`, and relation metadata at extraction time so downstream
consumers can read the schema as SQL without re-deriving it from the
structural collections.

**Distinguishing Hyperlink from Memo.** Access stores Hyperlink
columns as `TypeCode 12` (`Memo` / `LONGTEXT`) and signals
"this is a Hyperlink" via a bit in the per-field `Attributes`
bitmask — the DAO `dbHyperlinkField` flag (`0x00008000`). A consumer
that needs to distinguish Hyperlink columns from plain Long Text
reads `Attributes & 0x8000`; the harvester preserves the raw
`Attributes` integer for exactly this purpose. The wire-format inside
a Hyperlink string column is Access's `<display>#<address>#<sub-address>`
encoding — that decoding is a consumer-side concern, not the
harvester's.

**AutoNumber columns.** AutoNumber surfaces as a regular `Long`
(`TypeCode: 4`) plus `IsAutoIncrement: true` — a typed boolean the
harvester derives from the DAO `dbAutoIncrField` bit in
`Attributes`. The DDL builder reads the same bit and renders the
column as `COUNTER` rather than `LONG`. A consumer can switch on
`IsAutoIncrement` directly without re-decoding the `Attributes` bit.

**Lookup columns.** Access "Lookup" columns are not a separate type
code — they are a regular column (typically a `Long` foreign key or
a `Text` value) plus a `DisplayControl` value of `111` (combo box)
or `110` (list box) plus a `RowSource` entry in the field's raw
property bag. The harvester surfaces `DisplayControl` as a
first-class typed field on `TableField`; the `RowSource` + bound /
display column plumbing remains in the raw property bag (the same
shape forms' combo controls expose via `manifest.RowSourceSpecs`).
A consumer reconstructing a Lookup column's behaviour reads
`DisplayControl` first, then opens the underlying row-source query
or value list to follow the displayed-vs-stored split.

**Multi-valued fields.** Type codes 102–109 cover Access's
multi-valued field family — a column that stores a *set* of values
per row, surfaced in the Access UI as a multi-select checkbox or
combo list. The harvester captures the parent column's type code,
its slot in the table's `Ddl`, and the `Attributes` bitmask (which
carries multi-value flag bits including DAO `dbMultiValueField`
`0x00001000` plus the variable-length flag `dbVariableField`
`0x00000002`; the complementary `dbFixedField` is `0x00000001`)
but does **not** expand the hidden child table that backs the
column. A consumer
that needs the actual member values has to query the live database
directly; the harvester does not exfiltrate row-level data, and the
metadata that would describe the child table's columns is hidden by
Access itself.

**Validation rules and defaults.** All three surface as per-field
strings: `ValidationRule` is a Jet SQL boolean expression evaluated
over the candidate value, `ValidationText` is the user-visible
message Access shows on rejection, and `DefaultValue` is the
literal or `=`-prefixed expression Access uses when the row is
created. The harvester preserves these verbatim — structural
parsing of the embedded expressions is the
[Jet SQL parser](../../AccessHarvester.Sql.Parser/) and
[VBA-expression grammar](../../AccessHarvester.Vba.Grammar/) work,
not the harvester's job. Empty strings (`""`) mean "Access stored
an empty entry" — a meaningful Access-level signal distinct from
`null` (the harvester read no value at all).

**Worked example — an `Attachment` column.** Such a column lands in
its table's `ir/Tables/<Table>.ir.json` as:

```json
{
  "Name": "Attachments",
  "TypeCode": 101,
  "TypeName": "AttachmentField",
  "Size": 4,
  "Required": false,
  "AllowZeroLength": false,
  "DefaultValue": "",
  "ValidationRule": "",
  "ValidationText": "",
  "InputMask": null,
  "Format": null,
  "DisplayControl": 126,
  "Attributes": 1,
  "OrdinalPosition": 9,
  "ForeignName": null
}
```

The same table's top-level `Ddl` string includes
`[Attachments] UNKNOWN(101)` — the DDL builder has no Jet spelling
for code 101 and falls back to the `UNKNOWN(<code>)` form. Both
signals — the typed `TypeName: "AttachmentField"` on the field and
the `UNKNOWN(101)` slot in the table's `Ddl` — tell a consumer the
same thing: this is an Access Attachment column. The harvester
captured its presence; the attached blobs themselves are not in the
output, and the hidden child table Access uses to store them is not
expanded. A migration consumer is expected to plan a per-row
attachment-extraction pass against the live `.accdb` if the
attachments need to be moved, not to look in the harvester's `ir/`
for them.

#### `generate-migration` — the SQL Server migration product

`generate-migration` is the final, runnable product of the classify →
sanitize → **migrate** chain. It is **not** part of `run`: it is a
separate, COM-free subcommand that reads already-harvested artefacts
(`ir/Tables` + `ir/Relationships` for schema, the reviewed
`classification.json` for multi-value / preserve decisions, and the
`sanitize` CSV-per-table output for data) and emits a deterministic
SQL Server migration under `<out>/<database-slug>/`:

- **`schema.sql`** — `CREATE TABLE` (Access→SQL Server type map,
  nullability, defaults, primary keys) followed by `ALTER TABLE … ADD
  CONSTRAINT … FOREIGN KEY`. Identifiers are bracket-quoted; each batch
  ends with `GO`. Autonumber columns become `IDENTITY(1,1)`.
- **`data.sql`** — the data-load, emitted in **FK-safe order** (parents
  before children; a topological sort over the FK graph). A table whose
  identity column is being loaded from the sanitized CSV is wrapped in
  `SET IDENTITY_INSERT [T] ON … OFF` so the preserved keys land verbatim.
  **Run `schema.sql` first, then `data.sql`.**
- **`migration-manifest.json`** — `{ SchemaType: "MigrationManifest",
  Database, GeneratedFiles, TableLoadOrder, DegradationCount,
  Degradations[] }`. This is the honest record of what could **not**
  migrate cleanly.

**Degradation semantics (read the manifest, not just the SQL).** The
generator never silently drops or fabricates. Each `Degradations[]`
entry carries `{ Kind, Table, Column, Reason }`; the kinds a consumer
will see are: `UnmappableColumnType` / `MultiValueColumn` (Attachment /
multi-value columns — omitted from `CREATE TABLE`, recovered per the
Attachments note above), `LinkedTableSkipped` (a linked/attached table
excluded), `UntranslatedDefault` (column kept, its Access default
dropped), `AssumedTypePrecision` (a length/precision the IR did not
carry — tighten before production), `ForeignKeyDropped` (an FK to a
table/column that did not survive), `ForeignKeyCycle` (a set of tables
forming an FK reference cycle — emitted in a deterministic order and
flagged so a reviewer can defer the offending constraints during load),
`MissingDataFile` (no sanitized CSV for a migrated table — `CREATE
TABLE` stands, no rows), and `DataColumnOmitted` (a surviving column not
loaded — binary/OLE that cannot round-trip through text CSV, or a column
the CSV had no data for; SQL Server supplies its default/NULL).

A clean run exits `0`; any flagged degradation exits `3` (review the
manifest). The migration validates **hermetically** — there is no SQL
Server in the harvester's test environment, so correctness is pinned by
a golden-script comparison and dialect well-formedness checks, plus an
optional, non-gated manual run against a real SQL Server. The committed
golden lives at
[`samples/GoldenOutput/HomeHealthCare/migration/`](../../samples/GoldenOutput/HomeHealthCare/migration/).

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

### 5.7 The navigation graph (`manifest.NavigationGraph`)

`manifest.NavigationGraph` is the application-wide map of how the UI
moves between screens. It is a single object (not an array), emitted
in the envelope between `EventHandlerClassifications` and
`UnresolvedItems`. It was wired into the `run` pipeline in set 011
(P3, F-2-2); before that the builder existed but had no production
call site.

**What is populated today (VBA-derived).** The graph is built from the
parsed VBA bodies only — the pipeline calls the builder with no
metadata-extractor inputs (`inputs: null`). So today the graph carries
exactly the two VBA-side facets:

- **`Edges`** — one record per `DoCmd.OpenForm` / `DoCmd.OpenReport` /
  `DoCmd.Close` / `DoCmd.BrowseTo` dispatch in a procedure body whose
  target was a **string literal**. Each edge names who triggers the
  navigation (`SourceModule` + `SourceProcedure` +
  `SourceProcedureKind` + `SourceLine`/`SourceColumn`), the verb
  (`Action`), where it leads (`TargetKind` + `TargetName`), and the
  raw `WhereCondition` / `OpenArgs` source text the dispatch carried
  (each null when the argument was omitted).
- **`UnresolvedTargets`** — one record per recognised dispatch whose
  target was **not** a string literal (a variable, function call, or
  concatenation). Same source provenance as an edge, plus a `Reason`
  (`"non-literal-target"` or `"missing-target"`) and the
  `TargetExpressionText` (the original VBA expression, e.g.
  `"detailFormName"`). These dispatch sites are *also* projected into
  `unresolved-items.json` with `Source: "NavigationTarget"` (§6), so a
  consumer building a manual-review queue can read them from either
  surface.

Example edge and unresolved target from the HomeHealthCare golden
(22 edges — 13 `OpenForm`, 8 `Close`, 1 `OpenReport`; 22 unresolved
targets, all `non-literal-target`):

```json
// NavigationGraph.Edges[*]
{ "SourceModule": "frmClientDetail", "SourceProcedure": "btnPrint_Click",
  "SourceProcedureKind": "Sub", "SourceLine": 349, "SourceColumn": 4,
  "Action": "OpenReport", "TargetKind": "Report",
  "TargetName": "rptClientProfile" }

// NavigationGraph.UnresolvedTargets[*]
{ "SourceModule": "basListHelpers", "SourceProcedure": "OpenDetailFromList",
  "SourceProcedureKind": "Sub", "SourceLine": 128, "SourceColumn": 4,
  "Action": "OpenForm", "Reason": "non-literal-target",
  "TargetExpressionText": "detailFormName" }
```

**Joining edges to the rest of the manifest.** `SourceModule` +
`SourceProcedure` is the same `(ModuleName, ProcedureName)` key used by
`FunctionClassifications` and `EventHandlerClassifications` (§5.4), so a
consumer can pin a navigation edge to its side-effect classification
and its event-handler binding. `TargetName` is the form/report object
name; join it to the manifest's `Forms` / `Reports` entries by
**`Spec.SourceFormName`** (the per-entry name field — reports use that
same `Spec.SourceFormName` key, not a `Name` field), or equivalently to
the `ir/Forms|Reports/<TargetName>.ir.json` filename stem. Do **not**
expect a top-level `Name` on a `Forms` / `Reports` entry — those entries
are `{ Spec, … }` records (see §4). `WhereCondition` / `OpenArgs` are
preserved as raw source text so a migration consumer can pass a
`WhereCondition` through to a parameterised-query translator without
re-stringifying it.

**What is NOT populated today (needs metadata-extractor inputs).** The
top-level, non-VBA facets stay empty because they require inputs the
VBA AST does not carry:

- **`EntryPoints`** — emitted as an empty array `[]`. The ordered
  AutoExec → default-form → switchboard-root root list needs the
  startup options and macro chain.
- **`Macros`** — empty array `[]`. Needs the macro extractor to supply
  `MSysObjects` action sequences (the HomeHealthCare switchboard is
  driven by a harvested macro, but turning the macro IR into
  `NavigationGraphInputs.Macros` is new production code).
- **`Ribbon`** and **`Switchboard`** — **omitted entirely** (null, so
  the omit-null serializer drops the key) rather than emitted as empty
  objects. `Switchboard` needs the `Switchboard Items` table rows,
  which ship `(no data file)` in the harvested output — reading them
  needs a new OleDb/DAO row-read (a new COM surface). `Ribbon` needs
  the `USysRibbons` XML parsed into structure.

This is the **omit-null vs empty-array split** a consumer must code
for: the four list facets (`EntryPoints`, `Edges`,
`UnresolvedTargets`, `Macros`) are **always present** (possibly empty);
the two object facets (`Ribbon`, `Switchboard`) are **absent when
null**. Test for `("Switchboard" in NavigationGraph)`, not for a
non-null value.

**Relationship to the R2-1 / R2-2 consumer requests.** The two
consumer asks that motivated this arc are **not** satisfied by the
graph as it stands today, and that is a recorded, deliberate deferral
— not a gap a consumer should read as "done":

- **R2-1 (switchboard menu tree → structured edges)** needs
  `Switchboard` populated from the switchboard item rows. Those rows
  are not in any harvested artifact (`tblSwitchboardItems` ships
  `(no data file)`), so this needs a new row-read surface. Deferred as
  **DW-9** in [`docs/planning/deferred-work.md`](../planning/deferred-work.md).
- **R2-2 (dialog → query → report chains)** needs the individual
  edges/`WhereCondition`/`OpenArgs` (present today) **joined** into
  cross-object chains, plus report-menu embedded-macro parsing and
  `RecordSource` edges. That derivation is substantial new logic, not
  a wiring task. Deferred as **DW-10**.

Both are owned by the planned next set,
`012-switchboard-and-report-chain-edges` (006's P4). A consumer that
needs the switchboard tree or the launcher→report→dialog→query chains
today must treat them as unavailable and fall back to reading the raw
edges plus the per-object `ir/` records.

**Determinism.** Every collection is sorted at construction
(`Edges` and `UnresolvedTargets` by source module → procedure → line →
column, case-insensitive), so identical input yields a byte-identical
graph across runs — the determinism standard the project guidance pins
for enrichment outputs.

---

## 6. The `UnresolvedItems` array

> **Status (as of set 011 / P3, F-2-2): `unresolved-items.json` is now wired
> into the `run` pipeline.** It aggregates the navigation graph's unresolved
> dispatch targets (`DoCmd.OpenForm`/`OpenReport` with a non-literal or missing
> target) and the per-procedure unresolved references the function-classification
> pass collected, so a non-empty list now reflects genuine manual-review items.
> Two producer seams remain **not** fed into this array yet: the SQL/form-reference
> and dynamic-SQL seams (their analyzers have no production call site — see DW-8),
> and the switchboard/macro navigation inputs (DW-9). An empty
> `unresolved-items.json` therefore means "no unresolved navigation targets or
> unresolved code references were found," not necessarily "every construct in the
> database resolved."
>
> **Scope caveat (DW-11): the unresolved surfaces undercount in two distinct
> ways.** Neither affects the HomeHealthCare golden (that corpus exercises
> neither case) but both will undercount on databases that do:
>
> 1. **Procedure-header unresolved references are missing from the run-wide
>    index, but present inline.** The VBA half of `unresolved-items.json` is drawn
>    from `FunctionClassification.UnresolvedReferences`, which the classifier
>    collects by walking only procedure **bodies**. An unresolved reference in a
>    procedure **header** — e.g. an optional parameter's default expression,
>    `Optional ByVal x As Variant = MissingThing` — is **absent from
>    `unresolved-items.json`** but **is** flagged inline on
>    `ParserOutput.Body.References[]` (the projector groups by procedure *scope*,
>    header included). For these, the inline per-reference fields are the complete
>    signal.
> 2. **Module-level unresolved references have NO manifest-level signal at all.**
>    References outside any procedure — a module-level `Const`/`Dim` initializer,
>    `Implements <unresolved>`, `Private x As New <unresolved>` — are dropped at
>    projection: `ParserOutputProjector` keeps only references with an enclosing
>    `ProcedureScope` (the v1.0 schema has no module-level item type). So such a
>    reference appears in **neither** `ParserOutput.Body.References[]` **nor**
>    `unresolved-items.json`. There is currently no complete manifest signal for
>    module-level unresolved references; until DW-11 lands, a consumer that must
>    account for them has to read the source directly.
>
> So the per-reference `ResolutionReason` / `ResolutionStatus` fields on each
> `ParserOutput.Body.References[]` entry (see below) are the authoritative signal
> for everything **inside a procedure** (body and header) — they capture the
> header references the run-wide index omits — but they are **not** complete for
> module-level references, which no manifest surface currently carries.

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

**Inline reason on each reference (008 Session 3, F-3-6).** The same
resolver reason is now also emitted inline on every unresolved
reference inside a module/form/report `ParserOutput`, as
`ResolutionReason` on the `Body.References[]` entry (alongside
`ResolutionStatus: "Unresolved"`). A consumer triaging one specific
reference can read the reason in place rather than cross-walking to
this aggregated `UnresolvedItems` list. The reason values are the
resolver's append-only `UnresolvedReasons` set (`not-in-scope`,
`me-in-standard-module`, `default-member-not-yet-resolved`,
`with-block-flattening-deferred`,
`member-access-on-unresolved-target`); the same narrative doc above
is authoritative for their meanings.

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
"Label": { "source": "Attached", "text": "Customer Name" }
"Label": { "source": "Unresolved" }
"Binding": { "kind": "Unbound" }
```

The provenance *keys* are lower-case (`source`, `text`, `kind`); the
`Label.source` *value* is a **closed PascalCase set** emitted verbatim
by the resolver: `"Attached"` (an attached `Label` control's caption),
`"Proximity"` (a nearby peer label, conservative 2× rule), `"FromName"`
(camelCase split of a non-generic control `Name`), `"FromBinding"`
(camelCase split of a simple `ControlSource`), or `"Unresolved"` (none
resolved). Match these values exactly. Bare missing keys are reserved
for "this property is at its Access default"; an explicit
`{ source: "Unresolved" }` is "the harvester looked here and found
nothing the consumer should trust."

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
- **No workflow-package authoring.** The order-entry pilot packages
  have been removed from this repo; further package work is deferred
  to the migration repo.
- **No symbolic execution of dynamic SQL.** Concatenated-SQL build
  sites surface as `UnresolvedItems[Source=DynamicSqlSite]` entries
  rather than getting resolved.
- **No late-binding type inference.** Member-access chains on
  Object-typed locals surface as `LateBound` classifications and
  `UnresolvedReferences` entries rather than getting resolved.
- **No top-level navigation surfaces yet.**
  `manifest.NavigationGraph` carries only the VBA-derived `Edges` and
  `UnresolvedTargets` today; `EntryPoints` / `Macros` are empty and
  `Switchboard` / `Ribbon` are absent (they need metadata-extractor
  inputs the pipeline does not yet supply — DW-9). The
  launcher→report→dialog→query **chain** derivation (R2-2) is likewise
  not done — the constituent edges are present but not joined (DW-10).
  Both are owned by the planned set
  `012-switchboard-and-report-chain-edges`. See §5.7.

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
A populated `manifest.json` envelope shows every top-level key §4
documents (envelope identification, `Database`, `Summary`, the
per-category stub arrays, and the cross-cutting enrichment arrays) in
the order the writer emits them. Any live harvester run produces one;
read a `manifest.json` from a run's output root for the full envelope
shape, and the committed golden corpus (below) for the populated
per-record `ir/<Category>/` detail.

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
  the committed canonical fixture: unbound forms, ADODB, validation-only
  data macros, Hungarian naming.

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
- **Walking a developer through the review artifacts.** [`docs/harvester/reviewing-the-feedback-artifacts.md`](reviewing-the-feedback-artifacts.md) —
  the plain-language guide you direct a developer to when presenting the
  `*-checklist.json` / `classification.json` files (see §3.3 for the
  agent-facing rule).
- **Running the harvester across many databases.** [`docs/harvester/batch-operations.md`](batch-operations.md) —
  multi-DB layout, `--clean` / `--no-clean`, the cross-DB roll-up
  index, MSACCESS / RCW hygiene, and the `--json-status`
  provenance envelope.
- **Unresolved-reason narrative (authoritative).** [`docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`](../session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md).
- **CLI usage and project orientation.** [`README.md`](../../README.md).
- **Parser test cases (canonical input / expected pairs).** [`docs/test-cases/`](../test-cases/).
- **Project guidance (principles + conventions).** [`docs/planning/project-guidance.md`](../planning/project-guidance.md).
- **Workflow doc (how harvester sessions are run).** [`docs/ai-led-session-workflow.md`](../ai-led-session-workflow.md).

The full docs index lives at [`docs/README.md`](../README.md).
