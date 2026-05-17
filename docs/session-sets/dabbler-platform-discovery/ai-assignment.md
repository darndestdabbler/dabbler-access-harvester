# AI Assignment: dabbler-platform-discovery

This assignment directs the AI orchestrator for each session in the `dabbler-platform-discovery` session set. The orchestrator's primary role is to select the appropriate `task_type` for each step, which the AI Router then uses to select a specific model based on `router-config.yaml`. This document provides the recommended orchestrator and routing decisions for each session.

---

## Session 1 of 3: Inventory + Schema

- **Recommended orchestrator:** `anthropic/claude-sonnet-4-6` at `effort=medium`
- **Rationale:** Session 1's work is mechanical inventory + structured documentation, not synthesis — per the spec's own routing notes ("Sonnet 4.6 at effort=medium across all three sessions; Opus is over-spend"). The routed `analysis` calls for Feature 1 and Feature 2 already pull Gemini Pro at tier 2; a Sonnet orchestrator drives them at low marginal cost.
- **Estimated routed cost:** moderate

**Per-step Routing Decision table**

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1.1 | Generate component inventory from `../dabbler-platform/` source. | `task_type="analysis"` |
| 1.2 | Generate `report.rdf.json` schema documentation. | `task_type="analysis"` |
| 1.3 | Verify session outputs for accuracy and completeness. | `task_type="session-verification"` |
| 1.4 | Generate change-log for committed artifacts. | `task_type="summarization"` (last session only — Session 1 does NOT generate a change log) |

**Actuals (filled after the session)**
- **Actual orchestrator:** `anthropic/claude-opus-4-7` at `effort=high` — **human override** of the routed recommendation. The human explicitly chose Opus for Session 1 at the start; the routed recommendation (Sonnet 4.6 at medium) was authored after the fact and represents what the router would have picked if asked first.
- **Actual routed cost:** moderate (~$0.66 across Step 3.5 + Feature 1 + Feature 2 + Step 6 verify + Step 7 third-opinion + Step 7 redo Feature 1 + Step 7 redo Feature 2 + Step 6 round 2 verify; see cost report)
- **Session outcome:** VERIFIED on round 2 after fixup. Round 1 returned ISSUES_FOUND with 4 Major + 1 Minor; Issues 1–3 were addressed by re-routing both deliverables with full per-component grounding for the inventory and explicit Sorting/Expressions coverage for the schema doc; Issue 4 (sample `.rdf.json` copying) was adjudicated via an Opus tiebreaker that picked option C (synthetic minimal examples + see-also citations); Issue 5 is this annotation.
- **Key observations:** The "no copying of platform source code" Boundaries clause was read by the verifier as forbidding even small cited JSON-fixture snippets. The tiebreaker reading (synthetic + see-also pointer) is a stronger Feature 2 contract than verbatim quotes and was adopted on round 2.

**Next-session orchestrator recommendation (Session 2):**
- **Engine:** `claude-code`
- **Model:** `claude-opus-4-7`
- **Effort:** `high`
- **Rationale (routed via `task_type=analysis`, gemini-pro):** "Given Session 1 required a high-effort Opus run and multiple verification rounds to satisfy the spec, using the same orchestrator for Session 2's complex analysis task minimizes the risk of similar churn and rework."

---

## Session 2 of 3: Migration Map + PDF Boundary

- **Recommended orchestrator:** `anthropic/claude-sonnet-4-6` at `effort=medium`
- **Rationale:** This session's work involves cross-referencing and documenting from the structured artifacts created in Session 1, which is more synthesis than novel discovery. A cost-effective, mid-tier model is well-suited for these structured analysis and documentation tasks, avoiding the over-spend of a frontier model.
- **Estimated routed cost:** moderate

**Per-step Routing Decision table**

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 2.1 | Generate migration-relevance map, cross-referencing Access constructs and platform components. | `task_type="analysis"` |
| 2.2 | Document the `dabbler-pdf` public API boundary. | `task_type="documentation"` |
| 2.3 | Verify session outputs for logical consistency and correctness. | `task_type="session-verification"` |
| 2.4 | Generate change-log for committed artifacts. | `task_type="summarization"` |

**Actuals (filled after the session)**
- **Actual orchestrator:**
- **Actual routed cost:**
- **Session outcome:**
- **Key observations:**

*See Session 3 for the next orchestrator recommendation.*

---

## Session 3 of 3: Smoke-Test + Buffer

- **Recommended orchestrator:** `anthropic/claude-sonnet-4-6` at `effort=medium`
- **Rationale:** The final session focuses on validation and refinement by applying the generated maps to a concrete example. This requires strong reasoning within a narrow scope, making a mid-tier model a cost-effective choice for validating prior work and addressing any verifier-flagged issues.
- **Estimated routed cost:** low

**Per-step Routing Decision table**

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 3.1 | Perform smoke-test walkthrough using a sample Access form. | `task_type="synthesis"` |
| 3.2 | Address any verifier-surfaced findings from previous sessions. | `task_type="code-gen"` |
| 3.3 | Verify final session outputs. | `task_type="session-verification"` |
| 3.4 | Generate final change-log for the session set. | `task_type="summarization"` |

**Actuals (filled after the session)**
- **Actual orchestrator:**
- **Actual routed cost:**
- **Session outcome:**
- **Key observations:**