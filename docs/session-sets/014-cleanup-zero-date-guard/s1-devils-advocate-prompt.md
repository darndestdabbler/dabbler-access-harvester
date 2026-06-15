# Set 014 Session 1 — Devil's-Advocate Review Prompt (for the in-IDE pair)

> Run this **as the primary review of the fresh code**, in GitHub Copilot →
> **Gemini Pro** AND **GPT 5.4**, separately. This is additive to (not a
> replacement for) the routed `session-verification` already in `s1-verification*.md`
> (gpt-5-4, 3 rounds). The routed verifier and this adversarial pair have
> non-overlapping blind spots.

## Bias-cautions preamble

You are a skeptical reviewer. Your **null hypothesis is that the change is wrong.**
Do not produce a generic "be contrarian" review — for each concern below, steelman
the **specific** contrarian hypothesis stated, then either confirm it with a
concrete reproducer/argument or knock it down with evidence. A finding you cannot
ground in a concrete input/scenario is not a finding. Severity-rate each
(Critical/Major/Minor) and prefer a false-positive over a false-negative.

## Context

`identify-cleanup` reports per-object staleness from a per-column date MAXIMUM
sampled over an Access DB. Set 014 fixed three defects:
- **Headline misfire:** a *time-only* `Date/Time` column's `MAX()` presents as the
  OLE zero-date `1899-12-30 HH:MM:SS` → a spurious ~46176-day-stale finding.
- **O-1-5:** the COM sampler mislabelled the Access local wall-clock value as UTC,
  and swallowed per-column read failures silently.
- **O-2-6:** the analyser had no zero-date floor.

Changed files: `CleanupAnalyser.cs`, `CleanupDegradationNotes.cs` (new),
`DateMaximaSampler.cs`, `ComDateMaximaProbe.cs`, `AccessDateMaximaSampler.cs`,
`IdentifyCleanupCommand.cs`, three test files, `deferred-work.md` (DW-16).

## Specific contrarian hypotheses to steelman

### DA-1 — "The `== 1899-12-30` guard still suppresses a legitimate finding the team didn't anticipate."
The guard suppresses any sampled maximum whose **date component equals** the epoch
base. The team accepted two false-negatives (real exact-epoch max; pre-epoch real
date masked by a sibling time-only column's cross-column MAX → DW-16). **Hypothesis
to prove or refute:** there exists a *third*, more realistic case where a real,
recent, meaningful date is suppressed — e.g. a time zone / DST edge, a
`DateTimeOffset.UtcDateTime` rounding interaction, or a table whose only meaningful
column legitimately stores values on 1899-12-30 by business design (a "time of day"
domain the consumer DOES want flagged). Try to construct one.

### DA-2 — "The cross-column-MAX completeness reasoning is wrong."
The team argues the guard never hides real signal *except* the DW-16 cases because
the sampler takes `MAX` across all of a table's DateTime columns, so any real date
in a sibling column dominates the time-only column's 1899-12-30. **Hypothesis:**
find a column-combination or `MAX` semantics detail (NULL handling, mixed
date/time columns, a column whose values straddle the epoch) that breaks this.

### DA-3 — "The timezone change is NOT behavior-preserving."
`SpecifyKind(Utc)` → `SpecifyKind(Unspecified)`, still `new DateTimeOffset(naive,
TimeSpan.Zero)`. **Hypothesis:** find any comparison, serialization
(`object-cleanup.json` `MaxValue`/`generatedAt`), or downstream consumer that
observes the `Kind`/offset difference and changes output. Check the golden's
date-bearing fields and any `.ToString`/serializer that emits an offset or `Z`.

### DA-4 — "The degradation channel drops or double-counts notes."
`ISamplerDegradationSource` → `ComProbeResult.SamplerDegradations` →
`CleanupDegradationNotes.Fold`. **Hypothesis:** find an ordering/dedupe/instance-
sharing bug (e.g. `_degradations` not cleared between `Sample` calls, the COM note
and a sampler note duplicating the same failure, the `unavailable`-source branch
dropping a real sampler note). Note `_degradations.Clear()` is called at the top of
`Sample` — is that correct for the probe's single-call usage and any re-sample path?

### DA-5 — "No-regen is false."
The claim: the committed `--no-com` HHC golden is unaffected (guard only suppresses;
0 findings stays 0). **Hypothesis:** find a path where the `--no-com`/dao-metadata
output changes (e.g. a dao `LastUpdated` that lands on 1899-12-30, a degradation
note added on the no-com path). Re-run `identify-cleanup --no-com --harvest-date
2026-06-05 --threshold-days 90` against a copy of the golden `ir/` and diff.

## Output

For each DAn: verdict (`upheld` / `overturned` / `partial`), severity if upheld, a
concrete reproducer or the evidence that refutes it, and a recommended fix. Then an
overall verdict.
