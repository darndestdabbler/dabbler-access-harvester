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

### Recommended orchestrator (preview)
claude-code claude-opus-4-7 @ effort=high

### Rationale (preview)
Authoring stub specs requires absorbing the Phase 4 criterion
each gap maps to, the harvester's existing patterns (csproj
conventions, the `_template.md` shape, the Stop-Rules pattern,
the per-feature prereq table from `lessons-learned.md`), and
producing per-stub coherence. Opus at high again.

### Estimated routed cost (preview)
Low (~$0.05–$0.20 routed — single cross-provider verification
plus any stub-spec routed calls if the audit produces many gaps
and the orchestrator chooses to route some of them; the latter
is unlikely if the orchestrator stays Opus/Sonnet).

### Actuals
- (filled after Session 2)

---

## Session 3: Final Roadmap + Change Log

### Recommended orchestrator (preview)
claude-code claude-opus-4-7 @ effort=medium

### Rationale (preview)
Roadmap authoring + change-log writing are synthesis tasks but
narrower than the gap audit's full-pipeline synthesis. Medium
effort suffices. Sonnet could also handle this if the user
prefers a cost-down switch on the last session; the trade is
slightly less crisp narrative prose.

### Estimated routed cost (preview)
Low (~$0.05–$0.20 routed — single cross-provider verification;
the roadmap and change-log author in-conversation per the
orchestrator-matches-target convention).

### Actuals
- (filled after Session 3)

---

**Next-session orchestrator recommendation (Session 2):**
claude-code claude-opus-4-7 @ effort=high
Rationale: Stub-spec authoring against the gap audit benefits
from the same context Session 1 builds; an Opus continuation
preserves that without re-reading. Switch to Sonnet only if
Session 1's verification finds the audit overspecified and the
stub-spec authoring becomes more mechanical than synthesis-heavy.
