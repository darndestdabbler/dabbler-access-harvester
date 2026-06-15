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
