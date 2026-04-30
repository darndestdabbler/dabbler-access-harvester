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

## Session 2 Actuals

- **Orchestrator used:** GitHub Copilot Chat / GPT-5.4 (Codex) —
  matches the Session 1 recommendation that Codex draft the Add an
  Order package and leave Session 3's peer review to Claude for
  cross-provider independence.
- **Deliverables produced:** `packages/add-an-order.md` plus four
  AddAnOrder characterization files covering order defaults,
  customer tax-status defaulting, order save validation, line-item
  validation and uniqueness, allocation ordering/status filtering,
  and reallocation behavior.
- **Total routed cost:** $2.03364 across two Claude Opus
  `session-verification` calls (Round 1: $1.43628 for 32,712 input /
  12,608 output tokens; Round 2: $0.59736 for 16,904 input / 4,584
  output tokens). No other routed work was used in Session 2.
- **Verification verdict:** VERIFIED. Round 1 returned 6 grounded
  findings; Session 2 fixed all of them, and Round 2 returned
  `VERIFIED` with zero remaining issues.
- **Deviations from recommendation:** The orchestrator choice matched
  the recommendation, but verification intentionally deviated from the
  spec's Gemini Pro pin because the human explicitly instructed that
  this session's only external routing should be cross-provider
  verification via Claude Opus.
- **Notes for Session 3 calibration:**
    - Session 3 should review the Add-an-Order package and tests
      against the original harvester evidence independently, not treat
      Session 2's phrasing as authoritative. The most fruitful review
      targets remain missing invariants, over-confident rule wording,
      and anti-coupling drift in characterization tests.
    - The verifier-surfaced fixes strengthened several important edges:
      `OrderDate` defaulting, line-item save validation, the unique
      `(OrderID, ProductID)` constraint, cross-order allocation scope,
      the `(1,4,5)` reallocation status filter, and the negative cache
      precondition for prior-product reallocation.
    - The open questions still worth pressure-testing are whether
      customer-derived tax status is merely a default or effectively
      authoritative, and whether the `OrderID` tiebreak inside the
      oldest-first allocation policy is business-visible or just an
      implementation detail.

## Session 3 Recommendation

**Next-session orchestrator recommendation (Session 3):** Claude Opus
4.7 (Claude Code), in-conversation for the peer review and
falsification work.

**Rationale:** The set spec explicitly calls for the *other*
orchestrator to run Session 3 so Add-an-Order receives a genuinely
independent second pass. Session 2 used Codex/GPT-5.4, so Session 3
should move to Claude Opus for the package review, test falsification,
and ambiguity-log tightening. That keeps the path-b-probe pattern
intact: one provider drafts, the other provider tries to break it.

If the human does not repeat the Session 2 override, cross-provider
verification for Session 3 should revert to the set default of Gemini
Pro. If the human wants Claude Opus reserved for verification again,
the orchestrator assignment should be revisited because Session 3's key
requirement is provider independence from Session 2.

## Session 3: Add an Order — Peer Review and Falsification

### Recommended orchestrator
Claude Code Opus 4.7 @ effort=high

### Rationale
Session 2 was authored by Codex/GPT-5.4; the spec mandates the *other*
orchestrator run Session 3 so Add-an-Order gets cross-provider
falsification rather than self-review. Claude Opus 4.7 in-IDE handles
the package re-read, the source-IR cross-check, and falsification-test
authoring at marginal-zero per-call cost (Anthropic Max window).

### Estimated routed cost
Low — one Gemini Pro `session-verification` call.

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Required reading; re-read package + tests | In-conversation (orchestrator) |
| 2    | Independent semantic review of package | In-conversation (per session-local direction) |
| 3    | Independent review of characterization tests | In-conversation |
| 4    | Author falsification tests in `AddAnOrder/Session3*Tests.cs` | In-conversation |
| 5    | Refine package (rules, ambiguity log) | In-conversation |
| 6    | `dotnet build && dotnet test` (sequential) | Local |
| 7    | Cross-provider session verification | Routed → Gemini Pro |

### Session-Local Direction

- Per the human's instruction at the start of this session: no routed
  reasoning except the mandatory cross-provider session verification.
- Cross-provider verification routes to **Gemini Pro** per the spec's
  Routing Notes pin (Session 2's verification went to Claude Opus per
  a one-off override; Session 3 reverts to the set default).
- Per the lessons-learned entry "Verify The Prior Session Actually
  Committed And Pushed Before Starting A New One," Session 2's
  deliverables (`packages/add-an-order.md`, four AddAnOrder test
  files, `session-reviews/session-002.md`, `issue-logs/session-002.json`,
  and the Session 2 actuals appended to this file) were untracked at
  the start of Session 3. They are bundled into the Session 3 commit
  with disclosure in the commit message rather than created as a
  separate catch-up commit.

### Actuals (filled after the session)
- **Orchestrator used:** Claude Opus 4.7 (Claude Code) — matches the
  Session 2 recommendation (other-provider for Session 3 peer review).
- **Total routed cost:** $0.0356 (one Gemini Pro `session-verification`
  call: 18,226 input tokens / 1,277 output tokens).
- **Verification verdict:** VERIFIED, 0 issues. Verifier (Gemini Pro)
  confirmed test faithfulness, business-rule justification, prose
  tightening accuracy, ambiguity-log conservatism, anti-coupling, and
  falsifiability.
