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
- Orchestrator used: <to be filled>
- Total routed cost: <to be filled>
- Deviations from recommendation: <to be filled>
- Notes for next-session calibration: <to be filled>

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