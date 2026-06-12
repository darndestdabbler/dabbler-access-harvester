# Manifest VBA Ingestion And Parser Hardening Spec

> **Purpose:** Make the manifest's parsed VBA layer real for the UI surface of
> every harvested database, and stop legal legacy VBA from silently costing
> consumers whole modules. Today the manifest pipeline feeds raw SaveAsText
> form/report dumps to the VBA parser whole-file, so **all** form/report
> code-behind ships with empty `ParserOutput` — silently (006 review F-3-1,
> the set's one Critical). Alongside it: legal constructs (`Function F$()`,
> `Dim step As String`) crash the AstBuilder into whole-module loss (F-3-2),
> type-suffixed module declarations are unresolvable by their own references
> (F-3-3), and the manifest layer discards the parse diagnostics and
> unresolved reasons the parser stack already produces (F-3-6). This set is
> 006's P1a — the first product-fix set of the review arc, unblocked by 007's
> green integration baseline.
> **Created:** 2026-06-12 (authored by 007 Session 2 per the
> last-session-authors-next-set pattern)
> **Session Set:** `docs/session-sets/008-manifest-vba-ingestion-and-parser-hardening/`
> **Prerequisite:** `007-integration-test-rebase` (complete — green
> `AccessHarvester.Integration` baseline; every session here regenerates or
> re-validates goldens against it)
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 3
prerequisites:
  - slug: 007-integration-test-rebase
    condition: complete
