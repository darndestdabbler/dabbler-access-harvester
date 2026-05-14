# Session 1 Audit Findings — structured-form-report-extractor

> **Session:** 1 of 3
> **Orchestrator:** Claude Code / claude-opus-4-7 / effort=high
> **Date:** 2026-05-14
> **Spec:** `docs/session-sets/structured-form-report-extractor/spec.md`
> **Method:** read source code for translator/parser/enrichers; sampled YAML and
> aspirational typed schemas; routed independent analysis via gemini-pro
> (`route(task_type="analysis")`, $0.021).

This document is the Session 1 deliverable for the audit step. It supersedes
the spec's Step 6 instruction to write `session-reviews/session-001.md` — that
path is owned by `log.save_session_review()` (verifier output) and would be
overwritten, per `lessons-learned.md` → "Don't Name A Spec Deliverable
`session-reviews/session-NNN.md` For Non-Verifier Content." The spec has been
patched alongside this commit to point at this file.

---

## 1. Summary verdict

The spec's premise — that the `ir/` output has narrow gaps versus a YAML
baseline and a one-line `RecordSource` promotion plus a "verify GroupLevel and
embedded-macro correlation" sweep will close them — **partially holds but is
materially miscalibrated.** The miscalibration traces back to a row in the
prior spike's coverage table that overstated a difference between YAML and the
current C# `ir/`.

The real opportunity sitting in this repo is **not** a narrow patch to the
`AccessNode`-based `ir/` pipeline. It is to write `FormMetadataExtractor` and
`ReportMetadataExtractor` that produce the already-designed but
not-yet-implemented `FormMetadata` / `ReportMetadata` typed records — the
exact pattern the just-landed `table-extractor-coverage` set established for
tables. The `09-report-grouping-events.expected.json` fixture and the typed
records in `AccessHarvester.Access` are sitting alongside untested integration
paths waiting for those extractors.

This is a Step 9 / spec-refinement question, not a runtime override. Session 2
should not start until the human picks an option.

---

## 2. The actual current `ir/` schema (for Forms and Reports)

The `ir/` JSON for a Form or Report is the serialization of an
`AccessNode` tree produced by `AccessHarvester.Core.Translation.SaveAsTextTranslator`
plus a chain of enrichers in `AccessHarvester.Core.Enrichment.*`.

### Root node

```jsonc
{
  "Type": "Form" | "Report",
  "Properties": {                  // flat string-keyed bag
    "RecordSource": "tblClients",
    "Caption": "Client Profile",
    "DefaultView": "1",            // numeric code, undecoded at this layer
    "Filter": "",
    "OrderBy": "",
    "OnNoData": "[Event Procedure]",
    "OnClose": "[Embedded Macro]",
    "OnCloseEmMacro": "Version =196611\nColumnsShown =0\nBegin\n  Action =\"Close\"\n...",
    // ... ~150-200 other event/property keys, including theme/font/printer fields
    "Width": "11520",              // raw twips (string)
    "GrpKeepTogether": "1",        // report-only grouping props live as flat keys here
    "DateGrouping": "1",
    "PageHeader": "0",
    "PageFooter": "0"
  },
  "Children": [                    // sections, anonymous Begin wrappers, controls
    {
      "Type": "Section",
      "Properties": { "Name": "Detail", "Height": "1440", ... },
      "GeometryIn": { "H": 1.0 },  // added by GeometryEnricher
      "Children": [
        {
          "Type": "TextBox",
          "Properties": { "Name": "txtClientName", "ControlSource": "ClientName", "Left": "120", ... },
          "GeometryIn": { "X": 0.083, "Y": ..., "W": ..., "H": ... },
          "Label": { "source": "attached", "text": "Client Name" }    // added by LabelResolver
        },
        // ... more controls or nested Begin wrappers
      ]
    }
    // ... more sections (FormHeader / FormFooter / GroupLevel0Header etc.)
  ],
  "VbaCode": "Attribute VB_Name = ...\nOption Compare Database\n...",   // code-behind text
  "Procedures": [                  // added by VbaSignatureEnricher
    {
      "Name": "Form_Load",
      "ProcedureType": "Sub",
      "AccessModifier": "Private",
      "Parameters": [],
      "StartLine": 12,
      "InferredFunctionality": null,   // AI fills downstream
      "Notes": null
    }
  ],
  "EventMacros": {                 // added by MacroActionsEnricher on nodes with EmMacro keys
    "OnClose": {
      "Actions": [ { "ActionName": "CloseWindow", "Arguments": { "ObjectType": "Form", "ObjectName": "frmReportParam_Client", "Save": "No" } } ],
      "InferredFunctionality": null
    }
  }
}
```

