# Harvester Data Classifier — Session Set Change Log

**Sessions:** 4 of 4 (complete)
**Started:** 2026-05-28
**Closed:** 2026-05-28
**Final verdict:** VERIFIED (Session 4 cross-provider, gemini-pro R1, $0.07)

This session set extended the harvester CLI with the data-classification +
sanitization pipeline from `docs/planning/data-classification-plan.md`
Phases 0-3. Phase 4 (SQL Server migration generator) was explicitly deferred
to a future set per the spec.

---

## Session 1 of 4: JSON Contract

**Orchestrator:** claude-opus-4-7 @ effort=medium (matched the recommended
`architecture` task type)
**Verification:** VERIFIED after four rounds (R1+R2+R3 closed substantive
findings; R4 was a contrived edge case adjudicated as accept-dismissal after a
Gemini Pro tiebreaker)
**Routed cost:** ≈$0.69

### Created
- `AccessHarvester.Pipeline/Classification/classification-schema.json` — the
  v1.0 keystone contract (Tables / Columns / TableHumanOverride /
  ColumnHumanOverride / 10-category column taxonomy / 7-strategy
  MaskingStrategy / FK-authority `allOf` rule).
- `samples/classification-fixture.json` — realistic HomeHealthCare-shaped
  sample document.

### Notes
- Schema was authored in-conversation because `architecture` task type routes
  to Opus and the orchestrator was already Opus (per project-guidance
  Convention "When the orchestrator's model matches the routing target").
- Privacy invariant (plan § 4) was implemented two ways: (a)
  `valueFormatPattern` is structurally constrained so raw digits / arbitrary
  literals cannot validate; (b) `notes` fields documented as reviewer-discipline
  zones the scanner never writes.

---

## Session 2 of 4: Scanner CLI

**Orchestrator:** claude-opus-4-7 @ effort=medium (operator override of the
routed Sonnet-at-high recommendation; same Anthropic Max window)
**Verification:** VERIFIED after three rounds (R1 surfaced five issues, R2
four more, R3 VERIFIED)
**Routed cost:** ≈$1.01

### Created
- `AccessHarvester.Pipeline/Classification/ClassificationFinding.cs` — typed
  finding records + `ColumnCategory` / `TableHint` enums + kebab-case wire
  mapping.
- `AccessHarvester.Pipeline/Classification/RawSchema.cs` — schema-only snapshot
  produced by `ISchemaReader`.
- `AccessHarvester.Pipeline/Classification/ISchemaReader.cs` — platform-neutral
  contract + `IValueSampler` interface.
- `AccessHarvester.Pipeline/Classification/HeuristicsEngine.cs` — lookup
  detector + PII name matcher + PII value-pattern regex + system/audit detector
  + PII veto rule.
- `AccessHarvester.Pipeline/Classification/PiiDictionary.cs` — Presidio-seeded
  vocabulary + value-pattern regexes.
- `AccessHarvester.Pipeline/Classification/ValueFormatDetector.cs` — canonical
  format-pattern detector (digits collapse to `#`, letters to `?`, separators
  preserved).
- `AccessHarvester.Pipeline/Classification/ScanConfig.cs` — effective scan
  configuration (system column patterns / PII name patterns / value sample
  size).
- `AccessHarvester.Pipeline/Classification/ClassificationWriter.cs` —
  serializer to the v1.0 schema shape.
- `AccessHarvester.Pipeline/Classification/ClassificationRunner.cs` — wires
  reader + sampler + engine + writer into a single in-memory pipeline.
- `AccessHarvester.Pipeline/Classification/ClassificationDegradation.cs` —
  degradation report shape.
- `AccessHarvester.Core/Com/OleDbDaoSchemaReader.cs` — OLE DB schema rowset
  reader + DAO interop for AutoNumber + combo-box RowSource implicit FKs.
- `AccessHarvester.Core/Com/OleDbValueSampler.cs` — Jet/ACE-compatible value
  sampler (cardinality via subquery; PII regex match; format-pattern detection
  with raw values never stored).
- `AccessHarvester.Cli/Commands/ClassifyCommand.cs` — `classify` subcommand
  with `--in / --out / --exclude-cleanup / --json-status`.
- Unit + integration test files under
  `tests/AccessHarvester.Pipeline.Tests/Classification/` and
  `tests/AccessHarvester.Integration/Cli/ClassifyCommandTests.cs`.

