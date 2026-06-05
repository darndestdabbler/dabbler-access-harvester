# AI Assignment — 002-harvester-cleanup-golden-refresh-and-release

## Session 1: Stale-Artifact Refresh (Cleanup Goldens + Dead Code + Feedback Doc)

### Recommended orchestrator
anthropic/claude-3.5-sonnet @ effort=medium

### Rationale
The session is mostly mechanical (file regeneration, code deletion, doc annotation) which can be routed to cheap models. A mid-tier orchestrator provides sufficient reasoning to manage the workflow without the cost of a max-tier engine.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | `confirm-fresh-behavior` | Confirm current cleanup behavior on a fresh run. | `task_type=execution, max_tier=0` |
| 2 | `regen-goldens` | Regenerate committed cleanup golden fixtures. | `task_type=execution, max_tier=0` |
| 3 | `prune-dead-code` | Remove or document `CrossReferenceDetector` and its tests. | `task_type=code_analysis, max_tier=1` |
| 4 | `refresh-feedback-doc` | Refresh `harvester-upstream-feedback.md` with resolutions. | `task_type=documentation, max_tier=0` |
| 5 | `build-test` | Build solution and run hermetic tests. | `task_type=execution, max_tier=0` |
| 6 | `verify` | Cross-provider verification of changes and scope discipline. | `task_type=verification, max_tier=2` |
| 7 | `closeout` | Update session state and append actuals to assignment. | `task_type=file_edit, max_tier=0` |

### Actuals (filled after session)
- Orchestrator used: claude-code (Claude Opus 4.8 @ effort=medium) — ran higher-tier than the
  recommended 3.5-sonnet, but the work was mechanical so reasoning cost was minimal.
- Total routed cost: low (cross-provider verification gpt-5-4 dominated; regen/doc steps were
  direct file work, not routed).
- Deviations from recommendation: none material. Session stayed in scope (no classifier logic);
  COM-vs-no-com regeneration decision routed through cross-provider consensus per the spec.
- Notes for next-session calibration: verification routed correctly to a different provider
  (gpt-5-4). Session 2 is release mechanics; mid-tier orchestrator is fine.

**Next-session orchestrator recommendation (Session 2):**
anthropic/claude-3.5-sonnet @ effort=medium
Rationale: The release session is also primarily mechanical; a mid-tier orchestrator remains the most cost-effective choice.

## Session 2: Release v1.2.0

### Recommended orchestrator
anthropic/claude-3.5-sonnet @ effort=medium

### Rationale
This session is a mechanical release process (build, package, tag, publish) where most steps route to cheap models. A mid-tier orchestrator is cost-effective, reserving higher-tier models only for release-notes synthesis and final verification.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | `confirm-clean-main` | Confirm `main` is clean with all tests green. | `task_type=execution, max_tier=0` |
| 2 | `build-package` | Build release binary, package zip, compute SHA256. | `task_type=execution, max_tier=0` |
| 3 | `release-notes` | Author release notes summarizing improvements since v1.1.0. | `task_type=summarization, max_tier=1` |
| 4 | `tag-and-publish` | Tag `v1.2.0` and publish the GitHub release. | `task_type=execution, max_tier=0` |
| 5 | `update-install-doc` | Update `install.md` to reflect `v1.2.0`. | `task_type=documentation, max_tier=0` |
| 6 | `verify` | Cross-provider verification of release artifacts and docs. | `task_type=verification, max_tier=2` |
| 7 | `closeout` | Author change-log and other closeout documents. | `task_type=summarization, max_tier=1` |

### Actuals (filled after session)
- Orchestrator used: claude-code (Claude Opus 4.8 @ effort=medium).
- Total routed cost: ~$0.24 (release-notes documentation gemini-pro ~$0.023; session-verification
  gpt-5-4 R1 ~$0.140 + R2 ~$0.037; change-log documentation gemini-pro ~$0.027;
  next-set-recommendation analysis gemini-pro ~$0.003). Auto-verify on the release-notes
  documentation task added a cross-provider gpt-5-4-mini pass.
- Deviations from recommendation: (a) Release notes deliberately covered ALL consumer-visible
  changes since the v1.1.0 tag, not just sets 001/002 — the spec's Step-4 list was a subset and
  v1.1.0 was never published; cross-provider verifier confirmed this is correct, not scope creep.
  (b) Used the established CI tag-push -> draft -> maintainer-publish path (CI artifact authoritative)
  rather than uploading a locally-built artifact; the local and CI SHAs differed, validating that
  choice. (c) The full COM integration test tier was excluded due to a known testhost post-COM
  shutdown wedge (reaped via WMI); acceptable per verifier since the session changed no product code.
- Notes for next-session calibration / maintainer: ROUTER BUG — task_type='summarization' currently
  returns HTTP 400 from gemini-2.5-pro even on a trivial prompt (documentation/analysis/general
  paths work); the change-log was routed via task_type='documentation' as a workaround. Worth a
  router-config fix. Set is FINAL — no further sessions; repo is consumer-driven-only.
