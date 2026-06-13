# Output Contract Fidelity Batch Spec

> **Purpose:** Stop the harvester from shipping output that is *mislabeled or
> non-conformant in ways that directly mislead a downstream triage AI*. Five
> distinct contract defects, proven in the 006 review, ride in one coordinated
> set because they share one golden regeneration: every crosstab in every
> database is labeled `MakeTable` and classified a possibly-unused action query
> (F-2-1); explicit `ByVal` is indistinguishable from default ByRef under the
> repo's own omission contract (F-3-5); four consumer-facing JSON files carry a
> UTF-8 BOM strict parsers reject (F-2-3); the documented label-provenance
> casing contradicts the emitter (F-2-6); and a short tail of source-only
> Minors (F-2-4 implement-or-strike, F-2-5, F-2-7, F-1-4, F-1-7) plus the R2-3
> classify name-form documentation ride-along (O-1-2) and O-1-1's interim
> interpretation-guide honesty note.
> **Created:** 2026-06-13 (authored by 009 Session 1 per the
> last-session-authors-next-set pattern)
> **Session Set:** `docs/session-sets/010-output-contract-fidelity-batch/`
> **Prerequisite:** P0 (committed green build/test baseline). Sequence after
> P1a (008, complete) and P1b (009, complete) to share regen discipline; not
> blocked by either.
> **This is 006's P2.** Source of truth: the 006 review findings
> (`s1/s2/s3-review-findings.md`) and `review-recommendations.md` §3 P2.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 2
prerequisites: []
```

> Rationale: no UI surface; the quality gate is build + tests + cross-provider
> verification. Unlike 009 (ASCII-stable, no regen), this set **does** change
> shipped output (query-type labels, ByVal emission, BOM removal, casing) and
> therefore **regenerates the HomeHealthCare golden once, at the end of the
> set** (Session 2), per the established regeneration discipline. The COM tier
> is exercised deliberately for that single regen + the
> `CanonicalGoldenRegressionTests` confirmation.

---

## Project Overview

What this set delivers, in consumer terms — the findings are **proven** in the
006 record; this set implements the recorded recommendations, it does not
re-litigate the diagnoses. Read each finding's full Evidence/Recommendation
block in the cited 006 file before touching it.

### Major (golden-affecting emission)

1. **F-2-1 (Major) — DAO query-type decode table is shifted.** Crosstabs ship
   as `"MakeTable"` and classify as possibly-unused action queries. Fix the DAO
   query-type decode mapping; cross-check against the jet-sql parser's
   `Sql.Parsed.QueryType` (F-3-7, Minor — the parser already parses `TRANSFORM`
   correctly but its result is discarded and re-stamped by the shifted DAO
   decode). Source: `s2-review-findings.md` F-2-1, `s3-review-findings.md`
   F-3-7. **Sequence F-2-1 first** (report-feeding queries depend on correct
   typing).

2. **F-3-5 (Major) — explicit `ByVal` is never emitted in `ParserOutput`
   signatures.** Under the repo's documented "only emit what Access wrote /
   omission = default" convention, a consumer reads every `ByVal` parameter as
   the default `ByRef`. Emit explicit `ByVal` (the parser carries it). Source:
   `s3-review-findings.md` F-3-5. **Consider the DW-3 suffix→type-for-variables
   and DW-4 `!`-function-name deferrals** (`docs/planning/deferred-work.md`) —
   F-3-5 is the natural home for the suffix→type emission parallel (DW-3);
   decide explicitly whether to pull DW-3 in here or leave it, and record the
   decision. (DW-4 is a grammar/lexer change, out of scope.)

### Minor (contract conformance, casing, dead-code, docs)

3. **F-2-3 (Minor) — UTF-8 BOM on the four usage/cleanup JSON outputs**, unlike
   every other output surface; strict parsers reject it. Write them without a
   BOM. Source: `s2-review-findings.md` F-2-3.
4. **F-2-6 (Minor) — label-provenance casing in shipped output contradicts the
   documented convention.** Reconcile emitter and doc (decide which is
   authoritative; the 006 finding has the grep). Source: `s2-review-findings.md`
   F-2-6.
5. **F-2-4 (Minor) — Strategy rule Q6 ("query references no tables") is dead
   code** (the helper unconditionally returns false). **Implement-or-strike
   decision** required — route it through decision consensus if non-obvious.
   Source: `s2-review-findings.md` F-2-4.
6. **F-2-5, F-2-7, F-1-4, F-1-7 (Minor)** — inline-SQL RecordSource
   referenced-ness credit (F-2-5); `data.sql` unquoted numeric/bit CSV token
   interpolation (F-2-7); plus F-1-4 and F-1-7 (see `s1-review-findings.md`).
   Each is partially- or fully-proven from source in 006; fix or explicitly
   defer (with a `DW-<n>` entry) per its finding.
7. **R2-3 ride-along (O-1-2) — classify slug casing / database name-form
   documentation.** Document which name-form (`.accdb`-suffixed vs stripped)
   each surface uses so consumers can join classification output to manifest
   data. Source: `s1-review-findings.md` O-1-2.
8. **O-1-1 interim note** — if the P3 wiring arc has not landed,
   `unresolved-items.json` is a permanently-empty artifact; add a one-paragraph
   honesty note to the interpretation guide (interim, removed when P3 wires it).
   Source: `s1-review-findings.md` O-1-1.

**Non-goals:** P3 resolver/navigation wiring (F-3-4, F-2-2), P4 edge
derivation, P5/P6 CLI robustness. Do not wire resolver seams here. Anything
discovered beyond these findings that no set owns → a `DW-<n>` entry, not
ride-along scope creep.

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

Per the 2026-06-12 lesson *constraints travel in routed prompts*, these are
restated here in full, not by reference:

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json`
  or any NorthwindDevEdition-derived artifact** — regeneration reintroduces
  the residue sets `003`–`005` removed. All fixtures and goldens derive from
  committed HomeHealthCare artifacts only. NorthwindDevEdition is
  proprietary and local-only.
