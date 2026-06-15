# Next Session Set Recommendation — after Set 017

> Routed analysis (gemini-pro) per docs/ai-led-session-workflow.md. The repo is
> consumer-driven-only (reached 2026-06-01); this records the post-017 deferred-work
> landscape so a future consumer driver can pick quickly. No concrete next set authored.

# Next Session Set Recommendation (Post-Set 017)

### Recommendation: Consumer-Driven-Only Handoff

Author a `consumer-driven-only-handoff.md` and do not plan a new concrete session set.

**Justification:** The repository is in `CONSUMER-DRIVEN-ONLY` status; no new consumer has emerged to drive work beyond the Set 017 deferred-work scope.

### Strongest Pull-in Candidates

Should a driver emerge, the following deferred-work items represent the highest-value candidates for the next session set.

*   **DW-26 (Schema-Invalid Manifests):** Corrects a manifest schema violation for any corpus with line-leading tokens, representing the highest-value correctness fix.
*   **DW-18 (Intrinsic Constant Resolution):** Resolves thousands of false-positive unresolved references (e.g., `vbCrLf`, witnessed heavily in StudentICT) and is golden-affecting.
*   **DW-24 (Credential Redaction):** Addresses a privacy/security risk by redacting plaintext credentials from linked-table `Connect` strings in the manifest.
*   **DW-8 + DW-19 (Dynamic SQL Analysis):** Wires up the existing but inactive dynamic SQL analyzer, a major feature gap.
*   **DW-25 (Linked Table Metadata):** Enriches manifest data for linked tables and is potentially golden-affecting.
*   **DW-14 (Navigation Completeness):** Adds support for common reference forms (named arguments, indexers, `With` blocks) to improve navigation accuracy.

### Natural Bundles

The following work items are naturally bundled by feature area and should be considered together.

*   **Parser Completeness:** `DW-1` (contextual keywords), `DW-2` (graphics syntax), `DW-4` (`!`-suffix lexing).
*   **Ribbon Completeness:** `DW-20` (dynamic callbacks), `DW-21` (non-tab UI), `DW-22` (callback signatures), `DW-23` (table detection).
*   **Dynamic SQL:** `DW-8` (analyzer wiring) and `DW-19` (scalar attribution).

### Hard Constraint: StudentICTDatabase Artifacts

StudentICTDatabase (FE/BE/.lnk) is proprietary, local-only, gitignored; its linked-table Connect strings embed a plaintext password + absolute path. NEVER commit/regenerate any StudentICT-derived artifact. Golden regeneration remains HomeHealthCare-only.