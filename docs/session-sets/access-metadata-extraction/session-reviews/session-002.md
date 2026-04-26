# Verification Round 1

ISSUES_FOUND

1. **Severity:** Major  
   **Issue:** Phase 4.3 explicitly calls for query output-column metadata “when available,” but the typed query schema has no place to carry it. Even if Session 5 or a widened raw extractor can surface column info, `QueryMetadata` will require a contract change instead of just populating an existing optional field.  
   **Location:** `AccessHarvester.Access/QueryMetadata.cs`; `AccessHarvester.Access/QueryMetadataExtractor.cs`  
   **Fix:** Add an optional query-output-column field now (for example `IReadOnlyList<QueryOutputColumn>? OutputColumns`) and leave it `null` under the current raw IR shape. When wiring can obtain column metadata, populate it without changing the schema.

2. **Severity:** Major  
   **Issue:** Table-level validation-rule coverage is not actually reachable from the current raw IR shape. `TableMetadata` models `ValidationRule` / `ValidationText`, but `TableMetadataExtractor` reads top-level keys that `TableExporter` does not emit, so those fields will always be `null` with the current pipeline.  
   **Location:** `AccessHarvester.Access/TableMetadataExtractor.cs`; `AccessHarvester.Core/Extraction/TableExporter.cs`  
   **Fix:** Extend the raw table export shape to include table-level `ValidationRule` and `ValidationText`, or explicitly defer these fields in the session docs/tests as placeholders pending raw-exporter widening. Add extractor tests once the raw keys exist.

3. **Severity:** Minor  
   **Issue:** Several docstrings say native/local objects are `null`, but the implementation intentionally preserves empty strings from raw JSON and the tests assert that behavior. That makes the schema docs internally misleading in the same way Session 1 already corrected elsewhere.  
   **Location:** `AccessHarvester.Access/QueryMetadata.cs` (`Connect`); `AccessHarvester.Access/TableMetadata.cs` (`Connect`, `SourceTableName`)  
   **Fix:** Rewrite those remarks to say empty strings are preserved if Access/DAO wrote `""`; `null` means missing/unreadable, not “local/native” per se.

4. **Severity:** Minor  
   **Issue:** `QueryMetadata.Parameters` is documented as being emitted “in the order they were discovered,” but `ParseParameters` appends all form references before all report references. Mixed form/report references therefore do not preserve discovery order.  
   **Location:** `AccessHarvester.Access/QueryMetadata.cs` (`Parameters` remarks); `AccessHarvester.Access/QueryMetadataExtractor.cs` (`ParseParameters`)  
   **Fix:** Either update the docs to describe the actual ordering, or preserve textual order by collecting both regex match sets with their indices, merging, sorting, and then deduplicating.

5. **Severity:** Minor  
   **Issue:** `QueryParameter.Source` documents `unresolved` as an emitted value, but the extractor never produces unresolved parameter records; no detection yields `Parameters = null` instead.  
   **Location:** `AccessHarvester.Access/QueryMetadata.cs` (`QueryParameter.Source`)  
   **Fix:** Remove `unresolved` from the documented value set unless you plan to emit it, or add explicit unresolved-parameter emission and tests.

6. **Severity:** Minor  
   **Issue:** Relationship bitmask coverage is not complete in tests: `IsUnique` and `IsInherited` are decoded in production code but never asserted. Given the session’s emphasis on bitmask correctness, those flags should not remain untested.  
   **Location:** `tests/AccessHarvester.Tests/RelationshipMetadataExtractorTests.cs`  
   **Fix:** Add a case with `Attributes = 0x00000005` and assert `IsUnique == true`, `IsInherited == true`, plus existing join/enforcement expectations.

---

# Verification Round 2

## Verification Round 2

ISSUES_FOUND

1. **Round-1 finding:** `QueryMetadata` missing `OutputColumns` slot  
   **Status:** Resolved.  
   **Evidence:** `AccessHarvester.Access/QueryMetadata.cs` now includes `IReadOnlyList<QueryOutputColumn>? OutputColumns` and defines `QueryOutputColumn` as the reserved schema slot for future population.

2. **Round-1 finding:** `TableMetadata.ValidationRule` / `ValidationText` unreachable from current raw IR  
   **Status:** Partially resolved.  
   **Severity:** Major  
   **Location:** `AccessHarvester.Access/TableMetadata.cs`  
   **What's still missing:** The field-level remarks now correctly disclose the raw-IR coverage gap, but the actual pipeline is still unchanged, so these fields remain unpopulatable and will still always be `null` in the current exporter/extractor path. If the intended fix is documentation-only deferral, the higher-level type docs still overstate present coverage (`"covering the deliverables of Plan Phase 4.3"` / `"holds ... table-level validation rules"`).  
   **Fix:** Either widen `AccessHarvester.Core.Extraction.TableExporter` to emit table-level `ValidationRule` / `ValidationText`, or make the deferral explicit in the type-level/session-level coverage docs as well so current behavior is described consistently end-to-end.

3. **Round-1 finding:** `Connect` / `SourceTableName` docs misleadingly said `null` for native/local objects  
   **Status:** Resolved.  
   **Evidence:** Updated remarks now state that DAO empty strings are preserved and `null` specifically means unreadable/missing.

4. **Round-1 finding:** `QueryMetadata.Parameters` ordering docs contradicted implementation  
   **Status:** Resolved.  
   **Evidence:** The remarks now match the implemented ordering: declared first, then form references in source order, then report references in source order.

5. **Round-1 finding:** `QueryParameter.Source` documented `unresolved` though extractor never emitted it  
   **Status:** Resolved.  
   **Evidence:** `unresolved` was removed from the documented value set and the null-on-parent behavior is documented instead.

6. **Round-1 finding:** Relationship bitmask test coverage missed `IsUnique` / `IsInherited`  
   **Status:** Resolved.  
   **Evidence:** `tests/AccessHarvester.Tests/RelationshipMetadataExtractorTests.cs` now includes `Extract_UniqueAndInheritedFlagsDecodedFromAttributes` asserting both flags for `Attributes = 0x05`.

**New issues introduced by the patch:** None beyond item 2's remaining inconsistency.

---

# Verification Round 3

VERIFIED

The remaining Round-2 Major is resolved. `TableMetadata`’s type-level `<summary>` now explicitly states that `ValidationRule` / `ValidationText` are committed schema slots that the current raw IR does not populate, and it directs readers to the per-field remarks for that limitation. That makes the type-level documentation consistent with the field-level disclosure and no longer presents current behavior as full end-to-end coverage for table-level validation metadata.
