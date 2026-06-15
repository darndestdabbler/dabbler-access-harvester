# Manifest Source-Location Column Convention Spec (DW-26)

> **Purpose:** Reconcile the **0-based source-location *column* convention** the
> harvester emits with the **1-based contract** the manifest schema declares, so a
> real split / richly-authored VBA database produces a **schema-valid** manifest.
> Today the VBA AST's column is 0-based (ANTLR convention) and
> `ParserOutputProjector.BuildLocation` projects it straight through with no `+1`,
> while `harvester-schema.v1.1`'s `SourceLocation` `$def` requires
> `StartColumn`/`EndColumn` `minimum: 1`. A reference/call token at the **first
> character of a line** is column `0` → below the minimum → the manifest fails
> `SchemaValidator.Validate`. The committed HomeHealthCare golden and the local
> NorthwindDevEdition corpus validate **only because their indented VBA happens to
> have no column-0 token** — not because they honour a 1-based contract. The local
> StudentICTDatabase corpus (Set 017) is the first to make the mismatch
> schema-visible (521 `StartColumn: 0` values) and is the validation witness.
> **Created:** 2026-06-15 (authored by Set 017 Session 1 at human direction —
> overriding the consumer-driven-only handoff — as the strongest pull-in candidate;
> see `docs/session-sets/017-studentict-local-invariant-corpus/next-session-set-recommendation.md`).
> **Session Set:** `docs/session-sets/018-manifest-source-location-column-convention/`
> **Prerequisite:** **P0 only** (a committed green build/test baseline). Independent
> of any other open set.
> **Source of truth:** `docs/planning/deferred-work.md` → **DW-26** (the corrected
> root-cause analysis + the two fix options); `AccessHarvester.Vba.Parser/Ast/SourceLocation.cs`
> ("Column is 0-based (ANTLR convention)"); `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs`
> (`BuildLocation` ~line 431 projects the raw column; the synthetic top-level
> location is hardcoded `StartColumn = 1` ~line 115); `docs/test-cases/harvester-schema.v1.1.json`
> (the `SourceLocation` `$def`, `minimum: 1`); `tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs`
> (the DW-26 known-limitation pin to flip) and `NorthwindLocalInvariantTests`.
> Re-confirm every pointer before editing.
> **Workflow:** Orchestrator → AI Router → cross-provider verification. Full tier;
> no UI, so no UAT/E2E. **Decision-time cross-provider consensus is REQUIRED** on
> the convention choice (it changes the public column-base contract and is
> golden-affecting) before any code change.

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

> Rationale: no UI surface; the quality gate is build + tests + schema validation +
> cross-provider verification. A single session: the work is one decision + one
> focused projection/schema change + (if golden-affecting) a coordinated HHC golden
> regen + flipping the Set 017 known-limitation pin + a local StudentICT validation.
> `verificationMode: out-of-band-or-none` matches the inline-verify Full-tier pattern
> (007–017); register with `--verification-mode out-of-band-or-none`.

---

## Project Overview

DW-26 is **column-only**. `StartLine`/`EndLine` are already 1-based (ANTLR lines are
1-based; the HHC golden and the StudentICT manifest both have a line minimum of 1,
never 0) and match the schema's `minimum: 1`. Only the **column** is 0-based, so the
manifest's `line:col` pairs are internally inconsistent: 1-based line, 0-based column.
The projector itself betrays the intended 1-based column contract — it **hardcodes**
the synthetic document-root location to `StartColumn = 1` while passing every real
reference/call column through 0-based. The fix is therefore a narrow, well-localised
column-base reconciliation; the *decision* (which way to reconcile) is the substantive
part because it sets the public contract and determines golden churn.

**Two mutually-exclusive options (decide via consensus + human sign-off):**

- **Option A — normalise columns to 1-based at projection (`+1`).** Add `+1` to
  `StartColumn`/`EndColumn` in `ParserOutputProjector.BuildLocation` (and to any
  sibling column-emitting site), giving a uniform **1-based `line:col`** contract that
  matches the already-1-based lines, the schema's `minimum: 1`, the hardcoded
  top-level `=1`, and the human-editor convention a downstream AI expects.
  **Golden-affecting:** *every* source-location column in the committed HomeHealthCare
  golden shifts `+1` — a large, mechanical, coordinated regen. Recommended unless
  consensus surfaces a reason not to (it makes the contract consistent and
  consumer-intuitive).