```

> Rationale: no UI surface in this repo; the quality gate is build + tests +
> cross-provider verification. The set changes shipping code (`Pipeline.cs`,
> the VBA grammar/AstBuilder, the manifest projector) and regenerates the
> committed HomeHealthCare golden — `requiresE2E` does not apply (the
> Integration suite and golden-regression tests are the behavioral gate).

---

## Project Overview

Source material: `006-solution-review-and-upstream-feedback-discovery` —
`review-recommendations.md` §3 P1a and the Recommendation blocks of F-3-1
(Critical), F-3-2, F-3-3 (Major), F-3-6 (Minor), with the F-3-9 corpus
additions and the F-3-10 AstBuilder ride-alongs that the same files' edits
naturally absorb (`s3-review-findings.md`). All findings are
**proven-with-probes** in the 006 record; sessions here implement the
recorded recommendations, they do not re-litigate the diagnosis.

What this set delivers, in consumer terms:

1. **Form/report code-behind becomes visible in `ParserOutput`** (F-3-1).
   `EmitManifestArtifacts` (`Pipeline.cs:279-307`) currently reads
   `raw/Forms/*.txt` / `raw/Reports/*.txt` and feeds the entire SaveAsText
   dump — layout DSL and all — to `VbaModuleBodyLoader.LoadAndParse`, which
   returns `mode=Recovering, items=0`, and the discard at `Pipeline.cs:307`
   (`var (ast, _) = …`) throws away the diagnostic that would have flagged
   it. The fix sources form/report VBA from the kb tier's `VbaCode` field
   using the in-repo working reference implementation — the SymbolDb
   ingestion path (`KbVbaSourceLoader.StripVbeHeader`,
   `SourceLoading/KbVbaSourceLoader.cs:66-100`).
2. **Legal legacy VBA stops killing modules** (F-3-2). Grammar: accept
   `TYPED_IDENTIFIER` in function/property-get name position
   (`Vba.g4:1100,1107,1129`) and add the safely-contextual keywords (`STEP`,
   `COMPARE`, `EXPLICIT`, `PRESERVE`, `LIB`, `ALIAS` — **not** `ERROR`,
   which real VBA reserves) to the identifier alternatives. AstBuilder:
   guard module items the way `BuildStatement` guards statements
   (`HasErrorNode` → recovered stub; null name token → recovered stub, not
   NRE). SymbolDbBuilder: per-module catch mirroring Pipeline's
   (`SymbolDbBuilder.cs` Build Pass 2 has none).
3. **Type-suffixed declarations resolve** (F-3-3). Strip the suffix into
   `TypeSuffix` at every AstBuilder declaration site exactly as
   `BuildVariableDeclarator` already does for locals — the unused
   `strippedName` parameter of `ExtractTypeSuffix` (`AstBuilder.cs:1079-1086`)
   shows where to route it.
4. **Diagnostics and unresolved reasons reach the consumer** (F-3-6).
   Surface `ResolutionReason` on unresolved references in ParserOutput, fold
   per-module `ParseResult.Diagnostics` (or at least counts + `ModeUsed`)
   into the manifest `Diagnostics` array, and replace the bare catch in
   `BuildQueryExports` (`Pipeline.cs:717-729`) with a diagnostic-emitting
   one. Schema additions are additive.

**Known, accepted consequence (disclose, don't "fix"):** F-3-4's resolver
wiring is **out of scope** (it is set P3 in the 006 ranking). Once F-3-1
lands, form/report modules will produce parsed items whose cross-module,
control, and `Me` references resolve as `Unresolved` — a wall of honest
`Unresolved` where the unwired capability would have resolved them. That is
the expected intermediate state the 006 review sequenced deliberately
(F-3-4: "they ship together or the second masks the first" — P1a first,
wiring set after). Do not treat the Unresolved volume as a regression, and
do not start wiring `ProjectScope`/registries in this set.

**Non-goals:** F-3-4 / F-2-2 wiring (P3), F-2-1 query-type decode and the
rest of the contract-fidelity batch (P2), encoding correctness (P1b),
R2-1/R2-2 edge derivation (P4), CLI robustness (P5). F-3-5 (ByVal emission)
and F-3-7 (parsed query type) belong to P2 and must not ride along here —
one coordinated golden regeneration per session is churn enough.

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

Per the 2026-06-12 lesson *constraints travel in routed prompts*, these are
restated here in full, not by reference:

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json`
  or any NorthwindDevEdition-derived artifact** — regeneration reintroduces
  the residue sets `003`–`005` removed. All fixtures and goldens derive from
  committed HomeHealthCare artifacts only. NorthwindDevEdition is
  proprietary and local-only.
- **Golden regeneration is HomeHealthCare-only** and follows the established
  regeneration path (`samples/GoldenOutput/HomeHealthCare/`); regenerate
  once per session, after that session's behavioral changes are complete,
  and re-run `CanonicalGoldenRegressionTests` to confirm.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on,
  TRX `<Counters>` as the completion signal; never background a `dotnet`
  invocation; sweep spurious `samples/*.accdb` diffs
  (`git checkout -- samples/*.accdb`) before staging any commit.
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`;
  its re-enable belongs to the proposed sanitize-testhost-wedge
  investigation, not to this set.
- **Do not weaken assertions or tests to force green.** Intent changes
  require explicit disclosure in the verification prompt.

---

## Sessions

### Session 1 of 3: Kb-Tier VBA Ingestion And Golden Regeneration (F-3-1)

**Steps:**

1. Read `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`
   (mandatory pre-session reading; specifically incremental-test-by-project,
   never-background-dotnet, TRX-as-truth, `.accdb`-sweep, and
   constraints-travel-in-routed-prompts), 006's `s3-review-findings.md`
   F-3-1, and this spec.
2. Re-ground F-3-1 against the current tree (the 006 probes are dated
   2026-06-12; confirm `Pipeline.cs` line anchors still hold). Record the
   grounding in the session log.
3. Implement the ingestion fix in `EmitManifestArtifacts`: source
   form/report VBA from the kb tier's `VbaCode` (the `KbVbaSourceLoader`
   pattern — reuse/extract the header-strip rather than duplicating it), or,
   if kb-tier sourcing is structurally unworkable in the manifest path,
   split the raw `.txt` at the `CodeBehindForm` marker (record which option
   was chosen and why; the 006 recommendation prefers the kb-tier source).
   Stop discarding the parse result's diagnostics at the call site — retain
   them for manifest emission (full surfacing of reasons/ModeUsed is
   Session 3; at minimum the F-3-1 fix must not continue the silent
   discard for form/report parses).
4. Hermetic pins: a form-shaped SaveAsText/kb input with a real event
   handler must produce non-empty parsed items through the manifest path
   (the 006 mechanism probe — layout DSL + `CodeBehindForm` + 
   `Private Sub Form_Load()` — is the natural fixture), plus a pin that a
   degraded parse is no longer silent.
5. Hermetic validation: `dotnet build`, then the affected hermetic test
   projects only (sequential, foreground).
6. COM-tier validation on the Access host: regenerate the HomeHealthCare
   golden, re-run `AccessHarvester.Integration` (TRX-as-truth). Confirm in
   the regenerated manifest: `Forms[].ParserOutput`/`Reports[].ParserOutput`
   carry items for code-bearing forms/reports (the 006 golden probe found
   0 of 19 / 0 of 12), and the `Diagnostics` array reflects real parse
   health. Sweep `.accdb` diffs before staging.
7. Cross-provider verification of the session's diff (include
   `Pipeline.cs`, the loader/ingestion seam, new tests, and a summary of
   golden deltas; disclose the expected Unresolved wall per the Project
   Overview).
8. Closeout: `disposition.json`, `close_session`, commit, push, notify.

**Creates:** hermetic ingestion-path tests; possibly a shared header-strip
helper if extraction is the chosen reuse route
**Touches:** `AccessHarvester.Core/Pipeline.cs`, possibly
`AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs` or a new kb-source
seam, `samples/GoldenOutput/HomeHealthCare/**` (regenerated), tests, set
artifacts
**Ends with:** form/report code-behind parses into manifest `ParserOutput`
items on the regenerated golden; degraded parses are no longer silently
discarded at the F-3-1 call site; Integration green per TRX
(quarantined sanitize test excepted); verification VERIFIED.
**Progress keys:** `s1-ingestion-fixed`, `s1-golden-regenerated`,
`s1-verified`

---

### Session 2 of 3: Grammar And AstBuilder Hardening, Corpus Pins (F-3-2, F-3-3, F-3-10 ride-alongs)

**Steps:**

1. Mandatory pre-session reading (as Session 1 step 1), plus 006's F-3-2,
   F-3-3, F-3-10, and the F-3-9 corpus-addition item.
2. Grammar (`Vba.g4`): accept `TYPED_IDENTIFIER` in function and
   property-get name position; add `STEP`, `COMPARE`, `EXPLICIT`,
   `PRESERVE`, `LIB`, `ALIAS` to the identifier alternatives per the
   grammar's existing contextual-keyword pattern. Do **not** admit `ERROR`
   (real VBA reserves it — 006 breadth probe).
3. AstBuilder: (a) `BuildModuleItem` gets the `HasErrorNode` → recovered
   stub guard `BuildStatement` already has; (b) null-check procedure name
   tokens → recovered stub instead of NRE; (c) F-3-10 ride-alongs in the
   same file — replace the whole-block-collapse behavior with the
   recovered-stub tagging the class comment promises where the new guard
   makes that natural, replace the silent enum defaults (unknown option →
   `Explicit`, unknown comparison → `Equal`, unknown on-error →
   `ResumeNext`, unknown exit → `Do`) with recovered stubs, deduplicate
   `BuildPreprocessorStatement`/`BuildPreprocessor`, drop the dead locals
   in `BuildArguments`. Ride-alongs are in-scope only insofar as the F-3-2
   guard work already has the file open — do not let them grow the session;
   defer any that resist with a recorded note.
4. F-3-3: strip the type suffix into `TypeSuffix` at every declaration site
   (module variables, module constants, parameters, and the other suffixed
   declaration sites the 006 evidence enumerates), routing through
   `ExtractTypeSuffix`'s existing-but-unused `strippedName` parameter.
5. SymbolDbBuilder: per-module try/catch in Build Pass 2 mirroring
   `Pipeline.cs`'s per-file catch, so one bad module degrades that module,
   not the whole build.
6. Tests: hermetic pins for every trigger input — `Function SqlStr$(…)`,
   `Property Get Title$()`, `Dim step As String` (and the other five
   contextual keywords), suffixed module var / const / parameter
   resolution through the real parser → scope → resolver chain (006's
   F-3-3 probe shape). Add the F-3-2 trigger inputs as new corpus cases
   under `docs/test-cases/` with `<n>.expected.json`, per the testing
   convention that every parser feature lands with a test case.
7. Validation: `dotnet build`; affected hermetic projects (Grammar, Parser,
   Resolver, Pipeline.Tests, SymbolDb.Tests) sequentially. COM tier only if
   golden-affecting output changed (a `basResetFixture` module that now
   parses will change the golden — expect a regeneration; same COM-run and
   `.accdb`-sweep discipline as Session 1).
8. Cross-provider verification (diff + new corpus cases + golden deltas if
   regenerated).
9. Closeout: `disposition.json`, `close_session`, commit, push, notify.

**Creates:** corpus cases for the F-3-2 trigger inputs
(`docs/test-cases/…` input + `.expected.json` pairs); hermetic pins
**Touches:** `AccessHarvester.Vba.Grammar/Vba.g4` (+ regenerated lexer/
parser), `AccessHarvester.Vba.Parser/Ast/AstBuilder.cs`,
`AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs`, tests,
possibly `samples/GoldenOutput/HomeHealthCare/**`, set artifacts
**Ends with:** all trigger inputs parse to recovered-or-better (no NRE, no
whole-module loss); suffixed declarations resolve; new corpus cases pinned;
hermetic suites green; verification VERIFIED.
**Progress keys:** `s2-grammar-hardened`, `s2-suffix-symmetry`,
`s2-corpus-pinned`, `s2-verified`

---

### Session 3 of 3: Diagnostics Surfacing And Conformance-Contract Decision (F-3-6, F-3-9)

**Steps:**

1. Mandatory pre-session reading, plus 006's F-3-6 and F-3-9.
2. F-3-6 implementation: surface `ResolutionReason` on unresolved
   references in ParserOutput (`ParserOutputProjector.cs` — the closed
   reason set the resolver already produces); fold per-module
   `ParseResult.Diagnostics` (or counts + `ModeUsed`) into the manifest
   `Diagnostics` array; replace the bare catch in `BuildQueryExports`
   with one that emits a diagnostic. Schema changes are additive — bump
   per the schema set's established versioning discipline and update the
   schema docs the consumer reads.
3. F-3-9 decision (decision-time-consensus eligible per
   `docs/ai-led-session-workflow.md` → Decision-time consensus): either
   (a) implement the conformance runner the guidance promises — execute
   the parser/resolver over each `docs/test-cases/` input and diff against
   `<n>.expected.json` with a documented field-mask for not-yet-implemented
   surfaces — or (b) amend `project-guidance.md` + the test-cases README to
   state the actual contract (schema-shape + selected annotations). Route
   the trade-off for consensus before deciding; the README case-list (13,
   not 12) and schema-version sentence are corrected under either option.
4. Hermetic validation, then COM-tier golden regeneration + Integration
   re-run (TRX-as-truth, `.accdb` sweep) — the diagnostics additions change
   the manifest, so a golden regen is expected.
5. **Author the next session set** (last-session-authors-next-set): per the
   006 ranking the natural candidates are `output-encoding-correctness`
   (P1b — 1 session, independent) and/or the
   `output-contract-fidelity-batch` (P2); author against actual post-008
   completion state, carrying the Hard Constraints block verbatim.
6. Cross-provider verification — closeout grade: prose-first lead before
   the file listings, plus the required JSON response block. Include TRX
   evidence, golden-delta summary, the F-3-9 decision record, and the new
   set's spec.
7. Closeout: `disposition.json` **before** the closeout commit,
   `change-log.md`, `next-session-set-recommendation.md` (routed analysis —
   carry the Hard Constraints verbatim in the routed prompt),
   `close_session`, commit, push, notify; Step 9 reorganization review
   post-notify.

**Creates:** the next set's `spec.md` + `session-state.json` stub; set
closeout artifacts (`change-log.md`, `next-session-set-recommendation.md`,
`disposition.json`); possibly a conformance-runner test class (F-3-9
option a)
**Touches:** `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs`,
`AccessHarvester.Core/Pipeline.cs` (`BuildQueryExports`), schema files +
docs, `docs/test-cases/README.md`, possibly `docs/planning/project-guidance.md`
(F-3-9 option b), `samples/GoldenOutput/HomeHealthCare/**`, set artifacts
**Ends with:** unresolved references carry reasons and parse health reaches
the manifest `Diagnostics`; the F-3-9 contract question is decided and
recorded; goldens regenerated; Integration green per TRX; next set authored;
verification VERIFIED (closeout grade).
**Progress keys:** `s3-diagnostics-surfaced`, `s3-conformance-decided`,
`s3-next-set-authored`, `s3-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is P1a only.** If a fix appears to require F-3-4 wiring, F-2-1
  decode work, or schema surgery beyond additive fields, stop at the
  boundary, record the evidence, and leave it to the owning set (P2/P3).
- **One golden regeneration per session, at the end of that session's
  behavioral work.** If mid-session regen churn appears necessary, the
  session is mis-sequenced — stop and re-plan rather than regenerating
  repeatedly.
- **The expected post-F-3-1 Unresolved wall is not a defect.** Do not
  "fix" it by weakening emission, suppressing unresolved entries, or
  prematurely wiring the resolver.
- If the AstBuilder ride-alongs (F-3-10) start driving golden or corpus
  churn beyond what the F-3-2 guard work naturally produces, defer them
  with a recorded note — they are Observation-severity.

## End-Of-Set Deliverables

- Form/report code-behind visible in manifest `ParserOutput` on the
  regenerated HomeHealthCare golden; degraded parses visible in
  `Diagnostics`.
- Grammar/AstBuilder accept the proven-legal trigger inputs (recovered-or-
  better); suffixed declarations resolve; SymbolDb build survives a bad
  module.
- Unresolved references carry the resolver's reason; query-export failures
  emit diagnostics.
- F-3-9 conformance-contract decision recorded and implemented (runner or
  amended guidance), README corrected.
- New corpus cases for every F-3-2 trigger input.
- Next session set authored (spec + not-started v4 state stub).
- Standard set artifacts: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md` per session,
  `sN-issues.json` for findings-bearing rounds, `disposition.json`,
  `change-log.md`, `next-session-set-recommendation.md`.
