```markdown
# AI Assignment Ledger: access-metadata-extraction

| Session | Purpose                                                      | Primary Task Type(s)                | Recommended Orchestrator/Tier                  |
|:--------|:-------------------------------------------------------------|:------------------------------------|:-----------------------------------------------|
| 1 of 6  | Synthesize form/report metadata schema (`.cs` files)         | `architecture`                      | `AI Router -> opus`                            |
|         | **Actuals:**                                                 |                                     |                                                |
| 2 of 6  | Synthesize query/table metadata schema and extractor         | `architecture`                      | `AI Router -> opus`                            |
|         | **Actuals:**                                                 |                                     |                                                |
| 3 of 6  | Author ≥7 `.accdb` fixtures + `expected.json` via test-builder | `test-generation`, `code-generation`  | `AI Router -> gemini-flash`                    |
|         | **Actuals:**                                                 |                                     |                                                |
| 4 of 6  | Wire form/report extractor against fixtures                  | `schema-validation`, `code-generation`| `AI Router -> gemini-flash`                    |
|         | **Actuals:**                                                 |                                     |                                                |
| 5 of 6  | Wire query/table extractor and land integration tests        | `schema-validation`, `code-generation`| `AI Router -> gemini-flash`                    |
|         | **Actuals:**                                                 |                                     |                                                |
| 6 of 6  | (Buffer) Address verifier-surfaced issues                    | `refactoring`, `analysis`           | `AI Router -> gemini-pro`                      |
|         | **Actuals:**                                                 |                                     |                                                |
| **All** | **Session Verification**                                     | `session-verification`              | `AI Router -> opus` (Cross-provider for Codex) |
```

---

## Session 1 actuals (appended ai_router)

**Orchestrator used:** Claude Opus 4.7 [1m context] @ effort=max (mid-session
handoff from Codex per the `maxout Claude` Token-Window Budget Override).

**Total routed cost — Session 1:**
- Codex's prior route(analysis) for ai-assignment.md: $0.012 (gemini-pro)
- Codex's prior route(architecture) synthesis (orphaned, did not convert to .cs files): $1.867 (opus) + $0.185 (verifier)
- Claude's round-1 cross-provider verification: $0.277 (gpt-5-4)
- Claude's round-2 cross-provider verification: $0.148 (gpt-5-4)
- Claude's next-orchestrator recommendation (this step): $0.005 (gemini-pro)

**Verifier verdict:** ISSUES_FOUND in round 1 (2 Major, 1 Minor) — all
addressed; round 2 ISSUES_FOUND (1 Major, 1 Minor, 1 Nitpick) — all addressed.
Round 3 retry skipped at the human's discretion; the patches build and test
clean. Session 1 commits with no unresolved Major/Critical issues.

**Deviations from recommendation:** Orchestrator switched mid-session from
Codex to Claude Opus 4.7 after Codex's synthesis produced design-doc orphans
rather than .cs files. Claude authored the schema in-conversation per the
'Opus orchestrator handles synthesis in-conversation' Convention.

**Notes for Session 2 calibration:** Codex's orphan synthesis output and
verifier review are still on disk at
`docs/session-sets/access-metadata-extraction/session-1-form-report-schema-architecture{,.verification}.md`
and were not committed. The human can decide whether to keep them as a
historical artifact or delete them. Their design diverged from the landed
schema (different `EventWiringKind` lift, `IReadOnlyDictionary<string, string>`
properties bag, different sub-form nesting model) — none of those choices were
adopted into the final schema.

**Next session orchestrator recommendation (Session 2):**
Claude opus @ effort=high

**Rationale:** Session 1's router-invoked Codex failed on C# synthesis, requiring a costly handoff to Claude Opus. Session 2 is a similar synthesis task (schema + extractor), making a direct, in-conversation Opus session the most reliable and cost-effective approach.

---

## Session 2 actuals (appended by Claude Opus 4.7 [1M] orchestrator)

**Orchestrator used:** Claude Opus 4.7 [1M context] @ effort=high.
The schema and extractors were authored in-conversation per the
`project-guidance.md -> Conventions -> Workflow Expectations -> "When
the orchestrator's model matches the routing target..."` Convention.
No `route(architecture)` call was made; the only paid calls were the
three rounds of cross-provider verification and the
next-orchestrator recommendation.

**Total routed cost — Session 2:**
- Round-1 cross-provider verification: $0.250 (gpt-5-4)
- Round-2 cross-provider verification: $0.094 (gpt-5-4)
- Round-3 cross-provider verification: $0.022 (gpt-5-4)
- Next-orchestrator recommendation: $0.006 (gemini-pro)
- **Total:** $0.372

**Verifier verdict:** ISSUES_FOUND in round 1 (2 Major, 4 Minor) — all
addressed; round 2 ISSUES_FOUND (1 Partially-resolved Major) —
addressed via type-level docstring tightening; round 3 VERIFIED.
Session 2 commits with no unresolved Major / Critical issues.

**Deviations from recommendation:** None. The Session 1 -> Session 2
handoff recommendation was Claude Opus @ effort=high, and that is what
ran. The Opus-orchestrator-handles-synthesis-in-conversation
Convention saved a routed Opus synthesis call (estimated $1.50–$2.00
per Session 1's actuals).

**Notes for Session 3 calibration:** The verifier flagged two Major
schema-level findings on round 1 that required an additional typed
slot (`QueryOutputColumn`) and explicit raw-IR coverage-gap disclosure
on `TableMetadata.ValidationRule` / `ValidationText`. Both are
schema-shape commitments worth catching before fixture authoring;
making them now avoids a contract change once Session 4 / Session 5
extractor wiring is in flight. The patches build and test clean
(127 tests passing, up from 95).

**Next session orchestrator recommendation (Session 3):**
Codex (gpt-5-4) @ effort=low

**Rationale:** Session 3 is mechanical Python work involving file I/O
and driving an existing `test-builder/` tool, which does not require a
high-tier synthesis orchestrator like Opus. Codex at low effort is
cost-effective and well-suited for reliable, multi-step tool-use and
code generation tasks like authoring test fixtures.
