# Unresolved-Items Completeness Spec

> **Purpose:** Close **DW-11** — `unresolved-items.json` is procedure-body-scoped
> and **undercounts** unresolved references in two distinct layers, so the index
> is not the complete superset a consumer expects. Both layers were surfaced (with
> proven reproducers) by the `011` Session 3 devil's-advocate review and recorded
> in `docs/planning/deferred-work.md` → DW-11. This is **proactive hardening of a
> known aggregation bug** (proven reproducers), authored under the consumer-driven-
> only posture after the 006 review arc completed (set 014).
> **Created:** 2026-06-15 (authored by 014 Session 1 per the
> last-session-authors-next-set pattern; routed recommendation in
> `docs/session-sets/014-cleanup-zero-date-guard/next-session-set-recommendation.md`)
> **Session Set:** `docs/session-sets/015-unresolved-items-completeness/`
> **Prerequisite:** **P0 only** (a committed green build/test baseline). Independent
> of any other open set; slots anywhere.
> **Source of truth:** `docs/planning/deferred-work.md` → DW-11 (the two proven
> reproducers), the `011` Session 3 closeout review (concern C9), and the current
> tree (the DW-11 file/line pointers are dated 2026-06-14; sets 012–014 have landed
> since — re-confirm every pointer before editing).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full tier;
> no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 2
prerequisites: []
verificationMode: out-of-band-or-none
```

> Rationale: no UI surface; the quality gate is build + tests + cross-provider
> verification. **Session 1 (Layer A)** is a lean, golden-neutral aggregator fix
> (include procedure-header unresolved refs without double-counting) — hermetically
> pinnable. **Session 2 (Layer B)** is materially larger and higher-risk: it adds a
> manifest home for module-level unresolved references (a schema change) plus the
> projector change to stop dropping them, with coordinated golden regen — so it is
> split out per the "split sessions whose risk/scope differ" lesson.
> `verificationMode: out-of-band-or-none` matches the inline-verify Full-tier
> pattern (007–014): the cross-provider `session-verification` runs inside each
> work session; register with `--verification-mode out-of-band-or-none`.

---

## Project Overview

`unresolved-items.json` is the manifest index a consumer reads to enumerate every
VBA reference the resolver could **not** bind. DW-11 proved it undercounts in two
layers:

1. **Layer A — aggregator drops procedure-HEADER refs (index-only gap).** The VBA
   half of `unresolved-items.json` is built from
   `FunctionClassification.UnresolvedReferences` via
   `UnresolvedItemAggregator.Build(navigationGraph, functionClassifications)`
   (`AccessHarvester.Core/Pipeline.cs` ~655). `FunctionClassificationBuilder` walks
   only procedure **bodies** (`procedureAst.Body`), so an unresolved reference in a
   procedure **header** (an optional parameter's default expression) is **absent
   from `unresolved-items.json`** — even though it **is** present inline on
   `ParserOutput.Body.References[]` (the projector groups by procedure *scope*,
   header included). The index is therefore **not a structural superset** of the
   inline `Body.References[ResolutionStatus="Unresolved"]`.
   - **Proven reproducer (GPT-5.4, 011 S3):**
     `Public Sub Demo(Optional ByVal x As Variant = MissingThing)` (trivial body)
     projects **one** unresolved reference (`MissingThing`, `not-in-scope`) into
     `Body.References`, while `FunctionClassification.UnresolvedReferences` is **0**
     and `UnresolvedItemAggregator.Build(...)` returns **0**.
2. **Layer B — projection drops MODULE-LEVEL refs (no manifest signal at all).**
   `ParserOutputProjector.GroupReferencesByProcedure` keeps only references with an
   enclosing `ProcedureScope` and explicitly drops module-level references ("the
   v1.0 schema has no module-level item type",
   `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs` ~65-67). So a
   module-level unresolved reference (`Const`/`Dim` initializer,
   `Implements <unresolved>`, module `Private x As New <unresolved>`) appears in
   **neither** `ParserOutput.Body.References[]` **nor** `unresolved-items.json` —
   there is no complete manifest signal for it. Fixing Layer B needs a **manifest
   home** for module-level references (a schema addition) plus the projector change.
   - **Proven reproducer (GPT-5.4, 011 S3):**
     `Public Const DemoConst = MissingThing` (plus a trivial procedure) yields one
     unresolved reference on the raw `ResolvedModule`, but
     `ParserOutput.Body.References[]` is **0** and the aggregator is **0**.

### Design constraints carried from DW-11 (do not regress)

- **Layer A must not re-introduce double-counting.** The aggregator was
  *deliberately* fed only `functionClassifications` (not `resolverDiagnostics`) to
  avoid double-emitting references both sources carry. The fix must aggregate the
  **full procedure-scoped reference set the `ParserOutputProjector` already uses**
  (body + header) **without** re-introducing that double-count. Verify the dedup
  story explicitly.
- **Layer B is an envelope/schema change.** Per the "Additive Field On An
  `additionalProperties`-Tolerant Shared `$def`" lesson, first determine the
  *smallest correct* schema change: does a module-level reference need a brand-new
  manifest item type (envelope structural change → full version bump + golden
  churn), or can it ride an existing `additionalProperties`-tolerant `$def` /
  sub-document version? Decide explicitly and record it before editing the schema.

---

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json` or any
  NorthwindDevEdition-derived artifact** — regeneration reintroduces the residue
  sets `003`–`005` removed. All fixtures and goldens derive from committed
  HomeHealthCare artifacts only. NorthwindDevEdition is proprietary and local-only.
