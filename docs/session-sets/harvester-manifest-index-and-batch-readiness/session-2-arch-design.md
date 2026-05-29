### 1. Manifest v1.1 SHAPE — additive fields

New top-level nullable fields `Documentation` and `FileMap` will be added to `DatabaseManifest`.

-   **`Documentation`**: A single field containing a nested object with a canonical URL to the interpretation guide and the schema version.
    -   Shape: `Documentation: { Url, SchemaVersion }`
    -   Canonical URL: `https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md`

-   **`FileMap`**: A per-category navigation index pointing to on-disk Intermediate Representation (IR) files.
    -   Shape: An object with keys for each main category. Each key's value is an object containing `count` and a relative `glob`.
    -   Glob convention: Relative to the `manifest.json` file's directory. For a category `<C>`, the glob will be `ir/<DiskCategory>/*.ir.json`. The on-disk `ModulesAndClasses` directory maps to the `Modules` schema category.
    -   Example structure:
        ```json
        "FileMap": {
          "Forms":         { "count": 5, "glob": "ir/Forms/*.ir.json" },
          "Reports":       { "count": 2, "glob": "ir/Reports/*.ir.json" },
          "Queries":       { "count": 10, "glob": "ir/Queries/*.ir.json" },
          "Modules":       { "count": 3, "glob": "ir/ModulesAndClasses/*.ir.json" },
          "Tables":        { "count": 8, "glob": "ir/Tables/*.ir.json" },
          "Relationships": { "count": 7, "glob": "ir/Relationships/*.ir.json" }
        }
        ```

-   **Coexistence:** The `FileMap` field COEXISTS with the existing inlined content arrays (`Forms`, `Reports`, etc.). This preserves backward compatibility for consumers that only read the inlined content, while providing the new navigation index for consumers that need to access individual object files. Replacing the arrays would be a breaking change and violate the additive schema policy.

-   **Invariant Enforcement:** The `ManifestCompletenessTests` invariant remains enforceable. The `FileMap.<X>.count` field will be derived from the same source collection as `Summary.<X>Count` and `manifest.<X>.Length`, ensuring `FileMap.<X>.count == manifest.<X>.Length` by construction. The test will verify this against the on-disk file count.

### 2. Schema v1.1 JSON Schema file — concrete edits to `harvester-schema.v1.1.json`

-   **File Management:**
    -   Rename `AccessHarvester.Cli/harvester-schema.v1.0.json` to `harvester-schema.v1.1.json`.
    -   The v1.1 schema will be a standalone, self-contained file. Do not use `$ref` to v1.0.

-   **Root Object Edits:**
    -   Update the `SchemaVersion` property: `"properties": { "SchemaVersion": { "const": "1.1" } }`.
    -   Add optional `Documentation` and `FileMap` properties to the root `DatabaseManifest` definition.

      ```json
      "properties": {
        ...
        "Documentation": { "$ref": "#/$defs/DocumentationDefinition" },
        "FileMap": { "$ref": "#/$defs/FileMapDefinition" },
        ...
      }
      ```

-   **New Definitions in `$defs`:**
    ```json
    "DocumentationDefinition": {
      "type": "object",
      "required": ["Url", "SchemaVersion"],
      "properties": {
        "Url": { "type": "string", "format": "uri" },
        "SchemaVersion": { "type": "string" }
      }
    },
    "FileMapDefinition": {
      "type": "object",
      "required": ["Forms", "Reports", "Queries", "Modules", "Tables", "Relationships"],
      "properties": {
        "Forms": { "$ref": "#/$defs/FileMapEntryDefinition" },
        "Reports": { "$ref": "#/$defs/FileMapEntryDefinition" },
        "Queries": { "$ref": "#/$defs/FileMapEntryDefinition" },
        "Modules": { "$ref": "#/$defs/FileMapEntryDefinition" },
        "Tables": { "$ref": "#/$defs/FileMapEntryDefinition" },
        "Relationships": { "$ref": "#/$defs/FileMapEntryDefinition" }
      }
    },
    "FileMapEntryDefinition": {
      "type": "object",
      "required": ["count", "glob"],
      "properties": {
        "count": { "type": "integer", "minimum": 0 },
        "glob": { "type": "string" }
      }
    }
    ```

