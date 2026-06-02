# Change Log — harvester-sql-migration-generator
**Status:** COMPLETE (2026-06-01)
**Sessions:** 3 of 3
**Maintainer:** claude-code (Claude Opus 4.8)
**Target release:** v1.2.0

## Summary
This session set delivers the `generate-migration` CLI subcommand, the final runnable product of the classify -> sanitize -> migrate pipeline. It emits a deterministic SQL Server migration (schema + data-load) from already-harvested artifacts: `ir/Tables`, `ir/Relationships`, the reviewed `classification.json`, and the sanitize CSV-per-table output.

This feature is Phase 4 of the data-classification plan and the long-deferred capstone of the mechanical harvester. With its completion, the harvester development arc is finished, and the repository transitions to a consumer-driven-only model.

## Per-session work
### Session 1: Input contract + Access->SQL Server DDL
- Defined the sanitize CSV on-disk contract (`SanitizeCsvContract`) and the generator input model (`MigrationInputs`, `MigrationIrReader` for structured per-field IR metadata).
- Implemented the Access->SQL Server type map (`SqlServerTypeMapper`), `CREATE TABLE` generation with nullability/defaults/PK (`DefaultValueTranslator`, `SqlServerDdlGenerator`), and `FOREIGN KEY` emission from `ir/Relationships`.
- Configured unmappable types to route to flagged degradations.
- Re-confirmed stop-rule against `HomeHealthCare` and `NorthwindDevEdition` golden fixtures (`MigrationFixtureCoverageTests`).
- Fixed platform-dependent newlines, unsafe `#...#` default translation, and coverage issues identified during verification.

### Session 2: Data-load + identity + FK-safe ordering
- Refactored `SqlServerDdlGenerator` into a shared `MigrationSchemaResolver` and `SqlServerDdlEmitter`, maintaining DDL byte-identical output.
- Added `TableDependencyOrderer` for FK-safe load order via Tarjan SCC + deterministic Kahn, flagging `ForeignKeyCycle` for true multi-table SCCs.
- Implemented `CsvTableReader` (RFC 4180; documenting `CsvWriter` `''->NULL` collapse) and `SqlValueFormatter` (typed `INSERT` literals, binary flagged not-loadable).
- Developed `SqlServerDataLoadGenerator` (INSERTs in FK-safe order, `SET IDENTITY_INSERT ON/OFF` where applicable, flagging missing-file/missing-column).
- Implemented `SqlServerMigrationGenerator` to combine DDL, order, data, and merged byte-stable degradations.
- Fixed `ForeignKeyCycle` fallback misordering and NULL-vs-empty contract issues identified during verification (Tarjan SCC + condensation logic, `CsvWriter` round-trip test).

### Session 3: CLI subcommand + end-to-end emit + docs + closeout
- Wired the `generate-migration` CLI subcommand (`GenerateMigrationCommand`), registered in `Program.cs` dispatch, with bare-file exclusion and usage/exit-code docs.
- Ensured COM-free operation, reading `ir/`, `classification`, and sanitize CSVs without opening `.accdb` files.
- Defined options: `--in`, `--out`, `--classification`, `--data`, `--json-status`. Output goes to `<out>/<database-slug>/` (schema.sql, data.sql, migration-manifest.json).
- Established exit codes: `0` clean, `1` usage, `2` unexpected, `3` completed-with-degradations.
- Performed end-to-end emit against committed `HomeHealthCare` golden IR and sanitize CSV slice, producing golden schema.sql, data.sql, and sample data under `samples/GoldenOutput/HomeHealthCare/migration/`.
- Added `MigrationGoldenScriptTests` (hermetic, cross-platform byte-golden schema/data, LF-normalization, dialect well-formedness) and `GenerateMigrationCommandTests` (COM-free black-box testing of files, manifest, status, usage errors, unknown-option rejection).
- Updated documentation: `interpretation-guide.md` (new 5.5 subsection, output-map note), `install.md` (subcommand list, exit codes), `ROADMAP.md`, and `data-classification-plan.md`.
- Fixed critical issues identified during verification: unknown `--options` silently accepted (now rejected), broad `InvalidDataException` catch misattributing failures (now scoped), and mis-scoped read operations (now tightly within main try-catch).

## Outcome
- The `generate-migration` CLI subcommand is fully shipped and integrated.
- The classify -> sanitize -> migrate pipeline is end-to-end useful, producing SQL Server schema and data migration scripts.
- Documentation has been updated to describe the new artifact, its output structure, and degradation semantics.
- The harvester development arc is complete, fulfilling its long-term objective, and the repository is now consumer-driven-only.
