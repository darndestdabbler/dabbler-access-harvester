# Next Session Set Recommendation

## Recommendation

**`generalization-validation-on-non-northwind`** — Author one workflow package from `MiniComplex.accdb` to validate the harvester's output and the workflow package strategy on a non-Northwind corpus.

This deviates from the original `VBA-Parser-Migration-Harvester-Plan.md` (which had Phase 8 next) to align with the current, validated `strategy.md` (specifically, Phase 3c).

## Why this set, why now

*   **Validates the Core Strategy:** `strategy.md` Phases 1 and 2 proved the "workflow package" concept works for Northwind. This set addresses the single most important remaining question (Open Validation #2): does the entire approach generalize?
*   **De-risks Future Investment:** Before scaling up Blazor implementation (Phase 3a) or other Northwind-based work, we must confirm the harvester and package template aren't over-fitted to Northwind's specific patterns.
*   **Builds on a Stable Base:** The `form-report-code-and-grouping` set just filled in the last major deferred fields in the form/report metadata schema, providing a complete and stable foundation for this validation.
*   **Directly Implements the Strategic Plan:** This is Phase 3c from `strategy.md`, the only open Phase-3 item that lives in the harvester repo. It's the logical next step in the documented strategic sequence.

## What this set delivers

*   **Empirical verdict** on whether the harvester's output is generalizable beyond Northwind.
*   **One complete workflow package** for a representative workflow from `MiniComplex.accdb` (e.g., "Add a Timesheet Entry").
*   **Resolution of Open Validation #2** from `strategy.md`: "Does the package shape generalize to non-Northwind databases?"
*   **A clear go/no-go signal** for revising the workflow package template before scaling the Blazor migration effort.

## Prereqs

None. The `MiniComplex.accdb` and `HomeHealthCare.accdb` samples are already present in the `samples/` directory.

## Alternatives considered

*   **`pagesetup-decoding`**: Lower priority because it's a minor feature enhancement with narrow value, explicitly deferred multiple times. It does not answer any strategic questions.
*   **`resume-integration-testing-and-acceptance`**: Rejected because this set was explicitly `cancelled`. The successful Blazor pilot (Phase 2) shipped without it, demonstrating it was not on the critical path. Resuming it ignores this strategic pivot.
*   **`blazor-update-workflows` (Phase 3a)**: Lower priority for this repo because that work belongs to a sibling Blazor repository. More importantly, validating generalization first (3c) is strategically sounder than building more on a potentially un-validated foundation.

## Recommended orchestrator

*   **Engine:** Anthropic Claude
*   **Model:** `claude-3-5-sonnet-20240620` or `claude-3-opus-20240229`
*   **Effort:** 2-3 sessions
*   **Rationale:** The task is synthesizing a workflow package, which involves analysis, interpretation, and structured prose generation. This is a complex synthesis task, not a mechanical refactor. The pattern from Phase 1 (one AI drafts, another peer-reviews) should be repeated, favoring high-capability models.