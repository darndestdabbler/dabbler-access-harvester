# Architecture Decision Record: harvester-schema v0 → v1.0

> **Date:** 2026-04-28
> **Session:** Session 1 of `schema-finalization-and-docs`
> **Status:** Accepted
> **Schema file:** `docs/test-cases/harvester-schema.v1.0.json`
> **Source of deltas:** `docs/schema/v0-vs-tiers.md` §11–12 (produced by `parser-foundations` Session 2)

---

## Summary

`harvester-schema.v1.0.json` promotes the provisional v0 parser schema to a stable v1.0 release by:
1. Applying all gap fixes deferred from v0 (D3–D21 items marked "Deferred to #17" in `v0-vs-tiers.md` §12).
2. Normalizing field-name casing to PascalCase across the entire parser section.
3. Adding the enrichment pipeline output types from Phase 6 child sets (forms, queries, event handlers, function classifications, navigation graph, validation rules, row-source specs, unresolved items).
4. Introducing the `DatabaseManifest` root document (Phase 0.3/7.1 deliverable) that wraps the full pipeline output.

The v0 schema remains valid for existing test fixtures and consumers until a migration script is shipped (see Back-Compatibility Plan below).

---

## Architecture Decisions

### D20 — Casing Normalization: PascalCase

**Decision:** Option B — Normalize the parser schema to PascalCase to align with the C# tier schemas and eliminate the camelCase/PascalCase split.

**Rationale:** The downstream consumer is an AI agent, not a human; casing does not affect readability, only tooling consistency. A single casing convention across the full pipeline output (parser + tier + enrichment) simplifies AI prompts and any normalization shims consumers would otherwise need. The one-time cost of updating the 13 `.expected.json` test fixtures is bounded and mechanical.

**Impact on test fixtures:** All 13 `docs/test-cases/*.expected.json` files use camelCase today. Session 2 of this set will update them as part of wiring the schema validator against real pipeline output.

### D1/D2 — DatabaseManifest Envelope

**Decision:** Introduce a `DatabaseManifest` JSON document as the root of every full database export. The manifest carries `SchemaVersion`, `HarvesterVersion`, `Database` metadata, a `Summary` count block, and typed arrays for every enrichment output category.

**Rationale:** The v0 schema covered per-module parser invocations only. A database-level envelope is required to (a) version tier output (D2 gap), (b) bundle enrichment results alongside parser results for a single export, and (c) give downstream consumers a single entry point rather than N per-module files.

**Shape:** See `$defs/DatabaseManifest` in `harvester-schema.v1.0.json`.

---

## Gap Fixes Applied

| Delta | Fix applied in v1.0 |
|---|---|
| **D3** — `procedureKind` enum missing `DeclareFunction`/`DeclareSub` | Added `DeclareFunction` and `DeclareSub` to `ProcedureItem.ProcedureKind` enum. |
| **D4** — `visibility` vs `AccessModifier` field naming | Renamed to `Visibility` (PascalCase, consistent with tier `AccessModifier` concept). |
| **D8** — `sourceLocation` shape only, no flat `StartLine` | Added `StartLine: integer` directly on `ProcedureItem` as a flat shorthand (in addition to nested `SourceLocation` objects in the body). |
| **D9** — No `Lib`/`Alias` on `procedureItem` for `Declare` statements | Added `Lib: string` and `Alias: string` to `ProcedureItem`. |
| **D10** — `byRef: boolean` doesn't distinguish explicit ByRef from default ByRef | Replaced `byRef: boolean` with `PassingMechanism: enum { ByRef, ByVal, ByRefDefault }` on `Parameter`. `ByRefDefault` = no keyword written (VBA default). |
| **D13** — Query DAO fields absent | Added `QueryTypeCode: integer`, `Connect: string`, `ReturnsRecords: boolean`, `OdbcTimeout: integer` to `QueryItem`. |
| **D16** — No table/relationship schema | Added `TableSchema`, `TableField`, `TableIndex`, `RelationshipDefinition` types and wired into `DatabaseManifest.Tables` and `DatabaseManifest.Relationships`. |
| **D20** — Dual casing (camelCase in parser, PascalCase in tier) | All parser schema field names normalized to PascalCase throughout v1.0. |
| **D21** — Root-level entity field mapping gap | `DatabaseManifest.Forms[]` uses `FormExport { Spec: FormComponentSpec, ParserOutput?: ParserOutput }`. `ModuleExport { ModuleName, ModuleKind, ParserOutput? }` for standard/class modules. The v0 envelope (`source`, `items`, `schemaVersion`) is preserved as `ParserOutput` (a nested $def) embedded inside each export. |

---

## New Types Added (Enrichment Pipeline)

All enrichment types are new in v1.0 (no v0 counterpart). They are added as `$defs` and referenced from `DatabaseManifest`.

