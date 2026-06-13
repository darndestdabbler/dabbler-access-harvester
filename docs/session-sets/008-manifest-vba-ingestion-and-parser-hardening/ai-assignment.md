# AI Assignment: 008-manifest-vba-ingestion-and-parser-hardening

This document outlines the AI-led execution plan for the `008-manifest-vba-ingestion-and-parser-hardening` session set. Each session's orchestrator recommendation, rationale, and step-by-step routing plan is defined below.

## Session 1: Kb-Tier VBA Ingestion And Golden Regeneration (F-3-1)

### Recommended orchestrator
claude-code claude-fable-5 @ effort=high

### Rationale
This session is code-heavy, focusing on a specific ingestion-path fix in `Pipeline.cs` that requires COM-tier validation. The pre-selected orchestrator is suitable for targeted code implementation and direct execution of build/test/validation commands on the host.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Read `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`, 006's `s3-review-findings.md` F-3-1, and this spec. | route(analysis) |
| 2    | Re-ground F-3-1 against the current tree and record the grounding in the session log. | route(analysis) |
| 3    | Implement the ingestion fix in `EmitManifestArtifacts` to source form/report VBA from the kb tier. | orchestrator-direct |
| 4    | Create hermetic pins for the manifest path to ensure non-empty parsed items and visibility of degraded parses. | orchestrator-direct |
| 5    | Hermetic validation: `dotnet build`, then run affected hermetic test projects sequentially and in the foreground. | orchestrator-direct |
| 6    | COM-tier validation: regenerate the HomeHealthCare golden, re-run `AccessHarvester.Integration`, and sweep `.accdb` diffs. | orchestrator-direct |
| 7    | Cross-provider verification of the session's diff, including a summary of golden deltas and disclosure of the expected Unresolved wall. | route(session-verification) |
| 8    | Closeout: update `disposition.json`, run `close_session`, commit, push, and notify. | orchestrator-direct |