### Touched
- `AccessHarvester.Cli/Program.cs` — added `classify` subcommand dispatch +
  usage text.

---

## Session 3 of 4: HTML Classification Editor

**Orchestrator:** claude-opus-4-7 @ effort=medium (operator override of the
routed Sonnet-at-normal recommendation)
**Verification:** VERIFIED after two rounds (R1 one Minor JS-test finding
fixed mechanically; R2 returned a bare VERIFIED — a prose-first retry was
abandoned per the verifier-failure escalation ladder after the OpenAI 429
rate-limit, leaving the bare verdict standing)
**Routed cost:** ≈$0.35

### Created
- `tools/classification-editor.html` — single-file zero-install editor.
  Loads/saves `classification.json` via FileReader + Blob URL. Sidebar/content
  layout with the full 10-category radio group + 7 masking-strategy selector +
  preserve-autonumber checkbox per column; 3-state lookup-override radio + reviewer
  + notes per table. Sticky reviewer (localStorage-backed) auto-populates new
  edits' `reviewedBy`. Ctrl+S / Ctrl+O shortcuts. Unsaved-changes guard.
  FK-authority `allOf` constraint enforced in the UI (non-FK category radios
  and non-{preserve,remap-id} masking strategies are disabled for FK columns)
  and at save time. `findSuspectNotes` heuristic flags SSN/phone/email patterns
  in reviewer-authored notes textareas.
- `tools/classification-editor-self-test.py` — Python headless test that
  exercises the editor's schema round-trip through edit-like mutations and
  asserts scanner-emitted fields never drift.
- `tools/classification-editor-js-test.mjs` — Node-vm headless test that
  extracts the editor's pure-helper block, evaluates it in a vm context, and
  asserts CATEGORIES + MASKING_STRATEGIES + SCHEMA_VERSION match the schema's
  enum + version const.

### Decisions
- The spec's "open decision — new repo vs in-repo file" was resolved at session
  start in favour of an in-repo `tools/classification-editor.html` per the
  spec's own stop rule ("HTML editor open decision produces disagreement →
  default to in-repo"). The new-repo path was preserved as a future-work option.

---

## Session 4 of 4: Test-Data Generator CLI

**Orchestrator:** claude-opus-4-7 @ effort=high (matched the recommendation;
correctness-critical synthesis on the FK graph builder + lockstep PK/FK remap)
**Verification:** VERIFIED on R1 by gemini-pro (10-concern substantive prose
analysis, $0.07). The configured gpt-5-4 verifier hit a 429 rate limit on the
first attempt; the verifier-failure escalation ladder
(`project-guidance.md` → Conventions) directed the fall-back to gemini-pro,
which succeeded cleanly on the first call.
**Routed cost:** ≈$0.08 (R1 verification gemini-pro $0.074; next-set
recommendation gemini-pro $0.003)

### Output-format decision
The spec required an output-format choice at session start: sanitized `.accdb`
or CSV. The decision was **CSV per table**, documented in
`SanitizeCommand`'s class summary and `ai-assignment.md` Session 4 actuals.
Rationale (also captured in `disposition.json`):

- The spec's own stop rule names `.accdb` lockstep remap via DAO as fragile.
  CSV removes that risk entirely.
- CSV is hermetically testable (no DAO transactions, no RCW lifecycle); the
  unit-test suite exercises every masking handler and the FK consistency
  invariant against an in-memory row source with zero COM.
- CSV serves both downstream uses (test-data + the deferred SQL Server
  migration generator) — SQL Server `BULK INSERT` and Access "link to text"
  both consume it cleanly.

### Created
- `AccessHarvester.Pipeline/Sanitization/MaskingStrategy.cs` — the
  7-value `MaskingStrategy` enum + bidirectional kebab-case wire mapping.
- `AccessHarvester.Pipeline/Sanitization/ClassificationDocument.cs` — typed
  reader-side model (`ClassificationDocument` / `ClassifiedTable` /
  `ClassifiedColumn` / `TableHumanOverride` / `ColumnHumanOverride` /
  `ClassifiedFkTarget`).
- `AccessHarvester.Pipeline/Sanitization/ClassificationReader.cs` — strict
  v1.0 JSON parser with required-field checks; throws
  `InvalidDataException` on a malformed document so the CLI can surface a
  clean usage-grade error.
