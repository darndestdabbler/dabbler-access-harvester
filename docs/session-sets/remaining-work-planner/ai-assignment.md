# AI Assignment — remaining-work-planner

> **Session set:** `docs/session-sets/remaining-work-planner/`
> **Created:** 2026-05-17 (Session 1 in progress)
> **Cost target per session:** ~$0.05–$0.20 routed
> (cross-provider session-verification only; optional routed
> planning calls if the orchestrator is not Opus/Sonnet).
> **Routing policy:** This is a meta-planning set. File authoring
> (gap audit, stub specs, roadmap, change log) is in-conversation
> per the orchestrator-matches-target convention when the
> orchestrator is Opus or Sonnet. Only cross-provider session-
> verification routes.

> **Authoring note:** This ledger is authored in-conversation by
> Claude Opus 4.7 (the in-progress orchestrator), per Rule #17
> escape clause: "the orchestrator's opinion of which model could
> have been cheaper is the precise thing the routed analysis is
> designed to displace." When the orchestrator matches the
> routing target the routed call would just summon another
> instance of the same judgment at additional cost. The
> recommendation below is calibrated against the in-progress
> Session 1 actuals (the meta-planning work was authored
> directly without any routed analysis call), the cost-conscious
> outsource-last pattern in
> `docs/planning/strategy.md`, and the convention promoted
> 2026-04-29 ("subscription-paid in-IDE orchestrator pair as
> cross-provider rigor").

---

## Session 1: Phase 4 Gap Audit

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=high

### Rationale
Synthesis-heavy meta-planning across the full harvester pipeline
state (10 production projects, 10 test projects, ~250 C# source
files, 13 strategy/planning docs, ~30 prior-set change-logs).
Opus at high effort is the right calibration — the work is
analysis + synthesis (the gap audit recommends in/out-of-scope
per Phase 4 criterion), not mechanical correspondence. Sonnet
would likely produce a thinner audit; Gemini Pro would have
trouble with the cross-document synthesis at this volume. The
orchestrator-matches-target convention applies — author directly,
do not route the synthesis.

### Estimated routed cost
Low (~$0.05–$0.15 — single cross-provider session-verification
call only)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read strategy.md, project-guidance, lessons-learned, authoring-guide, strategic-reviews | Direct — context inherited from this conversation; no incremental cost |
| 2 | Read VBA-Parser-Migration-Harvester-Plan.md (893 lines) | Direct — single `Read` call |
| 3 | Inspect C# solution state (csproj inventory, Pipeline.cs, Program.cs, file counts) | Direct — `Bash` + `Read` calls |
| 4 | Skim change-log index; drill into 19 parser/resolver/enricher/CLI shaping sets | Direct — single `Bash` call surfacing titles + targeted reads |
| 5 | Inspect schema state, examples/, samples/, UnresolvedItem aggregator surface | Direct — `Bash` + `Read` |
| 6 | Author `gap-audit.md` — Phase 4 criterion-by-criterion synthesis | **Direct in-conversation** (orchestrator-matches-target; Opus would otherwise route to Opus, which adds cost without changing judgment) |
| 7 | `dotnet build && dotnet test` | N/A — meta-planning set; no code touched |
| 8 | Cross-provider session-verification | **route() → gpt-5-4** — mandatory cross-provider per Rule #2; verifies the audit against the strategic-review reports, the strategy doc's Phase 4 definition, and the inspected solution state |

### Actuals (filled during the session)
- Orchestrator used: claude-code claude-opus-4-7 @ effort=high
- Total routed cost: **$0.337** across 3 verification rounds
  (R1 $0.1746, R2 $0.0855, R3 $0.0765 — all gpt-5-4
  cross-provider). **Over the $0.05–$0.20 spec target by ~70%**
  due to two retry rounds.
- Verification verdict: **VERIFIED** (Round 3). R1 found 1 Major
  + 2 Minor; R2 found 1 Minor introduced by the R1 fixes; R3
  confirmed all four findings resolved. Issue log at
  `issue-logs/session-001.json`.
- Deviations from recommendation:
  1. The housekeeping pass prior to this set's start was
     authored as a direct commit outside the session-set
     framework; the strategy-reset artifacts (`strategy.md`
     revision, the two flipped state files,
     `lowdefy-ir-foundation/change-log.md`, the
     strategic-reviews/ filing) all landed in commits `138a319`
     and `2dd55e7`. That commit pair plus this Session 1
     collectively comprise the Phase 3 reset; Session 1's gap
     audit is the executable plan that the strategy reset
     pointed at.
  2. Cost overrun: spec target was per-session $0.05-$0.20;
     actual was $0.337. Two retry rounds (legitimate findings,
     all fixed) were the driver. R1's Major finding was a
     classification-consistency defect in the gap-audit; R2's
     Minor finding was a stale-cross-reference drift introduced
     by the R1 fix (the "Post-Fix Sweep All Narrative Artifacts"
     lesson at work). R3 confirmed clean.
- Notes for next-session calibration:
  1. **Pre-fix narrative sweep before re-verification.** When
     applying a fix that changes a count, list, or numbering
     scheme in narrative prose, grep the document for stale
     references *before* re-running verification. Round 2's
     finding could have been caught at zero cost by a one-line
     `grep "Question 4|5 open questions"` between the R1 fix
     and R2 invocation. Same pattern as the existing
     "Post-Fix Sweep All Narrative Artifacts" lesson in
     `lessons-learned.md` — promotion candidate for Convention
     if Session 2 or 3 hits the same pattern.
  2. **Anti-coupling discipline for Session 2 stub-spec
     authoring.** R1's Minor #3 (packaging-implementation
     prescription) is a Session-1-vs-Session-2 boundary
     reminder: gap audit recommends scope and acceptance
     criteria; stub-spec authoring picks the implementation
     shape. Session 2 should hold this boundary.
  3. **Three open questions for the human are waiting** on
     `gap-audit.md` Open Questions block. Session 2 should not
     start until at least Question 1 (MiniComplex inclusion)
     and Question 2 (macros/property-sheet boundary) have
     human resolutions; Question 3 (packaging shape) can
     resolve at the stub-spec authoring step if the human
     hasn't pre-decided.

