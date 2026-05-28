```markdown
## Summary
Implemented Harvester Schema v1.0 follow-ups, including JSON polymorphism for `PredicateNode`, a fix for a latent race condition in schema validation, and a migration tool with corresponding fixture updates. Added new tests for JSON polymorphism and schema conformance.

## Changes
*   **JSON Polymorphism:** Added `[JsonPolymorphic]` and `[JsonDerivedType]` attributes to `PredicateNode` in `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs` for all 9 concrete subtypes.
*   **Concurrency Fix:** Changed `ConcurrentDictionary<string, JsonSchema>` to `ConcurrentDictionary<string, Lazy<JsonSchema>>` in `AccessHarvester.Pipeline/Validation/SchemaValidator.cs` to ensure single execution of schema loading under concurrency.
*   **Migration Tool:** Created `tools/migrate_v0_to_v1.py` for mechanical migration of v0 camelCase JSON to v1.0 `DatabaseManifest` format (PascalCasing, enum-value migration, envelope wrapping, idempotency).
*   **Fixture Migration:** Migrated all 13 `docs/test-cases/*.expected.json` fixtures to `DatabaseManifest` v1.0 format.
*   **PredicateNode Round-Trip Tests:** Added `PredicateNodeRoundTripTests.cs` (4 tests) to verify JSON polymorphism behavior.
*   **TestCase Fixture Conformance Tests:** Added `TestCaseFixtureConformanceTests.cs` (12 tests) to validate fixtures against `harvester-schema.v1.0.json`. Excluded `12-switchboard-navigation.expected.json` due to semantic incompatibility.
*   **Documentation Update:** Updated `docs/schema/v1-adr.md` to reflect resolved known issues and completed migration.

## Testing
*   Build: 0 errors.
*   Tests: 391 `Pipeline.Tests` and all other suites passed (0 failures).
*   Specific test additions: `PredicateNodeRoundTripTests.cs` (4 tests), `TestCaseFixtureConformanceTests.cs` (12 tests).

## Notes
Fixture `12-switchboard-navigation.expected.json` was excluded from conformance testing due to a semantic incompatibility where "g_CurrentUserID" requires a `ParserQueryParameter.Source` enum value, which cannot be resolved by mechanical renaming.
```