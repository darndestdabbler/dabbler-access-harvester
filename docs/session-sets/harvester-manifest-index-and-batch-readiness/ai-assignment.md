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
- Orchestrator used: `claude-code opus-4.7 @ effort=medium`
- Total routed cost: `$0.4016` (R1 gpt-5-4 session-verification = $0.26272; R2 gpt-5-4 session-verification = $0.13595; gemini-pro Session 4 next-orchestrator analysis = $0.00292). Two-round session-verification; R1 → ISSUES_FOUND, both fixed in-session, R2 → VERIFIED with empty issues array.
- Deviations from recommendation:
  - Spec line 178 mentions `--clean` as the candidate flag name; ai-assignment.md line 104-105 says `--no-clean` is the disable-flag. Implementation lands only `--no-clean` (default = clean). Rationale: with default-clean, an explicit `--clean` would be redundant (no-op). Minimises CLI surface; behaviour matches the spec's stated semantics. Disclosed in the verifier prompt.
  - User explicitly opted in (this session, before Step 4) to the OPTIONAL cross-DB roll-up index per the disposition's "bump to opus only if the optional cross-DB roll-up index is in scope" rule. Effort stayed at medium per the original recommendation; the optional feature did not raise enough architectural complexity to warrant effort=high.
  - Steps 3.4 (CLI help text) and 3.5 (verification) are not separately routed because Sonnet-cost mechanical-correspondence routes are not load-bearing for help-text edits; the verifier round itself covered both. Avoided redundant route() calls.
  - **R1 verifier surfaced two findings**, both addressed in-session and re-verified VERIFIED in R2:
    - **Major (HarvesterVersion provenance mismatch):** `Pipeline.cs.ResolveHarvesterVersion()` and `Program.cs.ResolveHarvesterVersion()` read from different assemblies and applied different `+<commit-sha>` stripping. Manifest's `HarvesterVersion` could disagree with `--json-status` envelope's `HarvesterVersion` for the same run. Fixed by centralising version resolution in `DatabaseManifest.ResolveHarvesterVersion()` (one canonical helper on the manifest-shape-defining assembly; strips the SourceLink `+<sha>` suffix). Pipeline.cs (manifest emission), Program.cs (`--json-status` envelope), and Program.cs (`--version` output via PrintVersion) all now call the same helper — three prior duplicates collapsed to one.
    - **Minor (--types + default-clean interaction undocumented):** `run --types Forms` against an existing output tree clears all three tiers wholesale, deleting previously-harvested unselected categories. PrintUsage extended with an `IMPORTANT:` paragraph spelling out the hazard and recommending `--no-clean` alongside `--types` for incremental partial re-harvests.
