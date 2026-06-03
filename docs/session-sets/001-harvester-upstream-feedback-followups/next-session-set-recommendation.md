# Recommendation for Next Harvester Workstream

## Recommendation

No new session set should be opened at this time. The repository should remain in its consumer-driven-only standby posture, awaiting the next concrete external need.

## Rationale

The `001-harvester-upstream-feedback-followups` set is closed VERIFIED and fully addressed the consumer feedback that initiated it. The repository's program of work explicitly dictates a "consumer-driven-only" posture now that the core extraction and enrichment arc is complete.

The final session of the preceding set included verification and regeneration of key consumer artifacts (HHC and Northwind goldens), leaving no deferred tasks or known loose ends. Initiating new work without an external trigger would violate the established project posture.

## What Would Re-open Work

Work should only resume upon receiving a direct, concrete request from a downstream consumer. Signals for a new session set include:

*   **A bug report or feedback from a consumer** identifying a flaw in the harvested output for their specific Access application (e.g., a misidentified entry point, a false-positive `ParameterizedInput` signal, or an unhandled VBA pattern causing incorrect usage linkage).
*   **Onboarding a new consumer application** whose codebase reveals a novel pattern not covered by the existing heuristics, necessitating an adjustment to the harvester logic.