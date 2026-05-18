## Session 2: Black-box Smoke Tests + Install Docs + Closeout

### Recommended orchestrator
claude-code opus @ effort=high

### Rationale
This session mixes mechanical coding with high-context synthesis for documentation and final closeout artifacts. A top-tier model ensures high quality for both task types, providing a reliable close for the phase.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Author path-resolution helper | `task_type: code_generation` → `gpt-5-4` |
| 2 | Author black-box smoke test | `task_type: code_generation` → `gpt-5-4` |
| 3 | Author install / invoke docs | `task_type: prose_generation` → `opus` |
| 4 | Implement `--version` flag | `task_type: code_generation` → `gpt-5-4` |
| 5 | Run build and test suite | `N/A (local execution)` |
| 6 | Cross-provider session verification | `task_type: session_verification` → `gemini-pro` |
| 7 | Author closeout artifacts | `task_type: prose_generation` → `opus` |