### Compared to the YAML for the same object (rptClientProfile.yaml)

```yaml
Database: "..."
ObjectType: "Report"
Name: "rptClientProfile"
Properties:
  RecordSource: "tblClients"     # <-- in Properties, NOT promoted
  Caption: "Client Profile"
  DefaultView: "1"
  OnClose: "[Embedded Macro]"
  OnCloseEmMacro: "Version =196611\n..."   # AXL XML inline as raw string
  GrpKeepTogether: "1"           # grouping as flat keys, no GroupLevels structure
  Width: "11520"
  # ... ~200 other keys, lots of theme/print noise
Controls:                        # FLAT array — no section hierarchy
  - Name: "lblClientProfileReport"
    ControlType: "100"           # numeric code, not name
    Properties:
      Caption: "Client Profile Report"
      Top: "60"
      Left: "0"
      # ...
```

### Where the YAML and the current C# `ir/` are actually different

| Aspect | C# `ir/` (current) | YAML |
|---|---|---|
| Section hierarchy in Children | ✓ present (Section → controls) | ✗ flat Controls list, no section grouping |
| Embedded macro AXL parsed to structured actions | ✓ `EventMacros` block from `MacroActionsEnricher` | ✗ AXL inline as raw multi-line string |
| Label resolution (attached/proximity/from-name/from-binding) | ✓ `Label: { source, text }` from `LabelResolver` | ✗ not provided per control |
| VBA procedure signatures | ✓ `Procedures` array from `VbaSignatureEnricher` | ✗ requires reading separate `Modules/Form_*.yaml` |
| Inch-converted geometry | ✓ `GeometryIn` from `GeometryEnricher` | ✗ raw twips only |
| Control Type as string | ✓ `Type: "TextBox"` | ✗ numeric code `ControlType: "109"` |
| RecordSource as first-class field | ✗ in Properties | ✗ in Properties (the spike was wrong about YAML promoting it) |
| Structured GroupLevels array | ✗ grouping is flat keys in Properties | ✗ same as C# (flat keys) |

**The C# `ir/` is materially RICHER than the YAML on almost every row.** The
spike's strategy doc framed Forms/Reports as a YAML-superior case — that
framing is inaccurate for the current pipeline state.

---

## 3. Gap-by-gap audit against the spec's targeted fixes

| Spec target | YAML | Current C# `ir/` | Aspirational `FormMetadata`/`ReportMetadata` | Verdict |
|---|---|---|---|---|
| RecordSource promotion to first-class field | ✗ in Properties | ✗ in Properties | ✓ first-class field | Real gap exists, but **vs. the typed schema**, not vs. YAML. |
| Structured `GroupLevels` array | ✗ flat props | ✗ flat props | ✓ `IReadOnlyList<GroupLevel>` with Level/GroupOn/SortOrder/HasHeader/HasFooter/KeepTogether | Real gap vs. typed schema. Both YAML and current `ir/` lack it. |
| Embedded macro correlation by event name | ✗ inline AXL | ✓ already done by `MacroActionsEnricher` (`EventMacros: { OnX: { Actions:[…] } }`) | matches current `ir/` | **No gap — already closed.** |
| Label resolution on controls | ✗ not per control | ✓ `Label: { source, text }` from `LabelResolver` | matches current `ir/` shape (typed `LabelInfo`) | **No gap — already closed.** |
| Geometry in inches | ✗ raw twips | ✓ `GeometryIn` from `GeometryEnricher` | ✓ `GeometryInches` typed | **No gap — already closed.** |
| VBA procedure signatures | (in a separate Modules file) | ✓ `Procedures` array from `VbaSignatureEnricher` | typed schema notes it lives on `ir/`, not re-modeled | **No gap — already closed.** |
| `DefaultView` decoded label | ✗ numeric code | ✗ numeric code in Properties (no decoded sibling) | ✓ decoded label + raw code retained | Real gap vs. typed schema. |
| `Filter`/`OrderBy` promotion | ✗ in Properties | ✗ in Properties | ✓ first-class fields | Real gap vs. typed schema. |
| Property bag minus promoted keys | ✗ everything in Properties | ✗ everything in Properties (after promotion of nothing) | ✓ promoted keys removed from Properties | Implementation detail of the typed-schema lift; comes for free with the extractor. |
| `NonDefaultProperties` array | ✗ | ✓ via `PropertyDecoder` | ✓ typed | **No gap — already closed.** |
| `PageSetup` decoded from PrtMip/PrtDevMode | ✗ raw binary blobs | ✗ raw binary in Properties | ✓ typed `PageSetup { Orientation, PaperSize, Margins }` (decoded best-effort) | Real gap vs. typed schema. Not in spec. |