- **Deviations from recommendation:** None on orchestrator choice.
  Per session-local direction, all reasoning was in-conversation; only
  cross-provider verification was routed (Gemini Pro per spec default,
  reverting from Session 2's one-off Claude Opus override).
- **Notes for Session 4 calibration:**
    - Session 3 found no factual errors in Session 2's package or
      tests. The two Round-1+Round-2 verification rounds Session 2 went
      through (with Claude Opus as the verifier) appear to have caught
      the substantive content gaps. Session 3's value-add was three
      additional invariant pins (deletion-reallocation, cross-order
      scope, OrderID tie-break) and four open-questions entries that
      surface intent-vs-implementation ambiguities the original review
      did not surface.
    - Session 2's untracked deliverables were bundled into the Session 3
      commit per the lessons-learned entry "Verify The Prior Session
      Actually Committed And Pushed Before Starting A New One." This is
      the **second** confirmed hit on this failure mode (first was
      `vba-symbol-db-path-b-probe` Session 3 catching Codex's identical
      lapse). The lesson should be promoted to a Convention in
      `project-guidance.md` at Session 7's Step 9 reorganization
      review, and a `verify_prior_session_committed()` helper should be
      added to `ai-router/session_log.py` so the check is mechanical
      rather than depending on the next orchestrator's discipline.
    - Sessions 4-6 (`update-an-order`, `update-a-product`,
      `update-an-employee`) are single-pass single-author per the
      spec — no peer-review session for them. They will rely on
      cross-provider session-verification only as the independence
      check.

## Session 4 Recommendation

**Next-session orchestrator recommendation (Session 4):** Codex /
GPT-5.4 (GitHub Copilot Chat), in-conversation single-pass authoring.

**Rationale:** Per the spec's "Routing Notes" guidance to alternate
orchestrators across the three update-workflow sessions, Session 4
should swap back to Codex after Claude ran Session 3. This sets up the
Sessions 4-6 alternation (Codex → Claude → Codex, or Codex → Claude →
either, depending on Session 7 orchestrator preference). Session 4 is
single-pass (no peer review session) per the spec's "compressed into
one session" framing for the three updates, so the cross-provider check
is solely the routed `session-verification` call at end of session.
Verification should route to **Claude Opus** (cross-provider from
Codex) unless the human pins Gemini Pro.

No routed analysis was used for this recommendation: per the
session-local direction above, this Session 3 used in-conversation
reasoning for ai-assignment authoring.

## Session 4 Actuals

- **Orchestrator used:** GitHub Copilot Chat / GPT-5.4 (Codex) —
  matches the Session 3 recommendation to alternate back to Codex for
  the first single-pass update workflow.
- **Deliverables produced:** `packages/update-an-order.md` plus three
  UpdateAnOrder characterization files covering state transitions,
  save/delete policy, and status-driven editability.
- **Total routed cost:** $1.24386 across two Claude Opus
  `session-verification` calls (Round 1: $1.011465 for 18,031 input /
  9,880 output tokens; Round 2: $0.232395 for 9,768 input / 1,145
  output tokens). No other routed work was used in Session 4.
- **Verification verdict:** VERIFIED. Round 1 returned `VERIFIED` with
  one minor missing-coverage note (Paid lock-set assertion); the test
  gap was fixed immediately, and Round 2 returned `VERIFIED` with zero
  issues.
- **Deviations from recommendation:** None on orchestrator choice.
  Verification also matched the Session 3 recommendation because the
  human explicitly pinned cross-provider verification to Claude Opus for
  this session.
- **Notes for Session 5 calibration:**
    - The richest Update-an-Order surface was the order-status state
      machine, not extra query spelunking. Session 5 should keep the
      same discipline: focus on the behavior-bearing cascade rules and
      avoid over-documenting Access UX mechanics.
    - `LockControls` proved worth characterizing because it encodes a
      concrete editability policy, but the package also records that the
      exact field matrix may mix business intent with Access UX
      convenience. Similar caution will matter for Update-a-Product's
      cascade surface.
    - Session 4 again confirmed that the local IR is sufficient for the
      package/test workflow; no temp symbol DB rebuild was needed.

## Session 5 Recommendation

**Next-session orchestrator recommendation (Session 5):** Claude Opus
4.7 (Claude Code), in-conversation single-pass authoring for
`update-a-product`.

**Rationale:** The spec asks Sessions 4-6 to alternate orchestrators
where practical. Session 4 used Codex/GPT-5.4, so Session 5 should swap
to Claude Opus for variety and independence before Session 6 returns to
Codex or stays flexible for Session 7 planning. The `update-a-product`
workflow is still single-pass, but it has the widest cascade surface of
the remaining three and is a good fit for Claude's synthesis across
product, inventory, order, and purchase-order evidence.

If the human repeats the Session 4 instruction to route verification to
Opus, revisit the assignment before starting Session 5: Opus cannot be
the cross-provider verifier for an Opus-authored session. In that case,
either use Codex for Session 5 authoring or keep Claude as author and
route verification to Gemini Pro or another non-Anthropic verifier.

No routed analysis was used for this recommendation: per the
session-local direction above, this Session 4 used in-conversation
reasoning for ai-assignment authoring.