- **Golden regeneration is HomeHealthCare-only** and follows the established
  regeneration path (`samples/GoldenOutput/HomeHealthCare/`); regenerate once per
  session after that session's behavioral changes are complete, and re-run
  `CanonicalGoldenRegressionTests`. If a session asserts "no regen needed," prove
  it **byte-level** (a fresh run reproduces the committed golden), never by
  assertion. **Layer A is expected golden-neutral on HHC** (DW-11: zero header/
  module-level unresolved cases in the corpus) — prove it byte-level. **Layer B's
  schema change is likely golden-affecting** (the `unresolved-items.json` shape
  and/or `ParserOutput` gain a module-level slot) — that is a coordinated HHC regen
  this session; explain every delta by the change.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on, TRX
  `<Counters>` as the completion signal; never background a `dotnet` invocation;
  sweep spurious `samples/*.accdb` diffs (`git checkout -- samples/HomeHealthCare.accdb`)
  before staging any commit. (Most of this set is hermetic — parser/projector/
  aggregator — but golden regen and the integration suite touch the COM tier.)
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`.
- **Do not weaken assertions or tests to force green.** New behaviour (a reference
  that now appears in `unresolved-items.json` where it previously did not) must be
  pinned by a hermetic test asserting the new behaviour, not silently accepted. The
  index-is-a-superset property must be pinned, not assumed.

### Set-specific constraint — completeness without double-counting

- **`unresolved-items.json` must become a structural superset of the inline
  `Body.References[ResolutionStatus="Unresolved"]`** for procedure-scoped refs
  (Layer A), and must additionally surface module-level unresolved refs through
  whatever manifest home Layer B adds — **with no reference emitted twice**.
- **Both proven reproducers become hermetic pins** (the owning session's first
  task per DW-11's mechanical-backstop note): Layer A
  (`Public Sub Demo(Optional ByVal x As Variant = MissingThing)`) asserting the
  reference appears in **both** `Body.References` and the aggregated
  `UnresolvedItems`; Layer B (`Public Const DemoConst = MissingThing`) asserting the
  module-level reference reaches the manifest home the fix gives it.

**Non-goals:** the other open DW items (DW-1..DW-8, DW-12, DW-13, DW-14, DW-16) and
any new extraction/parser capability. Anything discovered beyond the two DW-11
layers that no set owns → a `DW-<n>` entry, not scope creep.

---

## Sessions

### Session 1 of 2: Layer A — procedure-header unresolved references reach the index

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `deferred-work.md` → DW-11) and **re-ground**: confirm the aggregator call site
   (`Pipeline.cs` ~655), that `FunctionClassificationBuilder` walks only
   `procedureAst.Body`, and that `ParserOutputProjector` already groups
   header-scoped references into `Body.References`. Encode the Layer A reproducer as
   a **failing** hermetic pin first (red), to prove the gap on the current tree.
2. Fix Layer A: aggregate the **full procedure-scoped reference set** (body +
   header) the projector already uses, **without** re-introducing the double-count
   the original `functionClassifications`-only feed avoided. Decide explicitly
   whether the cleanest seam is the aggregator consuming the projector's
   procedure-scoped reference set, or `FunctionClassificationBuilder` including
   header refs — record the decision and the dedup argument.
3. Pin: the Layer A reproducer now appears in **both** `Body.References` and the
   aggregated `UnresolvedItems`; add a **no-double-count** pin (a reference both
   sources carry is emitted exactly once); add a **superset** pin (every
   procedure-scoped `Unresolved` inline reference is present in the index).
4. Hermetic validation of the affected projector/aggregator/pipeline projects;
   **assess golden / `unresolved-items.json` impact** — expected neutral on HHC
   (prove byte-level), else regen HHC and explain every delta by the fix.
5. Cross-provider verification (prose-first lead + trailing JSON block; embed the
   reproducer evidence; run the in-IDE devil's-advocate pair as primary on the
   fresh code — focus the contrarian hypothesis on the dedup/double-count seam).
6. Log every step; commit + push.

**Progress keys:** `s1-reground-and-red-pin`, `s1-layer-a-fix`,
`s1-superset-and-dedup-pins`, `s1-golden-or-no-regen`, `s1-verified`

---

### Session 2 of 2: Layer B — module-level unresolved references get a manifest home

**Steps:**

1. Pre-session reading + **re-ground** Layer B: confirm
   `ParserOutputProjector.GroupReferencesByProcedure` still drops module-level
   references (~65-67) and the v1.0 schema still has no module-level item type.
   Encode the Layer B reproducer as a **failing** hermetic pin first (red).
2. **Decide the schema change explicitly** (record it): the smallest correct
   manifest home for a module-level unresolved reference. Apply the "bump the
   sub-document version, not the envelope, when the host `$def` is
   `additionalProperties`-tolerant" lesson — only do a full envelope bump if a
   strict (`additionalProperties: false`) def must admit the new shape or the
   envelope's required/structural shape changes.
3. Fix Layer B: stop dropping module-level references at projection; route them to
   the new manifest home; ensure the aggregator surfaces them in
   `unresolved-items.json` — **without** double-counting any procedure-scoped
   reference Layer A already covers.
4. Pin: the Layer B reproducer reaches the manifest home and the index; the
   superset property now holds for module-level refs too.
5. **Coordinated HHC golden regen** (the schema/shape change is likely
   golden-affecting): regenerate HomeHealthCare only, re-run
   `CanonicalGoldenRegressionTests`, and explain every delta by the change; if a
   session asserts no-regen, prove it byte-level.
6. Cross-provider verification (closeout-grade: prose-first lead + trailing JSON
   block; **embed the Session-1 Layer A code + test snippet** per the cross-session-
   evidence convention since this is the final session; run the in-IDE devil's-
   advocate pair as primary on the fresh code).
7. **Author the next session set** (last-session-authors-next-set) via routed
   recommendation against actual post-015 state, carrying the Hard Constraints
   block verbatim — or a recorded consumer-driven-only handoff if no DW trigger has
   arrived (DW-11 closes here; the standing DW-bundling candidates from 014's
   recommendation remain).
8. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify. **Discharge DW-11** in
   `deferred-work.md` (move to Closed with this set/commit, retain the entry).

**Progress keys:** `s2-reground-and-red-pin`, `s2-schema-decision`,
`s2-layer-b-fix`, `s2-golden-regen`, `s2-next-set-authored`, `s2-verified`,
`set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is DW-11 only** (Layer A procedure-header undercount + Layer B
  module-level undercount). If a fix appears to require new parser capability or a
  different DW item, stop at the boundary, record a `DW-<n>` entry, and leave it to
  the owning set.
- **No double-counting.** A reference must never be emitted twice across the inline
  references, the procedure-scoped index, and the module-level home.
- **The index must become a structural superset of the inline unresolved
  references** — pin it, do not assume it.
- **Every golden / `unresolved-items.json` delta must be explained by the fix.** An
  unexplained delta is a stop-and-investigate signal; a "no regen needed" claim
  must be proven byte-level.
- **Do not weaken assertions or tests to force green.**

## End-Of-Set Deliverables

- Layer A: `unresolved-items.json` includes procedure-header unresolved references;
  hermetically pinned (reproducer + no-double-count + superset).
- Layer B: a manifest home for module-level unresolved references (with the schema
  decision recorded), projector no longer drops them, aggregator surfaces them;
  hermetically pinned; coordinated HHC golden regen with every delta explained.
- DW-11 discharged in `deferred-work.md` (moved to Closed, entry retained, cites
  this set + commit).
- Next session set authored (spec + not-started v4 state stub) or a recorded
  consumer-driven-only handoff if no DW trigger has arrived.
- Standard set artifacts per session: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md`, `sN-issues.json` for
  findings-bearing rounds, `disposition.json` (final session), `change-log.md`
  (final session), `next-session-set-recommendation.md` (final session).
