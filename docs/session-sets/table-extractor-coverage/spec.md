# Table Extractor Coverage Spec

> **Purpose:** Close the field-coverage gap between `TableExporter` / `RelationshipExporter`
> and the YAML extraction baseline identified in `access-object-extractor-spike`. Add
> table-level validation constraints, field display metadata, and a reconstructed DDL
> string to the C# extraction output. These fields are the highest-value additions
> for downstream AI interpretation.
> **Created:** 2026-05-14
> **Session Set:** `docs/session-sets/table-extractor-coverage/`
> **Prerequisite:** `access-object-extractor-spike` (gap analysis complete — see
> `docs/session-sets/access-object-extractor-spike/gap-analysis-and-strategy.md`)
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface; purely additive changes to the extraction layer. Quality
> is guarded by hermetic unit tests and cross-provider AI verification.

---

## Background

The spike (`access-object-extractor-spike`) identified these YAML-only fields that
belong in the C# `TableExporter` output:

| Field | Where in C# output | Source in DAO |
|---|---|---|
| Table-level `ValidationRule` | Missing | `TableDef.ValidationRule` |
| Table-level `ValidationText` | Missing | `TableDef.ValidationText` |
| Field `InputMask` | Missing | `Field.Properties("InputMask")` |
| Field `Format` | Missing | `Field.Properties("Format")` |
| Field `DisplayControl` | Missing | `Field.Properties("DisplayControl")` |
| DDL string | Missing | Reconstructed from `Fields` + `Relations` |

And one `RelationshipExporter` addition:

| Field | Where in C# output | Source in DAO |
|---|---|---|
| `EnforceReferentialIntegrity` (bool) | Implicit in `AttributesDecoded` only | Bitmask bit `0x00000002` absent = enforced |
| `CascadeUpdate` (bool) | `AttributesDecoded: ["UpdateCascade"]` | Bitmask bit `0x00000100` |
| `CascadeDelete` (bool) | `AttributesDecoded: ["DeleteCascade"]` | Bitmask bit `0x00001000` |

**Pipeline flow for tables:** `TableExporter` → `raw/` → `JsonPassthroughTranslator`
(copy) → `kb/` → (no table-specific enricher) → `ir/`. All additions land automatically
in `kb/` and `ir/` without touching Translation or Enrichment.

---

## Scope

### In scope
- `TableExporter.cs`: add `ValidationRule`, `ValidationText`, DDL string at the
  table level; add `InputMask`, `Format`, `DisplayControl` at the per-field level
- `RelationshipExporter.cs`: add explicit `EnforceReferentialIntegrity`, `CascadeUpdate`,
  `CascadeDelete` boolean fields
- Unit tests for the new fields (hermetic, no COM required)
- One integration test assertion per new table-level field using the committed
  HomeHealthCare fixtures (gated `[SkippableFact]`)

### Out of scope
- Translation or Enrichment changes (not needed — table JSON passes through)
- Index improvements (index field-level attributes are a lower-value addition)
- DataMacros capture (separate concern, not part of this set)
- Any form/report changes (that is `structured-form-report-extractor`)

### DDL reconstruction approach
Access DAO does not expose a `TableDef.DDL` property. The DDL must be reconstructed
from `TableDef.Fields` (column types, sizes, NOT NULL, DEFAULT, field-level constraints)
and `CurrentDb().Relations` (FK clauses). Session 1 step 1 is to verify this approach
against the committed YAML DDL sample — confirm the reconstructed output matches. If
DAO exposes DDL via another path (e.g., via `QueryDef` wrapping), use that instead.

---

## Session Plan

### Session 1 of 2: Core Additions (Table + Relationship)

**Steps:**
1. Read `AccessHarvester.Core/Extraction/TableExporter.cs` and
   `AccessHarvester.Core/Extraction/RelationshipExporter.cs` to understand the current
   payloads. Read the YAML sample `samples/DatabaseYaml/HomeHealthCare.accdb/Tables/tblClients.yaml`
   and `Relationships.yaml` for the target field coverage.
2. Add `ValidationRule` and `ValidationText` to the `TableExporter` table-level payload
   (DAO: `td.ValidationRule`, `td.ValidationText` via `SafeString`).
