# Harvester Manifest-Index & Batch-Readiness — Session Set Change Log

**Sessions:** 4 of 4 (complete)
**Started:** 2026-05-29
**Closed:** 2026-05-29
**Final verdict:** VERIFIED after two rounds of Session 4 cross-provider session-verification (R1 gpt-5-4 ISSUES_FOUND with one Major + two Minor — all three addressed in-session; R2 gpt-5-4 VERIFIED).

This session set closed the six findings the first real downstream
consumer surfaced when it ran `AccessHarvester run` against
`NorthwindDevEdition.accdb` on the same day Phase 4 closed
([2026-05-29](../harvester-cli-distribution/change-log.md), v1.0.0).
The headline problem the consumer named precisely: **the manifest
should be a complete, self-describing navigation index — and that
file must be complete enough that an agent reading it cold can
discover what was harvested without crawling the directory tree.**

The harvester already *had* `manifest.json`; it was silently
incomplete (Sessions 1 / 2) and operationally fragile across many
DBs (Session 3); the per-field-type semantics and many-DB
operations story were undocumented (Session 4). All six findings
landed in four sessions plus one additive schema bump (v1.0 → v1.1)
that preserved backward compatibility for v1.0 manifests in the
field.

---

## Session 1 of 4: Manifest completeness — populate Tables, Relationships, Modules

**Orchestrator:** `claude-code opus-4.7 @ effort=medium`
**Verification:** VERIFIED (single round, gpt-5-4 cross-provider)
**Routed cost:** ≈$0.13

### Closed
- **Finding #1 — Manifest omits Tables, Relationships, Modules.** The
  call site in `Pipeline.cs` constructed `DatabaseManifestAggregator`
  without passing the typed `tables`, `relationships`, or `modules`
  collections, so the manifest's per-category arrays + `Summary.*Count`
  fields all came out empty even though the per-file IR was complete on
  disk. Both committed goldens silently reproduced the bug. Session 1
  fixed the call site, root-caused the Modules omission separately
  (the variable shadowed the correct collection), and regenerated both
  goldens.

### Created / changed
- `AccessHarvester.Core/Pipeline.cs` — call-site fix passing `tables`,
  `relationships`, `modules` to `DatabaseManifestAggregator.Build()`.
- `tests/AccessHarvester.Pipeline.Tests/ManifestCompletenessTests.cs` —
  the durable per-category invariant
  (`Summary.<X>Count == manifest.<X>.Length == count of ir/<Category>/*.ir.json`
  on disk). The test that would have caught the original bug.
- Regenerated `samples/GoldenOutput/HomeHealthCare/manifest.json` and
  `samples/GoldenOutput/NorthwindDevEdition/manifest.json`.

### In-session scope decision
Three pre-existing schema-vs-typed-record drifts surfaced as soon as
the previously-empty arrays got populated (ParserOutput.Items VBA-
procedure shape, RelationshipDefinition Parent/Child rename,
TableIndex.Fields object shape). Per user direction, all three were
deferred to Session 2's planned v1.0 → v1.1 schema bump rather than
expanding Session 1's scope. One affected integration test was
explicitly `Skip`'d with a precise Session-2 pointer; four others were
preserved by reverting `examples/northwind-output.json` to its
empty-arrays smoke shape.

---

## Session 2 of 4: Manifest as a self-describing index — schema v1.0 → v1.1

**Orchestrator:** `claude-code opus-4.7 @ effort=high`
**Verification:** VERIFIED after two rounds (R1 surfaced one Major + one
Minor; both fixed in-session; R2 VERIFIED)
**Routed cost:** ≈$0.18

### Closed
- **Finding #2 — Manifest is not a navigation index.** A binary-only
  consumer that opened the output cold could not discover the
  per-category file paths, the schema version of the file in hand, or
  where the interpretation guide lived. Session 2 added a top-level
  `FileMap` (per-category `{ count, glob }` records pointing at the
  on-disk per-object files), a top-level `Documentation` block with the
  schema version + a public HTTPS URL to `interpretation-guide.md`, and
  a tiny static `READ-ME-FIRST.md` emitted alongside `manifest.json` so
  a human or agent reading the output cold finds the entry point
  without prior knowledge of the schema.
- **Schema-corrective debt** from Session 1 (the three drifts plus a
  fourth — `LateBound` missing from the `Classification.Purity` enum)
  landed as part of the additive v1.0 → v1.1 bump. The
  `LateBound`-enum-omission fix was the actual top-level cause of the
  1099-error cascade Session 1 attributed to the `HarvestedItem` oneOf
  shape; once it was added, `SchemaValidator` returned `Valid` early
  on the canonical golden.

### Created / changed
- `AccessHarvester.Core/DatabaseManifest.cs` — `FileMap` and
  `Documentation` records, plus a single canonical
  `ResolveHarvesterVersion()` helper that strips the SourceLink
  `+<commit-sha>` suffix from the assembly version.
- `harvester-schema.v1.1.json` (new) — additive v1.1 schema embedded
  alongside the existing `v1.0` schema.