- **Option B — relax the schema to `minimum: 0` (0-based columns).** Change the
  `SourceLocation` `$def` (and the flat-`StartLine`-shorthand sibling if applicable)
  to `minimum: 0` for columns, document the column base as 0-based, and reconcile the
  projector's hardcoded top-level `StartColumn = 1` to the true 0-based root.
  **Not golden-column-affecting** (HHC columns already 0-based-but-≥1 stay valid), but
  it makes the public contract 0-based — any consumer assuming 1-based must adjust, and
  it leaves `line:col` mixed-base (1-based line, 0-based column) unless lines are also
  reconsidered (they should NOT be — they are correct).

The decision must be made and recorded **before** writing code (decision-time
consensus per `docs/ai-led-session-workflow.md` → Decision-time consensus, with human
sign-off because it is a public-contract + golden-affecting choice).

---

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

- **NEVER commit, regenerate, or recreate any StudentICTDatabase-derived artifact** —
  not the FE/BE `.accdb`, not the `.lnk`, not any harvested `raw/`/`kb/`/`ir/`/
  `manifest.json`/parse report. StudentICTDatabase is proprietary and **local-only**
  (FE/BE/`samples/*.lnk` gitignored; confirm `git check-ignore` before any `git add`).
  Its linked-table `Connect` strings embed a **plaintext password and an absolute local
  path** — committing output leaks a live credential. Validate against it **locally**
  only; never reproduce the literal password/path in any committed artifact (DW
  entries, evidence docs, activity-log, change-log) — placeholders only. See
  `docs/planning/deferred-work.md` DW-24 and the `feedback_redact_corpus_credentials_from_all_committed_artifacts` lesson.
- **Golden regeneration remains HomeHealthCare-only** (`GoldenCorpusRegenerator`,
  `AH_REGEN_GOLDEN=1`). **Under Option A this set IS golden-affecting** — every HHC
  source-location column shifts `+1`; regenerate the HHC golden in the **same commit**
  as the fix and prove the delta is *exactly* the intended column shift (no other
  bytes move), per the "regenerate every committed golden the same set that changes
  their output, and prove deltas at the byte/codepoint level" Convention. **Under
  Option B the set is expected HHC golden-neutral** — prove that byte-level (a fresh
  run reproduces the committed golden) rather than asserting it.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on, TRX
  `<Counters>` as the completion signal; never background a `dotnet` invocation.
  `git checkout -- samples/HomeHealthCare.accdb` before staging if any HHC COM test ran.
- **The quarantined sanitize test stays quarantined.**
- **Scope is the source-location COLUMN base only.** Do **not** touch line numbers
  (already correct), and do **not** fold in other DW items (DW-25 IsLinked, DW-18
  constants, etc.). If the fix appears to need broader change, stop and record/confirm
  the relevant DW.
- **Do not weaken assertions or tests to force green.** The Set 017 DW-26
  known-limitation pin must be **flipped to assert validity** as part of the fix — not
  deleted or softened.

---

## Sessions

### Session 1 of 1: Reconcile the source-location column base and close DW-26

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `deferred-work.md` DW-26, `feedback_use_northwind_as_local_test_corpus`,
   `feedback_redact_corpus_credentials_from_all_committed_artifacts`) and **re-ground**:
   re-confirm on the current tree that `SourceLocation` columns are 0-based and lines
   1-based; that `ParserOutputProjector.BuildLocation` projects the raw column and the
   top-level is hardcoded `StartColumn = 1`; and that the StudentICT manifest is
   schema-invalid solely on the `StartColumn` `[minimum]` class (re-run the **local**
   StudentICT harvest, foreground COM, output discarded).
2. **Decide the convention via decision-time cross-provider consensus + human
   sign-off** (Option A normalise-to-1-based vs Option B schema-0-based). Record the
   consensus journal (`s1-column-base-consensus-*.md`) and the human decision before
   any code change. Default lean: **Option A** (uniform 1-based `line:col`, matches
   lines + schema + hardcoded top-level + editor convention) unless consensus surfaces
   a stronger Option-B case.
