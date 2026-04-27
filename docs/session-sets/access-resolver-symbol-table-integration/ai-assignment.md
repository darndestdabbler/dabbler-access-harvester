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

---

## Session 2: Cross-Cut + Mechanical Tightening

### Recommended orchestrator
claude sonnet-4-6 @ effort=high

### Rationale
Mechanical-correspondence session: schema-validation, cross-cut test authoring,
fixture correspondence. Sonnet at high effort is the mechanical-gate target per
project-guidance Convention "Pin mechanical-correspondence task types to Sonnet
rather than Opus." No synthesis-grade reasoning required.

### Estimated routed cost
Low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Register session start, verify prereqs | in-conversation |
| 2 | Verify Session 1 outputs exist | in-conversation |
| 3 | Write FixtureCrossCutTests.cs (12 hermetic tests) | in-conversation (Sonnet = mechanical-gate target per project-guidance Convention) |
| 4 | Schema-validation against expected.json | in-conversation (Sonnet = mechanical-gate target) |
| 5 | Build + test | in-conversation |
| 6 | Cross-provider session verification | route(task_type="session-verification") → gpt-5-4 |

### Actuals (Session 2)
- **Orchestrator used:** claude-sonnet-4-6 @ effort=high
- **Total routed cost:** ~$0.23 (2× gpt-5-4 verification: $0.14 round 1, $0.09 round 2; plus $0.003 next-orc analysis = ~$0.23)
- **Deviations from recommendation:** None.
- **Verification rounds:**
  - Round 1: ISSUES_FOUND — 1 Major (missing `Fixture02_MeDotTxtCaseworkerPhone_ResolvesToSyntheticTextBoxControl` test), 1 Minor (embedded VBA vs. file loading — deferred by design per "lean by default"). Fixed Major in-session.
  - Round 2: VERIFIED.
- **Test footprint:** 1148 passed (400 resolver tests, 12 new in FixtureCrossCutTests.cs), 0 failed, 2 Windows-only skips.
- **Notes for Session 3 calibration:** Session 3 is the optional buffer session (verifier-surfaced cleanup). Session 2 VERIFIED cleanly after one round of fixes. Session 3 scope is very light — a gemini-flash or low-effort Sonnet pass is sufficient.

---

**Next-session orchestrator recommendation (Session 3):** gemini-flash
@ effort=low (or sonnet @ effort=low if mechanical cleanup is needed).
**Rationale:** Session 3 is an explicit optional buffer for verifier-surfaced
cleanup. Session 2 VERIFIED after a single Major fix, leaving no major work for
Session 3. The cheapest available model (gemini-flash, tier 1) is appropriate
for any minor cleanup, documentation touches, or a minimal close-out commit.
If the orchestrator finds non-trivial work (e.g., a verifier-surfaced architecture
question), escalate to Sonnet. Routed analysis recommends Haiku-class effort,
which maps to gemini-flash in the router's configured tier 1.

---

## Session 3: Close-out (Optional Buffer)

### Recommended orchestrator
gemini-flash @ effort=low (per Session 2 recommendation)

### Rationale
Session 3 is the optional buffer — reserved for verifier-surfaced cleanup. Session 2 VERIFIED cleanly after one fix round. No major work carried over. The cheapest tier-1 model is appropriate for a close-out commit.

### Estimated routed cost
Minimal

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Register session start, review deferred Minor | in-conversation |
| 2 | Confirm deferred Minor stays deferred | in-conversation |
| 3 | Build + test | in-conversation |
| 4 | Generate change-log.md | route(task_type="summarization", max_tier=1) → gemini-flash |
| 5 | Route next-set recommendation | route(task_type="analysis") → gemini-pro |
| 6 | Cross-provider session verification | route(task_type="session-verification") → gpt-5-4 |

### Actuals (Session 3)
- **Orchestrator used:** claude-sonnet-4-6 @ effort=high (Sonnet used per human trigger phrase; gemini-flash recommended but not actioned — current Claude Code environment defaulted to Sonnet)
- **Total routed cost:** ~$0.009 (gemini-flash change-log $0.0006 + gemini-pro rec $0.0035 + gpt-5-4 verification $0.0057)
- **Deviations from recommendation:** Orchestrator was Sonnet (not gemini-flash) due to the Claude Code environment. Cost negligible since no synthesis work was performed; all routed tasks were cheap.
- **Verification:** VERIFIED on Round 1. No issues.
- **Test footprint:** 1148 passed, 0 failed, 2 Windows-only skips. No changes from Session 2.
- **Notes:** Set is complete. Next set is #13 jet-sql-resolution-and-dynamic (prereqs #11b and #12 both complete).

---

**Next-session-set recommendation:** `jet-sql-resolution-and-dynamic` (#13)
**Rationale:** Both prereqs (#11b access-resolver-symbol-table-integration and #12 jet-sql-grammar-and-parser) are now complete. #13 is the only unblocked set. It covers Phase 5.3-5.4 (form-reference detection in SQL + dynamic SQL analysis). Contains 2 synthesis steps — recommend Opus 4.7 @ effort=high for Session 1 (architecture synthesis).