- `RunValidate` (CLI) — per-manifest schema dispatch on
  `SchemaVersion`; supports both `v1.0` and `v1.1` documents in the
  same `validate --in <dir>` call; `--json-status` envelope grew
  `SupportedSchemaVersions[]` and `PerManifestSchemaVersion[]`.
- `tests/AccessHarvester.Integration/Cli/Validate*Tests.cs` — two new
  integration tests pinning the dispatch behaviour
  (`Validate_V1_0_Manifest_DispatchesToV1_0_Schema_AndReturnsZero`,
  `Validate_Mixed_V1_0_And_V1_1_Manifests_BothDispatchedAndPass`).
- `samples/GoldenOutput/*/READ-ME-FIRST.md` (new, byte-identical
  across both goldens) — the static human/agent pointer.
- Regenerated both goldens against the v1.1 shape; the determinism
  gate is green on two fresh `Pipeline.Run` calls.

### Design decisions of record
- **Public HTTPS URL for the documentation pointer.** A relative path
  breaks once the output is detached from the source repo (a binary-
  only consumer is the canonical case). The URL points at the default
  branch (`main`) of the GitHub repo.
- **Both READ-ME-FIRST.md and the in-manifest `Documentation` block.**
  Open Decision 1 from the spec resolved as "both" — the manifest is
  the machine entry point; the README is the human pointer that says
  "start at manifest.json."
- **Backward-compatible v1.1.** The schema bump only added fields; the
  `validate` subcommand dispatches per-document to whichever embedded
  schema the manifest's own `SchemaVersion` field declares.

---

## Session 3 of 4: Batch-run ergonomics — `--no-clean`, status provenance, cross-DB index

**Orchestrator:** `claude-code opus-4.7 @ effort=medium`
**Verification:** VERIFIED after two rounds (R1 surfaced one Major + one
Minor; both fixed in-session; R2 VERIFIED with empty issues array)
**Routed cost:** ≈$0.40

### Closed
- **Finding #4 — Re-run leaves stale files.** Default `run` / `extract` /
  `translate` now wipe the target per-tier directory before writing, so
  objects deleted from a re-harvested DB do not leave orphan IR files.
  `--no-clean` opts back into the legacy file-by-file overwrite.
- **Finding #6 — `--json-status` envelope lacks provenance.** The
  envelope now carries `HarvesterVersion`, `StartedAt` (UTC ISO-8601),
  `DurationMs`, and `Clean` for `run` / `extract` / `translate`. The
  non-deterministic fields (`StartedAt`, `DurationMs`) are excluded
  from the determinism gate the same way `manifest.json`'s
  `ExportedAt` is.
- **Cross-DB roll-up index (in-scope per user opt-in).**
  `<out>/AccessMetaData/index.json` is rebuilt from scratch at the end
  of every `Pipeline.Run` by re-scanning sibling
  `<dbName>/manifest.json` files. Entries carry `{ Name, Manifest,
  SchemaVersion }`, sorted by `Name` (Ordinal). Manifests that fail
  to parse are skipped silently. The public
  `Pipeline.EmitRootIndex(outputRoot)` entry point lets a batch driver
  refresh the index without re-running against a live `.accdb`.

### Created / changed
- `AccessHarvester.Cli/Program.cs` — `--no-clean` wiring; `--json-status`
  envelope extension; `--help` text covering both surfaces (including
  the `--types` + default-clean destructive interaction).
- `AccessHarvester.Core/Pipeline.cs` — per-tier clean before
  `Extract` / `Translate` / `Run`; the public `EmitRootIndex` emitter;
  the cross-DB roll-up's `RootIndex` / `RootIndexEntry` shapes.
- `DatabaseManifest.ResolveHarvesterVersion()` is now the single
  canonical version-resolution helper consumed by manifest emission,
  `--json-status`, and `PrintVersion` — three prior duplicates
  collapsed to one in response to a R1 Major finding (the duplicates
  had drifted, producing manifest-vs-envelope mismatch on the same
  run).
- `tests/AccessHarvester.Pipeline.Tests/PipelineCleanTests.cs` — five
  hermetic tests around the clean semantics.

