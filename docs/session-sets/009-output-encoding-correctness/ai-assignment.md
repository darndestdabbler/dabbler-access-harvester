# AI Assignment Retrospective: Set 009-output-encoding-correctness

## Per-Session Analysis

| Session | Task class                          | Orchestrator used | Verification model | Routed cost | Fit assessment |
| :------ | :---------------------------------- | :---------------- | :----------------- | :---------- | :------------- |
| 1       | Small targeted correctness fix + hermetic test authoring | Claude Opus 4.8   | GPT-5-4            | ~$0.2317    | Good           |

## Actuals Summary

Set 009-output-encoding-correctness, Session 1 (final), 2026-06-13:

*   **Orchestrator**: Claude Opus 4.8 (1M context). Effort: high, but within expected bounds for direct code/test authoring.
*   **Task Class**: Small, targeted encoding correctness fix (~2 lines in `SaveAsTextTranslator.BuildPayload`) plus 9 hermetic xUnit facts covering various `cp1252` scenarios, BOM precedence, and signature routing. Pure file-to-file logic.
*   **Re-grounding**: Empirical `cp1252`-vs-`UTF-8` probe confirmed the bug's persistence, adhering to an "evidence-over-inference" principle.
*   **Validation**: `dotnet build` (0 errors), successful execution of hermetic test project (9/9 new, 5/5 adjacent parser tests). No golden regen, no COM tier impact.
*   **Verification**: Cross-provider GPT-5-4.
    *   **Round 1**: Issues found (3 minor test posture findings: dropped test pre-registration, added `Module/ReadAllText`-path accented test, added UTF-8 BOM precedence test). Cost: $0.14075.
    *   **Round 2**: Verified. Cost: $0.090927.
    *   **Total Routed Verification Cost**: ~$0.2317.
*   **Orchestration Scope**: Opus handled all code and tests directly, as the work fell below the ~50-line delegation threshold for single-file mechanical fixes. The routed components were mandatory cross-provider verification and closeout artifacts.

## Cost/Model-Fit Analysis

Claude Opus 4.8 was a **good fit** as the orchestrator for this session.

*   **Cost**: While Opus is a premium model, its direct application for code authoring in this "mechanical fix" scenario did not incur routed token costs, effectively leveraging its capabilities without disproportionate expenditure. The routed costs were exclusively for cross-provider verification, which is a fixed process regardless of the orchestrator.
*   **Model Fit**: The task (small, self-contained encoding fix with robust hermetic test authoring) aligned well with Opus's strengths in precise code generation and adherence to detailed instructions. The "high effort" for the orchestrator reflects the thoroughness in re-grounding and crafting comprehensive tests, which is a desirable trait for correctness-focused tasks, even when mechanical. The model successfully identified and implemented the nuanced encoding correction and constructed a resilient test suite, including edge cases. The value-add here was the thoroughness and the strong verification posture established, rather than raw routing.

## Forward Recommendation for Next Set

**Next Set**: 010-output-contract-fidelity-batch (006 P2, 2 sessions)

**Recommendation**: For set 010, which involves major emission changes (query-type decode, ByVal), coordinated COM golden regeneration, and several minor fixes across 2 sessions, we recommend **GPT-5-4** as the orchestrator.

**Justification**: This next set is significantly larger and more complex, requiring sophisticated reasoning for:

1.  **Major Emission Changes**: Handling complex data transformations and new output contracts demands a model with superior contextual understanding and robust code generation capabilities for intricate logic.
2.  **Coordinated COM Golden Regeneration**: This implies cross-cutting changes, requiring strong architectural awareness and the ability to coordinate updates across multiple components and ensure compatibility.
3.  **Overall Complexity**: The increased scope and interdependencies favor a model known for its comprehensive problem-solving and higher-level synthesis capabilities, which are essential for managing multi-session efforts involving significant system-level impacts.

While Opus performs well on isolated tasks, GPT-5-4 is better suited for the strategic orchestration of complex, multi-faceted changes with system-wide implications and coordination requirements, especially concerning COM interactions and golden regeneration.