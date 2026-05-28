# Change Log — harvester-object-use-identifier

## Session 1: Schema + Analysis Strategy

- Designed usage classification taxonomy: MainDataEntry, LookupReference, Reporting,
  NavigationShell, StandaloneUtility, PossiblyUnused, Unknown.
- Defined six signal collector families: RecordSource, RowSource, NavigationEdge, VbaReference,
  QueryTableReference, FkRelationship, plus QueryType and ObjectNameConvention.
- Defined question-generation templates per uncertainty signal.
- Committed `docs/planning/object-use-identification-strategy.md` and
  `AccessHarvester.Pipeline/UsageIdentification/object-use-schema.json`.
- Cross-provider verified (3 rounds via gemini-pro): VERIFIED.

## Session 2: CLI Analysis Code

- Implemented `AccessHarvester identify-usage --in <root>` CLI subcommand.
- Six signal collectors: RecordSourceCollector, RowSourceCollector, NavigationEdgeCollector,
  VbaReferenceCollector, QueryTableReferenceCollector, RelationshipCollector.
- SignalIndex, UsageClassifier, QuestionGenerator, UsageChecklistWriter, IrLoader.
- Output writers for `object-use.json` and `object-use-checklist.json`.
- 84 unit tests under `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/`.
- Cross-provider verified (3 rounds via gpt-5-4): VERIFIED.

## Session 3: Pilot + Calibration

### Pilot runs
- HomeHealthCare: 74 objects classified.
- NorthwindDevEdition: 167 objects classified (6% Unknown — below the 60% stop-rule threshold).

### Calibration fixes

**Fix A — QueryTypeCode constants corrected**
Previous code used sequential integers 0–6 for DAO query types. Corrected to DAO values:
Select=0, MakeTable=16, Append=32, Update=48, Delete=64, Union=80, CrossTab=112, DataDef=128,
PassThrough=144. All test type codes updated to match.

**Fix B — Inline SQL RecordSource scanning added to SignalIndex**
Queries referenced via inline SQL in form/report RecordSource (e.g.,
`SELECT * FROM qryFoo WHERE ...`) were not detected as references. Added
`ExtractQueryNamesFromSql()` to scan FROM/JOIN tokens against `knownQueryNames`.
`qryClientNeedsNotMet` and `qryClientServicePlanDetail` correctly reclassified
from PossiblyUnused to Reporting.

**Fix C — Form F7b: nav-target with no other signals → Unknown**
A form that IS opened by navigation but has no RecordSource, nav-edges-out, or VBA was
classified PossiblyUnused. Added F7b branch: such forms are Unknown (reachable but
purpose unclassifiable). `frmAbout` corrected from PossiblyUnused to Unknown.

**Fix D — Table T4b: FK-child + used as RecordSource → MainDataEntry**
A table with FK parents AND used as a form's RecordSource was falling through to
StandaloneUtility (T5). Added T4b branch: `usedAsRS && hasFkParents` → MainDataEntry 0.60.
`tblClientBudgets` and `tblClientServicePlan` corrected from StandaloneUtility to MainDataEntry.

### New tests (3)
- `Form_F7b_NavigationTarget_NoOtherSignals_Is_Unknown`
- `Table_T4b_FkParent_UsedAsRecordSource_Is_MainDataEntry`
- `Query_Q3_UsedInReportInlineSql_Is_Reporting`

### Golden fixtures committed
- `samples/GoldenOutput/HomeHealthCare/usage/object-use.json`
- `samples/GoldenOutput/HomeHealthCare/usage/object-use-checklist.json`
- `samples/GoldenOutput/NorthwindDevEdition/usage/object-use.json`
- `samples/GoldenOutput/NorthwindDevEdition/usage/object-use-checklist.json`

### Verification
Cross-provider verification R1 via gemini-pro: **VERIFIED** (no issues).
