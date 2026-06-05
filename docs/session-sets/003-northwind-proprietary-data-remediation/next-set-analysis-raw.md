### 1. Next Session Set

Start **`004-northwind-symbol-db-sqlite-residue`**.

**Reasoning:** Session set 003 is complete, but it explicitly deferred the known, secondary footprint of proprietary data. This residue, especially the serialized source code in binary SQLite databases, represents a continuing intellectual property risk and makes the remediation from 003 incomplete. This is the designated and logical next step.

### 2. Recommended Orchestrator for Session 1

**Session 1 Task:** "forward removal/skip-hardening" — This is an analysis and triage task to identify the precise locations of proprietary residue within ~123 text and binary files, and to script the interaction with the two SQLite databases.

**Recommendation:**
*   **Engine Profile:** An orchestrator with state-of-the-art code generation and complex instruction following. The primary challenge is not just identifying residue in text, but generating precise SQL/Python scripts to safely query and stage updates for the binary SQLite databases (`symbol-db.sqlite`, `grades.sqlite`) without corrupting them.
*   **Model Candidates:**
    *   **Codex (GPT-5.x):** Suited for the heavy code-generation requirement to script the inspection of the binary SQLite files.
    *   **Claude-code (Anthropic, Opus 4.8):** Suited for its large context window and reasoning capabilities, which are necessary for analyzing the ~123 files and the `embedded-residue-findings.md` to form a consistent redaction plan.
*   **Effort:** **Medium.** This session requires careful analysis and the generation of scripts for interacting with the binary database formats, which is more complex than text-file manipulation.

### 3. Sequencing Caveats

*   **Release:** **Do not cut a release.** The repository `main` branch contains known proprietary data. A release would formalize and distribute that data. All releases must be blocked until set 004 is complete.
*   **Prerequisites:**
    1.  **Backup:** Before starting Session 2 ("curate allowlist + backup + dry-run"), create a tagged, full backup of the remote repository.
    2.  **Test Fixture Migration:** The work to re-base characterization and translation tests onto the `HomeHealthCare` fixture must be completed and merged *before* the final execution session that removes the data. Otherwise, tests will break with no baseline for validation.
*   **Parallelism Risk:**
    *   **Code Freeze:** The `main` branch must be frozen during the execution of this session set. Any commits made to `main` after the analysis starts but before the final force-push will be lost or require complex, manual intervention.
    *   **Developer Workflow:** All developers must be notified that a history rewrite and force-push are imminent. They will need to re-base or re-clone after completion. Un-merged work from branches based on the old history will be complicated to integrate.