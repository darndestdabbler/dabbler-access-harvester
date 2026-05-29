# ai-assignment.md — harvester-cli-distribution

## Session 1 of 2: Channel Decision + Publish-Flow Wiring

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=high

### Rationale
Session 1 combines a key architectural decision (the release channel) with non-trivial scripting to produce a deterministic release artifact. Claude Opus provides the strong reasoning and coding capabilities needed for this mix of planning and implementation.

### Estimated routed cost
low ($0.03-$0.08)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read project docs | `in-conversation` |
| 2 | Pick release channel and artifact shape | `route(task_type="planning")` |
| 3 | Update publish script to produce artifact bundle | `in-conversation` |
| 4 | Add SHA-256 hashing to publish script and verify determinism | `in-conversation` |
| 5 | Smoke-validate the bundled executable | `in-conversation` |
| 6 | `dotnet build && dotnet test` | `in-conversation` |
| 7 | End-of-session verification | `route(task_type="session-verification", provider="gemini-cli")` |

### Actuals (filled after the session)
- Orchestrator used: claude-code claude-opus-4-7 @ effort=medium (deviation: spec recommended `high`, ran at `medium`; sufficient for the work)
- Total routed cost: ≈$0.02 (planning routed to gemini-pro; verification fell back to gemini-pro after OpenAI 429)
- Deviations from recommendation: effort=medium not high; verification provider was the fallback rather than the preferred one
- Notes for next-session calibration: Session 2's YAML-author + extensive doc-rewrite scope is the larger work; carry effort=medium forward unless the workflow YAML proves harder than expected

**Next-session orchestrator recommendation (Session 2):** claude-code claude-opus-4-7 @ effort=high

---

## Session 2 of 2: CI Workflow + Docs + First Release

### Recommended orchestrator
claude-code claude-opus-4-7 @ effort=high

### Rationale
Session 2 involves CI/CD workflow authoring and extensive documentation updates across multiple files, requiring strong context-awareness. Claude Opus is well-suited for both the structured YAML generation and the coherent narrative prose required for the documentation.

### Estimated routed cost
moderate ($0.05-$0.15)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read Session 1 review and channel decision | `in-conversation` |
| 2 | Author release workflow (`.github/workflows/`) or runbook (`docs/`) | `in-conversation` |
| 3 | Rewrite `docs/harvester/install.md` | `in-conversation` |
| 4 | Update `README.md`, `CLAUDE.md`, `AGENTS.md`, `GEMINI.md`, `docs/cross-repo/*.md` | `route(task_type="documentation")` (per file) |
| 5 | Cut first release (tag, push, monitor workflow) | `in-conversation` |
| 6 | Validate install from a consumer perspective | `in-conversation` |
| 7 | `dotnet build && dotnet test` | `in-conversation` |
| 8 | End-of-session verification | `route(task_type="session-verification", provider="codex")` |
| 9 | Closeout (`disposition.json`, `change-log.md`, etc.) | `route(task_type="summarization")` for `change-log.md`; others `in-conversation` |

### Actuals (filled after the session)
- Orchestrator used: claude-code claude-opus-4-7 @ effort=medium (deviation: spec recommended `high`, ran at `medium`; the work was YAML authoring + doc rewrites that medium handled cleanly)
- Total routed cost: ≈$1.36 (architecture YAML author $0.45 + a wasted $0.73 architecture call lost to a UnicodeEncodeError print crash on the first try + install.md doc rewrite $0.01 to gemini-pro + verification $0.16 to gpt-5-4 + next-set analysis $0.00)
- Deviations from recommendation: effort=medium not high; one routed call ($0.73 architecture) was effectively wasted on a `UnicodeEncodeError` in the orchestrator's Python wrapper (rerun produced the canonical output); release was published in-session rather than left as a draft for manual approval (user approved publish after the consumer transcript verified); scope expanded to add the release pointer to CLAUDE/AGENTS/GEMINI even though those files did not previously reference the install path (user request)
- Notes for next-session calibration: when capturing routed-call output to stdout, always write-to-file FIRST then print, so the response is not lost to a Windows cp1252 print crash; the cost of the lost call equalled or exceeded the verification cost

**Next-session orchestrator recommendation (Session 3):** N/A — last session of the set; next-session-set recommendation authored at closeout (see [next-session-set-recommendation.md](next-session-set-recommendation.md): `harvester-sql-migration-generator`).