# StudentICT Local Invariant Corpus Spec

> **Purpose:** Onboard **StudentICTDatabase** (a proprietary, split front-end /
> back-end Access application with rich VBA) as a **second local-only invariant
> corpus**, alongside NorthwindDevEdition — **never as a committed golden**. It
> covers a path no committed corpus exercises: a **split FE/BE design with linked
> tables**, a **second independent custom ribbon**, and a **large real DW-18
> signal** (intrinsic-constant false-positive unresolved refs). Add a skip-gated
> `StudentIctLocalInvariantTests` asserting corpus-independent invariants, file the
> deferred-work items the onboarding harvest surfaced, and document the
> split-FE behaviour — committing **no** StudentICT-derived artifact.
> **Created:** 2026-06-15 (after 016 closed DW-13; the human supplied a second
> proprietary DB and asked whether it could be a golden — a local exploratory
> harvest proved it cannot be committed but is a high-value local corpus; see
> `feedback_use_northwind_as_local_test_corpus` and the exploratory findings in
> Project Overview).
> **Session Set:** `docs/session-sets/017-studentict-local-invariant-corpus/`
> **Prerequisite:** **P0 only** (a committed green build/test baseline).
> Independent of any other open set; slots anywhere.
> **Source of truth:** `NorthwindLocalInvariantTests`
> (`tests/AccessHarvester.Integration/Pipeline/NorthwindLocalInvariantTests.cs`,
> the local-corpus template), the local exploratory harvest of
> `samples/StudentICTDatabase.accdb` (`run` → `C:/temp`, never committed), and
> `docs/planning/deferred-work.md` (DW-18 magnitude evidence; the new DW the
> onboarding files). Re-confirm every pointer before editing.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full tier;
> no UI, so no UAT/E2E.

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
> verification. A single session: the onboarding is **test + docs + deferred-work**
> work (the production producers already handle the corpus — the exploratory
> harvest ran clean), so there is no behaviour change to split across sessions.
> `verificationMode: out-of-band-or-none` matches the inline-verify Full-tier
> pattern (007–016); register with `--verification-mode out-of-band-or-none`.

---

## Project Overview

A local exploratory harvest of `samples/StudentICTDatabase.accdb` (FE) on
2026-06-15 (`run --db … --out C:/temp/…`, foreground COM, output discarded)
**ran clean** (0 failures across Tables/Queries/Forms/Reports/Modules/Macros/
Navigation) and surfaced four facts that make this a high-value **local** corpus —
and that **rule it out as a golden**:

1. **It is split FE/BE with linked tables — coverage nothing committed has.**
   13 of 20 FE tables carry a `Connect` string + `SourceTableName` pointing at
   `StudentICTDatabase_BE.accdb`; 7 are local (incl. `USysRibbons`).
   `Relationships` is `0` on the FE (a split FE's relationships live in the BE).
   Neither HomeHealthCare nor NorthwindDevEdition is split, so the linked-table
   surface and the split-FE relationships-absent behaviour are currently untested.
2. **It embeds a credential + an absolute local path — a hard reason it can NEVER
   be a committed golden.** Each linked-table `Connect` string contains a
   **plaintext `PWD=…`** and the **absolute filesystem path** to the BE
   (`…\samples\StudentICTDatabase_BE.accdb`). Committing harvested output would
   leak a password and a machine path — exactly the proprietary-residue class
   sets 003–005 removed, made worse by a live credential. (This is also a real
   harvester finding → DW-24, below.)
3. **A second independent custom ribbon.** `NavigationGraph.Ribbon` = "Help"
   (tab `tab1`) with 5 `RibbonEntryPoints` (1 `OnLoad` + 4 `OnAction`), **all
   `Resolved`** to `modRibbonCallbacks` (the four buttons share one
   `OnActionButton` callback — a common dispatch pattern). This independently
   re-validates the 016 / DW-13 ribbon producer + S2 bindable-resolution on a
   different ribbon than Northwind.
4. **A large real DW-18 signal.** `UnresolvedItems` = 3380, of which **3315 are
   `FunctionClassification`** — almost certainly dominated by VBA intrinsic
   constants (`vbCrLf`, `acForm`, …) surfacing as false-positive unresolved refs.
   This corpus is the empirical demonstration of DW-18's scale and the natural
   validation corpus when DW-18 is fixed.

**Conclusion (decided before this set was authored):** StudentICTDatabase is **not
a golden** (proprietary + embedded credential/path) but **is** a valuable second
**local-only invariant corpus** — it is the only split-FE/BE corpus, a second
custom-ribbon corpus, and a large DW-18 witness.

---

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

- **NEVER commit, regenerate, or recreate any StudentICTDatabase-derived
  artifact** — not the FE/BE `.accdb`, not the `.lnk` shortcut, not any harvested
  `raw/`/`kb/`/`ir/`/`manifest.json`/ribbon dump/parse report. StudentICTDatabase
  is proprietary and **local-only** (FE, BE, and `samples/*.lnk` are gitignored;
  confirm `git check-ignore` before any `git add`). Its linked-table `Connect`
  strings embed a **plaintext password and an absolute local path** — committing
  output leaks a live credential. Validate against it **locally** only; commit
  **no** golden and **no** fixture derived from it.
- **No new committed golden.** This corpus is asserted only by a **skip-gated**
  `StudentIctLocalInvariantTests` over **corpus-independent invariants** (presence
  / structural shape, not brittle exact counts), exactly like
  `NorthwindLocalInvariantTests`. There is no committed golden for it, by design.
- **The test must not assert machine-specific or sensitive values.** Do NOT assert
  the absolute BE path or the password from the `Connect` string. Assert robust
  invariants: that a linked table surfaces a non-empty `Connect` + `SourceTableName`
  and that the `Connect` references the BE *filename* (`StudentICTDatabase_BE.accdb`),
  that the second ribbon resolves, that the FE harvest runs clean, that
  `FunctionClassification` unresolved refs are present (DW-18 witness). Skip-gate on
  **both** the FE and BE files being present (linked-table resolution needs the BE).
- **Golden regeneration remains HomeHealthCare-only** (`GoldenCorpusRegenerator`,
  `AH_REGEN_GOLDEN=1`). This set is **expected golden-neutral on HHC** — it adds a
  local test + docs + deferred-work, touching no producer that affects the HHC
  manifest. Prove HHC byte-neutral byte-level (a fresh run reproduces the committed
  golden), never by assertion.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on, TRX
  `<Counters>` as the completion signal; never background a `dotnet` invocation.
  StudentICTDatabase FE/BE are gitignored, so Access mutating their timestamps
  needs no sweep; still `git checkout -- samples/HomeHealthCare.accdb` before
  staging if any HHC COM test ran.
- **The quarantined sanitize test stays quarantined.**
- **Do not weaken assertions or tests to force green.** If an invariant does not
  hold on the real corpus, that is a finding to record, not an assertion to soften.

---

## Sessions

### Session 1 of 1: Add StudentICTDatabase as a local invariant corpus + onboarding findings

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `feedback_use_northwind_as_local_test_corpus`) and **re-ground**: re-run the
   **local** FE harvest (`run --db samples/StudentICTDatabase.accdb --out
   C:/temp/…`, foreground COM, discard output) to re-confirm the four Project
   Overview facts on the current tree; confirm `git check-ignore` lists the FE, BE,
   and `samples/*.lnk`; read `NorthwindLocalInvariantTests` as the template.
