# Next Session Set Recommendation

## 1. Recommended Next Set

**`harvester-finish-canonical-golden-output`**

This is set **F2** in the Phase 4 plan.

## 2. Authority

This recommendation follows the sequential order established by the project's master plans:

- **Primary Authority:** `docs/session-sets/ROADMAP.md`, Phase 4 table, row **F2**.
- **Structural Authority:** `docs/session-sets/remaining-work-planner/remaining-work-tree.md`, Tree table, row **F2**. The `Prereqs` column for F2 explicitly lists "F1", which is the session set `harvester-finish-canonical-parse-success-report` that has just concluded.

The completion of F1 satisfies the prerequisite for F2, making it the next set to execute per the plan. No deviation is proposed.

## 3. Spec Status and Session 1 Elaboration

- **Status:** The spec for `harvester-finish-canonical-golden-output` is present as a **stub**, per the "Status" column in the `remaining-work-tree.md`.

- **Session 1 Elaboration Mandate:** Session 1 of this new set will be responsible for elaborating the stub spec into a complete execution plan. Key tasks will include:
    - Designing the `Manifest Aggregator` architecture responsible for combining per-object intermediate representation (IR) files into a single, canonical golden output.
    - Finalizing the JSON schema for the golden output files.
    - Detailing the pipeline integration points for emitting the golden output and the folded-in `UnresolvedItem` records (Phase 4 Criterion #5).
    - Defining the test strategy for diff-based regression testing of the golden files, including the normalization layer required for deterministic output, as hinted at by F3's dependency on F2.
    - Performing the light schema-documentation spot-check noted in the `ROADMAP.md` as a potential pre-flight task for F2.

## 4. Session 1 Orchestrator Recommendation

- **Orchestrator:** `claude-code`
- **Model:** `claude-opus-4-7`
- **Effort:** `high`

**Grounding:** This recommendation is grounded in the successful execution of Session 1 of the just-completed `harvester-finish-canonical-parse-success-report` set. That session was also a spec-elaboration and foundational implementation task, successfully executed by Claude Opus 4.7 at `effort=high`. Given that Session 1 of F2 involves similar high-level design work (aggregator architecture, schema definition, pipeline strategy for a 4-session set), mirroring the resourcing of the preceding set's analogous session is the most direct, evidence-based approach.

## 5. Planned Cost and Prerequisite Shift

- **Planned Cost Band:** The plan (`remaining-work-tree.md`, "Routed-cost rollup" table) budgets **`~$0.40–$0.80`** for the entire `harvester-finish-canonical-golden-output` set (F2).

- **Prerequisite Shift:** The completion of `harvester-finish-canonical-parse-success-report` (F1) fully unblocks F2. It also satisfies one of the two prerequisites for F4 (`harvester-finish-operations-and-interpretation-docs`), which can proceed once F2 is also complete.