-   **Drift #2 Fix (`RelationshipDefinition`):** Rename properties to match the typed record.
    ```diff
     "RelationshipDefinition": {
    -  "required": ["Name", "Table", "ForeignTable"],
    +  "required": ["Name", "ParentTable", "ChildTable"],
       "properties": {
         "Name": { "type": "string" },
    -    "Table": { "type": "string" },
    -    "ForeignTable": { "type": "string" },
    +    "ParentTable": { "type": "string" },
    +    "ChildTable": { "type": "string" },
         "Attributes": { "type": "integer" },
         "Fields": {
           "type": "array",
           "items": {
             "type": "object",
    +        "required": ["ParentField", "ChildField"],
             "properties": {
    -          "Name": { "type": "string" },
    -          "ForeignName": { "type": "string" }
    +          "ParentField": { "type": "string" },
    +          "ChildField": { "type": "string" }
             }
           }
         }
       }
     }
    ```

-   **Drift #3 Fix (`TableIndex`):** Rename properties and change `Fields` to be an array of objects.
    -   Add `IndexFieldDefinition` to `$defs`:
      ```json
      "IndexFieldDefinition": {
        "type": "object",
        "required": ["Name"],
        "properties": {
          "Name": { "type": "string" },
          "IsDescending": { "type": "boolean" },
          "Attributes": { "type": "integer" }
        }
      }
      ```
    -   Update `TableIndex`:
      ```diff
       "TableIndex": {
         "required": ["Name"],
         "properties": {
           "Name": { "type": "string" },
      -    "Primary": { "type": "boolean" },
      -    "Unique": { "type": "boolean" },
      -    "Foreign": { "type": "boolean" },
      -    "Fields": { "type": "array", "items": { "type": "string" } }
      +    "IsPrimary": { "type": "boolean" },
      +    "IsUnique": { "type": "boolean" },
      +    "IsRequired": { "type": "boolean" },
      +    "Fields": {
      +      "type": "array",
      +      "items": { "$ref": "#/$defs/IndexFieldDefinition" }
      +    }
         }
       }
      ```

-   **Drift #1 Fix (`ParserOutput.Items`):** Use a discriminator for `HarvestedItem`. This is the correct Draft 2020-12 approach and provides clear validation paths.
    -   Add `ProcedureBodyDefinition` to `$defs`:
      ```json
      "ProcedureBodyDefinition": {
        "type": "object",
        "properties": {
          "References": { "type": "array", "items": { "type": "object" } },
          "Calls": { "type": "array", "items": { "type": "object" } },
          "SqlStrings": { "type": "array", "items": { "type": "string" } }
        }
      },
      ```
    -   Add `Body` property to `ProcedureItem`:
      ```json
      "ProcedureItem": {
        ...
        "properties": {
          ...
          "Signature": { "type": "object" },
          "Body": { "$ref": "#/$defs/ProcedureBodyDefinition" }
          ...
        }
      }
      ```
    -   Modify `HarvestedItem` to use the discriminator:
      ```diff
       "HarvestedItem": {
      -  "oneOf": [
      +  "discriminator": { "propertyName": "ItemKind" },
      +  "anyOf": [
           { "$ref": "#/$defs/ProcedureItem" },
           { "$ref": "#/$defs/QueryItem" },
           { "$ref": "#/$defs/ExpressionItem" }
         ]
       }
      ```

-   **Backward Compatibility:** The `examples/northwind-output.json` fixture will **fail** against v1.1 if not updated, because the new top-level `Documentation` and `FileMap` fields are required by the C# record and thus should be required in a strict schema. To maintain the `additive` policy, `Documentation` and `FileMap` are made **optional** at the schema's root level (not in `required` array). This ensures old v1.0 documents (which lack these fields) remain valid against the v1.1 schema. The harvester will always emit them for v1.1.

### 3. C# typed records — concrete edits

