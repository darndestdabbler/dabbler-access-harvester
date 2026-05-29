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
- Orchestrator used: `<fill at close>`
- Total routed cost: `<fill at close>`
- Deviations from recommendation: `<fill at close>`
- Notes for next-session calibration: `<fill at close>`

**Next-session orchestrator recommendation (Session 3):**
`claude sonnet-4.5 @ effort=medium`
Rationale: Session 3 is a straightforward implementation of a well-defined feature, making Sonnet a cost-effective choice.

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