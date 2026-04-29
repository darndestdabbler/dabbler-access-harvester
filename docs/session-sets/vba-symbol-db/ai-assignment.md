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
- Orchestrator used: <to be filled>
- Total routed cost: <to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>

## Session 5: Comprehension Probe + Migration-Readiness Review

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