# Harvester Report-Baseline Capture — Session Set Change Log

**Sessions:** 1 of 3 (S1 complete; S2-S3 not started)
**Started:** 2026-05-29
**Latest close:** 2026-05-29 (Session 1)
**Final verdict (S1):** VERIFIED after four rounds of cross-provider session-verification (R1/R2/R3 ISSUES_FOUND, all must-fix items addressed in-session; R4 gpt-5-4 VERIFIED, must_fix empty, three minor nice-to-haves filed).

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
