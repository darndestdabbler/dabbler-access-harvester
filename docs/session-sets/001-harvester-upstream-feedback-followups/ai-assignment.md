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
