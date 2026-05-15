## Code Review: form-report-code-and-grouping Session 1

### AccessObjectReader.ReadModuleName

**Off-by-one risk — none, but the structure is fragile.**
The increment happens *after* the regex check, so 10 lines are scanned correctly. However the loop is slightly awkward; a `for (int i = 0; i < 10; i++)` with `ReadLine()` would be clearer and harder to misread.

**Regex allows embedded quotes in the module name?**
`[^"]+` stops at the first `"`, which is correct. No issue here.

**Empty-string vbaCode.**
`new StringReader("").ReadLine()` returns `null` immediately; the loop exits without touching `linesScanned`. Returns `null`. Correct, but untested.

**The 10-line ceiling is a silent failure mode.**
If Access ever prepends a BOM comment or a `VERSION` line before `Attribute VB_Name`, the scan quietly returns `null`. The limit is probably fine in practice, but a comment explaining *why* 10 is enough would prevent future maintainers raising the same question.

---

### ReportMetadataExtractor.BuildGroupLevels

**GroupLevel config nodes bleed into `meta.Children`.**
`AccessObjectReader.ReadChildren(root)` iterates *all* children unconditionally, so every `"GroupLevel 0"` config node also ends up in `meta.Children` alongside `PageHeader`, `Detail`, etc. These are metadata nodes, not design-band sections, so downstream consumers iterating `Children` for layout purposes will see unexpected entries. If this is intentional the XML-doc comment on `ReportMetadata.Children` should say so; otherwise filter them out in `ReadChildren` or in `BuildGroupLevels`.

**Duplicate config nodes silently overwrite.**
```csharp
configs[n] = ( ... );   // unconditional assignment
```
A second `"GroupLevel 0"` child overwrites the first. Probably impossible in valid IR JSON, but a comment or a `Debug.Assert(!configs.ContainsKey(n))` would make the assumption explicit.

**`sections` merge is correct but relies on value-type default.**
```csharp
sections.TryGetValue(sn, out var state);          // state = (false,false) if absent
sections[sn] = isHeader ? (true, state.HasFooter) : (state.HasHeader, true);
```
This works because `bool` defaults to `false`. It is subtle; a brief comment would help.

**`GroupInterval = 0` is representable.**
`int.TryParse("0", ...)` yields `gi = 0` and `GroupInterval = 0`. That's logically correct (not null for an explicit zero), but no test covers it.

**Cross-extractor coupling on `PromotedRootKeys`.**
```csharp
Properties = AccessObjectReader.BuildPromotedPropertiesBag(rawProperties, FormMetadataExtractor.PromotedRootKeys),
```
`ReportMetadataExtractor` takes a hard dependency on `FormMetadataExtractor` for a set that happens to be identical today. If report-specific promoted keys diverge (e.g. `GrpKeepTogether`) the report extractor silently retains them in the bag. The set should either be moved to `AccessObjectReader` or duplicated with a comment linking the two.

---

### Decoding Tables

All nine `GroupingMode` codes (0–9), both `SortOrder` codes, and all three `KeepTogether` codes are present and match the Access documentation. `DecodeLabel` correctly returns `null` for unknown codes rather than throwing. ✓

---

### Tests

**ModuleName test constructs JSON by hand-escaping.**
```csharp
string irJson = $$"""{ ..., "VbaCode": "{{vbaCode.Replace(...)}}" }""";
```
This works but is fragile (the escape order matters: `\` before `"`). Prefer `JsonSerializer.Serialize(vbaCode)` to produce a valid JSON string token and interpolate that, removing the brittle chain of `Replace` calls.

**Missing test: GroupLevel config node without a `Properties` key** (distinct from an empty `Properties` object).
`GetObject(child, "Properties")` returns `null` if the key is absent, and all `ReadString(null, ...)` calls return `null`. Correct, but the only test (`Extract_PerLevelMetadata_AbsentWhenConfigNodeHasNoProperties`) supplies `"Properties": { "GroupOnCol": "X" }` — it tests missing *optional* keys, not a missing `Properties` object entirely.

**Missing test: `GroupInterval = 0`** — `GroupInterval` should be `0` (int), not `null`.

**Missing test: GroupLevel config nodes in `meta.Children`** — the test suite never asserts whether or how these nodes appear in `Children`, making the double-representation invisible.

**Missing test: empty `VbaCode` string** — `ReadModuleName("")` returns `null`; no test covers this.

---

### Summary

| Severity | Issue |
|---|---|
| Medium | GroupLevel config nodes included in `meta.Children` (unspecified, potentially harmful) |
| Low | `PromotedRootKeys` borrowed from `FormMetadataExtractor`; divergence risk |
| Low | Duplicate GroupLevel config nodes silently overwrite |
| Low | Manual JSON escape chain in tests; fragile to escape-order bugs |
| Cosmetic | Off-by-one readability in `ReadModuleName` loop |
| Test gap | `GroupInterval = 0`, empty VbaCode, missing `Properties` key, Children pollution |