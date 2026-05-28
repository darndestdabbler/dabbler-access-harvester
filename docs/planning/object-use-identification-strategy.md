# Object Use Identification Strategy

> **Status:** Authored in Session 1 of `harvester-object-use-identifier`.
> **Authority:** This document is the design specification for the
> `AccessHarvester identify-usage` subcommand. Session 2 implements it
> exactly as described here; divergences require human approval.

---

## 1. Purpose

The `identify-usage` subcommand analyses KB/IR output from a harvested
Access database and produces two artefacts:

1. `object-use.json` — intermediate structured findings per object
   (classification, confidence, fired signals, clarifying questions).
2. `object-use-checklist.json` — a `uat-checklist-editor`-compatible
   JSON checklist the developer and user review together.

All input signals come from already-harvested KB/IR files. No Access COM
is required.

---

## 2. Usage Classification Taxonomy

Seven mutually exclusive categories assigned to each object:

| Category | Description | Typical objects |
|---|---|---|
| **MainDataEntry** | Form backed by a transactional table; VBA handlers are CRUD-focused (BeforeUpdate, AfterUpdate, OnDelete) | `frmClientDetail`, `frmProviderDetail` |
| **LookupReference** | Form or table holding code lists; referenced as RowSource by combo/list controls; few or no FK children | `tlkpStates`, `tlkpPlanStatuses`, combo-only forms |
| **Reporting** | Report or select-only query not used as the record source for a form; presents data without writing it | `rptClientProfile`, `qryClientList` |
| **NavigationShell** | Form with no RecordSource; opens other forms or reports via VBA/macros (DoCmd.OpenForm, OpenReport) | `frmSwitchboard`, `frmReportMenu` |
| **StandaloneUtility** | Table or query not referenced as RecordSource, RowSource, VBA target, macro target, or FK peer; may be used in ad-hoc queries or exports | staging tables, scratch queries |
| **PossiblyUnused** | Object absent from all six signal types; no reference found anywhere in the KB/IR | — |
| **Unknown** | Insufficient signals to classify with confidence ≥ 0.2, or classification is contradictory | form with no RecordSource and no VBA |

**Confidence** is a 0.0–1.0 score:
- ≥ 0.8 = High
- 0.5–0.8 = Medium
- 0.2–0.5 = Low
- < 0.2 = Unknown (category becomes `Unknown` regardless of signals)

---

## 3. Signal Families

Six signal collectors feed the classifier. All read from `ir/` files.

### 3.1 RecordSourceCollector

- **Source:** `ir/Forms/*.ir.json`, `ir/Reports/*.ir.json`
- **Extraction:** `Properties.RecordSource` string value
- **Produces:** A set of `(referrer, recordSourceName)` pairs where
  `recordSourceName` is a table or query name.
- **Supports classifying:** MainDataEntry (if table has FK children),
  LookupReference (if table matches lookup pattern), Reporting (reports
  always qualify).

### 3.2 RowSourceCollector

- **Source:** `ir/Forms/*.ir.json` Children control nodes
- **Extraction:** Any child node with `RowSource` in `Properties` where
  the value is a table or query name (not a value-list literal and not
  an SQL expression starting with `SELECT`).
- **Produces:** A set of `(formName, controlName, rowSourceName)` triples.
- **Supports classifying:** LookupReference for the referenced tables/queries.

### 3.3 NavigationGraphReader

- **Source:** `ir/Macros/*.ir.json` (action nodes with `Action:
  "OpenForm"`, `"OpenReport"`, `"OpenQuery"` and `Argument[0]` = target
  object name) and `VbaCode` strings in form/module IR files (regex for
  `DoCmd\.Open(?:Form|Report|Query)\s*["(]([^,")\s]+)`)
- **Produces:** A set of directed edges `(sourceObject, targetObject,
  edgeType)` where `edgeType ∈ { "OpenForm", "OpenReport", "OpenQuery" }`.
- **Supports classifying:** NavigationShell (source has no RecordSource
  and emits navigation edges), MainDataEntry (target is a detail form
  opened from a search form).

### 3.4 VbaReferenceReader

- **Source:** `VbaCode` in `ir/Forms/*.ir.json`,
  `ir/Reports/*.ir.json`, `ir/ModulesAndClasses/*.ir.json`
