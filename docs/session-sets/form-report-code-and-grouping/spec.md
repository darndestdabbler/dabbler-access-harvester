# Form/Report Code and Grouping Spec

> **Purpose:** Populate the two remaining deferred fields on the typed
> `FormMetadata` / `ReportMetadata` schema: `ModuleName` (the VBA class-module
> name backing a form or report) and the per-level `GroupLevel` metadata fields
> (`GroupingMode`, `GroupInterval`, `SortOrder`, `KeepTogether`). Correct the
> `GroupLevels` construction in `ReportMetadataExtractor` to read from the
> correct source in the IR JSON, and update `docs/ir-schema-forms-reports.md`
> to remove the deferral notices.
> **Created:** 2026-05-15
> **Session Set:** `docs/session-sets/form-report-code-and-grouping/`
> **Prerequisite:** `structured-form-report-extractor` (complete — `FormMetadataExtractor`,
> `ReportMetadataExtractor`, and `docs/ir-schema-forms-reports.md` are all in place)
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. Quality is guarded by hermetic unit tests,
> integration tests, and cross-provider AI verification.

---

## Background

The prior set (`structured-form-report-extractor`) established the typed
`FormMetadata` / `ReportMetadata` schema and wired up extractors. Two areas
were explicitly deferred:

1. **`ModuleName`** — the VBA class-module name for forms/reports with
   code-behind (e.g. `Form_frmPatients`). The `Attribute VB_Name = "..."` line
   is present in the `VbaCode` string when Access emits it; extracting it is
   straightforward and was deferred only for time.

2. **GroupLevel per-level metadata** — `GroupingMode`, `GroupInterval`,
   `SortOrder`, `KeepTogether`. The Session 1 analysis in the prior set
   documented the `BuildGroupLevels` implementation as using a flat
   `GroupLevel<N>` root-Properties key for `GroupOn`, but flagged this as
   unverified against a real Access IR extract.

**GroupLevel source-of-truth finding (Session 0 analysis, 2026-05-15):**

`AccessTextParser` handles `Begin GroupLevel <N>` blocks by creating a child
`AccessNode` with `Type = "GroupLevel <N>"` (space-separated, e.g.
`"GroupLevel 0"`). The group-on expression lives in that child node's
`Properties["GroupOnCol"]`, and the per-level metadata (`GroupOn` / mode code,
`SortOrder`, `KeepTogether`, `GroupInterval`) lives in the same child node's
`Properties` bag.