| Type | Phase | Source C# file |
|---|---|---|
| `FormComponentSpec` + supporting types (12 types) | 6.2 | `AccessHarvester.Pipeline/Enrichment/FormComponentSpec.cs` |
| `QueryAbstraction` + supporting types (12 types including `PredicateNode`, `ExpressionDescriptor`) | 6.1 | `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs` |
| `EventHandlerClassification`, `EventHandlerClassificationSignal` | 6.3 | `AccessHarvester.Pipeline/Enrichment/EventHandlerClassification.cs` |
| `FunctionClassification`, `ComplexityMetrics`, `DependencyEdge`, `UnresolvedReferenceFact` | 6.6 | `AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs` |
| `NavigationGraph` + supporting types (9 types) | 6.7 | `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs` |
| `ValidationRule` + supporting types (5 types) | 6.4 | `AccessHarvester.Pipeline/Enrichment/ValidationRule.cs` |
| `RowSourceSpec`, `RowSourceColumnDescriptor`, `RowSourceCascadingDriver` | 6.5 | `AccessHarvester.Pipeline/Enrichment/RowSourceSpec.cs` |
| `UnresolvedItem`, `UnresolvedLocation` | 6.8 | `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs` |
| `DatabaseManifest`, `DatabaseInfo`, `DatabaseSummary`, `FormExport`, `ModuleExport`, `TableSchema`, `TableField`, `TableIndex`, `RelationshipDefinition` | 0.3/7.1 | New in this set |

---

## Gaps Deferred to Post-v1.0

These items from `v0-vs-tiers.md` §12 are intentionally left for a future schema update:

| Delta | Status |
|---|---|
| D5 — Body AST forward-declarations (`statements`, `references`, `calls`, `sqlStrings`) | Parser deliverable; forward-declarations remain in `ProcedureItem.Body`. Populated by later resolver sessions. |
| D6 — `eventBinding` on procedures (D5 corollary) | Parser deliverable; preserved as nullable on `ProcedureItem`. |
| D7 — Classification structured vs. flat strings | Design tension between the v0 structured `classification` object and the tier flat strings resolved in favour of keeping the v0 structured form on `ProcedureItem`. Both remain; downstream session picks the richer one. |
| D12 — `derivedFrom` on `Parameter` | Parser provenance field; kept in v1.0 as `DerivedFrom: string`. |
| D14 — Query `sql.parsed` / `sql.parameters[]` | Parser deliverables in `QueryItem.Sql.Parsed` / `QueryItem.Sql.Parameters`; populated by later sessions. |
| D15 — Expression `context` mapping | `ExpressionItem.Context` enum unchanged from v0 (PascalCase only). The §5 mapping table in `v0-vs-tiers.md` is the contract for locating expressions in tier output. |
| D17 — Control `Name` absent in AccessNode | Verify against resolved ir output. `FormControlSpec.Name` is now populated by `FormComponentSpecBuilder`; AccessNode itself may still lack a stable Name property. |
| D18 — Reference resolution placeholder fields | `ResolvedReference` structure preserved. Populated by resolver sessions. |
| D19 — Provenance tagged-object `{ source: "attached" }` shape on ir fields | ir enricher convention; not formally in v0/v1 parser schema. `LabelInfo.Source` enum in v1.0 mirrors this convention for form control labels only. |

---

## Known Issues

### PredicateNode polymorphism requires `[JsonPolymorphic]`

The C# `PredicateNode` hierarchy uses abstract + sealed records. Without `[JsonPolymorphic]` / `[JsonDerivedType]` attributes, `System.Text.Json` serializes each concrete type without a discriminator field, making `AndPredicate` and `OrPredicate` produce identical JSON shapes `{ "Operands": [...] }`.

**v1.0 resolution:** The schema defines a `NodeKind` discriminator field on `PredicateNode`. The C# builders for `QueryAbstraction` must be updated to annotate `PredicateNode` with `[JsonPolymorphic(TypeDiscriminatorPropertyName = "NodeKind")]` and `[JsonDerivedType(typeof(AndPredicate), "And")]` etc., or implement a custom `JsonConverter<PredicateNode>`.

**Action required:** Session 2 or a subsequent resolver session must add these attributes / converter before pipeline output can be validated against the v1.0 schema for `PredicateNode`-containing fields.

### Test fixture casing migration

All 13 `docs/test-cases/*.expected.json` files use camelCase field names (v0 convention). They will fail validation against v1.0 until migrated to PascalCase. Session 2 of this set performs the migration as part of wiring the output validator.

---

## Back-Compatibility Plan

| Version | Behavior |
|---|---|
| v0 (current) | Continues to validate existing `*.expected.json` test fixtures. Remains the operative schema until Session 2 migrates those fixtures. |
| v1.0 (this release) | New schema for all future pipeline output. Identified by `SchemaVersion: "1.0"` in the `DatabaseManifest.SchemaVersion` field. |
| Migration path | Session 2 ships a migration script (`tools/migrate_v0_to_v1.py`) that renames camelCase keys to PascalCase and wraps bare parser-output files in a `DatabaseManifest` envelope. The script is applied to the 13 test fixtures. Consumers that maintain their own v0 files should run the same script. |
| Version detection | Consumers inspect `document.SchemaVersion` (for `DatabaseManifest` documents) or `document.Source.SchemaVersion` (for embedded `ParserOutput` nodes). `1.0` prefix indicates v1; `0.*` indicates v0. |

---

## Files Changed

| File | Change |
|---|---|
| `docs/test-cases/harvester-schema.v1.0.json` | Created — the v1.0 schema |
| `docs/schema/v1-adr.md` | Created — this document |
| `docs/test-cases/harvester-schema.v0.json` | Not modified — v0 remains as-is |