**Summary of confirmed gaps:** RecordSource / Filter / OrderBy promotion;
structured `GroupLevels`; decoded `DefaultView` label; (optionally) `PageSetup`
decoding from binary blobs.

**Summary of "gaps" the spec asks to verify that are already closed:** embedded
macro correlation; label resolution; geometry; VBA signatures; `NonDefaultProperties`.

---

## 4. The strategic question (for human adjudication)

The spec proposes **Option (A): the narrow fix** — promote `RecordSource` in
`SaveAsTextTranslator.BuildPayload` (or a new micro-enricher) and verify the
"already-closed" items. This:
- Touches a single key in a single place.
- Leaves the schema mismatched both ways: the `ir/` JSON would now have one
  promoted field with everything else in Properties; the typed
  `FormMetadata`/`ReportMetadata` records would still have no extractor and
  remain orphan type definitions.
- Diverges further from the table-extractor-coverage pattern just landed in
  this repo (May 14, two commits ago).

The evidence points to **Option (B): apply the table-extractor-coverage pattern.**
- Write `AccessHarvester.Access/FormMetadataExtractor.cs` and `ReportMetadataExtractor.cs`
  that read `ir/Forms/<name>.json` / `ir/Reports/<name>.json` and produce typed
  `FormMetadata` / `ReportMetadata` records.
- The extractor does the promotion (RecordSource, Filter, OrderBy lifted out of
  Properties; DefaultView decoded; GroupLevels constructed from the report's
  grouping properties; sections / controls deserialized into the typed
  `AccessControl` tree).
- Wire it into `SyntheticMetadataExtractorIntegrationTests` so the Forms/Reports
  sections of `09-report-grouping-events.expected.json` are actually validated.
- The `AccessNode` `ir/` stays as the intermediate harvester output; the typed
  records are the public contract for the downstream AI.

**Option (C): hybrid** — do Option (A) AND queue Option (B) for a follow-on set.
This is the worst of both: it ships a one-off promotion that doesn't match
either schema and still leaves the typed-extractor work undone.

### Recommendation

**Option (B).** It matches the established pattern, exercises the typed records
that have been sitting unused since 2026-05-06, and lights up the
`09-report-grouping-events.expected.json` fixture that has been waiting for a
test path. The size-of-change is materially larger than the spec's one-liner,
but the work is mechanical pattern application — the schemas are designed,
the example (`TableMetadataExtractor`) is two files away, the test harness is
already there.

If the human adjudicates Option (B), the spec should be updated before Session 2:
- Session 2 scope: write `FormMetadataExtractor` + `ReportMetadataExtractor`, lift
  promoted fields, build `GroupLevels`, deserialize `Children` into
  `IReadOnlyList<AccessControl>`. Extend `SyntheticMetadataExtractorIntegrationTests`
  to validate Forms and Reports.
- Session 3 scope: hermetic unit tests for `GroupLevel` construction and edge
  cases; `docs/ir-schema-forms-reports.md` describing the **typed
  `FormMetadata`/`ReportMetadata` schema** as the public contract (not the
  `AccessNode` intermediate).

If the human adjudicates Option (A), the spec stands as written — but the
"verify GroupLevel and embedded-macro correlation" steps should be downscoped to
"confirm via existing tests" rather than "investigate further," because the
audit has already done the investigation.

---

## 5. Recommended Session 2 scope under Option (B)

1. Add `AccessHarvester.Access/FormMetadataExtractor.cs`:
   - `public static FormMetadata Extract(JsonElement irRoot)` (mirroring `TableMetadataExtractor.Extract`).
   - Promote `RecordSource`, `Filter`, `OrderBy` out of Properties (remove the keys).
   - Decode `DefaultView` numeric → label (e.g. `0` → `"Single Form"`, `1` → `"Continuous Forms"`, `2` → `"Datasheet"`, `3` → `"PivotTable"`, `4` → `"PivotChart"`, `5` → `"Split Form"`).
   - Deserialize `Children` into `IReadOnlyList<AccessControl>` recursively.
   - Read `NonDefaultProperties` array if present.
   - **`Procedures` is intentionally NOT a field on `FormMetadata` /
     `ReportMetadata`** — see `FormMetadata.cs` lines 122-129: "Procedure-level
     structure is surfaced separately by `VbaSignatureEnricher` (the
     `Procedures` array on the IR JSON); it is not re-modelled in the metadata
     schema for Session 1." Downstream consumers that need procedure
     signatures read them from the underlying `ir/` JSON, not from the typed
     record. Do not pass it through in the extractor; if a future
     consumer needs typed procedures on the metadata record, that is a
     separate spec change.
