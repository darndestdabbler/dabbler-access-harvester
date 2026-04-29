# AI Assignment: vba-symbol-db-path-b-probe Session 1

- **SessionId**: `vba-symbol-db-path-b-probe-s1`
- **Orchestrator**: `Claude Opus 4.7 @ effort=high (via Claude Code)`

## Task

Fix the VBA reference resolver to correctly bind public symbols across modules, specifically from a Form module to a Standard module. This involves authoring regression tests, implementing the fix, rebuilding the symbol database, and verifying the outcome.

## Acceptance Criteria

- [x] A hermetic regression test is authored in `tests/AccessHarvester.SymbolDb.Tests/` asserting that a call site in `Form_frmCompanyDetail` referencing `GetString` correctly binds to the `modStrings.GetString` declaration.
- [x] `AccessHarvester.Vba.Resolver.References.ReferenceResolver.ResolveIdentifier` is modified to look up public declarations in sibling Standard modules within the same `ProjectScope` when a direct scope-chain lookup fails.
- [x] Adjacent regression tests are authored and pass for:
    - [x] The `StringFormat` call site binding correctly.
    - [x] An `enumStrings.s*` value reference binding correctly.
    - [x] An existing within-module reference continuing to bind correctly.
    - [x] A genuinely unresolved reference continuing to fail with the correct reason.
- [~] The symbol database at `tools/symbol-db/symbol-db.sqlite` is rebuilt using the fixed resolver. *(Deferred to human decision: rebuilding destructively wipes ~$24 of vba-symbol-db Sessions 4-7 interpretation columns. End-to-end persistence verified instead via a temp-DB build in `RebuiltSymbolDb_PersistsResolvedCrossModuleReferences`.)*
- [x] A query against the rebuilt database confirms that the call sites in `Form_frmCompanyDetail` for `GetString`, `StringFormat`, and `sCompanyDelete_CompanyTypeChange` now resolve to their respective declarations in `modStrings`. *(Verified end-to-end against the temp-DB build via SQL queries on the persisted [References] table.)*
- [x] `dotnet build` completes without errors.
- [x] `dotnet test` completes with all tests passing.
- [x] A cross-provider session verification confirms the fix, the passing tests, and the corrected database state.

## Context

The `ReferenceResolver` currently traverses the scope chain up to the `ProjectScope` but does not check sibling `ModuleScope`s for public declarations. Consequently, call sites in a `Form` module (e.g., `Form_frmCompanyDetail`) referencing public symbols in a `Standard` module (e.g., `modStrings.GetString`) are incorrectly marked as unresolved. The fix must be implemented in `AccessHarvester.Vba.Resolver.References.ReferenceResolver` to add a cross-module lookup step for public symbols before declaring an identifier unresolved at the project level.

## Next Session

### Forward-looking Recommendation for Session 2

Session 2's task is a C# translation pass for the `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` VBA procedure, using the symbol database fixed in this session. The human operator will choose one of the following in-IDE orchestrator pairings. The *other* pairing will then be assigned Session 3's peer review task to ensure a cross-provider check on the synthesized code.

- **Option A**:
    - **IDE Tool**: Claude Code (VS Code Extension)
    - **Model**: Claude Opus 4.7

- **Option B**:
    - **IDE Tool**: GitHub Copilot Chat (VS Code Extension)
    - **Model**: Sonnet 4.6

## Session 1 Actuals