3. **Implement the chosen option:**
   - *Option A:* `+1` the column in `ParserOutputProjector.BuildLocation` (and any
     sibling column-emitting projection site); leave the hardcoded top-level `=1` as-is
     (now consistent). **Regenerate the HHC golden in the same commit** and prove the
     ONLY delta is the intended `+1` column shift (byte/codepoint-level diff).
   - *Option B:* set the schema `SourceLocation` column minima to `0` (+ the flat
     shorthand sibling if applicable); reconcile the hardcoded top-level `StartColumn`
     to the true 0-based root; document the 0-based column base in the schema and the
     interpretation guide. Prove HHC golden byte-neutral.
4. **Flip the Set 017 known-limitation pin:** update
   `StudentIctLocalInvariantTests` so it asserts the manifest **schema-validates**
   (like `NorthwindLocalInvariantTests`), removing the `Assert.False(IsValid)` /
   `StartColumn`-`[minimum]` / `HasZeroStartColumn` known-limitation block. Add a
   hermetic pin proving a **line-leading reference/call** projects the intended column
   base (a synthetic 1-line module whose first token is at the line start). Validate
   the StudentICT manifest now schema-validates **locally**.
5. **Close DW-26** in `docs/planning/deferred-work.md` (move to Closed with the
   commit/set + date; retain the entry). Update the interpretation guide if the column
   base is now documented (especially under Option B).
6. Cross-provider verification (routed `session-verification`, prose-first + trailing
   JSON) + in-IDE devil's-advocate pair as **primary** on the convention choice and the
   golden delta (guard against an unintended non-column golden byte move under Option A,
   and against a mixed-base / consumer-breaking contract under Option B).
7. **Author the next session set** (last-session-authors-next-set) via routed
   recommendation against actual post-018 state, carrying the Hard Constraints — or a
   recorded consumer-driven-only handoff (standing bundles incl. DW-18 golden-affecting,
   DW-25, ribbon-completeness DW-20/21/22/23, parser DW-1/2/4, DW-8+DW-19, DW-14, DW-17,
   plus DW-24).
8. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push, notify;
   Step 9 reorganization review post-notify.

**Progress keys:** `s1-reground`, `s1-column-base-decision`, `s1-implement`,
`s1-golden`, `s1-flip-studentict-pin`, `s1-verified`, `s1-next-set-authored`,
`set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full.
- **Scope is the source-location column base only.** If the work appears to need a
  broader producer change (line numbering, other DW items), stop at the boundary,
  record/confirm the `DW-<n>` entry, and leave it to the owning set.
- **Every golden delta must be explained by the fix.** Under Option A the ONLY HHC
  golden delta is the `+1` source-location column shift; any other byte move is a
  stop-and-investigate signal. Under Option B the set is HHC golden-neutral.
- **Never commit a StudentICTDatabase artifact** (FE/BE/`.lnk`/derived output) and
  never reproduce its literal credential/path in a committed artifact.
- **Do not weaken assertions or tests to force green** — the Set 017 DW-26 pin is
  *flipped to assert validity*, not deleted/softened, and the new line-leading pin must
  genuinely exercise a column-0-in-AST token.

## End-Of-Set Deliverables

- The source-location column base reconciled (Option A normalise-to-1-based, or
  Option B schema-0-based) so a real split/rich-VBA manifest schema-validates;
  decision recorded via cross-provider consensus + human sign-off.
- HHC golden handled per the chosen option (Option A: regenerated in-commit, delta
  proven to be only the column shift; Option B: proven byte-neutral).
- `StudentIctLocalInvariantTests` flipped to assert full schema validity; a hermetic
  line-leading-column pin added; StudentICT manifest validates locally.
- **DW-26 closed** in `deferred-work.md` (with commit/set + date).
- Next session set authored (spec + not-started v4 stub) or a recorded
  consumer-driven-only handoff.
- Standard set artifacts: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `s1-column-base-consensus-*.md`,
  `s1-verification.md`, `s1-issues.json` for findings-bearing rounds,
  `disposition.json`, `change-log.md`, `next-session-set-recommendation.md`.