2. **Add `StudentIctLocalInvariantTests`** (skip-gated like Northwind: skip when
   Access COM is absent OR when either `StudentICTDatabase.accdb` /
   `StudentICTDatabase_BE.accdb` is absent; serialised via the
   `HomeHealthCareCom` collection). Assert **corpus-independent invariants**, e.g.:
   the full pipeline runs clean on the split FE; at least one table surfaces a
   non-empty `Connect` + `SourceTableName` whose `Connect` references
   `StudentICTDatabase_BE.accdb` (the linked-table / split-FE surface); the manifest
   schema-validates; `NavigationGraph.Ribbon` is populated and its
   `RibbonEntryPoints` resolve (second-ribbon witness — `OnRibbonLoad` /
   `OnActionButton` → `modRibbonCallbacks`, all `Resolved`); `FunctionClassification`
   unresolved references are present (DW-18 witness). **Never** assert the absolute
   BE path or the `PWD=` value.
3. **File the onboarding deferred-work item(s):**
   - **DW-24** — linked-table `Connect` strings are harvested with a **plaintext
     password and absolute local path**; the harvester does not redact connection
     credentials. Scope: should `sanitize` (and/or the table exporter) redact
     `PWD=`/`PASSWORD=` and normalise absolute back-end paths in linked-table
     `Connect` strings? Record the privacy/security concern for **any** split DB a
     consumer harvests; do **not** fix it in this set (scope is corpus onboarding).
   - Document the **split-FE `Relationships: 0`** behaviour (relationships live in
     the BE; the FE harvest does not reach them) — as an interpretation-guide note
     and/or a DW if a consumer needs split-DB relationship completeness.
4. **Golden:** prove HHC byte-level neutral (this set touches no HHC-affecting
   producer); run the **local** StudentICT harvest and confirm the invariants hold.
5. Cross-provider verification (prose-first + trailing JSON; in-IDE
   devil's-advocate pair as primary on the invariant robustness — guard against
   brittle / machine-specific / sensitive assertions).
6. **Author the next session set** (last-session-authors-next-set) via routed
   recommendation against actual post-017 state, carrying the Hard Constraints
   block — or a recorded consumer-driven-only handoff (the standing bundles —
   ribbon-completeness DW-20/21/22/23, DW-18 golden-affecting, navigation DW-14,
   encoding DW-5/6, parser DW-1..4, reference-output DW-8/17/19 — plus the new
   DW-24).
7. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify.

**Progress keys:** `s1-reground-and-local-harvest`, `s1-studentict-invariant-tests`,
`s1-onboarding-deferred-work`, `s1-golden-neutral`, `s1-verified`,
`s1-next-set-authored`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full.
- **Scope is corpus onboarding only** (a local invariant test + docs + deferred-work
  filing). If the work appears to need a **producer change** (e.g. redacting
  `Connect`-string credentials, harvesting the BE for relationships, fixing DW-18),
  stop at the boundary, record/confirm the `DW-<n>` entry, and leave it to the
  owning set. This set must remain golden-neutral on HHC.
- **Never commit a StudentICTDatabase artifact** (FE/BE/`.lnk`/derived output).
  Validate locally; commit only the skip-gated test + docs + deferred-work edits.
- **Every golden delta must be explained by the fix.** This set is expected HHC
  golden-neutral; any HHC delta is a stop-and-investigate signal.
- **Do not weaken assertions or tests to force green** — a failing real-corpus
  invariant is a finding, not an assertion to soften.

## End-Of-Set Deliverables

- A skip-gated `StudentIctLocalInvariantTests` asserting corpus-independent
  invariants over the **local** split-FE/BE StudentICTDatabase (linked-table /
  split surface, second ribbon, DW-18 witness) — **no committed golden**, no
  sensitive/machine-specific assertions.
- DW-24 filed (linked-table `Connect`-string credential/path redaction) + the
  split-FE relationships-absent behaviour documented.
- HHC golden proven byte-level neutral.
- Next session set authored (spec + not-started v4 state stub) or a recorded
  consumer-driven-only handoff.
- Standard set artifacts: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `s1-verification.md`, `s1-issues.json` for
  findings-bearing rounds, `disposition.json`, `change-log.md`,
  `next-session-set-recommendation.md`.