-   **`DatabaseManifest.cs`:**
    ```csharp
    // in AccessHarvester.Pipeline/Manifest/DatabaseManifest.cs
    public sealed record DatabaseManifest
    {
        [JsonPropertyOrder(-100)]
        public string SchemaType { get; } = "DatabaseManifest";
        [JsonPropertyOrder(-99)]
        public string SchemaVersion { get; } = "1.1"; // <-- BUMPED

        [JsonPropertyOrder(-96)] // <-- NEW
        public required Documentation Documentation { get; init; }
        [JsonPropertyOrder(-95)] // <-- NEW
        public required FileMap FileMap { get; init; }

        // ... existing properties
    }

    // NEW record definitions in a new file, e.g., AccessHarvester.Pipeline/Manifest/Navigation.cs
    public sealed record Documentation
    {
        public required string Url { get; init; }
        public required string SchemaVersion { get; init; }
    }

    public sealed record FileMap
    {
        public required FileMapEntry Forms { get; init; }
        public required FileMapEntry Reports { get; init; }
        public required FileMapEntry Queries { get; init; }
        public required FileMapEntry Modules { get; init; }
        public required FileMapEntry Tables { get; init; }
        public required FileMapEntry Relationships { get; init; }
    }

    public sealed record FileMapEntry
    {
        public required int count { get; init; }
        public required string glob { get; init; }
    }
    ```
    - The `RelationshipMetadata` and `TableIndex` records in C# already have the correct property names (`ParentTable`, `IsPrimary`, etc.). No C# changes are needed for drift fixes #2 and #3; the task is to align the schema with the existing C# models.

-   **`DatabaseManifestAggregator.Build(...)`:**
    -   The `Build` method signature will be updated to accept the pre-computed `Documentation` and `FileMap` objects.

      ```csharp
      // in AccessHarvester.Core/DatabaseManifestAggregator.cs
      public DatabaseManifest Build(
          string harvesterVersion,
          DatabaseInfo dbInfo,
          Documentation documentation, // <-- NEW
          FileMap fileMap,           // <-- NEW
          // ... existing collections
      ) { /* ... */ }
      ```
    - The aggregator will assign these directly to the `DatabaseManifest` instance. It will not compute them. This keeps concerns separate.

-   **`Pipeline.EmitManifestArtifacts()`:**
    -   This method is responsible for constructing the `Documentation` and `FileMap` objects.
    -   **Order of Operations:**
        1.  Emit all per-object `ir/*.ir.json` files to disk.
        2.  After emission, construct the `FileMap` object using the counts from the in-memory collections passed to the per-object emitters. Globs are static constants.
        3.  Construct the `Documentation` object using constants.
        4.  Call `DatabaseManifestAggregator.Build(...)` with the new objects and the data collections.
        5.  Serialize and write `manifest.json`.

### 4. CLI + embedded-resource edits

-   **`AccessHarvester.Cli/Program.cs`:**
    -   Update constant: `const string SchemaVersionLiteral = "1.1";`.
-   **`AccessHarvester.Cli/AccessHarvester.Cli.csproj`:**
    -   Rename the file `harvester-schema.v1.0.json` to `harvester-schema.v1.1.json`.
    -   Update the project file:
      ```xml
      <EmbeddedResource Include="harvester-schema.v1.1.json" />
      ```
-   **`validate` subcommand:**
    -   Update any help text or version output that explicitly mentions "v1.0" to "v1.1".
-   **Resource embedding:** Embed v1.1 only. The `validate` command's purpose is to ensure the harvester's *current* output conforms to its *current* schema. There is no requirement to validate historical output formats.

### 5. READ-ME-FIRST.md emission

-   **Location in Pipeline:** In `Pipeline.cs`, method `EmitManifestArtifacts()`, immediately after `manifest.json` is successfully written to disk.
-   **File Location:** `<out>/AccessMetaData/<dbName>/READ-ME-FIRST.md`
-   **Exact Content:**
    ```markdown
    # Dabbler Access Harvester Output

    The canonical entry point for this dataset is `manifest.json`.

    This data conforms to `harvester-schema` version 1.1.

    For a guide on interpreting the contents, see:
    https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md
    ```
