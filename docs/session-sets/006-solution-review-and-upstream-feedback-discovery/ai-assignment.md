# AI Assignment — 006-solution-review-and-upstream-feedback-discovery

> **Authoring note:** the standing convention is to author this file via
> routed analysis. For this set the orchestrator assignment was **explicit
> human direction** (precedence rule 1): the maintainer requested a Claude
> Fable 5 second-opinion review of the Opus-built solution — the model choice
> *is* the set's purpose, so there is no model-selection question for routed
> analysis to answer. Recorded here instead of routed.

## All Sessions (1–4)

**Assigned Orchestrator:** Claude Code / Claude Fable 5 (`claude-fable-5`)

**Rationale:** The maintainer explicitly directed that a higher-capability
model double-check the solution built by Opus-class orchestrators. The review
analysis is authored in-conversation (see the spec's *Orchestrator Assignment
And Delegation Deviation* section); `route()` is still used for the mandatory
cross-provider `session-verification` on every session, and for decision-time
consensus in Session 4 if the R2-3 ownership call is contested.

**Estimated Routed Cost:** Low (verification calls plus at most one
consensus round) — the review reasoning itself is on the orchestrator's
subscription window, not the metered router.

**Verification provider:** per `router-config.yaml` task-type mapping
(cross-provider with respect to Anthropic — gpt-5-4 or gemini-pro), with the
escalation ladder from `project-guidance.md` if the configured verifier fails.

---

## Actuals

*(append per session at closeout)*

### Session 1 (2026-06-12)

- **Orchestrator used:** Claude Code / Claude Fable 5 (`claude-fable-5`) @ effort=high
- **Total routed cost:** $0.38 (session-verification x2: gpt-5-4 round 1
  $0.26 ISSUES_FOUND -> fixes -> round 2 $0.12 VERIFIED)
- **Deviations from recommendation:** none — review authored
  in-conversation per the spec's human-directed deviation; router used
  only for mandatory cross-provider verification.
- **Notes for next-session calibration:** Round-1 verifier issues were
  all findings-doc discipline (explicit proven/inferred Status labels,
  severity-vs-evidence on an inferred premise, claim-vs-evidence width,
  excerpt-packet observability). Session 2 should bake the Status label
  and evidence-basis preambles into the findings doc from the start, and
  state in the verification prompt which claims rest on full in-session
  reads. Also note for Session 2 scope: the geometry/label/property-decode
  enrichers named in the spec's Session 2 step list physically live under
  `AccessHarvester.Core/Enrichment/` (not the Pipeline project) — Session 1
  deliberately left them unreviewed to avoid double coverage.

**Next-session orchestrator recommendation (Session 2):**
claude-code claude-fable-5 @ effort=high
Rationale: maintainer-assigned for all four sessions (the model choice is
the set's purpose); recorded here per the spec's deviation note rather
than via routed analysis.

### Session 2 (2026-06-12)

- **Orchestrator used:** Claude Code / Claude Fable 5 (`claude-fable-5`) @ effort=high
- **Total routed cost:** $0.22 (session-verification x2: gpt-5-4 round 1
  $0.18 ISSUES_FOUND (1 Minor) -> fix -> round 2 $0.03 VERIFIED)
- **Deviations from recommendation:** none — review authored
  in-conversation per the spec's human-directed deviation; router used
  only for mandatory cross-provider verification.
- **Notes for next-session calibration:** Baking the Status-label legend
  and evidence-basis preambles in from the start (Session 1's note) paid
  off — round 1 returned a single Minor labelling issue instead of
  Session 1's four. The one miss: a hybrid Status wording on F-2-5;
  Session 3 should use ONLY the three canonical labels verbatim and put
  the proven/inferred split inside the label line. Embedding live probe
  outputs (golden greps, BOM byte survey, call-site greps) directly in
  the verification prompt worked well for observability. Session 3
  scope note: F-2-1 (query-type decode shift) also infects the strategy
  doc's own table; the parser-stack session should treat the jet-sql
  parser as the contract-grade query-type source when assessing whether
  Q-rule classification could re-derive types from parsed SQL.

**Next-session orchestrator recommendation (Session 3):**
claude-code claude-fable-5 @ effort=high
Rationale: maintainer-assigned for all four sessions; recorded here per
the spec's deviation note rather than via routed analysis.

### Session 3 (2026-06-12)

- **Orchestrator used:** Claude Code / Claude Fable 5 (`claude-fable-5`) @ effort=high
- **Total routed cost:** $0.30 (session-verification x2: gpt-5-4 round 1
  $0.28 ISSUES_FOUND (3 Minor) -> fixes -> round 2 $0.02 VERIFIED)
- **Deviations from recommendation:** none — review authored
  in-conversation per the spec's human-directed deviation; router used
  only for mandatory cross-provider verification.
- **Notes for next-session calibration:** The Status-label discipline
  carried from Session 2 held (no label issues), but R1 still found three
  evidence-discipline Minors: a blast-radius sub-claim worded stronger
  than its source-read evidence, an uncited convention premise, and a
  worked example that didn't match its own cited regex. Session 4 should
  (a) cite the convention doc verbatim whenever a finding's impact rests
  on an output convention, and (b) sanity-check every worked example
  against the exact cited mechanism before authoring. Embedding ephemeral-
  probe console output verbatim in the verification prompt again kept R1
  focused on the doc rather than on observability. Session 4 is closeout-
  grade: use the prose-first lead BEFORE the file listings plus the JSON
  block, per convention.

**Next-session orchestrator recommendation (Session 4):**
claude-code claude-fable-5 @ effort=high
Rationale: maintainer-assigned for all four sessions; recorded here per
the spec's deviation note rather than via routed analysis. Session 4 is
the only COM-touching session (fresh HomeHealthCare run) — run it on the
Access-equipped host, deliberately and sequentially, with the
spurious-`.accdb`-diff sweep before staging.
