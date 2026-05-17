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
| 2.4 | Generate change-log for committed artifacts. | `task_type="summarization"` (last session only — Session 2 does NOT generate a change log) |

**Actuals (filled after the session)**
- **Actual orchestrator:** `anthropic/claude-opus-4-7` at `effort=high` — followed Session 1's routed next-orc recommendation; the spec's original recommendation (Sonnet 4.6 at medium) was again departed from because Session 1's three-round verification history made Opus's stronger spec-interpretation grounding the lower-risk choice for Feature 3's cross-reference work.
- **Actual routed cost:** ~$0.74 across 5 calls (Feature 3 migration map gemini-pro, Feature 4 PDF boundary gemini-pro + auto-verify gpt-5-4-mini, round-1 verify gpt-5-4, round-2 verify gpt-5-4; next-orc gemini-pro). See cost report.
- **Session outcome:** VERIFIED on round 2. Round 1 surfaced 1 Major (currency-header convention for `dabbler-pdf-overview.md`). Orchestrator disagreed (per-artifact pin is more honest; Session 1 driver already split into platform/pdf header variants as precedent), surfaced to human, human routed third opinion to Codex GPT-5 — which sided with the verifier (spec text is controlling); hybrid fix applied (standard `../dabbler-platform` header + body note recording the dabbler-pdf SHA/date and NuGet pin). `record_adjudication` logged context-gap/accept-finding. Round 2 returned VERIFIED with zero issues.
- **Key observations:** (1) `task_type="documentation"` invokes the router's auto-verification path (gpt-5-4-mini); `result.content` contains both the generated doc AND an appended `**Verification (model, provider):** VERDICT\nIssue 1...` block. The current driver saves `result.content` verbatim, so the appendix landed in the file and had to be manually stripped. Future driver work should split on the `---\n**Verification (` boundary before saving narrative outputs. (2) Auto-verification for `documentation` produced three useful findings (overstated `PdfPageRenderer` claim from partial source; thin surface table; over-strong `THIRD-PARTY-NOTICES.txt` wording) — addressing them before Step 6 verification made the cross-provider round cleaner. (3) Spec's literal Feature 5 template is controlling per Codex tiebreaker; the per-artifact pin precedent baked into the Session 1 driver did not survive adjudication. Future cross-repo specs should clarify the snapshot-pin convention upfront if per-artifact pins are intended.

*See Session 3 for the next orchestrator recommendation.*

---

## Session 3 of 3: Smoke-Test + Buffer

- **Recommended orchestrator:** `anthropic/claude-sonnet-4-6` at `effort=low` (routed via `task_type=analysis` after Session 2 closeout — gemini-pro)
- **Rationale (routed):** "This session's tasks are validation and cleanup, not novel synthesis, making a cost-effective model the appropriate choice per the spec's original guidance." Session 2's verify pass closed cleanly on round 2 with one Major adjudication that is fully resolved on disk; Session 3 will not face the same volume of spec-interpretation pressure that justified Opus for Sessions 1-2. Sonnet at low effort is the right calibration for: (a) hand-walking one Northwind form through the migration map and noting any gaps, (b) generating the change-log, and (c) generating disposition.json. Authoring the next-session-set recommendation will route through `task_type=analysis` per the spec.
- **Estimated routed cost:** low

**Per-step Routing Decision table**

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 3.1 | Perform smoke-test walkthrough using a sample Access form. | `task_type="synthesis"` |
| 3.2 | Address any verifier-surfaced findings from previous sessions. | `task_type="code-gen"` |
| 3.3 | Verify final session outputs. | `task_type="session-verification"` |
| 3.4 | Generate final change-log for the session set. | `task_type="summarization"` |

**Actuals (filled after the session)**
- **Actual orchestrator:** `anthropic/claude-opus-4-7` at `effort=high` — **human override** of the routed recommendation (Sonnet 4.6 at low effort, authored via gemini-pro at Session 2 close). The human picked Opus 4.7 to keep the orchestrator consistent across the entire set; Session 3's work (smoke-test analysis routed to gemini-pro, plus mechanical map amendments) was not the kind of synthesis-pressure case that justified Opus on its own merits.
- **Actual routed cost:** ~$0.13 across 2 calls (smoke-test analysis to gemini-pro $0.0321; cross-provider verification to gpt-5-4 $0.0978). Plus the next-session-set routed analysis ($0.01 est.) at Step 8 below. See cost report.
- **Session outcome:** VERIFIED on round 1 with zero issues. The smoke-test of `frmOrderDetails` + `sfrmOrderLineItems` against the migration map surfaced 6 concrete gaps (3 Major: calculated controls, cross-form references, UDFs in ControlSource; 3 Minor: ComboBox RowSource types, button intent, StatusBarText). All 6 gaps closed by 8 added/amended rows in the migration map (7 to Form table, 1 to Subform table) plus a refinement to the Form section's Open-gaps note. gpt-5-4 verifier confirmed coverage, plausibility against the pinned platform snapshot, and consistency with cross-cutting notes.
- **Key observations:** (1) The smoke-test paid for itself many times over — $0.03 of gemini-pro analysis produced six specific, actionable gap proposals that were each adopted essentially as-written. The "by hand" framing in the spec didn't preclude routing the analysis; routing a different provider through the form is itself a useful independent check on the map. (2) Round-1 VERIFIED with zero issues is a notable contrast to Sessions 1 and 2 (both required multi-round verification + adjudication). The likely reasons: (a) the changes were purely additive table rows against an existing structure, no new interpretive claims; (b) the verification prompt explicitly grounded the verifier with the smoke-test artifact, which acted as a structured worksheet of expected coverage; (c) the spec-boundary disclosures pre-empted the "is this re-snapshotting?" question that bit Session 2. (3) Choosing one form that exercised cross-form expressions, UDFs, ComboBox-of-both-row-source-types, and domain-workflow buttons was the right cost/coverage trade — a simpler form (e.g., a single-record dialog) would have left the calculated-control and cross-form gaps undiscovered.

*This is the last session of the set — see disposition.json and change-log.md for the closeout artifacts and next-session-set recommendation.*