- **Golden regeneration is HomeHealthCare-only** and follows the established
  regeneration path (`samples/GoldenOutput/HomeHealthCare/`); regenerate
  once per session, after that session's behavioral changes are complete,
  and re-run `CanonicalGoldenRegressionTests` to confirm. *(For this set a
  single coordinated regen at the END of Session 2 is expected, because the
  emission changes (F-2-1, F-3-5, F-2-3, F-2-6) alter shipped output. Each
  golden delta must be explained by a specific finding's fix — an
  unexplained delta is a stop-and-investigate signal.)*
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on,
  TRX `<Counters>` as the completion signal; never background a `dotnet`
  invocation; sweep spurious `samples/*.accdb` diffs
  (`git checkout -- samples/*.accdb`) before staging any commit.
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`;
  its re-enable belongs to the proposed sanitize-testhost-wedge
  investigation, not to this set.
- **Do not weaken assertions or tests to force green.** Intent changes
  require explicit disclosure in the verification prompt.

---

## Sessions

### Session 1 of 2: Query-Type Decode + ByVal Emission (F-2-1, F-3-7, F-3-5)

The two Majors — the golden-affecting emission corrections. Both are pure
emission/decode logic with hermetic-testable seams.

**Steps:**

1. Read `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`,
   `docs/planning/deferred-work.md`, and the 006 findings F-2-1, F-3-7, F-3-5.
2. **Re-ground** each finding against the current tree (006 probes are dated
   2026-06-12). Prove F-2-1's shifted decode and F-3-5's missing-ByVal with a
   fresh probe before fixing (evidence-over-inference).
3. Fix F-2-1's DAO query-type decode; reconcile with the jet-sql parser's
   `QueryType` (F-3-7) so the parser result is no longer discarded/re-stamped.
4. Emit explicit `ByVal` in `ParserOutput` signatures (F-3-5). Make the DW-3
   suffix→type-for-variables pull-in-or-defer decision and record it.
5. Add/extend hermetic tests pinning corrected query-type decode (per-type) and
   ByVal emission. Hermetic validation: `dotnet build` + affected hermetic
   project(s), sequential, foreground.
6. **Defer the golden regen to Session 2** (one coordinated regen for the whole
   set). If a golden-affecting change lands here, note the expected delta but do
   not regenerate yet — Session 2 owns the single regen + COM confirmation.
7. Cross-provider verification of the session diff.
8. Closeout: `disposition.json` (in-progress), `change-log.md` increment,
   `close_session`, commit, push, notify.

**Progress keys:** `s1-query-type-decode-fixed`, `s1-byval-emitted`,
`s1-hermetic-pins-added`, `s1-verified`

### Session 2 of 2: BOM, Casing, Dead-Code, Docs + Coordinated Golden Regen

The Minors, the documentation ride-alongs, and the single end-of-set golden
regeneration with COM confirmation.

**Steps:**

1. Pre-session reading (as S1). Re-ground each Minor against the tree.
2. F-2-3 (strip BOM from the four usage/cleanup JSON writers), F-2-6 (label
   casing reconcile), F-2-4 (Q6 implement-or-strike — decision-consensus if
   non-obvious), F-2-5, F-2-7, F-1-4, F-1-7 (fix or defer w/ `DW-<n>`).
3. R2-3 ride-along (O-1-2 name-form documentation); O-1-1 interim
   interpretation-guide note if P3 has not landed.
4. Hermetic validation (build + affected hermetic projects).
5. **Single coordinated golden regen** (HomeHealthCare only), then the COM-tier
   `CanonicalGoldenRegressionTests` confirmation — deliberate, sequential,
   foreground, TRX `<Counters>` as truth; sweep `samples/*.accdb` before
   staging. Every golden delta must map to a specific finding's fix.
6. Cross-provider verification (closeout-grade: prose-first lead + trailing JSON
   block; embed earlier-session evidence per the cross-session-evidence lesson).
7. **Author the next session set** (last-session-authors-next-set): per the 006
   ranking the natural next set is `resolver-and-navigation-wiring` (P3, 3
   sessions; prereqs P1a ✓, P2 ✓ recommended) — author against actual post-010
   completion state, carrying the Hard Constraints block verbatim.
8. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify.

**Progress keys:** `s2-bom-stripped`, `s2-casing-reconciled`,
`s2-minors-resolved`, `s2-golden-regenerated`, `s2-next-set-authored`,
`s2-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is P2 only.** If a fix appears to require wiring a resolver/navigation
  seam (P3) or deriving edges (P4), stop at the boundary, record a `DW-<n>`
  entry, and leave it to the owning set.
- **Every golden delta must be explained by a named finding's fix.** An
  unexplained delta in the regenerated HomeHealthCare golden is a
  stop-and-investigate signal, not a silent regen.
- **Do not weaken assertions or tests to force green.**

## End-Of-Set Deliverables

- F-2-1 query-type decode corrected (crosstabs typed correctly, no longer
  mislabeled `MakeTable` / mis-classified); F-3-7 parser result no longer
  discarded.
- F-3-5 explicit `ByVal` emitted; DW-3 decision recorded.
- F-2-3 BOM removed from the four usage/cleanup JSON outputs; F-2-6 casing
  reconciled; F-2-4/F-2-5/F-2-7/F-1-4/F-1-7 resolved or deferred (w/ `DW-<n>`);
  O-1-2 name-form documented; O-1-1 interim note (if P3 not landed).
- Single coordinated HomeHealthCare golden regeneration; every delta explained;
  `CanonicalGoldenRegressionTests` green.
- Next session set authored (spec + not-started v4 state stub).
- Standard set artifacts per session: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md`, `sN-issues.json` for
  findings-bearing rounds, `disposition.json`, `change-log.md`,
  `next-session-set-recommendation.md`.
