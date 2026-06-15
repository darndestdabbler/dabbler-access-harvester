**R1 Major — zero-date guard**

The `<=` → `== OleEpochBaseDate` change does fix the specific R1 over-suppression bug. A real pre-epoch max such as `1890-01-01` will now survive the guard, and the new `Run_Keeps_PreEpoch_RealDate_As_Stale_Finding` pin is the right regression test for that failure mode.

I do **not** think this is fully clean yet, though. There is still another legitimate-finding suppression case at the analyser boundary: a table whose true maximum date is **exactly `1899-12-30`** will now be suppressed as “no date signal,” even if that is a real calendar date and should produce a stale-date finding. With the current sampler contract (single cross-column MAX, no per-column provenance, no indication that the value came from a time-only column), the analyser cannot distinguish:

- real `1899-12-30 ...`
- zero-date `1899-12-30 00:00:00`
- time-only `1899-12-30 HH:MM:SS`

So `==` is the right fix for the original `<=` bug, and the pre-epoch pin is correct, but there remains a real exact-epoch false-negative that is not covered by the documented DW-16 residual. The new comment also overstates things slightly when it says equality “targets only” the sentinel/time-only cases.

**R1 Minor — CLI fold extraction**

From the call site shown in `IdentifyCleanupCommand.cs`, the extraction to `CleanupDegradationNotes.Fold(...)` looks directionally correct: the CLI now passes both `comDegradationNote` and `samplerDegradations`, and stderr emission remains COM-tier-only / probe-tier-only as described. Nothing in the shown call-site change contradicts the intended legacy behavior.

That said, the actual helper implementation and `CleanupDegradationNotesTests` are not present in the supplied diff, so I cannot independently audit the stated equivalence properties (`same instance`, `source==unavailable` suppression, exact ordering, etc.) from the materials here. I don’t see a concrete defect at the call site, but I also can’t fully certify the helper/test pin without those files.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Cleanup/CleanupAnalyser.cs",
      "summary": "The new equality guard still suppresses a legitimate stale-date finding when the table's real maximum date is exactly 1899-12-30, because that value is treated the same as Access zero-date/time-only output.",
      "fix": "Either extend the sampler contract to distinguish time-only/zero-date from an actual 1899-12-30 date (preferred), or explicitly document and regression-test this accepted exact-epoch false-negative instead of claiming the guard targets only sentinel values."
    }
  ]
}
```