# Next Session Set Recommendation (authored by Set 013 Session 2)

> Routed recommendation (gpt-5-4 via `route(task_type="planning")`, 2026-06-14).
> Raw routed output: `_nextset-routed.md` in this folder. Synthesised here against
> actual post-013 completion state.

## Recommendation: **Set 014 — `cleanup-zero-date-guard`** (006's P6)

**Scope (1 session):** Fix the known `identify-cleanup` zero-date misfire — the
COM sampler misreads time-only `DateTime` columns as the OLE zero-date
`1899-12-30`, which flows straight into a ~46176-day stale **Medium/High**
cleanup finding — together with its two adjacent 006 findings:

- **O-1-5** — `AccessDateMaximaSampler` (`Core/Com`) stamps Access local
  wall-clock dates as UTC (`DateTime.SpecifyKind(..., Utc)`) **and** swallows
  per-column failures silently (broad `catch → null`, no degradation record).
- **O-2-6** — `CleanupAnalyser` has **no zero-date guard**; it computes `days`
  from whatever maximum the sampler returns, so the misread maximum becomes a
  false high-severity finding.

**Lean fix options (decide explicitly in-session, per the established pattern):**
(a) skip time-only columns at the sampler (root cause); and/or (b) treat
`MaxValue <= 1899-12-30` as no-signal in the analyser (cheap guard); plus a
`MigrationDegradation`/degradation note for the swallowed per-column failures.

## Why this set next

1. **DAG completion.** P6 is the **last unowned ranked set** in the 006
   solution-review arc (P0→P1a∥P1b→P2→P3→P4→P5→P6). Sets 007–012 closed P0–P4;
   Set 013 closed P5. P6 closes the arc.
2. **Consumer value.** A false "High"-severity staleness finding erodes trust in
   `identify-cleanup`; eliminating the zero-date misfire makes the command
   actionable.
3. **De-risked.** Root causes (O-1-5, O-2-6) are well understood with lean fix
   options pre-identified, and the fix is hermetically testable (synthesise a
   time-only-column sampler input; assert no stale finding + a degradation note).

## Recommended orchestrator

- **Engine / model / effort:** Claude Code · **Opus 4.8** · **effort=low**.
- **Rationale:** a well-scoped C# fix with specific known root causes
  (`DateTime` kind handling, an OLE zero-date guard, a degradation record). The
  mandatory cross-provider `session-verification` still routes to **gpt-5-4**.

## Independence & prerequisites

- **Independent — prereq P0 only** (a committed green build/test baseline). It
  does not depend on the 006 P1–P5 arc; it slots anywhere. **1 session.**

## Also-open (not the next pick, but tracked)

The deferred-work register carries DW-1..DW-8, DW-11, DW-12, DW-13, DW-14, and
the **new DW-15** (`identify-cleanup` returns exit 2, not the contract's 1, for
usage errors → candidate small `cli-exit-code-consistency` set). None is a ranked
006 set; each becomes a focused set when its trigger arrives. DW-15 is a natural
neighbour of Set 014 (both touch `identify-cleanup`) but is a distinct concern
(exit-code contract vs. zero-date staleness) — keep them separate unless the human
prefers to bundle.

## Hard Constraints (carry verbatim into every routed prompt for Set 014)

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json` or any
  NorthwindDevEdition-derived artifact** — regeneration reintroduces the residue
  sets `003`–`005` removed. All fixtures and goldens derive from committed
  HomeHealthCare artifacts only. NorthwindDevEdition is proprietary and local-only.
- **Golden regeneration is HomeHealthCare-only** and follows the established
  regeneration path (`samples/GoldenOutput/HomeHealthCare/`); regenerate once per
  session, after that session's behavioral changes are complete, and re-run
  `CanonicalGoldenRegressionTests`. If a session asserts "no golden regen needed,"
  prove it byte-level (a fresh COM run reproduces the committed golden), never by
  assertion. *(The zero-date fix touches the COM sampler / cleanup analyser, whose
  output feeds `identify-cleanup` — assess golden/checklist impact explicitly; the
  `object-use-checklist` and cleanup outputs are the artifacts to watch.)*
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on, TRX
  `<Counters>` as the completion signal; never background a `dotnet` invocation;
  sweep spurious `samples/*.accdb` diffs (`git checkout -- samples/HomeHealthCare.accdb`)
  before staging any commit.
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`.
- **Do not weaken assertions or tests to force green.** Intent changes require
  explicit disclosure in the verification prompt. New validation that changes an
  exit code or a finding's severity must be pinned by a hermetic test asserting
  the new behaviour, not silently accepted.