### Design decisions of record
- **Default = clean.** The user-stated invariant ("a re-harvest should
  reflect *this* run's source, not the accumulated history of every
  prior run") wins by default. `--no-clean` is the opt-in for the rare
  case where merge-overwrite is wanted.
- **`--types` + clean = destructive.** The CLI explicitly documents
  the hazard: `run --types Forms` against an existing tree wipes
  `raw/`, `kb/`, `ir/` for that DB wholesale, including previously
  harvested unselected categories. The mitigation is `--no-clean`
  alongside `--types`; the docs say so prominently.

---

## Session 4 of 4: Interpretation docs — field-type semantics + batch operations + closeout

**Orchestrator:** `claude-code opus-4.7 @ effort=high`
**Verification:** VERIFIED after two rounds (R1 surfaced one Major closeout-placeholder
finding plus two Minor doc-accuracy findings — DDL fallback rows omitted the
unsized spellings for `BINARY` / `TEXT` / `VARBINARY` / `CHAR` columns; and the
`dbVariableField` hex value was given as `0x00000001` when the correct value is
`0x00000002` — `0x00000001` is `dbFixedField`. All three fixed in-session and
R2 VERIFIED.)
**Routed cost:** $0.3818 (R1 gpt-5-4 session-verification $0.2738 + R2 gpt-5-4
session-verification $0.1052 + gemini-pro next-set analysis $0.0028).

### Closed
- **Finding #3 — Field-type semantics undocumented.** Extended
  `docs/harvester/interpretation-guide.md` §5.5 with a per-field-type
  table mapping every DAO `dbType*` code (1–23 plus the 101
  Attachment code plus the 102–109 multi-valued family) to its
  `TypeCode` / `TypeName` JSON shape and the DDL spelling
  `TableDdlBuilder` emits. Added prose on Hyperlink vs Memo
  (`Attributes & 0x8000` distinguishes), AutoNumber (`IsAutoIncrement`
  + DDL `COUNTER`), Lookup columns (`DisplayControl` + table
  property-bag `RowSource`), multi-valued fields (metadata only — the
  hidden child table is not expanded), and validation rules /
  defaults. Northwind's `Employees.Attachments` is the worked example.
- **Finding #5 — No batch / COM-lifecycle guidance.** New doc
  [`docs/harvester/batch-operations.md`](../../harvester/batch-operations.md)
  covers the `<out>/AccessMetaData/<dbName>/` layout, sequential-only
  operation, MSACCESS / RCW hygiene across runs, the `--clean` /
  `--no-clean` semantics from Session 3, the cross-DB roll-up index
  from Session 3, and the `--json-status` envelope provenance fields
  with a determinism column. Wired into `install.md`, the
  interpretation guide's §10, and the top-level `docs/README.md`
  under a new "Running the harvester" subsection.

### Created / changed
- `docs/harvester/interpretation-guide.md` — §5.5 extended with
  "Per-field type semantics" subsection (~140 lines of new content
  including the worked example).
- `docs/harvester/batch-operations.md` (new, ~270 lines).
- `docs/harvester/install.md` — "Related documents" entry pointing at
  the new batch-operations doc.
- `docs/README.md` — new "Running the harvester (downstream-AI
  consumer / operator)" subsection grouping `install.md` and
  `batch-operations.md`.

### Closeout artifacts
- `change-log.md` (this file).
- `next-session-set-recommendation.md` (routed `analysis`).
- `disposition.json` (final-session contract; written before the
  closeout commit per the
  [project-guidance Convention](../../planning/project-guidance.md)).
- `session-state.json` flipped to `complete` via
  `python -m ai_router.close_session`.
- ROADMAP row added under "Phase 4 closed — post-Phase-4 extensions"
  alongside `harvester-data-classifier` and `harvester-cli-distribution`.

### Test posture at close
- Hermetic suites (Resolver, SymbolDb, Pipeline, AccessHarvester) all
  pass: 400 + 70 + 689 + 452 = 1611 tests; 2 skipped (synthetic-
  fixture skips unrelated to this set).
- Integration suite: 53/56 pass under the full sequential
  `dotnet test` run. The three failures (`CliExtractTests.Extract_...`,
  `CliRunTests.Run_AgainstHomeHealthCare_...`,
  `CliTranslateTests.Translate_AgainstHHCRaw_...`) hit the
  documented parallel-COM contention pattern (xUnit runs test classes
  in parallel; ACE OLE DB serialises file-level access; the 60 s
  per-test budget expires before contention clears). **All three pass
  cleanly in isolation** (`dotnet test --filter` for the three tests
  completed in 33 s). This is the same root cause Session 3's
  calibration notes flagged for `ClassifyCommandTests`; the victim
  rotates non-deterministically across runs. Mitigation (collection
  serialisation or pre-staged inputs) is the scope of a future
  cleanup set, not this docs-only session.

---

## Set-level outcomes

- **All six findings closed.** #1 (Session 1), #2 (Session 2), #3
  (Session 4), #4 (Session 3), #5 (Session 4), #6 (Session 3).
- **One additive schema bump (v1.0 → v1.1)** ships with backward-
  compatible dispatch so v1.0 manifests in the field stay valid.
- **Both committed goldens** regenerate byte-identically; the
  determinism gate is green on two fresh runs.
- **No deferred schema-corrective debt** carries forward; Session 1's
  three drifts plus the `LateBound`-enum gap were all absorbed into
  v1.1.
- **Total routed cost across the set:** ≈$1.09 (S1 $0.13 + S2 $0.18 +
  S3 $0.40 + S4 $0.38). Mostly session-verification — the
  orchestrator's Opus model matched the architecture / synthesis
  routing targets for most steps, so in-conversation authoring saved
  a routed Opus round.
- **The first real downstream consumer's grievance is fully
  addressed.** A binary-only consumer that opens
  `<out>/AccessMetaData/index.json` cold can now discover every
  harvested DB, traverse its `manifest.json`, follow the schema-
  version-stamped `Documentation.Url` to the interpretation guide,
  read the per-category `FileMap.<Category>.Glob` to find the per-
  object IR files, and run the harvester safely across hundreds of
  DBs in one batch per the new batch-operations guide — with no
  prior knowledge of the repo layout.
