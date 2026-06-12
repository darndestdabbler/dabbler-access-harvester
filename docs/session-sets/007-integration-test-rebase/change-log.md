# Change Log - 007-integration-test-rebase

## Summary
The `007-integration-test-rebase` set restored `AccessHarvester.Integration` to a green state on a clean checkout. This involved re-basing tests that depended on uncommitted `NorthwindDevEdition` examples onto committed `HomeHealthCare` derived fixtures. The residual COM timeout in `SanitizeCommandTests` was investigated and dispositioned. This set focused on compliance and maintenance, with no product code changes.

## Session 1 (2026-06-12, Claude Code claude-fable-5, VERIFIED on round 2)
- Inventoried 8 tests across 5 files dependent on `northwind-output.json`. Reconciled this with the spec's expected failure count of 7 (5 unconditional, 2 publish-artifact-gated; 1 usage-error test passed due to an early mutual-exclusion check).
- Created `examples/homehealthcare-output.json` by hand-trimming from the committed HomeHealthCare golden manifest. The fixture used SchemaVersion "1.1" but was deliberately v1.0-shaped (no Documentation/FileMap) to preserve a backward-compatibility smoke role. Provenance and refresh rules were documented in `examples/README.md`.
- Re-based the 5 identified test files; no assertion was weakened. A `git grep northwind-output -- tests/` confirmed zero references.
- Hermetic run resulted in 23/23 tests passing per TRX. Round 1 verification identified 1 Major issue (HarvesterVersion derived from `Directory.Build.props` instead of the golden's literal value) and 1 Minor issue (stale rationale), both of which were fixed. Round 2 verification passed.

## Session 2 (2026-06-12, Claude Code claude-fable-5, VERIFIED on round 2)
- A full Integration suite run on the Access host (sequential, foreground, TRX-as-truth) yielded 60 total / 58 passed / 1 failed. The single failure was the predicted sanitize timeout; all 8 re-based tests passed.
- Flake disposition for the sanitize timeout overturned the initial "intermittent contention flake" premise. The failure was persistent under a `dotnet-test testhost` parent (batch and targeted re-runs failed at 120s budget) but absent outside it (direct CLI probes 0.54-0.55s exit 0, including a `CliInvoker` replica; a `classify` testhost-spawned ACE run passed in the same batch; no `msaccess.exe` was involved as sanitize is pure ACE OLE DB). A 2026-06-01 "passed targeted in 0.5s" measurement was reinterpreted as a direct-CLI run. Disposition followed the spec's persistent branch: quarantine via `[SkippableFact(Skip = ...)]` with evidence recorded in `s2-flake-disposition.md`. A scoped `sanitize-testhost-wedge` investigation set was proposed for closeout. The other two `SanitizeCommandTests` remained active.
- A post-quarantine full-suite TRX (Round 1 fix) reported total=60 executed=58 passed=58 failed=0 (2 non-executed: by-design corpus-regenerator skip + the quarantined test).
- Authored set `008-manifest-vba-ingestion-and-parser-hardening` (3 sessions, P1a of 006 review: F-3-1 Critical kb-tier VBA ingestion, F-3-2/F-3-3 grammar/AstBuilder hardening, F-3-6 diagnostics surfacing, F-3-9/F-3-10 ride-alongs), including `spec.md` and a not-started v4 state stub. Hard constraints were carried verbatim.
- Spurious `samples/*.accdb` COM-touch diffs were swept before staging in both sessions.
- Verification Round 1 (gpt-5-4) found 1 Minor issue (final tree not demonstrated by a full-suite TRX), which was fixed by the post-quarantine run. Round 2 verification passed.

## Verification
The set was verified across two sessions. Session 1 was verified on Round 2 after addressing a Major fixture issue and a Minor rationale issue. Session 2 was verified on Round 2 after addressing a Minor issue regarding the final TRX state. All deliverables were confirmed.

## Deliverables
- Integration suite is green with the `SanitizeCommandTests` flake explicitly recorded and dispositioned.
- A committed `HomeHealthCare`-derived manifest fixture, `examples/homehealthcare-output.json`, with documented provenance, is present.
- Zero references to `northwind-output.json` exist under `tests/`.
- Set `008-manifest-vba-ingestion-and-parser-hardening` is authored and ready to trigger.
- Standard set artifacts are present.

## Known follow-ups
- Trigger set `008-manifest-vba-ingestion-and-parser-hardening` (maintainer decision).
- A `sanitize-testhost-wedge` investigation set is proposed (low urgency, as the quarantine records the issue and blocks no other work).
- P1b `output-encoding-correctness` can run in parallel to set 008 per the 006 ranking.