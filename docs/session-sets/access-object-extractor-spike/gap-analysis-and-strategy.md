# Access Object Extractor Spike — Gap Analysis and Integration Strategy

> **Session:** 1 of 1
> **Orchestrator:** Claude Code / `claude-opus-4-7` / effort=high
> **Date:** 2026-05-14
> **Spec:** `docs/session-sets/access-object-extractor-spike/spec.md`
>
> This document answers the five spike questions from the spec:
> coverage comparison, gap analysis, integration strategy,
> `scripts/run_extraction.ps1` role, and follow-on session-set scope.

---

## 1. How the Two Extractors Work

### AccessObjectExtractor.accdb (YAML side)

`samples/AccessObjectExtractor.accdb` is a standalone Access database whose
`modYamlDocumenter` module exposes a public sub `RunExtractionRelatively`.
`scripts/run_extraction.ps1` spawns `Access.Application` via COM, opens the
extractor `.accdb`, and invokes that sub. Internally the sub opens a *second*
`Access.Application` instance to read the target database (e.g.,
`HomeHealthCare.accdb`) via DAO — effectively Access-inside-Access — and serializes
everything to YAML files under `samples/DatabaseYaml/<TargetDb>/`.

The YAML extraction enumerates every DAO property it can read. Properties that
DAO raises an exception on are recorded as `null # SKIPPED: <reason>`. The DDL
for tables is reconstructed by the VBA tool from both DAO field metadata AND
relationship metadata — field types, sizes, constraints, and FK clauses derived
from `CurrentDb().Relations` — producing a complete CREATE TABLE statement
including FOREIGN KEY constraints with cascade rules.

**Output layout:**
```
samples/DatabaseYaml/<db>/
  Summary.yaml          ← database manifest (counts + name list per category)
  Relationships.yaml    ← all relationships in one file
  Tables/<name>.yaml    ← one file per table
  Queries/<name>.yaml   ← one file per query
  Forms/<name>.yaml     ← one file per form (properties + controls)
  Reports/<name>.yaml   ← one file per report (properties + controls + sections)
  Macros/<name>.yaml    ← one file per macro (SaveAsText-equivalent text)
  Modules/<name>.yaml   ← one file per module (VBA source + ModuleType)
```

### C# Extractors (JSON/TXT side)

Four exporters in `AccessHarvester.Core/Extraction/` use DAO via `AccessComSession`:

