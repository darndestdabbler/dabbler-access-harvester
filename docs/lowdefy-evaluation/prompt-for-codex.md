You are providing a cross-provider second opinion on an
architecture decision for a multi-repo Access-to-Blazor migration project.

Your peer model from a different provider is receiving the same prompt; the
orchestrator will synthesize both responses. Treat my initial position as
something to argue with. Push back hard on anything that looks wrong.

The original brief authored by the human follows verbatim. After the brief,
I include the orchestrator's framing of what context you actually have,
plus my initial position on the eight dimensions and the architectural
fork. After all of that, please produce the six-section deliverable the
brief asks for.

=========================================================================
ORIGINAL BRIEF (verbatim)
=========================================================================

# Brief: Evaluate Lowdefy's Schema as IR for the Access → Blazor Codegen

## Background

I am building a tool that migrates Microsoft Access databases (`.accdb`) to .NET/Blazor applications. The pipeline today is roughly:

```
Access (.accdb)
  ↓ extraction tool (already built)
Access-shaped YAML  (one file per Tables/Queries/Forms/Reports/Macros/Modules object)
  ↓ AI-assisted translation (in progress)
Custom IR YAML       ← the schema I'm designing right now
  ↓ deterministic code generator
Blazor C# project
  ↓ dotnet build
running app
```

I have been working on the custom IR layer (and its code generator) for about two months. The IR is intended to be the schema that AI writes to and humans review — a typed, validated intermediate representation that sits between extracted Access YAML and generated Blazor.

A recent conversation suggested that **Lowdefy's schema** (https://lowdefy.com, https://docs.lowdefy.com) — independently of Lowdefy as a runtime — is worth studying as a reference for how to shape this IR. Lowdefy decomposes UI declaratively into five orthogonal primitives:

1. **Blocks** — visual UI components (forms, tables, charts, containers, modals, etc.). Composable trees.
2. **Requests** — named, parameterized data operations against a connection (read or write). Decoupled from blocks; one block may render many requests, one request may feed many blocks.
3. **Operators** — pure-ish functions used in expressions (`_if`, `_get`, `_state`, `_js`, `_eq`, etc.). Used wherever a value is needed.
4. **Actions** — typed event handlers (`Validate`, `Reset`, `Request`, `Link`, `SetState`, `CallMethod`, etc.). Triggered by Events.
5. **Events** — named triggers on blocks/pages (`onClick`, `onInit`, `onSubmit`, etc.) that run an ordered list of Actions, with `try`/`catch` and debounce semantics.

Plus two build-time concerns worth noting:

- **`_ref` / `_var`** — file-level inclusion with parameter substitution, used to share configuration across pages and apps.
- **Schema validation** — every property of every block/action/operator is JSON-schema-validated; the runtime refuses to start on an invalid config.

The question on the table: **how much of this decomposition should the IR borrow, and where should it deliberately diverge?**

## Your Task

1. Read enough of the current codebase to understand what the IR looks like today (schema definitions, code generator, any sample IR files), the inputs (Access-shaped YAML — see the `HomeHealthCare.accdb` reference database and its extraction), and the outputs (generated Blazor).
2. Compare the current IR's shape against Lowdefy's five-primitive decomposition.
3. Identify where adopting Lowdefy's structure would *improve* the IR — for AI authoring, for human review, for code generation, or for maintenance over time.
4. Identify where it would be *wrong* to adopt — places where Access semantics or the Blazor target make a different decomposition more honest.
5. Surface concrete, prioritized recommendations.

## What to Examine in the Codebase

Please orient yourself before diving in. Look for:

