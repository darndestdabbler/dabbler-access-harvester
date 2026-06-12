# Session 1 Review Findings — Extraction Core, COM Session, And CLI

> **Set:** `006-solution-review-and-upstream-feedback-discovery`
> **Session:** 1 of 4 (2026-06-12)
> **Orchestrator:** Claude Code / Claude Fable 5 (review authored in-conversation
> per the spec's Orchestrator Assignment And Delegation Deviation section)
> **Scope reviewed:** `AccessHarvester.Access` (all extractors + `QueryMetadata`
> family), `AccessHarvester.Core` — extraction, translation, Com layer
> (`AccessComSession.cs` line-level), `Pipeline.cs`, model, abstractions —
> and `AccessHarvester.Cli` (`Program.cs` full, `ClassifyCommand` full, the
> `ParseOptions` family across all seven commands).
> **Validation:** solution build green (0 errors); claims proven via hermetic
> probes recorded below. No COM-driven runs (per spec stop rule).

## Summary verdict

The reviewed surface is in better shape than a 50-set incremental build
usually produces. The COM lifecycle layer is exemplary: `AccessComSession`
implements its documented two-layer teardown contract exactly (graceful
`Quit(2)` with `Marshal.FinalReleaseComObject` in a `finally`, doubled
GC/finalizer drain, zero-guarded exact-PID failsafe via the hermetically
tested `ComProcessReaper`), `OleDbDaoSchemaReader` pairs every RCW acquire
with a `finally` release and filters DAO property-absent HRESULTs narrowly
(pinned by `DaoPropertyAbsentHResultTests`), and the value sampler escapes
identifiers with explicit degradations instead of silent skips.

Two findings are rated **Major** on proven evidence, and one further
finding (F-1-2) is rated Minor with a named escalation condition pending
a Session 4 COM probe. The most consequential finding is an encoding
split: the kb/ tier decodes Access SaveAsText output as UTF-8 while every
other reader in the repo (manifest emission, `parse`) correctly uses
cp1252 — proven end-to-end to corrupt non-ASCII captions to U+FFFD in
kb/ JSON. It is latent on the all-ASCII committed fixtures, which is
exactly why goldens never caught it.

Every finding below carries an explicit **Status** label per the spec's
Evidence-Over-Inference rule: `proven` (a named command/probe/source
check establishes the claim), `partially proven` (the mechanism is proven
from source; a stated premise about impact remains inferred), or
`inferred — not verified`.

## Findings

### F-1-1: kb/ tier decodes SaveAsText raw text as UTF-8, corrupting non-ASCII to U+FFFD
- **Severity:** Major
- **Status:** proven (the two probes recorded under Evidence; source
  pointers verified)
- **Area:** AccessHarvester.Core — `Translation/SaveAsTextTranslator.cs:59,71`
- **Evidence:** `BuildPayload` reads raw `.txt` via `File.ReadAllText(file)`
  and `File.ReadAllLines(file)` — .NET defaults to UTF-8. The repo's own
  premise is that SaveAsText output is Windows-1252: `Pipeline.cs:262-265`
  registers the code-pages provider with the comment "Windows codepage 1252
  (used by Access SaveAsText output)" and reads the **same** files with
  `Encoding.GetEncoding(1252)` (`Pipeline.cs:297`); `Program.cs:581-600`
  (`parse`) implements BOM-detect-then-cp1252 with a rationale comment that
  explicitly condemns UTF-8-first reads ("silently misclasses Windows-1252
  byte sequences"). **Proven by probe (2026-06-12):**
  (a) `[IO.File]::WriteAllBytes` of cp1252 bytes `43 E9 6C` ("Cél") then
  default `ReadAllText` → `C�l`; cp1252 read → `Cél`.
  (b) End-to-end: a raw `Forms/frmEnc.txt` written as cp1252 containing
  `Caption ="Re/Création"` run through the built CLI
  `translate` produced `"Caption": "Re/Cr�ation"` in the kb JSON.
- **Problem:** Any real-world `.accdb` with non-ASCII captions, labels,
  validation text, or VBA string literals (accented names are routine in
  production Access apps) gets silently corrupted in kb/ and therefore ir/
  — the tiers the downstream AI consumer reads. Corruption is one-way
  (U+FFFD destroys the original byte) and invisible on the committed
  ASCII-only fixtures (probed: golden `kb/` contains no non-ASCII byte and
  no U+FFFD). The manifest path decodes correctly, so the same caption can
  differ between `manifest.json` and the ir/ file — a determinism/contract
  violation across output surfaces.
- **Recommendation:** In `SaveAsTextTranslator.BuildPayload`, read with the
  same strategy `RunParse` uses (honour BOM if present, else
  `Encoding.GetEncoding(1252)`); `File.ReadAllText(file, Encoding.GetEncoding(1252))`
  preserves BOM detection and is a two-line change. Add a hermetic
  translator test with a cp1252 fixture (see F-1-8). One mechanical sweep:
  `Encoding.RegisterProvider` must run before translator use from any host
  (the CLI already registers it in `Main`; tests must too).

### F-1-2: SaveAsTextExporter writes raw files to an unsanitized object-name path
- **Severity:** Minor — escalates to Major if Session 4's COM probe
  confirms the inferred premise below (that Access permits `/` or `\` in
  form/report/macro/module names). The recommended fix is identical at
  either severity.
- **Status:** partially proven — the sanitization gap and the divergence
  from sibling exporters are proven from source; the path-traversal /
  outside-root-write impact rests on an Access naming-rule premise that is
  **inferred — not verified** this session (the spec forbids COM runs in
  Sessions 1–3; Session 4's live run must test creating/obtaining an
  object name containing `/` or `\`).
- **Area:** AccessHarvester.Core — `Extraction/SaveAsTextExporter.cs:51`
- **Evidence:** Proven part: `string outFile = Path.Combine(rawCategoryDir,
  name + ".txt");` — no sanitization, while every sibling exporter
  sanitizes (`QueryExporter.cs:49` + `SanitizeFileName` (`:96-101`),
  `TableExporter.cs:43`, `RelationshipExporter.cs:68`). Inferred part:
  Access object names cannot contain `.` `!` `` ` `` `[` `]` but (per
  Access naming rules as recalled, **not verified against a live
  instance**) can contain `/` and `\`.
- **Problem:** The `.accdb` is untrusted input (spec dimension 6). IF the
  inferred premise holds, a form named `..\..\x` or `a/b` makes
  `Path.Combine` resolve outside `rawCategoryDir` — at best the per-object
  export fails (recorded as an `ExportOutcome` failure, exit 3), at worst
  `SaveAsText` plants a file outside the output root (path traversal
  driven by hostile DB content). Independently of the premise, the
  asymmetry against the three sibling exporters is a real latent
  inconsistency in how untrusted names reach the filesystem.
- **Recommendation:** Apply the same `SanitizeFileName` used by the sibling
  exporters (its `Path.GetInvalidFileNameChars()` set covers `/`, `\`, and
  traversal dots cannot survive `_` replacement of separators). Note the
  side effect to design around: for SaveAsText categories the object's true
  name is currently recoverable only from the file name, so sanitization
  loses name fidelity for affected objects — the lean fix is to record the
  original name in the `ExportOutcome` (it already carries `Name`) and
  accept the sanitized file name, mirroring the Queries/Tables behaviour
  where the JSON payload preserves `Name` verbatim.

### F-1-3: A misspelled `--types` value silently produces an empty harvest with exit 0
- **Severity:** Major
- **Status:** proven (the probe recorded under Evidence, run against the
  built CLI)
- **Area:** AccessHarvester.Cli — `Program.cs:262-271` (`BuildPipeline`);
  AccessHarvester.Core — `Pipeline.cs:50-57` (`WithCategories`)
- **Evidence:** `WithCategories` is a pure filter; unknown names simply
  match nothing. **Proven by probe (2026-06-12):** against a staged raw
  dir containing one Forms file, the built CLI ran
  `translate --in raw --out kb --types Formz --json-status` →
  `"Translated to: …"` on stderr, `ExitCode: 0`,
  `"Translate": { "Ok": 0, "Failed": 0, "Categories": [] }`, empty kb/.
  The same `BuildPipeline` feeds `extract` and `run`.
- **Problem:** The CLI's primary consumer is a downstream automation/AI
  orchestrator keying off exit codes (`0 = all objects processed
  successfully` per the usage text). A typo'd or stale category name turns
  a full harvest into a silent no-op that reports success. This is the
  same defect class as the recorded `generate-migration` unknown-switch
  lesson (`docs/planning/lessons-learned.md` → "New CLI Subcommands Should
  Reject Unknown Switches"), one layer down: the switch is known, its
  value is not validated.
- **Recommendation:** In `BuildPipeline`, validate each `--types` entry
  against `CorePipeline.Default()`'s known category set (union of
  exporter/translator/enricher categories) and throw `ArgumentException`
  (existing usage-error exit-1 path) listing the valid values. Also reject
  an all-filtered-out result defensively.

### F-1-4: Manifest comment promises a diagnostic for spec-less parser outputs; none is emitted
- **Severity:** Minor
- **Status:** proven (source-only claim: the cited method body contains no
  diagnostic emission for the parsed-but-no-ir-spec case; comment vs code
  verifiable by inspection of the cited lines)
- **Area:** AccessHarvester.Core — `Pipeline.cs:638-692`
  (`BuildFormOrReportExports`)
- **Evidence:** The method comment states "Forms with no spec are omitted;
  the parser output is not lost — the HarvesterDiagnostic records the skip
  so the human can investigate." The implementation iterates ir/ files
  only; a `parsed` entry whose name has no matching `*.ir.json` (e.g. its
  enrichment failed, so the form exists in raw/ but not ir/) is dropped
  with **no** diagnostic — the only trace is the per-file `EnrichOutcome`
  failure on the CLI summary surface. The only diagnostic emitted here is
  `FORM_SPEC_LOAD_FAILURE` for ir/ files that fail to load.
- **Problem:** Contract dimension — the codebase's explicit-over-silent
  rule. A manifest consumer sees `manifest.Forms` shrink with no
  corresponding `Diagnostics[]` entry; the comment actively misleads the
  next maintainer into believing the case is covered.
- **Recommendation:** Emit the promised warning diagnostic for the set
  difference (`parserOutputs.Keys` minus ir-file names), e.g. code
  `FORM_SPEC_MISSING`; or, if the EnrichOutcome surface is deemed
  sufficient, fix the comment. Emitting the diagnostic is the lean fix
  consistent with the stated contract.

### F-1-5: OLE DB connection strings interpolate the database path unquoted
- **Severity:** Minor
- **Status:** partially proven — the unquoted interpolation is proven from
  the cited source; the failure behaviour on a `;`-bearing path is
  **inferred — not verified** (no probe run; OLE DB connection-string
  grammar treats `;` as the keyword separator, and `;` is a legal NTFS
  filename character)
- **Area:** AccessHarvester.Core — `Com/OleDbDaoSchemaReader.cs:626`,
  `Com/OleDbValueSampler.cs:247`
- **Evidence:** `$"Provider={provider};Data Source={path};Persist Security
  Info=False;"` — `path` is interpolated raw in both files.
- **Problem:** `;` is a legal NTFS filename character; a database path
  containing `;` (or a leading/trailing quote) breaks connection-string
  parsing — at best a confusing provider error attributed to a missing
  ACE provider (the `BuildConnectionString` catch conflates "provider
  missing" with "connection string malformed" and reports "No working
  Microsoft.ACE.OLEDB provider was found"). Operator-supplied path, so
  this is robustness rather than a security boundary.
- **Recommendation:** Build via `OleDbConnectionStringBuilder` (sets and
  quotes `Data Source` correctly), or quote the value
  (`Data Source="{path}";`). Two call sites, mechanical.

### F-1-6: PARAMETERS-clause parsing breaks on `,` or `;` inside bracketed parameter names
- **Severity:** Minor
- **Status:** partially proven — the split-on-`,` / terminate-at-`;`
  mechanics are proven from the cited source; the premise that Access
  permits `,`/`;` inside bracketed parameter names is **inferred — not
  verified** against a live Access instance (Session 4 can confirm)
- **Area:** AccessHarvester.Access — `QueryMetadataExtractor.cs:125-178`
  (`ParseParametersClause`), `:230-232` (`ParametersClauseRegex`)
- **Evidence:** The clause body is split on bare `,`
  (comment claims "Bracketed identifiers in JET don't contain unescaped
  commas") and the clause itself terminates at the first `;`
  (`PARAMETERS\s+(.*?);`). Access parameter names are prompt-style
  bracketed text that **can** contain commas and semicolons (e.g.
  `[Enter start date, please]`) — *inferred from Access bracket-name
  rules (only `]` and a few chars are excluded) — not verified against a
  live Access instance this session.*
- **Problem:** A comma inside a bracketed name splits one declared
  parameter into two garbage entries; a semicolon truncates the clause.
  The shallow-scan limitations documented on
  `QueryMetadata.Parameters` (string literals, comments) do not disclose
  this one. Impact is bounded: the verbatim `Sql` is preserved, and the
  jet-sql parser is the contract-grade re-extraction path.
- **Recommendation:** Bracket-aware tokenization of the clause body
  (track `[`…`]` nesting before splitting on `,`/`;`), or extend the
  documented limitation list to name this case. Either satisfies the
  explicit-over-silent rule; the doc-only route is the leaner change if
  Session 3's parser review confirms the grammar path covers it.

### F-1-7: System-object name filters drift across extraction surfaces
- **Severity:** Minor
- **Status:** proven for the source divergence (cited lines use bare
  `StartsWith(string)` vs explicit `StringComparison` overloads — .NET's
  documented default for the string overload is CurrentCulture, per
  CA1310); the locale scenarios in which the behaviours actually diverge
  are **inferred — not verified** (no probe under a contraction-rule
  culture was run)
- **Area:** AccessHarvester.Core — `Extraction/TableExporter.cs:36`,
  `Extraction/QueryExporter.cs:33` vs `Com/OleDbDaoSchemaReader.cs:176-177,446-447`
- **Evidence:** The DAO/OLE DB schema reader skips `MSys*`/`~*` with
  explicit `StringComparison.OrdinalIgnoreCase`/`Ordinal`; the exporters
  use bare `name.StartsWith("MSys")` / `name.StartsWith("~")` — which in
  .NET is **culture-sensitive and case-sensitive** (`CA1310`). A system
  table surfacing as `MSYS*` (case drift) or a culture with contraction
  rules could pass the exporter filter while the schema reader skips it.
- **Problem:** Cross-cutting consistency (dimension 7): two definitions of
  "system object" for the same database produce tier-dependent object
  sets in rare locales/casings. Practical likelihood is low; the cost of
  alignment is near zero.
- **Recommendation:** Use `StringComparison.OrdinalIgnoreCase` for `MSys`
  and `StringComparison.Ordinal` for `~` in both exporters (matching the
  schema reader), ideally via one shared `IsSystemObjectName` helper.

### F-1-8: No hermetic test pins SaveAsTextTranslator's behaviour
- **Severity:** Minor (test posture)
- **Status:** proven by two greps (2026-06-12): (a) `SaveAsTextTranslator`
  appears in zero test files; (b) every test call path that could exercise
  it indirectly — the `CorePipeline.Default()` / `.Translate(` /
  `.Run(` call sites in tests — lives in `tests/AccessHarvester.Integration/`
  or in `[SkippableFact]` Access-gated classes
  (`SyntheticDbIntegrationTests`, `SyntheticFixtureSnapshotTests`,
  `SyntheticMetadataExtractorIntegrationTests`,
  `HomeHealthCareTableCoverageTests` — attribute checked on
  `SyntheticDbIntegrationTests.cs:25`). No hermetic (always-running) test
  reaches the translator.
- **Area:** tests/AccessHarvester.Tests — translator seam
- **Evidence:** Greps above; `AccessTextParserTests.cs` covers the parser
  only. The translator's own responsibilities — signature-based routing
  (`<?xml` → Macro / `Attribute VB_Name`/`Option ` → Module / block format),
  the `keepRoot` macro flag, and file decoding — are exercised only behind
  Access-gated runs, which skip on hosts without Access and use all-ASCII
  fixtures.
- **Problem:** The seam where F-1-1 lives is precisely the untested one,
  and it is pure file-to-file logic — the cheapest kind to pin
  hermetically (Testing Hierarchy preference 1). The all-ASCII fixtures
  mean the integration goldens can never catch an encoding regression.
- **Recommendation:** Add hermetic tests: one per routing branch plus a
  cp1252-byte fixture asserting the decoded caption (locks in the F-1-1
  fix). No COM required.

## Observations (recorded, below the finding bar)

> Evidence basis: each observation was checked against the full source
> file named in its pointer, read in-session. These are source-inspection
> claims (no probes); the file:line pointers are the verification trail.

- **O-1-1 — `unresolved-items.json` is a permanently empty artifact.**
  `Pipeline.cs:399` hardcodes `Array.Empty<UnresolvedItem>()`; the file is
  always `[]`. Either a reserved slot (then say so in the interpretation
  guide) or lean-rule dead weight. Fold into Session 4's ranking.
- **O-1-2 — database-name casing/extension drift.** `RawSchema.DatabaseName`
  = `Path.GetFileName` (keeps `.accdb`; `OleDbDaoSchemaReader.cs:158`),
  manifest `DatabaseInfo.Name` likewise, but `AccessComSession.DatabaseName`
  strips the extension. Consumers joining classification output to manifest
  data by name must know which form they hold.
- **O-1-3 — `Enrich` iterates `Directory.GetFiles` unordered**
  (`Pipeline.cs:165,176`) — affects only in-memory outcome ordering (CLI
  summary), not file contents; manifest emission paths all sort
  `OrderBy(..., StringComparer.Ordinal)`. No action needed; noted for the
  determinism dimension.
- **O-1-4 — `ParseOptions` is copy-pasted seven times** (`Program.cs` + six
  command classes); only `GenerateMigrationCommand` rejects unknown option
  keys (the recorded lesson). The next CLI change should lift the helper to
  one shared implementation with an allowed-keys parameter — that also
  retrofits unknown-switch rejection everywhere for free. Candidate
  follow-up-set item (Session 4 ranking).
- **O-1-5 — `AccessDateMaximaSampler` (Core/Com) stamps Access local
  wall-clock dates as UTC** (`DateTime.SpecifyKind(..., Utc)`,
  `AccessDateMaximaSampler.cs:47`) **and swallows per-column failures
  silently** (broad `catch → null`, `:76-79`, no degradation record).
  Both are inputs to the cleanup analyser — fold into Session 2's
  treatment of the known zero-date misfire rather than fixing here.
- **O-1-6 — `TableExporter` emits `DateCreated`/`LastUpdated`/`RecordCount`**
  — Access-managed values that can drift run-to-run; goldens cope via
  normalization, but consumers diffing ir/Tables across runs see noise.
  Acceptable (it is what Access wrote); worth one line in the
  interpretation guide.
- **O-1-7 — bare-file-mode dispatch list** (`Program.cs:119`) must be
  manually kept in sync with the subcommand switch (`:125-140`); a missed
  entry makes `AccessHarvester <newcmd>` fall into implicit-parse. A
  static-set-plus-switch-from-same-table refactor would remove the drift
  risk; below the value bar alone.
- **O-1-8 — `TableDdlBuilder.MapType` lacks codes 22/23** (Time/TimeStamp →
  `UNKNOWN(22)`/`UNKNOWN(23)`) although `TableExporter.FieldTypeName` maps
  them — only reachable via ODBC-linked tables. Honest-UNKNOWN output, so
  contract-compliant; alignment is a one-liner whenever the file is next
  touched.

## Positive confirmations (claims checked and found sound)

> Evidence basis: these confirmations come from full in-session reads of
> the named files (`AccessComSession.cs`, `ComProcessReaper.cs`,
> `SystemProcessController.cs`, `OleDbDaoSchemaReader.cs`,
> `OleDbValueSampler.cs` read end-to-end), the culture grep recorded
> below, and the cited hermetic test files. A verifier working from
> excerpts alone cannot re-derive all of them; the file:line pointers
> are the audit trail for spot-checking any single claim.

- **COM lifecycle (dimension 3):** `AccessComSession.cs` (192 lines, read
  line-by-line) matches `docs/access-com-zombie-cleanup.md`: PID captured
  immediately post-activation with zero-guard and re-capture after
  `OpenCurrentDatabase`; constructor-failure path tears down through the
  same `TeardownCom`; `Quit(2)` = `acQuitSaveNone`; RCW released in
  `finally` even when `Quit` throws; doubled GC/finalizer drain; bounded
  wait then exact-PID kill (`ComProcessReaper`, hermetically tested
  including the null-PID degrade path). The process-sticky
  `ActivatedInThisProcess` latch and the CLI hard-exit guard
  (`Program.cs:84-93`) are scoped exactly as documented.
- **DAO property probing:** narrow HRESULT allow-list (3265/3251/3270)
  with `catch … when` filters; everything else surfaces as a
  `SchemaReadError` degradation — explicit-over-silence done right, and
  the HRESULT mapping is hermetically pinned.
- **Identifier escaping for generated SELECTs:** `EscapeIdentifier`
  rejects `]`/NUL/CR/LF with a degradation instead of guessing
  (`OleDbValueSampler.cs:275-286`); sampling SQL embeds only escaped
  identifiers and a fixed integer TOP literal. Privacy invariant (raw
  values never leave the sampler) holds in the code as documented.
- **Culture discipline:** no `DateTime.Now`/bare-`Parse`/format-string
  `ToString` hits in Core (grep 2026-06-12); manifest timestamps use
  `"o"` + `InvariantCulture`; `Convert.*` calls in samplers pass
  `InvariantCulture`. (Sole CLI exception: `IdentifyCleanupCommand.cs:57`
  defaults the harvest date to local `DateTime.Now` — defensible
  date-of-run semantics.)
- **Composite-FK degradation logic** (`OleDbDaoSchemaReader.DetectCompositeFks`)
  correctly keys on `FK_NAME` only, avoiding the false-composite trap its
  own remarks describe; implicit-FK RowSource resolution refuses to invent
  target columns and emits `UnresolvedImplicitFk` degradations instead.
- **Report vs form `DefaultView` enums are correctly separated**
  (`ReportMetadataExtractor.DefaultViewLabels` with the rationale comment) —
  a classic silent-mislabel avoided.

## Not reviewed this session (stop-rule remainder)

Recorded per the spec's "do not silently thin the review" rule; none of
these blocked the session's findings:

- `AccessHarvester.Core/ReportBaseline/*` (~1.5K lines) — has a dedicated
  hermetic test suite (9 test classes); skipped under the session budget.
- `AccessHarvester.Core/Com/` report-path readers: `AccessReportSpecReader`,
  `OleDbReportQueryExecutor`, `OleDbRowSource`, `AccessReportPdfRenderer`.
- `AccessHarvester.Access/TypeLibraryRegistry.cs` (1144 lines, curated
  member tables; has `TypeLibraryRegistryTests`).
- CLI command bodies other than `Program.cs`/`ClassifyCommand` (the
  `ParseOptions` pattern was checked in all seven; `SanitizeCommand`,
  `IdentifyUsageCommand`, `IdentifyCleanupCommand`, `ReportBaselineCommand`,
  `GenerateMigrationCommand` bodies were not line-reviewed).
- Record-only schema files (`FormMetadata`, `ReportMetadata`,
  `TableMetadata`, `RelationshipMetadata`) — skimmed; the `QueryMetadata`
  family was read in full as the representative.

Session 4 should decide whether the remainder folds into Session 2's
budget (ReportBaseline borders the Pipeline scope), a follow-up set, or
is accepted as-is given existing test coverage.

## Known issues cited as context (not re-discovered)

- Integration suite red on clean checkout (7 tests depend on local-only
  `examples/northwind-output.json`) — owned by the standing
  `integration-test-rebase` recommendation.
- `identify-cleanup` zero-date misfire (time-only DateTime → 1899-12-30,
  ~46176d stale) — deferred classifier-fix candidate; O-1-5 above adds two
  adjacent inputs for Session 2's treatment.
