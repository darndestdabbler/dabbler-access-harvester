# Output Encoding Correctness Spec

> **Purpose:** Stop the kb/ → ir/ pipeline from silently and irreversibly
> corrupting every non-ASCII caption, label, validation string, and VBA string
> literal in real-world databases. Today `SaveAsTextTranslator.BuildPayload`
> reads the Windows-1252 SaveAsText `.txt` dumps as UTF-8 (the .NET default),
> so a byte like `0xE9` ("é") decodes to U+FFFD — destroying the original byte
> one-way (006 review F-1-1, Major). The same caption then differs between
> `manifest.json` (which decodes correctly via the cp1252 path) and the ir/
> file the consumer reads — a determinism/contract violation across output
> surfaces. The corruption is invisible on the all-ASCII committed
> HomeHealthCare fixtures and certain on production Access apps (accented names
> are routine). This is 006's **P1b** — independent of P1a (008, complete) and
> carrying no shared golden churn.
> **Created:** 2026-06-13 (authored by 008 Session 3 per the
> last-session-authors-next-set pattern)
> **Session Set:** `docs/session-sets/009-output-encoding-correctness/`
> **Prerequisite:** P0 only (the committed green build/test baseline).
> **Independent of P1a (008).** Different files; the kb/ir goldens are
> ASCII-stable under the fix, so there is no golden contention with 008's
> manifest regen.
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
```

> Rationale: no UI surface in this repo; the quality gate is build + tests +
> cross-provider verification. The fix is a translator decode correction plus
> hermetic cp1252 pins; the kb/ir goldens are ASCII-stable under it, so a
> golden regeneration is **not expected** (and if one appears it is a signal
> the fix changed ASCII output — stop and investigate). `requiresE2E` does not
> apply (the hermetic translator pins are the behavioral gate).

---

## Project Overview

Source material: `006-solution-review-and-upstream-feedback-discovery` —
`review-recommendations.md` §3 P1b and the Recommendation blocks of F-1-1
(Major) and F-1-8 (Minor, test posture). Both are **proven-with-probes** in
the 006 record (`s1-review-findings.md`); this session implements the recorded
recommendations, it does not re-litigate the diagnosis.

What this set delivers, in consumer terms:

1. **Non-ASCII text survives the kb/ → ir/ tiers** (F-1-1). In
   `SaveAsTextTranslator.BuildPayload` (`Translation/SaveAsTextTranslator.cs`
   around lines 59/71), `File.ReadAllText(file)` / `File.ReadAllLines(file)`
   default to UTF-8. Replace with the **same** BOM-detect-then-cp1252 strategy
   the CLI `parse` path (`RunParse`, `Program.cs:581-600`) and the manifest
   path (`Pipeline.cs:297`, `Encoding.GetEncoding(1252)`) already use, so the
   translator decodes SaveAsText output as the Windows-1252 it actually is.
   `File.ReadAllText(file, Encoding.GetEncoding(1252))` preserves BOM detection
   and is a two-line change. Ensure `Encoding.RegisterProvider(
   CodePagesEncodingProvider.Instance)` runs before translator use from **any**
   host (the CLI registers it in `Main`; the new hermetic tests must register
   it too).

2. **The translator seam is pinned hermetically** (F-1-8). Add always-running
   (non-Access-gated) hermetic tests for `SaveAsTextTranslator`: one per
   signature-routing branch (`<?xml` → Macro, `Attribute VB_Name` / `Option `
   → Module, block format), the `keepRoot` macro flag, and — the F-1-1 lock —
   a **cp1252-byte fixture** asserting the decoded caption round-trips (e.g.
   bytes `43 E9 6C` → "Cél", and an end-to-end `Caption ="Re/Création"`
   asserting no U+FFFD reaches the kb JSON). No COM required — the translator
   is pure file-to-file logic (Testing Hierarchy preference 1).

**Known, accepted scope:** this set fixes the **translator** decode only. If a
sibling read site is found to share the UTF-8-default bug, fix it only if it is
on the same kb/ir text-decode seam; anything broader (a general
encoding-audit) is out of scope — record it as a `DW-<n>` deferred-work entry
and carry it forward. The BOM-on-output-files issue (F-2-3) and the rest of the
contract-fidelity batch belong to **P2** (`output-contract-fidelity-batch`) and
must not ride along here.

**Non-goals:** P2 contract-fidelity work (F-2-1 query decode, F-3-5 ByVal, BOM
on output JSON, label casing), P3 resolver/navigation wiring, P4 edge
derivation, P5 CLI robustness. No golden regeneration is expected; do not force
one.

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
  regen is not expected — the fix is ASCII-stable on the committed fixtures.
  If the golden changes, stop and investigate before regenerating.)*
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

### Session 1 of 1: Translator cp1252 Decode Fix And Hermetic Encoding Pins (F-1-1, F-1-8)

**Steps:**

1. Read `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`,
   `docs/planning/deferred-work.md` (mandatory pre-session reading;
   specifically incremental-test-by-project, never-background-dotnet,
   TRX-as-truth, `.accdb`-sweep, and constraints-travel-in-routed-prompts),
   006's `s1-review-findings.md` F-1-1 and F-1-8, and this spec.
2. Re-ground F-1-1 against the current tree (the 006 probes are dated
   2026-06-12; confirm the `SaveAsTextTranslator.BuildPayload` read sites and
   the `RunParse` BOM-detect-then-cp1252 reference still hold). Record the
   grounding in the session log. **Evidence-over-inference:** prove the bug
   still reproduces (write cp1252 bytes, read default vs cp1252) before fixing.
3. Implement the F-1-1 fix in `SaveAsTextTranslator.BuildPayload`: read with
   the BOM-detect-then-cp1252 strategy `RunParse` uses. Register the code-pages
   provider wherever the translator may run without the CLI's `Main` having
   registered it.
4. F-1-8 hermetic pins: add always-running tests for the translator — one per
   signature-routing branch, the `keepRoot` flag, and the cp1252 round-trip
   that locks the F-1-1 fix (the canonical `43 E9 6C` → "Cél" probe plus an
   end-to-end caption assertion of no U+FFFD).
5. Hermetic validation: `dotnet build`, then the affected hermetic test
   project(s) only (sequential, foreground). **No golden regen is expected;**
   if `CanonicalGoldenRegressionTests` would change, stop and investigate —
   the committed fixtures are ASCII-only, so a green tree is the expectation.
6. Confirm no COM tier is needed (the change is pure file-to-file translator
   logic on hermetic fixtures). If a golden-affecting change is discovered,
   treat it as out-of-expectation and follow the COM-run + `.accdb`-sweep
   discipline before regenerating once.
7. Cross-provider verification of the session's diff (include
   `SaveAsTextTranslator.cs`, the new hermetic tests, the `RunParse` reference
   read site, and the cp1252 evidence; disclose that no golden regen was
   needed and why).
8. **Author the next session set** (last-session-authors-next-set): per the
   006 ranking the natural next set is `output-contract-fidelity-batch` (P2,
   2 sessions) — author against actual post-009 completion state, carrying the
   Hard Constraints block verbatim.
9. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed analysis — carry the Hard
   Constraints verbatim in the routed prompt), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify.

**Creates:** hermetic `SaveAsTextTranslator` encoding tests (incl. a
cp1252-byte fixture); the next set's `spec.md` + `session-state.json` stub; set
closeout artifacts (`change-log.md`, `next-session-set-recommendation.md`,
`disposition.json`)
**Touches:** `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs`, tests,
set artifacts
**Ends with:** non-ASCII captions/labels/validation survive the kb/ → ir/
tiers (cp1252-decoded, no U+FFFD); the translator seam is pinned hermetically;
hermetic suites green; no golden regression; verification VERIFIED (closeout
grade); next set authored.
**Progress keys:** `s1-translator-decode-fixed`, `s1-encoding-pins-added`,
`s1-next-set-authored`, `s1-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to the session.
- **Scope is P1b only** (translator decode + its hermetic pins). If the fix
  appears to require touching the manifest path, the parser, or output-file
  BOM handling, stop at the boundary, record the evidence as a `DW-<n>` entry,
  and leave it to the owning set (P2/P3).
- **No golden regeneration is expected.** If the committed HomeHealthCare
  golden changes under the fix, the change is altering ASCII output — stop and
  investigate the cause before regenerating; a silent regen here would mask a
  real behavior change.
- **Do not weaken assertions or tests to force green.**

## End-Of-Set Deliverables

- `SaveAsTextTranslator` decodes SaveAsText `.txt` as BOM-or-cp1252; non-ASCII
  text survives into kb/ and ir/.
- Hermetic translator tests cover every routing branch plus the cp1252
  round-trip that locks the F-1-1 fix.
- No golden regression (or, if a change was unavoidable, a disclosed,
  investigated, single regeneration).
- Next session set authored (spec + not-started v4 state stub).
- Standard set artifacts: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `s1-verification.md`, `s1-issues.json`
  for findings-bearing rounds, `disposition.json`, `change-log.md`,
  `next-session-set-recommendation.md`.
