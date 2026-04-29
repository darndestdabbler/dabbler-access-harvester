# AI Assignment - vba-symbol-db

## Session 1: Symbols Table + Dead-Code / Dead-Object / MD5 Detection + Measurement

### Recommended orchestrator
claude-code (Anthropic Opus 4.7 @ effort=high)

### Rationale
This session establishes the foundational schema and data extraction logic for the entire set, justifying a high-capability model for initial setup. While mechanically driven, Opus's comprehension of the detailed spec ensures the core C# projects and data structures are architected correctly from the start.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read and comprehend prerequisite docs. | `local` |
| 2 | Re-confirm inventory of existing assets. | `local` |
| 3 | Design SQLite schema migration code. | `local` |
| 4 | Implement `Modules` and `Symbols` extractors. | `local`, with optional `route(task_type="code-review")` if any file exceeds ~50 lines. |
| 5 | Implement dead-code detection logic. | `local`, with optional `route(task_type="code-review")` if any file exceeds ~50 lines. |
| 6 | Implement `DeferredArtifacts` pre-filter pass. | `local`, with optional `route(task_type="code-review")` if any file exceeds ~50 lines. |
| 7 | Implement MD5-hash duplicate detection. | `local`, with optional `route(task_type="code-review")` if any file exceeds ~50 lines. |
| 8 | Generate `measurement-northwind.md` report. | `local` |
| 9 | Write hermetic xUnit tests. | `local` |
| 10 | Run `dotnet build && dotnet test`. | `local` |
| 11 | Perform cross-provider session verification. | `route(task_type="session-verification")` to `gpt-5-4` per `router-config.yaml`. |

### Actuals (filled after the session)
- Orchestrator used: claude-code (Anthropic Opus 4.7 @ effort=high) -- matched recommendation
- Total routed cost: ~$0.61 (one analysis call for ai-assignment.md authoring + three session-verification calls)
- Deviations from recommendation: None on orchestrator. Routing surprises:
  - The session-verification call was retried twice (rounds 1+2 returned ISSUES_FOUND) before round 3 returned VERIFIED. Five Major or Minor findings were addressed in-session, the largest cost driver after the orchestrator-side coding work.
  - Round 1 verifier cost: $0.261; Round 2: $0.257; Round 3: $0.070 (smaller scope after fix).
  - Round 3 was small enough to land in 19 seconds; the pattern matches the workflow's "max 2 retries" guidance with `complexity_hint=85` for the retry rounds.
- Notes for next-session calibration:
  - The Round-1 verifier surfaced two correctness issues the orchestrator should have anticipated (FK enforcement, string-aware comment stripping). Future SQLite + token-walker work in this set should pre-emptively cover those shapes in tests before routing for verification.
  - Property-triplet collision (Round-2 finding) was a missed schema-level hazard. When persisting symbol kinds to a single FQN PK, double-check kinds that are name-distinct in source but kind-distinct in semantics (Property triplets, multi-pass enums, etc.).
  - The verifier prefers prose-then-JSON when the prompt explicitly asks for prose first; preserving that pattern in Sessions 2-5 keeps verification cost predictable.

## Session 2: References + Member Accesses + Caller-Kind Annotation

### Recommended orchestrator
claude-code (Anthropic Haiku 3.5 @ effort=medium)

### Rationale
This session is purely mechanical, transforming existing in-memory reference graphs into SQLite rows without requiring novel synthesis. Haiku is the most cost-effective orchestrator suitable for this type of structured data plumbing.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Required reading. | `local` |
| 2 | Verify Session 1 prerequisites. | `local` |
| 3 | Implement ReferencesExtractor + FqnResolver + model records. | `local` |
| 4 | Implement MemberAccesses writer (integrated into ReferencesExtractor). | `local` |
| 5 | Implement caller_kind annotation (naming-shape heuristic). | `local` |
| 6 | Update BuildResult, SymbolDbBuilder, SymbolDbWriter, BuildArtifacts. | `local` |
| 7 | Write hermetic xUnit tests. | `local` |
| 8 | Run `dotnet build && dotnet test`. | `local` |
| 9 | Cross-provider session verification (3 rounds). | `route(task_type="session-verification")` to `gpt-5-4`; M2 tiebreaker via `query("gemini-pro")` |

