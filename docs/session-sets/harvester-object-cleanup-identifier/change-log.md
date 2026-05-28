# Change Log — harvester-object-cleanup-identifier

## Session 1: Schema + Detection Strategy

- Designed `object-cleanup.json` schema covering three signal families:
  name-pattern (vocabulary, year, year-month, quarter, numeric-duplicate),
  date-signal (90-day threshold against DateTime-column maxima, with DAO
  `LastUpdated` fallback when COM is unavailable), and cross-reference
  absence (NavigationEdge, VbaReference, RecordSource, RowSource,
  QueryTableReference, FkRelationship).
- Defined the confidence-tier rubric (High = name-pattern + date-signal,
  Medium = name-pattern only OR date-signal only, Low = cross-reference
  absence only).
- Defined the mapping to `uat-checklist-editor` format
  (FunctionalArea = object type, Subarea = exact object name,
  ItemLabel = tier-prefixed explanation, Steps with one human-review
  action, `IsJudgmentItem: true`).
- Committed `docs/planning/object-cleanup-strategy.md` (600+ lines)
  and `AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json`.
- Cross-provider verified across 4 rounds via gpt-5-4: VERIFIED with
  a trivial Minor (example arithmetic) fixed post-R4.

## Session 2: CLI Detection Code

- Implemented `AccessHarvester identify-cleanup --in <root>` and the
  `--manifest` variant; added `--harvest-date`, `--threshold-days`,
  `--no-com`, `--json-status` flags.
- Three detectors under `AccessHarvester.Pipeline/Cleanup/`:
  `NamePatternDetector`, `CrossReferenceDetector`,
  `DateMaximaSampler` (interface + DAO fallback).
- COM-backed date sampler at `AccessHarvester.Core/Com/AccessDateMaximaSampler.cs`
  (Pipeline cannot reference Core; that lives behind the
  `IDateMaximaSampler` interface from Pipeline). `ComDateMaximaProbe`
  encapsulates the upfront COM probe + degradation-note logic.
- `CleanupAnalyser` orchestrates per-table date-signal selection
  between the COM primary and DAO fallback, mixing sources per table
  when a table has no DateTime columns or COM declined.
- `CleanupChecklistWriter` emits both `object-cleanup.json`
  (structured findings) and `object-cleanup-checklist.json`
  (uat-checklist-editor-compatible).
- IR loader additions: form/report/control `Properties` dict;
  table `LastUpdated`, `Connect`, `DateTimeColumnNames`.
- 13 new unit + integration tests across the Cleanup namespace.
- Cross-provider verified across 4 rounds via gpt-5-4: R1 found 5
  issues (per-table date fallback, VBA `DoCmd.Open*` parsing,
  event-property scope, matchedSubstring boundary, test coverage);
  R2 found 4 including a regression in `IrLoader.WalkChildren`
  introduced by R1 fixes (generic-properties branch swallowing
  `Begin` macro-action nodes — verifier-caught, fixed with 2
  regression tests); R3 found 2 trivial Minors; R4 VERIFIED.

## Session 3: Pilot + Calibration

### Pilot runs

- HomeHealthCare: 30 findings (all Low / CrossReferenceAbsence) on
  first run. Names use Hungarian convention with no staleness
  vocabulary; DAO `LastUpdated` was within 30 days so the 90-day
  threshold did not fire.
- NorthwindDevEdition: 41 findings on first run — 4 Medium (vocabulary)
  + 37 Low (cross-reference absence).

### Calibration fix — Vocabulary regex `(?i)` scoping

The 4 Medium findings on Northwind were all false positives:
`qryrptEmployeeEmailList`, `qryrptEmployeePhoneList`,
`rptEmployeeEmailList`, `rptEmployeePhoneList` all matched the
substring `tEmp` inside `Employee`. The strategy doc §3.1.1
intent was a CamelCase boundary (lower→upper letter transition)
before and after the matched vocabulary token, but the regex used
a global `(?i)` flag which case-folds the `[a-z]` / `[A-Z]`
character classes in the boundary lookarounds, collapsing the
lower→upper check into a generic letter→letter check. That
turned the `p|t` letter-letter transition inside "Employee" into
a fake boundary.

Fix: scope `(?i)` to the vocabulary alternation only with the
inline form `(?i:Old|Copy|Bak|Archive|Tmp|Temp|Hold|Save|Backup|Snapshot)`.
The vocabulary token still matches case-insensitively (so `OLD`,
`Old`, `old`, `oLd` all match) but the boundary lookarounds now
do real case-sensitive lower→upper checks.

After the fix:
- Northwind drops from 41 → 37 findings (4 FPs eliminated).
- HomeHealthCare unchanged at 30 findings.
- All previously cited positive examples (`tblClients_OLD`,
  `frmBackup`, `qryReports_Archive`, `OldReports`,
  `frmCopyOfClients`) still match.

### Test additions

Six new InlineData rows added to `Vocabulary_Does_Not_Match` —
the 4 Northwind FPs (`qryrptEmployeeEmailList`,
`qryrptEmployeePhoneList`, `rptEmployeeEmailList`,
`rptEmployeePhoneList`) plus two synthetic cases:
`Hollander` (contains `Hold` without a trailing boundary) and
`Tempest` (contains `Temp` without a trailing boundary).
NamePatternDetector test count: 28 → 34, all green.

### Strategy doc update

§3.1.1 updated with the corrected regex, a "Does not match"
list calibrated against NorthwindDevEdition, and a paragraph
explaining the case-folding pitfall in global `(?i)`.

### Threshold / template review

- 90-day date threshold: not exercised on either pilot DB
  (fresh DAO `LastUpdated` on both). Left at default.
- `ItemLabel` description templates: tier-prefixed, object-name-
  quoted, clear. No change.
- Subarea = objectName byte-for-byte invariant verified on
  both pilot outputs.

### Golden fixtures

- `samples/GoldenOutput/HomeHealthCare/cleanup/{object-cleanup.json,object-cleanup-checklist.json}`
- `samples/GoldenOutput/NorthwindDevEdition/cleanup/{object-cleanup.json,object-cleanup-checklist.json}`

Placed under `cleanup/` subdirs to match the spec's "Creates"
paths and the existing `usage/` subdir precedent from
harvester-object-use-identifier.

### Build and test

- `dotnet build` clean (327 warnings, all pre-existing CA1416).
- `dotnet test` all green: 2127 passing across 10 assemblies, 2
  skipped synthetic-fixture cases (unchanged from baseline), 0
  failed. The 3 HomeHealthCare CLI integration test timeouts
  noted in Session 2 actuals did not recur in this session's
  run (45/45 integration).

### Cross-provider verification

R1 (gpt-5-4, $0.157, 127s): VERIFIED with a 7-point substantive
prose analysis confirming the regex fix correctness, test
coverage adequacy, strategy-doc/code consistency, fixture
quality, checklist readability, scope completeness, and the
closeout-ordering rationale. One non-blocking suggestion (add
`Save`/`Saved` hardening test) acknowledged in disposition;
deferred per "lean by default" — the calibration fix has
adequate regression coverage as-is.

### Deviations from spec

- Pilot used `--no-com` (DAO-fallback only) on both DBs. The
  date-signal path requires the live `.accdb` alongside the
  golden output; Northwind's is gitignored and HHC's date
  signal was already verified in Session 2's `[SkippableFact]`
  tests. The calibration goal (name-pattern + cross-reference
  detection) does not depend on the date dimension.
- The CLI continues to emit to `<inputRoot>` directly; the
  `cleanup/` subdir placement is manual at fixture-commit
  time (same convention as `usage/`).
