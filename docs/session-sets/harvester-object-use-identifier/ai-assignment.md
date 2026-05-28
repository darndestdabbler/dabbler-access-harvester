# AI Assignment — harvester-object-use-identifier

## Session 1 of 3: Schema + Analysis Strategy

**Recommended orchestrator:** `claude-opus-4-7`
**Rationale:** The primary task is high-complexity `architecture` synthesis —
designing a usage-classification taxonomy, a multi-signal detection schema,
clarifying-question templates, and the mapping to `uat-checklist-editor`.
Opus is the target model for `architecture` tasks; using an Opus
orchestrator allows the core synthesis to be handled in-conversation.
**Estimated routed cost:** Moderate ($0.05–$0.15)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read project-guidance.md, lessons-learned.md, and object-use-identifier spec. | `In-conversation` |
| 2 | Read `harvester-object-cleanup-identifier` output schema (for exclusion format). | `In-conversation` |
| 3 | Synthesise the usage-classification taxonomy and `object-use.json` schema. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 4 | Design question templates per uncertainty signal. | `In-conversation` |
| 5 | Write strategy doc and JSON Schema files. | `tool-use` |
| 6 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 7 | Summarise. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- Orchestrator: `claude-sonnet-4-6` (ai-assignment recommended Opus; Sonnet used because session was started by Sonnet orchestrator; architecture authored in-conversation)
- Architecture: In-conversation on Sonnet; taxonomy, signal collectors, classification rules, and question templates designed from HomeHealthCare golden output inspection
- Verification: 3 rounds via gemini-pro ($0.0542 total)
  - R1: REJECTED — Action Queries (TypeCode 2-5) not covered; soft-prereq degradation imprecise
  - R2: ISSUES_FOUND — Union queries (TypeCode 64) misclassified as StandaloneUtility
  - R3: VERIFIED
- Deliverables: `docs/planning/object-use-identification-strategy.md` (taxonomy, 6 signal collectors, 11 query rules, confidence scoring, question templates, output mappings), `AccessHarvester.Pipeline/UsageIdentification/object-use-schema.json` (JSON Schema Draft-07)

---

**Next-session orchestrator recommendation (Session 2):** `claude-sonnet-4-6` — Session 2 is mechanical implementation of the strategy. All interfaces are defined; Sonnet at effort=medium is appropriate per the original ai-assignment.

## Session 2 of 3: CLI Analysis Code

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** The core task is `mechanical` implementation of six signal
collectors, a classifier, a question generator, and output writers.
Each component has a well-defined interface from Session 1. Sonnet at
`effort=medium` is the appropriate band for `mechanical` tasks.
**Estimated routed cost:** Low–Moderate ($0.03–$0.10)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read strategy and schema from Session 1. | `In-conversation` |
| 2 | Implement `IdentifyUsageCommand.cs`, signal collectors, classifier, question generator, output writers. | `In-conversation` (task type `mechanical`, matches orchestrator) |
| 3 | Write C# files to `AccessHarvester.Pipeline/UsageIdentification/`. | `tool-use` |
| 4 | Write unit tests. | `tool-use` |
| 5 | Run `dotnet build && dotnet test`. | `tool-use` |
| 6 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 7 | Summarise. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- Orchestrator: `claude-sonnet-4-6` (ai-assignment recommended Sonnet; applied)
- Implementation: All 6 signal collectors, SignalIndex, UsageClassifier, QuestionGenerator, UsageChecklistWriter, IrLoader, IdentifyUsageCommand implemented in-conversation
- Tests: 84 unit tests added under `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/` — all pass
- Verification: 3 rounds via gpt-5-4 ($0.179 + $0.179 + $0.056 = $0.414 total)
  - R1: ISSUES_FOUND — 2 Major (F4 overreach, no Module/Macro findings), 1 Major (inline SQL detection), 2 Minor
  - R2: ISSUES_FOUND — 1 Major (inline-SQL RecordSource misclassified as unbound)
  - R3: VERIFIED
- Notable deviations from spec:
  - RowSourceCollector reads static VBA assignments (`.RowSource = "name"`) rather than design-time control nodes because form IR JSON lacks the control tree (documented as accepted limitation)
  - Module and Macro findings added beyond original spec scope (all four verifier rounds accepted this)

---

**Next-session orchestrator recommendation (Session 3):** `claude-sonnet-4-6` — Session 3 is pilot execution and calibration (run `identify-usage` against golden output, review in uat-checklist-editor, tune). Sonnet at effort=medium is appropriate; no new architecture decisions are expected.

## Session 3 of 3: Pilot + Calibration

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** Pilot execution and calibration is an `analysis` task.
Sonnet at `effort=medium` is appropriate; no architecture decisions
are expected. The verification step guards output quality.
**Estimated routed cost:** Low ($0.02–$0.08)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Run `identify-usage` against HomeHealthCare golden output (with optional cleanup exclusion). | `tool-use` |
| 2 | Open result in uat-checklist-editor; review and tune. | `In-conversation` (task type `analysis`, matches orchestrator) |
| 3 | Run `identify-usage` against NorthwindDevEdition golden output. | `tool-use` |
| 4 | Write calibrated sample outputs as golden fixtures. | `tool-use` |
| 5 | Run `dotnet build && dotnet test`. | `tool-use` |
| 6 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 7 | Closeout: disposition.json, change-log.md, next-session-set-recommendation.md. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- Orchestrator: `claude-sonnet-4-6` (ai-assignment recommended Sonnet; applied)
- Pilot: HomeHealthCare 74 objects, NorthwindDevEdition 167 objects; 6% Unknown on Northwind (clear of 60% stop rule)
- Calibration: 4 fixes — query type constants (0-6 → DAO values), inline SQL RecordSource scanning, form F7b nav-target rule, table T4b FK-child+RS rule; 3 new tests added
- Verification: R1 via gemini-pro: VERIFIED ($0.00 — cost n/a from router)
- Deliverables: golden fixtures in samples/GoldenOutput/{HomeHealthCare,NorthwindDevEdition}/usage/, change-log.md, disposition.json, next-session-set-recommendation.md
- Notable: inline SQL scanning closed a signal gap where queries referenced via SELECT * FROM <name> WHERE... in report RecordSources were falsely classified PossiblyUnused