- **Extraction:**
  - DoCmd.Open* patterns (captured by NavigationGraphReader above)
  - DCount/DLookup/DSum/DMin/DMax domain-function table references:
    regex `\bD(?:Count|Lookup|Sum|Min|Max)\s*\(\s*[^,]+,\s*["']([^"']+)["']`
  - CurrentDb().OpenRecordset table references:
    regex `OpenRecordset\s*\(\s*["']([^"']+)["']`
- **Produces:** A set of `(sourceObject, referencedTableOrQuery)` pairs.
- **Supports classifying:** StandaloneUtility detection (table IS
  referenced from VBA even without formal RecordSource binding).

### 3.5 QueryTableReferenceReader

- **Source:** `ir/Queries/*.ir.json` field `Sql`
- **Extraction:** Simple FROM-clause parse: extract all `FROM <name>`,
  `JOIN <name>`, and `FROM <name1>, <name2>` table/query names using
  regex. Handles aliases: `FROM tblClients AS c` → `tblClients`.
- **Produces:** A set of `(queryName, referencedTableOrQuery)` pairs.
- **Supports classifying:** Reporting vs. StandaloneUtility for queries;
  LookupReference for tables that appear only as JOIN targets.

### 3.6 RelationshipGraphReader

- **Source:** `ir/Relationships/*.ir.json`
- **Extraction:** FK edges from relationship files; skip
  `MSysNavPane*` system relationships.
- **Produces:** A set of `(parentTable, childTable)` pairs.
- **Supports classifying:** MainDataEntry (table has FK children →
  transactional); LookupReference (table has FK parents only, or is
  parent-only with small column count).

---

## 4. Classification Rules

The classifier takes the six signal collections for the target object
and applies these rules in order. The first matching rule wins.

### Forms

| Rule | Condition | Category | Base confidence |
|---|---|---|---|
| F1 | Has RecordSource → transactional table (FK child in relationship graph) AND has VBA CRUD handlers | MainDataEntry | 0.85 |
| F2 | Has RecordSource → transactional table (FK child) but no CRUD VBA handlers | MainDataEntry | 0.65 |
| F3 | Has RecordSource → lookup table (table name matches `tlkp` or `_Lookup` pattern, or table has ≤ 3 columns with no FK children) | LookupReference | 0.75 |
| F4 | Has RecordSource → query | Reporting or MainDataEntry | 0.55 (sub-classify by query type) |
| F5 | No RecordSource AND has navigation edges (opens other forms/reports) | NavigationShell | 0.80 |
| F6 | No RecordSource AND no navigation edges AND has VBA code | Unknown | 0.30 |
| F7 | No RecordSource AND no navigation edges AND no VBA | PossiblyUnused | 0.70 |
| F8 | Named `*Param*`, `*Filter*`, `*Criteria*`, `*Dialog*` | (unknown intent, mark question) | — |

### Reports

| Rule | Condition | Category | Base confidence |
|---|---|---|---|
| R1 | Has RecordSource (any) | Reporting | 0.90 |
| R2 | No RecordSource but opened by VBA/macro in another object | Reporting | 0.70 |
| R3 | No RecordSource and not opened anywhere | PossiblyUnused | 0.80 |

### Queries

Access `QueryTypeCode` values: 0 = Select, 1 = Crosstab, 2 = Delete,
3 = Update, 4 = Append, 5 = MakeTable, 6 = Data Definition, 64 = Union,
128 = Pass-through.

| Rule | Condition | Category | Base confidence |
|---|---|---|---|
| Q1 | QueryTypeCode = 1 (Crosstab) | Reporting | 0.70 (mark CrosstabQuery question) |
| Q2 | QueryTypeCode = 0 (Select) AND used as RecordSource for a form | MainDataEntry | 0.75 |
| Q3 | QueryTypeCode = 0 (Select) AND used as RecordSource for a report | Reporting | 0.80 |
| Q4 | QueryTypeCode = 0 (Select) AND not used as RecordSource anywhere AND no parameter markers (`?` or `[`) | Reporting | 0.60 |
| Q5 | Contains parameter markers (`?` or `[...Param...]`) | Reporting | 0.65 |
| Q6 | References no tables (only other queries in FROM) | Unknown | 0.40 (mark QueryReferencesNoTables question) |
| Q7 | Not referenced as RecordSource, RowSource, or in any other query SQL | PossiblyUnused | 0.65 |
| Q8 | QueryTypeCode ∈ {2, 3, 4, 5} (Delete, Update, Append, MakeTable — Action Queries) AND opened by VBA or macro | StandaloneUtility | 0.75 |
| Q9 | QueryTypeCode ∈ {2, 3, 4, 5} (Action Query) AND NOT opened by any VBA or macro | PossiblyUnused | 0.65 |
| Q10 | QueryTypeCode = 64 (Union) — treat identically to Select: apply Q2/Q3/Q4/Q5/Q6/Q7 logic with Union as the query type | same as equivalent Select rule | same base confidence |
| Q11 | QueryTypeCode ∈ {6, 128} (Data Definition, Pass-through) | StandaloneUtility | 0.60 |

