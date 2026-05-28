## Harvester Body and Query SQL - Session 2 (D14)

This release focuses on enhancing SQL query processing and manifest generation.

*   **New:** `QuerySqlProjector.cs` introduced, converting query abstractions and DAO metadata into `QueryItem` JSON.
*   **Changed:** `DatabaseManifest.Queries` now stores `IReadOnlyList<JsonNode>` (representing `QueryItem`).
*   **Changed:** `DatabaseManifestAggregator.Build()` updated to accept `IEnumerable<JsonNode>` for queries.
*   **Updated:** `harvester-schema.v1.0.json` reflects the new `QueryItem` structure for manifest validation.
*   **New:** `Pipeline.cs` now includes `BuildQueryExports()`, which parses raw SQL, creates abstractions, projects them, and adds to the manifest.
*   **New:** 20 unit tests added for `QuerySqlProjectorTests.cs`.
*   **New:** `HomeHealthCare_FullRun_ManifestValidatesAgainstSchema` integration test implemented.
*   **Updated:** Existing tests (`DatabaseManifestAggregatorTests.cs`, `PredicateNodeRoundTripTests.cs`) adapted for the new `QueryItem` format.
*   **Rebuilt:** Golden corpus for HomeHealthCare and NorthwindDevEdition.