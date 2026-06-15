# AI Assignment — 015-unresolved-items-completeness

Full-tier set; inline cross-provider verification (`out-of-band-or-none`).
Orchestrator routes reasoning (verification, recommendations) per the
Delegation Discipline; implementation of an already-designed fix is the
orchestrator's execution.

## Session 1 — actuals

- **Orchestrator:** Claude Code (Opus 4.8 @ high), as registered.
- **Scope:** DW-11 **Layer A** — procedure-HEADER unresolved references reach
  `unresolved-items.json`.
- **Delivered:** a **dedup-additive feed**. `unresolved-items.json`'s VBA half is
  built from the procedure-BODY-only `FunctionClassification.UnresolvedReferences`
  feed, so an unresolved ref in a procedure HEADER (an `Optional` parameter's
  default expression) was dropped from the index even though the projector groups
  it into `Body.References`. The aggregator now ALSO consumes the full
  procedure-scoped resolver reference set (body + header) via the new
  `ParserOutputProjector.EnumerateProcedureScopedReferences` (the SAME set that
  builds `Body.References`, so the index is a superset by construction), filtered
  to (with-flattened) Unresolved, and suppresses the body overlap on the key
  `(module, procedure, line, column, reason, memberName)` with a **non-mutating**
  dedup. Net-new items are exactly the header refs (tagged
  `Source = FunctionClassification`). Files:
  `UnresolvedItemAggregator.cs`, `ParserOutputProjector.cs`, `Pipeline.cs`
  (one call site), `UnresolvedItemsCompletenessTests.cs`.
- **Validation:** 5 hermetic pins; full `AccessHarvester.Pipeline.Tests` **912/912**;
  full solution builds; COM golden gate `CanonicalGoldenRegressionTests` reproduces
  HHC byte-identical — Layer A **proven golden-neutral, no regen** (HHC has zero
  header/module-level unresolved cases).
- **Verification:** routed cross-provider gpt-5-4 — **R1 ISSUES_FOUND** (1 Major:
  the dedup loop's `bodyKeys.Add(...)` dedup'd the procedure-scoped feed against
  itself, dropping a distinct chained-header link `Foo.Bar` vs `Foo.Bar.Baz` that
  shares the 5-tuple key — DEMONSTRATED real, pin red pre-fix) → fixed
  (non-mutating `Contains` + `SurroundingText` added to the key) → **R2 VERIFIED**.
  Routed cost ≈ $0.30 (R1 $0.224 + R2 $0.075 + next-orch $0.002). A focused
  devil's-advocate prompt (`s1-devils-advocate-prompt.md`) was authored for the
  human-driven in-IDE GPT+Gemini pass.
- **Routing-decision note:** the dedup over-suppression Major was squarely on this
  set's theme (completeness without losing references) and was a *latent* fragility
  in the orchestrator's own first implementation that the routed adversarial pass
  surfaced with a concrete reproducer — exactly the case where routing earns its keep.
- **Scope discipline:** Layer B (module-level refs) untouched — still dropped at
  projection; Session 2 owns it.

## Session 2 — recommendation (routed; do-not-self-opine)

- **Routed recommendation (gemini-pro, cross-provider, $0.0018):** Claude Opus @
  high effort, multi-pass (R1–R3). Rationale: the central risk is the **coordinated
  golden regen** for Layer B's schema/shape change — every `unresolved-items.json`
  (and/or `ParserOutput`) delta must be precisely justified, which needs Opus's
  analytical-explanatory strength over pure code generation; the session interweaves
  schema design, implementation, verification, and forward-planning docs.
- **Resolved to the current model registry:** **Claude Code (Opus 4.8 @ high)** for
  Session 2.
