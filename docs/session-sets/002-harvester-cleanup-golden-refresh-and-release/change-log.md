# Change Log — 002-harvester-cleanup-golden-refresh-and-release

Status: COMPLETE (2026-06-05)
Sessions: 2 of 2
Maintainer: claude-code (Claude Opus 4.8 @ effort=medium)

## Summary

This work set makes the harvester's set-001 improvements consumer-visible. It refreshes stale committed cleanup "golden" artifacts, prunes dead cross-reference detection code, marks the upstream feedback document as resolved, and cuts the v1.2.0 release. No changes were made to the core cleanup or usage classification logic, which was shipped and verified in set 001.

## Per-session work

### Session 1 of 2: Stale-Artifact Refresh

Confirmed that `identify-cleanup --no-com` now yields zero findings against the HomeHealthCare and NorthwindDevEdition fixtures, matching the narrowed classifier's specification. A COM-enabled run was also exercised and surfaced a known `1899-12-30` OLE zero-date misfire, which was deferred to the maintainer per the stop rule.

The committed `object-cleanup.json` and `object-cleanup-checklist.json` goldens for both fixtures were regenerated via the deterministic `--no-com` path. The new files correctly reflect zero findings, free of the `CrossReferenceAbsence`, launcher, and parameter false positives addressed in set 001.

Removed the dead `CrossReferenceDetector.cs` and its associated tests, as it had zero production callers. The `CrossReferenceAbsenceSignal` type, its schema enum, and the checklist-writer arm were retained to avoid a breaking contract change. The negative-assertion test `CleanupAnalyserTests.Run_Does_Not_Emit_CrossReferenceOnly_Findings` was also kept to pin this behavior.

Updated `docs/planning/harvester-upstream-feedback.md` with a resolution table confirming all five feedback items have been addressed.

*Cross-provider verification (gpt-5-4): R1 VERIFIED.*

### Session 2 of 2: Release v1.2.0

Bumped the version in `Directory.Build.props` from `1.1.0` to `1.2.0`. The CLI now reports `AccessHarvester 1.2.0 (schema v1.1)`. The manifest schema literal itself remains unchanged at `v1.1`.

Authored release notes for v1.2.0, covering all consumer-visible changes since the v1.1.0 tag (which was an unpublished CI draft). This includes the new `generate-migration` command, the `report-baseline DisplayTotals` fix, the `classify` shutdown-hang fix, and the narrowed cleanup/usage signals from set 001.

Refreshed `install.md` with `1.2.0` version examples and corrected stale references to "schema v1.0". The documentation now correctly notes that the binary embeds both v1.0 and v1.1 schemas and validates against the manifest's declared version.

The build was green with 2234 hermetic tests passing across 9 projects. The COM integration test tier was excluded due to a known environmental testhost wedge, which was acceptable as this session made no product code changes.

The release was packaged via `publish.ps1`, then published through the standard CI pipeline by pushing the `v1.2.0` tag. The resulting GitHub Actions artifact was verified (SHA-256: `3820e6000fca44320fb4180d26137ad1f6dba213c4050f5b8daf993d4ee27b53`), smoke-checked, and published as the "Latest" release.

*Cross-provider verification (gpt-5-4): R1 ISSUES_FOUND (3 minor doc/process items) -> all fixed -> R2 VERIFIED.*

## Outcome

The harvester's committed cleanup goldens now match the narrowed classifier logic shipped in set 001. Dead cross-reference detection code has been removed. The upstream-feedback planning document now accurately reflects that all items are resolved. Finally, the `v1.2.0` GitHub release is published, providing consumers with a binary that includes all recent improvements, most notably the `generate-migration` capstone feature.