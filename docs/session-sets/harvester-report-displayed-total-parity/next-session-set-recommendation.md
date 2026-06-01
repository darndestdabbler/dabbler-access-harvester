# Next-Session-Set Recommendation (routed)

> Produced via `route(task_type="analysis")` at the close of
> `harvester-report-displayed-total-parity` Session 2 (Rule 17: never
> self-opine on the next orchestrator). Model: gemini-pro.

1.  **Session Set:** `harvester-fix-classify-timeout`
    This session set will investigate and resolve the persistent, reproducible timeout in the `ClassifyCommandTests.HomeHealthCare_classify...` test. The work involves profiling the command's execution against the large `HomeHealthCare.accdb` database to identify the performance bottleneck—likely within the value-sampling logic across its numerous tables—and implementing a fix to ensure the test completes reliably and efficiently, unblocking the test suite.

2.  **Narrowing:** Yes, drop the `sanitize` investigation. The re-measurement confirms it was a non-reproducible flake, whereas the `classify` failure is a persistent, blocking issue requiring focused effort.

3.  **Orchestrator:** Anthropic/Claude 3 Sonnet, Low effort.

4.  **Rationale:** The task is a well-defined performance debugging problem, not novel architecture, making a fast, cost-effective coding model the most efficient choice.
