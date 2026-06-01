# AI Assignment — `harvester-fix-classify-timeout`

> Routing plan + actuals for this 2-session reliability spike. The
> **recommendation** sections below were authored via
> `route(task_type="analysis")` (model: `gemini-pro`) per workflow Step 3.5 — the
> orchestrator never self-opines on which model is cheapest. The **Actuals**
> section is filled in at each session's closeout.

---

## Recommended Orchestrator (per session, with rationale)
*   **Session 2:** Claude Code (Opus 4.8)
    *   **Rationale:** The primary task is implementing a `Process.Terminate`-based shutdown guard for a COM-interop CLI. This requires nuanced understanding of C# process lifecycle management, potential race conditions with stdout/stderr flushing, and careful scoping to avoid unintended side effects. The orchestrator's high capability is necessary to author this non-trivial, reliability-critical code correctly.

## Estimated Routed Cost (Low/Medium/High + why)
*   **Medium**
    *   **Rationale:** The session has one mandatory, high-cost, cross-provider verification call to `gpt-5-4`. While the core implementation will be handled directly by the high-capability orchestrator, the plan includes cost-optimization by potentially routing documentation tasks. The cost is not High as there are no large-scale analysis or iterative refactoring loops planned.

## Step -> Routing Decision (Session 2)
| Step | Task | Route or Direct? | task_type/model | Rationale |
| :--- | :--- | :--- | :--- | :--- |
| 1 | Add "Property not found" HRESULT to `OleDbDaoSchemaReader.DaoPropertyAbsentHResults` + hermetic test. | Direct | `code-gen/Claude Opus 4.8` | Trivial one-line change and associated unit test. The orchestrator can complete this without delegation. |
| 2 | Implement scoped `flush-then-TerminateProcess` shutdown guard in `AccessHarvester.Cli/Program.cs`. | Direct | `code-gen/Claude Opus 4.8` | **Critical Path.** Requires high-capability model to correctly handle process lifecycle, stream flushing, and conditional logic for COM-dependent commands. The orchestrator is the designated expert. |
| 3 | Run `AccessHarvester.Integration` tests, reaping wedged processes with WMI `Win32_Process.Terminate`. | Direct | `scripting/Claude Opus 4.8` | Orchestrator generates the test execution and cleanup script. Requires OS-level process management knowledge. |
| 4 | Update `lessons-learned.md` and `ROADMAP.md` with findings from the spike. | Route | `text-gen/Claude 3.5 Sonnet` | Cost optimization. Standard documentation task suitable for a cheaper, capable model. |
| 5 | **Session Verification:** Verify solution correctness, blast radius, and adherence to scoped implementation. | Route | `verification/gpt-5-4` | **Mandatory.** Cross-provider verification override ensures solution robustness and guards against orchestrator blind spots. |

## Next-Session Recommendation
*   **Orchestrator:** Claude Code (Opus 4.8). The final session set is the `harvester-sql-migration-generator` capstone, a significant feature requiring strong architectural and code-generation capabilities.
*   **Host Precondition:** Session execution requires a **Windows host with Microsoft Access Database Engine 2016 Redistributable** installed to support the COM-based integration test suite.

---

## Actuals

### Session 1 — closed 2026-06-01

- **Orchestrator used:** Claude Code / Opus 4.8 (`claude-opus-4-8`), effort `high`.
  Session 1 was started in a prior conversation (steps 1–2 partial: reproduce +
  static analysis) and **resumed** here to complete the owed per-phase timed run,
  the root-cause finding, and the recommendation.
- **Routed calls (3, total ~$0.069):**
  - `analysis` → `gpt-5-4` ($0.0519) — cross-provider consensus opinion 1 on
    fix-vs-quarantine (recommended Option 3 / quarantine).
  - `analysis` → `gemini-pro` ($0.0092) — cross-provider consensus opinion 2
    (recommended Option 2 / hard-terminate).
  - `analysis` → `gemini-pro` ($0.0077) — this ai-assignment.md routing plan.
  - (Step 6 `session-verification` → `gpt-5-4` is appended below after it runs.)
- **Diagnosis was done directly (not routed):** the timing harness, the direct
  CLI runs, the two `Environment.Exit` / `TerminateProcess` fix-direction probes,
  and the WMI zombie reaping are mechanical orchestrator work (run commands,
  observe, revert). The two reasoning calls that WERE routed are the design
  decision (fix-vs-quarantine consensus) and this routing plan.
- **Headline outcome:** the spec's premise was **overturned by evidence** — the
  240 s timeout is a CLI **process-shutdown COM-un-init wedge**, not product
  latency and not the `DISTINCT` cardinality (0.06 s). See
  [`root-cause-findings.md`](root-cause-findings.md). The human locked Session 2
  to **Option 2 scoped** (flush-then-`TerminateProcess` on the COM command path)
  with quarantine as fallback, and **folded in** the E5 `Property not found`
  HRESULT fix.
- **Deviations from plan:** none material to the workflow; the routed analysis
  recommended Session 2 stay on Claude Opus, which matches the consumer-driven
  trajectory. The cross-provider consensus split (1 quarantine / 1 fix) was
  escalated to the human per the decision-consensus rule.
