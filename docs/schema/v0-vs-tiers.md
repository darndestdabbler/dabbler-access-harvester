# Schema Delta: v0 Parser Schema vs. Tier Output Schemas

> **Purpose:** Enumerate every field-shape difference between `docs/test-cases/harvester-schema.v0.json`
> (the VBA parser output schema) and the `raw/` / `kb/` / `ir/` schemas produced by
> `accessexportertemp1-import`. This document does **not** collapse the two schemas — they remain
> intentionally independent per `project-guidance.md` → Output Architecture. It records named
> differences with rationale so that `schema-finalization-and-docs` (#17) has a clear starting point.
>
> **Produced by:** Session 2 of `parser-foundations` (in-conversation by Sonnet orchestrator;
> `schema-validation` task type not yet in `router-config.yaml` — scheduled for
> `vba-parser-session-set-generator` child set #4).
>
> **Tier sample source:** `tmp/run-output/AccessMetaData/MiniComplex/{raw,kb,ir}/`
>
> **Status:** Reconciliation pass only. The v0 schema is touched only for outright errors found
> below (see §4.2 and §11). All structural changes wait for #17.

---

## 1. File Structure and Envelope

| Dimension | v0 parser schema | Tier schemas |
|---|---|---|
| Granularity | One JSON document per *parser invocation*, wrapping N `items[]` | One JSON file per *Access object* (one form, one query, one module, …) |
| Wrapper | Required `schemaVersion`, `source`, `items[]` envelope | No envelope; object is the root |
| Versioning | `schemaVersion` field required on every document | No version field in output files |

**Rationale:** The v0 schema is the parser's output contract — it wraps one parse session's results.
The tier files are extraction artifacts — each file *is* the entity. They serve different consumers
at different pipeline stages.

**Version gap action:** Version tracking at the tier level is deferred to `schema-finalization-and-docs` (#17),
where a `DatabaseManifest` envelope (per Phase 0.3 of the plan) will add `harvesterVersion` and
`schemaVersion` to tier output.

---

## 2. Procedure Representation

### 2.1 Field inventory comparison

| Field concept | v0 field (camelCase) | ir `Procedures[]` field (PascalCase) | Notes |
|---|---|---|---|
| Discriminator | `itemKind: "procedure"` | `ProcedureType` on procedure; `Type` on the owning AccessNode | Different levels |
| Procedure name | `name` | `Name` | Casing only |
| Procedure kind | `procedureKind` (see §2.2) | `ProcedureType` (see §2.2) | Values differ |
| Visibility | `visibility` | `AccessModifier` | Same values; different field name |
| Static flag | `isStatic: boolean` | `IsStatic: boolean` | Casing only |
| Return type | `signature.returnType: string` | `ReturnType: string \| null` | v0 omits for Sub; ir emits null |
| Parameters | `signature.parameters[]` | `Parameters[]` | See §3 |
| Source location | `body.statements[*].location`, `signature.parameters[*].location` | `StartLine: integer` | v0 is full `sourceLocation`; ir is single integer |
| Event binding | `eventBinding: { controlName, eventName, resolvedControl }` | *(absent)* | See §2.3 |
| Body AST | `body.statements[]`, `body.references[]`, `body.calls[]`, `body.sqlStrings[]` | *(absent)* | Parser deliverable; not yet implemented |
| Classification | `classification: { purposeTags, purity, complexity, linesOfCode, translatability }` | `InferredFunctionality: string \| null`, `Notes: string \| null` | v0 is structured; ir is two flat strings |
| Lib target | *(absent)* | `Lib: string \| null` | `Declare Lib` support — v0 gap |
| Alias | *(absent)* | `Alias: string \| null` | `Declare Alias` support — v0 gap |

### 2.2 `procedureKind` vs. `ProcedureType` enum values

| v0 `procedureKind` | ir `ProcedureType` | Notes |
|---|---|---|
| `"Sub"` | `"Sub"` | Same |
| `"Function"` | `"Function"` | Same |
| `"PropertyGet"` | `"PropertyGet"` | Same |
| `"PropertyLet"` | `"PropertyLet"` | Same |
| `"PropertySet"` | `"PropertySet"` | Same |
| `"EventHandler"` | *(absent)* | v0 models event handlers as a first-class kind; ir identifies them by name convention (`Object_EventName`). The v0 design is richer for resolution. |
| *(absent)* | `"DeclareFunction"` | VBA `Declare Function` statements (external DLL calls). The ir emits `ProcedureType: "DeclareFunction"` and `ProcedureType: "DeclareSub"` as distinct kinds. v0 has no equivalent — `Declare` procedures would need to be added to the `procedureKind` enum (e.g., `"DeclareFunction"` and `"DeclareSub"`). The `Lib` and `Alias` fields (§2.1, D9) are part of the same gap. **This is a v0 gap to fix at #17.** |
| *(absent)* | `"DeclareSub"` | VBA `Declare Sub` statements. Same rationale as `"DeclareFunction"`. |

### 2.3 Event binding

v0 has an explicit `eventBinding: { controlName, eventName, resolvedControl }` object on procedures
with `procedureKind: "EventHandler"`. The ir does not have this. Event handler detection in the ir
relies on procedure name conventions only. The v0 `eventBinding` is a parser-layer enrichment, not
an extractor concern — it will be populated by resolver sessions downstream.

### 2.4 Body AST absence

v0 defines four body-level collections (`statements`, `references`, `calls`, `sqlStrings`) that will
hold the parse tree once the grammar and resolver are implemented. None of these exist in the ir
today. These fields are forward-declarations in the schema, not gaps in the extractor.

### 2.5 `StartLine` shape mismatch

v0 uses a `sourceLocation` object (`{ file, module, startLine, startColumn, endLine, endColumn }`)
for all location references. The ir extractor emits a single integer `StartLine`. The v0 shape is a
superset; a mapping layer will need to synthesize `startColumn`, `endLine`, `endColumn` from parser
output when those become available.

---

## 3. Parameter Representation

| Field concept | v0 `parameter` field | ir `Parameters[]` field | Notes |
|---|---|---|---|
| Name | `name` | `Name` | Casing only |
| Data type | `type: string` | `DataType: string` | Field name differs |
| By-ref flag | `byRef: boolean` | `PassingMechanism: "ByRef" \| "ByVal"` | **Shape differs** (see §3.1) |
| Optional | `optional: boolean` | `IsOptional: boolean` | Casing only |
| ParamArray | `paramArray: boolean` | `IsParamArray: boolean` | Casing only |
| Array type | *(absent)* | `IsArray: boolean` | **v0 gap** (see §3.2) |
| Default value | `defaultValue: string` | `DefaultValue: string \| null` | v0 can omit; ir emits null |
| Provenance | `derivedFrom: string` | *(absent)* | Parser-layer concept; not an extractor concern |

### 3.1 `byRef` vs. `PassingMechanism` shape difference

v0 uses a boolean (`byRef: true` = ByRef, `byRef: false` = ByVal). The ir uses a string enum
`"ByRef"` or `"ByVal"`. The string form is less ambiguous — VBA's default (no keyword) is ByRef,
which can be lost in a boolean. **Recommendation for #17:** migrate v0 `byRef: boolean` to
`passingMechanism: "ByRef" | "ByVal" | "ByRefDefault"` (with `"ByRefDefault"` for the no-keyword
case), aligning with the ir representation while preserving the default-vs-explicit distinction.

### 3.2 Missing `IsArray` in v0

The ir tracks `IsArray` separately from `IsParamArray` — `IsArray` is true when the parameter is an
array type (e.g., `myArr() As String`), while `IsParamArray` is true when the `ParamArray` keyword
is used. v0 only has `paramArray: boolean`. **This is a schema gap in v0.** Recommendation for #17:
add `isArray: boolean` to the `parameter` definition.

---

## 4. Query Representation

### 4.1 Field inventory comparison

| Field concept | v0 `queryItem` | ir/kb/raw Query | Notes |
|---|---|---|---|
| Discriminator | `itemKind: "query"` | `Type: "Query"` | Different discriminators |
| Name | `name` | `Name` | Casing only |
| Query kind | `queryType: string enum` | `QueryTypeName: string` + `QueryTypeCode: integer` | v0 omits numeric code |
| SQL text | `sql.originalText` | `Sql` (flat string) | v0 nests inside `sql`; tier is flat |
| Parsed SQL | `sql.parsed` (AST) | *(absent)* | Parser deliverable |
| Bound params | `sql.parameters[]` | *(absent)* | Parser deliverable |
| Migration abstraction | `abstraction: { suggestedMethodName, suggestedParameters, returnShape }` | *(absent)* | v0-only; migration planning layer |
| DAO connect string | *(absent)* | `Connect: string` | Access-native DAO field; not a parser concern |
| Returns records | *(absent)* | `ReturnsRecords: boolean` | Access-native DAO field |
| ODBC timeout | *(absent)* | `OdbcTimeout: integer` | Access-native DAO field |

### 4.2 `queryType` enum completeness check

Both v0 and ir use the same string values: `Select`, `MakeTable`, `Update`, `Delete`, `Insert`,
`Crosstab`, `Union`, `DataDefinition`, `PassThrough`. **No gap.** The ir `QueryTypeCode` integer
provides the DAO constant for completeness; the string name is the canonical identifier.

---

## 5. Expression Representation

**v0:** `expressionItem` is a first-class item type with `context`, `expression.originalText`,
`expression.parsed` (AST), `expression.references[]`, and `expression.inferredType`.

**Tiers:** Expressions are stored as raw strings inside `Properties` maps on AccessNode objects.
No structured expression tier exists.

| v0 `context` value | Tier location |
|---|---|
| `"controlSource"` | `AccessNode.Properties["ControlSource"]` (kb/ir control nodes) |
| `"validationRule"` | `AccessNode.Properties["ValidationRule"]` (kb/ir control nodes); also `Fields[*].ValidationRule` in table tier |
| `"defaultValue"` | `AccessNode.Properties["DefaultValue"]` (kb/ir control nodes); also `Fields[*].DefaultValue` in table tier |
| `"rowSource"` | `AccessNode.Properties["RowSource"]` (kb/ir ComboBox/ListBox nodes) |
| `"queryColumn"` | SQL `SELECT` column expressions (inside query `Sql` string) |
| `"calculatedField"` | `AccessNode.Properties["ControlSource"]` when it starts with `=` |
| `"conditionalFormat"` | *(not yet surfaced in tier output)* |

**Rationale:** Structured expression output is a pure parser deliverable. The tier extraction phase
only captures raw strings. The mapping table above is the contract the parser layer will use to
locate expressions in the tier output and produce `expressionItem` records.

---

## 6. Tables and Relationships (in tiers; not in v0)

| Tier field | Notes |
|---|---|
| `Name`, `Type="Table"`, `Attributes`, `DateCreated`, `LastUpdated`, `RecordCount` | Table-level metadata; no v0 equivalent |
| `Fields[*].TypeCode`, `Fields[*].TypeName`, `Fields[*].Size`, etc. | DAO field metadata; no v0 equivalent |
| `Fields[*].ValidationRule`, `Fields[*].DefaultValue` | Expressions as raw strings — future `expressionItem` candidates (see §5) |
| `Indexes[]` | No v0 equivalent |
| Relationships (in `raw/Relationships/`) | No v0 equivalent |

**Rationale:** The v0 parser schema is scoped to VBA and expression parsing, not database structural
metadata. Table/relationship schemas are extractor concerns only. The plan's Phase 0.3
`DatabaseManifest` + `TableSchemas` level (see plan §7.1) is the future home for these; finalization
is deferred to #17.

---

## 7. Forms and Reports: Host Context vs. AccessNode Tree

**v0** represents forms/reports as `source.hostContext` — the metadata the parser needs to resolve
`Me.ControlName` references within VBA procedures. The v0 `hostContext.controlsAvailable[]` is a
typed subset of the full AccessNode tree:

| v0 `controlMetadata` field | Tier equivalent |
|---|---|
| `name` | `AccessNode.Properties` implicitly (node is identified by its position in `Children`) — **gap: AccessNode has no `Name` property at the node level in kb; names come from parent context** |
| `controlType` | `AccessNode.Type` (e.g., `"TextBox"`, `"ComboBox"`) |
| `boundField` | `AccessNode.Properties["ControlSource"]` (without leading `=`) |
| `controlSource` | `AccessNode.Properties["ControlSource"]` |
| `rowSource` | `AccessNode.Properties["RowSource"]` |
| `rowSourceType` | `AccessNode.Properties["RowSourceType"]` |
| `boundColumn` | `AccessNode.Properties["BoundColumn"]` |
| `columnCount` | `AccessNode.Properties["ColumnCount"]` |

**Gap noted:** The `Name` property of a form control is not directly available on the AccessNode
in the current kb/ir output — controls appear as positional children without a stable name field in
the node itself. The LabelResolver and related enrichers may supply this; verify against an ir file
where label resolution has run.

---

## 8. Resolved References (v0) vs. Raw Strings (Tiers)

**v0 `resolvedReference`** is a rich object:
```
{ originalText, location, resolutionStatus, referenceKind, resolvedTarget, implicitContext, confidence, notes }
```

**Tier output:** Access expressions and SQL strings are raw strings in `Properties` or `Sql` fields.
No reference resolution has occurred.

**Rationale:** Reference resolution is a parser/resolver deliverable. The tier output is
pre-resolution. The `resolvedReference` schema is the target shape for what will be produced once the
grammar, resolver, and symbol table sessions are complete (child sets #3–#8).

---

## 9. Provenance Field Shape

**v0 `resolvedTarget` actual schema shape:** `oneOf [{ kind, name, container, fullyQualifiedName, type }, null]`.
When a reference is unresolved, `resolvedTarget` is `null` (not a tagged object). The
`resolutionStatus` field carries the `"unresolved"` signal. There is no `{ source: "unresolved" }`
wrapper in the v0 schema as defined.

**ir tier:** The `GeometryIn` enricher emits direct numeric fields (`{ W: 7.5 }`). Per
`project-guidance.md` → Conventions → Output JSON Conventions, the `LabelResolver` enricher is
expected to emit `Label: { source: "attached", text: "..." }` or `Label: { source: "unresolved" }`.
However this tagged-object provenance shape is an **ir enricher convention only** — it applies to
enricher-added fields like `Label`, not to v0 parser output fields like `resolvedTarget`.

**Differences:**
- v0 `resolvedTarget` uses `null` for unresolved references; the provenance signal is on
  `resolutionStatus`. The ir label enricher uses a `{ source: "unresolved" }` tagged object for
  labels. These are two different conventions serving different purposes — not a conflict.
- The ir `{ source: "attached" | "unresolved" }` pattern described in `project-guidance.md`
  conventions is not formally defined in the v0 JSON Schema. If future parser output fields use
  the tagged-object convention, they will need corresponding `$defs` entries in the v0 schema.

**Action:** Verify the `{ source: "attached" | "unresolved" }` provenance shape appears in ir
forms where LabelResolver does resolve labels — this confirms the enricher is operating as documented.
The absence in the MiniComplex sample may mean the sample forms had no detached labels, or
LabelResolver did not run on this sample.

---

## 10. Root-Level Entity Field Mapping (Tier vs. v0 Envelope)

The table below maps the top-level fields of each tier entity type to their v0 counterparts. This
covers the boundary between the tier file's root object and the v0 envelope fields.

### Module / Class (ir: `{ Name, Type, VbaCode, Procedures[] }`)

| Tier root field | v0 equivalent | Notes |
|---|---|---|
| `Name` | `source.location.module` | Module name lives in the v0 source location |
| `Type` (`"Module"` / `"ClassModule"`) | `source.hostContext.hostKind` (`"standard"` / `"class"`) | Different enum values; `standard` = `Module`, `class` = `ClassModule` |
| `VbaCode` | `source.originalSource` | Same concept; v0 is camelCase, tier is PascalCase |
| `Procedures[]` | `items[]` (where `itemKind: "procedure"`) | Tier wraps procedures in the module; v0 makes each procedure a top-level item |

### Form / Report (ir: `{ Type, Properties{}, Children[], VbaCode, GeometryIn, NonDefaultProperties[], Procedures[] }`)

| Tier root field | v0 equivalent | Notes |
|---|---|---|
| `Type` (`"Form"` / `"Report"`) | `source.kind` (`"form"` / `"report"`) + `source.hostContext.hostKind` | v0 separates kind from context |
| `Properties["RecordSource"]` | *(not in v0)* | DAO binding metadata; no parser schema equivalent |
| `VbaCode` | `source.originalSource` | Same concept |
| `Children[]` (AccessNode tree) | `source.hostContext.controlsAvailable[]` (subset) | v0 carries only the control metadata needed for resolver context, not the full tree |
| `Procedures[]` | `items[]` (where `itemKind: "procedure"`) | Same as module above |
| `GeometryIn` | *(not in v0)* | ir enricher output; not a parser concern |
| `NonDefaultProperties[]` | *(not in v0)* | ir enricher output; not a parser concern |

### Query (kb/ir: `{ Name, Type, QueryTypeCode, QueryTypeName, Sql, Connect, ReturnsRecords, OdbcTimeout }`)

| Tier root field | v0 equivalent | Notes |
|---|---|---|
| `Name` | `items[0].name` (where `itemKind: "query"`) | Moves from root to item |
| `Type: "Query"` | `source.kind: "query"` | Same concept; different paths |
| `QueryTypeName` | `items[0].queryType` | Same string values (see §4.2) |
| `QueryTypeCode` | *(not in v0)* | Numeric DAO constant; v0 uses string only |
| `Sql` | `items[0].sql.originalText` | v0 nests inside `sql`; tier is flat |
| `Connect`, `ReturnsRecords`, `OdbcTimeout` | *(not in v0)* | Access-native DAO fields; no parser equivalent |

---

## 11. Casing and Naming Conventions

| Convention | v0 parser schema | Tier schemas |
|---|---|---|
| Field names | camelCase | PascalCase |
| Discriminator field | `itemKind` (string enum on each item) | `Type` (on AccessNode root) |
| Array names | plural lower-camel (`items`, `parameters`, `statements`) | plural Pascal (`Fields`, `Indexes`, `Procedures`, `Parameters`, `Children`) |

**Rationale:** Intentional difference. v0 targets JSON consumers (camelCase is the JSON idiom);
tier output is serialized from .NET classes (PascalCase is the C# serialization default). Any tool
consuming both schemas will need a normalization shim at read time. This will be addressed at v1
finalization (#17) — the decision is between normalizing tier output to camelCase or accepting
the dual-casing in the merged schema.

---

## 11. v0 Schema Gaps Surfaced by This Pass

The following are errors or gaps in `harvester-schema.v0.json` discovered during this reconciliation.
These are the only changes permitted to the v0 schema this session (per spec: "touched only to fix
outright errors").

| # | Issue | Recommended fix |
|---|---|---|
| v0-G1 | `parameter` definition has no `isArray` field; the ir distinguishes `IsArray` (array type) from `IsParamArray` (ParamArray keyword) | Add `isArray: boolean` to `$defs/parameter` — **fixed this session** |
| v0-G2 | `byRef: boolean` does not distinguish explicit ByRef from the implicit default (no keyword = ByRef by default in VBA) | Not fixing now — structural change, deferred to #17 per spec |
| v0-G3 | No `Lib` or `Alias` fields on `procedureItem` for `Declare` statement support | Not fixing now — deferred to #17 |
| v0-G4 | `procedureItem` has no `startLine` at the procedure level (only nested inside `sourceLocation` objects on body nodes); the ir's flat `StartLine: integer` is simpler for indexing | Not fixing now — deferred to #17 |
| v0-G5 | `procedureKind` enum is missing `"DeclareFunction"` and `"DeclareSub"` — the ir emits `ProcedureType: "DeclareFunction"` and `ProcedureType: "DeclareSub"` for `Declare` statement procedures (confirmed in `basUtilities.ir.json`) | Not fixing now — structural change (enum extension); deferred to #17 |

Only **v0-G1** is an outright error (the `IsArray` vs. `IsParamArray` distinction is real and
discoverable from existing source code). The others are design-evolution items for #17.

---

## 12. Summary Table

| Delta # | Category | v0 | Tiers | Action |
|---|---|---|---|---|
| D1 | File structure | Envelope with `items[]` | One file per object, no envelope | Deferred to #17 (DatabaseManifest) |
| D2 | Versioning | `schemaVersion` required | None | Deferred to #17 |
| D3 | Procedure kind | `procedureKind` + `EventHandler` value; no `DeclareFunction`/`DeclareSub` | `ProcedureType`, no `EventHandler`; `DeclareFunction`/`DeclareSub` present | Deferred to #17 (add enum values v0-G5) |
| D4 | Visibility field name | `visibility` | `AccessModifier` | Deferred to #17 (normalize to one name) |
| D5 | Event binding | Explicit `eventBinding` object | Name-convention only | Parser deliverable; no action now |
| D6 | Body AST | `statements`, `references`, `calls`, `sqlStrings` defined | None | Parser deliverable; no action now |
| D7 | Classification | Structured `classification` object | Two flat strings | Parser deliverable; no action now |
| D8 | Source location shape | `sourceLocation` object | Flat `StartLine: integer` | Deferred to #17 |
| D9 | `Lib` / `Alias` on procedure | Absent | Present | v0 gap; deferred to #17 |
| D10 | Parameter: by-ref shape | `byRef: boolean` | `PassingMechanism: string enum` | Deferred to #17 |
| D11 | Parameter: `IsArray` | Absent | Present | **Fixed in v0 this session (v0-G1)** |
| D12 | Parameter: `derivedFrom` | Present | Absent | Parser deliverable; no action now |
| D13 | Query: DAO fields | Absent | `Connect`, `ReturnsRecords`, `OdbcTimeout`, `QueryTypeCode` | Deferred to #17 |
| D14 | Query: SQL structure | `sql.parsed`, `sql.parameters[]` defined | None | Parser deliverable; no action now |
| D15 | Expressions | `expressionItem` defined | Raw strings in Properties | Parser deliverable; §5 mapping table is the contract |
| D16 | Tables/Relationships | Not in v0 | Full DAO metadata | Deferred to #17 |
| D17 | Control `Name` in AccessNode | `controlsAvailable[*].name` | No Name on node | Verify against resolved ir; possible extractor gap |
| D18 | Reference resolution | `resolvedReference` defined | Raw strings only | Parser deliverable; no action now |
| D19 | Provenance shape | `resolvedTarget: object \| null`; unresolved state carried by `resolutionStatus` (no tagged object) | ir enricher convention: `Label: { source: "attached"\|"unresolved", text: ... }` | Verify LabelResolver output; tagged-object shape is ir enricher only, not defined in v0 |
| D20 | Casing convention | camelCase | PascalCase | Deferred to #17 (normalization decision) |
| D21 | Root-level entity field mapping | `source.kind`, `source.originalSource`, `source.location`, `items[]` | `Type`, `VbaCode`, `Name`, `Procedures[]` / top-level object | See §10 mapping tables; deferred to #17 |
