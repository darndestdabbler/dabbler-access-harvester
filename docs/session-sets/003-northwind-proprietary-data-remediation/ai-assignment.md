# AI Assignment — 003-northwind-proprietary-data-remediation

## Session 1: Forward Removal + Test Skip-Hardening (non-destructive)

### Recommended orchestrator
openai gpt-4o @ effort=medium

### Rationale
The tasks are well-defined, non-destructive code and configuration edits that follow existing patterns. A capable model at medium effort can execute these mechanical changes reliably with low risk.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Inventory tracked files at HEAD | task_type=code_gen, max_tier=hermetic |
| 2 | Add skip-guards to dependent tests | task_type=code_gen, max_tier=hermetic |
| 3 | Untrack golden tree and update .gitignore | task_type=code_gen, max_tier=hermetic |
| 4 | Update project documentation | task_type=prose_gen, max_tier=hermetic |
| 5 | Build and test with/without local golden | task_type=code_gen, max_tier=hermetic |
| 6 | Cross-provider verification | task_type=verification, max_tier=cross_provider |
| 7 | Session closeout | task_type=prose_gen, max_tier=hermetic |

### Actuals (filled after the session)
- Orchestrator used: claude-code (Claude Opus 4.8 @ effort=medium) — ran higher-tier than
  the recommended gpt-4o, but the work was mechanical (git rm --cached, gitignore + test
  skip-guards, doc edits) so reasoning cost was minimal; the one judgment call (the spec-premise
  discrepancy) was surfaced to the maintainer rather than decided solo.
- Total routed cost: ~$0.16 (ai-assignment analysis gemini-pro ~$0.019; session-verification
  gpt-5-4 R1 ~$0.114 + R2 ~$0.025). No work steps were routed — all file edits were mechanical
  and handled directly per Delegation Discipline.
- Deviations from recommendation: (a) Scope expanded by maintainer approval to also untrack the
  two NorthwindDevEdition.accdb mirror files (FormsAndReportsYaml YAML + parse-success-report
  JSON) found still tracked at HEAD — the spec's Step-1 premise that they were 'already
  gitignored' was factually wrong. (b) 408 deletions total (406 golden + 2 mirrors) vs the
  spec's implied 406. (c) The routing-decision table's task_type/max_tier labels are
  router-invented placeholders, not this repo's actual task types; ignored in favor of direct
  mechanical handling.
- Notes for next-session calibration: session-verification routed correctly cross-provider
  (gpt-5-4) per the calibrated override. Verifier surfaced one real Minor doc-consistency issue
  (stale 'Secondary' label on HomeHealthCare) — fixed, R2 VERIFIED. Session 2 is allowlist
  curation (judgment-heavy) — keep a top-tier orchestrator; the destructive rewrite is Session 3.

**Next-session orchestrator recommendation (Session 2):**
anthropic claude-3-opus @ effort=high
Rationale: Session 2 requires nuanced reasoning and content analysis to curate the scrub allowlist, a critical judgment task that benefits from a top-tier model.

---

## Session 2: Curate Scrub Allowlist + Tooling + Backup (analysis/prep, no rewrite)

### Recommended orchestrator
anthropic claude-3-opus @ effort=high

### Rationale
This session's core task is curating a scrub allowlist by inspecting file contents, which requires strong reasoning and judgment. A top-tier model is necessary to minimize the risk of misclassifying paths before the destructive next session.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Install `git-filter-repo` tooling | task_type=code_gen, max_tier=hermetic |
| 2 | Curate scrub allowlist with rationale | task_type=reasoning, max_tier=cross_provider |
| 3 | Write allowlist manifest files | task_type=prose_gen, max_tier=hermetic |
| 4 | Scan for embedded proprietary residue | task_type=reasoning, max_tier=hermetic |
| 5 | Take full repository backup bundle | task_type=code_gen, max_tier=hermetic |
| 6 | Dry-run rewrite on a throwaway clone | task_type=code_gen, max_tier=hermetic |
| 7 | Cross-provider verification | task_type=verification, max_tier=cross_provider |
| 8 | Session closeout | task_type=prose_gen, max_tier=hermetic |

### Actuals (filled after the session)
- Orchestrator used: (TBD)
- Total routed cost: (TBD)
- Deviations from recommendation: (TBD)
- Notes for next-session calibration: (TBD)

**Next-session orchestrator recommendation (Session 3):**
openai gpt-4o @ effort=max
Rationale: Session 3 is a destructive, high-stakes procedure requiring maximum precision and reliability for execution of irreversible commands.

---

## Session 3: Execute History Rewrite + Force-Push (DESTRUCTIVE — gated)

### Recommended orchestrator
openai gpt-4o @ effort=max

### Rationale
This session is a destructive, irreversible, and outward-facing procedure requiring flawless execution of a precise sequence of commands. Maximum effort from a top-tier model is mandated to mitigate the high risk.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Pre-flight gate confirmation | task_type=reasoning, max_tier=hermetic |
| 2 | Run `git filter-repo` on bare repo | task_type=code_gen, max_tier=hermetic |
| 3 | Verify local rewrite completeness | task_type=code_gen, max_tier=hermetic |
| 4 | Force-push all refs and tags | task_type=code_gen, max_tier=hermetic |
| 5 | Re-establish worktrees | task_type=code_gen, max_tier=hermetic |
| 6 | Document GitHub unreachable-object caveat | task_type=prose_gen, max_tier=hermetic |
| 7 | Cross-provider verification | task_type=verification, max_tier=cross_provider |
| 8 | Session closeout and reporting | task_type=prose_gen, max_tier=hermetic |

### Actuals (filled after the session)
- Orchestrator used: (TBD)
- Total routed cost: (TBD)
- Deviations from recommendation: (TBD)
- Notes for next-session calibration: (TBD)