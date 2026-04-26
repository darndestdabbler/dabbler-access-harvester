Acknowledged. The new `SyntheticFixtureSnapshotTests.cs` is a snapshot-based integration test that:

1. **Discovers** all `*.expected.json` files in `SyntheticDbs/`
2. **Pairs** each with its `.accdb` sibling
3. **Runs** `Pipeline.Default().WithCategories(...)` scoped to the categories present in the snapshot
4. **Asserts** all Extract/Translate/Enrich outcomes succeed
5. **Compares** each category's items by `Name` key, using `JsonNode.DeepEquals` on property-sorted normalized trees

One thing worth noting: `ResolveProjectRoot()` returns `AppContext.BaseDirectory` (the build output directory, e.g. `bin/Debug/net8.0/`), not the actual project source root. This means the `SyntheticDbs/` directory and its `.accdb` + `.expected.json` files must be copied to the output directory via the `.csproj` (e.g., `<Content CopyToOutputDirectory="PreserveNewest" />`). Since build/test passed, this is presumably already configured — just calling it out as a coupling point if someone later adds a new fixture and forgets the copy rule.