---

## Session 2: Plan The Remaining Work

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=high

### Rationale
Authoring stub specs requires absorbing the Phase 4 criterion
each gap maps to, the harvester's existing patterns (csproj
conventions, prior stubs from the `vba-parser-session-set-generator`
output, the Stop-Rules pattern, the per-feature prereq table
from `lessons-learned.md`), and producing per-stub coherence.
Opus at high again.

### Estimated routed cost
Low (~$0.05–$0.20 routed — single cross-provider verification
plus any stub-spec routed calls if the audit produces many gaps
and the orchestrator chooses to route some of them; the latter
is unlikely if the orchestrator stays Opus/Sonnet).

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Register session start; verify Session 1 commit state | Direct — bash + python |
| 2 | Inventory existing assets (csproj, planner_template, Cli/Program.cs, UnresolvedItem* wiring, integration-testing-and-acceptance/coverage-audit) | Direct — Read calls |
| 3 | Decide GAP #5 fold-in vs. standalone (inventory found aggregator is in tests only) | **Direct in-conversation** (orchestrator-matches-target; this is the kind of meta-judgment Opus is paying to do) |
| 4-8 | Author 5 stub specs (F1-F5) at acceptance-criteria level only (per Session 1's anti-coupling calibration note) | **Direct in-conversation** (orchestrator-matches-target; routing the synthesis to Opus would just summon another instance) |
| 9 | Author remaining-work-tree.md (per-set index + per-feature prereq table) | **Direct in-conversation** (same reason) |
| 10 | Skip tree-amendments.md (no proposals) + routing-playbook-delta.md (no new task types) | Direct — decision recorded in the tree's "Out-of-tree items" section |
| 11-13 | Cross-provider session-verification (3 rounds) | **route() → gpt-5-4** — mandatory cross-provider per Rule #2 |
| 14 | Next-orchestrator recommendation for Session 3 | **route() → gemini-pro** — Rule #17 cross-provider; complexity_hint=50 forces tier 2 |

### Actuals (filled during the session)
- Orchestrator used: claude-code claude-opus-4-7 @ effort=high
- Total routed cost: **$0.6813** across 4 routed calls.
  - R1 verification: $0.2710 (gpt-5-4, 26973 input + 13572 output)
  - R2 verification: $0.2422 (gpt-5-4, 20332 input + 12755 output)
  - R3 verification: $0.1590 (gpt-5-4, 8219 input + 9227 output)
  - Next-orc recommendation: $0.0091 (gemini-pro, 5110 input + 276 output)
  - **Over the $0.05–$0.20 spec target by ~240%** due to two
    retry rounds. Same pattern as Session 1 ($0.337 there with the
    same R1+R2 retry cycle). The spec target was authored against a
    no-retry assumption that has now been falsified in both Session
    1 AND Session 2 — promotion candidate for a Convention update
    if Session 3 hits it too. (See Notes for next-session calibration.)
- Verification verdict: **VERIFIED** (Round 3). R1 found 3 Major
  (tree prereq inconsistency; missing skip-rationale for
  tree-amendments.md / routing-playbook-delta.md; missing
  freeze-boundary bullets across all 5 stubs). R2 found 1 Major + 1
  Minor introduced by the R1 fixes / deeper sweep (F3 determinism
  gate didn't cover extract/translate; cost rollup mismatch). R3
  confirmed clean. Issue traces: session-reviews/session-002.md
  (R1), session-002-round2.md (R2), session-002-round3.md (R3).
- Deviations from recommendation:
  1. None on orchestrator choice — Opus @ high worked as planned.
  2. Cost: $0.6813 vs. $0.05–$0.20 spec target. Driver: two
     retry rounds (legitimate findings, all fixed) plus higher
     per-round verification cost than Session 1 because Round 1
     prompt included all 5 stubs + tree (~107K chars input).
  3. The next-orc recommendation routed via Rule #17 (per a strict
     reading) rather than authored in-conversation (per Session 1's
     escape-clause precedent). The cost difference is negligible
     ($0.009 vs. zero); the cross-provider perspective surfaced a
     calibration suggestion ("reduce effort from high to medium")
     that an in-conversation Opus author would likely have biased
     against. The routed call earned its keep at this price point.
- Notes for next-session calibration:
  1. **Two-session retry-cycle pattern.** Both Session 1 and Session
     2 produced R1 ISSUES_FOUND → R2 ISSUES_FOUND (new findings) →
     R3 VERIFIED. Cost overrun was 70% in Session 1 and 240% in
     Session 2. **Promotion candidate:** if Session 3 also hits
     this pattern, propose promoting "meta-planning sets typically
     require 2-3 verification rounds; budget accordingly" to a
     Convention in `project-guidance.md` AND update the planner
     spec's Cost Expectations section. The current $0.05-$0.20
     target is a no-retry estimate that does not match observed
     repeat-context-set behaviour.
  2. **Pre-fix narrative sweep caught some but not all R2 findings.**
     The sweep would have caught the cost-rollup mismatch (a
     count-changed-without-grep failure mode the lesson predicts)
     but not the F3 determinism finding (R2 surfaced that via
     fresh criterion-by-criterion sweep — that's a separate class
     of finding the lesson doesn't cover). The lesson remains
     sound but is not a complete defence against R2 ISSUES.
  3. **The retry-round narrative-shape pattern.** Both sets'
     verifier findings concentrated in *consistency / freeze-
     boundary / cross-document alignment* (Session 2) and
     *classification-consistency / over-deferral / over-prescription*
     (Session 1). The orchestrator's natural mode for stub-spec
     authoring biases toward optimism about its own internal
     consistency; the verifier reliably catches this. Session 3's
     ROADMAP is simpler in shape (single document, no
     cross-stub-consistency surface) so this pattern may not
     repeat — Gemini Pro's "reduce effort from high to medium"
     advice is calibrated to that.

---

---

## Session 3: Final Roadmap + Change Log

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=medium

### Rationale
**Routed recommendation from gemini-pro (cross-provider, Rule #17):**
Opus is required for the session's core synthesis task of building
a dependency DAG from multiple file sources and authoring the
resulting `ROADMAP.md`. The `orchestrator-matches-target`
convention also favors Opus for efficient, in-conversation
authoring of the key documentation deliverables. Effort is medium
(not high) because Session 3's work is narrower than Sessions 1-2
— ROADMAP is a single pointer document with no cross-stub
consistency surface to police, so the high-effort tax that produced
R2 findings in Session 2 should not repeat here.

Full routed recommendation preserved at
`session-3-next-orchestrator-recommendation.md` ($0.0091, gemini-pro,
2026-05-17).

### Estimated routed cost
Low. Routed calls limited to a final cross-provider verification of
the generated documents, which have a simpler verification surface
than Session 2's outputs (single ROADMAP vs. 5 cross-coherent stubs).
Budget: ~$0.10–$0.30 if the no-retry case holds; ~$0.30–$0.60 if
Session 3 also hits the meta-planning-set two-retry pattern that
calibration note #1 calls out.

### Actuals
- (filled after Session 3)

---

**Next-session orchestrator recommendation (Session 3):**
claude-code claude-opus-4-7 @ effort=medium
Rationale: per the routed gemini-pro analysis above, medium effort
matches Session 3's narrower synthesis surface (single document,
no cross-stub consistency) and the calibration delta is to instruct
Session 3 explicitly to treat `remaining-work-tree.md` as the
authoritative input rather than re-interpreting it.
