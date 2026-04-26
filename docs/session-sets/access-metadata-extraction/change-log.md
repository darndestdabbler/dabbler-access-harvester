- Introduces core metadata schemas: `AccessObjectCommon`, `FormMetadata`, `ReportMetadata`.
- Adds `QueryMetadata`, `TableMetadata`, and `RelationshipMetadata` with dedicated extractor classes.
- Implements 32 hermetic unit tests for new extractors.
- Develops 7 `.accdb` fixtures (cases 01/02/03/09/10/11/12) and corresponding `expected.json` contracts.
- Establishes `SyntheticFixtureSnapshotTests.cs` for validating fixture outputs.
- Adds `SyntheticMetadataExtractorIntegrationTests.cs`, passing 127 integration tests with a vacuous-pass guard.
- Ensures a clean build with no blocking issues.

---

**Next session-set recommendation (after access-metadata-extraction):**
`access-symbol-table-and-types`

**Recommended orchestrator for `access-symbol-table-and-types`:** Anthropic Claude Opus 4.7 @ effort=high
**Rationale:** This session set contains two high-leverage synthesis tasks (synthetic-declaration injection design, type-library registry curation) requiring significant architectural design. The preceding set's successful in-conversation Opus synthesis for schema design confirms that Opus is the right call for the synthesis sessions.