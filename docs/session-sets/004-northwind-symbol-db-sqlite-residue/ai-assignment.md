# Session Set 004: Northwind Symbol DB SQLite Residue Removal

**Goal:** remove proprietary NorthwindDevEdition-derived symbol-DB SQLite binaries + derived consistency data, first from HEAD then from all git history via a gated git filter-repo rewrite + force-push.

**Involves:** C# test edits, .gitignore updates, git history rewrite, documentation updates.

**RequiresUAT:** false
**RequiresE2E:** false

## Session 1: Forward Removal + Test/Tooling Skip-Hardening

**Recommended orchestrator:** claude-code (Claude Opus 4.8) @ high
**Rationale:** The session requires nuanced C# code analysis to locate proprietary fixtures and mechanical-but-context-aware edits to add skip guards. A high-capability model ensures accuracy in both the analysis and implementation phases.
**Estimated routed cost:** moderate

| Step | Action                                                                                                                    | Routing Decision    |
|------|---------------------------------------------------------------------------------------------------------------------------|---------------------|
| 1    | Inventory tracked symbol-db data, classify scrub-vs-keep, and locate `CrossModuleResolutionTests.BuildNorthwind()` source. | gemini-pro (tier 2) |
| 2    | Add skip-when-absent guards to all symbol-db-data-dependent hermetic tests using the `SkippableFact` pattern.              | local/no-route      |
| 3    | Execute `git rm --cached` on the 7 identified proprietary data artifacts.                                                  | local/no-route      |
| 4    | Add corresponding entries to the root `.gitignore` file.                                                                   | local/no-route      |
| 5    | Update project guidance and tools READMEs to note the proprietary data is now local-only.                                 | local/no-route      |
| 6    | Build solution and run affected hermetic tests twice: once with artifacts present, once with them moved aside.            | local/no-route      |
| 7    | Perform cross-provider verification of the session's work against the goal description.                                   | gpt-5-4 (tier 3)    |
| 8    | Closeout: commit changes, write commit message, prepare for next session.                                                 | local/no-route      |

### Actuals (filled after the session)
*   **Actual orchestrator:** claude-code (Claude Opus 4.8) @ high
*   **Actual routed cost:** ~$0.106 — ai-assignment analysis (gemini-pro, $0.0138) +
    cross-provider session-verification (gpt-5-4, $0.0923). No verification retries.
*   **Deviations from recommendation:** None on orchestrator (ran on the recommended
    claude-code Opus). Step 1 inventory + Step 2 skip-guard design were handled
    in-conversation rather than routed to gemini-pro: the classification was largely
    pre-settled by the spec and grounded in reading file contents the orchestrator had
    to inspect directly, and the skip-guard work was mechanical C# edits mirroring the
    003 SkippableFact pattern — both under the Delegation Discipline "mechanical /
    in-context" bar. Verification correctly routed cross-provider to gpt-5-4.
*   **Notes for next-session calibration:** Session 1 verified VERIFIED on round 1.
    Session 2 recommendation stands (cross-provider orchestrator for the destructive-prep
    curation + dry-run); if Session 2 runs on Codex/OpenAI, swap the
    `session-verification` router override from gpt-5-4 to opus.

## Session 2: History Rewrite Analysis and Preparation

**Recommended orchestrator:** gpt-5-4 @ moderate
**Rationale:** The session's core task is curating a precise path allowlist and its rationale for a destructive operation, then verifying a dry run. This demands high-fidelity analysis and logical reasoning to prevent accidental data loss during the rewrite.
**Estimated routed cost:** moderate

| Step | Action                                                                                                       | Routing Decision    |
|------|--------------------------------------------------------------------------------------------------------------|---------------------|
| 1    | Curate the final, complete scrub allowlist of paths to be removed from history and document the rationale.     | opus (tier 3)       |
| 2    | Take a full, local backup bundle of the repository using `git bundle`.                                       | local/no-route      |
| 3    | On a throwaway `--mirror` clone, dry-run `git filter-repo --invert-paths` with the curated allowlist.        | local/no-route      |
| 4    | Verify path-complete exactness: script a comparison proving `(BEFORE - allowlist) == AFTER`.                 | gemini-pro (tier 2) |
| 5    | Document the exact, verified `git filter-repo` and `git push` command sequence for Session 3 execution.      | local/no-route      |
| 6    | Closeout: review the plan, confirm readiness for the destructive rewrite.                                    | local/no-route      |

### Actuals (filled after the session)
*   **Actual orchestrator:** github-copilot (OpenAI GPT-5.4) @ medium
*   **Actual routed cost:** ~$0.0774 total: gemini-pro allowlist classification
    ($0.0064) + opus session-verification ($0.06375) + gemini-pro closeout /
    next-orchestrator follow-ups ($0.00724).
*   **Deviations from recommendation:** The original step-level plan expected an
    Opus-class pass for the initial allowlist curation. In practice the candidate
    set was already narrowed to a verified 47-path historical universe, so a
    gemini-pro classification pass was sufficient and the remaining curation /
    dry-run proof work stayed mechanical-local. Because this session ran on an
    OpenAI orchestrator, verification used a temporary local router-config copy
    that swapped `session-verification` from `gpt-5-4` to `opus` so the review
    stayed cross-provider. One extra gemini-pro follow-up was needed because the
    first Session-3 recommendation came back as a non-schema-compatible human
    stop-gate rather than a concrete next-orchestrator block.
*   **Notes for next-session calibration:** Session 2 verified `VERIFIED` on
    round 1. Session 3 should switch to `claude-code` / Anthropic so OpenAI can
    remain the closeout verifier for the destructive rewrite session; the
    maintainer go-ahead gate remains mandatory before any rewrite or force-push.

## Session 3: DESTRUCTIVE History Rewrite Execution

**Recommended orchestrator:** claude-code (Claude Opus 4.8) @ low
**Rationale:** Session 2 already completed the reasoning-heavy part: the allowlist, backup path, dry-run proof, and exact command sequence are all pinned. Session 3 is mostly mechanical execution plus a hard human go-ahead gate. Switching away from the Session-2 OpenAI orchestrator preserves a clean OpenAI cross-provider verifier for the destructive closeout without reopening the plan.
**Estimated routed cost:** low

| Step | Action                                                                                                             | Routing Decision |
|------|--------------------------------------------------------------------------------------------------------------------|------------------|
| 1    | Await and confirm explicit go-ahead from a repository maintainer before proceeding.                                | local/no-route   |
| 2    | Execute the pre-verified `git filter-repo --invert-paths` command against the primary repository clone.            | local/no-route   |
| 3    | Force-push all rewritten branches and tags to the `origin` remote using the pre-verified push command.             | local/no-route   |
| 4    | Re-establish local repository layout (e.g., re-clone, restore local-only files from backup).                       | local/no-route   |
| 5    | Record the GitHub unreachable-object caveat in project documentation (rewritten commits are not immediately GC'd). | local/no-route   |
| 6    | Perform final verification by comparing the live repository state against the session's description of work.       | local/no-route   |
| 7    | Closeout: Communicate completion to the team, archive session materials.                                           | local/no-route   |

### Actuals (filled after the session)
*   **Actual orchestrator:**
*   **Actual cost:**
*   **Notes:**

---
**Next-session orchestrator recommendation:** claude-code (Claude Opus 4.8) @ low