### Actuals (filled after the session)
- Orchestrator used: claude-code (Anthropic Sonnet 4.6 @ effort=medium) — post-Session-1 recommendation was Sonnet; matched
- Total routed cost: ~$0.92 (3 verification rounds + 1 Gemini tiebreaker for M2 adjudication)
- Deviations from recommendation: None. Three verification rounds were needed (vs. expected 1-2); all findings by round 3 were Minor.
- Notes for next-session calibration:
  - M1 (local FQN): DeclarationFqn must handle ProcedureScope locals with a 3-part FQN. Session 3 will have similar scope-to-FQN mappings — test all declaration-in-scope variants before routing for verification.
  - M2 (variable_write): `ResolvedReference` has no write-context. Write detection requires AST parent walking. Dismissed via tiebreaker; document as known limitation if symbol DB ever needs this.
  - Test assertiveness: guards that protect vacuous code paths should document WHY they guard (fixture count = 0). Assertive tests (e.g., `Assert.DoesNotContain`) are better than shape-only guards.
  - Caller-kind heuristic: using naming convention (Form/Report + '_') is a correct approximation for Session 2. Session 3's EventBindings table will enable an exact join; update EntryPointHeuristic at that point.

## Session 3: Column Mappings (Form Controls + Event Bindings + Queries) + Macro Normalization

### Recommended orchestrator
claude-code (Anthropic Haiku-class @ effort=low) — or Sonnet @ effort=low

### Rationale
Similar to Session 2, this work involves translating existing, well-structured in-memory objects into database records. Haiku remains the recommended choice for its efficiency in handling these mechanical correspondence tasks.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| ... | ... | ... |

### Actuals (filled after the session)
- Orchestrator used: claude-code (Anthropic Sonnet 4.6 @ effort=medium) — within recommended Sonnet/Haiku-class envelope
- Total routed cost: ~$0.34 (Round 1 verification $0.237 + Round 2 re-verification $0.100; no tiebreaker)
- Deviations from recommendation: None on orchestrator. Two verification rounds were needed (Round 1 ISSUES_FOUND with 6 Major + 2 Minor; Round 2 VERIFIED with 5 fixes accepted + 4 deferrals accepted).
- Notes for next-session calibration:
  - Round-1 verifier flagged 8 issues; the orchestrator fixed 4 Major + 1 Minor in-session and explicitly deferred 3 Major + 1 Minor with documented rationale (QueryColumns, subform nested-FQN, AST clause-hash, BaseInfo parser). All four deferrals were accepted by Round 2; treat the deferral pattern (with explicit unblock criteria) as the working contract for "out-of-scope-but-acknowledged" findings.
  - Round-1 also caught a binary-compatibility break from default-parameter additions on a public method. For future sessions that extend public Writer / Builder surfaces, **default to keeping the original signature as a separate overload** rather than relying on default-parameter source compatibility. Add this to the "future sessions checklist" before extending Sessions 4 / 5 writers.
  - The query-deadness wiring shows the value of the Session 2 reference graph: with `vbaResult.References` available the deadness signal landed cheaply. Session 4 should similarly lean on Session 2/3 outputs (reference graph + form/macro/query/table data) when computing leaves-first interpretation order.
  - QueryColumns deferral exposes a concrete raw-IR coverage gap (`AccessHarvester.Core.Extraction.QueryExporter` does not enumerate `QueryDef.Fields`). If Session 5's migration probe needs output-column metadata, schedule a small raw-exporter widening session before Session 5 — or accept that the JET-SQL parser session set is the gating dependency.

## Session 4: AI Interpretation Pipeline (Leaves-First; Confidence-Thresholded)

### Recommended orchestrator
claude-code (Anthropic Sonnet 3.5 @ effort=high)

### Rationale
This session involves a high volume of synthesis-light interpretation tasks, requiring good code understanding but not the deep reasoning of Opus. Sonnet provides the optimal balance of capability and cost for generating per-symbol documentation at scale.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| ... | ... | ... |

### Actuals (filled after the session)
- Orchestrator used: claude-code (Anthropic Opus 4.7 @ effort=high) — matched recommendation
- Total routed cost: ~$2.16 (interpretation pipeline $1.49 + three verification rounds at $0.244 / $0.257 / $0.172 = $0.67)
- Deviations from recommendation: None on orchestrator. Three verification rounds were needed (vs. anticipated 1-2):
  - Round 1: ISSUES_FOUND with 2 Critical + 4 Major + 1 Minor (queries-not-interpreted + Step-11 dotnet-test re-running BuildArtifacts and wiping the AI columns mid-flight + several idempotency gaps in skip-row writes).
  - Round 2: ISSUES_FOUND with 4 Major + 1 Minor (symbol-side transient skips not idempotent, parse-failure retry outside transient handler, broken `--limit` exception path, docstring drift, plus a verify-prompt-construction artifact where the helper sent placeholder Justification1 fields for non-symbol classes).
  - Round 3: VERIFIED (clean) after fixing the four Major + one Minor and sending the actual stored Justification1 text in the prompt.
