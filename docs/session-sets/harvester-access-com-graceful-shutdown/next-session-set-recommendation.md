# Next Session-Set Recommendation

> Routed via `route(task_type="analysis")` at `harvester-access-com-graceful-shutdown` Session 2 closeout (final session). Orchestrator never self-opines on the next set.

**Routed model:** gemini-pro | **cost:** $0.0027

---

**(a) Slug:** `harvester-sql-migration-generator`

**(b) Rationale:** This work was explicitly deferred pending the completion of a reliable CI, a condition the `harvester-access-com-graceful-shutdown` set just satisfied. Unblocking deferred, core feature work takes priority over finishing an in-progress sibling set that was not blocked. This directly capitalizes on the newly established stability of the integration test environment.

**(c) Recommended orchestrator:** `Claude 3 Opus`. The task is to generate complex SQL migration logic, requiring the high-fidelity reasoning and code-generation capabilities Opus provides to minimize the risk of subtle bugs in database schema modifications.

**(d) Precondition:** The orchestrator must first execute a full, clean run of the COM/Access integration test suite. It must confirm a 100% pass rate with zero orphaned `msaccess.exe` or `testhost.exe` processes before generating any new feature code.
