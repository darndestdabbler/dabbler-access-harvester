# AI Assignment — table-extractor-coverage

## Session 1 of 2: Core Additions (Table + Relationship)

### Recommended orchestrator
Claude Code sonnet @ effort=medium

### Rationale
The task involves adding well-defined fields to existing C# classes and a moderately complex DDL reconstruction. A Tier 2 coding model like Sonnet is sufficient for this structured task without needing a Tier 3 model's architectural reasoning.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read source files + YAML | Direct (file I/O) |
| 2 | Add `ValidationRule` and `ValidationText` | Route (code-gen) |
| 3 | Add `InputMask`, `Format`, `DisplayControl` | Route (code-gen) |
| 4 | Add DDL reconstruction helper method | Route (code-gen) |
| 5 | Add explicit bools to `RelationshipExporter` | Route (code-gen) |
| 6 | Route code review | Route (review) |
| 7 | `dotnet build && dotnet test` | Direct (local command) |
| 8 | Route session verification | Route (verification) |

### Actuals (filled after the session)
- Orchestrator used: Claude Code claude-sonnet-4-6 @ effort=high
- Total routed cost: $0.2846 (5 routed calls: ai-assignment via gemini-pro $0.0103; code-review via sonnet $0.0877; verification round 1 via gpt-5-4 $0.1023; verification round 2 via gpt-5-4 $0.0638; plus gemini-pro verification auto-verify on code-review)
- Deviations from recommendation: DDL reconstruction deferred to Session 2 per spec clause (requires db access in BuildTablePayload for FK clauses); code changes were all direct edits not routed (per Delegation Discipline — mechanical, single-file, under ~50 lines); verification required 2 rounds due to context-gap false-positive in round 1 (session-reviews file)
- Notes for next-session calibration: Session 1 went smoothly. All 4 spec goals completed except DDL (deferred). Session 2 should implement DDL first, then add integration test assertions, then YAML comparison. The `BuildTablePayload` signature change to accept `db` is the main architectural decision for Session 2.

**Next-session orchestrator recommendation (Session 2):**
Claude Code sonnet @ effort=medium
Rationale: Session 2 continues the C# development with a focus on DDL reconstruction, integration test assertions, and YAML comparison — all structured tasks that Sonnet handles well at medium effort.

---

## Session 2 of 2: Tests, YAML Comparison, and Gap Close

### Recommended orchestrator
Claude Code sonnet @ effort=medium

### Rationale
The session focuses on writing hermetic and integration tests for the new C# code, a core competency for Tier 2 coding models. Sonnet provides the best balance of capability and cost for this structured testing task.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Verify prior session committed | Direct (git log) |
| 2 | If DDL reconstruction was deferred, implement it | Route (code-gen) |
| 3 | Write hermetic unit tests for every new field | Route (code-gen) |
| 4 | Write or update the integration test assertion | Route (code-gen) |
| 5 | Run extraction script and compare output | Direct (local command) then Route (analysis) |
| 6 | Route code review | Route (review) |
| 7 | `dotnet build && dotnet test` | Direct (local command) |
| 8 | Route session verification | Route (verification) |
| 9 | Generate change-log and next-session-set recommendation | Route (summarization) |

### Actuals (filled after the session)
- Orchestrator used: TBD
- Total routed cost: TBD
- Deviations from recommendation: TBD
- Notes for next-session calibration: TBD