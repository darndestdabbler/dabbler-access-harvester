# AI Assignment — 001-harvester-upstream-feedback-followups

## Session 1 of 3: Launcher Surfaces + Out-Of-Band Entry Uncertainty

**Recommended orchestrator:** `claude-opus-4-8` (claude-code)
**Rationale:** The primary task is high-complexity `architecture` synthesis —
defining the launcher/navigation-shell explanation contract and the out-of-band
entry-uncertainty model, then threading new signals through the usage classifier,
question generator, and schema. Opus is the routing target for `architecture`;
per project-guidance.md → Conventions ("When the orchestrator's model matches the
routing target, handle the task in-conversation rather than calling `route()`"),
an Opus orchestrator handles the synthesis in-conversation. The non-negotiable
exception is `session-verification`, which is routed cross-provider.
**Estimated routed cost:** Low ($0.05–$0.20) — only the cross-provider
verification round(s) are routed; design + implementation are in-conversation.

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read `harvester-upstream-feedback.md`, `access-domain-notes.md`, and inspect usage-side seams (`IrDatabase`, `IrLoader`, `UsageClassifier`, `QuestionGenerator`, `UsageChecklistWriter`, `NavigationEdgeCollector`) + golden HHC ground truth. | `In-conversation` |
| 2 | Define the launcher/menu-shell + missing-opener explanation contract. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 3 | Thread launcher/startup/out-of-band signals into the classifier, signal index, and question generator. | `In-conversation` + `tool-use` |
| 4 | Add `LauncherSurface` / `StartupEntry` signals and `OutOfBandEntryPossible` question text; reframe `UnreferencedObject`; retire `NoNavigationEntry` (kept in enum for backward compat). | `tool-use` |
| 5 | Add regression tests (switchboard form + helper macro/table, modal report-menu, startup-opened form, chrome-only negative guard, out-of-band questions). | `tool-use` |
| 6 | Pilot `identify-usage` against committed HHC golden IR (temp dir, no golden overwrite) for before/after evidence; run `dotnet build` + `dotnet test` sequentially. | `tool-use` |
| 7 | Cross-provider session verification. | `route(session-verification)` (cross-provider, MUST route) |
| 8 | Closeout: update `session-state.json`, append actuals here. | `tool-use` |

### Actuals (filled after the session)

- **Orchestrator used:** claude-code / claude-opus-4-8 @ effort=medium.
- **Total routed cost:** ~$0.975 USD — four gpt-5-4 session-verification rounds
  (R1 $0.230, R2 $0.295, R3 $0.250, R4 $0.250) plus one gemini-pro `analysis`
  route for the Session 2 recommendation ($0.0019). Design and implementation
  were handled in-conversation (Opus orchestrator matches the `architecture`
  routing target), so no synthesis routes were spent.
- **Deviations from recommendation:**
  - Four cross-provider verification rounds instead of the workflow's Step 7
    "max 2 retries" cap. The human explicitly authorized Round 4 (cap override)
    to confirm a clean VERIFIED, with the standing instruction "if R4 returns
    only trivial minors, fix post-hoc and close (no R5 chase)." R4 returned one
    trivial Minor (table launcher output re-derived from name alone); it was
    fixed post-hoc and the session closed without R5. No clean VERIFIED round was
    obtained; the verdict is recorded honestly as ISSUES_FOUND with all six
    findings resolved.
  - The pre-existing uncommitted cleanup-narrowing work + planning docs + this
    set's scaffold were landed as a separate baseline commit first (human choice)
    so the Session 1 diff stays purely usage-side.
- **Notes for Session 2 calibration:**
  - The verifier surfaced one new Minor per round, all of the same class:
    signal/summary text must agree with the actual classification *decision*, not
    be re-derived from the object name. R2 closed it for form signals, R3 for
    form/macro summaries, R4 for table summary/signal. Session 2 should apply the
    same discipline up front — any new parameter signal/summary must be gated on
    the rule that actually fired, and threaded as an explicit flag rather than
    recomputed from the name in BuildSignals/BuildSummary.
  - `frmReportParam_*` forms are intentionally still `Unknown` after Session 1
    (chrome-only launcher promotion was rejected to avoid catching parameter
    dialogs). They are Session 2's primary target.
  - The committed HomeHealthCare usage golden under
    `samples/GoldenOutput/HomeHealthCare/usage/` is now stale relative to the
    classifier and is intentionally NOT regenerated until Session 3.
  - Data-hygiene note (out of scope, flagged for the human): the prerequisite set
    `harvester-object-use-identifier/session-state.json` has a duplicate top-level
    `status` key (`in-progress` then `complete`); it parses as `complete` so the
    prerequisite is satisfied, but a naive reader could show N-1/N drift.

---

**Next-session orchestrator recommendation (Session 2):** claude-code /
claude-opus-4-8 @ effort=high. Rationale (routed via `gemini-pro` analysis,
do-not-self-opine): Session 2 ("Parameterized Queries + Parameter-Dialog Flows")
is synthesis-dominant — the mechanical plumbing (carrying saved-query `PARAMETERS`
metadata through `IrQuery`/`IrLoader`) is outweighed by the novel heuristics for
recognizing likely parameter-collection forms and report-open parameter flows and
for expressing parameter uncertainty distinctly. Per project-guidance.md,
novel-heuristic/architecture work earns Opus, handled in-conversation by an Opus
orchestrator (only session-verification routed). Estimated routed cost: Low
($0.05–$0.20, verification only).

---

## Session 2 of 3: Parameterized Queries + Parameter-Dialog Flows

**Recommended orchestrator:** `claude-opus-4-8` (claude-code) @ effort=high
**Rationale:** Synthesis-dominant — the mechanical plumbing (carrying saved-query
`PARAMETERS` metadata through `IrQuery`/`IrLoader`) is outweighed by the novel
heuristics for recognizing likely parameter-collection forms and report-open
parameter flows and for expressing parameter uncertainty distinctly. Opus is the
routing target for `architecture`; per project-guidance.md, an Opus orchestrator
handles the synthesis in-conversation, routing only `session-verification`.
**Estimated routed cost:** Low ($0.05–$0.20) — verification only.

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Inventory the usage-side seams (`IrQuery`/`IrLoader`/`UsageClassifier`/`QuestionGenerator`/`SignalIndex`/schema) + the Access `QueryMetadataExtractor` parameter model; ground the design in the HHC fixture. | `In-conversation` |
| 2 | Author the parameterized-query + parameter-dialog contract in the strategy doc (§2b/§3.9/F8/§5b/§6). | `In-conversation` (task type `architecture`, matches orchestrator) |
| 3 | Widen `IrQuery` with `Parameters`, populated at load by `IrLoader` (reusing `QueryMetadataExtractor.ParseParameters`). | `tool-use` |
| 4 | Add the `ParameterizedInput` signal + F8 parameter-dialog form rule (gated flags, not name-recompute); add `ParameterizedQuery`/`ParameterDialogFlow` questions with generic-question suppression. | `In-conversation` + `tool-use` |
| 5 | Regression tests (`ParameterDialogTests`). | `tool-use` |
| 6 | Pilot `identify-usage` against committed HHC golden IR (temp dir, no golden overwrite); `dotnet build` + `dotnet test` sequentially. | `tool-use` |
| 7 | Cross-provider session verification. | `route(session-verification)` (cross-provider, MUST route) |
| 8 | Closeout: issue log, ai-assignment actuals, disposition.json, `close_session`. | `tool-use` |

### Actuals (filled after the session)

- **Orchestrator used:** claude-code / claude-opus-4-8 @ effort=high.
- **Total routed cost:** ~$0.277 USD — two gpt-5-4 session-verification rounds
  (R1 $0.1421, R2 $0.1322) plus one gemini-pro `analysis` route for the Session 3
  recommendation ($0.0023). Design and implementation were handled in-conversation
  (Opus orchestrator matches the `architecture` routing target), so no synthesis
  routes were spent. (Set cumulative through Session 2: ~$1.25.)
- **Verdict:** VERIFIED (Round 2). Clean — unlike Session 1's ISSUES_FOUND close.
  Round 1 returned ISSUES_FOUND with two Minor findings, both test-coverage-only
  (no behavioral defects — the verifier confirmed concerns 1–5 correct): the
  `[Reports]!` control-reference path and the ODBC `?` marker path were implemented
  but unpinned. Fixed test-only (two added regressions); Round 2 returned a clean
  VERIFIED on the first re-try, well inside the Step 7 2-retry cap.
- **Deviations from recommendation:**
  - One mid-session design reconciliation: parameter parsing was first done
    on-demand in the classifier, then refactored to carry parsed parameters on
    `IrQuery.Parameters` (populated at load by `IrLoader.ExtractQueryParameters`) to
    honor spec Step 2's "widen the usage-side IR … through `IrQuery`/`IrLoader`"
    wording. Behavior-preserving — the pilot evidence was byte-identical before and
    after the refactor — and verifier-confirmed as the right placement.
  - Cost landed just above the Low estimate's high end ($0.277 vs $0.20) because two
    verification rounds were needed; both were warranted and R2 was clean.
- **Notes for Session 3 calibration:**
  - The committed HHC usage golden under `samples/GoldenOutput/HomeHealthCare/usage/`
    is now stale relative to BOTH the Session 1 launcher work and the Session 2
    parameter work, and is intentionally NOT regenerated until Session 3. Session 3's
    golden regen will bake in the cumulative launcher + parameter changes.
  - Both the Session 1 and Session 2 pilots showed checklist top-level keys remain
    canonical PascalCase. The spec's "verify casing, only patch if a fresh run
    drifts" task is likely a verification-only confirmation — honor the stop rule
    (do not touch casing code unless a current run actually drifts).
  - Parameter recognition surfaced cleanly on the 5 `frmReportParam_*` forms
    (Unknown 0.45 + `ParameterizedInput` + `ParameterDialogFlow`) and the 2
    declared-`PARAMETERS` queries (`ParameterizedInput` + `ParameterizedQuery`). If
    the Session 3 pilot review finds the `ParameterDialogFlow` / `ParameterizedQuery`
    question wording unclear, that is the place to calibrate it (in-scope wording
    only — not new heuristics).
  - The `QuestionGenerator` has a dormant `ParameterizedInput` → Report branch
    (reports do not fire the signal this session — scoped out deliberately). The
    verifier flagged it as future-facing, not a defect; it is ready if a later set
    adds report-side parameter flows.

---

**Next-session orchestrator recommendation (Session 3, final):** claude-code /
claude-opus-4-8 @ effort=high. Rationale (routed via `gemini-pro` analysis,
do-not-self-opine; the routed reply named a stale Opus model id, normalized here to
the current `claude-opus-4-8`): although the Session 3 body is predominantly
mechanical verification + committed-golden regeneration + checklist-casing
verification (which the conventions would route to Sonnet), Session 3 is the FINAL
session and its primary deliverables are the synthesis closeout artifacts —
`change-log.md`, `next-session-set-recommendation.md`, and the Step 9 reorganization
review (promoting lessons to conventions/principles). Per project-guidance.md, that
high-level synthesis earns Opus, handled in-conversation, with only the
cross-provider `session-verification` routed. Estimated routed cost: Low
($0.05–$0.20, verification only).
