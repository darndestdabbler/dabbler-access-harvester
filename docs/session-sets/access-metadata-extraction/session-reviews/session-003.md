# Verification Round 1

VERIFIED

**All four criteria pass.**

### 1. Fixture builder changes — correct and lean ✓

- `build_metadata_fixtures.py` is cleanly separated from `build_synthetic.py`, with its own manifest-driven flow (tables → queries → relationships → forms → reports → expected.json).
- `builder.py` extensions are minimal: report lifecycle helpers (`_create_report`, `_close_report`), report control helpers (`_add_report_textbox`, `_add_report_label`, `_add_report_subreport`, `_report_create_control`, `_create_attached_report_label`), and the corresponding section/command constants.
- Output paths are uniformly `tests/AccessHarvester.Tests/SyntheticDbs/` — the stale `AccessExporter.Tests` path is gone from `build_synthetic.py`, `build_metadata_fixtures.py`, and `README.md`.
- No extractor wiring landed — this is fixture/contract authoring only as scoped.

### 2. Expected.json contracts — schema-aligned, not over-promised ✓

Every field used across the seven expected.json files maps to a typed slot in the C# schema:

| Contract field | Schema type |
|---|---|
| `Forms[].Name`, `.RecordSource`, `.Children` | `FormMetadata` |
| `Reports[].Name`, `.RecordSource`, `.GroupLevels`, `.Children` | `ReportMetadata` |
| `Tables[].Name`, `.Fields`, `.PrimaryKey` | `TableMetadata` |
| `Queries[].Name`, `.QueryType`, `.Sql` | `QueryMetadata` |
| `Relationships[].Name`, `.ParentTable`, `.ChildTable`, `.Fields` | `RelationshipMetadata` |
| `Children[].Type`, `.Name`, `.Label`, `.Properties` | `AccessControl` / `LabelInfo` |
| `Fields[].Name`, `.DataType`, `.Size` | `TableField` |
| `GroupLevels[].Level`, `.GroupOn`, `.HasHeader`, `.HasFooter` | `GroupLevel` |

No enrichment-phase fields are asserted (no `GeometryIn`, `NonDefaultProperties`, `VbaCode`, `ModuleName`, `DefaultView`). Contracts stay at the structural-metadata layer the COM builder can deterministically produce.

### 3. Fixture coverage — materially sufficient ✓

| Object type | Covered by |
|---|---|
| Form extraction (basic) | 01, 10 |
| Form with combos + subform | 02, 03, 09, 12 |
| Form with buttons (switchboard) | 12 |
| Empty/skeleton forms | 11, 12 |
| Report with group levels | 09 |
| Report skeleton | 12 |
| Query extraction | 03, 09 |
| Table + PK extraction | 02, 03, 09, 10, 11, 12 |
| Relationship graph | 02, 03, 11 |
| Control types: TextBox, ComboBox, CheckBox, CommandButton, Label, Subform | spread across all seven |
| Attached-label resolution | 01, 02, 03, 09, 10, 12 |
| ControlSource binding | 03, 09, 10 |

### 4. Notes (none rise to ISSUES FOUND)

- **SourceObject prefix convention**: The COM builder writes `"Form.sfrmOpenCases"` into the Access property, but expected.json asserts `"sfrmOpenCases"`. This is a legitimate contract choice — the Session 4/5 extractor will need to strip the `Form.`/`Report.` prefix. The convention is consistent across all subform/subreport expected entries.
- **`report-header` section not mapped**: `REPORT_SECTIONS` and `_create_reports` wire `report-footer` but not `report-header`. No current fixture needs it; the gap is immaterial for Session 3 and trivially patchable when needed.
- **`data_type` / `query_type` fields in manifests are documentary**: They inform the hand-authored `expected` blocks but are not consumed by the builder code. This is fine — they serve as the source-of-truth annotation for why the expected DataType/QueryType values are what they are.
