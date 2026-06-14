# AI Assignment — 011-resolver-and-navigation-wiring

Per-session ledger of the cheapest capable orchestrator + routing decisions.
Next-session recommendations are authored via routed analysis
(`route(task_type="analysis")`), never self-opined.

---

## Session 1: Resolver Wiring (F-3-4)

### Recommended orchestrator
Claude Code Opus 4.8 @ effort=medium

### Rationale
Synthesis-heavy: a closed-type design decision (how to surface default-member
resolution without breaking `ResolutionOutcome`), a two-pass restructure of the
shared parse loop, and per-delta attribution of the largest golden move of the
006 arc. Opus's judgment earns its cost here; the mechanical parts (registry
construction, projector plumbing) ride along.

### Estimated routed cost
Moderate (one design-consensus pair + cross-provider verification).

| Step | Action | Routing Decision |
|------|--------|------------------|
| Re-ground F-3-4 vs current tree | Read Pipeline/resolver/projector; measure corpus impact | Orchestrator direct (mechanical read) |
| Items 1,2 (shared ProjectScope + registry/owningClass) | Implement two-pass + registry build | Orchestrator direct (implementation) |
| Item 3 design (DefaultMemberRegistry outcome shape) | Closed-type breaking-change vs reuse decision | **Routed decision consensus** → gpt-5-4 + gemini-pro (both chose Option B) |
| Item 3 implement + item 4 (SqlStrings omit) | Synthetic decl via ResolvedToObjectModel; omit key | Orchestrator direct |
| Hermetic pins + golden regen | 9 new pins + COM regen + canonical gate | Orchestrator direct (COM babysat) |
| End-of-session verification | Independent review | **Routed** → gpt-5-4 (cross-provider, mandatory) |

### Actuals (filled after the session)
- Orchestrator used: Claude Code Opus 4.8 @ effort=medium
- Total routed cost: ~$0.32 (item-3 design consensus $0.084; verification R1 $0.208 + R2 $0.031; ai-assignment analysis $0.001)
- Deviations from recommendation: none (matched the recommended orchestrator).
- Notes for next-session calibration: the item-3 closed-type decision genuinely
  benefited from routed consensus before coding — both providers independently
  converged on Option B, which de-risked a ResolutionOutcome breaking change.
  Verification R1 surfaced two findings (one Major, one Minor); the Major was a
  reachability question (resolved by evidence, no code change) and the Minor by a
  hermetic pin — R2 VERIFIED. Budget a verification round or two for the F-2-2
  wiring session too (substrate-vs-wired reconciliation invites verifier scrutiny).

**Next-session orchestrator recommendation (Session 2 — F-2-2 navigation & enrichment wiring):**
Mid-tier @ effort=medium (routed analysis: gemini-pro recommended a Gemini-Pro-class
orchestrator — Session 2 is known-pattern integration wiring + a new table reader +
test reconciliation, a lower synthesis ceiling than Session 1's novel closed-type
design, so an Opus-class premium is unnecessary).
Rationale: known-pattern substrate wiring off the existing `resolvedModules` +
`crossRefs` seam; the moderate design surface (R2-2 edge derivation) is bounded.
**Calibration caveat:** the `session-verification` router override is calibrated for
a Claude orchestrator (→ gpt-5-4). If Session 2 runs under Gemini, swap the override
to a non-Google verifier (opus or gpt-5-4) before Step 6, per the verifier-selection
lesson; Sonnet @ medium is a Claude-side alternative that keeps the existing
gpt-5-4 override valid.

---

## Session 2: Navigation & Enrichment Wiring (F-2-2)

### Recommended orchestrator
Mid-tier @ effort=medium (Sonnet 4.6 / Gemini-Pro-class), per Session 1's routed
next-session analysis.

### Orchestrator actually used
Claude Code Opus 4.8 @ effort=medium — **deviation from the mid-tier
recommendation**, see Actuals. (Opus is strictly more capable than the
recommended Sonnet and keeps the gpt-5-4 cross-provider verifier override valid;
the re-grounding surfaced a material scope decision that benefited from
Opus-level judgment plus routed cross-provider consensus.)

