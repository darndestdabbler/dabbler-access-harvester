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
| 4 of 6  | Wire form/report extractor against fixtures                  | `schema-validation`, `code-generation`| `google gemini-flash @ effort=low`             |
|         | **Actuals:**                                                 |                                     |                                                |
| 5 of 6  | Wire query/table extractor and land integration tests        | `schema-validation`, `code-generation`| `AI Router -> opus @ effort=medium`            |
|         | **Actuals:**                                                 |                                     |                                                |
| 6 of 6  | (Buffer) Address verifier-surfaced issues                    | `refactoring`, `analysis`           | `AI Router -> gemini-pro`                      |
|         | **Actuals:**                                                 |                                     |                                                |
| **All** | **Session Verification**                                     | `session-verification`              | `AI Router -> opus` (Cross-provider for Codex) |

---

## Session 4 actuals (appended by Codex orchestrator)

**Orchestrator used:** Codex (GPT-5 family) @ default coding effort.

**Total routed cost — Session 4:**
- Schema-validation guidance: $0.004 (gemini-pro)
- Cross-provider verification: $0.079 (opus)
- Next-orchestrator recommendation: $0.002 (gemini-pro)
- **Total:** $0.085

**Verifier verdict:** No blocking issues surfaced. The verifier called out the existing `ResolveProjectRoot()` / `AppContext.BaseDirectory` coupling as a dependency to remember, but the `.csproj` already copies `SyntheticDbs/` to the output directory and the build/test pass confirms the harness works as written.

**Deviations from recommendation:** The original Session 4 row pointed at Gemini Flash, but the actual work stayed small enough to complete comfortably in Codex with one routed schema-validation check and one routed verification pass.

**Notes for Session 5 calibration:** The generic snapshot harness now covers every committed `*.expected.json` fixture pair under `SyntheticDbs/`. Session 5 will be deeper extractor wiring, so the routed recommendation moved up to Opus rather than the original Flash guess.

**Next session orchestrator recommendation (Session 5):**
anthropic/claude-3-opus-20240229/Medium

**Rationale:** Session 5 is no longer just fixture plumbing. It needs query/table extractor wiring across the new synthetic cases, and the routed analysis favored a top-tier Anthropic orchestrator to reduce rework on the richer extraction logic.
```
