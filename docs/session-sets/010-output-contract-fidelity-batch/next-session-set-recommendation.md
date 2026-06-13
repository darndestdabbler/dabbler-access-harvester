# Next Session Set Recommendation: 011-resolver-and-navigation-wiring

- **Classification**: P3 / Core Wiring / High-Impact
- **Estimate**: 3 sessions

## 1. Context

Sets 008 (P1a), 009 (P1b), and 010 (P2) are complete. The harvester now correctly extracts form-module items (`F-3-1`) that require cross-module name resolution. However, the production pipeline bypasses the necessary components, leaving this new metadata incomplete and leading directly to consumer feedback items `R2-1` (missing switchboard navigation) and `R2-2` (broken dialog->query->report chains).

This set wires the existing but unused VBA resolver (`F-3-4`) and Phase 6 enrichment substrate (`F-2-2`) into the production pipeline to deliver complete navigation metadata and resolve outstanding consumer feedback.

## 2. Scope

- **F-3-4: VBA Resolver Wiring**
  - Modify `Pipeline.cs` to instantiate a single, shared production `ProjectScope` and VBA resolver for the entire harvest.
  - Decommission the temporary, per-module, registry-less resolver.
  - Aggregate unresolved name resolution items from all modules into a single collection for downstream processing.

- **F-2-2: Navigation & Enrichment Wiring**
  - Integrate the Phase 6 enrichment substrate into the production pipeline execution flow.
  - Feed the aggregated unresolved items into the enrichment phase.
  - Populate the `NavigationGraph` manifest member from resolved form/report control events and switchboard items.
  - Populate `unresolved-items.json` with the final list of unresolvable identifiers.

## 3. Discipline

The `golden-regen-per-session` discipline is mandatory. Each session will conclude with a full regeneration of golden files. The resulting diffs, which are expected to be substantial, must be verified to confirm the correct population of the `NavigationGraph` and `unresolved-items.json`.

## 4. Orchestrator Recommendation

- **Orchestrator**: Opus-class LLM

- **Rationale**:
    - **Multi-file Wiring**: The required changes span multiple, loosely-coupled components of the pipeline (`Pipeline.cs`, resolver implementation, enrichment phase). This requires an orchestrator with a large context window and strong architectural reasoning capabilities.
    - **Large Golden Mover**: Activating these components will fundamentally alter the output contract, generating large, complex diffs in golden files. An Opus-class model is required to manage and validate these changes effectively.
    - **Per-Delta Explanation**: The complexity of the wiring necessitates the strict per-delta explanation discipline to maintain a clear audit trail of the integration logic.