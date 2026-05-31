# Harvester Report-Baseline Capture — Session Set Change Log

**Sessions:** 3 of 3 (complete)
**Started:** 2026-05-29
**Latest close:** 2026-05-31 (Session 3)
**Final verdict (S1):** VERIFIED after four rounds of cross-provider session-verification (R1/R2/R3 ISSUES_FOUND, all must-fix items addressed in-session; R4 gpt-5-4 VERIFIED, must_fix empty, three minor nice-to-haves filed).
**Final verdict (S2):** VERIFIED on R2 (R1 returned ISSUES_FOUND with two Major items + a coverage gap + minor doc nits; all addressed in-session; R2 gpt-5-4 VERIFIED with empty issues).
**Final verdict (S3):** VERIFIED on R2. R1 routed to gpt-5-4 because `router-config.yaml`'s `session-verification` override is calibrated for Claude orchestrators; for this GitHub Copilot / OpenAI recovery pass, the counting cross-provider closeout review is R2 via forced `gemini-pro`, which returned VERIFIED with empty issues.

This session set was authored as a DRAFT by a downstream consumer
(`Dabbler.NorthwindToBlazor` Set 001 Session 3, 2026-05-29) and
finalized by the maintainer the same day. The headline problem it
closes: report PDF and aggregate-number baselines are the one
remaining manual step in the harvest → migrate pipeline, and "human
opens Access and exports PDFs by hand" does not scale to the stated
target of hundreds of databases. The set adds a harvester
capability that captures both artifacts automatically — PDF via the
existing COM session, aggregates via ACE OLE DB against the report's
`RecordSource`.

The spec's four open maintainer decisions were resolved at
finalization time via cross-provider consensus (Gemini Pro tier 2 +
Opus tier 3, GPT-5-4 verifier on each): standalone `report-baseline`
subcommand; query-result aggregates (not footer-expression
evaluation); hybrid parameter strategy (manifest exposes binding
sources; runtime defaults to unfiltered; required-param reports
escalate via `identify-usage`); default-all-reports with
`--reports name,name` scoping. See [`spec.md`](spec.md) →
*Resolved decisions* for the full rationale.

---

## Session 1 of 3: Aggregates from the report's RecordSource query

**Orchestrator:** `claude-code opus-4.7 @ effort=medium`
**Verification:** VERIFIED on R4 (R1+R2+R3 returned ISSUES_FOUND; every must-fix item addressed in-session before re-routing).
**Routed cost (S1 total):** ≈$1.05
- Spec-finalization consensus (Gemini Pro + Opus + GPT-5-4 ×2 verifiers): $0.218
- `ai-assignment.md` authoring (Gemini Pro analysis): $0.020
- Session-verification R1 → R4 (GPT-5-4 ×4): $0.235 + $0.201 + $0.179 + $0.199 = $0.814

### Closed (S1 scope)

