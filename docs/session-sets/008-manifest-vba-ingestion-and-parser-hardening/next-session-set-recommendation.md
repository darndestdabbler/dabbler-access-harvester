# Next Session Set Recommendation — after 008 (P1a)

> **Authored:** 2026-06-13, 008 Session 3 closeout. Backed by a routed analysis
> (`s3-next-set-analysis.md`, task_type=analysis) carrying the Hard Constraints
> block verbatim, per the *always-route-next-set-recommendations* convention.

## Recommendation: `009-output-encoding-correctness` (006 P1b)

**Authored and ready** (`docs/session-sets/009-output-encoding-correctness/`):
spec + not-started v4 state stub committed with this session.

- **Size:** 1 session. **Prereqs:** P0 only (independent of P1a/008).
- **Why next:** the routed analysis and the 006 ranking agree — F-1-1 (Major)
  is *irreversible, silent data corruption*, not a degradation: any production
  Access DB with non-ASCII captions/labels/validation has those bytes
  destroyed to U+FFFD in the kb/ and ir/ tiers the consumer reads, while the
  manifest path decodes correctly (so the same caption differs across output
  surfaces). Highest consumer value for the lowest cost (a two-line translator
  decode fix + hermetic cp1252 pins). It restores trust in the harvester's
  primary output before the finer-grained P2 fidelity work.
- **No golden churn:** the kb/ir goldens are ASCII-stable under the fix, so
  009 does not contend with any committed golden — it could even have run in
  parallel with 008.

### Orchestrator suggestion

The routed analysis returned `orchestrator: manual, effort: low`. Refined for
routing economics: this is a small, well-scoped, mechanical decode fix plus
hermetic file-to-file tests — a **mid-tier orchestrator (e.g. Sonnet) at
effort=medium** is sufficient and cheaper than Opus; the reasoning load is low
and there is no architecture/grammar synthesis. Cross-provider
`session-verification` stays mandatory and cross-provider (gpt-5-4 for a
Claude/Sonnet orchestrator; swap only if an OpenAI orchestrator runs it).
`ai-assignment.md` for 009 should be authored via routed analysis at its start,
not self-opined.

## Following sets (006 ranking, unchanged)

- **P2 — `output-contract-fidelity-batch`** (2 sessions): F-2-1 (crosstabs
  mislabeled `MakeTable`; parser-side cross-check via F-3-7), F-3-5 (ByVal vs
  default ByRef), BOM on 4 consumer JSON files, label-casing contradiction,
  others. One coordinated set, one golden regen at the end. Can run in parallel
  with 009 (the routed analysis flagged this) if two windows are burning.
- **P3 — `resolver-and-navigation-wiring`** (3 sessions): wires the resolver
  registries so the post-F-3-1 Unresolved wall (the 2306 `ResolutionReason`
  entries 008 surfaced) resolves. Hard prereq P1a (done); P2 recommended.
- **P4 — `switchboard-and-report-chain-edges`** (R2-1/R2-2), **P5 —
  `cli-input-robustness`** trail.

## Open deferred work to keep visible (does not block 009)

`docs/planning/deferred-work.md` carries DW-1..DW-4 (contextual-keyword slots,
`.Line`/`.Circle` graphics syntax, variable suffix→type, `!`-suffix function
names) — all parser-grammar-completeness candidates, none consumer-blocking and
none triggered by 009. The 009 closeout must re-check whether any trigger has
arrived (per the deferred-work convention) before authoring P2.
