# AI Assignment — access-resolver-symbol-table-integration

## Session 1: Synthetic Declaration Injection (Synthesis)

### Recommended orchestrator
anthropic claude-opus-4-7 @ effort=high

### Rationale
The core task is a high-complexity architecture synthesis
(`complexity_hint=75`) that the spec explicitly allows handling
in-conversation. Opus is required for this level of synthesis and for
managing the large context from multiple prerequisite documents and
schemas (#9 resolver, #10 metadata, #11a type-library registry).

### Estimated routed cost
High

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read Phase 4.4, prereq outputs, and test fixtures | in-conversation |
| 2 | Synthesise the architecture for synthetic declaration injection | in-conversation (per spec note for Opus orchestrator) |
| 3 | Land injection visitor and integration types under `AccessIntegration/` | in-conversation |
| 4 | Build + run tests | in-conversation |
| 5 | Cross-provider session verification | route(task_type="session-verification") → gpt-5-4 |

### Actuals (Session 1)
- **Orchestrator used:** claude-opus-4-7 @ effort=max (trigger phrase
  included `— maxout Claude`).
- **Total routed cost:** ~$0.44 across the session set so far
  (gemini-pro analysis $0.0066 + three gpt-5-4 verification rounds
  totalling $0.43).
- **Deviations from recommendation:** Effort was max rather than high
  due to the maxout suffix; this is the human's window-budget override
  per project-guidance "Token-Window Budget Overrides".
- **Verification rounds:**
  - Round 1: ISSUES_FOUND — 1 Major (bare-control identifier resolution
    in form/report modules) + 2 Minor (test-coverage gap on the
    null-registry path; OwnerTypeName documentation mismatch). All
    fixed in-session.
  - Round 2: ISSUES_FOUND — 1 Minor (precedence collision between
    bare-control lookup and top-level / built-in lookup not pinned by
    test). Fixed by adding `BareControl_ShadowsTopLevelForms` and
    `BareControl_ShadowsBuiltInFunction`.
  - Round 3: VERIFIED.
- **Test footprint:** 1135 tests pass (388 in resolver tests, up from
  starting baseline of ~317 by ~71). 0 failures, 2 Windows-only skips.
- **Notes for next-session calibration:** Session 2 is
  mechanical-correspondence work — pin to Sonnet per Convention. The
  registry's `TryResolveControlOnForm` dual-key (ModuleName, Name)
  index handled the maxout-Claude orchestrator's questions about
  fixture coverage cleanly; Session 2's fixtures (01, 02, 03) all have
  `ModuleName: null`, so the bare-form-name path is the one that gets
  exercised end-to-end.

---

**Next-session orchestrator recommendation (Session 2):** claude
sonnet-4-6 @ effort=high.
**Rationale:** Session 2's mechanical-correspondence workload (running
resolver over fixtures, schema-validating against expected.json,
landing tightening commits) is the designated, cost-effective use case
for Sonnet per the project-guidance Convention "Pin
mechanical-correspondence task types to Sonnet rather than Opus." The
synthesis-grade reasoning load Session 1 carried is gone; Session 2 is
"compare two structured outputs, fix mismatches" — Sonnet handles that
at a fraction of Opus's per-token cost.
