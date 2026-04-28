The coverage audit contains several critical discrepancies when compared against the canonical session-set tree. The primary issue is a systematic misattribution of components to incorrect producing session sets. Additionally, the audit omits all deliverables from session set #1 and invents a deliverable for set #16 that is not specified in the tree. The identified gaps (A and B) are correctly characterized and consistent with the project plan's scope.

### Discrepancy Analysis

1.  **Component Coverage Parity:** There are numerous critical errors in the audit's "Component → Test-Project Map".
    *   **VBA AST:** The audit attributes "VBA AST construction + parser pipeline" to sets #2-#5. The tree's row for set **#6** (`parser-infrastructure`) clearly assigns it `AccessHarvester.Vba.Parser/` and the AST infrastructure.
    *   **JET SQL Grammar:** The audit attributes "Jet SQL grammar + AST" to set #6. The tree assigns this to set **#12** (`jet-sql-grammar-and-parser`).
    *   **SQL Resolution:** The audit attributes "Form-reference detection, dynamic-SQL analyzer" to set #6 and later to set #12. The tree assigns this work to set **#13** (`jet-sql-resolution-and-dynamic`).
    *   **Enrichment Modules:** The audit's producing set numbers for the enrichment phase are consistently misaligned. The components attributed to sets #13, #14, and #15 were actually produced by sets **#14**, **#15**, and **#16**, respectively.

2.  **Gap Classification:** The audit's gap analysis is sound.
    *   **Gap A (Schema Validation):** The tree specifies a `harvester-schema.v1.0.json` in set #17. Identifying that no end-to-end test validates the actual pipeline output against this schema is a valid and important gap.
    *   **Gap B (CLI Tests):** The project's genesis (`accessexportertemp1-import` established `AccessHarvester.Cli`) confirms a CLI surface exists. Classifying the lack of black-box tests for it as a gap is correct.

3.  **Missed Components:**
    *   The audit **completely omits session set #1** (`parser-foundations`). Its deliverables (`tools/parser-harness/`, project wiring) are foundational and untracked in the audit.
    *   The audit omits non-code deliverables like the documentation from set #2 and fixtures from set #5, which is a minor oversight.

4.  **Spurious Claims:**
    *   The audit lists an `EnrichmentPipelineRunner` component produced by set #16. The tree's outputs for set #16 are `FunctionClassification.cs`, `NavigationGraph.cs`, and `UnresolvedItem.cs`. The "runner" is not a canonical deliverable of that set and this claim is unsupported by the tree.
    *   The audit references a test project `AccessHarvester.Tests` for metadata extraction work (set #10). The tree specifies `tests/AccessHarvester.Access.Tests/`. This is a minor path discrepancy.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "Component → Test-Project Map (multiple rows)",
      "summary": "Component deliverables are systematically attributed to the wrong producing session sets.",
      "fix": "Correct the 'Producing Set' column for all misattributed rows to align with the canonical `session-set-tree.md`. For example: VBA AST (#6), JET SQL grammar (#12), SQL resolution (#13), and enrichment modules (#14, #15, #16)."
    },
    {
      "severity": "Major",
      "location": "Component → Test-Project Map",
      "summary": "The audit omits all deliverables from session set #1 (`parser-foundations`).",
      "fix": "Add a new row to the audit table for session set #1, listing its deliverables (parser harness, project wiring) and determining their test coverage status."
    },
    {
      "severity": "Major",
      "location": "Audit Row: '#16 enrichment-pipeline runner'",
      "summary": "The audit claims set #16 produced an `EnrichmentPipelineRunner`, which is not a canonical deliverable listed in the session-set tree.",
      "fix": "Remove the spurious row for '#16 enrichment-pipeline runner'. If the associated tests exist, re-assign them to a canonical component from sets #14-#16."
    },
    {
      "severity": "Minor",
      "location": "Audit Row: '#2-#5 vba-grammar / parser'",
      "summary": "The producing set range for the VBA grammar incorrectly includes #2, which produced documentation, not grammar code.",
      "fix": "Change the 'Producing Set' value from '#2-#5' to '#3-#5'."
    },
    {
      "severity": "Minor",
      "location": "Audit Rows: '#10 access-metadata-extraction', '#11a access-type-library-registry'",
      "summary": "The audit lists `AccessHarvester.Tests` as the exercising test project, which differs from the `tests/AccessHarvester.Access.Tests/` path specified in the tree.",
      "fix": "Update the 'Exercising Test Project' path in the audit to match the canonical path `tests/AccessHarvester.Access.Tests/` from the session-set tree."
    }
  ]
}
```