| Exporter | Category | Output format |
|---|---|---|
| `TableExporter` | Tables | One `.json` per table |
| `QueryExporter` | Queries | One `.json` per query |
| `RelationshipExporter` | Relationships | One `.json` per relationship |
| `SaveAsTextExporter` | Forms, Reports, Macros, ModulesAndClasses | One `.txt` per object (Access's SaveAsText format) |

The first three exporters write lean JSON with a curated subset of structural fields.
The fourth uses Access's native `Application.SaveAsText` API which produces an
opaque proprietary text format (readable by Access's own `LoadFromText` but not
structured for general parsing).

**Output layout (raw tier):**
```
raw/<db>/
  Tables/<name>.json
  Queries/<name>.json
  Relationships/<name>.json
  Forms/<name>.txt
  Reports/<name>.txt
  Macros/<name>.txt
  ModulesAndClasses/<name>.txt
```

---

## 2. Coverage Comparison Tables

### 2.1 Tables

| Field / Concept | YAML | C# JSON |
|---|---|---|
| Name | ✓ | ✓ |
| DDL (`CREATE TABLE` with columns, PK, FK constraints, cascades) | ✓ | — |
| Connect, SourceTableName | ✓ | ✓ |
| Attributes (numeric) | ✓ (string) | ✓ (int) |
| DateCreated, LastUpdated | ✓ | ✓ |
| RecordCount | ✓ | ✓ |
| **Table-level ValidationRule** (row-level, spans fields) | ✓ | — |
| **Table-level ValidationText** | ✓ | — |
| DataMacros placeholder | ✓ (null for HHC) | — |
| Display properties (Orientation, OrderByOn, DefaultView, etc.) | ✓ (many) | — |
| **Fields: Name, OrdinalPosition** | ✓ | ✓ |
| **Fields: TypeCode + TypeName** | ✓ | ✓ |
| **Fields: Size** | ✓ | ✓ |
| **Fields: Required, AllowZeroLength** | ✓ | ✓ |
| **Fields: DefaultValue** | ✓ | ✓ |
| **Fields: ValidationRule, ValidationText** | ✓ | ✓ |
| **Fields: Attributes, ForeignName** | ✓ | ✓ |
| **Fields: InputMask** | ✓ | — |
| **Fields: Format** | ✓ | — |
| **Fields: DisplayControl** (textbox/combobox/checkbox hint) | ✓ | — |
| Fields: ColumnWidth, ColumnOrder, ColumnHidden | ✓ | — |
| Fields: IMEMode, IMESentenceMode, UnicodeCompression | ✓ | — |
| Fields: AggregateType, ResultType, CurrencyLCID | ✓ | — |
| Fields: TextFontCharSet, TextAlign | ✓ | — |
| Indexes: Name, Primary, Unique, Required | ✓ | ✓ |
| Indexes: DistinctCount | ✓ | — |
| Indexes: per-field Attributes (sort direction) | — | ✓ |

**Verdict — Tables:** The C# exporter captures the structural schema correctly.
The YAML adds substantial additional depth: the DDL (which includes FK constraints
derived from relationship metadata), table-level validation constraints, `InputMask`,
`Format`, and `DisplayControl` hints on fields.

Notable YAML-only values for AI interpretation:
- **DDL** — the clearest single-sentence schema representation. The FK constraints
  in the DDL (e.g., `FOREIGN KEY ([StateCode]) REFERENCES [tlkpStates] ([StateCode])
  ON UPDATE CASCADE`) make referential integrity unambiguous without parsing the
  Relationships collection separately.
- **Table-level ValidationRule/Text** — row-level constraints spanning multiple
  fields (e.g., `[EnrollmentDate] >= [DateOfBirth]`). These are business rules the
  downstream AI must understand. The C# TableExporter captures per-field validation
  but misses this row-level constraint layer.
- **InputMask / Format** — entry masks and display formats stored at the table-field
  level (`SSNLast4` has InputMask `"0000"`; date fields have Format `"Short Date"`).
  These are data-entry intent signals relevant for migration.
- **DisplayControl** — the intended widget type per field (109=TextBox, 106=CheckBox,
  111=ComboBox). Stored at the field level, independent of any form.

### 2.2 Queries

| Field / Concept | YAML | C# JSON |
|---|---|---|
| Name | ✓ | ✓ |
| QueryType (human-readable) | ✓ | ✓ (QueryTypeName) |
| QueryTypeCode (numeric) | — | ✓ |
| SQL | ✓ | ✓ |
| Connect | ✓ | ✓ |
| ReturnsRecords | ✓ | ✓ |
| ODBCTimeout | ✓ | ✓ |
| DateCreated, LastUpdated | ✓ | — |
| RecordLocks, RecordsetType | ✓ | — |
| Display props (OrderByOn, DefaultView, FilterOnLoad, etc.) | ✓ | — |

**Verdict — Queries:** Coverage of the fields that matter for AI interpretation
(SQL, type, Connect) is identical. The YAML adds display/ordering metadata and
timestamps, which are low-value for AI interpretation.

### 2.3 Relationships

| Field / Concept | YAML | C# JSON |
|---|---|---|
| Name | ✓ | ✓ |
| Parent table | ✓ (`Table`) | ✓ (`ParentTable`) |
| Child table | ✓ (`ForeignTable`) | ✓ (`ChildTable`) |
| Field mapping | ✓ (string: `"A -> B"`) | ✓ (structured array `{ParentField, ChildField}`) |
| **EnforceReferentialIntegrity** | ✓ (explicit bool) | via Attributes bitmask only |
| **CascadeUpdate** | ✓ (explicit bool) | via AttributesDecoded `"UpdateCascade"` |
| **CascadeDelete** | ✓ (explicit bool) | via AttributesDecoded `"DeleteCascade"` |
| Inherited | ✓ (explicit bool) | via AttributesDecoded `"Inherited"` |
| LeftJoin / RightJoin | — | ✓ (AttributesDecoded) |
| DontEnforce | — | ✓ (AttributesDecoded) |
| Attributes (numeric) | — | ✓ |

**Verdict — Relationships:** Both are nearly equivalent. The YAML uses clearer
boolean flags; the C# uses a decoded list from the bitmask that adds join-type
flags (LeftJoin/RightJoin) the YAML lacks. Neither is a strict superset; both are
close to complete for AI interpretation purposes. The C# structured field array is
marginally more machine-readable than the YAML's `"FieldA -> FieldB"` string.

### 2.4 Forms

| Aspect | YAML (`Forms/<name>.yaml`) | C# SaveAsTextExporter (`.txt`) |
|---|---|---|
| Output format | Structured YAML property bags | Raw Access SaveAsText format |
| RecordSource | ✓ explicit field | embedded in flat-text blob |
| All form-level event handlers (named) | ✓ one field per event | embedded in flat-text blob |
| Embedded macro text per event | ✓ inline YAML string | embedded in flat-text blob |
| Controls array | ✓ structured (Name, ControlType, Properties) | embedded in flat-text blob |
| Control position (Top, Left, Width, Height) | ✓ | embedded in flat-text blob |
| VBA code | — (split to `Modules/Form_*.yaml`) | ✓ (embedded in same file) |
| Parseable by AI/downstream without special tooling | ✓ high | ✗ requires SAT parser |

**Verdict — Forms:** The YAML is materially superior for AI interpretation.
The structured Controls array and explicit event bindings are exactly what a
downstream AI needs to understand form behavior without needing to parse the
SaveAsText format. The C# SaveAsTextExporter's `.txt` output is a proprietary
blob that the AI cannot read without a dedicated parser.

The YAML uniquely captures `ControlType` as an explicit code per control, and
provides all control properties (including bound `ControlSource`, `RowSource`,
`DefaultValue`, tooltip text, and event handlers) as named key-value pairs.

Note: the YAML splits form VBA into a separate `Modules/Form_<name>.yaml`; the
SaveAsText `.txt` bundles form VBA and UI definition in one file.

### 2.5 Reports

Same structural comparison as Forms on the controls and events side. Additionally:

| Aspect | YAML | C# SaveAsTextExporter |
|---|---|---|
| Report sections (Detail, Page Header/Footer, Group Header/Footer) | ✓ (embedded in Controls/Properties) | embedded in flat-text blob |
| GroupLevel / grouping field metadata | ✓ (in Properties) | embedded in flat-text blob |
| OnNoData event | ✓ explicit field | embedded in flat-text blob |

Report grouping (which sections print, what field they group on, sort order) is
semantically central to understanding what a report does. Both tools capture this
data; only the YAML makes it structured and directly queryable.

### 2.6 Macros

| Field / Concept | YAML | C# SaveAsTextExporter |
|---|---|---|
| Name | ✓ | filename |
| Extraction mechanism | same `SaveAsText` API (via VBA) | same `SaveAsText` API (C# COM) |
| Legacy Begin/End action blocks | ✓ (`Definition:` block) | ✓ (.txt content) |
| Embedded AXL XML (structured modern form) | ✓ (as `Comment` entries in `Definition:`) | ✓ (same content) |
| Format | YAML `Definition:` field (multi-line string) | raw `.txt` file |

**Verdict — Macros:** Both tools extract macros via the same underlying
`Application.SaveAsText` API. The content is identical; only the container format
differs (YAML envelope vs. raw `.txt`). The embedded AXL XML comments are the
modern structured macro representation, present in both.

### 2.7 Modules (VBA Code)

| Field / Concept | YAML | C# SaveAsTextExporter |
|---|---|---|
| Name | ✓ | filename |
| **ModuleType** (Standard / FormCodeBehind / ReportCodeBehind) | ✓ explicit | implicit (inferred from name prefix) |
| VBA source text | ✓ (`Definition:` block) | ✓ (.txt, includes Attribute headers) |
| Attribute VB_ headers | stripped | ✓ included |

**Verdict — Modules:** Coverage is equivalent. The YAML's explicit `ModuleType`
field is a minor convenience. Neither side parses VBA into an AST — that is
handled by the VBA parser downstream.

### 2.8 Summary / Manifest

| Concept | YAML | C# |
|---|---|---|
| Database-level manifest (object counts + name lists per category) | ✓ `Summary.yaml` | — |
| Extraction timestamp | ✓ `ExtractedAt` in `Summary.yaml` | — |

---

## 3. Gap Analysis

### 3.1 What the YAML captures that the C# side currently misses

1. **DDL for tables.** The CREATE TABLE statement with column types, PK, FK
   constraints (derived from relationship metadata), NOT NULL, and DEFAULT values
   inline. This is the clearest single-sentence schema representation and is
   valuable for AI interpretation.

2. **Table-level ValidationRule and ValidationText.** DAO exposes validation
   constraints at two levels: per-field (which C# captures) and per-table
   (row-level constraints spanning multiple fields, e.g.
   `[EnrollmentDate] >= [DateOfBirth]`). The C# TableExporter currently misses
   the table-level constraint layer. These are business rules the downstream AI
   must understand.

3. **Field InputMask and Format.** `SSNLast4` has InputMask `"0000"` and Format
   `"@@@@"`; `ZipCode` has `"00000\-9999;;_"`; date fields have `"Short Date"`.
   These tell the AI how the data was entered and displayed — relevant for
   understanding field semantics in a migration context.

4. **Field DisplayControl.** Code `109` = TextBox, `106` = CheckBox, `111` =
   ComboBox. The intended display widget hint stored at the table-field level,
   independent of any form. The AI needs this to infer binding intent.

5. **Structured Controls for Forms and Reports.** The YAML `Controls:` array
   exposes every control's Name, ControlType code, position (Top/Left/Width/Height),
   binding (`ControlSource`, `RowSource`), tooltip, and per-control event handlers
   as named YAML fields. The C# side produces only a raw `.txt` blob that is
   opaque to AI consumers without a dedicated parser.

6. **Structured report sections and GroupLevel metadata.** The YAML captures
   report grouping fields, section identifiers, and group-level properties in a
   structured form. These define what a report prints and in what order — central
   to AI interpretation of report logic.

7. **Per-form event handlers as named fields.** Named events (`OnCurrent`,
   `BeforeUpdate`, `OnLoad`, etc.) and their bindings including embedded macro text
   per event (`OnCloseEmMacro`, etc.). Event topology is visible without parsing.

8. **Database-level manifest (`Summary.yaml`).** Object counts and name lists per
   category. Trivial to add to the C# pipeline.

9. **Explicit boolean flags on Relationships.** `EnforceReferentialIntegrity`,
   `CascadeUpdate`, `CascadeDelete` as named booleans rather than decoded from a
   bitmask. Marginally more readable for AI.

### 3.2 What the C# side captures that the YAML does not (extraction output differences)

1. **QueryTypeCode (numeric).** The C# exports both human-readable name and numeric
   code. Minor; the string name alone is sufficient for AI.

2. **Relationship field mapping as structured array** (`{ParentField, ChildField}`
   per field) rather than the YAML's `"FieldA -> FieldB"` string. More
   machine-parseable.

3. **Relationship join-type flags** (`LeftJoin`, `RightJoin`, `DontEnforce`) from
   the `AttributesDecoded` list. The YAML does not expose these.

4. **Index field-level Attributes** (sort direction). The C# index field entry
   includes `Attributes` per indexed field.

5. **Curated field selection.** For Tables and Queries the C# JSON omits the
   display/visual noise (theme colors, font settings, printer blobs) that the YAML
   exhaustively includes. Leaner output is lower noise for the downstream AI.

*Note: The C# pipeline's Translation/Enrichment stages (`raw/ → kb/ → ir/`) are
downstream processing capabilities, not extraction outputs. They are not part of
the extractor coverage gap; they are the reason the C# pipeline is the right
long-term canonical path.*

---

## 4. Integration Strategy Recommendation

### Verdict: **Inform** (primary) + **short-term Diverge** (transitional)

#### Rationale

The YAML is not a replacement for the C# pipeline — it lacks the Translation and
Enrichment phases that produce the `kb/` and `ir/` tiers which the downstream AI
actually consumes. It is also not purely complementary — it covers most of the same
objects as the C# extractors, just with different depth and format choices.

The YAML was built by a former Access developer who knew which properties mattered.
Its field selection is the most valuable artifact: it reveals what a domain expert
chose to expose. Specifically:

- **DDL, table-level validation, InputMask, Format, DisplayControl** — fields the
  C# TableExporter should add.
- **Structured form/report Controls with event bindings** — the YAML's `Controls:`
  array is the right model for a future C# FormExporter that replaces
  `SaveAsTextExporter.Forms`. The current `.txt` approach is a dead end for the
  downstream AI consumer.
- **Structured report sections and GroupLevel** — these belong in a future
  ReportExporter alongside controls.

The YAML also serves an immediate practical purpose: it is already committed as
the canonical mirror for HomeHealthCare. Downstream AI comprehension work should
use the YAML directly rather than waiting for the C# pipeline to catch up on
forms/reports coverage.

#### The four options assessed

| Strategy | Fit | Reason |
|---|---|---|
| **Replace** (YAML → canonical `raw/`) | Poor | YAML has no enrichment layer; breaks three-tier pipeline; path references original .accdb location, not repo root |
| **Complement** (disjoint concerns, both needed) | Partial | Not truly disjoint — both cover Tables, Queries, Relationships with overlap; no clear merge plan |
| **Inform** (back-port YAML unique fields into C#, retire VBA tool when done) | **Best** | Treats YAML as the spec for what C# extractors should cover; produces a single well-typed pipeline output; eliminates dual-format confusion |
| **Diverge** (YAML for AI, C# for pipeline) | Acceptable short-term | Viable for immediate use; produces long-term confusion over what is canonical |

#### Short-term: Diverge (use YAML now)

While C# extractors are being enhanced, the committed YAML under
`samples/DatabaseYaml/HomeHealthCare.accdb/` is the best available input for
downstream AI interpretation work. Use it as the AI interpretation input for any
probe or comprehension work today.

#### Long-term: Inform (close the C# coverage gap)

The C# pipeline should be upgraded to match the YAML's coverage on fields that
matter for AI interpretation:

1. **`TableExporter` additions:**
   - Table-level `ValidationRule` and `ValidationText`
   - DDL string (reconstructed from field + relationship metadata)
   - Field-level `InputMask`, `Format`, `DisplayControl`

2. **`RelationshipExporter` minor additions:**
   - Explicit boolean `EnforceReferentialIntegrity`, `CascadeUpdate`, `CascadeDelete`

3. **New `FormExporter`** (replacing `SaveAsTextExporter` for Forms):
   - JSON: form-level properties + Controls array
   - Per-control: Name, ControlType (name + code), ControlSource, RowSource, Caption,
     Top/Left/Width/Height, Enabled, Locked, DefaultValue, event handlers
   - Filter display-noise (theme colors, printer blobs, font settings)

4. **New `ReportExporter`** (replacing `SaveAsTextExporter` for Reports):
   - Same as FormExporter for controls and event bindings
   - **Additionally**: explicit section structure (Detail, PageHeader, PageFooter,
     GroupHeader_N, GroupFooter_N), GroupLevel entries (grouping field, sort order,
     group-on / keep-together / force-new-page settings)
   - Report-specific events: OnNoData, OnPage
   - This is meaningfully more than "same as forms" — report sections and grouping
     are the primary semantic structure of any report

5. **`QueryExporter`** — no changes needed.

6. **`ModulesExporter` minor enhancement** — add explicit `ModuleType` field.

Once items 1–4 are complete, `scripts/run_extraction.ps1` and
`samples/AccessObjectExtractor.accdb` can be retained as developer validation tools
rather than as production inputs.

---

## 5. `scripts/run_extraction.ps1` Role Recommendation

**Promote to first-class developer tool.**

The script is already well-written:
- Proper COM cleanup sequence (CloseCurrentDatabase → Quit → FinalReleaseComObject)
- `$PSScriptRoot`-relative paths (portable, no hardcoded user paths)
- Error handling with exit codes
- Lock-file check post-cleanup
- Parametric (`-TargetDb`, `-OutputFolder`, `-Extractor`)

Recommended actions:
1. **Document it in the repo README** as the canonical way to regenerate the YAML
   mirror when `HomeHealthCare.accdb` changes.
2. **Run it as a validation tool** during C# extractor development: after a
   TableExporter or FormExporter enhancement, regenerate the YAML and compare
   the new C# JSON output against the YAML to confirm coverage is closing.
3. Do **not** integrate it into automated CI — it requires Windows + Access
   installed, same constraint as the C# integration tests (`[SkippableFact]`).
   Treat it like those tests: a developer-run tool, not a CI gate.
4. No significant rework needed.

---

## 6. Follow-On Session Set Scope

### Priority 1: `table-extractor-coverage` (1–2 sessions)

**Scope:** Enhance `TableExporter` to close the C# vs. YAML gap for tables.

Deliverables:
- Add table-level `ValidationRule` and `ValidationText` to output
- Add `InputMask`, `Format`, `DisplayControl` to per-field output
- Add reconstructed DDL string (reading both field and relationship metadata)
- Update tests using committed HomeHealthCare fixtures

Rationale: Tables are foundational — every downstream AI interpretation prompt
references field types and constraints. Highest leverage-per-session enhancement.

### Priority 2: `structured-form-report-extractor` (3–4 sessions)

**Scope:** Replace `SaveAsTextExporter.Forms` / `.Reports` with new JSON exporters.

Deliverables:
- **`FormExporter`** emitting structured JSON: form properties + Controls array
  per control (Name, ControlType name+code, ControlSource, RowSource, Caption/Label,
  Top/Left/Width/Height, Enabled, Locked, DefaultValue, event handlers with embedded
  macro text)
- **`ReportExporter`** emitting structured JSON: same as FormExporter for controls
  and events, **plus** explicit section structure (Detail, PageHeader/Footer,
  GroupHeader_N/GroupFooter_N) and GroupLevel entries (grouping field, sort order,
  group-on setting, keep-together, force-new-page, repeat-section)
- Filter display-noise (theme colors, printer blobs, binary font data)
- Tests using committed HomeHealthCare fixtures (hermetic)
- Update Translation/Enrichment for the new JSON shape if needed

This is explicitly a 3–4 session set (not 2) because the ReportExporter has
materially more work than FormExporter: report sections and grouping semantics are
distinct enough to require their own extraction and test coverage.

### Ordering

`table-extractor-coverage` first — smaller, lower-risk, outputs feed form/report
AI interpretation immediately. `structured-form-report-extractor` second.

Run `scripts/run_extraction.ps1` after each enhancement to regenerate the YAML
and diff the new C# JSON against the YAML to confirm the coverage gap is closing.
