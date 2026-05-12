# VBA Symbol DB — Path-B Probe Spec

> **Purpose:** Settle the Path A vs Path B question for the post-vba-symbol-db
> direction by running a one-procedure translation+test probe against the
> existing symbol DB substrate, after first closing a known cross-module
> resolver gap. Output is a decision document grounded in concrete probe
> evidence rather than predicted economics.
> **Created:** 2026-04-29
> **Session Set:** `docs/session-sets/vba-symbol-db-path-b-probe/`
> **Prerequisite:** `vba-symbol-db` is complete (verdict: PIVOT, change-log
> committed). The symbol DB at `tools/symbol-db/symbol-db.sqlite` is the
> input to Sessions 1-2.
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. The set produces a substrate fix (Session 1)
> and probe artifacts (Sessions 2-3) under `docs/session-sets/<slug>/` and
> `tests/`. Quality bar is build + tests + cross-provider AI verification
> per the harvester's standard. The Session 3 decision document is
> human-adjudicated inline at session close, not a UAT-checklist gate.

---

## Project Overview

### What this set delivers

1. A **fix to the cross-module reference writer** in
   `AccessHarvester.SymbolDb` so that call sites in
   `Form_frmCompanyDetail` referencing `modStrings`-internal symbols
   (`GetString`, `StringFormat`, the `enumStrings.s*` enum values) bind
   cleanly. Confirmed by a regression test plus a symbol DB rebuild.
2. A **Path-B probe**: ONE VBA procedure
   (`Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` — same target
   as the `vba-symbol-db` Session 7 comprehension probe) translated to
   C# using the symbol DB as input, with xUnit tests authored
   independently and run against the translation. POCO methods + faked
   form/control/DAO interfaces, no Blazor commitment.
3. A **decision document** (`decision.md`) grounded in the probe
   results: does the translated-and-testable substrate produce
   materially better epistemic signal than the AI-interpretation
   substrate? Recommendation for Path A vs Path B at scale, with
   explicit reasoning from the probe evidence.

### What this set does NOT do

- Does not pick a UI framework. The translated C# uses POCO methods
  plus *faked* form/control/DAO interfaces. Blazor / WPF / anything
  visual is out of scope.
- Does not translate macros, queries, or any other procedure besides
  the probe target. Path B at scale would do all of those; this set
  intentionally probes one procedure to settle the direction.
- Does not commit to a DAO->EF Core or DAO->Dapper substitution
  strategy. The faked DAO interface is the minimum surface area
  needed to make the translated procedure compile and run under
  test; downstream substitution is a follow-up decision.
- Does not produce a full Path-B program. If the decision is
  "Path B at scale," that is a follow-up multi-set program, not
  this one.
- Does not modify Sessions 1-3 of `vba-symbol-db` beyond the targeted
  resolver fix in Session 1 of this set.

### Why this shape

Three reasons, all argued in the closing conversation of
`vba-symbol-db` Session 7:

1. **The cross-module resolver gap is a known substrate defect** that
   would be re-paid by every future probe or interpretation pass; fix
   it once, before the next direction commits.
2. **Path A and Path B both consume the symbol DB**, but they make
   different bets on what kind of substrate produces the best
   downstream signal. Path A bets on indexed AI interpretations; Path B
   bets on translated-and-testable C#. The bet is decidable with one
   procedure, in a focused probe, before scaling either path.
3. **AI-generated tests can falsify interpretations** under provider
   independence (see `vba-symbol-db/change-log.md` and the closing
   discussion). The probe deliberately enforces independence by having
   the translation and the tests come from different orchestrators.

---

## Cross-module resolver gap (the Session 1 fix)

The `vba-symbol-db` Session 7 probe surfaced this defect:

The symbol DB's `[References]` table marks call sites in
`Form_frmCompanyDetail` to `GetString`, `StringFormat`, and the enum
value `sCompanyDelete_CompanyTypeChange` as
`resolution_status = 'unresolved'` with reason `'not-in-scope'`, even
though the corresponding declarations exist in the same DB under
`module_fqn = 'modStrings'`. The Sessions 1-2 reference writer in
`AccessHarvester.SymbolDb` is not consulting the project-wide module
inventory when binding cross-module references; it appears to bind
within-module only.

Concrete evidence (taken from
`docs/session-sets/vba-symbol-db/probe-results/comprehension-packet.md`,
"Direct References" table):