### Rationale
F-2-2 is wiring the already-built-but-unwired Phase 6 enrichment substrate
(`NavigationGraphBuilder`, `UnresolvedItemAggregator`, `EnrichmentPipelineRunner`
+ `EventHandlerClassifier` / `ValidationRuleExtractor` / `RowSourceSpecBuilder`)
into the production manifest pipeline off the existing `resolvedModules` +
`crossRefs` seam. No new COM/data surface for the core wiring.

### Estimated routed cost
Moderate (one scope-decision consensus pair + cross-provider verification).

| Step | Action | Routing Decision |
|------|--------|------------------|
| Re-ground F-2-2 vs current tree | Map substrate call sites; probe switchboard data | Orchestrator direct (Explore fan-out) |
| **Scope decision** (R2-1 premise disproven) | switchboard rows NOT harvested → R2-1/R2-2 = P4 | **Routed decision consensus** → gpt-5-4 + gemini-pro (both Option A); human-approved |
| NavigationGraph + UnresolvedItems wiring | Pass `navigation` + aggregator to manifest | Orchestrator direct (implementation) |
| Enrichment classification content | `EnrichmentPipelineRunner` per form/report + table validation | Orchestrator direct |
| DW entries + interp-guide §6 note removal | Defer R2-1/R2-2 to P4; remove "always empty" note | Orchestrator direct |
| Hermetic pins + golden regen | Production-wiring pins + COM regen + canonical gate | Orchestrator direct (COM babysat) |
| End-of-session verification | Independent review | **Routed** → gpt-5-4 (cross-provider, mandatory) |

### Actuals (filled after the session)
- Orchestrator used: Claude Code Opus 4.8 @ effort=medium.
- Total routed cost: ~$0.155 — scope consensus (gpt-5-4 $0.0283 + gemini-pro
  $0.0038) + session-verification (gpt-5-4 $0.1208) + S3 orchestrator analysis
  (gemini-pro $0.0014).
- Deviations from recommendation: ran Opus (recommended mid-tier). The session's
  defining work turned out to be a scope correction (the spec's R2-1 "switchboard
  rows are harvested" premise is disproven by the tree), not the anticipated
  known-pattern wiring — so Option A (core wiring; defer R2-1/R2-2 to the
  006-ranked P4 set) was chosen by routed consensus + human sign-off. The core
  wiring itself was mechanical once the seam was understood; the Opus-level value
  was in the re-grounding + scope adjudication.
- Verification: R1 VERIFIED (gpt-5-4), empty issues, first round. The only
  (non-blocking) note — RowSourceSpecs lacked a dedicated hermetic pin — was closed
  post-verdict by adding `RowSourceSpecs_PopulatedForComboWithRowSource`.
- Notes for next-session calibration: the full `ManifestSchemaConformanceTests`
  gate caught a PRE-EXISTING S1 (F-3-4) bug — `ResolvedTarget` emitting
  `"Kind": null` for ObjectModel control targets — that S1's golden-compare-only
  validation missed. Lesson: run the full Integration suite (not just the golden
  gate) before closeout; a green golden does not imply a schema-valid manifest.

**Next-session orchestrator recommendation (Session 3 — closeout):**
Claude Sonnet 4.6 @ effort=medium (routed analysis: gemini-pro). Rationale:
R2-1/R2-2 were deferred to P4 in S2, so S3's actual scope is validation +
interpretation-guide documentation of the delivered navigation-graph surface +
authoring the P4 next-set + coordinated closeout — moderate synthesis, not novel
design. **S3 scope adjustment (carry forward):** the spec's S3 R2-1/R2-2
*validation* steps are replaced by validating the S2-delivered NavigationGraph /
unresolved-items / enrichment surfaces against a fresh run; the "next set" to
author is the P4 `switchboard-and-report-chain-edges` set (carrying DW-9/DW-10 +
the Hard Constraints verbatim). Sonnet-on-Claude keeps the gpt-5-4
session-verification override valid; if run under Gemini, swap the verifier
override to a non-Google model first.
