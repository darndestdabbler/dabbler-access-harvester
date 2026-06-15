**Decision A — analyser guard as shipped fix; sampler-side time-only skip deferred**

The analyser-side guard does fix the stated headline false positive: a sampled max of `1899-12-30 HH:MM:SS` no longer turns into a bogus ~46k-day stale finding.

However, the claim that this is **“provably complete”** is not correct.

There is a clear legitimate-signal suppression case: Access `Date/Time` can represent real dates earlier than `1899-12-30`. With the current guard

```csharp
if (DateOnly.FromDateTime(chosen.MaxValue.UtcDateTime) <= OleEpochBaseDate)
    continue;
```

a table whose true latest real date is, for example, `1890-01-01` will now be treated as having **no date signal at all**. That is a false negative.

There is also a stronger counterexample to the “MAX across sibling columns makes this safe” rationale. If a table has:

- one real historical `Date/Time` column whose values are all before `1899-12-30`, and
- one time-only `Date/Time` column,

then the sampler’s **table-wide max across columns** will prefer the time-only value on `1899-12-30` over the real historical date, because `1899-12-30` is later than `1890-01-01`. The analyser then suppresses that result entirely. So a legitimate stale signal can be lost **even when real date data exists in a sibling column**.

So the shipped guard is a **heuristic suppression** for the 1899-12-30 time-only case, not a complete proof-safe fix. The critical question you asked — “is there any case where the guard suppresses a legitimate finding?” — is **yes**.

---

**Decision B — date-component comparison vs instant comparison**

The **date-component interpretation is correct**.

If the spec text `MaxValue <= 1899-12-30` were implemented as an instant comparison against midnight `1899-12-30T00:00:00`, it would indeed **miss** the headline time-only case such as `1899-12-30 14:30:00`. So reading the spec as “compare the date part” is the right interpretation.

Under the current implementation invariant that sampled values are carried as `DateTimeOffset` with offset `00:00`, using:

```csharp
DateOnly.FromDateTime(chosen.MaxValue.UtcDateTime)
```

does not introduce an off-by-one for the headline case.

But the boundary is too broad. To catch the time-only Access zero-date case, the needed condition is effectively:

- **date component == `1899-12-30`**

not

- **date component <= `1899-12-30`**

Using `<=` is what hides legitimate pre-epoch dates. So Decision B is only partially sound: the **date-component reading** is right, the **chosen boundary** is not.

---

**Decision C — `SpecifyKind(Utc)` to `SpecifyKind(Unspecified)`, then `new DateTimeOffset(..., TimeSpan.Zero)`**

This change is sound and behavior-preserving for the arithmetic in this pipeline.

- `DateTime.SpecifyKind` changes only the `Kind` flag; it does **not** convert the clock value.
- `new DateTimeOffset(naive, TimeSpan.Zero)` is valid for a `DateTime` whose kind is `Unspecified`; it does **not** throw.
- Because the clock value and zero offset are unchanged, the resulting `DateTimeOffset` compares the same way against the harvest-date anchor used by the analyser.

So this removes the false claim that the sampled value is UTC, while leaving the day-difference computation unchanged. Decision C verifies cleanly.

---

**Decision D — swallowed per-column failures surfaced via degradation channel; CLI folding equivalence**

The plumbing is coherent:

- `AccessDateMaximaSampler` now records per-column failures into `_degradations`.
- `ISamplerDegradationSource` exposes those notes.
- `ComDateMaximaProbe` reads them and threads them into `ComProbeResult.SamplerDegradations`.
- `IdentifyCleanupCommand` writes them to stderr and folds them into `CleanupFindings.DegradationNotes`.

On the specific equivalence question: when there are **no per-column failures** (`samplerDegradations.Count == 0`), the refactor is behavior-equivalent to the old logic.

Previously, augmentation happened only when:

```csharp
comDegradationNote is not null && findings.DateSignalSource != "unavailable"
```

Now that exact condition is preserved as `hasComNote`, and if there are no sampler degradations, the augmented list is built only under that same condition. So the old COM-failure-note behavior is preserved.

What is **not** shown here is a hermetic test of the final command-level fold into JSON/stderr. The probe-layer tests prove the channel up to `ComProbeResult`, but not the last hop to the user-visible output. The code reads correctly, but that output behavior is not pinned in this diff.

---

**No-regen claim**

This claim is sound for the committed golden described.

Reasons:

1. The committed HHC golden is `--no-com`, so the COM-only changes (`SpecifyKind` and per-column degradation surfacing) do not execute there.
2. The analyser guard is suppressive only; it can remove findings, not create new ones.
3. The cited golden already has `findings: []`, so “0 stays 0” is valid for this fixture.
4. The reported empirical rerun reproducing `dateSignalSource=dao-metadata, degradationNotes=[], findings=[]` modulo `generatedAt` is sufficient confirmation.

So: **no regen needed for this specific golden** is a sound conclusion. I would only qualify that this is not a universal statement for all possible DAO-path fixtures; it is sound for this one.

---

**Disclosed coverage gap**

The disclosed COM-only gap is only partly acceptable.

It is reasonable that:

- the live COM exception behavior in `TrySampleMax`, and
- the COM interop `SpecifyKind` shape change

are exercised only in the Access integration tier. Those are genuinely COM-bound behaviors.

But there is a **specific missing hermetic pin** around the analyser boundary that matters most: legitimate pre-epoch dates. The current new analyser tests cover:

- `1899-12-30 HH:MM:SS` suppressed,
- exact `1899-12-30` suppressed,
- a 1990 date retained,

but they do **not** cover a real maximum earlier than `1899-12-30`. That is exactly where the current implementation is wrong.

There is also a smaller missing hermetic pin for the final CLI/output fold: the new degradation channel is tested at probe level, but not end-to-end into `object-cleanup.json` + stderr in the command layer.

So I do **not** consider coverage fully acceptable as-is, because the boundary case that invalidates the completeness claim is untested.

```json
{ "verdict": "ISSUES_FOUND", "issues": [ { "severity": "Major", "location": "AccessHarvester.Pipeline/Cleanup/CleanupAnalyser.cs", "summary": "The zero-date guard suppresses every sampled maximum on or before 1899-12-30, which hides legitimate historical date signals and makes the claimed 'provably complete' fix incorrect.", "fix": "Narrow the guard to the exact zero-date day needed for the headline case (`date == 1899-12-30`), add a regression test for a real pre-epoch maximum, and explicitly document that full correctness cannot be guaranteed without richer sampler output/per-column signal." }, { "severity": "Minor", "location": "AccessHarvester.Cli/Commands/IdentifyCleanupCommand.cs", "summary": "The new sampler-degradation channel is not hermetically pinned through the final CLI/output layer.", "fix": "Add a command-level test that injects sampler degradation notes and verifies both stderr emission and `DegradationNotes` folding, while also proving legacy COM-fallback-note behavior is unchanged when there are no sampler degradations." } ] }
```