- Notes for next-session calibration:
  - **Critical lesson — Step-11 `dotnet test` was destructive.** The Session-1 `GenerateArtifacts` test ran `BuildArtifacts.RunForNorthwind` on every test invocation, which DELETES and recreates `symbol-db.sqlite`. Session 4 added a `HasInterpretationsPopulated` guard so the regeneration is now a no-op once Session 4+ has populated AI columns. This needs to land alongside any future session that writes downstream-only data into the same artifact (Session 5's probe outputs go elsewhere, so it's already safe).
  - **Cost framing.** $2.16 is comfortably under the $2-4 spec estimate, but only because escalation hardly fired (1 opinion-2 + 1 opinion-3 across 84 symbols, well below the 20% spec budget). With 98% of interpretations landing at confidence >= 0.90, the cross-provider auto-verification on `documentation` was sufficient calibration; opinions 2/3 were rarely necessary.
  - **Idempotent audit rows are non-trivial.** Round 2's findings exposed three different skip-write paths that needed `_already_skipped_history()` guards. Future sessions adding new audit-row writers should mirror the helper pattern from the start, not retrofit it.
  - **Verifier prompt construction matters.** Round 2 flagged "weakly grounded" Justification1 because the verify script sent literal placeholder text `'(macro)'` / `'(table)'` / `'(query)'` instead of pulling stored values. Always send the real DB-backed text into the verification prompt; the verifier cannot assume the prompt is a faithful sample.
  - Macros, lookup tables, and queries must be runnable by default — Round 1 flagged the original opt-in `--include-*` flags as a spec gap. Default-on with `--no-*` opt-outs is the right shape.

## Session 5: Block-Randomized Independent Cross-Provider Re-Interpretation

### Recommended orchestrator
claude-code (Anthropic Sonnet 4.6 @ effort=medium)

### Rationale
Session 5's complexity is low: it primarily involves generating boilerplate Python for sampling and routing, reusing existing complex components from Session 4. The 84 core reasoning tasks are pinned to Opus and GPT-5.4 via `query()` regardless of which orchestrator runs the script. A Sonnet-class model is optimal for this well-defined, non-creative implementation task.

(Full routed analysis preserved at `session-reviews/session-5-orchestrator-rec.txt`.)

### Estimated routed cost
- Orchestrator-side: ~$0.30–$0.50 (session verification + any analysis calls)
- 84 pinned `query()` calls: $5–$15 (fixed regardless of orchestrator)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Required reading | `local` |
| 2 | Define stratification policy, persist sample.json | `local` |
| 3 | Implement sampler.py | `local` |
| 4 | Implement independent_runner.py | `local` |
| 5 | Run opinion 2 — Opus (42 calls) | `query("opus", ...)` pinned |
| 6 | Run opinion 3 — GPT-5.4 (42 calls) | `query("gpt-5-4", ...)` pinned |
| 7 | Generate sample-report.md | `local` |
| 8 | Write hermetic Python tests | `local` |
| 9 | `dotnet build && dotnet test` | `local` |
| 10 | Cross-provider session verification | `route(task_type="session-verification")` to `gpt-5-4` |

### Actuals (filled after the session)
- Orchestrator used: claude-code (Anthropic Sonnet 4.6 @ effort=medium) — matched recommendation
- Total routed cost: ~$7.98 ($5.41 opinion 2 Opus + $2.14 opinion 3 GPT-5.4 + ~$0.43 orchestrator verification)
- Deviations from recommendation: None.
- Notes for next-session calibration:
  - Opinion 2 (Opus) mean confidence: 0.787 — materially lower than Gemini Pro's ~0.97 from Session 4.
  - Opinion 3 (GPT-5.4) mean confidence: 0.754 — similarly lower. Both providers agree on the hard items: `m_lng*` module vars, `sOneLineItem`/`sCatalog_QualityHeader` enum values, `SystemSettings` table.
  - Per-item cost actuals: Gemini ~$0.014/item, Opus ~$0.129/item, GPT-5.4 ~$0.051/item. Useful for scaling estimates to full DB.
  - **Batching opportunity for Session 6:** Session 6 makes 126 grading calls (42 items × 3 judges). Per-item calls add latency; batching 6–8 short items (enum values, tables) per call would cut call count to ~30–40 with no idempotency cost. Worth implementing given the scale.
  - The `independent_runner.py` pattern (neutral-evidence prompts, idempotent per-slot writes, per-item transient-error recovery) is reusable for Session 6's grading pass.

