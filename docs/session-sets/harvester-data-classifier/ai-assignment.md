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

- **Orchestrator used:** `claude-opus-4-7` at effort=medium (matched the
  recommendation).
- **Total routed cost:** ≈$0.69 — R1 verification $0.22 (GPT-5.4), R2 $0.20,
  R3 $0.13, R4 $0.14, Session-2-recommendation analysis (Gemini Pro) $0.002,
  R4 tiebreaker (Gemini Pro) $0.003. No Step-2 architecture route call;
  per the project-guidance convention "When the orchestrator's model
  matches the routing target for a given task type, handle the task
  in-conversation rather than calling `route()`," the schema was authored
  in-conversation because the `architecture` task type routes to Opus, and
  the orchestrator was already Opus.
- **Deviations from recommendation:** None on orchestrator choice. The
  routed cost ran higher than the original $0.05–$0.15 estimate because
  verification went four rounds (R1→R2→R3 closed substantive findings on
  the privacy regex and FK authority; R4 surfaced a contrived edge case
  that was adjudicated as accept-dismissal after a Gemini Pro tiebreaker
  and human confirmation per the Step 7 disagreement procedure).
- **Notes for next-session calibration:** The keystone-schema arc
  reinforced two existing conventions: (1) Opus is the right orchestrator
  for synthesis-document work on long-lived contracts even when each
  individual decision is small — the breadth of cross-cutting concerns
  (10-category coverage, privacy invariant, FK authority, editor-on-paper,
  sanitizer-on-paper, Access gotchas) is what justifies the cost; (2) the
  prose-before-JSON verification lead worked well for closeout-grade
  review — every round returned substantive analysis, not bare verdicts.
  A new lesson candidate emerged on multi-round convergent verification:
  when each round closes real issues but surfaces a smaller residual, the
  workflow's max-2-retries rule should be paired with a clear escalation
  path (orchestrator + tiebreaker + human option set), which this session
  exercised. Worth logging as a lessons-learned candidate at Step 9.

---

**Next-session orchestrator recommendation (Session 2):** `claude-sonnet-4-6` at effort=high (gemini-pro analysis 2026-05-28: "The key architectural decisions are already documented in the plan, making this session a task of implementation rather than design. Sonnet is better suited and more cost-effective for this type of mechanical correspondence, translating a detailed specification into C# code.")

## Session 2 of 4: Scanner CLI

**Recommended orchestrator:** `claude-sonnet-4-6` at effort=high
**Rationale:** Recommendation updated post-Session 1 via
`route(task_type="analysis")` to Gemini Pro: the plan section 3 + section 8
have already captured the architecture decisions, so Session 2 is mostly
mechanical correspondence (OLE DB schema rowset reads, DAO interop for
AutoNumber and RowSource, regex application for PII detection, subquery
cardinality). Per project-guidance "Pin mechanical-correspondence task
types to Sonnet rather than Opus", Sonnet at high effort is the right
cost band; cross-provider verification stays on to mitigate the
cheaper-model false-VERIFIED risk.
**Estimated routed cost:** Low-Moderate ($0.05–$0.15)

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
