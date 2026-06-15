# Cleanup Zero-Date Guard Spec

> **Purpose:** Close the 006 review's **P6** lane — the final unowned ranked set
> in the solution-review arc. The highest-value item is the known
> **`identify-cleanup` zero-date misfire**: the COM date-maxima sampler misreads
> a **time-only** `DateTime` column as the OLE zero-date `1899-12-30`, which the
> cleanup analyser then turns into a ~46176-day stale **Medium/High** "stale
> object" finding — a false high-severity result that erodes trust in the
> command whose whole job is to tell a consumer what is safe to delete. Folded in
> are its two adjacent 006 findings: **O-1-5** (the sampler stamps Access local
> wall-clock dates as UTC and swallows per-column failures silently) and
> **O-2-6** (the analyser has no zero-date guard).
> **Created:** 2026-06-14 (authored by 013 Session 2 per the
> last-session-authors-next-set pattern; routed recommendation in
> `docs/session-sets/013-cli-input-robustness/next-session-set-recommendation.md`)
> **Session Set:** `docs/session-sets/014-cleanup-zero-date-guard/`
> **Prerequisite:** **P0 only** (a committed green build/test baseline) —
> independent of the 006 P1–P5 arc, slots anywhere. With this set the 006 review
> arc P0→P1a∥P1b→P2→P3→P4→P5→P6 is **complete** (sets 007–014).
> **This is 006's P6.** Source of truth: the 006 review findings
> (`006-solution-review-and-upstream-feedback-discovery/s1-review-findings.md`
> O-1-5; `s2-review-findings.md` O-2-6) and the memory note
> `project_cleanup_com_zerodate_misfire`.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 1
prerequisites: []
verificationMode: out-of-band-or-none
```

> Rationale: no UI surface; the quality gate is build + tests + cross-provider
> verification. The work is **mostly hermetic** (a pure zero-date/time-only guard
> in the analyser, plus a `DateTimeKind` correction and a degradation record in
> the sampler). The COM sampler change is exercised by the Access-COM tier; the
> analyser guard is hermetically unit-testable against a synthesised sampler
> maximum. `verificationMode: out-of-band-or-none` matches the inline-verify
> Full-tier pattern (007–013): the cross-provider `session-verification` runs
> inside the work session; register with `--verification-mode out-of-band-or-none`.

---

## Project Overview

`identify-cleanup` reports, per object, how stale it is so a downstream
consumer can decide what to remove. The staleness is computed from a
per-column **date maximum** sampled over the database. Two defects corrupt that
signal:

1. **Zero-date misfire (the headline bug).** A **time-only** `DateTime` column
   (Access stores time-of-day as a fractional day on the OLE epoch base date
   `1899-12-30`) is read by the COM sampler as a maximum of `1899-12-30`. The
   analyser then computes `days = (harvestDate − 1899-12-30) ≈ 46176`, yielding a
   spurious Medium/High "very stale" finding for a column that actually carries
   live time-of-day data. Proven in the memory record
   `project_cleanup_com_zerodate_misfire`.
2. **O-1-5 — sampler timezone + silent swallow.** `AccessDateMaximaSampler`
   (`AccessHarvester.Core/Com/AccessDateMaximaSampler.cs`) stamps the sampled
   Access **local wall-clock** value as UTC via `DateTime.SpecifyKind(…, Utc)`
   (a kind mislabel, not a conversion), and wraps per-column sampling in a broad
   `catch → null` that records **no degradation** — a swallowed read looks
   identical to "column has no date."
3. **O-2-6 — analyser has no zero-date guard.** `CleanupAnalyser` computes
   `days` from whatever maximum the sampler returns, with no floor check, so the
   `1899-12-30` misread flows straight through to a finding.

Each defect is **proven** in the 006 record / memory; this set implements the
recorded recommendations and **re-grounds every claim against the current tree**
(the 006 probes are dated 2026-06-12; sets 008–013 have since landed — the exact
file/line pointers below MUST be re-confirmed before editing).

### Fix options (decide explicitly in-session and record the decision)

Per the established "decide explicitly, record it" pattern:

- **Root-cause (sampler):** skip / specially-handle **time-only** columns at the
  sampler so a time-of-day value never presents as a `1899-12-30` date maximum.
  This is the most faithful fix but requires identifying time-only columns from
  the COM/DAO type metadata (the harvester already distinguishes `Date/Time`;
  time-only-ness may need a format or value probe — re-ground what signal is
  actually available before committing to this option).
- **Cheap guard (analyser):** treat `MaxValue <= 1899-12-30` (the OLE epoch base)
  as **no-signal** in `CleanupAnalyser` — emit no staleness finding (and/or an
  explicit "no date signal" note) rather than a ~46176-day finding. Cheap,
  hermetic, and robust regardless of why the maximum is the zero-date.
- **Both** are compatible; the cheap analyser guard is the safety net even if the
  root-cause sampler fix is deferred. **At minimum ship the analyser guard.**
- **O-1-5 timezone:** correct the `DateTimeKind` handling so a local wall-clock
  value is not mislabelled UTC (either keep it `Unspecified`/`Local` honestly or
  document why UTC is acceptable). **O-1-5 swallow:** replace the silent
  `catch → null` with a degradation record so a swallowed per-column read is
  visible, not indistinguishable from "no date."

**Non-goals:** the open DW items (DW-1..DW-8, DW-11/12/13/14 and **DW-15** —
`identify-cleanup`'s exit-2-for-usage-errors convention; a distinct exit-code
concern, NOT a staleness concern, candidate `cli-exit-code-consistency` set), and
any new extraction/parser capability. O-2-5 (data-load NULL-pad/dup-header) is a
migration-generator finding, **not** in P6 scope. Anything discovered beyond the
zero-date misfire + O-1-5 + O-2-6 that no set owns → a `DW-<n>` entry, not scope
creep.

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json` or any
  NorthwindDevEdition-derived artifact** — regeneration reintroduces the residue
  sets `003`–`005` removed. All fixtures and goldens derive from committed
  HomeHealthCare artifacts only. NorthwindDevEdition is proprietary and local-only.