- `AccessHarvester.Pipeline/Sanitization/TableColumn.cs` — case-insensitive
  `(Table, Column)` key used by the FK graph and remap registry to honour
  Access identifier semantics.
- `AccessHarvester.Pipeline/Sanitization/FkGraph.cs` — graph data model:
  per-PK remap decision + per-FK effective strategy.
- `AccessHarvester.Pipeline/Sanitization/FkGraphBuilder.cs` — two-pass
  builder (Pass 1 decides PK remap; Pass 2 forces FK strategy to
  `preserve` or `remap-id` based on the target's decision). Missing FK
  targets are surfaced via `MissingFkTargets` rather than crashing.
- `AccessHarvester.Pipeline/Sanitization/EffectiveCategoryResolver.cs` —
  pure logic: effective category = `humanOverride.category ?? guessedCategory`;
  effective strategy comes from the FK graph for PK/FK columns and from
  `humanOverride.maskingStrategy ?? DefaultStrategyFor(category)` for
  everything else. Plan § 3.5 defaults inlined as a switch expression.
- `AccessHarvester.Pipeline/Sanitization/RemapRegistry.cs` — deterministic
  old-value → new-value memo keyed by `(referencedTable, referencedColumn)`.
  First-seen values get monotonic IDs starting at 1,000,000. Numeric
  originals are canonicalized via culture-invariant string conversion so
  `int 42` / `long 42` / `"42"` collide as one map entry — the FK
  consistency invariant survives boxed-type differences between PK + FK
  row sources.
- `AccessHarvester.Pipeline/Sanitization/MaskingHandlers.cs` — `IMaskingHandler`
  + the seven handlers (`PreserveHandler`, `MockReplaceHandler`,
  `GeneralizeHandler`, `RedactHandler`, `ScrubFreeTextHandler`,
  `RegenerateHandler`, `RemapIdHandler`). One file because each handler
  is small (3-15 lines of body); a per-strategy folder would not pay for
  the file overhead.
- `AccessHarvester.Pipeline/Sanitization/FormatPatternMocker.cs` — renders
  a mock value from a `valueFormatPattern` deterministically (FNV-1a seed,
  LCG digit stream, `X` literal for any-character placeholders).
- `AccessHarvester.Pipeline/Sanitization/IRowSource.cs` — abstract row
  reader (`tableName, columnNames` → enumerable of column-keyed dicts).
- `AccessHarvester.Pipeline/Sanitization/CsvWriter.cs` — RFC 4180-conformant
  streaming writer (commas / quotes / CR / LF trigger quoting; embedded
  quotes are doubled; `null` is empty field; dates render ISO 8601
  culture-invariantly).
- `AccessHarvester.Pipeline/Sanitization/SanitizationReport.cs` — typed
  report shape (`SanitizationReport` / `TableSanitizationResult` /
  `AppliedStrategy`).
- `AccessHarvester.Pipeline/Sanitization/MaskingEngine.cs` — orchestrator
  + the `ICsvOutput` abstraction + `FileSystemCsvOutput` concrete writer.
  Strategies are resolved once per column then re-used across every row.
- `AccessHarvester.Pipeline/Sanitization/SanitizationRunner.cs` — convenience
  wrapper that reads classification + writes per-table CSVs +
  `sanitization-report.json`.
- `AccessHarvester.Core/Com/OleDbRowSource.cs` — production
  `IRowSource` (OLE DB streaming reader with the same identifier-escape
  rule as `OleDbValueSampler` and the same provider-probe fallback chain
  `ACE.OLEDB.16.0` → `ACE.OLEDB.12.0`).
- `AccessHarvester.Cli/Commands/SanitizeCommand.cs` — `sanitize` subcommand
  with `--classification / --in / --out / --json-status`. Exit codes 0
  success / 1 usage / 2 unexpected / 3 partial success (missing FK
  targets surfaced).
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/TestFixtures.cs` —
  test helpers (`Col` / `Table` / `Doc` builders + `InMemoryRowSource`).
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/FkGraphBuilderTests.cs`
  — 8 tests covering surrogate-PK remap defaults, preserve-autonumber
  override, FK lockstep inheritance, missing-target degradation,
  case-insensitive matching, and explicit human-override on a non-default
  category.
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/EffectiveCategoryResolverTests.cs`
  — 5 tests covering the per-category default strategy table (plan §3.5),
  FK-graph routing, human-override precedence.
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/RemapRegistryTests.cs`
  — 7 tests covering determinism, distinct-original divergence,
  null-original handling, start-value control, cross-type numeric
  collision (the boxed-int-vs-long FK case), independent maps per key,
  size reporting.
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/MaskingHandlerTests.cs`
  — 14 tests covering every handler's happy + edge paths.
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/MaskingEngineTests.cs`
  — 5 tests covering per-table CSV emission, per-column strategy
  application, linked-table skip, empty-table header-only output, and the
  report's stable v1.0 shape.
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/FkConsistencyInvariantTests.cs`
  — 3 tests covering the headline correctness invariant: PK remap
  propagates to FK; self-referencing FK uses the same map; preserved PK
  with FK preserves both.
- `tests/AccessHarvester.Pipeline.Tests/Sanitization/ClassificationReaderTests.cs`
  — 4 tests covering the sample-fixture round-trip, wrong-version
  rejection, missing-required-field rejection, and `humanOverride`
  masking-strategy parsing.
- `tests/AccessHarvester.Integration/Cli/SanitizeCommandTests.cs` — 3
  tests: a `[SkippableFact]` end-to-end against `HomeHealthCare.accdb`
  (synthesized minimal classification targeting one small lookup table,
  avoiding xUnit parallelization races against `ClassifyCommandTests`)
  plus two usage-error checks.

### Touched
- `AccessHarvester.Cli/Program.cs` — added `sanitize` to the bare-file-mode
  exclusion check + dispatcher switch + usage text.
- `AccessHarvester.Pipeline/Classification/ClassificationFinding.cs` —
  added `ColumnCategoryWire.FromWire(string)` for the sanitizer's
  reader-side parsing of kebab-case category enums.
- `docs/session-sets/harvester-data-classifier/session-state.json` —
  bumped to per-session ledger Session 4 in-progress at register-start;
  per-session entry will flip to complete at closeout.
- `docs/session-sets/harvester-data-classifier/ai-assignment.md` —
  Session 4 actuals block filled in (final-session closeout).

### Tests
- `dotnet build AccessHarvester.sln -c Debug` — clean (warnings only, all
  pre-existing CA1416 platform-attribute warnings on the Windows-only COM
  surface).
- `dotnet test tests/AccessHarvester.Pipeline.Tests -c Debug` — **685 / 685
  passed** including the 46 new sanitization unit tests.
- `dotnet test tests/AccessHarvester.Integration -c Debug --filter
  SanitizeCommandTests` — **3 / 3 passed** (1 `[SkippableFact]` E2E against
  `HomeHealthCare.accdb` plus 2 usage-error checks).

---

## Cross-cutting lessons (Step 9 candidates)

See the orchestrator's Step 9 reorganization proposals at the bottom of
this set's `ai-assignment.md` Session 4 actuals block. Two lesson candidates
surfaced this set:

1. **`OleDbRowSource.ReadRows` connection-per-call pattern works for the
   sanitize use case** because the row reader is iterator-driven and each
   table read is short; the connection opens, the reader streams, the
   connection closes when enumeration ends. Worth recording so a future
   `migrate` subcommand that needs cross-table transactions does NOT
   inherit the same pattern.
2. **Avoid chaining COM-using integration tests against the same `.accdb`
   in parallel xUnit fixtures.** The first SanitizeCommandTests draft
   chained `classify` then `sanitize` against `HomeHealthCare.accdb` and
   ran into a 4-10 minute timeout from xUnit parallelizing the new test
   class against the existing `ClassifyCommandTests`. The fix was to use
   a synthesized minimal classification targeting one small table —
   keeping the integration test focused on what it's actually proving
   (CLI wire-up + OLE DB row source), not on classify's behaviour (already
   tested elsewhere). Promotion-trigger: a second xUnit-parallel COM
   contention pattern in this repo.

---

## Closeout artifacts

- [`disposition.json`](disposition.json) — final-session disposition,
  status: complete.
- [`next-session-set-recommendation.md`](next-session-set-recommendation.md)
  — analysis-routed recommendation: `harvester-cli-distribution` with
  `claude-sonnet-4-6` @ effort=medium.
- [`session-state.json`](session-state.json) — closed with all four
  per-session entries marked complete + the set's top-level `status`
  flipped to `complete`.
- Per-session reviews under [`session-reviews/`](session-reviews/) and
  issue logs under [`issue-logs/`](issue-logs/) (Session 4 issue log is
  empty per the VERIFIED verdict).
