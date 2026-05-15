# Change Log — structured-form-report-extractor

> Sessions 1–3 of 3. Completed 2026-05-15.

---

## Session 1: Audit the Current ir/ Output

**Read-only audit. No code changes.**

- Discovered that the `AccessHarvester.Access` project already defines typed
  `FormMetadata` / `ReportMetadata` records with fields for `RecordSource`,
  `GroupLevels`, etc., but has no extractor to populate them.
- Confirmed that the prior spike's premise was materially wrong: `RecordSource` is
  not a gap vs the YAML baseline (both store it in a Properties bag). The real gap
  is between the current `AccessNode` `ir/` JSON and the already-designed typed records.
- Wrote `audit-findings.md` with field-by-field gap analysis; surfaced the
  Option A vs Option B strategic choice.
- Human adjudicated **Option B** (write `FormMetadataExtractor` / `ReportMetadataExtractor`
  following the `TableMetadataExtractor` precedent).
- Spec updated to lock Session 2–3 scope to Option B.

**Creates:** `docs/session-sets/structured-form-report-extractor/audit-findings.md`

---

## Session 2: FormMetadataExtractor + ReportMetadataExtractor

- **`AccessHarvester.Access/FormMetadataExtractor.cs`** (new, 97 lines):
  Pure-logic transform from enriched IR JSON → typed `FormMetadata`. Exact-value
  lifts of `RecordSource`, `Filter`, `OrderBy` (removed from Properties bag after
  lift); decoded lift of `DefaultView` (raw code retained in Properties);
  recursive `AccessControl` tree via `AccessObjectReader` helpers; `ModuleName`
  null (deferred).

- **`AccessHarvester.Access/ReportMetadataExtractor.cs`** (new, 189 lines):
  Mirrors `FormMetadataExtractor` for shared fields. Adds `GroupLevels` construction
  by intersecting `GroupLevel<N>Header/Footer` section nodes in `Children` with
  `GroupLevel<N>` flat-key properties. Report-specific `DefaultView` codes
  (0=Report View, 1=Print Preview, 2=Layout View). `PageSetup` stubbed null
  (deferred). Per-level `GroupingMode`/`GroupInterval`/`SortOrder`/`KeepTogether`
  null (deferred — no real-fixture confirmation of raw shape).

- **`AccessHarvester.Access/AccessObjectReader.cs`** (new, 167 lines):
  Internal helpers shared by both extractors: `BuildPromotedPropertiesBag`,
  `BuildControlPropertiesBag`, `ReadChildren`, `ReadControl`, `ReadGeometry`,
  `ReadLabel`, `ReadStringArray`, `GetObject`, `ReadString`, `ReadDouble`,
  `DecodeDefaultView`.

- **`tests/AccessHarvester.Tests/SyntheticMetadataExtractorIntegrationTests.cs`**
  (modified): Added `Forms` and `Reports` to the validated category list; added
  `AssertForms` / `AssertReports` comparators; added `ReadNamedActualCategory`
  helper; added `AssertGroupLevels` comparator.

- **Bug fixes applied during code review** (Sonnet → gemini-pro verified):
  1. Report `DefaultView` codes separated from form codes (code "1" decodes to
     "Print Preview" on reports, not "Continuous Forms").
  2. `GroupLevel<N>` properties promoted to `GroupLevels` are now correctly
     excluded from the `Properties` bag via a `consumedGroupLevelKeys` set.
  3. Removed dead `ReadInt` helper from `AccessObjectReader`.

---

## Session 3: Hermetic Tests + Typed-Schema Documentation

- **`tests/AccessHarvester.Tests/FormMetadataExtractorTests.cs`** (new, 29 tests):
  Hermetic unit tests covering: `RecordSource`/`Filter`/`OrderBy` present and
  absent; key removal from Properties bag; `DefaultView` decoding all codes 0–5
  and unknown code; raw code retained in Properties; `Name` promotion on
  `AccessControl`; recursive children; `GeometryIn`/`Label`/`NonDefaultProperties`/
  `VbaCode` round-trips; `ModuleName` null (deferred); name from argument; guard
  conditions (empty name, null JSON, root array).

- **`tests/AccessHarvester.Tests/ReportMetadataExtractorTests.cs`** (new, 27 tests):
  All `FormMetadataExtractorTests` cases plus: report-specific `DefaultView` codes;
  "code 1 is not Continuous Forms" cross-type assertion; `GroupLevels` matrix
  (zero/header-only/footer-only/both/multi-level/missing-GroupOn/keys-removed/
  empty-value-excluded); deferred-null pins for `KeepTogether`/`GroupingMode`/
  `GroupInterval`/`SortOrder`; `PageSetup` null (deferred).

- **Bug fix** (`Extract_VbaCode_PassedThrough`): Test was embedding literal CR/LF
  characters into a JSON string, producing invalid JSON. Fixed by adding
  `.Replace("\r","\\r").Replace("\n","\\n")` to the escaping chain.

- **`docs/ir-schema-forms-reports.md`** (new, ~270 lines):
  Typed-schema reference for downstream AI consumers. Documents the full pipeline
  (SaveAsText → kb → ir → typed records), all public fields on `FormMetadata`,
  `ReportMetadata`, `AccessControl`, `LabelInfo`, `GeometryInches`, `GroupLevel`,
  `PageSetup`/`Margins`, a "Fields Not Re-Modeled" section for `Procedures`/
  `EventMacros`/binary blobs, and two worked examples (form + grouped report).

- **Test count:** 414 active tests pass, 2 SkippableFact skipped (Access COM).

---

## Deferred Items

The following were explicitly scoped out and are ready for a follow-on session:

| Item | Why deferred |
|---|---|
| `PageSetup` decoding (`PrtMip`/`PrtDevMode` binary blobs) | Binary-blob format requires a separate research spike; typed schema already documents the shape. |
| `GroupLevel` per-level metadata (`GroupingMode`, `GroupInterval`, `SortOrder`, `KeepTogether`) | No real-fixture confirmation of the raw property names/values; deferred until a live Access extract can confirm. |
| `ModuleName` population | Requires parsing `Attribute VB_Name` from `VbaCode`; straightforward but not in scope for this set. |