| identifier | actual to_fqn | actual resolution_status |
|---|---|---|
| `GetString` | _(unresolved)_ | unresolved (reason: not-in-scope) |
| `StringFormat` | _(unresolved)_ | unresolved (reason: not-in-scope) |
| `sCompanyDelete_CompanyTypeChange` | _(unresolved)_ | unresolved (reason: not-in-scope) |
| `IsFormOpen` | _(unresolved)_ | unresolved (reason: not-in-scope) |

The first three should bind to `modStrings.GetString`,
`modStrings.StringFormat`, `modStrings.enumStrings.sCompanyDelete_CompanyTypeChange`
respectively. `IsFormOpen` lives outside the two probe modules and
remains legitimately unresolved until full-DB extraction lands.

---

## Sessions
> Three sessions. Session 1 is a small focused fix. Sessions 2-3 are
> the actual Path-B probe, deliberately split across two orchestrators
> for cross-provider independence on translation vs. test-authoring.
>
> Mandatory Step 6 (build + test) and Step 8 (cross-provider
> session-verification) per `docs/ai-led-session-workflow.md` apply to
> every session.

### Session 1 of 3: Cross-Module Reference Writer Fix

**Steps:**

1. Required reading: `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`,
   `docs/planning/session-set-authoring-guide.md`. Re-read the
   `vba-symbol-db/verdict.md` and `change-log.md` for context on the
   resolver gap.
2. Locate the reference writer in `AccessHarvester.SymbolDb` (the
   Sessions 1-2 code that walks `ResolvedModule.References` and
   writes rows to `[References]`). Read it end-to-end so the fix is
   targeted, not speculative.
3. Author a hermetic regression test in
   `tests/AccessHarvester.SymbolDb.Tests/` that asserts a call site
   in `Form_frmCompanyDetail` referencing `GetString` resolves to
   `modStrings.GetString` (or whatever the resolver produces — read
   the existing `ReferenceResolver` output for the truth). Confirm
   the test fails on the current code (the gap is real) before
   writing the fix.
4. Fix the writer so cross-module references bind. The fix should
   consult the project-wide module inventory (or whatever
   `ReferenceResolver` already exposes) rather than re-implementing
   resolution logic. Mechanical correspondence task — Sonnet
   territory if the change is over ~50 lines, route via
   `code-review`.
5. Re-run the symbol DB build (the existing
   `tools/symbol-db/<entry-point>` driver — confirm with a
   one-symbol query that `GetString`-call sites now bind).
6. Write any additional regression tests that cover the obvious
   adjacent shapes: `StringFormat` call site binds, an `enumStrings.*`
   value reference binds, a within-module reference still binds (no
   regression), a genuinely-out-of-module-scope reference still
   resolves as unresolved with the right reason code.
7. `dotnet build && dotnet test` (sequential).
8. Cross-provider session verification per workflow Step 6.

**Creates:** Regression tests in
`tests/AccessHarvester.SymbolDb.Tests/`. No new C# project.

**Touches:** Reference writer in `AccessHarvester.SymbolDb/`. The
`tools/symbol-db/symbol-db.sqlite` build artifact (rebuilt and
re-committed if the project's existing convention treats it as a
checked-in artifact; otherwise leave to the next build).

**Ends with:** All four cross-module identifiers from the verdict
table now bind correctly in the rebuilt symbol DB; regression tests
green.

**Progress keys:** `session-1/required-reading`,
`session-1/regression-test-failing`,
`session-1/writer-fix`, `session-1/symbol-db-rebuild`,
`session-1/adjacent-tests`, `session-1/build-test`,
`session-1/verification`.

---

### Session 2 of 3: Translation Pass (one orchestrator authors)

> Run with **one** in-IDE orchestrator (Claude Code OR Codex,
> human's choice). Whichever runs Session 2 must NOT run Session 3 —
> Session 3's peer review needs a different orchestrator for
> cross-provider independence on the test-authoring side.
>
> Both translation and the orchestrator's own test-authoring run
> in-conversation per the in-conversation convention; routed cost
> for this session should be limited to the cross-provider
> session-verification step at the end.

**Steps:**

1. Required reading.
2. Build the translation packet for
   `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` from the
   (Session-1-fixed) symbol DB. Reuse and extend
   `tools/symbol-db/probe/build_packet.py` so the packet now includes
   the **source bodies of all directly-referenced procedures**
   (`CompanyIsActive`, `GetString`, `StringFormat` once those are
   resolvable cross-module) — the translator needs more context than
   the comprehension probe needed. Save to
   `docs/session-sets/vba-symbol-db-path-b-probe/translation-packet.md`.
3. Create a new test-only project
   `tests/AccessHarvester.TranslationProbe.Tests/` (xUnit, .NET 10).
   The project hosts:
   - The translated C# (`Form_frmCompanyDetail_cboCompanyTypeID_BeforeUpdate.cs`
     or similar; namespace `AccessHarvester.TranslationProbe`).
   - Faked form/control/DAO interfaces (`IFormContext`,
     `IComboBoxControl`, `IDaoCounts`, etc. — minimum surface area to
     make the translation compile and exercise).
   - The Session 2 author's own xUnit tests covering the procedure's
     observable behavior (cancel-when-active, no-cancel-when-inactive,
     dialog opened, undo called, error path delegates correctly).
