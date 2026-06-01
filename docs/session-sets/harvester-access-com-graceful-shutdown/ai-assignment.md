# AI Assignment — `harvester-access-com-graceful-shutdown`

> Routing plan + actuals for this 2-session set. The **recommendation**
> sections below were authored via `route(task_type="analysis")` (model:
> `gemini-pro`) per workflow Step 3.5 — the orchestrator never self-opines
> on which model is cheapest. The **Actuals** section is filled in at each
> session's closeout.

---

## Recommended Orchestrator

-   **Session 1**: The assigned orchestrator, Claude Code (Opus 4.8), is appropriate. The session involves precise, low-level C# modifications including P/Invoke, COM interop, and process lifecycle management. Orchestrating these tasks requires a high-capability model to maintain context, manage state, and generate correct, non-trivial system-level code.

-   **Session 2**: **Gemini Pro**. This session's primary tasks are running integration tests, executing a simple `taskkill` refactor, and wiring documentation files. These are less complex code-generation tasks and more about environment interaction and file management. A mid-tier, cost-effective model like Gemini Pro is sufficient and economically optimal for this orchestration role.

## Estimated Routed Cost

**Medium**. Session 1 requires routing for three distinct, non-trivial tasks: architectural design for the `IProcessController` seam, generation of a new hermetic unit test suite, and a final cross-provider session verification. While the orchestrator handles the direct code edits, these delegated tasks represent a moderate spend.

## Step -> Routing Decision (Session 1)

| Step | Task | Route or Direct? | task_type / model | Rationale |
| :--- | :--- | :--- | :--- | :--- |
| 1 | Capture Access PID | Direct | Orchestrator (Opus 4.8) | Mechanical edit to a single method in one file; within orchestrator scope. |
| 2 | Strengthen `Dispose()` | Direct | Orchestrator (Opus 4.8) | Localized addition of try/catch and process waiting logic; a direct edit. |
| 3 | Implement selective force-kill | Direct | Orchestrator (Opus 4.8) | Conditional logic addition to an existing method; a direct edit. |
| 4 | Extract seam, write tests | Route (tests) | test-generation(35, Gemini Pro) | The seam itself is **spec-prescribed** (the interface signature is given verbatim in the spec + implementation guide), so it is mechanical implementation, not an open architecture decision — built directly. The hermetic unit-test suite (4 enumerated cases) is routed as `test-generation`. |
| 5 | Build and run hermetic tests | Direct | Orchestrator (Opus 4.8) | Standard build/test execution falls under direct orchestrator responsibility. |
| 6 | Cross-provider verification | Route | session-verification(70, cross-provider via gpt-5-4) | Session verification is always routed to a different provider for quality assurance, per delegation discipline. |

> **Orchestrator note on Step 4 (architecture routing):** the routed
> analysis suggested routing the seam design as `architecture(80, Opus)`.
> Overridden to *direct*: the seam is not an open design question — the
> spec (step 4) and [`docs/access-com-zombie-cleanup.md`](../../access-com-zombie-cleanup.md)
> already prescribe the exact `IProcessController` signature
> (`IsRunning` / `WaitForExit` / `Kill`) and the kill-decision logic. There
> is no architecture synthesis to delegate; implementing a fully-specified
> seam in new <50-line files is mechanical orchestrator work. The
> test-generation routing is honored.

## Next-Session Recommendation

For Session 2, orchestrate with **Gemini Pro** for economic efficiency, as the tasks are primarily integration test execution and documentation updates. The orchestrator's critical precondition is to verify it is operating on a Windows host with a licensed, installed version of Microsoft Access before executing any COM-dependent code or tests. This environment check is mandatory for session success.

---

## Actuals

### Session 1 — closed 2026-06-01

- **Orchestrator used:** Claude Code / Opus 4.8 (`claude-opus-4-8`), effort `medium`.
- **Routed calls (5, total ~$0.45):**
  - `analysis` → `gemini-pro` ($0.0076) — this routing recommendation (Step 3.5).
  - `test-generation` → `gemini-pro` ($0.0149) — the hermetic `ComProcessReaperTests` suite.
  - `session-verification` → `gpt-5-4` ($0.1376) — round 1 (ISSUES_FOUND, 2 Major).
  - `session-verification` → `gpt-5-4` ($0.1427) — round 2 (ISSUES_FOUND, 1 new Major).
  - `session-verification` → `gpt-5-4` ($0.1487) — round 3 (**VERIFIED**).
- **Deviations from plan:** Step 4 seam design was built **directly** (not routed as
  `architecture`) — the seam was fully spec-prescribed, so there was no design to delegate
  (see the Step 4 orchestrator note above). Three rounds of cross-provider verification were
  needed (budget allows 2 retries); all three findings were accepted and fixed, none dismissed.
- **Notes:** A pre-existing wedged `testhost` zombie (PID 8772, COM-RPC-hung, predating this
  session) held the `AccessHarvester.Tests` DLL locks and blocked the first build; `taskkill`
  and `Stop-Process` could not reap it, but WMI `Win32_Process.Terminate` did. This is exactly
  the failure class the set targets. Verifier findings: (R1) `Dispose()` `Quit`/`FinalRelease`
  share-a-try gap; (R1) `Kill` missing `InvalidOperationException` catch; (R2) constructor
  leak if `OpenCurrentDatabase` throws — all fixed, with `Dispose()` and the constructor-failure
  path now sharing an idempotent `TeardownCom()`.

### Session 2 — _(not yet started)_