2. Add `AccessHarvester.Access/ReportMetadataExtractor.cs`:
   - Same as `FormMetadataExtractor` plus:
   - Build `IReadOnlyList<GroupLevel>` by enumerating root Properties keyed
     `GroupLevel0`, `GroupLevel1`, … OR by parsing the `GroupLevel` blocks the
     SaveAsText format emits. (The AccessNode tree exposes one or the other —
     see what `AccessTextParser` actually emits for a real grouped report
     fixture.)
   - Decode `KeepTogether` from `GrpKeepTogether` and per-level codes.
   - Best-effort `PageSetup` decoding from `PrtMip` / `PrtDevMode` (or stub out
     and defer — these are binary blobs and "best-effort" per the typed schema).
3. Update `SyntheticMetadataExtractorIntegrationTests.SyntheticFixtures_QueryTableRelationshipMetadataMatchesExpectedContracts`:
   - Add `"Forms"` and `"Reports"` to the validated category list.
   - Implement `AssertForms` / `AssertReports` comparators (mirror
     `AssertQueries`/`AssertTables`).
   - Read actual outputs via `FormMetadataExtractor.Extract` /
     `ReportMetadataExtractor.Extract` from the `ir/` JSON.
4. Run `dotnet build && dotnet test` sequentially.

## 6. Recommended Session 3 scope under Option (B)

1. Author `docs/ir-schema-forms-reports.md`:
   - Document the **typed `FormMetadata` / `ReportMetadata` schema** as the
     public contract for downstream AI consumers.
   - For each typed field, document: what it represents, where it sources from
     in the underlying `AccessNode` `ir/`, what convention applies (only-emit-what-Access-wrote, etc.).
   - Document `AccessControl`, `LabelInfo`, `GeometryInches`, `GroupLevel`, `PageSetup`, `Margins` similarly.
   - Note that the `AccessNode` ir/ JSON is the harvester's intermediate
     representation; the typed metadata is what downstream code reads.
   - Worked example: a small Form and a small Report, showing both the raw
     `ir/` JSON and the resulting typed records.
2. Hermetic unit tests for the extractors:
   - `GroupLevel` construction edge cases (zero levels, single level with header
     only, multi-level, KeepTogether decoding).
   - `DefaultView` decoding for every Access numeric code value.
   - `RecordSource` promotion when present and when absent.
   - Property-bag minus-promoted-keys correctness.
3. Generate `change-log.md` summarising the set and a
   `next-session-set-recommendation.md` per the workflow.

## 7. Gaps already closed — verify only, do not re-implement

These items appear in the spec but the audit confirms they are already produced
correctly by existing enrichers. Session 2 should not spend implementation
effort here — at most, write a sentence in the schema doc pointing to the
enricher source-of-truth.

- **Embedded macro correlation by event name** — `MacroActionsEnricher` emits
  `EventMacros: { OnX: { Actions: [...], InferredFunctionality: null } }` on
  nodes with `*EmMacro` properties.
- **Label resolution** — `LabelResolver` four-round resolution
  (attached → proximity → from-name → from-binding → unresolved).
- **Geometry** — `GeometryEnricher` (twips → inches, sparse).
- **VBA procedure signatures** — `VbaSignatureEnricher` (`Procedures` array on
  the root).
- **NonDefault property decoding** — `PropertyDecoder` (in-place value
  rewrite + sibling `NonDefaultProperties` list).

---

## 8. Build and test result (Step 4)

- `dotnet build`: clean. 0 errors. 321 pre-existing warnings (CA1416
  Windows-only-API false positives on cross-platform-test reachability; xUnit
  style hints).
- `dotnet test`: 1219 passing across `AccessHarvester.Tests` (350P/2S),
  `AccessHarvester.Pipeline.Tests` (355P), `AccessHarvester.Vba.Resolver.Tests`
  (400P), `AccessHarvester.Sql.Grammar.Tests` (85P), `AccessHarvester.Integration`
  (14P), plus 15 more from other small assemblies. 45 failures in
  `AccessHarvester.SymbolDb.Tests` — all driven by the absence of
  `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/`, which is not
  committed. This is the documented baseline matching `main`
  (see `docs/session-sets/table-extractor-coverage/change-log.md`).
- 2 `[SkippableFact]` tests skipped (Access COM not present in this CI-clean
  environment).

No regressions introduced this session (read-only audit).