## Session 6: Cross-Provider Consistency + Confidence Calibration

### Recommended orchestrator
claude-code (Anthropic Opus 4.7 @ effort=high)

### Rationale
Session 6's primary deliverable is a synthesis-heavy statistical report (`correlation-report.md`) that interprets Pearson/Spearman correlations, Krippendorff's alpha, and per-kind breakdowns to render a verdict on whether the symbol DB's confidence column is trustworthy. The repo convention pins synthesis work to Opus. The implementation side (grader/aggregator/correlation modules + 126-call runner) is comfortably within Sonnet's range, but using a single Opus orchestrator across both implementation and synthesis keeps context coherent and avoids a mid-session handoff. The 126 routed grading calls carry their own provider identity (gemini-pro / opus / gpt-5-4) regardless of orchestrator choice; the orchestrator's marginal cost is a small fraction of the session's total spend.

(Full routed analysis preserved at `session-reviews/session-6-orchestrator-rec.txt`.)

### Estimated routed cost
high (~$7–$15 routed across three judging providers on the 42-item sample, plus ~$0.30–$0.50 orchestrator-side verification)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Required reading | `local` |
| 2 | Define grading rubric (already specified) | `local` |
| 3 | Implement `grader.py` (prompt builder + parser + grades.sqlite schema) | `local` |
| 4 | Run 126 grading calls (3 judges × 42 items) | `query("gemini-pro" / "opus" / "gpt-5-4", task_type="analysis")` pinned |
| 5 | Per-judge intra-correlation reduction (`aggregator.py`) | `local` |
| 6 | Confidence vs consistency correlation + per-kind + IRR + outliers (`correlation.py`) | `local` |
| 7 | Generate `correlation-report.md` + `correlation-scatter.png` | `local` (Opus orchestrator authors in-conversation per the synthesis convention) |
| 8 | Hermetic Python tests | `local` |
| 9 | `dotnet build && dotnet test` | `local` |
| 10 | Cross-provider session verification | `route(task_type="session-verification")` to `gpt-5-4` |

### Actuals (filled after the session)
- Orchestrator used: claude-code (Anthropic Opus 4.7 @ effort=high) -- matched recommendation
- Total routed cost: ~$12.46 ($0.01 orchestrator-rec analysis + $11.96 grading [gemini-pro $0.30, opus $9.13, gpt-5-4 $2.53] + $0.49 verification across 3 rounds)
- Deviations from recommendation: None on orchestrator. The 126 grading calls were threaded (max_workers=4 per judge) after the initial sequential run hit ~1 item/min on Gemini-Pro latency; threading cut wall time roughly 4x without changing semantics.
- Notes for next-session calibration:
  - Verifier ran three rounds. Round 1 found 4 issues (2 Major outlier-selection + IRR-counting bugs, 2 Minor retry-failure-persistence + rationale-precision bugs); round 2 verified all four fixes clean and surfaced 1 new Minor (split-path per-kind only emitting interpretation, omitting justification); round 3 VERIFIED. The pattern matches the workflow's "max 2 retries" guidance well -- round 3 was a focused 19-second re-check at $0.06 because the scope shrank to one finding.
  - **Headline finding worth flagging for Session 7:** confidence is essentially uncorrelated with cross-provider interpretation agreement (Pearson +0.004) and mildly negative against justification agreement (-0.18). The Session 4 escalation thresholds (0.75/0.70) are not validated. Session 7's go/no-go verdict on the symbol DB should land separately from any decision about whether to *trust the confidence column at scale*; the right framing is "the substrate works, the self-rating layer needs recalibration."
  - **Threaded SQLite gotcha:** sqlite3 connections aren't thread-safe. The first threaded grader pass used a shared symbol_con across worker threads and produced 84 false triplet_missing failures before the bug surfaced. Fix: do all DB reads on the main thread, then thread only the API calls. Worth a `lessons-learned.md` entry if a second context hits the same pattern.
  - **Module_var stratum is the highest false-confidence risk.** Per-kind interp r = -0.73; justif r = -0.88 across the 7 sampled module variables. Session 7's comprehension probe targets a Form_*_BeforeUpdate handler, which is in the safer `sub` stratum (interp r = +0.05); but if the probe surfaces interpretation drift, the result generalizes worse than the per-stratum mean would suggest.

