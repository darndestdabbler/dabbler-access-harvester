# AI Assignment: `harvester-manifest-index-and-batch-readiness`

This assignment guides the four sessions planned to address the findings from the first downstream consumer. The primary goals are to make `manifest.json` a complete, self-describing navigation index and harden the CLI for batch operations.

---

## Session 1: Fix Critical Manifest Omissions

### Recommended orchestrator
`claude opus-4.7 @ effort=medium`

### Rationale
This session fixes a critical data-loss bug (Finding #1) in the primary output artifact. The task requires precise C# code modification, new test authoring, and careful regeneration of golden fixtures, justifying a capable model.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1.1  | Analyze `AccessHarvester.Core/Pipeline.cs` to confirm the call site where `tables`, `relationships`, and `modules` are omitted during manifest construction. | `orchestrator` |
| 1.2  | Modify `Pipeline.cs` to pass the correct collections to the `DatabaseManifestAggregator`, ensuring Tables, Relationships, and Modules are populated. | `orchestrator` |
| 1.3  | Author a new xUnit test in `tests/AccessHarvester.Pipeline.Tests/` that runs the pipeline against a fixture and asserts that `manifest.json` contains non-zero counts for tables, relationships, and modules. | `orchestrator` |
| 1.4  | Regenerate both golden fixtures (`Northwind.accdb`, `SimpleTest.accdb`) using the updated pipeline. | `orchestrator` |
| 1.5  | Perform a golden diff to confirm the only changes are the population of the previously empty manifest sections. | `sonnet` (mechanical-correspondence) |
| 1.6  | Validate the regenerated manifests against the existing `v1.0` schema. | `sonnet` (mechanical-correspondence) |
| 1.7  | Submit final changeset for verification. | `gpt-5-4` (session-verification) |

### Actuals (filled after the session)
- Orchestrator used: `claude-code opus-4.7 @ effort=medium`
- Total routed cost: `$0.1344` (gemini-pro analysis for ai-assignment.md = $0.0219; gpt-5-4 session-verification = $0.1125; both single-round, no retries)
- Deviations from recommendation:
  - Step 1.4 ("regenerate both golden fixtures") was wired through the existing `tmp/build-golden-corpus` tool against fresh CLI runs rather than ad hoc edits — the spec didn't name a mechanism so this is a clarification, not a deviation.
  - Steps 1.5/1.6 (mechanical golden + schema diff to Sonnet) were not separately routed — the schema-conformance integration tests already serve that role mechanically. Avoided paying for redundant route() calls.
  - Major in-session scope decision (AskUserQuestion 2026-05-29): three pre-existing schema-vs-typed-record drifts surfaced when the populate fix exposed previously-empty arrays. Per user direction, deferred all three corrective schema edits to Session 2's planned v1.0 → v1.1 bump rather than expanding Session 1 scope. One affected integration test was Skip'd with a precise Session-2 pointer; the other 4 affected tests were preserved by reverting `examples/northwind-output.json` to its empty-arrays smoke shape.
- Notes for next-session calibration:
  - Session 2 starts with three concrete, scoped schema-corrective fixes already characterized in `disposition.json`'s `blockers[0].details` (ParserOutput.Items VBA-procedure shape; RelationshipDefinition Parent/Child rename; TableIndex.Fields object shape). Treat these as part of the additive v1.1 bump, not as a separate set.
  - The `examples/northwind-output.json` smoke fixture stays the empty-arrays minimal example; Session 2's schema bump should leave that contract intact (or re-coordinate the validate smoke tests).
  - The new `ManifestCompletenessTests` per-category invariant is the durable pin for Finding #1 — Session 2's schema work must not break it.

**Next-session orchestrator recommendation (Session 2):**
`claude opus-4.7 @ effort=high`
Rationale: Session 2 requires architectural synthesis to redesign the manifest schema, a task best suited for a frontier model. The schema-corrective fixes inherited from Session 1's surfacing add concrete C# / schema editing work alongside the index design — both benefit from the higher reasoning depth.

---

## Session 2: Evolve Manifest to a Navigation Index

### Recommended orchestrator
`claude opus-4.7 @ effort=high`

### Rationale
This session addresses a core design flaw (Finding #2) by restructuring the manifest into a true navigation index. The task requires architectural synthesis for the new schema, plus coordinated changes to C# models and logic, demanding a high-effort engagement from a frontier model.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| 2.1  | Propose a new structure for `manifest.json` that includes top-level keys for schema version, a pointer to `interpretation-guide.md`, and per-category file indices instead of inlined content. | `opus` (synthesis) |
| 2.2  | Update the C# models (e.g., `DatabaseManifest.cs`) to reflect the new, index-oriented structure. | `orchestrator` |
| 2.3  | Modify the logic in `DatabaseManifestAggregator.cs` and `Pipeline.cs` to populate the new manifest structure. | `orchestrator` |
| 2.4  | Update the JSON schema file to v1.1 and adjust the schema validator accordingly. | `orchestrator` |
| 2.5  | Regenerate both golden fixtures to conform to the new manifest structure. | `orchestrator` |
| 2.6  | Perform a golden diff to confirm the structural changes are as designed. | `sonnet` (mechanical-correspondence) |
| 2.7  | Validate the regenerated manifests against the new v1.1 schema. | `sonnet` (mechanical-correspondence) |
| 2.8  | Submit final changeset for verification. | `gpt-5-4` (session-verification) |

### Actuals (filled after the session)
- Orchestrator used: `claude-code opus-4.7 @ effort=high`
- Total routed cost: `$0.1847` (gemini-pro architecture = $0.05005; gpt-5-4 R1 session-verification = $0.11534; gpt-5-4 R2 session-verification = $0.01931). Architecture call fell back from opus@max → gemini-pro after Anthropic opus repeatedly hit its 600s read timeout on the large architecture prompt (two attempts); routed via `task_type="planning"` + `max_tier=2` to land on gemini-pro deterministically. Session 6/7 routed to gpt-5-4 per `task_type_overrides.session-verification: gpt-5-4`.
- Deviations from recommendation:
  - Architecture route was opus → gemini-pro (force-pinned `architecture` task_type force-routes to opus; gemini-pro reached via `planning` + `max_tier=2`). Cost was lower than expected ($0.05 vs ~$0.30+ opus@max).
  - Steps 2.6 / 2.7 (mechanical golden + schema diff via Sonnet) were not separately routed — the SchemaConformanceTests + ManifestCompletenessTests + new FileMap-invariant test exercise mechanical correspondence at test-time, and the orchestrator ran the CLI's `validate` subcommand directly. Avoided redundant route() calls.
  - Schema work expanded beyond the three Session-1 drift fixes to also include a fourth corrective: added `"LateBound"` to the v1.1 schema's `Classification.Purity` enum. The `ClassificationCategory` C# enum (which `ParserOutputProjector` serializes via `.ToString()`) includes `LateBound` but the v1.0 schema's `Purity` enum omitted it — this was the actual top-level cause of the 1099-error cascade Session 1 attributed to the HarvestedItem oneOf shape. Once LateBound was added, results.IsValid flipped true and SchemaValidator returned Valid early; the if/then/else branch-discrimination rewrite is still in v1.1 but is no longer load-bearing for HHC validation. Disclosed in the verifier prompt.
  - **R1 verifier surfaced two additional findings** that were addressed in-session and re-verified VERIFIED in R2:
    - **Major:** Backward-compat policy didn't actually allow literal `SchemaVersion: "1.0"` documents through the v1.1 `validate` subcommand. Fixed by embedding BOTH `harvester-schema.v1.0.json` and `harvester-schema.v1.1.json` as resources and adding per-document schema dispatch in `RunValidate` (cached `SchemaValidator` per version; `--json-status` envelope now includes `SupportedSchemaVersions` + `PerManifestSchemaVersion[]`). Added two integration tests (`Validate_V1_0_Manifest_DispatchesToV1_0_Schema_AndReturnsZero` and `Validate_Mixed_V1_0_And_V1_1_Manifests_BothDispatchedAndPass`).
    - **Minor:** Pipeline.Run emits `READ-ME-FIRST.md` but `tmp/build-golden-corpus` was not copying it. Fixed by adding a `File.Copy(staging, golden)` step (no normalization needed — content is static). Both committed goldens now include byte-identical `READ-ME-FIRST.md`.
- Notes for next-session calibration:
  - Session 3 starts on the cleanly-validating v1.1 manifest shape; no schema-corrective debt carries forward. Both goldens regen byte-identically.
  - Determinism gate green: two fresh `Pipeline.Run` calls against `HomeHealthCare.accdb` produced byte-identical normalized manifests + READ-ME-FIRST.md.
  - Test counts: `dotnet test` = 2109 passed, 2 skipped (pre-existing synthetic-fixture skips), 0 failed. Integration suite up from 53 → 55 with the dispatch tests.
  - The schema-dispatch pattern (one embedded schema per supported version, document-driven validator selection) is the precedent for future additive bumps; document this in `docs/planning/project-guidance.md` if Session 3's batch-ergonomics work uncovers more schema-version surfaces.

**Next-session orchestrator recommendation (Session 3):**
`claude sonnet-4.6 @ effort=medium`
Rationale: Session 3 (Batch-run ergonomics — stale-file clean + status provenance for Findings #4/#6, optional cross-DB index) is a well-defined CLI feature with mechanical implementation — `--clean` flag wiring, `--json-status` envelope additions, target-clean tests. Sonnet's cost-to-performance ratio is the right call for that scope. Bump to opus only if Session 3 surfaces a non-trivial cross-DB roll-up index design (optional spec item).

---

## Session 3: Harden for Batch Operation

### Recommended orchestrator
`claude sonnet-4.5 @ effort=medium`

### Rationale
This session implements a well-defined batch-readiness feature: cleaning the output directory to prevent stale artifacts (Finding #4). The task is a self-contained C# implementation with a new test, for which Sonnet provides an excellent cost-to-performance ratio.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 3.1  | Modify the `run` command logic in `Pipeline.cs` to ensure the output directory is cleared before harvesting begins. | `orchestrator` |
| 3.2  | Implement this as the default behavior, but add a new CLI option (e.g., `--no-clean`) to disable it. | `orchestrator` |
| 3.3  | Author a new xUnit test to verify that a re-run on an existing output directory correctly removes a file that is no longer part of the harvest. | `orchestrator` |
| 3.4  | Update the CLI help text to document the new default behavior and the override flag. | `orchestrator` |
| 3.5  | Submit final changeset for verification. | `gpt-5-4` (session-verification) |

### Actuals (filled after the session)
- Orchestrator used: `<fill at close>`
- Total routed cost: `<fill at close>`
- Deviations from recommendation: `<fill at close>`
- Notes for next-session calibration: `<fill at close>`

**Next-session orchestrator recommendation (Session 4):**
`claude opus-4.7 @ effort=medium`
Rationale: Session 4 requires careful code comprehension and the synthesis of that knowledge into precise technical documentation, a task where Opus excels.

---

## Session 4: Document Field-Type Semantics

### Recommended orchestrator
`claude opus-4.7 @ effort=medium`

### Rationale
This session closes a documentation gap (Finding #3) by explaining complex field-type semantics. The task requires interpreting C# model definitions (`TableMetadata.cs`) and synthesizing that information into clear, accurate documentation for a downstream consumer, justifying the use of a frontier model.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 4.1  | Analyze `AccessHarvester.Core/Models/TableMetadata.cs` to fully understand the representation of special field types, including `DataTypeCode` for Attachment, OLE Object, and Hyperlink, and the `Attributes` bitmask. | `orchestrator` |
| 4.2  | Author a new section in `docs/harvester/interpretation-guide.md` that clearly explains these field-type semantics. | `orchestrator` |
| 4.3  | Ensure the documentation explicitly calls out how to interpret Attachment fields (DAO 101) and distinguishes them from OLE Objects (11), as seen in the Northwind database. | `orchestrator` |
| 4.4  | Submit the documentation changes for verification. | `gpt-5-4` (session-verification) |

### Actuals (filled after the session)
- Orchestrator used: `<fill at close>`
- Total routed cost: `<fill at close>`
- Deviations from recommendation: `<fill at close>`
- Notes for next-session calibration: `<fill at close>`

**Next-session orchestrator recommendation (Session 5):**
None. This is the final planned session for this set.