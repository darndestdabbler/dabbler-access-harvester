# Change Log -- Access COM Graceful Shutdown & Zombie-Process Failsafe

**Status:** COMPLETE (2026-06-01)
**Sessions:** 2 of 2

## Summary
This session set addresses the issue of orphaned `msaccess.exe` processes and associated `testhost.exe` hangs after COM-driven test/extraction runs. The core `AccessComSession` was hardened to ensure graceful COM object release and to capture the Access instance's Process ID (PID) for a targeted force-kill failsafe. This prevents ungraceful shutdowns from leaving behind zombie processes. Verification confirmed zero orphaned processes after full integration tier runs and led to simplifications in test teardown logic.

## Per-session work
### Session 1: Harden AccessComSession with PID capture + force-kill failsafe (hermetic)
*   Captured Access PID on construction via `Application.hWndAccessApp` and `GetWindowThreadProcessId` (P/Invoke), with a zero-PID guard (`PidCaptureWarning`) to degrade gracefully.
*   Strengthened teardown: `Quit(acQuitSaveNone=2)` in `try`, `Marshal.FinalReleaseComObject` in `finally`, doubled `GC.Collect()` and `WaitForPendingFinalizers()`, then bounded `WaitForExit` before an exact-PID force-kill.
*   Extracted kill-decision logic behind `IProcessController` seam, with `SystemProcessController` (using `System.Diagnostics.Process`) and hermetic `ComProcessReaper`. `Dispose()` and constructor-failure path share an idempotent `TeardownCom()`.
*   Added 9 hermetic unit tests (`ComProcessReaperTests`) covering decision cases and `ArgumentException` paths.

### Session 2: Verify against real Access, simplify test teardown, document
*   Confirmed Microsoft Access installation precondition.
*   Ran full Access-COM/PDF integration tier (`AccessHarvester.Integration`, 56/56 passed) on Windows+Access host. Verified zero `msaccess.exe` and `testhost.exe` orphans before and after all runs.
*   Simplified `CliInvoker` teardown: removed blunt external `taskkill /PID <pid> /T /F`; now uses managed PID-scoped `Process.Kill` as last resort. Harness for report-baseline now gives CLI 15s to exit gracefully.
*   Fixed a pre-existing parallel-COM file-lock race by adding `CanonicalGoldenRegressionTests`, `EndToEndPipelineTests`, and `ManifestSchemaConformanceTests` to `[Collection("HomeHealthCareCom")]`, making the tier deterministic.
*   Wired `docs/access-com-zombie-cleanup.md` into durable guidance: new Testing Convention in `docs/planning/project-guidance.md` and test-lane paragraphs in `CLAUDE.md`, `AGENTS.md`, and `GEMINI.md`.

## Files created or modified
*   AccessHarvester.Core/Com/AccessComSession.cs
*   AccessHarvester.Core/Com/IProcessController.cs
*   AccessHarvester.Core/Com/ComProcessReaper.cs
*   AccessHarvester.Core/Com/SystemProcessController.cs
*   tests/AccessHarvester.Tests/Com/ComProcessReaperTests.cs
*   tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs
*   tests/AccessHarvester.Integration/Cli/HomeHealthCareComCollection.cs
*   tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs
*   tests/AccessHarvester.Integration/Pipeline/EndToEndPipelineTests.cs
*   tests/AccessHarvester.Integration/Pipeline/ManifestSchemaConformanceTests.cs
*   docs/planning/project-guidance.md
*   CLAUDE.md
*   AGENTS.md
*   GEMINI.md
*   docs/session-sets/ROADMAP.md
*   ai-assignment.md
*   next-session-set-recommendation.md
*   disposition.json
*   session-state.json
*   activity-log.json
*   session-events.jsonl
*   session-reviews/session-002.md

## Verification
*   Solution build: 0 errors (933 pre-existing CA1416 warnings).
*   Hermetic `ComProcessReaperTests`: 9/9 passed.
*   Full `AccessHarvester.Integration` tier: 56/56 passed.
*   Process census confirmed zero `msaccess.exe` and `testhost.exe` orphans before and after all integration test runs.
*   Spurious `samples/*.accdb` timestamp diffs reverted before staging.

## Notes
*   Lock-file (`.laccdb/.ldb`) cleanup decision DEFERRED: force-kill is a rare failsafe, samples open read-only, Access auto-recovers stale `.laccdb`, and no disposable per-run DBs require recreation.
*   The orchestrator used was Claude Code (Opus 4.8) by human choice, despite a routed recommendation for Gemini Pro.
*   Step 9 reorganization review: (to be appended at closeout).
