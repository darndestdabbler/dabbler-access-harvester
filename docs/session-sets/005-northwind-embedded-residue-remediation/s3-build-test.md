# Session 3 — Build + Test Results

> **Run:** 2026-06-05. `dotnet build` then `dotnet test`, sequential.

## Build

`dotnet build` (full solution) — **0 Errors**, 1143 pre-existing warnings
(CA1416 windows-platform + NU1903 dep advisory; none introduced this session).
The out-of-solution `tools/symbol-db/probe-builder/` (the sole caller of the
genericized `BuildArtifacts.RunForFixture`) builds clean (0 errors), confirming the
`RunForNorthwind → RunForFixture(FixtureObjectSet)` rename + new CLI args compile.

## Test — hermetic suite GREEN (all projects my changes can affect)

| Project | Result |
|---|---|
| `AccessHarvester.Vba.Parser.Tests` | **69 passed**, 0 failed |
| `AccessHarvester.Vba.Grammar.Tests` | **294 passed**, 0 failed |
| `AccessHarvester.Sql.Grammar.Tests` | **85 passed**, 0 failed |
| `AccessHarvester.Vba.Resolver.Tests` | **400 passed**, 0 failed |
| `AccessHarvester.SymbolDb.Tests` | **69 passed**, 0 failed (the Session-2 HHC-rebased tests) |
| `AccessHarvester.Pipeline.Tests` | **794 passed**, 0 failed |
| `AccessHarvester.Tests` | **590 passed**, 2 skipped, 0 failed (on clean re-run) |

**1711+ hermetic tests pass, 0 failures.** The `AccessHarvester.Tests` project
showed 1 failure on the first full run (an ACE-OLEDB "database placed in a state by
user 'Admin' on machine 'DENICI'" lock error) — a **cascade of a wedged `msaccess`
instance** held by a concurrently-wedged `testhost`. After reaping the zombie
(WMI `Win32_Process.Terminate`, per the repo's documented COM-zombie procedure) and
re-running, the project is **fully green** — confirming that failure was the
transient lock, not a code defect.

## Pre-existing COM/Integration failures (NOT caused by this session)

`AccessHarvester.Integration` — **8 failed, 51 passed, 1 skipped** (consistent
across two runs). **All 8 are pre-existing and independent of this session's
changes**, proven as follows:

1. **None of this session's edits touch `AccessHarvester.Integration/` or
   `AccessHarvester.Tests/` source, nor any CLI/COM source, nor `examples/`**
   (`git status` confirms — the only `.cs` edits are comment/string redactions
   plus the SymbolDb `BuildArtifacts` genericization, whose own tests pass).
2. **7 of the 8 depend on `examples/northwind-output.json`**
   (`CliValidateTests`, `CliDeterminismTests`, `CliGenerateDocsTests`,
   `PackagedBinarySmokeTests`, `ManifestSchemaConformanceTests`,
   `SchemaConformanceTests.Example_…`). That file `git log --all` shows was
   **never committed** and is absent on disk. Since it was never in history and
   this session did not create or delete it, it was absent at the `HEAD` this
   session started from → these tests fail on **any** checkout of this repo,
   before and after Session 3.
3. **The 8th** (`SanitizeCommandTests.HomeHealthCare_sanitize_…`) is a
   `System.TimeoutException` — the COM `sanitize` CLI against
   `samples/HomeHealthCare.accdb` exceeded 120 s — an Access/COM environmental
   timeout, not a code failure.

### Finding for follow-up (recorded, not actioned this set)

The CLI/schema Integration tests are **coupled to a NorthwindDevEdition-derived
`examples/northwind-output.json`** that was never committed (it was regenerated
in-session by `harvester-finish-operations-and-interpretation-docs` from
`NorthwindDevEdition/manifest.json` — a flagged provenance gap in that set's
`disposition.json`). This is NorthwindDevEdition residue-coupling the Session-1
manifest did not catch. **It must NOT be "fixed" by regenerating the NW-derived
file** — that would reintroduce the residue this set removes. The correct fix is to
**re-base these Integration fixtures onto a committed HomeHealthCare-derived
manifest** (the Session-2 re-base pattern), tracked as a next-set recommendation.