-   **Determinism:** The content is static and contains no timestamps or run-specific data. It is byte-identical across runs.

### 6. Tests to add / modify

-   **`ManifestSchemaConformanceTests`:** Un-skip the test `HomeHealthCare_FullRun_OutputManifest_ConformsToSchema()`. It must be updated to load `harvester-schema.v1.1.json` for validation.
-   **New Backward-Compatibility Test:** Add `V1_0_Shaped_Fixture_ValidatesAgainst_V1_1_Schema()` that loads `examples/northwind-output.json` and asserts it passes validation against the v1.1 schema. This proves the additive policy is met.
-   **New `FileMap` Invariant Test:** In `ManifestCompletenessTests` or a new test class, add a test that:
    1.  Loads a generated manifest (`HomeHealthCare` golden is a good candidate).
    2.  For each category (e.g., `Forms`): asserts `manifest.FileMap.Forms.count == manifest.Forms.Length`.
    3.  Counts the files on disk matching the glob (e.g., `ir/Forms/*.ir.json`) and asserts this count also equals `manifest.FileMap.Forms.count`.
-   **New `READ-ME-FIRST.md` Test:** Add a test that runs the pipeline and asserts:
    1.  `READ-ME-FIRST.md` exists at the correct path.
    2.  Its content matches the specified markdown string exactly.
-   **Existing Test Updates:** No C# model renames are needed. The serializer tests for `RelationshipMetadata` and `TableIndex` may not exist, but if they do, they should be reviewed to ensure they produce JSON that conforms to the new v1.1 schema. The primary changes are in the schema file itself.

### 7. Golden regeneration plan

1.  **Build:** Compile the `AccessHarvester.Cli` project with all schema and code changes.
2.  **Execute:** Run the `tmp/build-golden-corpus` script, targeting both `HomeHealthCare` and `NorthwindDevEdition`.
3.  **Validate:** The script's internal validation step (which uses the `validate` CLI command) should pass, now using the v1.1 schema.
4.  **Diff:** Use a git diff tool to compare the regenerated `samples/GoldenOutput/` directories against the checked-in versions.
    -   **Expected changes in `manifest.json`:**
        -   `SchemaVersion`: "1.0" → "1.1"
        -   New top-level `Documentation` object.
        -   New top-level `FileMap` object.
        -   Fields inside `Relationships` array renamed (`Table`→`ParentTable`, etc.).
        -   Fields inside `Tables[...].Indexes` arrays renamed/reshaped (`Primary`→`IsPrimary`, `Fields` is now an array of objects).
    -   **Expected new file:** `READ-ME-FIRST.md` at the root of each golden's directory.
    -   No other changes are expected.
5.  **Determinism:** Run the `tmp/build-golden-corpus` script a second time. A byte-for-byte comparison of the output directories (after stripping `ExportedAt` and `HarvesterVersion` from `manifest.json` as is done currently) must show zero differences. The new `Documentation` and `FileMap` fields are deterministic and should NOT be excluded from this comparison.

### 8. Risks and decisions you punted to the orchestrator

-   **JsonSchema.Net Discriminator Behavior:** The use of the `discriminator` keyword is the correct, modern approach for Drift #1. However, if the specific version of `JsonSchema.Net` used in the project has a bug or incomplete implementation of this Draft 2020-12 feature, validation might not behave as expected.
    -   **Fallback:** If `discriminator` fails, the implementer should revert `HarvestedItem` to use a series of `if/then/else` clauses, which is more verbose but functionally equivalent and supported by older schema drafts.
      ```json
      "allOf": [
        {
          "if": { "properties": { "ItemKind": { "const": "Procedure" } } },
          "then": { "$ref": "#/$defs/ProcedureItem" }
        },
        {
          "if": { "properties": { "ItemKind": { "const": "Query" } } },
          "then": { "$ref": "#/$defs/QueryItem" }
        },
        // ... and so on
      ]
      ```