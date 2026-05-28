# Harvester CLI Validate Subcommand - Session 1 of 1

**Date:** 2024-03-01

- - -

### AccessHarvester.Pipeline/Validation/SchemaValidator.cs
- Rewrote internal caching from a single `ConcurrentDictionary` keyed by file path to a two-level cache: `_pathCache` (`OrdinalIgnoreCase`, path → `Lazy<string>`) and `_schemaCache` (`Ordinal`, schema $id → `Lazy<JsonSchema>`).
- Added private `SchemaValidator(JsonSchema)` constructor.
- Added public static `SchemaValidator.FromText(string schemaJson)` factory that loads from in-memory JSON text.
- Added private static `GetOrCreate(string schemaJson)` method.
- Added private static `ExtractId(string schemaJson)` helper.
- Fixed pre-existing CS8602 nullable warning (`results.Details ?? []` in the `Validate` method).

### AccessHarvester.Cli/AccessHarvester.Cli.csproj
- Added `EmbeddedResource` item that compiles `docs/test-cases/harvester-schema.v1.0.json` into the binary with `LogicalName AccessHarvester.Cli.harvester-schema.v1.0.json`.

### AccessHarvester.Cli/Program.cs
- Added "validate" to the bare-file-mode exclusion check in `Dispatch()`.
- Added "validate" case to the dispatcher switch → `RunValidate()`.
- Added `RunValidate(string[] args)` method implementing: `--in <output-root-dir>`, `--manifest <path>`, `--json-status`, exit codes 0/1/2/3.
- Added `LoadEmbeddedSchema()` helper that loads `harvester-schema.v1.0.json` from the assembly's embedded resource.
- Added `WellKnownHint(string error)` helper that maps known v1.0 validation error patterns to actionable suggestions.
- Updated `PrintUsage()` to document the validate subcommand and its exit codes.

### examples/validate-invalid-manifest.json
- Added minimal invalid manifest fixture.

### tests/AccessHarvester.Pipeline.Tests/Validation/SchemaValidatorTests.cs
- Added `FromText_ConformantDocument_ReturnsValid` test.
- Added `FromText_InvalidDocument_ReturnsErrors` test.

### tests/AccessHarvester.Integration/Cli/CliValidateTests.cs
- Added 11 black-box integration tests covering: valid manifest with `--manifest` (exit 0), invalid manifest (exit 3 + stderr), missing file (exit 1), non-JSON input (exit 1), flat `--in` with valid manifest (exit 0), nested `AccessMetaData/<dbName>/manifest.json` layout via `--in` (exit 0), no `manifest.json` found (exit 1), directory not found (exit 1), no options (exit 1), both `--in` and `--manifest` (exit 1), `--json-status` envelope for valid and invalid manifests.

### tests/AccessHarvester.Integration/Cli/CliUsageTextTests.cs
- `PrintUsage_ListsAllDispatcherSubcommands`: added validate assertion.
- `PrintUsage_DocumentsExitCodesForEachSubcommandGroup`: added "Exit codes for validate:" assertion.

### tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs
- Added `PackagedBinary_Validate_ConformantManifest_ReturnsZero [SkippableFact]` test.

### docs/harvester/install.md
- "What you get" section: added validate to the list of subcommands that do not require Access.
- "How to invoke" section: added validate `--in` and validate `--manifest` examples; added validate exit codes.
- "Verifying an install" section: expanded from three to four smoke-check invocations, adding a validate step.

### README.md
- Repository Layout: updated Cli subcommand list to enumerate all six subcommands.
- CLI Usage: added validate and generate-docs subsections.

### CLAUDE.md, AGENTS.md, GEMINI.md
- Solution Structure bullet: updated subcommand list from "extract, translate, run, parse, etc." to "extract, translate, run, generate-docs, validate, parse".