# AI Assignment — harvester-data-classifier

## Session 1 of 4: JSON Contract

**Recommended orchestrator:** `claude-opus-4-7`
**Rationale:** Finalising the `classification.json` schema is a high-stakes
`architecture` task — it is the keystone contract that binds the scanner
(Session 2), editor (Session 3), and sanitizer (Session 4). Mistakes here
cost multiple sessions to repair. Opus at `effort=high` is appropriate.
**Estimated routed cost:** Moderate ($0.05–$0.15)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read project-guidance.md, lessons-learned.md, and data-classification-plan.md in full. | `In-conversation` |
| 2 | Synthesise and finalise the `classification.json` schema. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 3 | Validate schema against Session 4 (sanitizer) and Session 3 (editor) on paper. | `In-conversation` |
| 4 | Write JSON Schema file and sample fixture. | `tool-use` |
| 5 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 6 | Summarise. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)

---

**Next-session orchestrator recommendation (Session 2):** (To be filled after Session 1 completes.)

## Session 2 of 4: Scanner CLI

**Recommended orchestrator:** `claude-opus-4-7`
**Rationale:** The scanner involves non-trivial `architecture` decisions:
OLE DB vs. DAO routing, PII veto rule interaction, cardinality subquery
workaround, and Access-specific COM gotchas. The heuristics engine has
enough complexity that Opus at `effort=medium` is warranted. A Sonnet
orchestrator could route the architecture sub-tasks, but Opus in-
conversation is cleaner given the interconnected decision surface.
**Estimated routed cost:** Moderate ($0.08–$0.20)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read Session 1 schema, data-classification-plan.md § 3 and § 8. | `In-conversation` |
| 2 | Implement `ClassifyCommand.cs`, schema reader, DAO interop, heuristics engine, output writer. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 3 | Write C# files to `AccessHarvester.Pipeline/Classification/`. | `tool-use` |
| 4 | Write unit tests and `[SkippableFact]` integration tests. | `tool-use` |
| 5 | Run `dotnet build && dotnet test`. | `tool-use` |
| 6 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 7 | Summarise. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)

---

**Next-session orchestrator recommendation (Session 3):** (To be filled after Session 2 completes.)

## Session 3 of 4: HTML Classification Editor

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** The HTML editor is a self-contained `mechanical` build task —
a single-file zero-install HTML tool. The design is prescribed by the
spec (10-category taxonomy, per-column/per-table controls, file-open/
save). Sonnet at `effort=medium` is the right cost band; Opus is not
needed unless an unexpected architecture decision arises during build.
**Estimated routed cost:** Low–Moderate ($0.03–$0.10)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Confirm open decision: new repo vs. in-repo file. Document the choice. | `In-conversation` |
| 2 | Build the single-file HTML editor per spec. | `In-conversation` (task type `mechanical`, matches orchestrator) |
| 3 | Write HTML file to its decided location. | `tool-use` |
| 4 | Test editor manually against `samples/classification-fixture.json`. | Manual / `tool-use` |
| 5 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 6 | Summarise. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)

---

**Next-session orchestrator recommendation (Session 4):** (To be filled after Session 3 completes.)

## Session 4 of 4: Test-Data Generator CLI

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** The sanitizer is primarily a `mechanical` task — per-category
masking handlers and FK graph traversal follow a clear specification.
The FK consistency invariant adds some complexity but is well-defined.
Sonnet at `effort=medium` is the appropriate band. Route an `architecture`
sub-task if the chosen output format (`.accdb` vs. CSV) introduces
unexpected complexity.
**Estimated routed cost:** Low–Moderate ($0.05–$0.12)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read Session 1 schema and data-classification-plan.md § 3.5 and § 5. | `In-conversation` |
| 2 | Decide output format (`.accdb` vs. CSV) and document in actuals. | `In-conversation` |
| 3 | Implement `SanitizeCommand.cs`, `MaskingEngine.cs`, `FkGraphBuilder.cs`, category handlers. | `In-conversation` (task type `mechanical`, matches orchestrator) |
| 4 | Write C# files to `AccessHarvester.Pipeline/Sanitization/`. | `tool-use` |
| 5 | Write unit tests and `[SkippableFact]` integration tests. | `tool-use` |
| 6 | Run `dotnet build && dotnet test`. | `tool-use` |
| 7 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 8 | Closeout: disposition.json, change-log.md, next-session-set-recommendation.md. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)