The current `ReportMetadataExtractor.BuildGroupLevels` has two problems:
- It reads `GroupOn` from flat root Properties key `GroupLevel<N>` —
  incorrect for real Access output (those keys don't exist in real IR JSON).
- It does not read the per-level metadata from the `GroupLevel <N>` child node
  at all.

The hermetic tests in `ReportMetadataExtractorTests.cs` pass because they use
synthetic JSON that puts `GroupLevel0` as a flat Properties key — matching the
wrong implementation. Those tests must be updated as part of this session set.

The `GroupLevel<N>Header` / `GroupLevel<N>Footer` section nodes
(`Type = "GroupLevel0Header"`) are SEPARATE child nodes from the `GroupLevel <N>`
configuration node and are correctly detected by the current
`GroupLevelSectionRegex`. Only the configuration-node lookup needs to change.

---

## Scope

### In scope
- `ModuleName` extraction from the `Attribute VB_Name = "..."` line in
  `VbaCode`; integrated into both `FormMetadataExtractor` and
  `ReportMetadataExtractor`.
- Correct `ReportMetadataExtractor.BuildGroupLevels` to source `GroupOn`
  (the field expression) and per-level metadata from `GroupLevel <N>` child
  nodes rather than flat root Properties keys.
- Decoding tables for `GroupingMode` (0=EachValue, 1=Prefix, 2=Year, 3=Quarter,
  4=Month, 5=Week, 6=Day, 7=Hour, 8=Minute, 9=Interval), `SortOrder`
  (0=Ascending, 1=Descending), and `KeepTogether` (0=None, 1=WholeGroup,
  2=WithFirstDetail).
- `GroupInterval` is an integer (number of prefix characters or interval units);
  lifted as `int?` from the `GroupLevel <N>` child node.
- Update `ReportMetadataExtractorTests.cs` to use the correct IR JSON shape
  (child nodes with `Type = "GroupLevel 0"` instead of flat Properties keys).
- Update the deferred-null tests that pin `GroupingMode`/`SortOrder`/
  `KeepTogether`/`GroupInterval` to null — flip them to assert decoded values
  once the implementation is in place.
- Update `docs/ir-schema-forms-reports.md`: remove all `**Always null — decoding deferred**`
  notices; document the actual `GroupLevel <N>` child node as the source; update
  `ModuleName` note from "Currently null" to the extraction rule.
- `FormMetadataExtractorTests.cs`: add `ModuleName` extraction tests (VB_Name
  present, absent, malformed).
- `ReportMetadataExtractorTests.cs`: add `ModuleName` tests + updated GroupLevel tests.

### Out of scope
- `PageSetup` decoding from `PrtMip` / `PrtDevMode` binary blobs (still deferred).
- Changes to the `ir/` enricher chain or `SaveAsTextTranslator`.
- Any new synthetic `.accdb` fixture creation via `pywin32` / Access COM.

---

## Access GroupLevel SaveAsText Shape (reference)

Real Access SaveAsText for a grouped report produces blocks like:

```
Begin GroupLevel 0
   GroupOn = 0              ← GroupingMode code (0=EachValue, 1=Prefix, ...)
   GroupOnCol = "[DistrictID]"   ← the grouping field expression
   SortOrder = 0            ← 0=Ascending, 1=Descending
   KeepTogether = 0         ← 0=None, 1=WholeGroup, 2=WithFirstDetail
   GroupInterval = "1"      ← interval value (Prefix or Interval modes)
End GroupLevel
```

After `AccessTextParser`, the IR JSON child array contains a node:
```json
{ "Type": "GroupLevel 0", "Properties": { "GroupOn": "0", "GroupOnCol": "[DistrictID]",
  "SortOrder": "0", "KeepTogether": "0", "GroupInterval": "1" } }
```

The typed `GroupLevel` fields map as:
| Typed field | IR JSON source |
|---|---|
| `GroupOn` (expression) | `Properties["GroupOnCol"]` on the `GroupLevel <N>` child node |
| `GroupingMode` (decoded) | `Properties["GroupOn"]` decoded via table |
| `SortOrder` (decoded) | `Properties["SortOrder"]` decoded via table |
| `KeepTogether` (decoded) | `Properties["KeepTogether"]` decoded via table |
| `GroupInterval` | `Properties["GroupInterval"]` parsed as `int?` |

> **Note:** The property name `GroupOn` in SaveAsText is the *mode code*, not the
> grouping field. The field expression is `GroupOnCol`. This naming is an Access
> API quirk — the typed record uses `GroupOn` for the human-readable field expression
> (from `GroupOnCol`) to avoid confusion.

---

## Session Plan

### Session 1 of 3: Fix GroupLevel Source + ModuleName Extraction

**Steps:**
1. Verify the prior session set committed (`git status`).
2. Read `AccessHarvester.Access/ReportMetadataExtractor.cs` and the current
   `ReportMetadataExtractorTests.cs` GroupLevel tests to confirm the flat-key
   assumption; read `AccessHarvester.Core/Translation/AccessTextParser.cs` lines
   around `Begin GroupLevel` handling to confirm the `GroupLevel <N>` child node shape.
3. Rewrite `ReportMetadataExtractor.BuildGroupLevels`:
   - Remove the `GroupLevelPropertyRegex` / flat-Properties scan.
   - Add a `GroupLevelConfigRegex` matching `^GroupLevel \d+$` (space-separated)
     to identify configuration child nodes.
   - Scan `Children` in two passes: one for configuration nodes (`GroupLevel <N>`)
     → read `GroupOnCol`, `GroupOn` (mode code), `SortOrder`, `KeepTogether`,
     `GroupInterval`; one for section nodes (`GroupLevel<N>Header` /
     `GroupLevel<N>Footer`) → set `HasHeader` / `HasFooter`. Merge by level index.
   - Remove `consumedGroupLevelKeys` from the signature (no longer any flat root
     Property keys to consume); simplify `Extract` accordingly.
   - Add decoding tables for `GroupingMode`, `SortOrder`, `KeepTogether`.
   - Parse `GroupInterval` as `int?`.
4. Add `ModuleName` extraction to `AccessObjectReader` (shared helper):
   - Scan the first ~10 lines of `VbaCode` for a line matching
     `Attribute VB_Name = "<value>"` and return the value.
   - Return null when `VbaCode` is null or the attribute line is absent.
5. Wire `ModuleName` into `FormMetadataExtractor.Extract` and
   `ReportMetadataExtractor.Extract` (replaces the hardcoded `null`).
6. Update `ReportMetadataExtractorTests.cs`:
   - Rewrite GroupLevel tests to use `Type = "GroupLevel 0"` child nodes with
     `Properties["GroupOnCol"]` etc. instead of flat root Properties keys.
   - Flip the deferred-null tests for `GroupingMode`/`SortOrder`/`KeepTogether`
     to assert decoded values (or split into a separate new test for the decoded
     path + keep the null tests for absent-property cases).
   - Add `ModuleName` tests: VB_Name present, absent, malformed (no `=` sign).
7. Update `FormMetadataExtractorTests.cs`:
   - Add `ModuleName` extraction tests (same three cases as above).
8. Route code review on the extractor changes + updated tests.
9. `dotnet build && dotnet test` (sequential).
10. Route session verification (cross-provider).

**Creates:**
- Nothing new; modifies existing files only.

**Touches:**
- `AccessHarvester.Access/ReportMetadataExtractor.cs`
- `AccessHarvester.Access/AccessObjectReader.cs`
- `AccessHarvester.Access/FormMetadataExtractor.cs`
- `tests/AccessHarvester.Tests/ReportMetadataExtractorTests.cs`
- `tests/AccessHarvester.Tests/FormMetadataExtractorTests.cs`

**Ends with:** `BuildGroupLevels` reads from `GroupLevel <N>` child nodes.
`ModuleName` is populated from `Attribute VB_Name` when present. Hermetic tests
updated and green. Verification VERIFIED.

**Progress keys:** `session-1/grouplevels-fix`, `session-1/module-name`

---

### Session 2 of 3: Decoding Tables + Integration Test

**Steps:**
1. Verify prior session committed.
2. Review Session 1's work. Confirm the decoding tables for `GroupingMode`,
   `SortOrder`, `KeepTogether` are correct per the Access object model.
3. Add a `SkippableFact` integration test to
   `tests/AccessHarvester.Tests/SyntheticMetadataExtractorIntegrationTests.cs`
   (or a new `GroupLevelIntegrationTests.cs`) that:
   - Reads the `09-report-grouping-events` synthetic DB (or a real `.accdb` if
     present) through the full pipeline and runs `ReportMetadataExtractor.Extract`.
   - Asserts `GroupLevels[0].GroupOn == "DistrictID"` and that `HasHeader`/
     `HasFooter` are correct.
   - The test skips if Access COM is unavailable (existing `[SkippableFact]` pattern).
4. Update `SyntheticMetadataExtractorIntegrationTests.AssertGroupLevels` to also
   check `GroupOn` sourced from `GroupOnCol` (previously it only checked structural
   fields).
5. Route code review.
6. `dotnet build && dotnet test`.
7. Route session verification.

**Creates:**
- Nothing new (modifies `SyntheticMetadataExtractorIntegrationTests.cs`).

**Touches:**
- `tests/AccessHarvester.Tests/SyntheticMetadataExtractorIntegrationTests.cs`

**Ends with:** Integration test confirms the pipeline-to-extractor round-trip for
grouped reports. Verification VERIFIED.

**Progress keys:** `session-2/integration-test`

---

### Session 3 of 3: Schema Documentation Update + Close

**Steps:**
1. Verify prior session committed.
2. Update `docs/ir-schema-forms-reports.md`:
   - Remove all `**Always null — decoding deferred**` notices.
   - Add the `GroupLevel <N>` child node as the IR JSON source for per-level
     metadata; describe the property-name mapping (especially `GroupOnCol` →
     `GroupOn`, `GroupOn` code → `GroupingMode`).
   - Update `ModuleName` note: "populated by parsing `Attribute VB_Name = "..."`
     from `VbaCode`; null when absent or when the form/report has no code-behind."
   - Update the worked examples to include a report with `GroupingMode` decoded
     and a form with `ModuleName` populated.
3. Route documentation review.
4. `dotnet build && dotnet test`.
5. Route session verification.
6. Generate `change-log.md` and `next-session-set-recommendation.md`.

**Creates:**
- `docs/session-sets/form-report-code-and-grouping/change-log.md`
- `docs/session-sets/form-report-code-and-grouping/next-session-set-recommendation.md`

**Touches:**
- `docs/ir-schema-forms-reports.md`

**Ends with:** All deferral notices removed. Schema doc is fully current.
Verification VERIFIED.

**Progress keys:** `session-3/schema-docs-update`

---

## Deliverables

1. `ReportMetadataExtractor.BuildGroupLevels` reads from `GroupLevel <N>` child
   nodes (correct source).
2. `GroupingMode`, `SortOrder`, `KeepTogether`, `GroupInterval` decoded and
   populated on `GroupLevel` (Session 1).
3. `ModuleName` populated from `Attribute VB_Name` on `FormMetadata` and
   `ReportMetadata` (Session 1).
4. Updated hermetic tests — all GroupLevel tests use the correct IR JSON shape
   (Session 1).
5. Integration test confirming full pipeline round-trip for grouped reports
   (Session 2).
6. `docs/ir-schema-forms-reports.md` with all deferral notices removed and
   correct source-of-truth documentation (Session 3).
7. `change-log.md` + `next-session-set-recommendation.md` (Session 3).

**Explicitly deferred:** `PageSetup` decoding from `PrtMip` / `PrtDevMode`.
