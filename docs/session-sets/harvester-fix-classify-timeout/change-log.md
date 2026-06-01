# Change Log -- Fix Classify Timeout

## Summary

A 2-session reliability spike that resolved the persistent ~240s timeout in
`AccessHarvester.Integration.Cli.ClassifyCommandTests.HomeHealthCare_classify_emits_schema_conformant_json`,
which had left the COM integration tier carrying a standing red that masked
real regressions.

The spike's defining outcome is that **the spec's premise was overturned by
evidence**. The timeout was *not* product-compute latency and *not* the
`SELECT DISTINCT` cardinality cost the spec named as prime suspect. The
`classify` work completes in **~7s** (schema read 6.4s — almost entirely the
DAO/Access-COM augmentation — and value sampling 0.16s; the suspected DISTINCT
cardinality is 0.06s total across all 93 columns). The real failure was that
the **`classify` CLI process never terminated**: after writing all output it
wedged in native process shutdown (consistent with COM apartment
un-initialization following the Access COM interop; the exact native frame was
not captured) with no surviving `msaccess.exe`, so the test's
`proc.WaitForExit(240000)` blew through.

Session 2 shipped the human-locked **Option 2, scoped** remediation: a
flush-then-`TerminateProcess` shutdown guard that fires only when an Access COM
apartment was actually activated — fixing both the red test and a real
consumer-facing defect (a downstream user's `classify` previously hung
forever). A collateral DAO-HRESULT fix (E5) was folded in. The fallback
(Skip.If quarantine) was not needed.

## Per-session work

### Session 1: Reproduce, Instrument, Root-Cause (diagnosis only)

- Reproduced the timeout in true isolation (242s `WaitForExit` failure) and a
  faithful in-process timing harness attributing wall-clock across phases.
- Established the evidence chain (E1–E4) that overturned the value-sampling
  premise and localized the hang to the managed→native shutdown path:
  `Environment.Exit` still wedged; a hard `TerminateProcess` after flushing
  exited cleanly in 7s with the correct exit code and no zombie.
- Surfaced the E5 collateral finding (93 spurious `Property not found`
  degradations forcing exit code 3 on the rebuilt fixture).
- Routed a cross-provider consensus on fix-vs-quarantine (gpt-5-4 → quarantine;
  gemini-pro → hard-terminate; both confirmed the root cause sound); the split
  was escalated to the human, who locked **Option 2 scoped** with quarantine as
  fallback and folded E5 into Session 2.
- Cross-provider session verification ran 3 rounds (gpt-5-4); substance VERIFIED
  throughout, residual was wording (proven-vs-inferred separation), fixed.
- No production code changed in Session 1; deliverables were diagnostic docs +
  spec corrections. See [`root-cause-findings.md`](root-cause-findings.md).

### Session 2: Remediate + Verify + Closeout

- **Scoped CLI hard-exit guard.** `AccessComSession` now latches a process-static
  `ActivatedInThisProcess` flag the moment an Access COM server is activated.
  `AccessHarvester.Cli/Program.Main` routes all exits through a new
  `FinalizeExit`, which flushes stdout/stderr and calls
  `TerminateProcess(GetCurrentProcess(), exitCode)` **only** when that flag is
  set. This realizes the "scoped to the COM-using command path(s), not every
  subcommand" requirement via *actual COM activation* rather than a
  subcommand-name allowlist — a tighter blast radius (parse/validate/translate/
  generate-docs and all usage-error early returns take the normal managed
  return path) that also auto-covers any future COM-using subcommand.
- **E5 DAO-HRESULT fold-in.** Added the "Property not found" HRESULT
  `0x800A0CC6` (= DAO error 3270), captured **empirically** (all 93 degradations
  carried it — not inferred), to `OleDbDaoSchemaReader.DaoPropertyAbsentHResults`,
  and routed both `catch … when` filters through a new internal
  `IsDaoPropertyAbsentHResult` predicate exposed via `InternalsVisibleTo` for a
  hermetic test.
- **Validation.** Full solution build clean; 15 hermetic tests pass (6 new
  HRESULT cases + 9 reaper); direct `classify` against HomeHealthCare exits in
  ~9s with exit code 0 (degradations 93 → 0), 13 tables classified, zero
  orphaned `AccessHarvester.exe`/`msaccess.exe`; full `AccessHarvester.Integration`
  COM tier TRX-verified at **56 passed / 1 skipped / 0 failed**, 0 zombies.
  The 240s-timeout target test now passes in **~7s**.
- Cross-provider session verification (gpt-5-4) returned **VERIFIED round 1,
  zero issues** across all five concerns; its sole non-issue maintainability
  caveat was documented in the flag's XML comment.

## Files created or modified

**Production code (Session 2):**
- `AccessHarvester.Cli/Program.cs` — `FinalizeExit` scoped hard-exit guard.
- `AccessHarvester.Core/Com/AccessComSession.cs` — `ActivatedInThisProcess` latch.
- `AccessHarvester.Core/Com/OleDbDaoSchemaReader.cs` — E5 HRESULT + predicate.
- `AccessHarvester.Core/AccessHarvester.Core.csproj` — `InternalsVisibleTo`.
- `tests/AccessHarvester.Tests/Com/DaoPropertyAbsentHResultTests.cs` — new hermetic test.

**Docs:**
- `docs/planning/lessons-learned.md` — RESOLVED note on the classify/sanitize entry.
- `docs/session-sets/ROADMAP.md` — set row flipped to `[x]` with outcome.

**Session-set artifacts:** `root-cause-findings.md`, `session-1-timing-evidence.md`,
`ai-assignment.md`, `decision-consensus-*.md`, `spec.md` (corrected), the
`session-reviews/` and `issue-logs/` for both sessions, `disposition.json`.

## Verification

- **Session 1:** gpt-5-4 session-verification, 3 rounds, VERIFIED on substance
  (wording residuals fixed).
- **Session 2:** gpt-5-4 session-verification, VERIFIED round 1, zero issues.
- Routed cost: Session 1 ~$0.069 (consensus + routing plan), Session 2 ~$0.111
  (session-verification). Set total ~$0.18.

## Notes

- This is the **first of the two final harvester sets**. The
  `harvester-sql-migration-generator` capstone follows; after it closes, the
  repo is consumer-driven-only (no self-initiated work).
- Lesson for future COM-tier work: a test "timeout" is not automatically slow
  product compute — attribute the wall-clock to phases AND confirm the process
  actually *exits*. A never-terminating CLI presents identically to a slow one
  through `WaitForExit`.
