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

- **Orchestrator used:** `claude-opus-4-7` at effort=medium (operator override of
  the routed Sonnet-at-high recommendation; the human triggered the session
  with the default trigger phrase, which retained the Anthropic Max window's
  Opus orchestrator).
- **Total routed cost:** ≈$1.01 — R1 session-verification (gpt-5-4) $0.384,
  R2 (gpt-5-4) $0.387, R3 (gpt-5-4) $0.233, Session-3 recommendation
  (gemini-pro) $0.009.
- **Deviations from recommendation:** Orchestrator. The routed analysis
  picked Sonnet-at-high; the operator used Opus-at-medium. In practice this
  was net-positive because the scanner pipeline turned out to need three
  rounds of cross-provider verification to settle (R1 surfaced five issues,
  R2 surfaced four more, R3 VERIFIED) and each fix touched multiple files
  with interleaved correctness, security, and resource-hygiene concerns —
  exactly the kind of cross-cutting synthesis Opus handles well. Routed cost
  came in within an order of magnitude of the Sonnet estimate ($0.05–$0.15)
  because verification ran three times.
- **Notes for next-session calibration:** Two existing lessons were
  re-confirmed here:
  (1) the **prose-before-JSON closeout-grade verification lead** kept every
  verifier round substantive — R1 returned a 7-paragraph audit, R2 a
  7-paragraph audit with new findings, R3 a 6-paragraph audit with a
  VERIFIED verdict, and not one round needed a re-prompt for the JSON block;
  and (2) the **"Send related files to the cross-provider verifier"**
  convention paid off — including the prior round's review file in R2 and
  R3 let the verifier explicitly check whether the prior findings were
  resolved rather than re-flagging from scratch. A new
  lesson-candidate worth logging at Step 9: when a session ships substantial
  COM-using code, the round-1 verification will almost always flag RCW
  hygiene + SQL identifier escaping; pre-empting both in the first cut
  would save a round.

---

