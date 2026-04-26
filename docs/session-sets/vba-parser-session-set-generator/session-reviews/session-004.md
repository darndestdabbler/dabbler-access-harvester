# Verification Round 1

**Verdict:** `ISSUES_FOUND`

`opus effort=max` is the correct fix for Anthropic, and `providers.anthropic.timeout_seconds: 600` is a reasonable response to repeated 300-second timeouts. The material problems are in the playbook itself: the Sonnet-authored substitution did produce quality loss, evidenced by unresolved contradictions and partial coverage of the adopt-now requirements.

---

### 1
- **Severity:** Major
- **Issue:** The playbook contradicts retrospective row **#12** by proposing additional mandatory `route()` task types. Section 3 says `session-verification` is the **only** mandatory `route()` call, but Section 6 adds `schema-validation`, `parser-regression-check`, and `grammar-authoring` to `delegation.always_route_task_types`. That also conflicts with the per-category guidance that these can be done in-conversation when the orchestrator matches the target model.
- **Location:** `routing-playbook.md` §§3.1, 3.7, 3.8, 3.13, 6
- **Fix:** Remove those three entries from `delegation.always_route_task_types`, or rewrite §§3.1/3.7/3.8 to make them always-route everywhere. The adopt-now row points to the first fix: keep `session-verification` as the only mandatory `route()` type.

### 2
- **Severity:** Major
- **Issue:** `vba-spec-study` is classified inconsistently. The playbook says MS-VBAL summary / coverage / hazard-list work is `analysis` on `gemini-pro`, which implies **no Opus orchestrator escalation**. But the playbook example and the change log recommend `Opus@high` for `vba-spec-study` because it allegedly has “2 synthesis sessions.” This breaks internal consistency and weakens adopt-now row **#7**.
- **Location:** `routing-playbook.md` §3.6, §8 example; `change-log.md` “Next Steps”
- **Fix:** Pick one model/routing strategy and apply it everywhere. If `vba-spec-study` is `analysis`, update §8 and `change-log.md` to recommend Sonnet as orchestrator with routed `analysis` calls. If it truly needs Opus-grade synthesis, update §3.6 and the deliverable/task-type mapping accordingly.

### 3
- **Severity:** Major
- **Issue:** Verification-prompt assembly coverage is only partial, so retrospective row **#11** is not fully satisfied. Section 5 includes recipes for grammar, AST, resolver, schema validation, code review, and a generic full-session bundle, but it omits explicit recipes for several listed task categories: JET SQL analyzer design, enrichment taxonomy/structural design, spec-study/coverage/hazard sessions, parser regression, test-fixture authoring, ADR/summarization sessions, and the recommendation tasks.
- **Location:** `routing-playbook.md` §5, relative to §§3.4–3.6, 3.8–3.9, 3.11–3.15
- **Fix:** Add explicit recipe blocks for the missing categories, or explicitly collapse them into named shared recipe classes and reference those classes from each task category in §3.

### 4
- **Severity:** Major
- **Issue:** The orchestrator recommendation for architecture-heavy sessions is under-specified. Section 2 says to escalate to `Opus@high`, but the architecture task definitions in §§3.1–3.5 all require `effort: max`. Because the playbook relies on the “same-model in-conversation” convention, this mismatch means the recommended orchestrator setting does not actually match the documented task setting.
- **Location:** `routing-playbook.md` §2 vs. §§3.1–3.5
- **Fix:** Change the synthesis-session recommendation from `Opus@high` to `Opus@max`, or explicitly document how an `Opus@high` orchestrator is expected to perform `max`-effort in-conversation architecture work.

### 5
- **Severity:** Minor
- **Issue:** The new next-session-set recommendation protocol is mostly defined, but the generated `change-log.md` does not follow the protocol it introduces. The playbook requires a structured recommendation block with next set(s), recommended orchestrator, and rationale, included in the change-log header; the change log only has a loose “Next Steps” section.
- **Location:** `routing-playbook.md` §3.15, §8; `change-log.md` “Next Steps”
- **Fix:** Update `change-log.md` to use the defined protocol format verbatim, including explicit rationale. Also clarify in §8 whether multiple parallel recommendations are allowed in the canonical output schema or only in exceptional cases.