## Session 7: Comprehension Probe + Migration-Readiness Review

> Originally numbered Session 5 of 5; renumbered 2026-04-29 when the
> calibration block (Sessions 5–6) was inserted.

### Recommended orchestrator
claude-code (Anthropic Opus 4.7 @ effort=max)

### Rationale
This final session requires deep synthesis, analysis, and adjudication to make a high-stakes go/no-go decision on the project's success. Opus is the designated orchestrator for complex reasoning, cross-model comparison, and authoring final project verdicts.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| ... | ... | ... |

### Actuals (filled after the session)
- Orchestrator used: <to be filled>
- Total routed cost: <to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>

## Next-session orchestrator recommendation (Session 2)

**Recommended (post-Session-1 re-route via gemini-pro):**
claude-code (Anthropic Sonnet 4.6 @ effort=medium)
Rationale: Session 2 is a mechanical data transformation task for which Sonnet is the most cost-effective capable orchestrator available, aligning with the project's guidance to avoid using frontier models for non-synthesis work.

**Initial recommendation at Session 1 start (kept for audit):**
claude-code (Anthropic Haiku 3.5 @ effort=medium)
Session 2 is a mechanical data transformation task for which Haiku is the most cost-effective capable orchestrator.

The post-Session-1 recommendation supersedes; Sonnet @ medium is the right balance because mechanical correspondence tasks in this repo have historically benefitted from Anthropic's careful line-by-line reasoning (see `lessons-learned.md` -- "Mechanical-Gate Task Types Calibrate To Sonnet-Cost").

## Next-session orchestrator recommendation (Session 4)

**Recommended (post-Session-3 re-route via `analysis` to gemini-pro):**
claude-code (Anthropic Opus 4.7 @ effort=high)

Rationale: Session 4 shifts from the mechanical work of Sessions 2-3 to complex system design and implementation, which mirrors Session 1's profile where Opus proved effective. The session requires strong reasoning for both algorithmic Python code (leaves-first topological sort with cycle handling, confidence-thresholded escalation logic) and the nuanced prompt engineering for 8+ distinct symbol-kind templates. Using Opus at high effort minimises verification cycles and produces a more robust initial implementation for this core pipeline component. Sticking with the incumbent Anthropic toolchain reduces variability versus switching providers.

(Full routed analysis preserved at `session-reviews/session-4-rec.txt`.)

## Next-session orchestrator recommendation (Session 7, originally Session 5)

> The recommendation below was routed at Session 4 close, when the
> capstone was Session 5 of 5. After the 2026-04-29 spec extension it
> applies to Session 7 (still the capstone). A fresh recommendation
> for the new Session 5 (block-randomization) appears below.

**Recommended (post-Session-4 re-route via `analysis` to gemini-pro):**
claude-code (Anthropic Opus 4.7 @ effort=max)

Rationale: Session 7 is the capstone — the primary tasks are not code generation but high-judgment synthesis, adjudication, and authoring the go/no-go verdict. The orchestrator must draft a ground-truth reference, compare three providers' probe responses against it, and write a defensible recommendation. Using Opus at max effort prioritises a definitive, well-supported conclusion over marginal cost savings in this final session.

(Full routed analysis preserved at `session-reviews/session-5-rec.txt`.)

## Next-session orchestrator recommendation (Session 5, block-randomization)

**Recommended (post-Session-4 re-route via `analysis` to gemini-pro):**
claude-code (Anthropic Sonnet 4.6 @ effort=medium)

Rationale: Session 5's work is mechanical Python wiring of existing Session-4 components (prompt_builder, db.py, topology). The 84 reasoning calls carry their own model identity. Sonnet is optimal for this non-synthesis orchestration task.

(Full routed analysis preserved at `session-reviews/session-5-orchestrator-rec.txt`.)

## Next-session orchestrator recommendation (Session 7, capstone)

**Recommended (post-Session-6 re-route via `analysis` to gemini-pro):**
claude-code (Anthropic Opus 4.7 @ effort=high)

Rationale: Session 7 is the capstone — the orchestrator authors the source-grounded reference description, compares three providers' probe + migration-readiness responses, drafts `verdict.md`, generates `change-log.md`, and runs the Step 9 reorganization review. All five tasks are synthesis and judgment work, and the repo convention pins synthesis to Opus. `effort=high` is right for capstone-level go/no-go reasoning; reserve `effort=max` for if the human invokes the maxout suffix to drain the Anthropic window.

(Full routed analysis preserved at `session-reviews/session-7-orchestrator-rec.txt`.)