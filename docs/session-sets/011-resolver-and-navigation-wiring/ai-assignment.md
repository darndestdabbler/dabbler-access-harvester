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
