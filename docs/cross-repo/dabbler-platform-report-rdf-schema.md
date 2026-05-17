# `report.rdf.json` Schema Reference

> **Snapshot of:** `../dabbler-platform` @ commit `aa77020e1ca0` on `2026-05-16`
> **Captured on:** `2026-05-17`
> **Regenerate when:** the platform's component library changes meaningfully, or when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.

> **Citation conventions used below.** Source-of-truth paths are given as
> `src/Libs/Dabbler.Reports.Core/Rdf/<File>.cs` under `../dabbler-platform/`.
> JSON property names are `camelCase` (per `RdfParser.cs`'s naming policy);
> the corresponding C# property names are PascalCase. Example JSON
> snippets in this document are **synthetic minimal examples authored in
> this repo** — they are not copied from sibling-repo sample files. The
> spec boundary forbids reproducing `../dabbler-platform/`'s sample
> content here; the synthetic snippets exist solely to illustrate the
> shape of each section.
>
> **See also — canonical real-world examples (not reproduced here).**
> Two end-to-end real RDF documents live in `../dabbler-platform/`:
> `src/Apps/AcmeCoffeeOnline.Web/Reports/orders-by-customer.rdf.json` (a
> grouped report with subtotals and a date-range parameter) and
> `src/Apps/AcmeCoffeeOnline.Web/Reports/sales-by-product.rdf.json` (a
> sales-by-product breakdown). Open those files directly in the sibling
> repo when an exhaustive real example is required — they exercise the
> full schema in combination, where the synthetic snippets in this
> document each isolate one section.

## Overview

The `report.rdf.json` format, or Report Definition File (RDF), is a high-level, semantic representation of a report's structure and layout. It describes *what* the report contains (data sources, parameters, fields) and *how* it's organized (a band-based layout with positioned elements). It is intentionally not a direct syntax-tree translation of a source format like MS Access `.rpt` or Crystal Reports `.rpx`. RDF abstracts away vendor-specific details (control IDs, proprietary formula syntax, etc.) in favor of a platform-agnostic model focused on intent — a text field is defined by its data source expression, position, and style, not by the byte sequence in the original Access file.

This abstraction is the critical seam for the Access-to-Blazor migration. `dabbler-access-harvester` parses the low-level Access report format and emits RDF; `dabbler-platform`'s rendering engine consumes RDF to produce a Blazor-based report view. Targeting a well-defined schema decouples the harvester from the platform's renderer, and the platform can render reports from any source that produces valid RDF.

## Top-level structure

A `report.rdf.json` document is a single JSON object. The C# model is `ReportDefinition` (`src/Libs/Dabbler.Reports.Core/Rdf/ReportDefinition.cs`).

| JSON Property | C# Type | Required | Description |
|---|---|---|---|
| `metadata` | `ReportMetadata` | Yes — semantically required | Page size, margins, name, description, defaults. |
| `dataSources` | `List<DataSourceDefinition>` | Yes — must contain ≥ 1 entry | Data sources, typically SQL queries. |
| `parameters` | `List<ParameterDefinition>` | Yes — may be empty | User-prompted parameters for filtering data. |
| `fields` | `List<FieldDefinition>` | Yes — must contain ≥ 1 entry | Maps data-source columns to named, typed report fields. |
| `sorting` | `List<SortDefinition>` | Yes — may be empty | Sort rules applied after grouping. |
| `grouping` | `List<GroupDefinition>` | Yes — may be empty | Data grouping levels, each with optional header/footer bands. |
| `bands` | `List<BandDefinition>` | Yes — may be empty | Core layout definition — every section of the report. |
| `styles` | `List<StyleDefinition>` | Yes — may be empty | Named, reusable styles for report elements. |
| `expressions` | `List<ExpressionDefinition>` | Yes — may be empty | Named, calculated fields. |

Required-vs-may-be-empty distinctions come from `RdfValidator.cs`: the validator rejects a missing or empty `dataSources` / `fields` array, but accepts an empty `parameters`, `sorting`, `grouping`, `bands`, `styles`, or `expressions`.

## Per-section reference

### Metadata

Defines global properties for the report.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/ReportMetadata.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | string | Yes | User-visible report title. |
| `description` | string | No | Longer description of the report's purpose. |
| `author` | string | No | Report author. |
| `version` | string | No (default `"1.0"`) | Schema version string. |
| `pageWidth` | number | No (default `8.5`) | Page width in inches. |
| `pageHeight` | number | No (default `11.0`) | Page height in inches. |
| `orientation` | string | No (default `"Portrait"`) | `"Portrait"` or `"Landscape"`. |
| `marginTop` | number | No (default `1.0`) | Top margin in inches. |
| `marginBottom` | number | No (default `1.0`) | Bottom margin in inches. |
| `marginLeft` | number | No (default `1.0`) | Left margin in inches. |
| `marginRight` | number | No (default `1.0`) | Right margin in inches. |
| `defaultFontFamily` | string | No | Default font for elements without a style. |
| `defaultFontSize` | number | No | Default font size (points). |

**Validation** (`RdfValidator.cs`): `name` must be non-empty; page dimensions must be positive; margins must not exceed page dimensions.

**Synthetic example:**
```json
"metadata": {
  "name": "Synthetic Sample Report",
  "pageWidth": 8.5,
  "pageHeight": 11.0,
  "orientation": "Portrait",
  "marginTop": 0.5,
  "marginBottom": 0.5
}
```

### Data sources

Defines where the report's data comes from.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/DataSourceDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | string | Yes | Unique name used to reference this data source (e.g., `"main"`). |
| `connectionName` | string | No | Named connection string resolved at runtime. |
| `providerType` | string | No | `"SqlServer"`, `"OleDb"`, `"Custom"`, etc. |
| `query` | string | Yes | SQL query; `@` prefix denotes parameter placeholders. |

**Validation** (`RdfValidator.cs`): at least one data source is required; each must have a unique, non-empty `name` and a non-empty `query`.

**Synthetic example:**
```json
"dataSources": [
  {
    "name": "main",
    "connectionName": "AppDb",
    "providerType": "SqlServer",
    "query": "SELECT Id, Name, Total FROM SampleTable WHERE Date BETWEEN @StartDate AND @EndDate"
  }
]
```

### Parameters

Declares user-facing parameters used to filter the report's data source query.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/ParameterDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | string | Yes | Unique identifier used in query binding (e.g., `"StartDate"`). |
| `dataType` | string | Yes | .NET type name (`"DateTime"`, `"Int32"`, `"String"`, …). |
| `controlType` | string | No (default `"TextBox"`) | One of: `"TextBox"`, `"DatePicker"`, `"ComboBox"`, `"MultiSelectList"`, `"CheckBox"`, `"NumericUpDown"`. From `ParameterControlType` enum. |
| `promptText` | string | No | User-visible label for the parameter prompt. |
| `defaultValue` | string | No | Static value or dynamic expression (e.g., `"Today"`, `"FirstOfMonth"`). |
| `isRequired` | boolean | No (default `true`) | When `true`, the user must supply a value. |
| `validation` | object | No | `ParameterValidation`: `minValue`, `maxValue`, `pattern`. |
| `lookupQuery` | string | No | SQL query populating choices for `ComboBox` / `MultiSelectList`. |
| `lookupDisplayField` | string | No | Display column from the lookup query result. |
| `lookupValueField` | string | No | Value column from the lookup query result. |
| `dependsOn` | string | No | Parent parameter name for cascading-parameter dependencies. |
| `sortOrder` | number | No | Display order in the parameter form. |

**Validation** (`RdfValidator.cs`): each parameter must have a unique, non-empty `name` and `dataType`; `lookupQuery` is required when `controlType` is `ComboBox` or `MultiSelectList`.

#### Nested object: `validation`

The `validation` property on a parameter is a `ParameterValidation` object.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/ParameterDefinition.cs` (class `ParameterValidation` near line 73).*

| Property | Type | Required | Description |
|---|---|---|---|
| `minValue` | string | No | Minimum value, serialized as a string. Use for numeric or date parameters; the platform parses against `dataType`. |
| `maxValue` | string | No | Maximum value, serialized as a string. |
| `pattern` | string | No | Regex pattern applied to the parameter's string-form value (typically used with `dataType: "String"`). |

All three fields are optional and nullable; omitting the entire `validation` object is equivalent to no validation.

**Synthetic example:**
```json
"parameters": [
  {
    "name": "StartDate",
    "dataType": "DateTime",
    "controlType": "DatePicker",
    "promptText": "Start date",
    "isRequired": true,
    "validation": { "minValue": "2020-01-01", "maxValue": "2030-12-31" },
    "sortOrder": 1
  }
]
```

### Fields

Maps data-source query columns to named, typed report fields available to bands.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/FieldDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | string | Yes | Name used to reference this field in expressions and elements. |
| `dataType` | string | Yes | .NET type name (`"String"`, `"Decimal"`, `"DateTime"`, …). |
| `sourceColumn` | string | No | Column name from the query result. Falls back to `name` when omitted. |
| `formatString` | string | No | Default .NET format string for this field (e.g., `"C2"`, `"d"`, `"N0"`). May be overridden per element. |

**Validation** (`RdfValidator.cs`): at least one field is required; each must have a unique, non-empty `name` and a `dataType`.

**Synthetic example:**
```json
"fields": [
  { "name": "Id", "dataType": "Int32" },
  { "name": "Total", "dataType": "Decimal", "formatString": "C2" }
]
```

### Sorting

Sort rules applied to the data source result **after** grouping levels are applied. Group-level sort direction is configured on the `GroupDefinition` itself (see Grouping below); `sorting` adds row-level ordering inside each group.

*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/SortDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `fieldName` | string | Yes | Name of the field to sort by. |
| `direction` | string | No (default `"Ascending"`) | `"Ascending"` or `"Descending"`. From `SortDirection` enum. |

**Validation** (`RdfValidator.cs`): each `fieldName` must reference a field defined in the top-level `fields` array. The `sorting` array itself may be empty.

**Synthetic example:**
```json
"sorting": [
  { "fieldName": "Total", "direction": "Descending" },
  { "fieldName": "Id", "direction": "Ascending" }
]
```

### Grouping

Defines data grouping levels — controls break logic and the rendering of group header/footer bands.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/GroupDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `fieldName` | string | Conditional | Field to group on. Either this or `groupExpression` must be set. |
| `groupExpression` | string | Conditional | Expression to group on (e.g., `"Left([FirstName], 1)"`). Either this or `fieldName` must be set. |
| `sortDirection` | string | No (default `"Ascending"`) | `"Ascending"` or `"Descending"`. |
| `headerBandName` | string | No | Name of a `GroupHeader` band rendered for this group. |
| `footerBandName` | string | No | Name of a `GroupFooter` band rendered for this group. |
| `keepTogether` | boolean | No (default `false`) | When `true`, the engine tries to keep the group header and first detail row on the same page. |
| `resetAggregates` | boolean | No (default `true`) | When `true`, aggregate expressions reset at this group boundary. |
| `pageBreakBefore` | boolean | No (default `false`) | Inserts a page break before each new group instance (except the first). |
| `pageBreakAfter` | boolean | No (default `false`) | Inserts a page break after each group. |

**Validation** (`RdfValidator.cs`): exactly one of `fieldName` or `groupExpression` must be set; referenced band names (`headerBandName`, `footerBandName`) must exist in the top-level `bands` array and be of the correct band type (`GroupHeader` / `GroupFooter`).

**Synthetic example:**
```json
"grouping": [
  {
    "fieldName": "Category",
    "sortDirection": "Ascending",
    "headerBandName": "categoryHeader",
    "footerBandName": "categoryFooter",
    "resetAggregates": true,
    "pageBreakBefore": false
  }
]
```

### Bands

Bands are the horizontal sections that compose the report layout. The engine processes them in a fixed order (Report Header → Page Header → Group Headers → Detail → Group Footers → Page Footer → Report Footer; `NoData` substitutes for `Detail` when the query returns zero rows).

*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/BandDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | string | Yes | Unique band name. |
| `type` | string | Yes | One of `"ReportHeader"`, `"PageHeader"`, `"GroupHeader"`, `"Detail"`, `"GroupFooter"`, `"PageFooter"`, `"ReportFooter"`, `"NoData"`. From `BandType` enum. |
| `height` | number | Yes | Band height in inches. |
| `groupName` | string | Conditional | For `GroupHeader` / `GroupFooter`, links to a grouping entry by its `fieldName` or `groupExpression`. |
| `printWhenExpression` | string | No | Expression that must evaluate to `true` for the band to render. |
| `pageBreakBefore` | boolean | No (default `false`) | Insert a page break before this band. |
| `pageBreakAfter` | boolean | No (default `false`) | Insert a page break after this band. |
| `backgroundColor` | string | No | Hex color (e.g., `"#F2F2F2"`) drawn behind all band elements. On a `Detail` band combined with `alternateBackgroundColor`, this is used on even-indexed rows. |
| `alternateBackgroundColor` | string | No | Hex color used on odd-indexed `Detail` rows for "green bar" striping. Ignored on non-Detail bands. Row alternation is continuous across the report (does not reset at group boundaries). |
| `elements` | array | Yes (may be empty) | `ElementDefinition` items positioned within this band. |

**Validation** (`RdfValidator.cs`): each band must have a unique `name`; singleton band types (`ReportHeader`, `PageHeader`, `Detail`, `PageFooter`, `ReportFooter`, `NoData`) can appear at most once; `groupName` on a group band must match a defined grouping entry.

**Synthetic example:**
```json
"bands": [
  {
    "name": "pageHeader",
    "type": "PageHeader",
    "height": 0.4,
    "elements": [ /* ElementDefinition items */ ]
  },
  {
    "name": "detail",
    "type": "Detail",
    "height": 0.3,
    "backgroundColor": "#FFFFFF",
    "alternateBackgroundColor": "#F7F7F7",
    "elements": [ /* ElementDefinition items */ ]
  }
]
```

### Elements

Elements are positioned controls placed within a band: text fields, static labels, lines, rectangles, images, subreports.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/ElementDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `type` | string | Yes | One of `"TextField"`, `"StaticText"`, `"Line"`, `"Rectangle"`, `"Image"`, `"Subreport"`. From `ElementType` enum. |
| `x` | number | Yes | X-position in inches, relative to the band left edge. |
| `y` | number | Yes | Y-position in inches, relative to the band top edge. |
| `width` | number | Yes | Width in inches. |
| `height` | number | Yes | Height in inches. |
| `styleName` | string | No | Name of a style in the top-level `styles` array. |
| `fieldName` | string | No | Shortcut for binding a `TextField` to a single field; equivalent to `expression: "[FieldName]"`. |
| `expression` | string | No | Expression for dynamic content (field reference, calculation, aggregate, or built-in like `{PageNumber}`). |
| `value` | string | No | Literal text for a `StaticText` element. |
| `formatString` | string | No | .NET format string for this element; overrides the field's `formatString`. |
| `imageSource` | string | No | Path or expression for `Image` elements. |
| `imageScaleMode` | string | No (default `"ScaleToFit"`) | `"ScaleToFit"`, `"Stretch"`, `"ActualSize"`, `"Tile"`. From `ImageScaleMode` enum. |
| `printWhenExpression` | string | No | Expression that must evaluate to `true` for the element to render. |
| `subreportPath` | string | No | Path to a subreport RDF file (for `Subreport` elements). |
| `subreportParameters` | object | No | Map of parameter-name → expression passed to the subreport. |
| `subreportDataSourceName` | string | No | Subreport data-source name when different from the parent. |
| `lineDirection` | string | No (default `"Horizontal"`) | `"Horizontal"` or `"Vertical"` for `Line` elements. From `LineDirection` enum. |
| `canGrow` | boolean | No (default `false`) | When `true`, the element and band expand to fit wrapped text (text elements with `TextWrap` style only). |
| `canShrink` | boolean | No (default `false`) | When `true`, the element height contracts when text is shorter than the defined height (same constraints as `canGrow`). |
| `runningTotal` | string | No (default `"None"`) | `"None"`, `"OverGroup"`, `"OverAll"`. From `RunningTotalMode` enum. Valid on `TextField` in `Detail` band only. |
| `hideDuplicates` | boolean | No (default `false`) | When `true`, only the first occurrence of a value is rendered; subsequent rows with the same formatted value show blank. Valid on `TextField` in `Detail` band only. |

**Validation** (`RdfValidator.cs`): `(x, y, width, height)` must define a rectangle inside the printable area (page dimensions minus margins); `styleName` must refer to a defined style.

**Synthetic example:**
```json
"elements": [
  {
    "type": "StaticText",
    "x": 0, "y": 0, "width": 4.0, "height": 0.3,
    "value": "Sample Report",
    "styleName": "reportTitle"
  },
  {
    "type": "TextField",
    "x": 4.0, "y": 0, "width": 2.5, "height": 0.3,
    "expression": "{Now}",
    "formatString": "g",
    "styleName": "dateStamp"
  }
]
```

### Styles

Named, reusable collections of formatting properties.
*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/StyleDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | string | Yes | Unique style name. |
| `fontFamily` | string | No | Font family name (e.g., `"Arial"`). |
| `fontSize` | number | No | Font size in points. |
| `isBold` | boolean | No | Bold weight. |
| `isItalic` | boolean | No | Italic style. |
| `isUnderline` | boolean | No | Underline text decoration. |
| `foregroundColor` | string | No | Hex color for text or lines. |
| `backgroundColor` | string | No | Hex color for the element background. |
| `horizontalAlignment` | string | No | `"Left"`, `"Center"`, `"Right"`. From `HorizontalTextAlignment` enum. |
| `verticalAlignment` | string | No | `"Top"`, `"Middle"`, `"Bottom"`. From `VerticalTextAlignment` enum. |
| `textWrap` | boolean | No | When `true`, text wraps within element bounds. Required for `canGrow` / `canShrink` element behavior to take effect. |
| `border` | string | No | Shorthand for all four sides (e.g., `"1px solid #000000"`). Individual sides below override. |
| `borderTop` | string | No | Top border specification; overrides `border` for the top side. |
| `borderBottom` | string | No | Bottom border specification; overrides `border` for the bottom side. |
| `borderLeft` | string | No | Left border specification; overrides `border` for the left side. |
| `borderRight` | string | No | Right border specification; overrides `border` for the right side. |
| `padding` | number | No | Shorthand padding (inches) for all four sides. Individual sides below override. |
| `paddingTop` | number | No | Top padding in inches; overrides `padding` for the top side. |
| `paddingBottom` | number | No | Bottom padding in inches; overrides `padding` for the bottom side. |
| `paddingLeft` | number | No | Left padding in inches; overrides `padding` for the left side. |
| `paddingRight` | number | No | Right padding in inches; overrides `padding` for the right side. |

**Validation** (`RdfValidator.cs`): each style must have a unique, non-empty `name`.

**Synthetic example:**
```json
"styles": [
  {
    "name": "reportTitle",
    "fontFamily": "Segoe UI",
    "fontSize": 16,
    "isBold": true,
    "horizontalAlignment": "Left"
  }
]
```

### Expressions

Named calculated expressions referenced by name from elements or other expressions. Supports field references, parameters, aggregate functions, and arithmetic operators.

*Source model: `src/Libs/Dabbler.Reports.Core/Rdf/ExpressionDefinition.cs`.*

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | string | Yes | Expression name; used by elements via `expression: "[Name]"` and by other expressions. |
| `expression` | string | Yes | Expression text (e.g., `"[Quantity] * [UnitPrice]"`, `"Sum([Total])"`). |
| `resultType` | string | No | .NET type name describing the expression's result (`"Decimal"`, `"Int32"`, …). |

**Validation** (`RdfValidator.cs`): each expression must have a unique, non-empty `name`; `expression` must be non-empty. The `expressions` array may be empty.

**Synthetic example:**
```json
"expressions": [
  {
    "name": "LineExtended",
    "expression": "[Quantity] * [UnitPrice]",
    "resultType": "Decimal"
  }
]
```

## Worked example: a synthetic minimal report

The synthetic snippets above can be combined into a single minimal valid RDF document. Conceptually, this mirrors how an MS Access report is configured:

1. **Report and page setup.** The `metadata` object plays the role of the Page Setup dialog — page dimensions, margins, defaults. `name` becomes the report title.
2. **Record source and parameters.** `dataSources` defines the report's "Record Source" property — a SQL query against the table. `parameters` defines the prompt the user sees before the report runs; the query's `WHERE` clause binds them by `@`-prefix.
3. **Grouping and sorting.** `grouping` corresponds to Access's "Group, Sort, and Total" pane. `sorting` adds row-level order inside each group. Group definitions reference `GroupHeader` / `GroupFooter` band names to bring those sections into the layout.
4. **Layout and controls.** `bands` mirror Access's report-designer sections. `reportHeader` / `pageHeader` / `detail` / group bands / `pageFooter` / `reportFooter` each hold `elements`. `StaticText` elements correspond to Access Labels; `TextField` elements correspond to Access Text Boxes bound to fields or expressions. Group-footer aggregates such as `Sum([Total])` are typically defined as a named expression in `expressions` and referenced by a `TextField` element in the appropriate `GroupFooter` band.

### Full end-to-end synthetic example

The document below is a complete, internally consistent, synthetic `report.rdf.json`. Every property used here also appears in the per-section tables above. No content is copied from `../dabbler-platform/`. An AI generator can model against this shape, then add or remove fields per the per-section tables as the source Access report's complexity demands.

```json
{
  "metadata": {
    "name": "Sales By Category",
    "description": "Sample synthetic report demonstrating grouping, sorting, and aggregates.",
    "version": "1.0",
    "pageWidth": 8.5,
    "pageHeight": 11.0,
    "orientation": "Portrait",
    "marginTop": 0.5,
    "marginBottom": 0.5,
    "marginLeft": 0.75,
    "marginRight": 0.75,
    "defaultFontFamily": "Segoe UI",
    "defaultFontSize": 9
  },
  "dataSources": [
    {
      "name": "main",
      "connectionName": "AppDb",
      "providerType": "SqlServer",
      "query": "SELECT Category, ProductName, Quantity, UnitPrice FROM SampleSales WHERE OrderDate BETWEEN @StartDate AND @EndDate"
    }
  ],
  "parameters": [
    {
      "name": "StartDate",
      "dataType": "DateTime",
      "controlType": "DatePicker",
      "promptText": "Start date",
      "isRequired": true,
      "sortOrder": 1
    },
    {
      "name": "EndDate",
      "dataType": "DateTime",
      "controlType": "DatePicker",
      "promptText": "End date",
      "isRequired": true,
      "sortOrder": 2
    }
  ],
  "fields": [
    { "name": "Category", "dataType": "String" },
    { "name": "ProductName", "dataType": "String" },
    { "name": "Quantity", "dataType": "Int32", "formatString": "N0" },
    { "name": "UnitPrice", "dataType": "Decimal", "formatString": "C2" }
  ],
  "sorting": [
    { "fieldName": "ProductName", "direction": "Ascending" }
  ],
  "grouping": [
    {
      "fieldName": "Category",
      "sortDirection": "Ascending",
      "headerBandName": "categoryHeader",
      "footerBandName": "categoryFooter",
      "resetAggregates": true
    }
  ],
  "expressions": [
    {
      "name": "LineExtended",
      "expression": "[Quantity] * [UnitPrice]",
      "resultType": "Decimal"
    },
    {
      "name": "CategorySubtotal",
      "expression": "Sum([Quantity] * [UnitPrice])",
      "resultType": "Decimal"
    }
  ],
  "styles": [
    {
      "name": "reportTitle",
      "fontFamily": "Segoe UI",
      "fontSize": 16,
      "isBold": true,
      "horizontalAlignment": "Left"
    },
    {
      "name": "categoryHeaderStyle",
      "fontSize": 11,
      "isBold": true,
      "backgroundColor": "#EFEFEF",
      "padding": 0.05
    },
    {
      "name": "detailTextStyle",
      "fontSize": 9
    },
    {
      "name": "subtotalStyle",
      "fontSize": 10,
      "isBold": true,
      "horizontalAlignment": "Right"
    }
  ],
  "bands": [
    {
      "name": "reportHeader",
      "type": "ReportHeader",
      "height": 0.5,
      "elements": [
        {
          "type": "StaticText",
          "x": 0, "y": 0.1, "width": 5.0, "height": 0.3,
          "value": "Sales By Category",
          "styleName": "reportTitle"
        }
      ]
    },
    {
      "name": "pageHeader",
      "type": "PageHeader",
      "height": 0.3,
      "elements": [
        { "type": "StaticText", "x": 0,    "y": 0, "width": 1.5, "height": 0.25, "value": "Product" },
        { "type": "StaticText", "x": 4.0,  "y": 0, "width": 1.0, "height": 0.25, "value": "Qty" },
        { "type": "StaticText", "x": 5.0,  "y": 0, "width": 1.0, "height": 0.25, "value": "Unit" },
        { "type": "StaticText", "x": 6.0,  "y": 0, "width": 1.0, "height": 0.25, "value": "Extended" }
      ]
    },
    {
      "name": "categoryHeader",
      "type": "GroupHeader",
      "height": 0.3,
      "groupName": "Category",
      "elements": [
        {
          "type": "TextField",
          "x": 0, "y": 0.05, "width": 4.0, "height": 0.2,
          "fieldName": "Category",
          "styleName": "categoryHeaderStyle"
        }
      ]
    },
    {
      "name": "detail",
      "type": "Detail",
      "height": 0.22,
      "elements": [
        { "type": "TextField", "x": 0,   "y": 0, "width": 4.0, "height": 0.2, "fieldName": "ProductName", "styleName": "detailTextStyle" },
        { "type": "TextField", "x": 4.0, "y": 0, "width": 1.0, "height": 0.2, "fieldName": "Quantity",    "styleName": "detailTextStyle" },
        { "type": "TextField", "x": 5.0, "y": 0, "width": 1.0, "height": 0.2, "fieldName": "UnitPrice",   "styleName": "detailTextStyle" },
        { "type": "TextField", "x": 6.0, "y": 0, "width": 1.0, "height": 0.2, "expression": "[LineExtended]", "formatString": "C2", "styleName": "detailTextStyle" }
      ]
    },
    {
      "name": "categoryFooter",
      "type": "GroupFooter",
      "height": 0.3,
      "groupName": "Category",
      "elements": [
        { "type": "StaticText", "x": 4.0, "y": 0.05, "width": 2.0, "height": 0.2, "value": "Category subtotal:", "styleName": "subtotalStyle" },
        { "type": "TextField",  "x": 6.0, "y": 0.05, "width": 1.0, "height": 0.2, "expression": "[CategorySubtotal]", "formatString": "C2", "styleName": "subtotalStyle" }
      ]
    },
    {
      "name": "pageFooter",
      "type": "PageFooter",
      "height": 0.25,
      "elements": [
        { "type": "TextField", "x": 6.0, "y": 0.05, "width": 1.0, "height": 0.2, "expression": "{PageNumber}" }
      ]
    }
  ]
}
```

## Generation contract for AI consumers

An AI agent generating a `report.rdf.json` document from a harvested Access report representation must adhere to these rules:

- **Property naming.** JSON property names MUST be `camelCase` (e.g., `pageWidth`). The platform deserializer enforces this (`src/Libs/Dabbler.Reports.Core/Rdf/RdfParser.cs`).
- **Required root properties.** `metadata`, `dataSources`, and `fields` are semantically required; `dataSources` and `fields` must be non-empty arrays (`RdfValidator.cs`).
- **Unique naming.** Values of `name` inside `dataSources`, `parameters`, `fields`, `bands`, `styles`, and `expressions` MUST be unique within their respective arrays (`RdfValidator.cs`).
- **Enum validity.** Properties typed as enums MUST use one of the prescribed string values. For example, `BandDefinition.type` must be one of the values from `BandType` (e.g., `"ReportHeader"`, `"Detail"`, `"GroupFooter"`); `ElementDefinition.type` must be one of the values from `ElementType`; `SortDefinition.direction` and `GroupDefinition.sortDirection` use `SortDirection`; `ParameterDefinition.controlType` uses `ParameterControlType`.
- **Reference integrity.** Cross-references must point to defined names:
  - `ElementDefinition.styleName` → a `name` in `styles`.
  - `GroupDefinition.headerBandName` / `footerBandName` → a `name` in `bands` of the matching `GroupHeader` / `GroupFooter` type.
  - `BandDefinition.groupName` (on a group band) → a `fieldName` or `groupExpression` in `grouping`.
  - `SortDefinition.fieldName` → a `name` in `fields`.
  - `ParameterDefinition.dependsOn` → a `name` of another parameter.
  (`src/Libs/Dabbler.Reports.Core/Rdf/RdfValidator.cs`)
- **Band uniqueness.** A report can have at most one of each singleton band type: `ReportHeader`, `PageHeader`, `Detail`, `PageFooter`, `ReportFooter`, `NoData` (`RdfValidator.cs`). Multiple `GroupHeader` / `GroupFooter` bands are allowed, one per group.
- **Element positioning.** All element coordinates (`x`, `y`, `width`, `height`) must define a rectangle inside the printable area (page dimensions minus margins) (`RdfValidator.cs`).
- **Element-type constraints.** `runningTotal` and `hideDuplicates` are valid only on `TextField` elements in a `Detail` band (`ElementDefinition.cs`). `imageSource` / `imageScaleMode` are meaningful only on `Image` elements; `subreportPath` / `subreportParameters` / `subreportDataSourceName` only on `Subreport` elements; `lineDirection` only on `Line` elements; `canGrow` / `canShrink` only effective on text elements with `TextWrap` styles.
- **Parameter binding.** Parameter placeholders in `dataSources[].query` use the `@` prefix; the `name` in `parameters[]` is the token following the `@`. ComboBox / MultiSelectList parameters MUST supply a `lookupQuery` (validator-enforced); use `lookupDisplayField` / `lookupValueField` to project arbitrary display/value columns.
