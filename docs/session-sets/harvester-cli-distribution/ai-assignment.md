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
- Orchestrator used: <TBD>
- Total routed cost: <TBD>
- Deviations from recommendation: <TBD>
- Notes for next-session calibration: <TBD>

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
- Orchestrator used: <TBD>
- Total routed cost: <TBD>
- Deviations from recommendation: <TBD>
- Notes for next-session calibration: <TBD>

**Next-session orchestrator recommendation (Session 3):** N/A — last session of the set; next-session-set recommendation will be authored at closeout.