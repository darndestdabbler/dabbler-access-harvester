ISSUES_FOUND

1. **Major — Required absent-case coverage for `Filter` / `OrderBy` promotion is incomplete**
   - The spec explicitly requires `RecordSource` / `Filter` / `OrderBy` promotion coverage for **present/absent** cases.
   - In `FormMetadataExtractorTests.cs`, absent behavior is only explicitly pinned for `RecordSource`.
     - `Filter` present: covered
     - `OrderBy` present: covered
     - `Filter` absent: **not explicitly tested**
     - `OrderBy` absent: **not explicitly tested**
   - In `ReportMetadataExtractorTests.cs`, the same gap exists for the shared promotion shape.
   - Existing tests with null-valued properties or bag-removal assertions do not fully substitute for “key absent => typed field null” cases.

2. **Minor — `docs/ir-schema-forms-reports.md` has an accuracy issue around form section `Type`**
   - In the `AccessControl` section, the doc states: the form body section has `Type = "Detail"`, **not** `"Section"`.
   - That is too strong for the actual contract:
     - the extractor passes `Type` through from IR unchanged,
     - unit tests use `"Section"`,
     - code comments elsewhere reference `Section (Detail)`.
   - For downstream AI consumers, this should be documented as pass-through from IR rather than a hard normalized enum/value.

**Verified**
- The report-specific `DefaultView` tests are present and correctly ensure report codes are not decoded using form semantics.
- The `GroupLevels` test matrix required by the spec is present.
- Deferred-null tests for `KeepTogether` / `GroupingMode` / `GroupInterval` / `SortOrder` and `PageSetup` are present.
- The `VbaCode` JSON-escaping fix is correct: CR/LF are escaped before interpolation, producing valid JSON while preserving round-trip equality after extraction.
- No functional regression is evident from the provided implementation and passing test run.