- **Orchestrator used**: Claude Opus 4.7 @ effort=high (Claude Code) — matched recommendation.
- **Total routed cost**: $0.156 (one analysis call for ai-assignment authoring at $0.0087, one cross-provider session-verification call to gpt-5-4 at $0.1474).
- **Verification verdict**: VERIFIED, 0 issues.
- **Deviations from recommendation**: None on orchestrator choice. One scope deviation noted in the "rebuild symbol-db.sqlite" acceptance criterion: the committed DB was deliberately not overwritten (would lose ~$24 of vba-symbol-db Sessions 4-7 interpretation columns). End-to-end persistence verified through a temp-DB build instead. Whether to commit a rebuilt structural DB is flagged as a deferred decision for the human (also flagged as an Open Question in the spec).
- **Notes for Session 2 calibration**:
    - The fix turned out to live in `AccessHarvester.Vba.Resolver.References.ReferenceResolver`, not the SymbolDb writer. The spec's diagnostic note pointed at the writer; the actual gap was that `ProjectScope` (by deliberate design) owns no declarations, so cross-module lookup had to be added as a Step 1.5 in the resolver's `ResolveIdentifier`.
    - The verifier observed an untested edge — implicit form/report control-name collision with a sibling-standard-module Public symbol — but explicitly judged it not sufficient to reject the session. Translation work in Session 2 may surface this if `cboCompanyTypeID_BeforeUpdate` references controls by bare name; if so, the translator should disclose the assumption and Session 3's reviewer can evaluate.
    - Subsequent sessions should consume the Session-1-fixed resolver as-is. No further resolver changes are anticipated for the probe target.

---

# AI Assignment: vba-symbol-db-path-b-probe Session 2

- **SessionId**: `vba-symbol-db-path-b-probe-s2`
- **Orchestrator**: `Codex / GPT-5.5 @ effort=medium`

## Task

Translate `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` into a
small, testable C# probe project. The session must generate a
translation packet from a Session-1-fixed symbol DB, author the
translation plus its own xUnit tests, record the test run, and capture
the symbol-DB gaps that surfaced.

## Session-Local Direction

- Per human direction for this session only, do **not** use the router
  except for the mandatory cross-provider session verification.
- That mandatory verification must use `Gemini Pro`.

## Acceptance Criteria

- [x] `translation-packet.md` was generated from a temporary, rebuilt
  symbol DB that includes Session 1's cross-module resolver fix without
  overwriting the committed interpreted `tools/symbol-db/symbol-db.sqlite`.
- [x] `tests/AccessHarvester.TranslationProbe.Tests/` was created and
  added to `AccessHarvester.sln`.
- [x] The translated C# probe, faked interfaces, and Session 2-authored
  xUnit tests compile and pass.
- [x] `translation-rationale.md`, `test-run-session-2.md`, and
  `symbol-db-gaps.md` were authored.
- [x] `dotnet build` and `dotnet test` ran green.
- [x] Cross-provider session verification via `gemini-pro` returned
  `VERIFIED`.

## Next Session

### Forward-looking Recommendation for Session 3

Per the spec, Session 3 should be run by the **other** in-IDE
orchestrator so the peer review and additional test authoring are
cross-provider relative to this Session 2 translation pass. No routed
analysis was used for this recommendation because the human explicitly
disabled normal routing for this special session.

---

# AI Assignment: vba-symbol-db-path-b-probe Session 3

- **SessionId**: `vba-symbol-db-path-b-probe-s3`
- **Orchestrator**: `Claude Opus 4.7 @ effort=high (via Claude Code)`

## Task

Independently peer-review the Codex/GPT-5.5 translation from Session 2,
author additional xUnit tests that target gaps in Session 2's coverage,
write the Path A vs. Path B decision document, generate the set's
change-log, and recommend the next session set. Cross-provider
verification at the end.

## Session-Local Direction

- Per human direction for this session only, do **not** use the router
  except for the mandatory cross-provider session verification.
- That mandatory verification must use `Gemini Pro`.
- This is the cross-provider counterpart to Session 2: Codex authored
  the translation; Claude reviews and falsifies.

## Acceptance Criteria

- [ ] `peer-review/translation-review.md` documents an independent
  semantic review of `FrmCompanyDetailTranslationProbe.cs` against the
  original VBA source body, calling out divergences (or confirming
  absence thereof) per the spec.
- [ ] Additional xUnit tests live in
  `tests/AccessHarvester.TranslationProbe.Tests/Session3*Tests.cs` and
  target areas Session 2's tests missed or covered shallowly.
- [ ] Combined test suite (Session 2 + Session 3) runs green; raw output
  saved to `peer-review/test-run-session-3.md`.
- [ ] `decision.md` argues Path A vs. Path B (or hybrid) grounded in
  this probe's evidence with explicit reference to specific tests or
  review findings.