- The IR schema definition itself (likely JSON Schema, C# records/POCOs with attributes, or YAML schema files). Note its top-level types and how they relate.
- The code generator (templates, source generators, or hand-rolled emitters). Note how IR types map to Blazor artifacts.
- Sample IR files, fixture files, or golden-master outputs that show real intended usage.
- Any documentation, README, ADRs, or design notes explaining the current schema's rationale.
- Test cases — what behaviors are pinned down, and what's intentionally loose?
- Any `TODO`, `FIXME`, or `// HACK` comments — these often mark places where the current abstraction is straining.

If a `HomeHealthCare`-derived sample IR exists, walk through the IR representation of `frmBudgetServicePlan` and `rptClientProfile` specifically. Those two objects exercise most of the hard patterns:

- `frmBudgetServicePlan` — cascading list selection (needs → plans), modal popup detail forms, parent–child state coordination, computed totals, conditional formatting.
- `rptClientProfile` — parent report with three subreports linked on `ClientID`, parameter-driven filtering via a separate parameter form.

## Dimensions to Evaluate

Score the current IR and the Lowdefy-aligned alternative on each of these:

1. **Compactness for AI authoring** — how many lines of IR does it take to express a typical detail form? A cascading list page? A parameter-driven report? Lowdefy's pitch is "50 lines vs 500." How does the current IR compare?
2. **Reviewability** — can a human scan a page of IR and predict what the UI does without running it? Lowdefy schemas are linear and dense; some custom DSLs trade that for "shaped like the domain" verbosity.
3. **Composability** — can the same sub-tree of IR be reused across pages without duplication? Is there a `_ref`-equivalent? If not, what's the duplication cost in real samples?
4. **Schema validation surface** — is every property statically validated? Are unknown properties rejected? Can the generator give a useful error pointing at the offending file and line?
5. **Separation of concerns** — are data fetching (Requests), display (Blocks), logic (Operators), and behavior (Actions/Events) cleanly separated, or do they bleed into each other? In Access, they're all tangled inside a Form object — easy to mirror, harder to maintain.
6. **Escape hatches** — when the IR can't express something, what's the path out? "Drop to hand-written Blazor for this page" must be cheap; if it's not, the IR will accrete junk types over time. Lowdefy's escape hatch is `_js` operators and custom Block/Action/Request plugins. What's ours?
7. **Code-gen ergonomics** — is the IR shaped so that the generator stays simple, or does the generator carry a lot of glue logic to bridge IR concepts to Blazor concepts? A good IR pushes complexity into the schema, not the emitter.
8. **Mapping fidelity from Access** — for the patterns in the reference DB (master/detail listboxes, modal dialog detail forms with `OpenArgs`, continuous forms, subreports, parameter forms, switchboard, data macros, transactional copy operations), how naturally does each shape sit in the IR?

## Initial Considerations (Seed Thoughts)

Use these as starting points; disagree freely with any of them after reading the code.

**Where Lowdefy's decomposition probably wins:**

- The Block/Request split is genuinely valuable. Access conflates them (a Form's `RecordSource` is a property of the form). Separating them lets the same query feed a list, an export, and a chart without duplication, and lets the same form bind to different queries depending on context.
- The Operator catalog is a forcing function for keeping expressions composable. Without it, expressions tend to grow into bespoke string templates that the codegen has to parse. With it, expressions are tree-shaped from the start and trivial to emit.
- The Event-as-ordered-list-of-Actions pattern matches Access's `[Event Procedure]` model directly, but adds `try`/`catch` and debounce without the user having to think about them. Worth stealing.
- `_ref` for cross-file reuse is the thing Access doesn't have, and Access apps suffer for it. A sample IR for a 20-form app will collapse to half the size with proper references.

**Where the Lowdefy shape may be wrong for this IR:**

- Lowdefy is general-purpose. We're not. Our IR can have first-class types for the patterns that actually show up: `MasterDetailListPage`, `ModalDetailForm`, `ContinuousForm`, `ReportWithSubreports`, `Switchboard`, `ReportParameterForm`. Lowdefy would express these as compositions of generic Blocks; we can name them and shrink the AI's surface area dramatically. The cost is less flexibility for cases outside the migration scope — that's the right tradeoff.
- Lowdefy's runtime is JavaScript; its operators are JS-shaped. We're generating C#. Operators in our IR should map cleanly to expression trees or C# methods, which means a smaller and slightly different set: `Nz`, `IIf`, `DLookup`, `DCount`, `DSum`, `Format`, plus the standard arithmetic/comparison/logical operators. `_js` becomes "inline C# expression" — or, more likely, doesn't exist at all, and the escape hatch is "name a method on a partial class."
- Lowdefy's connectors abstract over MongoDB/SQL/REST. We have a known target: EF Core against SQL Server or PostgreSQL. The Connection/Request layer can be much thinner — likely just "named query" and "named command," with EF Core handling everything else.
- Reports are a domain Lowdefy genuinely doesn't model well. If our IR has first-class `Report` and `Subreport` types with `LinkMasterFields` / `LinkChildFields` semantics, we're already past where Lowdefy's vocabulary helps us. Don't try to express subreports as nested Blocks — they aren't UI; they're a print-layout construct.

**Things worth checking against current code:**

- How is state propagated between a master and detail in the current IR? Lowdefy uses page-level `_state`; Access uses `Me.OpenArgs` and form references. Pick one explicitly and document it.
- Where does validation live? Field-level, form-level, table-level (data macros), and cross-form (the `BeforeUpdate` budget check) all exist in the source. The IR needs a story for each; check whether the current schema treats them uniformly or has four different mechanisms.
- How does the IR represent the "copy from prior fiscal year" transactional operation in `basDataOperations`? This is the canonical stress test. If it's not modeled at all yet (i.e., assumed to be hand-written), that's fine, but the IR should at least have a designated extension point — a named ServerAction or Command — so it's clear where such things live.
- Does the IR distinguish "page" from "modal" from "drawer"? Lowdefy does (via `type` on the block and via the `OpenLink`/`Modal` actions). Access does (`acDialog`, `PopUp`, `Modal`). Blazor needs to.

## Deliverable

Please write your evaluation as a markdown document with these sections:

1. **Codebase summary** — what the IR is today, in your own words. One or two paragraphs plus a sketch of the top-level types.
2. **Lowdefy-alignment scorecard** — the eight dimensions above, each with a short verdict (current IR strong / aligned / weak / divergent-but-justified) and one or two sentences of reasoning.
3. **Recommendations to adopt** — Lowdefy patterns worth importing, in priority order, each with a concrete sketch of what the change would look like in the current schema.
4. **Recommendations to reject** — Lowdefy patterns that would be wrong here, with reasoning.
5. **Open questions** — places where the right answer depends on a decision I haven't made yet, with the decision framed explicitly.
6. **Suggested next steps** — a short ordered list of small, reversible changes I could try first, before committing to a larger refactor.

Avoid speculation about parts of the codebase you didn't read. If something is unclear, ask before recommending.

## Reference Material

- Lowdefy site and docs: https://lowdefy.com, https://docs.lowdefy.com
- Lowdefy source: https://github.com/lowdefy/lowdefy (the `packages/docs/concepts/` directory has the cleanest descriptions of events, actions, operators, and the schema model)
- The reference Access database extracted YAML lives in the project; the `Summary.yaml` and `Relationships.yaml` files are the entry points.


=========================================================================
HARVESTER IR SHAPE (what `ir/` actually looks like in this repo)
=========================================================================

This is the harvester's Access-shaped IR — `FormMetadata`/`ReportMetadata`
C# records that faithfully preserve Access semantics. Not the Blazor codegen
target the brief asks about. Included so you understand the upstream layer.

# IR Schema Reference: Forms and Reports

> **Audience:** Downstream AI consumers of the harvester output.
> **Purpose:** Documents the typed `FormMetadata` / `ReportMetadata` C# records as the
> **public contract** for form and report metadata. The underlying `AccessNode` IR JSON
> is a harvester implementation detail; the typed records are what downstream consumers
> should work with.

---

## Overview of the Pipeline

Access forms and reports travel through a three-tier pipeline before they reach the typed schema:

```
SaveAsText (.txt)
    ↓  AccessTextParser  (AccessHarvester.Core.Translation)
kb/*.json              — AccessNode JSON tree (Type, Properties, Children, VbaCode)
    ↓  GeometryEnricher  → adds GeometryIn (inch-converted bounds)
    ↓  LabelResolver     → adds Label { source, text } to eligible controls
    ↓  PropertyDecoder   → decodes "NotDefault" sentinels, lists in NonDefaultProperties
    ↓  TextCapacityEnricher  → adds text-capacity annotations
    ↓  VbaSignatureEnricher  → adds Procedures array (procedure signatures)
    ↓  MacroActionsEnricher  → adds EventMacros (embedded-macro action lists)
ir/*.json              — enriched AccessNode tree
    ↓  FormMetadataExtractor / ReportMetadataExtractor  (AccessHarvester.Access)
FormMetadata / ReportMetadata  — typed records, public contract
```

The typed records promote a small set of high-value fields into first-class typed
properties and pass everything else through the `Properties` bag. **Not every field
from the IR JSON is re-modeled on the typed records.** See the
[Fields Not Re-Modeled](#fields-not-re-modeled) section for the deliberate omissions.

---

## FormMetadata

`AccessHarvester.Access.FormMetadata` — C# `sealed record`

| Field | Type | Notes |
|---|---|---|
| `Name` | `string` (required) | The form name as Access stores it (e.g. `frmPatients`). Derived from the source file name; not present in the IR JSON itself. |
| `ModuleName` | `string?` | The VBA class-module name backing this form (typically `Form_<Name>`). Populated by parsing the first `Attribute VB_Name = "..."` line out of `VbaCode` (scans the first 10 lines). Null when `VbaCode` is null (no code-behind) or when the attribute line is absent. |
| `RecordSource` | `string?` | The bound data source: a table name, a saved query name, or a SQL string. Null for unbound forms. Exact-value lift from `Properties["RecordSource"]`. |
| `Filter` | `string?` | JET SQL `WHERE`-fragment (no `WHERE` keyword). Null when not set. Exact-value lift from `Properties["Filter"]`. |
| `OrderBy` | `string?` | JET SQL `ORDER BY`-fragment (no keywords). Null when not set. Exact-value lift from `Properties["OrderBy"]`. |
| `DefaultView` | `string?` | Decoded label: `"Single Form"`, `"Continuous Forms"`, `"Datasheet"`, `"PivotTable"`, `"PivotChart"`, `"Split Form"`. Null if absent or unrecognized code. Raw numeric code is retained in `Properties["DefaultView"]`. |
| `Properties` | `IReadOnlyDictionary<string, JsonElement>?` | All other form-level properties as Access wrote them. Exact-value lifts (`RecordSource`, `Filter`, `OrderBy`) are **removed** from this bag; decoded-lift raw values (`DefaultView`) remain. Values are `JsonElement` for faithful round-trip of heterogeneous shapes. Null when no properties remain after removal. |
| `GeometryIn` | `GeometryInches?` | Inch-converted form-level geometry. See `GeometryInches`. |
| `NonDefaultProperties` | `IReadOnlyList<string>?` | Names of form-level properties whose Access source value was the `"NotDefault"` sentinel (i.e. developer explicitly deviated from the form default). The decoded value appears in `Properties`. |
| `Children` | `IReadOnlyList<AccessControl>?` | The form's design tree. Typically: a `FormHeader` section, a `Detail` section (the form body), and a `FormFooter` section, each holding their controls. See `AccessControl`. |
| `VbaCode` | `string?` | Complete VBA code-behind text, exactly as Access wrote it. Null for forms with no code-behind. Procedure-level structure is in the underlying IR JSON's `Procedures` array (see [Fields Not Re-Modeled](#fields-not-re-modeled)). |

---

## ReportMetadata

`AccessHarvester.Access.ReportMetadata` — C# `sealed record`

Mirrors `FormMetadata` for all shared fields (same promotion rules, same conventions)
and adds two report-specific fields:

| Field | Type | Notes |
|---|---|---|
| `Name` | `string` (required) | The report name (e.g. `rptWeeklyActivity`). |
| `ModuleName` | `string?` | The VBA class-module name backing this report (typically `Report_<Name>`). Same extraction rule as `FormMetadata.ModuleName`: parsed from the first `Attribute VB_Name = "..."` line in `VbaCode` (first 10 lines scanned); null when absent or when there is no code-behind. |
| `RecordSource` | `string?` | Bound data source. Same as `FormMetadata`. |
| `Filter` | `string?` | Same as `FormMetadata`. |
| `OrderBy` | `string?` | Top-level report sort. Per-group sorts live in `GroupLevels`. |
| `DefaultView` | `string?` | Decoded label. Report-specific codes: `"Report View"` (0), `"Print Preview"` (1), `"Layout View"` (2). Null if absent or unrecognized code. Raw numeric code is retained in `Properties["DefaultView"]`. **Different enumeration from forms.** |
| `Properties` | `IReadOnlyDictionary<string, JsonElement>?` | Same as `FormMetadata`. `GroupLevel <N>` configuration nodes do not appear here — they live in the IR JSON's `Children` array as separate nodes and are consumed by `GroupLevels` (see below). |
| `GeometryIn` | `GeometryInches?` | Same as `FormMetadata`. |
| `NonDefaultProperties` | `IReadOnlyList<string>?` | Same as `FormMetadata`. |
| `Children` | `IReadOnlyList<AccessControl>?` | Report design tree: `PageHeader`, `GroupLevel0Header`, `Detail`, `GroupLevel0Footer`, `PageFooter`, etc. The `GroupLevel <N>` configuration nodes (space-separated, e.g. `"GroupLevel 0"`) that carry per-level grouping metadata are **filtered out** of this list — they are not design bands and are consumed by `GroupLevels`. |
| `GroupLevels` | `IReadOnlyList<GroupLevel>?` | Grouping / sorting metadata. Null when the report has no groups. See `GroupLevel`. |
| `PageSetup` | `PageSetup?` | **Always null — decoding deferred.** See `PageSetup`. |
| `VbaCode` | `string?` | Same as `FormMetadata`. |

---

## AccessControl

`AccessHarvester.Access.AccessControl` — C# `sealed record`

One node in the form/report design tree. Covers controls, sections, and anonymous `Begin`
wrappers.

| Field | Type | Notes |
|---|---|---|
| `Type` | `string` (required) | Access object class, passed through from the IR JSON unchanged. Common values: `TextBox`, `ComboBox`, `CommandButton`, `CheckBox`, `OptionButton`, `ToggleButton`, `Label`, `Line`, `Rectangle`, `Image`, `TabCtl`, `PageBreak`, `Subform`, `Subreport`, `FormHeader`, `FormFooter`, `Detail`, `PageHeader`, `PageFooter`, `GroupLevel0Header`, `GroupLevel0Footer`, `Begin`. Form body sections additionally carry `Type = "Section"` with `Name = "Detail"` (that is how Access's SaveAsText encodes the detail band for forms). The literal `Begin` appears for anonymous default-property wrappers SaveAsText emits inside sections. |
| `Name` | `string?` | Control name (e.g. `txtCustomerName`, `Detail`). Null for anonymous `Begin` wrappers. Lifted from the raw `Name` property; that key is removed from `Properties` after promotion. |
| `Properties` | `IReadOnlyDictionary<string, JsonElement>?` | All other control properties as Access wrote them. `Name` is removed. Values are `JsonElement`. Null when empty after removal. |
| `GeometryIn` | `GeometryInches?` | Inch-converted control-level geometry. Raw twips remain in `Properties` (`Left`, `Top`, `Width`, `Height`). |
| `Label` | `LabelInfo?` | Resolved visible label. Always emitted for eligible control types (TextBox, ComboBox, ListBox, CheckBox, OptionButton, ToggleButton, Subform, Subreport); null for ineligible types (Label, Line, Rectangle, Image, PageBreak, Section nodes, Begin wrappers). |
| `NonDefaultProperties` | `IReadOnlyList<string>?` | Property names whose source value was `"NotDefault"`. Decoded values appear in `Properties`. |
| `Children` | `IReadOnlyList<AccessControl>?` | Child nodes: a section's controls, a tab control's pages, the contents of a `Begin` wrapper. **Subform / Subreport control nodes do NOT inline the contained form/report tree here** — the contained object is extracted separately as its own `FormMetadata` / `ReportMetadata` and referenced by name via `SourceObject` in `Properties`. |

---

## LabelInfo

`AccessHarvester.Access.LabelInfo` — C# `sealed record`

| Field | Type | Notes |
|---|---|---|
| `Source` | `string` (required) | Provenance of the label: `"attached"` (a Label child's Caption), `"proximity"` (a nearby peer Label, conservative 2× rule), `"from-name"` (camelCase split of a non-generic control Name), `"from-binding"` (camelCase split of a simple `ControlSource` identifier), `"unresolved"` (none of the above resolved). |
| `Text` | `string?` | The resolved label text. Null only when `Source` is `"unresolved"`. |

---

## GeometryInches

`AccessHarvester.Access.GeometryInches` — C# `sealed record`

All dimensions in inches (1 inch = 1440 twips). Raw twips remain in the node's
`Properties` bag. Fields are sparse — only dimensions Access actually wrote are present;
the rest are null and should be omitted from serialized output.

| Field | Type | Notes |
|---|---|---|
| `X` | `double?` | Left edge in inches (from `Left` twips). |
| `Y` | `double?` | Top edge in inches (from `Top` twips). |
| `W` | `double?` | Width in inches (from `Width` twips). |
| `H` | `double?` | Height in inches (from `Height` twips). |

---

## GroupLevel

`AccessHarvester.Access.GroupLevel` — C# `sealed record` (on `ReportMetadata` only)

### IR JSON source

Per-level grouping metadata lives on a `GroupLevel <N>` child node in the report root's
`Children` array (space-separated `Type` — e.g. `"GroupLevel 0"`). The
`AccessTextParser` produces this node from Access's SaveAsText `Begin GroupLevel N`
blocks. Example raw IR fragment:

```json
{
  "Type": "GroupLevel 0",
  "Properties": {
    "GroupOn":        "0",            // grouping mode CODE
    "GroupOnCol":     "[DistrictID]", // grouping field expression
    "SortOrder":      "0",
    "KeepTogether":   "0",
    "GroupInterval":  "1"
  }
}
```

The separate `GroupLevel<N>Header` / `GroupLevel<N>Footer` nodes (no space, e.g.
`"GroupLevel0Header"`) are design-band sections and remain in `ReportMetadata.Children`;
they contribute `HasHeader` / `HasFooter` only.

> **Property-name quirk.** In SaveAsText, the property literally named `GroupOn` is the
> *mode code* (0–9), and `GroupOnCol` is the *grouping field expression*. The typed
> `GroupLevel` record reverses this for clarity: `GroupOn` is the human-readable
> expression (lifted from `Properties["GroupOnCol"]`), and `GroupingMode` is the decoded
> label (from `Properties["GroupOn"]`).

### Fields

| Field | Type | Notes |
|---|---|---|
| `Level` | `int` (required) | Zero-based group level. 0 = outermost group (first in the `GroupLevels` list). |
| `GroupOn` | `string?` | The field name or expression the group is keyed on (e.g. `[DistrictID]`, `=Year([OrderDate])`). Lifted from `Properties["GroupOnCol"]` on the `GroupLevel <N>` child node. Null when the property is absent or the level has only a section node. |
| `HasHeader` | `bool?` | True when a `GroupLevel<N>Header` section node exists in `Children`. |
| `HasFooter` | `bool?` | True when a `GroupLevel<N>Footer` section node exists in `Children`. |
| `GroupingMode` | `string?` | Decoded from the mode CODE in `Properties["GroupOn"]` on the `GroupLevel <N>` child node. Mapping: `0`→`EachValue`, `1`→`Prefix`, `2`→`Year`, `3`→`Quarter`, `4`→`Month`, `5`→`Week`, `6`→`Day`, `7`→`Hour`, `8`→`Minute`, `9`→`Interval`. Null when the property is absent or the code is unrecognized. |
| `GroupInterval` | `int?` | Interval value for `Prefix` / `Interval` grouping modes (prefix character count or interval units). Parsed as `int` from `Properties["GroupInterval"]` on the `GroupLevel <N>` child node. Null when absent or unparseable. |
| `SortOrder` | `string?` | Decoded from `Properties["SortOrder"]` on the `GroupLevel <N>` child node: `0`→`Ascending`, `1`→`Descending`. Null when absent or unrecognized. |
| `KeepTogether` | `string?` | Decoded from `Properties["KeepTogether"]` on the `GroupLevel <N>` child node: `0`→`None`, `1`→`WholeGroup`, `2`→`WithFirstDetail`. Null when absent or unrecognized. |

---

## PageSetup

`AccessHarvester.Access.PageSetup` — C# `sealed record` (on `ReportMetadata` only)

**Always null on `ReportMetadata.PageSetup`.** Decoding the `PrtMip` / `PrtDevMode`
binary-blob properties is deferred. The typed schema documents the intended shape for
when a future session implements it:

| Field | Type | Notes |
|---|---|---|
| `Orientation` | `string?` | `"Portrait"` or `"Landscape"`. |
| `PaperSize` | `string?` | One of Access's paper-size labels (`Letter`, `Legal`, `A4`, …). |
| `Margins` | `Margins?` | Page margins in inches. See `Margins`. |

`Margins` fields: `TopIn`, `BottomIn`, `LeftIn`, `RightIn` (all `double?`).

---

## Fields Not Re-Modeled

The following enricher outputs are present in the underlying IR JSON but are **not**
surfaced on the typed records. Consumers that need them should read the IR JSON directly.

| IR JSON field | Enricher | Why not re-modeled |
|---|---|---|
| `Procedures` (array) | `VbaSignatureEnricher` | Procedure-signature analysis is the SymbolDb domain; the typed form/report schema defers to it. |
| `EventMacros` (object) | `MacroActionsEnricher` | Macro action lists are event-correlated in the IR JSON; re-modeling them on the typed schema would duplicate structure already designed for the SymbolDb layer. |
| `PrtMip`, `PrtDevMode` | (raw property bag) | Binary blobs — decoding is deferred to a future session. Raw bytes are in `Properties`. |

---

## Worked Example

### Small Form

**Input IR JSON (`ir/Forms/frmPatients.ir.json` — simplified):**

```json
{
  "Type": "Form",
  "Properties": {
    "RecordSource": "tblPatients",
    "DefaultView": "0",
    "Caption": "Patient Record",
    "AllowEdits": "True",
    "Width": "9360"
  },
  "GeometryIn": { "W": 6.5 },
  "NonDefaultProperties": ["AllowEdits"],
  "VbaCode": "Attribute VB_Name = \"Form_frmPatients\"\r\nOption Compare Database\r\nOption Explicit\r\n\r\nPrivate Sub Form_Load()\r\n    ' ...\r\nEnd Sub\r\n",
  "Children": [
    {
      "Type": "Section",
      "Properties": { "Name": "Detail", "Height": "3600" },
      "GeometryIn": { "H": 2.5 },
      "Children": [
        {
          "Type": "TextBox",
          "Properties": {
            "Name": "txtLastName",
            "ControlSource": "LastName",
            "Left": "1440", "Top": "120", "Width": "2880", "Height": "360"
          },
          "GeometryIn": { "X": 1.0, "Y": 0.083, "W": 2.0, "H": 0.25 },
          "Label": { "source": "attached", "text": "Last Name" }
        }
      ]
    }
  ]
}
```

**Resulting `FormMetadata` (typed record):**

```
Name:          "frmPatients"
ModuleName:    "Form_frmPatients"   ← parsed from Attribute VB_Name in VbaCode
RecordSource:  "tblPatients"   ← exact-value lift; key removed from Properties
Filter:        null
OrderBy:       null
DefaultView:   "Single Form"   ← decoded lift; raw "0" remains in Properties
Properties:    { "DefaultView": "0", "Caption": "Patient Record",
                 "AllowEdits": "True", "Width": "9360" }
GeometryIn:    { W: 6.5 }
NonDefaultProperties: ["AllowEdits"]
VbaCode:       "Attribute VB_Name = \"Form_frmPatients\"\r\n..."
Children:
  [0] AccessControl
      Type:    "Section"        ← form body section; Name "Detail" identifies the band
      Name:    "Detail"         ← lifted; key removed from Properties
      Properties: { "Height": "3600" }
      GeometryIn: { H: 2.5 }
      Children:
        [0] AccessControl
            Type:       "TextBox"
            Name:       "txtLastName"    ← lifted
            Properties: { "ControlSource": "LastName", "Left": "1440",
                          "Top": "120", "Width": "2880", "Height": "360" }
            GeometryIn: { X: 1.0, Y: 0.083, W: 2.0, H: 0.25 }
            Label:      { Source: "attached", Text: "Last Name" }
```

---

### Small Report with One Group Level

**Input IR JSON (`ir/Reports/rptByRegion.ir.json` — simplified):**

```json
{
  "Type": "Report",
  "Properties": {
    "RecordSource": "qrySalesByRegion",
    "DefaultView": "1"
  },
  "Children": [
    { "Type": "PageHeader",        "Properties": { "Name": "PageHeader" } },
    {
      "Type": "GroupLevel 0",
      "Properties": {
        "GroupOn":       "2",
        "GroupOnCol":    "=Year([OrderDate])",
        "SortOrder":     "0",
        "KeepTogether":  "1",
        "GroupInterval": "1"
      }
    },
    { "Type": "GroupLevel0Header", "Properties": { "Name": "GroupLevel0Header" } },
    { "Type": "Detail",            "Properties": { "Name": "Detail" } },
    { "Type": "GroupLevel0Footer", "Properties": { "Name": "GroupLevel0Footer" } },
    { "Type": "PageFooter",        "Properties": { "Name": "PageFooter" } }
  ]
}
```

**Resulting `ReportMetadata` (typed record):**

```
Name:          "rptByRegion"
ModuleName:    null   ← (no VbaCode in this example; would be "Report_rptByRegion" otherwise)
RecordSource:  "qrySalesByRegion"   ← exact-value lift
Filter:        null
OrderBy:       null
DefaultView:   "Print Preview"       ← report-specific code 1
Properties:    { "DefaultView": "1" }
               (RecordSource removed after exact-value lift;
                the "GroupLevel 0" config node lives in Children — not Properties —
                and is filtered out of the typed Children list)
GeometryIn:    null
GroupLevels:
  [0] GroupLevel
      Level:        0
      GroupOn:      "=Year([OrderDate])"  ← lifted from "GroupLevel 0" child's GroupOnCol
      HasHeader:    true                  ← GroupLevel0Header section found in Children
      HasFooter:    true                  ← GroupLevel0Footer section found in Children
      GroupingMode: "Year"                ← decoded from GroupOn code "2"
      GroupInterval: 1                    ← parsed from GroupInterval "1"
      SortOrder:    "Ascending"           ← decoded from SortOrder code "0"
      KeepTogether: "WholeGroup"          ← decoded from KeepTogether code "1"
PageSetup:     null   (deferred)
Children:
  [ PageHeader, GroupLevel0Header, Detail, GroupLevel0Footer, PageFooter sections ]
  (the "GroupLevel 0" config node is filtered out — it is metadata, not a design band)
VbaCode:       null
```


=========================================================================
PROJECT GUIDANCE (the repo's principles for the upstream Access-shaped IR)
=========================================================================

Note especially: Project Scope And Audience, Output Architecture, Lean By
Default. The harvester's `ir/` is explicitly NOT the Blazor codegen target
— interpretive slots remain null at this tier.

Key load-bearing principles from `docs/planning/project-guidance.md`:

1. **Project scope and audience** — the harvester produces structured
   metadata at sufficient fidelity that a downstream AI can interpret
   and document the original Access app's behavior. **AI interpretation
   is explicitly out of scope for this repo** — this codebase is
   mechanical extraction and deterministic enrichment.

2. **Output architecture** — three-tier output: `raw/` (Access-native),
   `kb/` (clean parsed JSON), `ir/` (`kb/` plus mechanical enrichments).
   **Interpretive slots remain null at the `ir/` tier** — fields like
   `purpose`, `notes`, `intent` are reserved for the AI consumer to fill
   downstream. The harvester does not guess at semantics.

3. **Lean by default** — don't add features, abstractions, or safety
   nets beyond what the task in front of you requires. Don't introduce
   an interface for a single implementation. Don't write a fallback
   heuristic when the only known input format is the one the test
   fixtures use.

4. **Output JSON conventions** — provenance fields use a `{ source, ... }`
   shape (e.g., `Label: { source: 'unresolved' }`). Numeric units are
   explicit in field names (e.g., `GeometryIn`). Only emit fields Access
   actually wrote — do not synthesize zeros or default values.

5. **VBA parser strategy** — output is **deterministic** given the same
   input. Unresolvable constructs are explicit (`{ source: 'unresolved',
   reason: '<string>' }`), not silent. Real grammar over regex.


=========================================================================
WORKFLOW PACKAGE EXAMPLE (de facto AI-readable layer today)
=========================================================================

This is the seven-section markdown format that has been the AI-authored
layer in practice for the Blazor pilot. It captures business invariants in
prose with structured sections; it is NOT YAML/JSON and is NOT directly
consumed by codegen. The Blazor pilot consumed this as guidance for
hand-authoring C#. Whether this format should be retained, supplemented by
a YAML IR, or replaced by a YAML IR is one of the open questions.

# Workflow Package: Add an Order

> **Slug:** `add-an-order` (filename: `packages/add-an-order.md`)
> **Pilot sessions:** Session 2 (draft) + Session 3 (peer review &
> falsification) of 7
> **Author orchestrator (Session 2):** Codex GPT-5.4
> **Peer-review orchestrator (Session 3):** Claude Opus 4.7
> **Source DB:** `samples/NorthwindDevEdition.accdb`
> **Source IR:** `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/`
>
> Open Decisions in effect for this set
> (resolved Session 1, 2026-04-30):
>
> - **Source operationalization format:** structured prose
>   (numbered click-sequence in markdown). See Section 5.
> - **Component / role mapping format:** generic-now
>   (implementation-agnostic hints). See Section 6.

---

## 1. Workflow summary

Sales staff create a new customer order by opening the order-detail form,
selecting the customer, and adding one or more line items. The workflow
captures the commercial intent of a new sale while immediately classifying
each requested line item against current inventory so the organization knows
whether the order can proceed from stock or needs replenishment. The business
outcome is a persisted order in `New` status with enough defaults and
inventory signals in place for later invoice, shipping, and payment steps.

## 2. Actors and entities

- **Actors:** Sales employee or other authenticated order-entry user; the form seeds `EmployeeID` from the current logged-in user (`modGlobal::Get_UserID`).
- **Primary entity:** Order.
- **Related entities:** Customer/company, order line items, products, order status, order-detail status, inventory / stock-take / purchase-order inputs, and tax status.

## 3. Business rules with evidence + confidence tags

- **Rule:** Starting a new order seeds the order with the current user, the system tax rate, and `New` status before the first save.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `Form_BeforeInsert`; `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderStatus.osNew`; `Tables/Orders.ir.json` shows `EmployeeID`, `TaxRate`, and `OrderStatusID` are required.
  - **Confidence:** confirmed

- **Rule:** `OrderDate` defaults to the current timestamp at the table level, even though `Form_BeforeInsert` does not assign it explicitly.
  - **Evidence:** `Tables/Orders.ir.json` -> `OrderDate` has `Required: true` and `DefaultValue: "Now()"`; `Forms/frmOrderDetails.ir.json` -> `Form_BeforeInsert` sets `EmployeeID`, `TaxRate`, and `OrderStatusID` only.
  - **Confidence:** confirmed

- **Rule:** Selecting a customer defaults the order's tax status from that customer's `StandardTaxStatusID`.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `CustomerID_AfterUpdate`; `Tables/Companies.ir.json` -> required `StandardTaxStatusID`; `Queries/qryCustomers.ir.json` limits the picker to customer-typed companies.
  - **Confidence:** confirmed

- **Rule:** The order cannot be saved while any required bound order field is still empty.
  - **Evidence:** `Forms/frmOrderDetails.ir.json` -> `Form_BeforeUpdate`; `ModulesAndClasses/modValidation.ir.json` -> `ValidateForm`, `IsValidForm`, `IsBoundToRequiredField`; `Tables/Orders.ir.json` marks `EmployeeID`, `CustomerID`, `OrderDate`, `TaxRate`, `TaxStatusID`, and `OrderStatusID` as required.
  - **Confidence:** confirmed

- **Rule:** A new line item starts in `New` status, then moves to `Allocated` only if current physical inventory can satisfy the entire requested quantity; otherwise it is preliminarily marked `No Stock`.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_BeforeInsert`, `SetOrderDetailStatus`, `Form_BeforeUpdate`; `ModulesAndClasses/modInventory.ir.json` -> `ProductAvailable`; `Tables/OrderDetails.ir.json` marks `ProductID`, `Quantity`, `UnitPrice`, and `OrderDetailStatusID` as required, with `Quantity > 0`.
  - **Confidence:** confirmed

- **Rule:** The line item cannot be saved while required fields are missing, and quantity must be greater than zero.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_BeforeUpdate`; `ModulesAndClasses/modValidation.ir.json` -> `ValidateForm`, `IsValidForm`; `Tables/OrderDetails.ir.json` marks `ProductID`, `Quantity`, `UnitPrice`, and `OrderDetailStatusID` as required and sets `Quantity` validation rule to `>0`.
  - **Confidence:** confirmed

- **Rule:** Choosing a product on a new line item copies that product's unit price into the order-detail row.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `ProductID_AfterUpdate`; `Tables/Products.ir.json` -> required `UnitPrice`; `Queries/qryOrderLineItems.ir.json` joins order details to products.
  - **Confidence:** confirmed

- **Rule:** Each product may appear at most once per order.
  - **Evidence:** `Tables/OrderDetails.ir.json` -> unique index `uidxOrderID_ProductID` over `(OrderID, ProductID)`.
  - **Confidence:** confirmed

- **Rule:** Inventory allocation is all-or-nothing per line item and is processed oldest-first across all orders for the affected product; only line items already in `Allocated`, `No Stock`, or `On Order` participate in rebalancing, and eligible lines become `Allocated`, `On Order`, or `No Stock` depending on available stock plus approved inbound stock.
  - **Evidence:** `ModulesAndClasses/modInventory.ir.json` -> `AllocateInventory` SQL filters `OrderDetailStatusID in (1, 4, 5)` and orders by `o.OrderDate, o.OrderID`; `Forms/sfrmOrderLineItems.ir.json` -> `ReallocateInventory`, `Form_AfterUpdate`, `Form_AfterDelConfirm`; `ModulesAndClasses/modGlobal.ir.json` -> `enumOrderDetailStatus` values.
  - **Confidence:** confirmed

- **Rule:** Editing a saved line item can trigger reallocation for both the previously selected product and the currently selected product.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_Current` caches allocated rows, `ReallocateInventory` reallocates the cached prior product when the product changes, then reallocates the current product.
  - **Confidence:** confirmed

- **Rule:** Deleting a line item is a fulfillment-side event, not just a UI removal: when a line-item delete is confirmed, inventory is reallocated for that product so that downstream demand for the same product can pick up the freed capacity. Deleting the parent order itself walks each line item, deletes it, and reallocates per-product before the cascading order delete completes.
  - **Evidence:** `Forms/sfrmOrderLineItems.ir.json` -> `Form_AfterDelConfirm` calls `AllocateInventory m_RowCache.ProductID` when `Status = acDeleteOK`; `Forms/frmOrderDetails.ir.json` -> `Form_Delete` walks `sfrmOrderLineItems.Form.RecordsetClone`, deletes each row, and calls `AllocateInventory(lngProductID)` before the cascading order delete.
  - **Confidence:** confirmed

## 4. Behavioral invariants -> characterization tests

- **Invariant:** Beginning a new order stamps the responsible employee, tax rate, and `New` order status before save.
  - **Source procedure:** `frmOrderDetails::Form_BeforeInsert`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is about default business state on order creation, not about Access event timing or control focus.

- **Invariant:** New orders receive an `OrderDate` default from the data model even though the insert-seeding VBA does not assign it.
  - **Source procedure:** `Tables/Orders::OrderDate DefaultValue`, `frmOrderDetails::Form_BeforeInsert`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins persisted order state, not the Access form event used to reach that state.

- **Invariant:** Selecting a customer causes the order's tax status to follow the customer's default tax-status record.
  - **Source procedure:** `frmOrderDetails::CustomerID_AfterUpdate`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains derived order data, not the specific combo-box event or UI widget.

- **Invariant:** Saving an order with missing required fields is rejected.
  - **Source procedure:** `frmOrderDetails::Form_BeforeUpdate`, `modValidation::ValidateForm`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant pins persistence preconditions, not Access highlighting or message-box behavior.

- **Invariant:** A new order line item starts as `New`, copies the product price, and resolves to `Allocated` versus `No Stock` based on whether full quantity is available.
  - **Source procedure:** `sfrmOrderLineItems::Form_BeforeInsert`, `sfrmOrderLineItems::ProductID_AfterUpdate`, `sfrmOrderLineItems::SetOrderDetailStatus`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderLineItemBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is expressed as domain-state transitions for an order detail, not as datasheet or combo-box mechanics.

- **Invariant:** A line item cannot be saved unless required fields are present, quantity is positive, and the `(OrderID, ProductID)` pair stays unique inside the order.
  - **Source procedure:** `sfrmOrderLineItems::Form_BeforeUpdate`, `modValidation::ValidateForm`, `Tables/OrderDetails::uidxOrderID_ProductID`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderLineItemBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains durable persistence rules, not message boxes, highlights, or datasheet interactions.

- **Invariant:** Allocation reorders pending demand oldest-first and never partially allocates an individual line item.
  - **Source procedure:** `modInventory::AllocateInventory`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/InventoryAllocationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant constrains fulfillment semantics and ordering policy, which survive a rewrite into a different UI architecture.

- **Invariant:** When a saved allocated line item changes product, inventory is reallocated for the prior product first and then for the current product.
  - **Source procedure:** `sfrmOrderLineItems::ReallocateInventory`, `sfrmOrderLineItems::Form_Current`, `sfrmOrderLineItems::Form_AfterUpdate`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/InventoryAllocationBehaviorTests.cs`
  - **Anti-coupling check:** The invariant is about inventory side effects of editing a line item, not about Access subform lifecycle details.

- **Invariant:** When a confirmed line-item delete completes (the cached pre-edit row was Allocated), the deleted row's product gets a reallocation pass; a cancelled delete leaves inventory untouched. Deleting the parent order reallocates each child line item's product before the cascading delete fires.
  - **Source procedure:** `sfrmOrderLineItems::Form_AfterDelConfirm`, `sfrmOrderLineItems::Form_Current` (cache precondition), `frmOrderDetails::Form_Delete`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3DeletionReallocationTests.cs`
  - **Anti-coupling check:** The invariant is about fulfillment-side side effects of removing demand, not about acDeleteOK return-code mechanics or recordset-clone iteration.

- **Invariant:** Allocation rebalancing is cross-order in scope for a given product (no per-order filter), and date ties are broken by `OrderID` ascending so rebalancing is order-independent given a fixed clock.
  - **Source procedure:** `modInventory::AllocateInventory`
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3CrossOrderScopeTests.cs`
  - **Anti-coupling check:** The invariant constrains demand-pool scope and tiebreak policy, both of which survive a rewrite into any UI architecture.

## 5. Source operationalization reference

1. The user opens the order workflow from `frmOrderList`, where `cmdAddOrder_Click` calls `modOrders::OpenOrderDetailsForm` with no `OrderID`, creating a new `frmOrderDetails` instance.
2. `frmOrderDetails::Form_Load` sees no `TempVars!OpenArgs` and navigates to a new record if needed.
3. The user begins typing into the new order; `frmOrderDetails::Form_BeforeInsert` assigns the current employee, current system tax rate, and `OrderStatusID = osNew`.
4. The underlying `Orders` table contributes `OrderDate = Now()` as a data default when the new record is created.
5. The user chooses a customer in `CustomerID`; `CustomerID_AfterUpdate` looks up `Companies.StandardTaxStatusID` and writes that value into `TaxStatusID`.
6. The user fills the remaining order header fields required by the bound `Orders` record and saves; `Form_BeforeUpdate` calls `ValidateForm(Me)` and refuses the save if any required bound field is still null.
7. The user moves into `sfrmOrderLineItems`; if the parent order has not been started yet, `sfrmOrderLineItems_Enter` / `Form_Error` warn that the order must exist before line items.
8. On a new line item, `sfrmOrderLineItems::Form_BeforeInsert` seeds `OrderDetailStatusID = odsNew`.
9. The user optionally filters by category through `cboProductCategories_AfterUpdate`, which requeries the product list and then unconditionally selects the first matching product (`Me.ProductID = Me.ProductID.Column(0, 0)`) and invokes `ProductID_AfterUpdate` programmatically — so a category change always seeds a product and unit price.
10. The user selects a product; `ProductID_AfterUpdate` copies the hidden unit-price column into `UnitPrice`.
11. The user enters quantity and saves the line; `Form_BeforeUpdate` validates required fields, enforces table constraints such as positive quantity and unique `(OrderID, ProductID)`, and calls `SetOrderDetailStatus`, which marks the row `Allocated` when `ProductAvailable(ProductID) >= Quantity` and `No Stock` otherwise.
12. After save, `Form_AfterUpdate` runs `ReallocateInventory`, which recalculates allocation across all orders for the current product and, if the product changed on an already allocated row, for the previous product as well.
13. The order remains in `New` status, with line items carrying the immediate allocation signal that later invoice / shipping steps will consume.

## 6. Component / role mapping hints

- **UI pattern:** Master-detail create flow: order header plus editable line-item grid/subform, with immediate derived-field defaults and fulfillment-state feedback.
- **Role gating:** No explicit privilege gate was confirmed in the create path itself; the workflow appears to rely primarily on authenticated employee context rather than an Add-specific privilege check.
- **Component library mapping (optional):** Deferred to library-readiness pass; likely maps cleanly to a create form with an embedded editable collection/grid for line items and inline validation summaries.

## 7. Open questions / ambiguity log

- **Question:** Is the customer-derived tax status only a default the user may later override, or is the chosen customer's tax status intended to remain authoritative through save?
  - **Source-of-uncertainty:** `CustomerID_AfterUpdate` populates `TaxStatusID`, but the reviewed Add-an-Order evidence does not show a corresponding guard that prevents later manual edits.
  - **Recommended resolution path:** Human review of the running Access app, or deeper review during the downstream Blazor implementation pilot if tax-policy ownership becomes important.

- **Question:** Should the builder preserve the exact oldest-first tie-breaker (`OrderDate`, then `OrderID`) for allocation, or is "oldest demand first" sufficient if timestamps are modeled differently in the new system?
  - **Source-of-uncertainty:** `modInventory::AllocateInventory` encodes the precise SQL ordering, but the business intent behind the `OrderID` tiebreak is not documented separately.
  - **Recommended resolution path:** Preserve the policy in the initial Blazor slice, then confirm with a domain reviewer whether the exact secondary ordering is business-visible or merely an implementation choice.

- **Question:** Should deleting a `No Stock` or `On Order` line item also trigger reallocation for that product, or is the source's "Allocated-only" cache precondition the intended behavior?
  - **Source-of-uncertainty:** `sfrmOrderLineItems::Form_Current` only populates `m_RowCache` when the row is `Allocated`; therefore `Form_AfterDelConfirm`'s `AllocateInventory m_RowCache.ProductID` becomes a no-op (productId = 0) when a `No Stock` or `On Order` row is deleted. This may be a deliberate optimization (no allocated capacity to free) or an oversight that leaves freed `On Order` capacity un-rebalanced. Section 4's deletion invariant matches the source behavior, not necessarily the intended behavior.
  - **Recommended resolution path:** Confirm with a domain reviewer whether the Blazor builder should reallocate on every line-item delete (regardless of pre-delete status). If yes, the new system should diverge from the source here and the invariant test must be updated.

- **Question:** Should the new system propagate `SetOrderDetailStatus`'s "do nothing if Product or Quantity is missing" defensive exit, or is the table-level required-field validation sufficient on its own?
  - **Source-of-uncertainty:** `sfrmOrderLineItems::SetOrderDetailStatus` exits silently when `IsNull(Me.ProductID) Or IsNull(Me.Quantity)` rather than letting allocation logic see partial state. Combined with `Form_BeforeUpdate`'s required-field validation, the line item cannot persist in that state anyway, so this guard is defense-in-depth. Whether the Blazor builder needs an analogous guard depends on its allocation-trigger architecture.
  - **Recommended resolution path:** Defer to the implementation pilot — if allocation is invoked from a single save-point that runs after validation, the guard is redundant; if allocation can be invoked mid-edit (e.g., for live status indicators), the guard or its equivalent is required.

- **Question:** Should the `ProductID_AfterUpdate` self-correction (resetting `ProductID = 0` when the combo's hidden `UnitPrice` column is null) be reproduced in the new system, or is it an Access-specific workaround for the cascading combobox after a programmatic category change?
  - **Source-of-uncertainty:** The VBA comment says the null-column case "can happen when changing product category on a new row without entering any other data," indicating an Access widget-state quirk. The behavior preserves data integrity (no priceless line item) but does so via UI-mechanic side-effects.
  - **Recommended resolution path:** The Blazor builder probably solves this implicitly by not exposing a no-price intermediate state in the product picker. Flag the source behavior so the implementer recognizes it as a workaround rather than a business rule.


=========================================================================

## Orchestrator's initial position (you may disagree)

I am Claude Opus 4.7 acting as orchestrator for the dabbler-access-harvester
repo. Before sending this brief to you and to a peer model from a different
provider, I formed an initial position. Treat it as something to argue with,
not a constraint.

### Context the orchestrator surfaces about the brief

The brief was placed in `docs/evaluate-lowdefy-as-ir.md` in the harvester
repo, but the IR it describes — "Custom IR YAML between Access-shaped YAML
and Blazor codegen, worked on for ~2 months" — **does not exist in the
harvester repo**.

What actually exists in this repo:

- **Harvester `ir/`**: Access-shaped metadata IR (`FormMetadata`,
  `ReportMetadata` C# records). Faithfully mirrors Access semantics. The
  project-guidance.md is explicit: "Interpretive slots remain null at the
  `ir/` tier. Fields like `purpose`, `notes`, `intent`, etc. are reserved
  for the AI consumer to fill downstream. The harvester does not guess at
  semantics."
- **Workflow packages**: markdown documents in
  `docs/session-sets/workflow-package-pilot/packages/`. Seven-section
  template (Workflow summary, Actors/entities, Business rules + evidence
  + confidence tags, Behavioral invariants → characterization tests,
  Source operationalization reference, Component/role mapping hints,
  Open questions / ambiguity log). This is the AI-readable layer that
  captures business invariants. It is NOT YAML/JSON; NOT codegen-shaped.
- **Blazor pilot at `output-tests/blazor-add-an-order/`**: a working
  Blazor Server + MudBlazor app that consumes the workflow packages as
  *guidance for hand-authoring* C# domain services, controllers, and
  pages. The Option D resolution from `workflow-package-pilot` is that
  the characterization-test fixtures stay unmodified and the Blazor
  domain services author parallel logic honoring the same invariants —
  no shared workflow code between tests and host. No IR-driven codegen.

The "Custom IR YAML + codegen" the brief describes plausibly lives in a
sibling repo (`../AppDefToBlazor`, `../access-to-blazor`, or
`../AccessBlazorPlanning`). I have not read those repos. If your evaluation
genuinely depends on seeing that IR, please name the specific files you
would need and we will run a follow-up.

### Initial verdicts on the eight dimensions in the brief

Treating "current IR" as "the workflow-package format plus the implied
target IR for a future Blazor codegen layer":

1. **Compactness for AI authoring** — Lowdefy primitives would likely
   *increase* AI surface area for a bounded Access→Blazor migration. A
   domain DSL with first-class `MasterDetailListPage`, `ModalDetailForm`,
   `ContinuousForm`, `ReportWithSubreports`, `Switchboard`,
   `ReportParameterForm` is more compact than Lowdefy-style generic
   block trees. Tentative: divergent-but-justified.
2. **Reviewability** — Lowdefy-style flat schemas are denser and easier
   to scan. The current workflow-package format is prose-heavy; a YAML
   IR would be more reviewable but require its own tooling. Tentative:
   Lowdefy wins on this dimension.
3. **Composability / `_ref`** — Access has no cross-file reuse mechanism.
   Whatever IR replaces this, `_ref`-style include-with-substitution
   is straightforwardly worth borrowing. Tentative: adopt.
4. **Schema validation surface** — current workflow packages are
   markdown; no formal schema validation. A JSON-Schema-validated IR is
   strictly better here. Tentative: adopt.
5. **Separation of concerns (Block/Request/Operator/Action/Event)** —
   the Block/Request split is the strongest single idea in Lowdefy.
   Access conflates them; downstream IR should not. Operators-as-tree
   and Events-as-ordered-Actions also worth borrowing. Tentative: adopt
   with C#-shaped operators (not JS).
6. **Escape hatches** — Lowdefy's `_js` operator does not map to a C#
   target. Better escape hatch: "name a partial-class method." Tentative:
   replace, not borrow.
7. **Code-gen ergonomics** — domain-DSL with first-class types pushes
   complexity into the schema rather than the emitter. Lowdefy's generic
   primitives push complexity into the emitter (it has to figure out
   "this Block tree is actually a master-detail page"). For a bounded
   target this is the wrong tradeoff. Tentative: domain DSL wins.
8. **Mapping fidelity from Access (master/detail, modal, continuous,
   subreports, parameter forms, switchboards, data macros, transactional
   `Copy from Prior FY`)** — domain DSL with first-class types maps these
   1:1. Lowdefy-style primitives require "discover the pattern" at
   codegen time. Domain DSL wins.

### The architectural fork I most want second opinions on

Three options for the Blazor-target IR layer:

- **Option A — Flat high-level domain DSL.** First-class types per
  Access pattern (`MasterDetailListPage`, `ModalDetailForm`, etc.).
  Compact for AI authoring. Schema validates the small surface. Codegen
  is a straight emit. Cost: less flexibility for cases outside scope;
  every new pattern requires a schema change.
- **Option B — Two-stage IR.** AI authors a high-level domain DSL (same
  as Option A); codegen *desugars* into a Lowdefy-style primitive IR
  (Blocks/Requests/Operators/Actions/Events) before emitting Blazor.
  AI surface stays small; codegen stays regular; new patterns can be
  added either by extending the high-level DSL or by composing
  primitives manually for one-off pages. Cost: two schemas to maintain,
  but the low-level is internal.
- **Option C — Flat Lowdefy-style primitive IR.** AI authors primitives
  directly. Maximum flexibility; biggest AI surface. Probably the
  wrong tradeoff for a bounded Access→Blazor migration, but the brief's
  seed thoughts deserve fair evaluation.

My tentative recommendation is **Option B** (two-stage), but I am
ambivalent. The maintenance cost of two schemas could be higher than
the flexibility benefit, especially early when the pattern set is
small.

### Borrowing-vs-rejecting splits I would propose to the human

Adopt from Lowdefy:
- Block/Request separation as a conceptual split (even if not the exact
  Lowdefy primitive names).
- Operator catalog as composable expression tree, with C#-shaped
  operators (`Nz`, `IIf`, `DLookup`, `DCount`, `DSum`, `Format`, plus
  standard arithmetic/comparison/logical).
- Events as ordered Actions with `try`/`catch` and debounce semantics.
- `_ref`-style cross-file reuse.
- Schema validation on every property; reject unknown properties.
- Explicit `page` vs `modal` vs `drawer` distinction at the page type
  level.

Reject from Lowdefy:
- `_js` operator (use partial-class C# methods as the escape hatch).
- Generic connector abstraction (EF Core against SQL Server / Postgres
  is fixed; "named query" / "named command" is enough).
- Generic Block composition for Reports (use first-class
  `Report`/`Subreport`/`GroupLevel` types).
- The JS-shaped operator names where C# idioms differ.

### Specific things you can disagree about

- Whether Option B's two-schema maintenance cost actually pays off, or
  whether a flat domain DSL (Option A) with explicit "fall through to
  hand-written Blazor for one-off pages" is the right pragmatic call.
- Whether the brief's "50 lines vs 500" pitch for Lowdefy genuinely
  applies to a bounded Access→Blazor migration where the domain types
  can be tighter than Lowdefy's generic Blocks.
- Whether reports should be first-class IR types at all, or expressed
  as a separate domain (since Blazor + a PDF renderer is materially
  different from Blazor + interactive form pages).
- Whether the workflow-package format (prose-heavy markdown) should be
  retained as the AI-authored layer with codegen reading both packages
  AND IR YAML, or whether the packages should be **replaced** by the
  IR YAML once the IR matures.


=========================================================================
DELIVERABLE
=========================================================================

Produce the six-section markdown document the brief asks for, but adapt
section 1 to "Context as provided" since you cannot read the codebase
directly — synthesize from what is included above and flag uncertainty.

Specifically:

1. **Context as provided** — your one-or-two-paragraph synthesis of what
   exists in this repo, what does not, and what the question is. Flag
   uncertainty about parts you would need to read in sibling repos.
2. **Lowdefy-alignment scorecard** — the eight dimensions, each with a
   short verdict (strong / aligned / weak / divergent-but-justified) and
   one or two sentences of reasoning. Disagree with my tentative verdicts
   where you see fit; cite the specific reason.
3. **Recommendations to adopt** — Lowdefy patterns worth importing in
   priority order. Each with a concrete sketch of what the change would
   look like.
4. **Recommendations to reject** — Lowdefy patterns that would be wrong
   here, with reasoning.
5. **Open questions** — places where the right answer depends on a
   decision the human has not yet made. Frame each decision explicitly.
   At minimum, address: (a) Option A vs Option B vs Option C above, with
   your recommended choice; (b) whether reports should be first-class IR
   types or a separate domain; (c) whether workflow packages should be
   retained, supplemented, or replaced once the IR matures.
6. **Suggested next steps** — a short ordered list of small, reversible
   changes the human could try first, before committing to a larger
   refactor.

If you are uncertain because you cannot read a sibling repo, name what
files you would need rather than speculating.