- **Why higher-risk than S1:** Layer B is an envelope/schema change (a manifest home
  for module-level unresolved refs) with **likely golden-affecting** regen, plus the
  no-double-count constraint against Layer A's procedure-scoped refs, the
  cross-session evidence-embedding requirement (final session), DW-11 discharge, and
  authoring the next set / consumer-driven-only handoff. Apply the "bump the
  sub-document version, not the envelope, when the host `$def` is
  `additionalProperties`-tolerant" lesson before touching the schema.

## Session 2 — actuals

- **Orchestrator:** Claude Code (Opus 4.8 @ high), as registered.
- **Scope:** DW-11 **Layer B** — module-level unresolved references get a manifest home.
- **Schema decision (recorded in `s2-design-note.md`, routed critique
  `s2-schema-decision-critique.md` opus SOUND-WITH-CAVEATS):** the manifest home is the
  run-wide `unresolved-items.json` index; a module-level ref surfaces under a NEW
  `UnresolvedSource.ModuleLevelReference` with `ProcedureName=null`. Smallest correct
  change: the `UnresolvedItem` `$def` is `additionalProperties`-tolerant and we add no
  property — only **widen the `Source` enum** in the living v1.1 schema. NO envelope bump;
  NO `ParserOutput` change. The enum serialises as a string (`JsonStringEnumConverter`),
  so appending is safe.
- **Delivered:** new `ParserOutputProjector.EnumerateModuleLevelReferences` (the complement
  of `GroupReferencesByProcedure` — references with no enclosing `ProcedureScope`), new
  `UnresolvedItemAggregator.FromModuleLevelReferences` feed + `ModuleLevelReference` record,
  the enum value, the schema widening, and Pipeline.cs wiring. **No double-count**:
  module-level refs are disjoint from the procedure-scoped feeds by construction.
- **Golden — NOT neutral (DW-11's "zero cases" was wrong):** the gate caught 2 real HHC
  module-level refs (`vbObjectError` in `basDataOperations` Const decls). Coordinated HHC
  regen; **proven multiset delta: exactly +2 `ModuleLevelReference` items, 0 removed**;
  `UnresolvedItemCount` 1414→1416. Every other diff line is reordering from a determinism
  fix (below). kb/ir trees byte-unchanged.
- **Determinism hardening (forced by the regen + verification):** `SortDeterministically`'s
  key was not a total order (same-key items differing only by member name reshuffled under
  the non-stable introsort when the +2 items perturbed it). Fixed with a precomputed,
  **injective `CanonicalResidual`** (full-item JSON serialization) final tiebreaker;
  `SortDeterministically` exposed `internal` and pinned by a `[Theory]` over every
  folded-collision class.
- **Validation:** 3 Layer B completeness pins + 1 determinism `[Fact]` + 1 `[Theory]`
  (6 cases); full `AccessHarvester.Pipeline.Tests` **922/922**; golden gate + schema
  conformance **7/7** integration byte-identical; full solution builds.
- **Verification:** routed cross-provider gpt-5-4 — **R1 ISSUES** (Major: comparer not a
  total order) → fixed → **R2 ISSUES** (Major: residual not provably injective + Minor) →
  fixed (injective serialization + expanded pins) → **R3 ISSUES** (Minor: pin breadth) →
  fixed → **R4 VERIFIED**. Routed cost ≈ $0.40 (R1 $0.187 + R2 $0.099 + R3 $0.095 +
  R4 $0.017) + schema critique $0.21 + planning $0.003. In-IDE devil's-advocate prompt
  authored (`s2-devils-advocate-prompt.md`).
- **Forward:** DW-11 discharged; new **DW-17** (per-module `ParserOutput` asymmetry);
  next set = **consumer-driven-only handoff** (routed gemini-pro;
  `next-session-set-recommendation.md`).
- **Calibration note:** Opus @ high was the right pick — the session's value came from the
  golden-regen investigation (catching the 2 real cases + the latent non-total sort) and
  the multi-round adversarial determinism hardening, exactly the analytical-explanatory
  work the S1 recommendation anticipated.