S1 lands the Core layer for the new feature without any CLI wiring
(CLI is S2's job). The end-to-end pipeline — resolve RecordSource →
execute via ACE OLE DB → compute aggregates → write canonical
`aggregates.json` — is exposed as a single `ReportBaselineService`
that S2 can call from CLI code.

The resolved **RecordSourceResultSet** contract: aggregates reflect
the report's `RecordSource` query result set as run through ACE OLE
DB, not the rendered report's footer-control expressions. Provenance
on every emitted document explicitly states this so a downstream
consumer comparing parity does not silently compare a query-result
sum against an evaluated `=Sum([Price]*[Qty])` footer.

### Created

- [`AccessHarvester.Core/ReportBaseline/RecordSourceKind.cs`](../../../AccessHarvester.Core/ReportBaseline/RecordSourceKind.cs)
  — two-way enum: `TableOrQueryName`, `InlineSql`.
- [`AccessHarvester.Core/ReportBaseline/ResolvedRecordSource.cs`](../../../AccessHarvester.Core/ReportBaseline/ResolvedRecordSource.cs)
  — input → SQL-to-execute carrier.
- [`AccessHarvester.Core/ReportBaseline/RecordSourceResolver.cs`](../../../AccessHarvester.Core/ReportBaseline/RecordSourceResolver.cs)
  — classifies the raw RecordSource string. Inline-SQL heuristic
  matches `SELECT | PARAMETERS | TRANSFORM` prefixes (the three forms
  Access accepts as a RecordSource SQL string). Object names are
  bracket-quoted into `SELECT * FROM [name]` after rejecting
  bracket-unsafe / control characters.
- [`AccessHarvester.Core/ReportBaseline/ReportAggregateInput.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportAggregateInput.cs)
  — report identity + RecordSource + group levels + parameter
  provenance dict.
- [`AccessHarvester.Core/ReportBaseline/ReportAggregates.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportAggregates.cs)
  — the emitted document shape: row count, grand totals per numeric
  column, per-group-level buckets (typed-value identity via
  `IsNull` flag), `Skipped` items with explicit `Kind` values
  (`GroupLevelExpression`, `GroupLevelMissingColumn`,
  `NumericOverflow`), and provenance (harvester version + contract
  + contract note + name-keyed parameters-used snapshot).
- [`AccessHarvester.Core/ReportBaseline/ReportAggregateComputer.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportAggregateComputer.cs)
  — pure functional aggregate computation from a `DataTable`. Group
  identity uses a `TypedValueComparer` that normalizes numeric CLR
  types (so `1m` / `1.0m` / `1.00m` / `1.0d` share a bucket); bucket
  keys are emitted in a canonical row-order-invariant form
  (trailing zeros after decimal point stripped). All emitted
  dictionaries are `ImmutableSortedDictionary` (ordinal) for
  byte-stable JSON; `Skipped` is sorted at end of `Compute` by
  `(Kind, Detail)`.
- [`AccessHarvester.Core/ReportBaseline/ReportAggregatesWriter.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportAggregatesWriter.cs)
  — `Serialize` + `WriteToFile` for `aggregates.json`. Deterministic;
  creates intermediate directories.
- [`AccessHarvester.Core/ReportBaseline/IReportQueryExecutor.cs`](../../../AccessHarvester.Core/ReportBaseline/IReportQueryExecutor.cs)
  — the execution seam: `DataTable Execute(string sql, IReadOnlyList<object?>? positionalParameters)`.
  Hermetic tests pass a fake; production uses the OLE DB
  implementation.
- [`AccessHarvester.Core/ReportBaseline/ReportBaselineService.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportBaselineService.cs)
  — end-to-end orchestrator composing resolve → execute → compute →
  write. The single API S2 wires from CLI code.
- [`AccessHarvester.Core/Com/OleDbReportQueryExecutor.cs`](../../../AccessHarvester.Core/Com/OleDbReportQueryExecutor.cs)
  — Windows-only ACE OLE DB implementation of `IReportQueryExecutor`.
  Same provider-probe policy as
  [`OleDbRowSource`](../../../AccessHarvester.Core/Com/OleDbRowSource.cs)
  and [`OleDbValueSampler`](../../../AccessHarvester.Core/Com/OleDbValueSampler.cs).
  Parameters bind **positionally** (the only mode Access OLE DB
  supports for text commands).
- [`tests/AccessHarvester.Tests/ReportBaseline/RecordSourceResolverTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/RecordSourceResolverTests.cs)
  — 16 hermetic tests covering object-name wrap, the three
  inline-SQL prefixes, rejected-identifier safety, and argument
  validation.
- [`tests/AccessHarvester.Tests/ReportBaseline/ReportAggregateComputerTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/ReportAggregateComputerTests.cs)
  — 17 hermetic tests covering empty / all-numeric / no-numeric
  result sets, mixed-CLR-type sums, null-key bucketing (literal
  `(null)` string does NOT merge with DBNull bucket), date-key
  formatting, expression and missing-column skip, numeric overflow,
  typed-value identity (`1.00m` ≡ `1.0m` ≡ `1m`), and
  row-order-invariance for both bucket keys and `Skipped` order.
- [`tests/AccessHarvester.Tests/ReportBaseline/ReportAggregatesWriterTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/ReportAggregatesWriterTests.cs)
  — 6 hermetic tests covering JSON shape, determinism across
  repeated calls, file-write round-trip, parent-directory creation,
  and canonical key-order for both `GrandTotals` and
  `Provenance.ParametersUsed`.
- [`tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineServiceTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineServiceTests.cs)
  — 6 hermetic tests covering end-to-end service composition over a
  fake `IReportQueryExecutor` (object-name SQL shape, inline-SQL
  verbatim + positional-parameter forwarding, `CaptureAndWrite`
  emitting valid `aggregates.json`, constructor argument validation).

### Changed

- [`docs/session-sets/ROADMAP.md`](../ROADMAP.md) — added
  *Currently in-progress* row for this set and a pointer to the
  still-DRAFT sibling [`harvester-consumer-workflow-instructions`](../harvester-consumer-workflow-instructions/spec.md).

### Test results

- `dotnet build AccessHarvester.sln -c Debug` — 0 errors (warning
  baseline unchanged).
- `dotnet test tests/AccessHarvester.Tests/` — **499 passed, 2
  skipped (COM-required, expected), 0 failed.** The 45 new tests
  under `AccessHarvester.Tests.ReportBaseline.*` cover the
  RecordSourceResultSet contract end-to-end.

### Verifier remediations (must-fix items addressed in-session)

R1 (gpt-5-4): ISSUES_FOUND — four items addressed.
1. `RecordSourceResolver` broadened from `SELECT`-only to
   `SELECT | PARAMETERS | TRANSFORM`.
2. `GroupBucket.IsNull` flag added so the literal string `"(null)"`
   cannot merge with a DBNull bucket.
3. `GrandTotals`, per-bucket `Subtotals`, and
   `Provenance.ParametersUsed` are all `ImmutableSortedDictionary`
   (ordinal). `ParametersUsed` is snapshotted at compute time.
4. `OleDbReportQueryExecutor.Execute` switched to positional
   parameter binding (`IReadOnlyList<object?>`); doc clarifies
   Access/OLE DB binds text-command parameters positionally, not by
   name.

R2 (gpt-5-4): ISSUES_FOUND — two items addressed.
1. New `ReportBaselineService` Core-layer orchestrator composes
   resolve → execute → compute → write against an
   `IReportQueryExecutor` interface (so hermetic tests can pass a
   fake; production uses `OleDbReportQueryExecutor`).
2. Group bucket identity switched from formatted-string to
   typed-value via a `TypedValueComparer` that normalizes numeric
   CLR types.
- R2 also recommended reverting the inherited
  `samples/HomeHealthCare.accdb` drift (a COM side-effect from a
  prior harvest run, unrelated to S1). Reverted via
  `git checkout HEAD --`.

R3 (gpt-5-4): ISSUES_FOUND — two items addressed.
1. `GroupBucket.Key` for numeric buckets is now row-order-invariant.
   A new `CanonicalGroupKey` formats numeric values through `decimal`
   and strips trailing zeros after the decimal point, so the
   emitted Key is identical regardless of which row LINQ's
   `GroupBy` picked as the exemplar.
2. `Skipped` is sorted at end of `Compute` by
   `(Kind ordinal, Detail ordinal)` so multiple-overflowing-bucket
   scenarios serialize identically across row-order permutations.

R4 (gpt-5-4): **VERIFIED**, must_fix empty, three minor
nice-to-haves filed for S2/S3.

### Nice-to-haves filed (deferred, not blocking)

- A regression test that mutates the input `ParametersUsed`
  dictionary after `Compute` and asserts the emitted JSON is
  unchanged. The contract is already met (compute-time snapshot
  via `ToImmutableSortedDictionary`); the test is defensive
  coverage. Land in S2 alongside CLI tests.
- Minor XML-doc cleanups (verifier-noted but no concrete drift in
  R4). Land opportunistically in S2.
- Future API refinement: bundle positional + provenance parameters
  in one input shape to make caller-side divergence impossible.
  Defer until S2's CLI wiring proves the current shape ergonomics.

### Notes for Session 2 calibration

- Session 2 wires the `report-baseline` standalone CLI subcommand
  around `ReportBaselineService` and adds the `DoCmd.OutputTo` PDF
  render through the existing COM session. The Core layer is
  feature-complete and contract-stable; S2 work is mostly CLI
  surface, COM render, determinism-gate handling for the PDF
  binary, and a `[SkippableFact]` integration test against a
  committed fixture DB.
- The `OleDbReportQueryExecutor` was wired into the
  `IReportQueryExecutor` interface so S2's CLI code can construct
  it directly per database (one executor per `--db` argument).
- The top-level `baseline-manifest.json` index per the spec
  (`{name, status, paths, skipped_reason?}` per report) is **NOT**
  in S1 scope — it lands in S2 alongside the CLI multi-report
  fan-out.
- Manifest-side parameter analysis (binding source: form control /
  TempVars / query PARAMETERS / OpenArgs) was deferred to a
  follow-up that the prerequisite set already accommodates; S2's
  `report-baseline` defaults to unfiltered execution and surfaces
  parameter-required reports through `identify-usage` per the
  resolved decision.

**Next-session orchestrator recommendation (Session 2):**
`claude opus-4.7 @ effort=high`
Rationale: S2 spans COM PDF render, CLI subcommand wiring, exit-code
contract alignment with `run`, the `baseline-manifest.json` index,
and determinism-gate handling for binary PDFs — multiple coordinated
concerns benefit from frontier-model synthesis.


---

## Session 2 of 3: COM PDF render + CLI subcommand wiring + determinism

**Orchestrator:** `claude-code opus-4.7 @ effort=high` (matches the S1
next-orchestrator recommendation; the four-concern synthesis surface —
CLI wiring + COM PDF render + exit-code contract + determinism —
benefits from frontier-model coherence per the S1 closing rationale).
**Verification:** VERIFIED on R2 (R1 returned ISSUES_FOUND with two
Major items + a both-fail coverage gap + minor doc tightening; every
item addressed in-session; R2 gpt-5-4 VERIFIED with empty issues).
**Routed cost (S2 total):** ~$0.45
- Session-verification R1 (gpt-5-4): $0.320
- Session-verification R2 (gpt-5-4): $0.125

### Closed (S2 scope)

Session 2 lands the `report-baseline` standalone CLI subcommand
end-to-end, composing the S1 `ReportBaselineService` with three new
seams (`IReportSource`, `IReportPdfRenderer`, `ReportBaselineRunner`)
plus Windows-only COM implementations for the seams.

CLI shape (matches the spec verbatim):

```
AccessHarvester report-baseline --db <file.accdb> --out <output-root-dir>
                                [--reports <name,name>] [--params <params.json>]
                                [--no-clean] [--json-status]
```

Exit codes mirror `run`: 0 (all captured/skipped cleanly) / 1 (usage
error) / 2 (unexpected error) / 3 (one or more per-report failures).

Per-report artifacts land at
`<out>/AccessMetaData/<dbName>/reports-baseline/<report>/{output.pdf,aggregates.json}`,
with the top-level `baseline-manifest.json` index at the parent.

### Created

- [`AccessHarvester.Core/ReportBaseline/IReportSource.cs`](../../../AccessHarvester.Core/ReportBaseline/IReportSource.cs)
  - enumerate-all + per-report (RecordSource + group-level keys)
  seam. Hermetic tests pass a fake; production uses
  `AccessReportSpecReader`.
- [`AccessHarvester.Core/ReportBaseline/IReportPdfRenderer.cs`](../../../AccessHarvester.Core/ReportBaseline/IReportPdfRenderer.cs)
  - render seam over `DoCmd.OutputTo`.
- [`AccessHarvester.Core/ReportBaseline/ReportLiveSpec.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportLiveSpec.cs)
  - DTO carrying the report name, RecordSource (nullable for
  unbound), and outermost-first group-level keys.
- [`AccessHarvester.Core/ReportBaseline/BaselineManifest.cs`](../../../AccessHarvester.Core/ReportBaseline/BaselineManifest.cs)
  - top-level index record + per-report entry (`Name`, `Status` in
  {Captured, Skipped, Failed}, ordinal-sorted `Paths` dict,
  `Reason`). No wall-clock fields - participates in the harvester's
  determinism gate alongside `aggregates.json`; the PDF binary is
  excluded from byte-comparison the way `ExportedAt` is, per the
  spec's Session 2 determinism note.
- [`AccessHarvester.Core/ReportBaseline/BaselineManifestWriter.cs`](../../../AccessHarvester.Core/ReportBaseline/BaselineManifestWriter.cs)
  - deterministic JSON serializer + file IO with parent-dir
  creation, matching the S1 `ReportAggregatesWriter` shape.
- [`AccessHarvester.Core/ReportBaseline/ReportBaselineRunner.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportBaselineRunner.cs)
  - end-to-end orchestrator composing `IReportSource` +
  `IReportPdfRenderer` + `ReportBaselineService` over a batch of
  reports. Sorted manifest output (ordinal-ascending by Name);
  per-report dir always recreated each capture so re-runs cannot
  inherit stale per-report artifacts; default `cleanRoot:true`
  wipes the reports-baseline root before capture to drop stale
  dirs for reports no longer being processed; `--no-clean` opts
  out. Failure matrix recorded as: Captured / Skipped(UnboundReport)
  / Failed(ReadSpecFailed) / Failed(RenderFailed) /
  Failed(AggregateFailed) / Failed(both, joined "; "). Positional
  parameter binding comes from an explicitly ordered
  `IReadOnlyList<KeyValuePair<string, object?>>` rather than any
  dictionary enumeration (R1 verifier flagged the dictionary-
  enumeration-order assumption as unsound for a user-visible
  contract - fixed).
- [`AccessHarvester.Core/Com/AccessReportSpecReader.cs`](../../../AccessHarvester.Core/Com/AccessReportSpecReader.cs)
  - Windows-only live-COM implementation: opens each report in
  hidden design view, reads `Reports!Name.RecordSource` +
  `GroupLevel(i).ControlSource` off the live collection, closes
  with `acSaveNo`. Enumerates the database's reports via
  `CurrentProject.AllReports`.
- [`AccessHarvester.Core/Com/AccessReportPdfRenderer.cs`](../../../AccessHarvester.Core/Com/AccessReportPdfRenderer.cs)
  - Windows-only COM wrapper around
  `DoCmd.OutputTo acOutputReport, name, "PDF Format (*.pdf)", path`.
- [`AccessHarvester.Cli/Commands/ReportBaselineCommand.cs`](../../../AccessHarvester.Cli/Commands/ReportBaselineCommand.cs)
  - CLI surface. Parses options, validates the `--reports` allowlist
  against the database's actual reports (returns exit 1 for unknown
  names), parses `--params` JSON into ordered per-report lists with
  duplicate-name detection, wires the COM seams +
  `OleDbReportQueryExecutor`, invokes the runner, prints summary +
  `--json-status` envelope, returns the correct exit code based on
  the manifest's `Failed` count.
- [`tests/AccessHarvester.Tests/ReportBaseline/BaselineManifestWriterTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/BaselineManifestWriterTests.cs)
  - 4 hermetic tests: canonical JSON shape, sorted Paths, byte
  stability, file-write round-trip + parent-dir creation.
- [`tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineRunnerTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineRunnerTests.cs)
  - 11 hermetic tests covering every Status branch (Captured /
  Skipped(UnboundReport) / Failed(RenderFailed) /
  Failed(AggregateFailed) / Failed(ReadSpecFailed) / Failed(both,
  joined reason)), manifest sort-order invariance, parameter
  forwarding in list order, byte-stable output across input
  permutations, default cleanRoot stale-removal, `cleanRoot:false`
  preserves unprocessed reports while still recreating processed
  ones, constructor argument validation.
- [`tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineServiceParametersUsedMutationTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineServiceParametersUsedMutationTests.cs)
  - the deferred-from-S1-R4 mutation regression test (mutating the
  input `ParametersUsed` after `Capture` does not change the
  serialized aggregates).
- [`tests/AccessHarvester.Tests/ReportBaselineCommandIntegrationTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaselineCommandIntegrationTests.cs)
  - `[SkippableFact]` integration test against the committed
  `samples/HomeHealthCare.accdb` fixture. Scoped via `--reports` to
  four `srpt*` sub-reports (two expected `Captured`, two expected
  `Failed/AggregateFailed`) because HomeHealthCare's eight `rpt*`
  top-level reports reference `Forms!` / `Reports!` / `PARAMETERS [X]`
  bindings and `DoCmd.OutputTo` pops a modal parameter prompt for
  those when run standalone - the spec's resolved decision routes
  required-param reports through the existing `identify-usage`
  mechanism in Session 3 ("parameter elicitation"), explicitly NOT
  in S2 scope. The scoped set exercises both Status branches of
  the runner's `CaptureOne` path in ~3 seconds without manual modal
  dismissal. Also defines `HomeHealthCareComCollection` so this
  test serializes against `HomeHealthCareTableCoverageTests` (both
  open the same `.accdb` via Access COM).
- [`tests/AccessHarvester.Integration/Cli/HomeHealthCareComCollection.cs`](../../../tests/AccessHarvester.Integration/Cli/HomeHealthCareComCollection.cs)
  - sibling collection definition for the AccessHarvester.Integration
  project, scoped to the five pre-existing parallel-COM-CLI test
  classes that touch HomeHealthCare.accdb (see Changed below).

### Changed

- [`AccessHarvester.Cli/Program.cs`](../../../AccessHarvester.Cli/Program.cs)
  - added `report-baseline` to the dispatch switch, the bare-file
  mode exclusion list, the `PrintUsage()` text (with the new
  `--no-clean` + parameter-order semantics + manifest-shape
  disclosure), and the exit-code legend.
- [`tests/AccessHarvester.Integration/Cli/CliUsageTextTests.cs`](../../../tests/AccessHarvester.Integration/Cli/CliUsageTextTests.cs)
  - drift gate updated: asserts `report-baseline` is listed in the
  usage and the new
  `Exit codes for extract/translate/run/report-baseline:` heading
  is present.
- [`tests/AccessHarvester.Integration/Cli/{Classify,Sanitize,CliExtract,CliRun,CliTranslate}CommandTests.cs`](../../../tests/AccessHarvester.Integration/Cli/)
  - added `[Collection("HomeHealthCareCom")]` to the five
  pre-existing CLI integration test classes that each spawn the
  CLI against `samples/HomeHealthCare.accdb`. Convention-prescribed
  mitigation per `project-guidance.md` -> Testing Conventions ->
  "Do Not Chain Parallel xUnit Integration Tests That Both Drive
  Access COM Against The Same `.accdb`". Knocked the Integration
  project's failure count from 3 (in the run that surfaced this)
  down to 1; the remaining Integration failure
  (`SanitizeCommandTests.HomeHealthCare_sanitize` timing out at the
  120-second test budget) is **pre-existing latent slowness in the
  sanitize CLI path against HomeHealthCare.accdb**, confirmed by
  reproducing the same timeout when running the test in true
  isolation (not a parallelism artifact); not introduced by S2 and
  not blocking this closeout. Logged as a follow-up for a future
  set.
- [`tests/AccessHarvester.Tests/AccessHarvester.Tests.csproj`](../../../tests/AccessHarvester.Tests/AccessHarvester.Tests.csproj)
  - added `ProjectReference` to `AccessHarvester.Cli` so the
  integration test can drive `ReportBaselineCommand.Run`
  end-to-end in-process.
- [`tests/AccessHarvester.Tests/HomeHealthCareTableCoverageTests.cs`](../../../tests/AccessHarvester.Tests/HomeHealthCareTableCoverageTests.cs)
  - added `[Collection("HomeHealthCareCom")]` so the
  AccessHarvester.Tests assembly's two HomeHealthCare-COM-driving
  test classes serialize against each other.

### Test results

- `dotnet build AccessHarvester.sln -c Debug` - 0 errors.
- `dotnet test tests/AccessHarvester.Tests/` - **517 passed, 2
  skipped (COM-required, expected), 0 failed in 14 s.** Up from the
  S1 baseline of 499 passing (+18: the 15 originally-planned S2
  tests plus the 3 R1-remediation tests).
- ReportBaseline-namespace tests alone (`--filter
  FullyQualifiedName~ReportBaseline`): 65 passed in 3.2 s
  (includes the `[SkippableFact]` integration test against the
  committed fixture).
- Full solution test run (other test projects): all green except
  the pre-existing Integration `SanitizeCommandTests` timeout
  disclosed above.

### Verifier remediations (must-fix items addressed in-session)

**R1** (gpt-5-4): ISSUES_FOUND - two Major items + a coverage gap
+ minor doc nits. All addressed.

1. **Positional-parameter contract** - R1 flagged that the runner
   relied on `Dictionary<string, object?>` enumeration order for
   positional binding to the SQL's `?` placeholders, which is not a
   sound contract boundary (current .NET preserves insertion order
   but `IReadOnlyDictionary` does not promise this, and a future
   refactor could silently drift). Fixed: the parameter type
   changed from
   `IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>?`
   to
   `IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>?`;
   the list's index is the source of truth for positional binding;
   the runner derives a name-keyed dictionary from the list for
   `ParametersUsed` provenance only. CLI `--params` parser emits
   the ordered list per report and rejects duplicate parameter
   names with a clear error.
2. **Stale-output cleanup on rerun** - R1 flagged that
   `Directory.CreateDirectory` is idempotent, so a re-run into the
   same `--out` could leave stale `output.pdf` / `aggregates.json`
   from a prior run whose reports are now `Skipped` or
   `Failed(partial)`. Fixed: `ReportBaselineRunner.Run` gained a
   `cleanRoot` parameter (default `true`) that deletes + recreates
   the `reports-baseline/` root before capture; the per-report
   directory is ALWAYS recreated fresh for each processed report
   (even under `cleanRoot:false`) so current-run captures cannot
   contaminate. CLI exposes `--no-clean` opt-out mirroring
   `extract`/`run`/`translate`. Usage text updated to disclose the
   behavior.
3. **Coverage gap (both-fail)** - added
   `Run_WithBothRenderAndAggregateFailure_RecordsFailedWithJoinedReasonAndEmptyPaths`
   plus the two cleanup tests
   `Run_DefaultClean_RemovesStaleArtifactsFromReportsNoLongerProcessed`
   and
   `Run_NoCleanRoot_PreservesUnprocessedReports_ButRecreatesProcessedReportDirs`.
4. **Minor doc nits** - `BaselineManifestEntry.Reason` XML now
   mentions `ReadSpecFailed`; `PrintUsage` describes the manifest
   fields as `Name / Status / Paths / Reason` matching the actual
   JSON shape (the prior phrasing was
   `status / paths / skipped_reason` which mis-named the actual
   JSON key).

**R2** (gpt-5-4): **VERIFIED**, must_fix empty. Confirmed all R1
items closed; one minor nice-to-have noted for opportunistic future
work (narrow the broad-catch in `AccessReportSpecReader` around
`RecordSource` + `GroupLevel.Count` reads - currently flattens any
COM read failure into "unbound/no groups" rather than surfacing as
a distinct failure).

### Deferred / out-of-scope items disclosed to the verifier

- **Parameter elicitation for required-param reports** is explicit
  Session 3 scope (spec's Resolved Decision #3 - required-param
  reports surface as a clarifying question through the existing
  `identify-usage` mechanism). S2 honours this by NOT suppressing
  `DoCmd.OutputTo`'s modal parameter prompts; the integration test
  scopes around them via `--reports`.
- **AccessReportSpecReader's broad-catch around COM reads** -
  verifier-noted nice-to-have; not touched this round.
- **Sanitize CLI timeout against HomeHealthCare** - pre-existing
  latent slowness, confirmed via isolation; not introduced by S2
  and not in this set's scope.

### Notes for Session 3 calibration

- The end-to-end `report-baseline` pipeline is contract-stable
  through aggregates + PDF render + manifest. S3 work is mostly
  documentation (CLI reference in `install.md`, interpretation
  guide, parameter selection strategy), golden capture for
  `NorthwindDevEdition` (rptInvoice, rptSalesByEmployee,
  rptSalesByProduct) and HomeHealthCare's in-scope reports
  (`srpt*` for the modal-free subset; `rpt*` requires the
  parameter elicitation work below), plus the closeout pieces.
- **Parameter elicitation design (S3 prerequisite for `rpt*`
  goldens):** the maintainer discussion this session validated
  that Access COM can resolve parameter-required reports without
  modifying the `.accdb` - via `TempVars` + hidden-form binding
  shimming, or via in-memory `RecordSource` substitution closed
  with `acSaveNo`. S3's `identify-usage` route should design
  around one of these (the `acSaveNo` literal-substitution
  approach is the universal fallback). See the maintainer
  transcript for the full trade-off discussion.
- **PDF necessity / opt-in design question** - maintainer raised
  whether PDFs are pulling their weight given the spec's
  RecordSourceResultSet contract for aggregates. The argument for
  keeping PDFs: footer-expression values (not in aggregates.json),
  visual layout fidelity, hedge against metadata-extraction gaps,
  human-stakeholder validation artifact, bug-faithful capture of
  the original output. The argument against: cost, modal-prompt
  exposure, non-deterministic binary. Possible S3 spec revisit:
  default to aggregates-only with `--include-pdf` opt-in. Not
  addressed in S2; flagged here for S3 maintainer consideration.

**Next-session orchestrator recommendation (Session 3):**
`gemini-pro @ effort=medium` (matches the S1 ai-assignment.md
recommendation; S3 is mostly documentation drafting + golden
capture + mechanical closeout, which is the cost-effective profile
for gemini-pro). The orchestrator should route docs drafting
through `route(task_type="summarization")` (NOT `documentation`,
per the convention) and handle file mechanics directly.


---

## Session 3 of 3: Recovery, docs alignment, and honest closeout

**Orchestrator:** `github-copilot gpt-5.4` (recovery/closeout pass on a Session 3 run that had previously been started under `claude-code opus-4.8` and left in a bad state).
**Verification:** VERIFIED on R2. R1 (`gpt-5-4`) was same-provider for the current OpenAI orchestrator and is recorded only as an informational first attempt caused by the repo's Claude-calibrated router override; R2 forced `gemini-pro` and is the counting cross-provider closeout review.
**Routed cost (S3 total):** ~$0.169
- Session-verification R1 (`gpt-5-4`, same-provider / informational only): $0.1553
- Session-verification R2 (`gemini-pro`, final cross-provider verdict): $0.0136

### Closed (S3 actual scope)

Session 3 did not end up being the originally-planned "goldens and closeout"
session. It first had to recover a bad prior closeout attempt plus the real
behavioral defect in the shipped `report-baseline` slice: with `--include-pdf`,
the runner could enter the render path before aggregate success was definitively
known.

The recovered implementation keeps the contract honest and narrow:

- aggregate capture remains authoritative and runs first;
- PDF capture is opt-in via `--include-pdf`;
- aggregate failure suppresses rendering for that report instead of partially
  succeeding and obscuring the failure;
- CLI/report-source lifetime handling was tightened for the validated COM / ACE
  OLE DB lock scenario in this slice;
- install/help/interpretation docs were updated to match the shipped behavior;
- the forged Session 3 review artifact from the failed closeout attempt was
  removed and replaced with a real verifier-backed review.

The originally-planned committed `reports-baseline/` golden corpus,
regression-gate wiring, and `READ-ME-FIRST` propagation did **not** ship in
this set. Those items remain explicitly deferred, with maintainer approval, to
[`harvester-report-baseline-golden-corpus`](harvester-report-baseline-golden-corpus/spec.md)
after reproducing the design-view/ACE-OLEDB lock conflict.

### Changed

- [`AccessHarvester.Core/ReportBaseline/ReportBaselineRunner.cs`](../../../AccessHarvester.Core/ReportBaseline/ReportBaselineRunner.cs)
  - enforces the recovered aggregates-first invariant so optional rendering
  never runs before aggregate success is known.
- [`AccessHarvester.Cli/Commands/ReportBaselineCommand.cs`](../../../AccessHarvester.Cli/Commands/ReportBaselineCommand.cs)
  - aligns the CLI surface with the recovered runtime behavior and opt-in PDF
  semantics.
- [`AccessHarvester.Cli/Program.cs`](../../../AccessHarvester.Cli/Program.cs)
  - updates usage/help text to reflect the shipped `report-baseline` contract.
- [`AccessHarvester.Core/ReportBaseline/BaselineManifest.cs`](../../../AccessHarvester.Core/ReportBaseline/BaselineManifest.cs)
  - keeps the manifest shape aligned with the recovered success/failure contract.
- [`tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineRunnerTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineRunnerTests.cs)
  - locks the aggregate-first / render-suppression invariant with hermetic coverage.
- [`tests/AccessHarvester.Tests/ReportBaselineCommandIntegrationTests.cs`](../../../tests/AccessHarvester.Tests/ReportBaselineCommandIntegrationTests.cs)
  - verifies the CLI/user-facing status surface for the recovered behavior.
- [`docs/harvester/install.md`](../../../docs/harvester/install.md)
  - documents `--include-pdf` as opt-in and reflects the final capture semantics.
- [`docs/harvester/interpretation-guide.md`](../../../docs/harvester/interpretation-guide.md)
  - explains how to interpret `Captured` vs `AggregateFailed` outcomes after the recovery.
- [`docs/session-sets/harvester-report-baseline-capture/session-reviews/session-003.md`](session-reviews/session-003.md)
  - canonical Session 3 review file now contains the real verifier output, ending with the valid Gemini Pro round.

### Test results

- `dotnet build AccessHarvester.sln` - success.
- Focused runner tests (`AccessHarvester.Tests.ReportBaseline.ReportBaselineRunnerTests`) - 13 passed, 0 failed.
- Usage/help text test slice (`CliUsageTextTests`) - pass.
- Live `report-baseline` run against `samples/HomeHealthCare.accdb` - 2 `Captured` (`srptClientBudgets`, `srptClientCurrentNeeds`) and 2 `AggregateFailed` (`srptServicePlanEntries`, `srptServiceShortfall`, both surfacing `No value given for one or more required parameters.`).

### Verifier notes

- **R1** (`gpt-5-4`): returned `VERIFIED`, but this did **not** satisfy the
  workflow's cross-provider requirement for the current GitHub Copilot / OpenAI
  orchestrator because the repo's `session-verification` override is tuned for
  Claude sessions.
- **R2** (`gemini-pro` forced via direct query): `VERIFIED`, issues `[]`. This
  is the counting closeout review.

### Deferred / follow-up items

- The design-view lock collision remains the blocker for the committed golden
  corpus and regression gate: `AccessReportSpecReader.ReadSpec` opens reports in
  hidden design view while aggregate capture concurrently uses ACE OLE DB.
- The named follow-up set
  [`harvester-report-baseline-golden-corpus`](harvester-report-baseline-golden-corpus/spec.md)
  owns the lock-free aggregate-capture refactor, the committed `reports-baseline/`
  corpus, the regression gate, and `READ-ME-FIRST` propagation.