### 6
- **Severity:** Minor
- **Issue:** Section 6 introduces a new `grammar-authoring` task type/alias even though the main playbook consistently maps grammar work to the existing `architecture` task type. This creates duplicate routing vocabulary for the same work and contributes to the contradictions above.
- **Location:** `routing-playbook.md` §3.1, §4, §6
- **Fix:** Either remove the `grammar-authoring` alias entirely and keep grammar work under `architecture`, or fully migrate the playbook to the new task type and update §§3.1, 4, and 6 consistently.

### 7
- **Severity:** Major
- **Issue:** The in-conversation substitute for the required architecture synthesis is materially lower quality than the spec-targeted route would have been. The evidence is not the disclosed deviation itself, but the unresolved contradictions across routing rules, orchestrator recommendations, and the change log.
- **Location:** `routing-playbook.md` overall, especially §§2, 3, 6, 8; `change-log.md`
- **Fix:** Run one reconciliation pass specifically to resolve the contradictions above before treating `routing-playbook.md` as canonical. A successful architecture-grade synthesis/review pass would be the safest fix, but a careful manual reconciliation is acceptable if it eliminates the inconsistencies.

---

# Verification Round 2

## ISSUES_FOUND

### Round-1 finding status

| # | Status | Note |
|---|---|---|
| 1 | Resolved | Section 6 no longer places `schema-validation`, `parser-regression-check`, or `grammar-authoring` in `delegation.always_route_task_types`; only `session-verification` is mandatory-route. |
| 2 | **Not fully resolved** | Section 2, Section 3.6, and `change-log.md` are fixed, but Section 8 still recommends `vba-spec-study` with `Opus@high`, which contradicts the corrected Sonnet@medium guidance for analysis-only sessions. |
| 3 | Resolved | Section 5 now includes explicit verification recipes for JET SQL, enrichment taxonomy, enrichment structural, spec-study, parser regression, test-fixture, ADR, and recommendation sessions. |
| 4 | **Not fully resolved** | Section 2 and Section 6 correctly use `Opus@max` for architecture work, but Section 8 still says `Opus@high`. |
| 5 | **Partially resolved** | `change-log.md` now includes next-session-set recommendations with per-set orchestrator/rationale blocks, but Section 3.15’s documented output schema is still singular, so the docs and example output are not fully aligned. |
| 6 | Resolved | Section 6 now clearly states `grammar-authoring` is an alias for `architecture`, shares the same model/effort/verification posture, and is not always-route. |

### Remaining issues

- **Issue** → Stale Section 8 example reintroduces the round-1 routing contradictions.  
  **Location** → `routing-playbook.md`, Section 8, example for `vba-parser-session-set-generator`:
  - "`#2 vba-spec-study` has 2 synthesis sessions → Opus@high"
  - "`#1 parser-foundations` ... + Opus@high for the harness-design session"  
  **Fix** → Update the example to match the corrected rules:
  - `vba-spec-study` → `Claude Sonnet 4.6 @ effort=medium` because its synthesis is `analysis`, not `architecture`
  - any architecture escalation example → `Opus@max`, not `Opus@high`

- **Issue** → Next-session-set recommendation protocol is still internally inconsistent in the parallel case.  
  **Location** → `routing-playbook.md`, Section 3.15 output schema vs `change-log.md` “Next Steps” block.  
  **Fix** → Make Section 3.15 explicitly define the parallel-output format with repeated per-set blocks:
  - `**Recommended orchestrator for \`<slug>\`:** ...`
  - `**Rationale:** ...`
  
  Or rewrite `change-log.md` to match the current singular schema exactly.

### Adopt-now rows #7–#16 coverage

Addressed overall, but the stale Section 8 example keeps the routing guidance for rows tied to orchestrator selection / effort calibration internally inconsistent.

---

# Verification Round 3

ISSUES_FOUND
