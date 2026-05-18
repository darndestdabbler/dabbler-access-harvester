# Change Log — Harvester Finish: CLI Packaging
**Status:** COMPLETE (2026-05-18)
**Sessions:** 2 of 2

## Summary
This session set delivered the final packaging and documentation for the harvester CLI, closing Phase 4 of the dabbler-access-harvester program-of-work, specifically Criterion #7 ("Packaging"). The harvester CLI now ships as a self-contained Windows binary (36.9 MB) with dedicated `[SkippableFact]` black-box smoke tests, comprehensive installation documentation (`docs/harvester/install.md`), and a `--version` flag that clearly surfaces both the tool's semantic version and the schema version it targets. This enables downstream consumers (AI tooling agents, future migration orchestrators, human evaluators) to install and invoke the CLI without building from source.

## Per-session work

### Session 1: Publish Wiring + Reproducible Build
* Updated `AccessHarvester.Cli.csproj` with publish properties: `PublishSingleFile=true`, `PublishTrimmed=false` (documented rationale citing ANTLR runtime, DAO COM interop, and SQLite native dependencies), `IncludeNativeLibrariesForSelfExtract=true`, and `EnableCompressionInSingleFile=true`. Properties live publish-time only so plain `dotnet build` stays framework-dependent.
* Authored `tools/publish/publish.ps1` — `dotnet publish --self-contained --runtime win-x64` wrapper that lands `publish/win-x64/AccessHarvester.exe`.
* Added `/publish/` to `.gitignore`.
* Reproducible: two consecutive publishes produced byte-identical SHA256 `0CC72857AC2678E809404A57D7FFA90F09E8FC5066CBFADE8265BD50FC3624DA`. No timestamp normalization needed — .NET 10's `Deterministic=true` default plus compressed single-file is already deterministic.
* Manually validated `AccessHarvester.exe --help` standalone.
* Cross-provider session-verification: GPT-5.4 returned 429 three times; fell back to `gemini-pro` per the verifier-failure escalation ladder step 2. Round 1 returned one Minor (invalid model name in the Step-3.5 placeholder); resolved at Step 8 by re-routing the Session 2 next-orchestrator recommendation with an explicit model-pool list.

### Session 2: Black-box Smoke Tests + Install Docs + Closeout
* Authored `tests/AccessHarvester.Integration/TestSupport/PublishedArtifact.cs` (path-resolution helper) and added `RepoPaths.LocateDirectoryContaining` so the helper can walk up from the test-bin to the repo root via the `AccessHarvester.sln` sentinel.
* Authored four `[SkippableFact]` smoke tests in `tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs` — bootstrap (no-args / `--help`), `parse` against `01-simple-validation.bas`, and `generate-docs` against `examples/northwind-output.json`. Tests skip cleanly when the publish step has not been run; they invoke `Process.Start` against the packaged exe (not the test-bin copy).
* Added `--version` and `version` flag to `AccessHarvester.Cli/Program.cs`. Output: `AccessHarvester <semver> (schema v<schema-version>)`. The schema-version literal "1.0" is pinned in `Program.cs` with a comment naming `DatabaseManifest.SchemaVersion` as the source of truth. New `[Theory]` in `CliBootstrapTests` covers both flag forms.
* Authored `docs/harvester/install.md` — distribution shape, where to get the artifact, install steps, invocation surface, exit codes, versioning convention, verify-an-install smoke recipe, and the stop-rule constraints.
* Updated `README.md` CLI section with a single-line link to the install guide.
* Build/test green: 0 errors; 31 Integration tests pass (including the new bootstrap `--version` theory and 4 smoke tests).
* Cross-provider session-verification: GPT-5.4 returned 429 three times; fell back to `gemini-pro` per the escalation ladder. Round 1 VERIFIED with substantive 7-point analysis and empty issues array — the prose-before-JSON lead worked on the first try.

## Files created or modified
* `AccessHarvester.Cli/AccessHarvester.Cli.csproj`
* `AccessHarvester.Cli/Program.cs`
* `.gitignore`
* `README.md`
* `tools/publish/publish.ps1`
* `tests/AccessHarvester.Integration/TestSupport/PublishedArtifact.cs`
* `tests/AccessHarvester.Integration/TestSupport/RepoPaths.cs`
* `tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs`
* `tests/AccessHarvester.Integration/Cli/CliBootstrapTests.cs`
* `docs/harvester/install.md`
* `docs/session-sets/harvester-finish-cli-packaging/ai-assignment.md`
* `docs/session-sets/harvester-finish-cli-packaging/next-orchestrator-recommendation-session-2.md`
* `docs/session-sets/harvester-finish-cli-packaging/disposition.json`
* `docs/session-sets/harvester-finish-cli-packaging/change-log.md`
* `docs/session-sets/harvester-finish-cli-packaging/next-session-set-recommendation.md`
* `docs/session-sets/harvester-finish-cli-packaging/session-reviews/session-001.md`
* `docs/session-sets/harvester-finish-cli-packaging/session-reviews/session-002.md`

## Verification
* **Per-session cross-provider verifier verdicts:**
    * Session 1: VERIFIED after one Minor finding resolved by re-routing the Session 2 recommendation. Verifier routed to `gemini-pro` (GPT-5.4 429 fallback).
    * Session 2: VERIFIED Round 1 with empty issues. Verifier routed to `gemini-pro` (GPT-5.4 429 fallback — three-for-three on closeout-grade verification rounds across the most recent harvester sets).
* **Build/test outcomes:**
    * Session 1: 0 errors. Six pre-existing test failures on pristine HEAD (line-ending mismatch — `i/lf` committed vs. `w/crlf` checkout because the repo has no `.gitattributes`) were flagged for follow-up; commit `4a8180c` (tree-amendment) cleared them ahead of Session 2.
    * Session 2: 0 errors. 31 Integration tests pass, including 2 new `CliBootstrapTests.VersionFlag_*` theory rows and 4 new `PackagedBinarySmokeTests` (which gated to skip cleanly when the publish artifact is absent and run when present).

## Phase 4 status
**Phase 4 CLOSES with this set.** Criterion #7 (Packaging) is now green; combined with #1 (canonical parse-success report), #3 + #5 (golden output + `UnresolvedItem` records), #4 (CLI E2E tests), and #6 (operations and interpretation docs) shipped earlier in Phase 4, and #2 (schema finality) shipped before Phase 4, all seven Phase 4 criteria are closed. Per `strategy.md` and `ROADMAP.md` → "What happens after Phase 4 closes": the harvester repo moves to maintenance, and the Access-to-Blazor application migration work moves to a future migration repo not yet created.

## Cost rollup
Total routed cost across the set: **~$0.078** — below the spec's $0.10–$0.40 band.

* **Session 1:** ~$0.030 across three `gemini-pro` calls (ai-assignment analysis $0.0135, fallback session-verification $0.0104, Session 2 next-orc recommendation $0.0063).
* **Session 2:** ~$0.0484 across three routed calls — fallback session-verification on `gemini-pro` $0.0363, change-log on `gemini-flash` (`max_tier=1`) $0.0020, next-session-set recommendation on `gemini-pro` $0.0101.

The recurring GPT-5.4 429 pattern (three-for-three across this set + `harvester-finish-cli-e2e-test-audit`) is worth surfacing for any Step 9 reorganization review — the configured `session-verification` target may want a swap to `gemini-pro` for Claude orchestrators until OpenAI capacity stabilizes.
