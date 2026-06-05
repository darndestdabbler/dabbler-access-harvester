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
*   **Actual orchestrator:**
*   **Actual cost:**
*   **Notes:**

## Session 3: DESTRUCTIVE History Rewrite Execution

**Recommended orchestrator:** gemini-pro @ low
**Rationale:** This session executes a pre-vetted, destructive command sequence where procedural precision is paramount. A cost-effective, mid-tier model is sufficient for orchestrating the exact, pre-planned steps without needing novel reasoning.
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
**Next-session orchestrator recommendation:** gemini-pro