4. Author the C# translation in-conversation. Document the
   translator's choices (what mapped 1:1, what required a stub, what
   needed a design call) in
   `docs/session-sets/vba-symbol-db-path-b-probe/translation/translation-rationale.md`.
5. Author the orchestrator's own xUnit tests. Save the test source
   alongside the translation; the rationale document gets a
   "test-coverage rationale" section explaining what each test
   asserts and which interpretation it would falsify if it failed.
6. Run the tests. Capture pass/fail and any test that exposed an
   ambiguity in the original VBA. Save raw output to
   `docs/session-sets/vba-symbol-db-path-b-probe/translation/test-run-session-2.md`.
7. Document gaps the translation surfaced: any symbol DB content the
   translator wished it had (additional Tables coverage,
   relationship metadata, etc.). Save to
   `docs/session-sets/vba-symbol-db-path-b-probe/translation/symbol-db-gaps.md`.
8. `dotnet build && dotnet test` (sequential; the new probe project
   should integrate cleanly with the existing test count).
9. Cross-provider session verification.

**Creates:**
- `docs/session-sets/vba-symbol-db-path-b-probe/translation-packet.md`
- `tests/AccessHarvester.TranslationProbe.Tests/` (project + tests +
  faked interfaces + the translated C#)
- `docs/session-sets/vba-symbol-db-path-b-probe/translation/translation-rationale.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/translation/test-run-session-2.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/translation/symbol-db-gaps.md`

**Touches:**
- `AccessHarvester.sln` (add the new test project)
- `tools/symbol-db/probe/build_packet.py` (extend to include callee
  source bodies)

**Ends with:** A compiling, runnable C# translation of the probe
target with the Session-2 orchestrator's own test suite passing
against it; the translation rationale and the symbol-DB-gaps
document captured.

**Progress keys:** `session-2/required-reading`,
`session-2/translation-packet`,
`session-2/probe-project-scaffold`,
`session-2/translation-authored`,
`session-2/own-tests-authored`,
`session-2/test-run`,
`session-2/symbol-db-gaps`,
`session-2/build-test`,
`session-2/verification`.

---

### Session 3 of 3: Peer Review + Decision Document (different orchestrator)

> Run with the **other** in-IDE orchestrator (whichever did NOT run
> Session 2). The point is cross-provider independence on the test
> side — Session 3's tests are the falsifier for Session 2's
> translation.
>
> Like Session 2, the synthesis work happens in-conversation;
> routed cost for the session is limited to the cross-provider
> session-verification step at the end.

**Steps:**

1. Required reading. Re-read Session 2's deliverables
   (`translation-rationale.md`, `test-run-session-2.md`,
   `symbol-db-gaps.md`) and the source of the translated C#.
2. **Independent semantic review** of the C# translation against the
   original VBA source body in the symbol DB. Note any place the
   translator's choice diverges from a reasonable reading of the
   original VBA. Save findings to
   `docs/session-sets/vba-symbol-db-path-b-probe/peer-review/translation-review.md`.
3. **Author additional xUnit tests** that target areas Session 2's
   tests did not cover or covered shallowly. Goal: at least one test
   that, if it had been written with the translation, would have
   forced a translation choice the Session 2 author did not make.
   Save to the same `tests/AccessHarvester.TranslationProbe.Tests/`
   project under a `Session3*Tests.cs` file naming convention.
4. Run the combined test suite (Session 2 + Session 3 tests). Capture
   pass/fail and any new ambiguities. Save raw output to
   `docs/session-sets/vba-symbol-db-path-b-probe/peer-review/test-run-session-3.md`.
5. **Author the decision document** at
   `docs/session-sets/vba-symbol-db-path-b-probe/decision.md`.
   Required content:
   - The probe's epistemic outcome: did the translation+tests
     surface signal the AI-interpretation pass would have missed?
     Be specific; cite which test or which review finding showed
     what.
   - Cost comparison: actual subscription token spend (qualitative
     OK) for this probe vs. the actuals from `vba-symbol-db`
     Sessions 4-7.
   - Recommendation: Path A at scale, Path B at scale, or a hybrid
     (and what the hybrid looks like).
   - Open follow-ups for whichever path is recommended (UI-framework
     decision, DAO substitution, macro translation, full-DB
     extraction sequencing, etc.).
6. Generate the set's `change-log.md` (Step 8 last-session
   requirement). Author in-conversation per the in-conversation
   convention; do NOT route `task_type="documentation"`.