3. Add `InputMask`, `Format`, and `DisplayControl` to the per-field payload.
   - These may not be directly available on `Field` — they may require
     `Field.Properties("InputMask")` via a Properties collection accessor. Research
     the DAO access path and use `SafeString/SafeInt` wrappers. If a property is not
     set on a field (normal — most fields have no InputMask), omit it (emit `null`
     or exclude the key, consistent with existing convention).
4. Add DDL reconstruction helper method to `TableExporter`:
   - Read `td.Fields` for column type/size/NOT NULL/default
   - Read `db.Relations` filtered to this table for FK clauses
   - Reconstruct a `CREATE TABLE` string matching the YAML format
   - If the DAO approach is ambiguous, scope to `InputMask/Format/DisplayControl`
     only and note DDL as deferred to Session 2
5. Add explicit `EnforceReferentialIntegrity` (bool), `CascadeUpdate` (bool),
   `CascadeDelete` (bool) to `RelationshipExporter` payload alongside the existing
   `AttributesDecoded` list.
6. Route code review.
7. `dotnet build && dotnet test`
8. Route session verification.

**Creates:**
- `docs/session-sets/table-extractor-coverage/session-reviews/session-001.md`

**Touches:**
- `AccessHarvester.Core/Extraction/TableExporter.cs`
- `AccessHarvester.Core/Extraction/RelationshipExporter.cs`
- `tests/AccessHarvester.Tests/TableMetadataExtractorTests.cs` (new assertions)

**Ends with:** `dotnet build` green; at least one unit test asserting the new
table-level `ValidationRule` field and one asserting a new per-field `DisplayControl`
value. Verification VERIFIED.

**Progress keys:** `session-1/table-validation-rule`, `session-1/field-display-metadata`,
`session-1/relationship-bool-flags`, `session-1/ddl-reconstruction`

---

### Session 2 of 2: Tests, YAML Comparison, and Gap Close

**Steps:**
1. Verify prior session committed.
2. If DDL reconstruction was deferred from Session 1, implement it here.
3. Write hermetic unit tests for every new field (parametric test data; no COM required).
4. Write or update the integration test assertion (HomeHealthCare fixture, `[SkippableFact]`)
   confirming: `tblClients.ValidationRule` equals `"[EnrollmentDate] >= [DateOfBirth]"`;
   `FirstName.ValidationRule` equals `"Is Not Null"`; `SSNLast4.InputMask` equals `"0000"`.
5. Run `scripts/run_extraction.ps1` (if Access available) to regenerate
   `samples/DatabaseYaml/HomeHealthCare.accdb/Tables/tblClients.yaml` and compare the
   new C# JSON output against the YAML. Document any remaining unclosed gaps in a brief
   comparison note in `session-reviews/session-002.md`.
6. Route code review.
7. `dotnet build && dotnet test`
8. Route session verification.
9. Generate change-log and next-session-set recommendation.

**Creates:**
- `docs/session-sets/table-extractor-coverage/change-log.md`
- `docs/session-sets/table-extractor-coverage/next-session-set-recommendation.md`
- `docs/session-sets/table-extractor-coverage/session-reviews/session-002.md`

**Touches:**
- `AccessHarvester.Core/Extraction/TableExporter.cs` (DDL, if deferred)
- `tests/AccessHarvester.Tests/TableMetadataExtractorTests.cs`

**Ends with:** All new fields have hermetic unit tests. `dotnet test` green. YAML
comparison confirms the targeted fields are now present in C# output. Verification VERIFIED.

**Progress keys:** `session-2/ddl-if-deferred`, `session-2/hermetic-tests`,
`session-2/yaml-comparison`

---

## Deliverables

1. `AccessHarvester.Core/Extraction/TableExporter.cs` — adds table-level
   `ValidationRule`, `ValidationText`, DDL; per-field `InputMask`, `Format`,
   `DisplayControl`
2. `AccessHarvester.Core/Extraction/RelationshipExporter.cs` — adds
   `EnforceReferentialIntegrity`, `CascadeUpdate`, `CascadeDelete` booleans
3. Updated tests in `tests/AccessHarvester.Tests/`
4. `change-log.md`
5. `next-session-set-recommendation.md`
