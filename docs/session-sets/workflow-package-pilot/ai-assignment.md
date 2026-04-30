# AI Assignment: workflow-package-pilot Session 1

- **SessionId**: `workflow-package-pilot-s1`
- **Orchestrator**: `Claude Opus 4.7 (via Claude Code)`

## Task

Set up the `workflow-package-pilot` session set. Surface the two Open
Decisions in the spec to the human and capture their answers; inventory
the four pilot-workflow artifacts in the existing NorthwindDevEdition
harvester output; author the workflow-package template; scaffold the
`AccessHarvester.CharacterizationTests` xUnit project and add it to
the solution; build and test; and run the mandatory cross-provider
session verification.

## Session-Local Direction

- Per the human's instruction at the start of this session, no routed
  reasoning is used in Session 1 **except** the mandatory cross-provider
  session verification. This is consistent with the spec's "Cost
  Expectations" — `ai-assignment.md` authoring, next-session
  recommendations, and the change log are in-conversation.
- Mandatory cross-provider verification routes to **Gemini Pro**, per
  the spec's "Routing Notes" section pinning verification for this
  set.

## Open Decisions Resolved

Per Session 1 Step 2 of the spec, both Open Decisions were surfaced to
the human via `AskUserQuestion` and answered:

- **Decision 1 — Source-operationalization format:**
  **(b) Structured prose** (numbered click-sequence in markdown).
  Spec default; promote to UAT DSL in a follow-up set if the
  cross-workflow review (Session 7) shows the dsl form would have
  added value.
- **Decision 2 — Component / role mapping hints format:**
  **(a) Generic-now** (implementation-agnostic hints). Spec default;
  library-specific hints can be added in a follow-up pass once the
  Blazor library is documented enough to support a clean mapping.

Both decisions are captured in the workflow-package template
(`_template.md`) so Session 2 onwards applies them automatically.

## Acceptance Criteria

- [x] Required reading completed: `docs/planning/project-guidance.md`,
      `docs/planning/lessons-learned.md`,
      `docs/planning/session-set-authoring-guide.md`, and a re-read of
      `spec.md` end-to-end.
- [x] Both Open Decisions surfaced to the human and resolved
      (Decision 1 = b, Decision 2 = a — both spec defaults).
- [x] Inventory of NorthwindDevEdition pilot workflows authored at
      `docs/session-sets/workflow-package-pilot/inventory.md`. One
      section per pilot workflow with located artifacts (forms,
      bound queries, underlying tables, event handlers, candidate
      behavior-bearing procedures, module-helper references).
- [x] Workflow-package template authored at
      `docs/session-sets/workflow-package-pilot/_template.md` with all
      seven sections from the spec, instructional comments, and the
      author checklist.
- [x] Test project scaffolded at
      `tests/AccessHarvester.CharacterizationTests/` (xUnit, .NET 10),
      added to `AccessHarvester.sln`, with one folder per pilot
      workflow slug (`AddAnOrder/`, `UpdateAnOrder/`, `UpdateAProduct/`,
      `UpdateAnEmployee/`).
- [x] `session-state.json` registered at session start, flipped to
      `complete` at session end with the verification verdict.
- [x] `dotnet build` completes without errors.
- [x] `dotnet test` completes with all tests passing (the new project
      has zero tests by design — Session 1 is scaffold only).
- [x] Cross-provider session verification via Gemini Pro returned a
      verdict of `VERIFIED` (or `ISSUES_FOUND` with all issues
      addressed).

## Context

The four pilot workflows from `spec.md` map cleanly to identifiable
artifacts in the NorthwindDevEdition harvester output (verified during
the inventory pass — see `inventory.md`'s "Identification verdict"
section). No harvester gap blocks any pilot workflow at the
identification level. The interpretive work — naming behavior-bearing
procedures, citing business rules with confidence tags, pinning
characterization-test invariants — is sessions 2-6's responsibility.

The committed `tools/symbol-db/symbol-db.sqlite` was NOT rebuilt by
this session (per the path-b-probe pattern). The local IR at
`tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/` was the
inventory source.

## Next Session

### Forward-looking Recommendation for Session 2

Per the spec's "Routing Notes" section, Session 2 (Add an Order —
package draft) should be authored by the **other** in-IDE orchestrator
so that Session 3's peer review is cross-provider. The spec
recommends:

- **Session 2 author:** Codex / GPT-5.x in GitHub Copilot Chat
  (matching the path-b-probe pattern where Codex authored the
  translation and Claude peer-reviewed).
- **Session 3 reviewer:** Claude Code / Opus 4.7.

If the human prefers to swap (Claude authors, Codex reviews), the
spec explicitly accepts this. The constraint that survives is
cross-provider independence between Session 2 and Session 3 — the
specific assignment is the human's call.

No routed analysis was used for this recommendation: per the
session-local direction above, this Session 1 used in-conversation
reasoning for ai-assignment authoring.

## Session 1 Actuals

- **Orchestrator used:** Claude Opus 4.7 (Claude Code) — matches
  spec's "Routing Notes" recommendation for Session 1 ("Claude Opus
  4.7 @ effort=high (Claude Code) for the inventory pass — needs to
  read across many harvester artifacts").
- **Total routed cost:** $0.0260 (one Gemini Pro
  `session-verification` call: 17,270 input tokens / 443 output
  tokens). No other routed work, per session-local direction.
- **Verification verdict:** VERIFIED, 0 issues. Verifier (Gemini
  Pro) confirmed inventory correctness, template completeness,
  ai-assignment fidelity, test scaffold appropriateness, and
  spec adherence. Note: the verifier emitted the JSON block with
  the key `"verification_result"` rather than `"verdict"`. The
  verdict value (`VERIFIED`) is unambiguous; this is a minor
  template-key drift, not an outcome issue.
- **Deviations from recommendation:** None on orchestrator choice.
  Per session-local direction, no routed analysis was used for
  ai-assignment authoring or next-session recommendation
  (in-conversation only). This matches the spec's "Cost Expectations"
  section explicitly.
- **Notes for Session 2 calibration:**
    - Inventory found the four pilot workflows clean to identify; no
      harvester gap blocks Session 2's start. Session 2 can begin
      against `frmOrderDetails` plus `modValidation`,
      `modInventory`, and `modOrders` immediately.
    - The `tmp/comprehension-ir/.../NorthwindDevEdition/ir/` tree is
      the canonical IR source for this set. Sessions 2-6 should read
      directly from it rather than rebuilding the symbol DB. If a
      session needs symbol-DB-style joins (cross-module references,
      event-binding rows beyond what the IR captures), it should
      build a temporary SQLite per the path-b-probe pattern, not
      overwrite the committed `tools/symbol-db/symbol-db.sqlite`.
    - Open Decisions are recorded in `_template.md`. Sessions 2-6
      apply them by following the template; no need to re-decide.
    - The new `AccessHarvester.CharacterizationTests` test project
      has zero tests in Session 1. Session 2 adds tests under
      `AddAnOrder/`. Each subsequent session adds tests under its
      own folder.