- **Golden regeneration is HomeHealthCare-only** and follows the established
  regeneration path (`samples/GoldenOutput/HomeHealthCare/`); regenerate once per
  session, after that session's behavioral changes are complete, and re-run
  `CanonicalGoldenRegressionTests`. If the analyser/sampler change alters the
  committed cleanup / `object-use-checklist` outputs, that is a coordinated
  HomeHealthCare regen this session; if it asserts "no regen needed," prove it
  **byte-level** (a fresh COM run reproduces the committed golden), never by
  assertion. Assess golden/checklist impact explicitly — the cleanup analyser
  output is golden-adjacent.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on, TRX
  `<Counters>` as the completion signal; never background a `dotnet` invocation;
  sweep spurious `samples/*.accdb` diffs (`git checkout -- samples/HomeHealthCare.accdb`)
  before staging any commit.
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`.
- **Do not weaken assertions or tests to force green.** Intent changes require
  explicit disclosure in the verification prompt. New validation that changes a
  finding's severity (e.g. suppressing a previously-emitted stale finding) must
  be pinned by a hermetic test asserting the new behaviour, not silently accepted.

### Set-specific constraint — the guard must suppress only the misfire, not real signal

- **A real recent date maximum must still produce its correct staleness
  finding** — the zero-date guard suppresses only the `<= 1899-12-30` no-signal
  case, never a legitimate old-but-real date.
- **A swallowed per-column read must become a visible degradation**, not a
  silent `null` that reads as "no date" — the explicit-over-silent contract.
- **The HomeHealthCare golden / `object-use-checklist` delta (if any) must be
  explained by the guard** (a previously-misfiring column losing its false
  finding). An unexplained delta is a stop-and-investigate signal.

---

## Sessions

### Session 1 of 1: Zero-Date Guard + Sampler Timezone/Degradation Fixes (misfire, O-1-5, O-2-6)

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `deferred-work.md`, the 006 O-1-5 / O-2-6 findings + the
   `project_cleanup_com_zerodate_misfire` memory) and **re-ground**: confirm the
   `AccessDateMaximaSampler` `SpecifyKind(Utc)` site and the per-column
   `catch → null`; confirm `CleanupAnalyser`'s `days` computation has no
   zero-date floor; confirm what type/format signal is available to identify a
   **time-only** column at the sampler.
2. **O-2-6 (the safety net) — analyser zero-date guard:** treat
   `MaxValue <= 1899-12-30` as no-signal in `CleanupAnalyser` (emit no staleness
   finding and/or an explicit "no date signal" note). Hermetic pins: a
   `1899-12-30` maximum → no stale finding; a real recent/old date → unchanged
   finding.
3. **Zero-date misfire root cause (decide explicitly):** if a reliable time-only
   signal exists at the sampler, skip/special-case time-only columns so they
   never present as `1899-12-30`; otherwise record the analyser guard as the
   shipped fix and the sampler root-cause as deferred (`DW-<n>`) with the trigger.
4. **O-1-5 — sampler timezone + degradation:** correct the `DateTimeKind`
   mislabel (do not stamp a local wall-clock value as UTC), and replace the
   silent per-column `catch → null` with a degradation record. Hermetic pins for
   the degradation; the timezone fix is golden-/behaviour-assessed.
5. Hermetic validation of the affected analyser/sampler projects; **assess golden
   / `object-use-checklist` impact** — regen HomeHealthCare only if output
   changed (explain every delta by the guard), else prove no-regen byte-level.
6. Cross-provider verification (closeout-grade: prose-first lead + trailing JSON
   block; embed any earlier-session evidence per the cross-session-evidence
   lesson; run the in-IDE devil's-advocate pair as primary on the fresh code).
7. **Author the next session set** (last-session-authors-next-set): the 006 arc
   is complete after this set, so the next pick is **consumer-driven only**
   unless an open DW item's trigger has arrived — author against actual
   post-014 state via routed recommendation, carrying the Hard Constraints block
   verbatim. (DW-15 `cli-exit-code-consistency` is a standing candidate.)
8. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify.

**Progress keys:** `s1-analyser-zero-date-guard`, `s1-sampler-timeonly-decision`,
`s1-sampler-timezone-degradation`, `s1-golden-or-no-regen`, `s1-next-set-authored`,
`s1-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is P6 only** (zero-date misfire + O-1-5 + O-2-6). If a fix appears to
  require new extraction/parser capability or a DW item, stop at the boundary,
  record a `DW-<n>` entry, and leave it to the owning set.
- **The guard must suppress only the misfire, never real signal**, and a
  swallowed read must become a visible degradation, never a silent `null`.
- **Every golden / checklist delta (if any) must be explained by the guard.** An
  unexplained delta is a stop-and-investigate signal; a "no regen needed" claim
  must be proven byte-level.
- **Do not weaken assertions or tests to force green.**

## End-Of-Set Deliverables

- Analyser zero-date guard (`MaxValue <= 1899-12-30` → no-signal), hermetically
  pinned, with a real-date regression guard.
- A recorded explicit decision on the sampler time-only root-cause fix (shipped
  or deferred as `DW-<n>` with its trigger).
- O-1-5: `DateTimeKind` mislabel corrected; per-column swallow replaced by a
  visible degradation record, hermetically pinned.
- Golden regen only if output changed, every delta explained by the guard;
  otherwise no-regen proven byte-level.
- Next session set authored (spec + not-started v4 state stub) or a recorded
  consumer-driven-only handoff if no DW trigger has arrived.
- Standard set artifacts per session: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md`, `sN-issues.json` for
  findings-bearing rounds, `disposition.json`, `change-log.md`,
  `next-session-set-recommendation.md`.
