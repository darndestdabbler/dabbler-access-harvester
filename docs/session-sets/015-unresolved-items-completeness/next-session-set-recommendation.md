# 015 — Next Session Set Recommendation (routed: gemini-pro, $0.0028)

> task_type=planning. Raw routed recommendation, unedited, plus the Hard Constraints carry-forward below.

The correct call is (B) a recorded CONSUMER-DRIVEN-ONLY handoff. Initiating a new set without an active consumer trigger or a satisfied real-corpus dependency violates the repo's governing posture. The next action is to codify the plan for the next trigger.

This handoff should record the standing Design Workspace (DW) items that are natural bundling candidates. When a new consumer report arrives that touches the parser or grammar, it should trigger a new set that pulls in that report's fix along with the following long-standing, low-impact items:

*   **DW-1..DW-8:** The core bundle of known parser/grammar edge cases.
*   **DW-14:** Additional parsing forms for nav-targets (named arguments, indexers, `With`).

---
**Recommendation:** (B) Record a consumer-driven-only handoff identifying DW-1..8 and DW-14 as bundling candidates for the next trigger.
---

## Hard Constraints carry-forward (verbatim, for whatever set a trigger pulls in)

- NEVER regenerate/commit `examples/northwind-output.json` or any
  NorthwindDevEdition-derived artifact (reintroduces residue sets 003-005 removed).
- Golden regeneration is HomeHealthCare-only, via `GoldenCorpusRegenerator`
  (`AH_REGEN_GOLDEN=1`); re-run `CanonicalGoldenRegressionTests`; explain every delta;
  a "no regen" claim must be proven byte-level.
- COM-tier runs are deliberate, sequential, foreground, TRX logger on; never background a
  `dotnet` invocation; sweep spurious `samples/*.accdb` diffs before staging.
- The quarantined `SanitizeCommandTests` stays quarantined.
- Do not weaken assertions/tests to force green; pin new behaviour explicitly.