- [ ] `change-log.md` summarizes the set in-conversation (per the
  in-conversation convention; not routed).
- [ ] Next-session-set recommendation is captured in `ai-assignment.md`
  Session 3 actuals.
- [ ] `dotnet build` and `dotnet test` run green.
- [ ] Cross-provider session verification via `gemini-pro` returns
  `VERIFIED`.

## Routing Decisions

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Required reading | direct |
| 2 | Independent semantic review | direct (in-conversation per spec; no router per session-local direction) |
| 3 | Author Session3 xUnit tests | direct (in-conversation) |
| 4 | Run combined test suite | direct (`dotnet test`) |
| 5 | Author decision.md | direct (in-conversation) |
| 6 | Generate change-log.md | direct (in-conversation per spec) |
| 7 | Next-session-set recommendation | direct (no router per session-local direction) |
| 8 | dotnet build && dotnet test | direct |
| 9 | Cross-provider session verification | route(`gemini-pro`, task_type=session-verification) |

## Session 3 Actuals

- **Orchestrator used**: Claude Opus 4.7 @ effort=high (Claude Code) —
  matched recommendation; the spec's "different in-IDE orchestrator
  from Session 2" requirement is satisfied (Codex did Session 2).
- **Total routed cost**: $0.0516 (single Gemini Pro
  session-verification call: 31,029 input tokens / 1,284 output
  tokens).
- **Verification verdict**: VERIFIED, confidence high, 0 issues.
  Verifier summary: "The session was executed flawlessly. The
  cross-provider peer review correctly identified and pinned subtle
  behavioral invariants, and the resulting decision document provides
  a clear, evidence-based strategic direction."
- **Acceptance Criteria (filled at completion):**
    - [x] `peer-review/translation-review.md` documents an
      independent semantic review with three acceptable divergences
      (D1-D3) and four under-tested invariants (U1-U4).
    - [x] Four additional xUnit tests in `Session3RegressionTests.cs`
      target U1-U4. All pass; one (U3) demonstrably catches a
      hypothetical translation defect Session 2's tests miss.
    - [x] Combined test suite green: 9 probe tests + 1600 other
      tests, 0 failed. Output captured in
      `peer-review/test-run-session-3.md`.
    - [x] `decision.md` argues **Hybrid** (Path B for procedures
      with falsifiable behavior; Path A for the rest), with three
      concrete probe instances and six open follow-ups.
    - [x] `change-log.md` summarizes the set in-conversation per the
      in-conversation convention (no `route(summarization)` call).
    - [x] Next-session-set recommendation captured below.
    - [x] `dotnet build` (4 warnings, 0 errors) and `dotnet test`
      (1609 passed, 4 skipped, 0 failed).
    - [x] Cross-provider session verification via `gemini-pro`:
      VERIFIED, high confidence, 0 issues.
- **Deviations from recommendation**: Per the human's special-session
  direction, no routed analysis was used for `ai-assignment.md`
  authoring, the change-log generation, or the next-session-set
  recommendation. The only router use is the mandatory Gemini Pro
  verification call. Session 3's in-conversation work was authored by
  Claude Opus 4.7 directly.
- **Notes for downstream calibration**:
    - The in-IDE-pair model (Codex translates, Claude reviews) worked
      cleanly — no merge or context-handoff friction. Both
      orchestrators read only the on-disk artifacts (translation
      packet, rationale, test-run, prior session deliverables); no
      conversational context was shared between orchestrators.
    - Session 3 found NO retroactive bugs in Session 2's translation
      but did find four under-pinned invariants worth prospective
      tests. The peer-review value is therefore "narrow the
      future-defect surface" rather than "catch a present defect."
      Worth re-validating across more probes before deciding whether
      the peer-review session can be skipped for simple procedures.
    - The `IDaoCounts` interface drops the VBA `DCount` "expression"
      argument. Acceptable for this probe; flagged in
      `decision.md` Open Follow-Ups as a Path-B-at-scale interface
      debt.

---

## Next-Session-Set Recommendation

**Slug:** `path-b-translation-pipeline`
**Driver:** Decision document recommends Hybrid; the next set is the
first concrete step of operationalizing it.

