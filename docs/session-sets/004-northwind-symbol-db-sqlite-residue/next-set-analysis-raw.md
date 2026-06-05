**Recommendation:** Start session set `005` to remediate all remaining embedded residue.

**Rationale:** The `003` finding that "remediation is not complete" correctly identifies that proprietary structural information is a compliance liability, distinct from feature work. Leaving known proprietary residue in the repository history creates unacceptable, persistent risk and violates the goal of a clean separation. The "consumer-driven-only" posture does not apply to compliance mandates.

---

### Recommended Next Set: `005-embedded-residue-redaction`

**Objective:** Purge all remaining proprietary NorthwindDevEdition structural names from the repository's files and history via a second `git filter-repo` rewrite.

**Proposed Sessions (3):**

1.  **S1: Triage and Redaction-Map Authoring:** Systematically inventory all residue strings from the HIGH/MEDIUM/LOW buckets and the deferred C# analysis. Author a definitive `sed` or Python script that maps each proprietary string to a non-proprietary, generic replacement (e.g., `tbl_Orders` -> `[REDACTED_TABLE_1]`). This script will serve as the `--blob-callback` for the history rewrite.
2.  **S2: Dry-Run and Verification:** On a fresh, disposable `--mirror` clone, execute a dry-run of the `git filter-repo` command using the S1 script. Meticulously verify the transformation by inspecting rewritten commits to confirm complete, exact redaction without unintended side effects.
3.  **S3: Execution and Closeout:** Following the documented procedure from set `004`, take a fresh backup and execute the verified `git filter-repo` command on a new mirror clone. Force-push the rewritten history and tags, then re-establish the primary checkout.

### Session 1 Plan (`005-S1`)

*   **Orchestrator Engine:** Local machine with standard Unix tools (`grep`, `sed`, `git`) or Python. This is a text-processing and analysis task. No external orchestrator is required; it is the cheapest and most capable engine.
*   **Effort:** **Low**. This session is investigative and declarative, not procedural. The primary output is a text-based transformation script/map.

**Sequencing Caveat:** This set must be another destructive history rewrite. It cannot be addressed with a simple commit on `main`. The documented lessons from `004` regarding backups, mirror clones, and force-pushing must be strictly followed. This set must be completed before resuming any other work or considering new releases.