**Next-session orchestrator recommendation (Session 3):**
`claude-sonnet-4-6` at effort=normal (gemini-pro analysis 2026-05-28: "The
task is to implement a single-file HTML/JS editor from a detailed
specification and a pre-existing JSON schema, which is a clear example of
mechanical correspondence. Per project guidance, this work is pinned to the
more cost-effective Sonnet model, as the architectural synthesis was
completed in Session 1. The only open decision (repo location) is
operator-driven, not a task for the AI."). Estimated routed cost: Low
($0.03–$0.10).

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

- **Orchestrator used:** `claude-opus-4-7` at effort=medium (operator override of
  the routed Sonnet-at-normal recommendation; the human triggered the session
  with the default trigger phrase, retaining the Anthropic Max window's Opus
  orchestrator — same pattern as Sessions 1 and 2).
- **Total routed cost:** ≈$0.35 — R1 session-verification (gpt-5-4) $0.214,
  R2 (gpt-5-4) $0.133, Session-4 recommendation (gemini-pro) $0.005. A R2
  prose-first retry was attempted (the initial R2 returned a bare
  VERIFIED JSON block with no analysis prose) but hit OpenAI HTTP 429
  rate-limit after the router's three internal retries and was abandoned per
  the verifier-failure escalation ladder. The R1 fix was mechanically narrow
  (load schema + assert enum equality + add probes) and is independently
  confirmed by `node tools/classification-editor-js-test.mjs` returning OK
  on the post-fix file.
- **Deviations from recommendation:** Orchestrator. The routed analysis
  picked Sonnet-at-normal; the operator used Opus-at-medium. The mechanical
  build itself fit within one round of routing (R1 surfaced one Minor JS-test
  finding that fixed in ~5 minutes), so the deviation cost was small —
  $0.35 vs. the original $0.03–$0.10 Sonnet estimate, all driven by
  verification rounds, not orchestrator inflation.
- **Notes for next-session calibration:** Two lesson-candidates worth surfacing
  at Step 9 (deferred to Session 4 close as this is not the final session):
  (1) **R2 bare-verdict + 429 abandonment pattern** — on a narrow R2 scope
  (verify a single mechanical fix landed), gpt-5-4 returned a bare JSON block
  with no prose. The convention's prose-first reminder retry is documented for
  closeout-grade prompts; this session demonstrates the retry is also useful
  on bare R2 verdicts, but the verifier-failure escalation ladder must be
  honoured when the retry hits a provider rate limit. Recommend the existing
  "Bare JSON Verdict From The Verifier" lesson in `lessons-learned.md`
  reference the verifier-failure ladder as the fallback when the retry can't
  reach the provider. (2) **Cross-provider verification reading single-file
  HTML editors** — the R1 review was strong (clean concern-by-concern audit,
  one actionable Minor finding) and is a good calibration point for "the
  verifier can read JS embedded in an HTML script tag and reason about its
  JSON-schema contract without external infrastructure" — applicable to any
  future single-file editor or static tool the repo ships.

---

**Next-session orchestrator recommendation (Session 4):** `claude-opus-4-7` at
effort=high (gemini-pro analysis 2026-05-28: "The task requires synthesizing a
correctness-critical algorithm for the FK graph builder and lockstep remapping,
which has a significant correctness risk. This choice aligns with the
`synthesis-document-Opus-orchestrator-when-large` convention for complex
synthesis and is consistent with prior sessions being manually escalated to
Opus for complex tasks."). Estimated routed cost: Moderate ($3.50–$6.00).

## Session 4 of 4: Test-Data Generator CLI

**Recommended orchestrator:** `claude-opus-4-7` at effort=high
**Rationale:** Updated post-Session 3 via `route(task_type="analysis")` to
gemini-pro: the sanitizer is correctness-critical synthesis — the FK graph
builder + lockstep PK/FK remap have a real correctness risk if remapping
ordering is wrong or composite-FK degradations are mishandled. The
masking-handler matrix is mechanical, but the overarching algorithm and the
output-format decision (`.accdb` vs CSV) are synthesis work. Plus this is the
final session of the set; final-session closeout (disposition.json,
change-log.md, next-session-set-recommendation.md, Step 9 reorganization
proposals) compounds the work. Opus at `effort=high` is the right band.
**Estimated routed cost:** Moderate ($3.50–$6.00)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read Session 1 schema and data-classification-plan.md § 3.5 and § 5. | `In-conversation` |
| 2 | Decide output format (`.accdb` vs. CSV) and document in actuals. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 3 | Implement `SanitizeCommand.cs`, `MaskingEngine.cs`, `FkGraphBuilder.cs`, category handlers. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 4 | Write C# files to `AccessHarvester.Pipeline/Sanitization/`. | `tool-use` |
| 5 | Write unit tests and `[SkippableFact]` integration tests. | `tool-use` |
| 6 | Run `dotnet build && dotnet test`. | `tool-use` |
| 7 | Run session verification. | `route(task_type="session-verification")` (gpt-5-4 per task_type_overrides) |
| 8 | Closeout: disposition.json, change-log.md, next-session-set-recommendation.md. | `route(summarization, max_tier=1)` for change-log; `route(analysis)` for next-set-recommendation. |
| 9 | Step 9 reorganization review (lessons-learned promotions + project-guidance updates from this set). | In-conversation, human accepts/rejects each candidate per workflow. |

### Actuals (filled after the session)

- **Orchestrator used:** `claude-opus-4-7` at effort=high (matched the
  recommendation — the FK graph builder + lockstep PK/FK remap was the
  correctness-critical synthesis the recommendation called out, and final-
  session closeout work compounded the synthesis count).
- **Total routed cost:** ≈$0.08 — R1 session-verification (gemini-pro after
  gpt-5-4 429 fall-back per the verifier-failure escalation ladder) $0.074,
  next-session-set recommendation (gemini-pro analysis) $0.003. The original
  $3.50–$6.00 estimate was conservative against the synthesis-document
  timeout convention; in practice, the orchestrator handled the synthesis
  in-conversation (the "When the orchestrator's model matches the routing
  target" convention) so the only routed traffic was the mandatory cross-
  provider verification + the next-set analysis.
- **Output-format decision:** **CSV per table** (single decision made at
  session start, before any code was written). Rationale documented in
  the change-log.md and in `SanitizeCommand`'s class summary. Two reasons,
  in order of weight: (1) the spec's own FK-remap stop rule names `.accdb`
  lockstep remap via DAO as fragile and points at CSV as the safe default;
  (2) CSV is hermetically testable — the unit-test suite exercises every
  masking handler and the FK consistency invariant against an in-memory
  row source with zero COM, while `.accdb` mutation would have required
  integration-test-only coverage. CSV also serves both downstream uses
  (test-data + the deferred Phase 4 SQL Server migration generator).
- **Deviations from recommendation:** None on orchestrator choice. The
  recommendation called for the per-strategy handlers to live under
  `MaskingHandlers/*.cs`; the implementation collapses them into a single
  `MaskingHandlers.cs` file because each handler's body is 3-15 lines and
  a per-strategy folder would not pay for the file-overhead. The R1
  verifier examined this and confirmed it (concern #10): "a reasonable
  choice for code organization and does not affect functionality." The
  decision is reversible if a future masker grows into a multi-file shape.
- **Notes for next-session calibration:** Three observations worth logging
  at Step 9 (this is the final session, so Step 9 lessons get proposed
  here and the human accepts/rejects each):
  (1) **The verifier-failure escalation ladder works in practice.** R1
  hit a 429 on the configured gpt-5-4 verifier on the first attempt. Per
  the ladder (project-guidance.md → Conventions → Workflow Expectations),
  step 2 is "switch to the other remaining cross-provider verifier" —
  fall-back to gemini-pro via `query("gemini-pro", ...)`. That call
  succeeded immediately with substantive 10-concern prose at $0.07. The
  ladder paid for itself in a session that would otherwise have stalled
  on a transient OpenAI rate limit. Worth recording so future sessions
  with the same transient-failure shape execute the fall-back
  mechanically.
  (2) **Avoid chaining COM-using integration tests against the same .accdb
  in parallel xUnit fixtures.** The first SanitizeCommandTests draft
  chained `classify` → `sanitize` against `HomeHealthCare.accdb` and hit
  a 4-10 minute timeout from xUnit parallelizing the new test class
  against the existing `ClassifyCommandTests`. Direct CLI runs of either
  subcommand finished in 6 seconds. The fix was to use a synthesized
  minimal classification.json (one small lookup table) so the new test
  doesn't need to chain classify at all — the integration test stays
  focused on what it's actually proving (CLI wire-up + OLE DB row source),
  not on classify's behaviour which is already tested elsewhere. Promotion-
  trigger: a second xUnit-parallel COM contention pattern in this repo.
  (3) **`OleDbRowSource.ReadRows` connection-per-call pattern works because
  the row reader is iterator-driven**. Each table read opens a connection,
  streams via `CommandBehavior.SequentialAccess`, and closes the connection
  when the enumeration ends. Worth recording so a future `migrate`
  subcommand that needs cross-table transactions does NOT inherit the
  same pattern — transactional row reading needs a connection that lives
  across multiple SELECTs.
