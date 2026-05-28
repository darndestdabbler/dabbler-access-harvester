# AI Assignment — harvester-object-cleanup-identifier

## Session 1 of 3: Schema + Detection Strategy

**Recommended orchestrator:** `claude-opus-4-7`
**Rationale:** The primary task is high-complexity `architecture` synthesis —
designing a multi-signal detection schema and its mapping to an external
JSON format. Opus is the target model for `architecture` tasks per
repo conventions, and using an Opus orchestrator allows the core synthesis
to be handled in-conversation.
**Estimated routed cost:** Moderate ($0.05–$0.15)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read project-guidance.md, lessons-learned.md, and object-cleanup-identifier spec. | `In-conversation` |
| 2 | Synthesise the `object-cleanup.json` schema and `uat-checklist-editor` mapping. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 3 | Draft `docs/planning/object-cleanup-strategy.md`. | `In-conversation` |
| 4 | Write strategy doc and JSON Schema files. | `tool-use` |
| 5 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 6 | Summarise and produce change-log entry. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)

---

**Next-session orchestrator recommendation (Session 2):** (To be filled after Session 1 completes.)

## Session 2 of 3: CLI Detection Code

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** The core task is `mechanical` implementation — translating
the Session 1 schema and strategy into C# code. Three detectors are
well-scoped and independently testable. Sonnet at `effort=medium` is
the appropriate cost band for `mechanical` tasks per repo conventions.
**Estimated routed cost:** Low–Moderate ($0.03–$0.10)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read strategy and schema from Session 1. | `In-conversation` |
| 2 | Implement `IdentifyCleanupCommand.cs` and detector types. | `In-conversation` (task type `mechanical`, matches orchestrator) |
| 3 | Write C# files to `AccessHarvester.Pipeline/Cleanup/`. | `tool-use` |
| 4 | Write unit tests. | `tool-use` |
| 5 | Run `dotnet build && dotnet test`. | `tool-use` |
| 6 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 7 | Summarise. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)

---

**Next-session orchestrator recommendation (Session 3):** (To be filled after Session 2 completes.)

## Session 3 of 3: Pilot + Calibration

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** Pilot execution and threshold tuning is a `analysis`-type
task — Sonnet at `effort=medium` is appropriate. No architecture
decisions are expected; the work is calibration and golden-fixture
production.
**Estimated routed cost:** Low ($0.02–$0.08)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Run `identify-cleanup` against HomeHealthCare and NorthwindDevEdition golden outputs. | `tool-use` |
| 2 | Review results; tune detector thresholds and description templates. | `In-conversation` (task type `analysis`, matches orchestrator) |
| 3 | Write calibrated sample output files as golden fixtures. | `tool-use` |
| 4 | Run `dotnet build && dotnet test`. | `tool-use` |
| 5 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 6 | Closeout: disposition.json, change-log.md, next-session-set-recommendation.md. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)