### Actuals (post-session)
- Orchestrator used: claude-code claude-fable-5 @ effort=high (as recommended)
- Total routed cost: $0.12 (ai-assignment analysis gemini-pro $0.022; session-verification gpt-5-4 $0.099)
- Deviations: steps 1–2 (required reading + F-3-1 re-grounding) were handled
  orchestrator-direct rather than via route(analysis) — they are mechanical
  reading plus empirical probes against the tree, which Delegation Discipline
  keeps in-conversation; the grounding evidence was recorded in the activity
  log. A wedged sanitize-probe `AccessHarvester.exe` left over from 007 held
  the Integration bin DLL locks and was reaped via WMI Terminate (standing
  authorization). The golden-regeneration path was formalized as a guarded
  `RegenerateCommittedPipelineCorpus` dev tool in GoldenCorpusRegenerator
  (mirrors the regression gate's capture + normalization) since no pipeline-
  slice regenerator existed.
- Notes for next calibration: VERIFIED on round 1 — the evidence-embedding
  sweep (probe outputs pasted verbatim into the verification prompt) plus the
  expected-states disclosure block prevented every prior-set round-1 failure
  mode; keep both. The parse pipeline's Recovering-by-default means
  `ModeUsed != Strict` is NOT a degraded signal — Session 3's diagnostics
  surfacing must key off `Diagnostics.Count > 0 || ModeUsed == Lossy`.

**Next-session recommendation (Session 2):**
codex gpt-5-5 @ effort=high (per the routed recommendation below)
Rationale: grammar + AstBuilder hardening is intricate multi-file parser work; cross-provider verification remains mandatory regardless of orchestrator (swap the session-verification override to a non-OpenAI model if Codex orchestrates, per lessons-learned).

## Session 2: Grammar And AstBuilder Hardening, Corpus Pins (F-3-2, F-3-3, F-3-10 ride-alongs)

### Recommended orchestrator
codex gpt-5-5 @ effort=high

### Rationale
This session involves complex, multi-file changes to core parsing logic, including ANTLR grammar edits and deep AstBuilder hardening. A top-tier coding model is recommended to handle the intricate dependencies and generate precise test corpus cases.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Read mandatory pre-session docs, plus 006's F-3-2, F-3-3, F-3-10, and the F-3-9 corpus-addition item. | route(analysis) |
| 2    | Edit `Vba.g4` to accept `TYPED_IDENTIFIER` in new positions and add specified contextual keywords. | orchestrator-direct |
| 3    | Harden `AstBuilder.cs` with error-node guards, null checks, and implement specified F-3-10 ride-alongs. | orchestrator-direct |
| 4    | Implement F-3-3: strip type suffixes at all declaration sites using the existing `ExtractTypeSuffix` helper. | orchestrator-direct |
| 5    | Add a per-module try/catch to `SymbolDbBuilder`'s Build Pass 2 to mirror pipeline resilience. | orchestrator-direct |
| 6    | Create hermetic pins for all trigger inputs and add new corpus cases under `docs/test-cases/`. | orchestrator-direct |
| 7    | Validation: build, run affected hermetic tests, and perform COM-tier golden regeneration if required. | orchestrator-direct |
| 8    | Cross-provider verification of the diff, new corpus cases, and any golden deltas. | route(session-verification) |
| 9    | Closeout: update `disposition.json`, run `close_session`, commit, push, and notify. | orchestrator-direct |

### Actuals (post-session)
- Orchestrator used: claude-code claude-fable-5 @ effort=high for Step 1-3 (grammar +
  AstBuilder + F-3-3, pre-storm), then **claude-code claude-opus-4-8 @ default** carried
  the work forward after Fable was shut down by Anthropic 2026-06-13 (human switched
  the model mid-set). The recommended codex gpt-5-5 did not orchestrate; human direction
  took precedence.
- Total routed cost: ~$0.23 (session-verification gpt-5-4 $0.2322; the two interim
  devil's-advocate reviews were NON-routed in-IDE Copilot, $0).
- Deviations / notes:
  - Mid-set model handoff (Fable -> Opus) introduced a validation-first + Fable-review-queue
    discipline (now in project-guidance.md + .claude memory + docs/planning/fable-review-queue.md):
    Fable's uncommitted work was validated empirically before building on it, and an interim
    devil's-advocate review (Gemini Pro + GPT 5.4, non-routed) was run before the routed
    verification. Both caught/confirmed real issues (H2/H3/H4), all fixed.
  - H4 (derive ReturnType from suffix) goes slightly beyond the spec's literal "strip into
    TypeSuffix"; justified + verifier-endorsed (completes F-3-2, existing field, golden-neutral).
  - Two gaps deferred + memorialized: DW-1, DW-4 in docs/planning/deferred-work.md.
- Next calibration: keep the interim non-routed in-IDE review for inherited/high-risk work
  (free, caught the named-arg symmetry hole the routed pass alone might not have prioritized).
  The routed verification's prose-first lead + soft-spot disclosure + embedded evidence drove
  a clean R1 VERIFIED, as in prior sets.

**Next-session recommendation (Session 3):**
claude-code claude-opus-4-8 @ effort=medium (per the routed Session 3 recommendation;
the ai-assignment named opus-4-7, 4-8 is the current Opus). Rationale: F-3-6 schema-additive
coding + the F-3-9 conformance-contract decision (reasoning, decision-time-consensus eligible)
+ next-set authoring (prose). Session 3 must read docs/planning/deferred-work.md (DW-1..4) per
the new required-reading convention. session-verification stays cross-provider (gpt-5-4).

## Session 3: Diagnostics Surfacing And Conformance-Contract Decision (F-3-6, F-3-9)

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=medium

### Rationale
This session mixes schema-additive coding with a significant reasoning task (F-3-9 contract decision) and prose generation (next-set authoring). A model with strong reasoning and writing capabilities is optimal for balancing implementation with high-level architectural and planning tasks.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Read mandatory pre-session docs, plus 006's F-3-6 and F-3-9. | route(analysis) |
| 2    | Implement F-3-6: surface `ResolutionReason` in `ParserOutput`, fold parse diagnostics into the manifest, and add diagnostics to `BuildQueryExports`. | orchestrator-direct |
| 3    | Address F-3-9: route the trade-off analysis for the conformance contract, then implement the consensus decision. | route(analysis) |
| 4    | Validation: build, run hermetic tests, then perform COM-tier golden regeneration and Integration re-run. | orchestrator-direct |
| 5    | Author the next session set spec and state file per the 006 ranking and current completion state. | route(analysis) |
| 6    | Cross-provider verification (closeout grade): prose lead, TRX evidence, golden delta summary, F-3-9 decision, and new set's spec. | route(session-verification) |
| 7    | Closeout: update `disposition.json`, `change-log.md`, `next-session-set-recommendation.md` (routed), `close_session`, commit, push, notify. | orchestrator-direct |

### Actuals (post-session)
- (to be filled at session close)