# Resolver And Navigation Wiring Spec

> **Purpose:** Deliver the differentiating capabilities the harvester already
> **built and hermetically tested** but never wired into production output — the
> Access-integrated VBA resolver (cross-module + registry + default-member +
> type-inference + dynamic-SQL) and the Phase 6 enrichment substrate (navigation
> graph, unresolved-items aggregation, event-handler / validation
> classifications). Today every one of these resolves only intra-module,
> registry-less references in the manifest path; the navigation graph and
> `unresolved-items.json` never reach output. This is **006's P3** — the proven
> root cause of the Round 2 consumer feedback (R2-1 switchboard navigation edges,
> R2-2 dialog→query→report chains).
> **Created:** 2026-06-13 (authored by 010 Session 2 per the
> last-session-authors-next-set pattern)
> **Session Set:** `docs/session-sets/011-resolver-and-navigation-wiring/`
> **Prerequisite:** P0 (committed green build/test baseline). Sequence after
> **P1a (008, complete — F-3-1 form-module items now produced)**, P1b (009,
> complete), and **P2 (010, complete)**. F-3-1 being done is load-bearing: the
> 006 finding warns "fix F-3-1 without this wiring and you surface a wall of
> Unresolved where the tested capability would have resolved controls, Me, and
> cross-module calls" — so this set ships the resolution the 008 items now need.
> **This is 006's P3.** Source of truth: the 006 review findings
> (`s2/s3-review-findings.md` F-3-4, F-2-2) and `review-recommendations.md` §3 P3.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 3
prerequisites: []
```

> Rationale: no UI surface; the quality gate is build + tests + cross-provider
> verification. This set **changes shipped output substantially** — forms/reports
> gain resolved references where the registry/default-member/cross-module
> capability fires, `manifest.json` gains a populated `NavigationGraph`,
> `unresolved-items.json` stops being permanently empty, and event-handler /
> validation records gain classification content. It therefore **regenerates the
> HomeHealthCare golden**, once per session after that session's behavioral
> changes are complete, per the established regeneration discipline. The COM tier
> is exercised deliberately for those regens + the
> `CanonicalGoldenRegressionTests` confirmation.

---

## Project Overview

What this set delivers, in consumer terms — the findings are **proven** in the
006 record (call-site greps + golden probes); this set implements the recorded
wiring recommendations, it does not re-litigate the diagnoses. Read each
finding's full Evidence/Recommendation block in the cited 006 file before
touching it, and **re-ground every claim against the current tree** (the 006
probes are dated 2026-06-12 and Sets 008–010 have since moved the code).

### Major (resolver wiring)

1. **F-3-4 — the resolver's differentiating capabilities are unwired.**
   `Pipeline.cs` constructs a **fresh per-module** `ProjectScope` and a
   registry-less, `owningClass`-less `VbaReferenceResolver` inside the parse
   loop, so cross-module Public lookup, bare-control and `Me`-member lookup, and
   the `DefaultMemberRegistry` consultation **can never fire**; `SqlStrings` is
   hardcoded empty. The correct wiring already exists in-repo
   (`SymbolDbBuilder` shares one `ProjectScope` across all modules). Share one
   `ProjectScope` across the manifest parse loop; build
   `AccessObjectModelRegistry` from the already-extracted form/report metadata
   and pass `owningClass` for Form/Report modules; consult
   `DefaultMemberRegistry` in `DecideMemberOutcome`; wire `DynamicSqlAnalyzer`
   output into `SqlStrings` (or omit the key until wired, per omission
   semantics). Source: `s3-review-findings.md` F-3-4.

### Major (enrichment / navigation wiring)

2. **F-2-2 — the Phase 6 enrichment substrate has zero production call sites.**
   `NavigationGraphBuilder`, `UnresolvedItemAggregator`,
   `EnrichmentPipelineRunner`, `EventHandlerClassifier`,
   `ValidationRuleExtractor`, and `RowSourceSpecBuilder` are referenced only by
   their own projects and tests; `manifest.json` has no `NavigationGraph`,
   `unresolved-items.json` is `[]`, and event-handler / validation records carry
   references but no classification/rule content. The seam already exists
   (`Pipeline.cs` builds `resolvedModules` + `crossRefs` for
   `FunctionClassificationBuilder` — the same inputs `NavigationGraphBuilder` and
   `UnresolvedItemAggregator` take). R2-1 additionally needs a `Switchboard
   Items` table reader to populate `NavigationGraphInputs.Switchboard` (rows are
   in the harvested table data; **no new COM surface**). This is the natural
   vehicle for the **R2-1 / R2-2** consumer requests. Source:
   `s2-review-findings.md` F-2-2; `006/next-session-set-recommendation.md`
   R2-1/R2-2 seam notes.

**Non-goals:** P4 edge derivation beyond R2-1/R2-2, P5/P6 CLI robustness, the
DW-6 encoding-architecture transcode option, the DW-1/DW-2/DW-4 grammar
completeness items. Anything discovered beyond F-3-4 / F-2-2 that no set owns →
a `DW-<n>` entry, not scope creep.

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
  and re-run `CanonicalGoldenRegressionTests` to confirm. *(Each golden delta
  must be explained by a specific finding's wiring — an unexplained delta is a
  stop-and-investigate signal. Wiring resolution will move many `Unresolved`
  references to resolved and add `NavigationGraph` / `unresolved-items` content;
  every such delta must map to F-3-4 or F-2-2.)*
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on,
  TRX `<Counters>` as the completion signal; never background a `dotnet`
  invocation; sweep spurious `samples/*.accdb` diffs
  (`git checkout -- samples/HomeHealthCare.accdb`) before staging any commit.
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`;
  its re-enable belongs to the proposed sanitize-testhost-wedge
  investigation, not to this set.
- **Do not weaken assertions or tests to force green.** Intent changes
  require explicit disclosure in the verification prompt. In particular, the
  hermetic `EndToEndFixtureTests` that today exercise the *unwired* substrate
  must be reconciled with the now-wired production path, not deleted.

---

## Sessions

### Session 1 of 3: Resolver Wiring (F-3-4)

Share one `ProjectScope`, build and pass the `AccessObjectModelRegistry` +
`owningClass`, consult the `DefaultMemberRegistry`, and wire (or omit)
`SqlStrings`.

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `deferred-work.md`, the 006 F-3-4 finding) and **re-ground** every call-site
   claim against the current tree (Sets 008–010 moved `Pipeline.cs`).
2. Share one `ProjectScope` across the manifest parse loop (the `SymbolDbBuilder`
   pattern); prove cross-module Public lookup now fires with a hermetic pin.
3. Build `AccessObjectModelRegistry` from the already-extracted form/report
   metadata; pass `owningClass` for Form/Report modules so bare-control and
   `Me`-member lookup can fire.
4. Consult `DefaultMemberRegistry` in `DecideMemberOutcome` (retire the hardcoded
   `default-member-not-yet-resolved` default arm where the registry resolves).
5. Wire `DynamicSqlAnalyzer` output into `SqlStrings`, **or** omit the key until
   wired per omission semantics — decide explicitly and record it.
6. Hermetic tests pinning each newly-fired capability. Then the **single
   coordinated HomeHealthCare golden regen** for this session's resolution
   deltas + the COM `CanonicalGoldenRegressionTests` confirmation; every delta
   maps to F-3-4.
7. Cross-provider verification; closeout.

**Progress keys:** `s1-shared-projectscope`, `s1-registry-wired`,
`s1-default-members-consulted`, `s1-sqlstrings-decided`, `s1-golden-regen`,
`s1-verified`

### Session 2 of 3: Navigation & Enrichment Wiring (F-2-2)

Wire the Phase 6 substrate into the production pipeline; add the `Switchboard
Items` table reader; deliver R2-1 / R2-2.

**Steps:**

1. Pre-session reading; re-ground the F-2-2 call-site claims against the tree.
2. Wire `NavigationGraphBuilder` + `UnresolvedItemAggregator` into the pipeline
   off the existing `resolvedModules` + `crossRefs` seam; pass `navigationGraph`
   to `DatabaseManifestAggregator.Build`; populate `unresolved-items.json` and
   **remove the 010 interim "always empty" note** from interpretation-guide §6.
3. Wire `EventHandlerClassifier` / `ValidationRuleExtractor` / `RowSourceSpecBuilder`
   so event-handler / validation records carry classification / rule content.
4. Add the `Switchboard Items` table reader to populate
   `NavigationGraphInputs.Switchboard` (R2-1) — harvested table data, no new COM
   surface; derive the dialog→query→report chain edges (R2-2) from the existing
   `NavigationEdge.WhereCondition` / `OpenArgs` + `IrQuery.Parameters` surfaces.
5. Reconcile the hermetic `EndToEndFixtureTests` (which exercised the unwired
   substrate) with the now-wired production path — do not delete coverage.
6. Hermetic validation; **single coordinated golden regen** + COM confirmation;
   every delta maps to F-2-2 (NavigationGraph appears, unresolved-items
   populates, classification content lands).
7. Cross-provider verification; closeout.

**Progress keys:** `s2-navgraph-wired`, `s2-unresolved-items-populated`,
`s2-classifications-wired`, `s2-switchboard-reader`, `s2-r2-chains`,
`s2-golden-regen`, `s2-verified`

### Session 3 of 3: R2-1/R2-2 Consumer Validation + Docs + Coordinated Closeout

Validate the delivered navigation surfaces against the R2-1/R2-2 consumer
requests, finish the interpretation-guide updates, and author the next set.

**Steps:**

1. Pre-session reading; ground the R2-1/R2-2 consumer reports against a **fresh
   tool run** (per the verify-consumer-reports-against-fresh-output lesson) — not
   stale goldens/docs.
2. Confirm the switchboard navigation edges (R2-1) and dialog→query→report chains
   (R2-2) are present and correct in the regenerated golden; document the
   navigation-graph consumer surface in the interpretation guide.
3. Any residual enrichment gaps → fix or defer with a `DW-<n>` entry.
4. Final hermetic validation; **single coordinated golden regen** (if step 2/3
   changed output) + COM confirmation; every delta explained.
5. Cross-provider verification (closeout-grade: prose-first lead + trailing JSON
   block; embed earlier-session evidence per the cross-session-evidence lesson).
6. **Author the next session set** (last-session-authors-next-set): per the 006
   ranking, the natural next set is the P4/P5/P6 remainder (edge derivation
   beyond R2-1/R2-2, CLI robustness) — author against actual post-011 completion
   state, carrying the Hard Constraints block verbatim.
7. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify.

**Progress keys:** `s3-r2-validated`, `s3-navgraph-documented`,
`s3-next-set-authored`, `s3-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is P3 only.** If a fix appears to require P4 edge derivation beyond
  R2-1/R2-2, P5/P6 CLI robustness, or the DW-6 encoding-architecture transcode,
  stop at the boundary, record a `DW-<n>` entry, and leave it to the owning set.
- **Every golden delta must be explained by F-3-4 or F-2-2 wiring.** An
  unexplained delta in the regenerated HomeHealthCare golden is a
  stop-and-investigate signal, not a silent regen. (This set is the largest
  golden mover of the 006 arc — resolution flips many `Unresolved` references and
  introduces whole new manifest surfaces; budget the per-delta explanation work.)
- **Do not weaken assertions or tests to force green.** Reconcile the substrate's
  hermetic end-to-end tests with the wired path; do not delete them.

## End-Of-Set Deliverables

- F-3-4: one shared `ProjectScope` across the manifest parse loop;
  `AccessObjectModelRegistry` + `owningClass` wired for Form/Report modules;
  `DefaultMemberRegistry` consulted; `SqlStrings` wired or omitted (decision
  recorded). Cross-module / control / `Me` / default-member resolution fires in
  production.
- F-2-2: `NavigationGraph` populated in `manifest.json`; `unresolved-items.json`
  populated (010 interim note removed); event-handler / validation /
  row-source classification content delivered; `Switchboard Items` reader (R2-1);
  dialog→query→report chains (R2-2).
- Single coordinated HomeHealthCare golden regeneration per session; every delta
  explained; `CanonicalGoldenRegressionTests` green.
- Next session set authored (spec + not-started v4 state stub).
- Standard set artifacts per session: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md`, `sN-issues.json` for
  findings-bearing rounds, `disposition.json`, `change-log.md`,
  `next-session-set-recommendation.md`.