**Repo has no master plan** (`session-set-tree.md` does not exist
at the repo root nor under `docs/`; `docs/planning/project-status.md`
catalogues durable content but does not name a sequenced backlog).
Per the workflow doc Step 8 "Repos without a master plan" branch,
the recommendation is grounded in this set's actuals plus the open
follow-ups in `decision.md`.

**Suggested scope (3-5 sessions):**

1. **Session 1 — `is_path_b_candidate` heuristic + symbol-DB
   surface.** Add a procedure-level classifier (branch-presence ×
   has-event-binding × non-dead) to either the symbol-DB writer or a
   queryable view. Inventory NorthwindDevEdition to confirm the rough
   ~50% Path B target.
2. **Session 2 — Translation packet generator generalized.** Take
   `tools/symbol-db/probe/build_packet.py` from a one-procedure
   shape to a batched-by-module shape so a single in-IDE session can
   translate ~5-10 related procedures.
3. **Sessions 3-N — First batch of Path B translations.** Pick the
   `Form_frmCompanyDetail` event handlers (siblings of the probe
   target) as the first batch. Codex translates one batch; Claude
   reviews + falsifies in the next session. Same model as this
   probe.
4. **Final session — Pipeline retrospective.** Compare actuals to
   `decision.md`'s per-procedure cost and time projections; revise
   the Hybrid boundary if the actuals diverge.

**Prerequisite chain:**

- This set's `change-log.md` (✓, this commit).
- Cross-module resolver fix from this Session 1 (✓, already in main).
- The committed `tools/symbol-db/symbol-db.sqlite` (still has the
  pre-Session-1 unresolved cross-module rows for `modStrings`-bound
  references, but the Session 1 fix code path is in main, so a
  pipeline run would use a Session-1-fixed temp DB the same way
  Session 2 of this set did).

**Cost target:** ~$0.05-$0.10 routed per Path B batch session
(verification only); subscription orchestrator window for the
synthesis. Total set estimate: <$1 routed.

**Why this and not something else:**

- The competing candidate is "complete the
  AccessExporterTemp1 import" — but that work has been progressing
  in parallel sets and does not block the Path B pipeline.
- Another candidate is "full-DB extraction so `IsFormOpen` and the
  Access UI constants resolve" — but that is itself a Path B
  follow-up (the Hybrid recommendation does not require it before
  the first batch of translations; it just requires acknowledging
  the unresolved-symbols seam in `IDialogService`-style fakes).
- The one thing that would justify deferring `path-b-translation-pipeline`
  is if the human wants to revisit the decision verdict first. The
  decision is grounded but is a single-data-point inference; if
  there is appetite for a second probe (e.g., a different procedure
  shape — a query expression, a macro action, a class-module method)
  before committing to the pipeline, that would be a reasonable
  intervening set. Default recommendation absent that signal: proceed
  to the pipeline.

Rationale: The probe paid for itself (~$0.21 routed) by producing a
defensible direction verdict; the cheapest next step is to start
executing that direction with a small first batch and recalibrate
after.

## Session 2 Actuals

- **Orchestrator used**: Codex / GPT-5.5 @ effort=medium.
- **Total routed cost**: $0.0267 (Gemini Pro session verification only).
- **Verification verdict**: VERIFIED, 0 issues.
- **Deviations from recommendation**: Normal routed analysis for
  `ai-assignment.md` authoring and next-session recommendation was
  skipped per the human's special-session instruction. The only router
  use was the mandatory Gemini Pro verification call.
- **Notes for Session 3 calibration**:
    - The translation probe worked best when Access-specific UI calls
      (`DoCmd.OpenForm`, `DoCmd.Close`, `IsFormOpen`) were collapsed
      into one `IDialogService` seam rather than translated literally.
    - The packet needed a temporary rebuilt DB because the committed
      `symbol-db.sqlite` intentionally preserves Session 4-7 AI columns
      and therefore still carries the pre-fix unresolved cross-module
      rows.
    - `IsFormOpen` and the Access constants remain unresolved in the
      symbol DB; Session 3 should review whether the chosen abstractions
      are faithful enough or whether those gaps materially weaken Path B.
