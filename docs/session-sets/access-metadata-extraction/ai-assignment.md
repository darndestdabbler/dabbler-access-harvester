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