> **Notes on rule priority:**
> - Q8/Q9 take priority over Q7 when QueryTypeCode identifies an action
>   query. Action queries perform batch data manipulation (not data
>   retrieval) and are never classified as Reporting.
> - Union queries (Q10) are data-retrieval queries. They follow the same
>   classification path as Select queries (Q2–Q7): if used as RecordSource
>   for a form → MainDataEntry; if used as RecordSource for a report →
>   Reporting; if unreferenced → PossiblyUnused. Rules Q2–Q7 apply to
>   `QueryTypeCode ∈ {0, 64}` (Select and Union).
> - DDL and Pass-through (Q11) are infrastructure queries; StandaloneUtility
>   is the appropriate fallback regardless of reference signals.

### Tables

| Rule | Condition | Category | Base confidence |
|---|---|---|---|
| T1 | Has FK children in relationship graph AND referenced as RecordSource | MainDataEntry | 0.85 |
| T2 | Has FK children AND not a RecordSource (intermediary/junction) | MainDataEntry | 0.65 |
| T3 | Is FK parent only (child of other tables) AND ≤ 4 columns | LookupReference | 0.80 |
| T4 | Name matches `tlkp*`, `lkp*`, `*_Lookup*`, `*_Codes*`, `*_Types*` | LookupReference | 0.75 |
| T5 | No FK relationships AND referenced in VBA domain functions | StandaloneUtility | 0.65 |
| T6 | No FK relationships AND no RecordSource references AND no query references | PossiblyUnused | 0.70 |
| T7 | System tables (`MSys*`, `USys*`) | — (excluded from output) | — |

### Confidence Adjustment