7. Author the next-session-set recommendation. If the decision is
   Path A, point at the spec-to-be (suggested slug
   `vba-symbol-db-full-northwind`); if Path B, point at the
   first-of-many translation-pipeline set; if hybrid, surface what
   the human needs to scope.
8. `dotnet build && dotnet test`.
9. Cross-provider session verification.
10. (Per workflow Step 9) Reorganization review of
    `docs/planning/project-guidance.md` and
    `docs/planning/lessons-learned.md` after the notify.

**Creates:**
- `docs/session-sets/vba-symbol-db-path-b-probe/peer-review/translation-review.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/peer-review/test-run-session-3.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/decision.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/change-log.md`
- Additional `Session3*Tests.cs` file in
  `tests/AccessHarvester.TranslationProbe.Tests/`

**Touches:** None outside the new test files.

**Ends with:** A defensible decision document grounded in the
probe's evidence; the set is closed and the next-session-set
direction is named.

**Progress keys:** `session-3/required-reading`,
`session-3/translation-review`,
`session-3/independent-tests-authored`,
`session-3/test-run`,
`session-3/decision-document`,
`session-3/change-log`,
`session-3/next-set-recommendation`,
`session-3/build-test`,
`session-3/verification`,
`session-3/step-9-reorganization`.

---

## Routing notes

- **Session 1** is a focused C# bugfix. Sonnet is the right
  orchestrator if Claude is in the chair (or whatever the in-IDE
  Codex/GPT equivalent is). Routed `code-review` is appropriate for
  the writer change if it lands above ~50 lines; otherwise direct.
  Routed cost target: <$0.50.
- **Session 2** is heavy synthesis (translation + test authoring),
  but per the in-conversation convention it runs in-conversation
  under whichever in-IDE orchestrator the human picks. Routed cost
  for this session is essentially the cross-provider verification at
  the end; target <$0.30.
- **Session 3** is also heavy synthesis (peer review + decision
  authoring), in-conversation under the OTHER in-IDE orchestrator.
  Same cost target: <$0.30 routed.
- **Cross-provider verification stays mandatory** for every session
  per Rule #2 of the workflow doc. This survives the in-conversation
  convention.

## Cost expectations

- **Session 1:** <$0.50 routed (one verification round assumed; small
  fix scope).
- **Session 2:** <$0.30 routed (verification only; in-conversation
  authoring is subscription-paid).
- **Session 3:** <$0.30 routed (verification only).
- **Total set:** ~$1.10 routed AI cost. Subscription token spend on
  Sessions 2-3 is bounded by the human's existing Anthropic Max +
  GitHub Copilot windows.

This is intentionally an order-of-magnitude cheaper than
`vba-symbol-db`'s ~$24, because the rigor model has shifted from
"routed cross-provider on every per-symbol call" to
"in-conversation by subscription orchestrators with one cross-
provider verification per session." That shift IS the experiment;
the per-procedure cost is what tells us whether Path B's economics
generalize.

---

## Open questions for the human

- **Orchestrator assignment for Sessions 2 and 3.** The spec
  requires the two sessions to use different in-IDE orchestrators.
  Claude Code on Session 2 + Codex/GPT on Session 3, or the
  reverse, is the human's call. The decision can wait until
  Session 1 is done, but committing it before Session 2 starts is
  cleanest.
- **Faked-interface scope for Session 2.** The spec says "minimum
  surface area" for `IFormContext` / `IComboBoxControl` /
  `IDaoCounts`. If Session 2's translator wants to introduce more
  surface (e.g., a richer IDao for DCount/DLookup), the deviation
  should be flagged in `translation-rationale.md` so Session 3's
  reviewer can evaluate whether the richer surface is justified or
  premature.
- **Whether to commit the rebuilt `symbol-db.sqlite` after Session
  1's fix.** The existing convention from `vba-symbol-db` Session 1
  was to check it in. Worth confirming before Session 1 commits.
