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
- Orchestrator used: claude-code (Claude Opus 4.8 @ effort=high) — matched the
  recommended top-tier orchestrator; the allowlist curation was genuine judgment
  (5 judgment-call paths inspected + classified) and the embedded-residue finding
  required scope discipline, both warranting the tier.
- Total routed cost: ~$0.385 — judgment-call classification (gemini-pro analysis
  $0.005) + Session-3 orchestrator rec (gemini-pro $0.002) + session-verification
  3 rounds (gpt-5-4: R1 $0.142 + R2 $0.158 + R3 $0.075 = $0.375). All work steps
  (file inspection, allowlist authoring, backup, dry-run) handled directly per
  Delegation Discipline (mechanical/single-file).
- Deviations from recommendation: (a) The recommended Session-2 model literal was
  "claude-3-opus" (a stale router emission); mapped to the current top tier
  claude-opus-4-8. (b) MAJOR unplanned finding: the embedded-residue scan surfaced
  123 keep-files (incl. 2 binary SQLite DBs with full proprietary source) that the
  narrow allowlist will NOT remove — recorded per spec stop-rules, deferred to a
  follow-up set by maintainer decision. (c) Verification took 3 rounds (not the
  hoped-for 1) — the gpt-5-4 verifier caught a real count error and, valuably, that
  the first exhaustiveness proof was built on `git rev-list --objects` (which
  blob-dedups pathnames); the proof was redone path-complete via `ls-tree`.
- Notes for next-session calibration: Session 3 is destructive (filter-repo rewrite
  + force-push). Two hard operational facts discovered this session that S3 must
  carry: the host git sets `safe.bareRepository=explicit`, so filter-repo needs a
  `GIT_CONFIG_COUNT=1 / GIT_CONFIG_KEY_0=safe.bareRepository / GIT_CONFIG_VALUE_0=all`
  env override; and filter-repo is invoked via `.venv/Scripts` on PATH. This clone
  is a plain (non-bare) single-worktree checkout — reconcile the spec's bare-repo
  worktree-re-establishment step against actual layout at S3 preflight. Backup
  bundle is at C:/temp/dabbler-access-harvester-backups/ (sha256 recorded).

**Next-session orchestrator recommendation (Session 3):**
claude-code (anthropic) claude-opus-4-8 @ effort=high
Rationale (routed, gemini-pro, capability-match): continuity with the orchestrator
that authored + dry-ran the VERIFIED allowlist and found the safe.bareRepository
quirk minimizes execution risk on the irreversible rewrite/force-push.

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
- Orchestrator used: claude-code (Claude Opus 4.8 @ effort=high) — NOT the
  recommended openai gpt-4o@max. The routed Session-3 recommendation (gemini-pro,
  do-not-self-opine) had called for claude-code@high for continuity (this
  orchestrator authored + dry-ran the VERIFIED allowlist and found the
  safe.bareRepository quirk); the human ran the session with this orchestrator
  accordingly. The destructive command sequence is mechanical, but the judgment
  load (gate adjudication, the bare-vs-plain layout reconciliation, the
  credential-helper failure recovery, evidence-typo fix) justified the tier.
- Total routed cost: ~$0.36 — next-set analysis (gemini-pro, do-not-self-opine,
  ~$0.01) + session-verification gpt-5-4 across 2 rounds (R1 ISSUES_FOUND $0.120 +
  R2 VERIFIED $0.124 = $0.244). All work steps (filter-repo, force-push, verify,
  re-establishment) handled directly per Delegation Discipline — they are
  mechanical command execution, not routable reasoning.
- Deviations from recommendation: (a) Orchestrator was claude-code, not gpt-4o
  (per the routed continuity rec, see above). (b) LAYOUT: spec Steps 2/5 assume
  the bare-repo+flat-worktree layout; the repo is a PLAIN single-worktree checkout,
  so the rewrite ran on a throwaway --mirror clone and re-establishment was
  reset+gc of the one checkout (disclosed in the verification prompt; verifier
  agreed it is a disclosed deviation, not a defect). (c) AUTH: the fresh --mirror
  clone's inherited GCM credential helper hung under the non-interactive shell;
  the reliable push used gh as the SOLE credential helper
  (`-c credential.helper= -c credential.helper='!gh auth git-credential'`).
- Notes for next-session calibration: Two reusable operational facts for the
  follow-up set 004's destructive session — (1) push from a fresh clone via
  `gh auth git-credential` with the helper list reset, do NOT rely on the
  inherited GCM helper (it pops a GUI dialog that hangs headless); (2) prove
  rewrite exactness over the path-complete `ls-tree`-union universe, never the
  `rev-list --objects` surrogate. R1's only finding was a transcription typo in
  the evidence table (caught by gpt-5-4), not a substantive rewrite error.