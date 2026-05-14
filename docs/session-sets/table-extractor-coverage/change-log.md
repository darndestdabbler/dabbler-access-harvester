## Change Log: table-extractor-coverage

This session set introduces comprehensive metadata extraction for Access database tables and relationships, significantly enhancing the `TableExporter` and `RelationshipExporter` capabilities. The primary goal was to lift a richer set of DDL-relevant properties from Access databases, enabling more faithful reconstruction and analysis of their schema.

The first session, already merged to `main`, focused on expanding the `TableExporter` to include table-level `ValidationRule` and `ValidationText`, along with per-field `InputMask`, `Format`, and `DisplayControl`. For `RelationshipExporter`, explicit `EnforceReferentialIntegrity`, `CascadeUpdate`, and `CascadeDelete` booleans were added. Core types like `TableMetadata` and `TableField` were updated with these new properties, and `TableMetadataExtractor` was extended with typed lifts and corresponding unit tests to support this richer data. DDL reconstruction was explicitly deferred to the second session.

The second session, part of this commit, implements the DDL reconstruction logic. A new `TableDdlBuilder` was developed as a pure-logic component to reconstruct Jet CREATE TABLE statements. The `TableExporter.CollectRelationsByChildTable` method was updated to thread relationship data into `BuildTablePayload`, which now emits the generated DDL as a `Ddl` field. The `TableMetadata.Ddl` property was introduced and lifted by `TableMetadataExtractor`, and `Ddl` was added to `PromotedKeys`. Extensive hermetic tests were added in `TableDdlBuilderTests` (13 new tests) and DDL roundtrip tests were integrated into `TableMetadataExtractorTests`. A new integration test, `HomeHealthCareTableCoverageTests`, was introduced. This test uses a committed `HomeHealthCare.accdb` database, piping its tables and relationships through the pipeline. It asserts specific metadata properties, including `ValidationRule`, `ValidationText`, `InputMask='0000'`, `DisplayControl=109`, `FirstName.ValidationRule='Is Not Null'`, and verifies that the `Ddl` starts with 'CREATE TABLE [tblClients] (' and contains `COUNTER` and a `PrimaryKey` constraint. It also confirms that `rel_State_Client` has `EnforceReferentialIntegrity=true`, `CascadeUpdate=true`, and `CascadeDelete=false`. During code review, several high-priority issues were identified and addressed, including incorrect handling of `TEXT(0)`/`BINARY(0)` for null sizes, case sensitivity in child-table lookups, a double-negative in a boolean variable, and fragile line-ending assertions. These fixes were applied and supplemented with multi-column primary and foreign key tests.

The final test result is a green `dotnet build` with all 41 focused tests passing. Pre-existing 45 `SymbolDb` test failures are confirmed identical to `main` and are unrelated to this change set.

### Files changed

*   `TableExporter.cs`
*   `RelationshipExporter.cs`
*   `TableMetadata.cs`
*   `TableField.cs`
*   `TableMetadataExtractor.cs`
*   `TableDdlBuilder.cs` (new)
*   `TableDdlBuilderTests.cs` (new)
*   `TableMetadataExtractorTests.cs`
*   `HomeHealthCareTableCoverageTests.cs` (new)
*   `HomeHealthCare.accdb` (new)

### Verification

*   Session 1: VERIFIED
*   Session 2: VERIFIED