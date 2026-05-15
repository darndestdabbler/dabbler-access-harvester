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
