# Next Session Set Recommendation

The standing plan should be followed. The next session set is **F2: harvester-finish-canonical-golden-output**.

-   **Plan Confirmation**: The project's standing plan correctly identifies `harvester-finish-canonical-golden-output` as the next priority (F2) after this set (F1.5).
-   **Prerequisite Satisfaction**: F2's prerequisites are now satisfied. F1 (`harvester-finish-canonical-parse-success-report`) is complete, and this set's primary output—the regenerated golden files with attribute headers stripped—has been committed.
-   **Session 1 Recommendation**: Recommended orchestrator: Claude Opus 4.7 @ effort=high. The initial session will require significant spec elaboration and synthesis to define the canonical output format, justifying the use of the more powerful model.

## Deviation Candidates

Two findings from the `vba-attribute-header-strip` audit present viable deviation candidates. A decision should be made whether to insert a small, targeted session set to address one or both before commencing F2.

1.  **Candidate: `vba-astbuilder-recovery-guards`**
    -   **Rationale**: F2 will re-exercise `LoadAndParse` across the corpus. The `AstBuilder` NullReferenceException (Follow-up Finding 1) is a hard crash that will likely be re-triggered, blocking progress on F2. A preceding micro-set to implement recovery guards would be a pragmatic, defensive measure.

2.  **Candidate: Address Remaining Parse Failures**
    -   **Rationale**: F2's goal is to produce canonical golden *output*, which presupposes successful parsing. The 7 modules across HHC and Northwind that still fail to parse represent known grammar gaps. Attempting to generate golden output for a corpus with known parse failures is inefficient. Addressing these failures first would make the F2 set more focused and productive.