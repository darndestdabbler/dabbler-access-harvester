# Change Log — Harvester Finish: CLI End-to-End Test Audit

**Status:** COMPLETE (2026-05-18)
**Sessions:** 2 of 2

## Summary
This session set closed the audit of all `AccessHarvester` CLI subcommands (extract, translate, run, parse, generate-docs), ensuring end-to-end test coverage for each. It also verified deterministic output where applicable and confirmed comprehensive documentation of usage and exit codes. This effort directly satisfied Phase 4 Criterion #4, marking a significant milestone in the project's completion.

## Per-session work

### Session 1: Black-box Tests for `extract` / `translate` / `run`
- Authored three `[SkippableFact]`-gated CLI test classes: `tests/AccessHarvester.Integration/Cli/CliExtractTests.cs`, `tests/AccessHarvester.Integration/Cli/CliTranslateTests.cs`, and `tests/AccessHarvester.Integration/Cli/CliRunTests.cs`.
- Each invokes the AccessHarvester binary out-of-process via `CliInvoker.Run(...)` and asserts on exit code, the `--json-status` envelope shape, and tier-directory shape.
- `CliTranslateTests` uses an `ExtractFixture` (xUnit IClassFixture) to amortize one Access extraction across its tests.
- Integration assembly: 20 passed / 1 skipped (Northwind gitignored).
- Cross-provider verification: gemini-pro fallback after gpt-5-4 429 — VERIFIED.

### Session 2: Determinism Gate + Help-Text Audit
- Authored two new test classes: `tests/AccessHarvester.Integration/Cli/CliDeterminismTests.cs` — run-twice-diff for `parse` and `generate-docs`; doc-comment delegates `run` determinism to `CanonicalGoldenRegressionTests` (single-run-vs-frozen-reference is strictly stronger and also subsumes `extract` + `translate`). And `tests/AccessHarvester.Integration/Cli/CliUsageTextTests.cs` — 4 tests covering: subcommand list, exit-code legend per group, option list, multi-arg `UnknownCommand` path.
- Patched `AccessHarvester.Cli/Program.cs` `PrintUsage()` to add the missing exit-code legend block for `generate-docs` (0=success, 1=usage error, 2=unexpected error). Drift found by the audit; `CliUsageTextTests.PrintUsage_DocumentsExitCodesForEachSubcommandGroup` asserts on the new block.
- Integration assembly: 26 passed / 1 skipped (Northwind gitignored). New tests this session: 6 added, 6 passed.
- Cross-provider verification: gemini-pro fallback after gpt-5-4 429 (two-for-two on this set). Round 1 bare-verdict; round 2 with explicit analysis-required reminder produced a substantive 5-point analysis — VERIFIED, empty issues.

## Files created or modified
- `tests/AccessHarvester.Integration/Cli/CliExtractTests.cs`
- `tests/AccessHarvester.Integration/Cli/CliTranslateTests.cs`
- `tests/AccessHarvester.Integration/Cli/CliRunTests.cs`
- `tests/AccessHarvester.Integration/Cli/CliDeterminismTests.cs`
- `tests/AccessHarvester.Integration/Cli/CliUsageTextTests.cs`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/ai-assignment.md`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/change-log.md`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/disposition.json`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/next-session-set-recommendation.md`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/session-reviews/session-001.md`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/session-reviews/session-002.md`
- `AccessHarvester.Cli/Program.cs`

## Verification
- Cross-provider verifier per session
- Build/test outcomes

## Phase 4 status
Criterion #4 closed by this set. Remaining open: F4 (`harvester-finish-operations-and-interpretation-docs`, in flight in a parallel worktree) for Criterion #6, and F5 (`harvester-finish-cli-packaging`) for Criterion #7 — the final Phase 4 gate.