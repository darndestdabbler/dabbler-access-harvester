# Change Log — access-object-extractor-spike

This session set focused on a read-only research spike to compare existing Access object extraction methods (VBA-based YAML extractor vs. C# extractors) and inform future integration strategy. The primary deliverable is a `gap-analysis-and-strategy.md` document.

**Summary of Discoveries:**

The existing YAML and C# extractors cover similar Access objects (Tables, Queries, Relationships, Forms, Reports, Macros, Modules). However, the YAML extractor provides significantly richer, structured metadata critical for AI interpretation, including table DDL with FKs, detailed field properties (InputMask, Format), and structured form/report control arrays with event bindings. The C# `SaveAsTextExporter` for Forms/Reports produces unstructured text, which is largely opaque to AI without further parsing.

**Follow-on Recommendations:**

1.  **Integration Strategy:**
    *   **Short-term:** Diverge. Utilize the existing YAML extractor's rich output for immediate AI interpretation tasks.
    *   **Long-term:** Inform. Back-port the unique, valuable fields discovered in the YAML extractor (e.g., structured controls, detailed DDL) into the C# extractors to achieve unified, high-fidelity extraction.

2.  **Follow-on Session Sets:**
    *   `table-extractor-coverage` (1-2 sessions): To define the precise scope and implementation details for enhancing table extraction in C#.
    *   `structured-form-report-extractor` (3-4 sessions): To design and begin implementing structured form and report metadata extraction within the C# codebase.

3.  **Tooling:** Promote `scripts/run_extraction.ps1` to a first-class developer tool, ensuring it is documented and used for local validation.