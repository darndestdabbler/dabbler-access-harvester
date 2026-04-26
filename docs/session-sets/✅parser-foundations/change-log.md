# parser-foundations Change Log

This session set established the foundation for the VBA parser migration by setting up the verification harness, reconciling parser schemas, and integrating ANTLR4 build infrastructure.

## Deliverables

### Session 1: Verification Harness Design + Skeleton
- Implemented `tools/parser-harness/harness.py` with `run`, `diff`, and `perf` subcommands.
- Defined `tools/parser-harness/harness-output.schema.json` for machine-readable harness output.

### Session 2: Schema Reconciliation Pass
- Documented 20 schema deltas in `docs/schema/v0-vs-tiers.md`.
- Applied v0-G1 fix: added `isArray` boolean to `$defs/parameter` in `harvester-schema.v0.json`.

### Session 3: ANTLR4 Build Wiring + Solution Update
- Created `AccessHarvester.Vba.Grammar.csproj` with Antlr4.Runtime 4.6.6 and MSBuild target.
- Added placeholder `AccessHarvester.Vba.Grammar/Vba.g4`.
- Initialized `tests/AccessHarvester.Vba.Grammar.Tests/` project with an xUnit placeholder test.
- Updated `AccessHarvester.sln` to include new projects and configurations.
- Added `.gitignore` entries for ANTLR-generated files.

### Session 4: Buffer
- No deferred findings required fixes.

## Deferred Items

- **ANTLR4 Code Generation:** To `#3 vba-lexical-and-expression-grammar`.
- **Grammar Rules Implementation:** To child sets `#3` and `#4`.
- **`schema-validation` Task Type:** To `vba-parser-session-set-generator` child set `#4`.

---

**Next session-set recommendation (after parser-foundations):**
`vba-lexical-and-expression-grammar` and `access-metadata-extraction` (run in parallel — both are DAG roots once `vba-spec-study` completes)

**Recommended orchestrator for `vba-lexical-and-expression-grammar`:** Claude Code, Opus 4.7 @ effort=high
**Rationale:** This is the critical-path synthesis set (2 of 4 sessions are grammar design); Opus is warranted. Session 1 of this set will have an architecture call for lexical grammar design that timed out as a routed call in prior sets — using Opus as the orchestrator handles it in-conversation and avoids the timeout pattern documented in lessons-learned.md.

**Recommended orchestrator for `access-metadata-extraction`:** Claude Code, Sonnet 4.6 @ effort=medium
**Rationale:** An independent mechanical branch (form/report/query/table metadata extraction); Sonnet at medium effort is the right default for implementation-heavy, lower-synthesis work that doesn't need top-tier reasoning.
