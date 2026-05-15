VERIFIED

Checked against the supplied Session 1 spec and code:

- `AccessObjectReader.ReadModuleName` scans up to the first 10 lines of `VbaCode`, extracts `Attribute VB_Name = "..."`, and returns null when `VbaCode` is absent or no `VB_Name` line is found.
- `ReportMetadataExtractor.BuildGroupLevels` now sources per-level metadata from child nodes typed `"GroupLevel N"` (space-separated), with:
  - `GroupOn` from `Properties["GroupOnCol"]`
  - `GroupingMode` from `Properties["GroupOn"]`
  - `SortOrder`, `KeepTogether`, `GroupInterval` from the same config node
  - `HasHeader` / `HasFooter` still from `GroupLevelNHeader/Footer` nodes
- Config nodes matching `"GroupLevel N"` are filtered out of `meta.Children`.
- Decode tables for `GroupingMode` (0–9), `SortOrder` (0–1), and `KeepTogether` (0–2) are present.
- `ModuleName` is wired in both `FormMetadataExtractor` and `ReportMetadataExtractor`.
- Tests shown are updated to the `"GroupLevel N"` shape and include module-name extraction, full decode-table coverage, and config-node filtering coverage.

No spec deviations found in the supplied implementation.