After the base confidence is set:
- **+0.05** for each additional corroborating signal type (max +0.15)
- **−0.10** if object name is cryptic (no conventional prefix and < 5 characters)
- **−0.05** if the object has not been updated in > 365 days (only available when the cleanup identifier's date signal is provided)

If adjusted confidence < 0.2 → override category to `Unknown`.

---

## 5. Standalone and Unclear-Intent Detection

### Standalone Object

An object is `StandaloneUtility` when it is not:
- Used as `RecordSource` for any form or report
- Used as `RowSource` for any control
- The target of any navigation edge (not opened by VBA or macro)
- Referenced in any query's SQL (FROM / JOIN)
- Involved in any FK relationship

Standalone objects at confidence < 0.5 become `PossiblyUnused`.

### Unclear-Intent Signals

These signals trigger the generation of clarifying questions:

| Signal | Trigger |
|---|---|
| `NoNavigationEntry` | Form not present as target of any macro or VBA navigation edge |
| `NoRecordSource` | Form has no `RecordSource` property |
| `QueryReferencesNoTables` | Query SQL references only other queries in FROM clause |
| `UnreferencedObject` | Object fired no signals at all (PossiblyUnused) |
| `LookupTablePattern` | Table classified as LookupReference |
| `CrosstabQuery` | Query is Crosstab type |
| `UnrecognizedVbaPattern` | Form/module has VBA but no parseable DoCmd/DFunction calls |
| `CrypticObjectName` | Object name has no conventional prefix and < 5 chars |

---

## 6. Clarifying Question Templates

One question per uncertainty signal. Phrasing uses the object's name
and type as context.

| Signal | Question template |
|---|---|
| `NoNavigationEntry` | "No navigation entry found for {objectType} '{name}' — is it launched from VBA code, another form, or an external application?" |
| `NoRecordSource` | "Form '{name}' has no record source — is it a navigation panel, a parameter/filter dialog, or a utility form?" |
| `QueryReferencesNoTables` | "Query '{name}' references only other queries — is it an intermediate computation step, a parameter source, or can it be merged into its callers?" |
| `UnreferencedObject` | "{objectType} '{name}' is not referenced by any form, report, query, VBA, or macro — is it still in use or can it be excluded from analysis?" |
| `LookupTablePattern` | "Table '{name}' appears to hold reference/lookup values — should it migrate as static reference data, or be replaced with application constants?" |
| `CrosstabQuery` | "Query '{name}' is a crosstab query — is it used for reporting, for driving a form or chart control, or for data exports?" |
| `UnrecognizedVbaPattern` | "{objectType} '{name}' contains VBA that couldn't be auto-classified — can you describe its primary function?" |
| `CrypticObjectName` | "{objectType} '{name}' has a name that doesn't clearly indicate its purpose — can you describe what it is used for?" |

---

## 7. Output: `object-use.json`

Intermediate machine-readable findings. JSON Schema at
`AccessHarvester.Pipeline/UsageIdentification/object-use-schema.json`.

Top-level structure:
```json
{
  "version": "1.0",
  "database": "<database name>",
  "generatedAt": "<ISO 8601>",
  "excludedObjects": ["<name>", ...],
  "objects": [ <ObjectUseFinding>, ... ]
}
```

`ObjectUseFinding` per object:
- `objectType`: `"Form" | "Report" | "Query" | "Table" | "Module" | "Macro"`
- `objectName`: string
- `classification`: taxonomy category string
- `confidence`: 0.0–1.0 float
- `usageSummary`: one-sentence description template (e.g., "Main data
  entry form for client records, backed by tblClients")
- `signals`: array of `Signal` (one per signal type, `fired: true/false`,
  with string evidence list)
- `questions`: array of `ClarifyingQuestion` (one per fired uncertainty
  signal)

---

## 8. Output: `object-use-checklist.json`

`uat-checklist-editor`-compatible JSON. Top-level matches the editor's
schema (`ProjectName`, `Review[]`).

Each `Review` item maps a single `ObjectUseFinding`:

| Checklist field | Value |
|---|---|
| `FunctionalArea` | Object type (`"Form"`, `"Table"`, etc.) |
| `Subarea` | Object name |
| `ItemLabel` | `usageSummary` from the finding |
| `Steps` | One step per clarifying question: `StepNumber` = 1…N, `Action` = question text, `Verifications` = `[]` |
| `IsJudgmentItem` | `true` |
| `Passes` | `false` |
| `Feedback` | `""` |

Objects with zero questions still appear in the checklist with an empty
`Steps` array so the developer can confirm the classification.

`ProjectName` = `"<database name> — Object Use Checklist"`.

---

## 9. Exclusion Input

`--exclude-cleanup <path>` reads an `object-cleanup-checklist.json`
produced by `identify-cleanup`. Any object whose `Passes` field is
`true` in that checklist is added to `excludedObjects[]` and skipped
during signal collection and classification. Objects that are `Passes:
false` or absent are included normally.

The cleanup checklist is optional. When absent, all objects are
processed.

---

## 10. CLI Interface

```
AccessHarvester identify-usage
    --in <harvested-output-root>      # directory containing ir/ and kb/ subdirs
    [--manifest <path>]               # single-manifest mode (alternative to --in)
    [--exclude-cleanup <path>]        # object-cleanup-checklist.json
    [--json-status]                   # emit structured JSON status envelope
```

Outputs written alongside the input root (or beside the manifest when
`--manifest` is used):
- `object-use.json`
- `object-use-checklist.json`

---

## 11. Precedence and Soft-Prereq Notes

- `harvester-object-cleanup-identifier` is a soft prerequisite. When
  no `--exclude-cleanup` is supplied the subcommand processes all
  objects; the `excludedObjects` field in `object-use.json` is an empty
  array.
- `harvester-body-and-query-sql` is a soft prerequisite. When query KB
  files are absent, the `QueryTableReferenceReader` emits no signals for
  any query. The other rules still apply: a query used as RecordSource
  (Q2/Q3) or unreferenced (Q7) can still be classified from those signals
  alone. Only when no other rule fires does the query fall to `Unknown`.
  The `QueryReferencesNoTables` clarifying question is generated only
  when the query's SQL is available and shows no direct table references
  (rule Q6); it is not automatically generated for all queries when KB
  files are absent.
