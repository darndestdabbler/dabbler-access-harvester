# Change Log — 001-harvester-upstream-feedback-followups

> Purpose: address the remaining upstream consumer (HomeHealthCare) feedback after
> the cleanup-output narrowing landed — classify launcher/menu surfaces explicitly,
> model missing-opener evidence as out-of-band entry uncertainty rather than
> staleness, and surface parameterized-query / parameter-dialog behavior as
> first-class usage signals. Full tier; no UI, so no UAT/E2E. All changes are
> confined to the usage-identification layer (`AccessHarvester.Pipeline/
> UsageIdentification`), its JSON schema, the strategy doc, tests, and the committed
> usage golden artifacts. No COM-extraction or cleanup-pipeline changes.

## Session 1: Launcher Surfaces + Out-Of-Band Entry Uncertainty

- Added the `LauncherSurface` and `StartupEntry` usage signals and the
  `OutOfBandEntryPossible` uncertainty signal to the schema and emitter.
- Form rule **F5b**: an unbound form recognized as a launcher by name
  (`IsLauncherFormName`) or by startup linkage (`IsStartupOpened`) classifies as
  `NavigationShell`; launcher *chrome* is a corroborator only (never classifies on
  its own), so stripped-chrome parameter dialogs (`frmReportParam_*`) stay `Unknown`.
- Helper artifacts tied to the launcher: switchboard/menu macros classify
  `NavigationShell` via nav edges **or** launcher naming; table rule **T5b** classifies
  switchboard config tables (e.g. `tblSwitchboardItems`) as `StandaloneUtility` so they
  no longer read as `PossiblyUnused`/deletable.
- Reframed the missing-opener question: `NoNavigationEntry` retired from emission
  (kept in the enum for backward compatibility); `OutOfBandEntryPossible` explains
  startup / direct object-pane / ribbon / VBA reachability; `UnreferencedObject`
  reworded so a missing harvested reference is **not** presented as staleness evidence.
  `HasHarvestedOpener` counts inbound query-SQL use so in-graph intermediate queries
  are not mislabeled as openerless.
- All signal/summary state is threaded from the actual classifier decision (explicit
  flags / evidence lists), never recomputed from the object name in
  `BuildSignals`/`BuildSummary`.
- New `LauncherSurfaceTests` regression coverage.
- Cross-provider verification (gpt-5-4): 4 rounds. Each round surfaced one Minor of
  the same class (signal/summary text must agree with the fired decision, not be
  re-derived from the name) — closed in turn for form signals (R2), form/macro
  summaries (R3), and table summary/signal (R4). All six findings resolved; verdict
  recorded honestly as ISSUES_FOUND with everything fixed.

## Session 2: Parameterized Queries + Parameter-Dialog Flows

- Widened the usage-side IR: `IrQuery` carries a new `Parameters` list, populated at
  load by `IrLoader.ExtractQueryParameters`, which **reuses** the Access-layer
  `QueryMetadataExtractor.ParseParameters` scan (declared `PARAMETERS` entries +
  `[Forms]!` / `[Reports]!` control references) rather than re-implementing parsing.
- Added the `ParameterizedInput` signal, computed independently of the Q2–Q7 category
  branch — a parameterized query used as a report `RecordSource` (Q3) now surfaces its
  parameter expectation instead of being lumped into generic reporting. Evidence covers
  declared parameters, form/report control references, inline bracketed prompts, and
  ODBC `?` markers.
- Form rule **F8**: an unbound form whose name matches `*Param*`/`*Filter*`/
  `*Criteria*`/`*Dialog*` is recognized as a likely parameter-collection dialog. Per
  the strategy-doc contract it keeps the `Unknown` category (the *question* is the
  deliverable, not a positive category) but is made distinguishable via the fired
  `ParameterizedInput` signal, a parameter-dialog summary, and a specific
  `ParameterDialogFlow` question that suppresses the now-redundant
  `NoRecordSource`/`UnrecognizedVbaPattern` questions. F8 yields to the RecordSource
  branches and the launcher rules first.