- Notes for next-session calibration:
  - Session 4 starts with no schema-corrective debt and a cleanly-validating v1.1 manifest. CanonicalGoldenRegressionTests green for both HHC and Northwind after Session 3's changes (the determinism gate normalises HarvesterVersion via `GoldenNormalizer`, so the new short-form version string is absorbed automatically).
  - `Pipeline.Run`, `Extract`, `Translate`, `Enrich` all default to `clean=true` now; existing test call sites use fresh-Guid temp dirs so the new default is harmless. Any future test that pre-seeds output before calling `Run` must pass `clean: false` or be rewritten.
  - `Pipeline.EmitRootIndex(outputRoot)` is the public batch-driver entry point for refreshing `<outputRoot>/AccessMetaData/index.json` after a DB is deleted from disk without a full re-run.
  - Status envelope provenance fields (HarvesterVersion + StartedAt + DurationMs + Clean) are now part of the public `--json-status` contract; Session 4's batch-operations doc must describe them.
  - Pre-existing parallel-COM flake in `ClassifyCommandTests.HomeHealthCare_classify_emits_schema_conformant_json` continues to surface when the full Integration suite runs end-to-end (~4-min Classify timeout). Not caused by Session 3 changes (ClassifyCommand is not in Pipeline.Run's call path). Mitigation deferred to a future cleanup session; isolation runs of CanonicalGoldenRegressionTests + CliRunTests + ClassifyCommandTests all pass cleanly.
  - Test counts: 689 Pipeline.Tests pass; 5 new hermetic PipelineCleanTests pass; CanonicalGoldenRegressionTests + CliRunTests + ManifestSchemaConformanceTests + CliBootstrapTests all pass in isolation against the post-Session-3 binary.

**Next-session orchestrator recommendation (Session 4):**
`claude opus-4.7 @ effort=high`
Rationale (routed gemini-pro analysis, 2026-05-29): the task combines complex document synthesis (per-field-type semantics in the interpretation guide + a new batch-operations doc) with a holistic project closeout, demanding the maximum reasoning capacity of Opus to ensure a clean final state and minimize verifier-driven rework. This directly addresses the reasoning gaps observed in Session 3's medium-effort run (the R1 Major was a reasoning-level provenance-consistency issue the orchestrator missed but the verifier caught) and aligns with the stated priority of a high-leverage clean closeout over marginal cost savings for the final session.

---

## Session 4: Document Field-Type Semantics + Batch Operations Guide + Closeout

### Recommended orchestrator
`claude opus-4.7 @ effort=high`

### Rationale (routed gemini-pro analysis, 2026-05-29; calibrated against Session 3 actuals)
This session closes documentation gaps (Finding #3 field-type semantics + Finding #5 batch operations guide) AND carries the set's closeout responsibilities (change-log, next-session-set recommendation, Step 9 reorganization review, ROADMAP row, final session-state flip). The task requires interpreting C# model definitions (`TableMetadata.cs` for field-type representation: `DataType`, `DataTypeCode`, `Attributes` bitmask, `Ddl`) and synthesizing that into precise consumer-facing docs that describe the FINAL post-Session-1-3 behaviour — including the new `--clean`/`--no-clean` semantics, the cross-DB roll-up index, and the status-envelope provenance fields. Session 3's R1 verifier finding (a reasoning-level provenance-consistency Major that the orchestrator missed but the verifier caught) is direct evidence that bumping to opus@high pays for itself when synthesis + closeout responsibilities concentrate in one session.

### Estimated routed cost
moderate (session-verification round + closeout artifacts; Step 9 may add a reorg-review analysis route)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 4.1  | Analyze `AccessHarvester.Access/Models/TableMetadata.cs` (the canonical field-type representation) to fully understand `DataType` / `DataTypeCode` / `Attributes` bitmask / `Ddl` for Attachment (101), OLE Object (11), Hyperlink, multi-valued, Lookup, Memo/LongText, Currency, AutoNumber/COUNTER. | `orchestrator` (Opus authors in-conversation per the synthesis-document convention) |
| 4.2  | Extend `docs/harvester/interpretation-guide.md` with a per-field-type table mapping each type to its `DataType`/`DataTypeCode`/`Attributes`/`Ddl` representation; state explicitly that the harvester is metadata-only and that attachment / multi-valued child schemas are not expanded. Use Northwind's `Employees.Attachments` field as the worked example. | `orchestrator` |
| 4.3  | Author a new batch-operations doc (or `install.md` operations section) covering: the `<out>/AccessMetaData/<dbName>/` convention for many DBs; sequential-only operation; MSACCESS / RCW hygiene across runs; `--clean` / `--no-clean` semantics (Session 3); the cross-DB roll-up index at `<out>/AccessMetaData/index.json` (Session 3); the new `--json-status` envelope provenance fields (HarvesterVersion, StartedAt, DurationMs, Clean). | `orchestrator` |
| 4.4  | Author `change-log.md`, `next-session-set-recommendation.md`, and `disposition.json` (final-session contract); flip the set's top-level `status` to `complete`. | `orchestrator` |
| 4.5  | Submit the documentation changes for cross-provider verification. | `gpt-5-4` (session-verification, routed via `task_type_overrides.session-verification`) |
| 4.6  | Step 9 reorganization review of `docs/planning/project-guidance.md` + `docs/planning/lessons-learned.md` — propose Convention promotions, relocations, staleness flags. May route an `analysis` call to elicit a second opinion if a promotion candidate is borderline. | `orchestrator` (+ optional `analysis` route for borderline candidates) |

### Actuals (filled after the session)
- Orchestrator used: `claude-code opus-4.7 @ effort=high`
- Total routed cost: `$0.3818` (gemini-pro next-set analysis $0.0028 + R1 gpt-5-4 session-verification $0.2738 + R2 gpt-5-4 session-verification $0.1052; R1 ISSUES_FOUND with 1 Major closeout-placeholder + 2 Minor doc-accuracy findings, all fixed in-session; R2 VERIFIED with empty issues array)
- Deviations from recommendation:
  - Step 4.1–4.3 (per-field-type semantics + batch-operations doc) authored entirely in-conversation by the Opus orchestrator per the synthesis-document convention ("when the orchestrator's model matches the routing target, handle in-conversation"). No `route(task_type="architecture")` call needed.
  - Step 4.5 (cross-provider session-verification) routed via `route(task_type="session-verification")` per the `task_type_overrides.session-verification: gpt-5-4` override.
  - Step 4.6 (Step 9 reorganization review) proposals authored in-conversation; no routed `analysis` call (no borderline promotion candidates that needed a second opinion).
  - **Test posture disclosed in the verifier prompt**: 1611 hermetic tests pass cleanly across all four hermetic suites; the Integration suite's 3 CLI-test failures (`CliExtractTests`, `CliRunTests`, `CliTranslateTests` against `HomeHealthCare.accdb`, 60s timeouts) are the documented xUnit parallel-COM contention pattern Session 3 first noted on `ClassifyCommandTests`. All three pass in isolation (33s wall-clock); this is **not** caused by Session 4's docs-only edits. Mitigation is the recommended next set's scope.
- Notes for next-session calibration:
  - The next session set this set recommends — `harvester-parallel-com-test-cleanup` — is a Sonnet @ medium target per the routed gemini-pro analysis. Mechanical work (xUnit Collection serialisation or pre-staged inputs); no synthesis. Routed cost band Low (<$0.50).
  - Both committed goldens regenerate byte-identically against the v1.1 manifest shape; the determinism gate stayed green throughout the set.
  - The set as a whole is **maintenance-coherent**: it answered the first real downstream consumer's six findings, and the recommended follow-up keeps the project in maintenance mode (close a CI-flake, not ship a new feature).

**Next-session orchestrator recommendation (Session 5):**
None. This is the final planned session for this set.