- Queries get an additive `ParameterizedQuery` question that deliberately does **not**
  suppress the Session 1 `OutOfBandEntryPossible` question (entry-path and
  parameter-source are different uncertainties).
- New `ParameterDialogTests` regression coverage.
- Cross-provider verification (gpt-5-4): R1 ISSUES_FOUND (2 Minor, test-coverage only —
  missing regressions for the `[Reports]!` control-reference path and the ODBC `?`
  marker path; concerns 1–5 confirmed correct) → fixed test-only → **R2 VERIFIED**.

## Session 3: Pilot, Fresh-Output Verification, And Closeout

- **Pilot.** Ran `identify-usage` fresh against the committed HomeHealthCare IR
  (74 objects) and NorthwindDevEdition IR (167 objects); exit 0 both. The new signals
  fire as designed — HHC: `LauncherSurface`×5, `ParameterizedInput`×7; Northwind:
  `LauncherSurface`×2, `StartupEntry`×1, `ParameterizedInput`×4. `identify-usage` runs
  hermetically on committed IR (no Access COM needed), so the pilot is reproducible
  from the committed fixtures.
- **Output review.** Launcher shells (`frmSwitchboard`/`frmReportMenu`/`frmStartup`)
  read clearly as `NavigationShell` launchers (0.8–0.85); `tblSwitchboardItems` reads
  as a switchboard config table (`StandaloneUtility`), no longer deletion-scented;
  parameter-dialog forms stay hedged `Unknown` @ 0.45 with a single
  `ParameterDialogFlow` question; parameterized queries keep both
  `OutOfBandEntryPossible` and `ParameterizedQuery` (additive). The pilot exposed **no
  local wording/threshold defect**, so no calibration was made (Step 4 is conditional).
- **Checklist casing.** A fresh run emits canonical PascalCase for both fixtures
  (top-level `Review`; item keys `FunctionalArea`/`Subarea`/`ItemLabel`/`Steps`/
  `IsJudgmentItem`/`Passes`/`Feedback`; no camelCase). Per the spec stop rule the
  verification is **recorded and the writer left unchanged** — no casing code change.
- **Golden regeneration.** Regenerated the committed usage goldens for both fixtures to
  bake in the cumulative Session 1 + Session 2 changes (deferred from S1/S2). The diff
  is the 3 new `fired:false` signal-type rows on every object plus the real changes on
  12 HHC / 6 Northwind objects; no objects added or removed. Files LF-normalized per
  `.gitattributes`; the spurious `samples/HomeHealthCare.accdb` test-build timestamp
  churn was reverted and excluded.
- **Docs.** No consumer-doc change: `docs/harvester/reviewing-the-feedback-artifacts.md`
  is intentionally high-level/vocabulary-agnostic and the new questions are
  self-describing; the concept vocabulary already lives in
  `docs/planning/object-use-identification-strategy.md` (updated in S1/S2). Per the
  Lean-by-default principle no edit was warranted.
- **Build/test.** `dotnet build`: 0 errors (pre-existing CA1416/NU1903 warnings only).
  `dotnet test AccessHarvester.Pipeline.Tests` hermetic (excl. the Access-COM
  ComDateMaximaProbe tier): **816 passed / 0 failed / 0 skipped**, including all S1
  `LauncherSurfaceTests` and S2 `ParameterDialogTests` unchanged and green. No
  production code changed this session.
- **Cross-provider verification (gpt-5-4): R1 VERIFIED (empty issues).** A clean
  first-round close on the regeneration + casing stop-rule + pilot judgment.

## Outcome

All three upstream-feedback items are implemented and verified, and the
checklist-casing question is closed based on a current upstream run (canonical
PascalCase) rather than stale output. The committed usage goldens reflect the new
launcher and parameter behavior. The harvester's usage-explanation layer now
positively explains launcher/menu surfaces, models missing-opener evidence as
out-of-band uncertainty instead of staleness, and surfaces parameterized-query and
parameter-dialog flows as first-class signals.

**Next session set:** none — see `next-session-set-recommendation.md`. The harvester
arc is complete and the repo is in a consumer-driven-only posture; new work resumes
only on a concrete downstream consumer need.
