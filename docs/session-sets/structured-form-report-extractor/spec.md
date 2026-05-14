# Structured Form/Report Extractor Spec

> **Purpose:** Audit the current `ir/` output for Forms and Reports against the YAML
> coverage baseline from `access-object-extractor-spike`, close the specific gaps found,
> and produce documented ir/ schemas so the downstream AI has a reliable reference for
> what each form/report field means.
> **Created:** 2026-05-14
> **Session Set:** `docs/session-sets/structured-form-report-extractor/`
> **Prerequisite:** `access-object-extractor-spike` (gap analysis complete)
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. The harvester produces data consumed by downstream AI,
> not by humans through a UI. Quality is guarded by hermetic unit tests,
> characterization tests, and cross-provider AI verification.

---

## Background

The spike (`access-object-extractor-spike`) characterized the YAML extractor's form/report
output as "structured property bags, immediately consumable by downstream AI" vs the C#
side producing "opaque SaveAsText .txt files." That framing was accurate for the `raw/`
tier — but the C# pipeline has a Translation phase that was not fully accounted for.

**Actual pipeline for Forms and Reports:**

```
SaveAsTextExporter → raw/*.txt
       ↓  SaveAsTextTranslator (AccessTextParser)
      kb/*.json   (AccessNode JSON tree)
       ↓  GeometryEnricher, LabelResolver, PropertyDecoder,
          TextCapacityEnricher, VbaSignatureEnricher, MacroActionsEnricher
      ir/*.json   (enriched AccessNode tree)
```

`AccessTextParser` parses the SaveAsText `Begin/End` block format into a generic
`AccessNode` tree with `Type`, `Properties`, `Children`, and `VbaCode`. The form/report
`ir/` JSON DOES have structured data — but its shape differs from the YAML:

| Aspect | YAML (`Forms/<name>.yaml`) | C# `ir/*.json` |
|---|---|---|
| RecordSource | explicit top-level field | in `Properties["Record Source"]` |
| Control type | `ControlType: "109"` (numeric code) | `Type: "TextBox"` (string name) |
| Controls | `Controls:` array | `Children:` array (mixed — sections + controls) |
| Event handlers | explicit per-event fields | `Properties["OnCurrent"]` etc. |
| Embedded macro content | `OnCloseEmMacro: "..."` inline | `MacroActions` array (from `MacroActionsEnricher`) |
| Report GroupLevel | explicit (sections, keep-together) | present as child nodes, needs verification |
| Geometry | not in YAML per-control | ✓ `GeometryIn` added by enricher |
| VBA signatures | separate Modules/ file | ✓ `Procedures` array by `VbaSignatureEnricher` |
| Labels resolved | not in YAML per-control | ✓ `Label` object by `LabelResolver` |

**The real gap is narrower than described in the spike analysis.** The C# pipeline IS
producing structured form/report data. The gaps to close are:

1. **RecordSource** — needs to be promoted to a first-class field at the form/report level
   so the AI doesn't need to know to look in `Properties["Record Source"]`
2. **Report GroupLevel** — needs field-by-field verification that section hierarchy and
   grouping metadata (group-on field, sort order, keep-together) are correctly captured
   in the `ir/` output
3. **Embedded macro correlation** — verify that events bound to embedded macros
   (e.g. `OnClose = "[Embedded Macro]"`) have their macro actions properly resolved
   by `MacroActionsEnricher` and accessible alongside the event name
4. **ir/ schema documentation** — the downstream AI needs a reference document
   explaining the `AccessNode` ir/ shape for forms/reports (what fields exist,
   where controls live, how to read event bindings)

This is an **audit-and-fix** set, not a from-scratch rebuild.

---

## Scope

### In scope
- Audit: Run or reconstruct the `ir/` output for HomeHealthCare sample forms and reports;
  compare against YAML coverage table from the spike
- RecordSource promotion: add a first-class `RecordSource` field to the form/report
  ir/ output (either in `SaveAsTextTranslator` or a new lightweight enricher)
- Report GroupLevel verification and fix (if needed)
- Embedded macro correlation verification and fix (if needed)
- Schema documentation: write `docs/ir-schema-forms-reports.md` describing the ir/ shape
- Tests for the fixes

### Out of scope
- Replacing `SaveAsTextExporter` with a new extraction mechanism (the existing pipeline
  is sufficient; adding a new extractor would break the Translation/Enrichment chain)
- Table or query extraction changes (`table-extractor-coverage` handles those)
- VBA parsing or enrichment beyond what `VbaSignatureEnricher` already does
- Geometry, label resolution, or text capacity changes (those enrichers work correctly)

### Windows/Access dependency
Session 1's audit step requires running `dotnet build && dotnet test` and, for the
full integration test, Windows + Access (`[SkippableFact]` integration). The
characterization tests in `tests/AccessHarvester.CharacterizationTests/` may provide
sufficient fixtures for the audit without re-running the extractor — check those first.

---

## Session Plan

### Session 1 of 3: Audit the Current ir/ Output

**Steps:**
1. Read `AccessHarvester.Core/Translation/AccessTextParser.cs`,
   `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs`, and
   `AccessHarvester.Core/Model/AccessNode.cs` to understand the current ir/ shape.
2. Check `tests/AccessHarvester.CharacterizationTests/` for any committed ir/ fixture
   files for HomeHealthCare forms or reports. If they exist, read representative
   samples.
3. Read `AccessHarvester.Core/Enrichment/GeometryEnricher.cs`,
   `LabelResolver.cs`, `MacroActionsEnricher.cs` to understand what they add and
   where they add it in the AccessNode tree.
4. Run `dotnet build && dotnet test` to confirm the current test suite is green.
5. Route an `analysis` call with: (a) the ir/ JSON for one form (from fixtures or
   generated), (b) the corresponding YAML from `samples/DatabaseYaml/`, (c) the
   coverage table from `gap-analysis-and-strategy.md`. Ask: which fields in the YAML
   are present in the ir/ JSON, which are absent, and which are present under a
   different key/structure?
6. Write a findings summary as `audit-findings.md` (at the session-set root —
   NOT under `session-reviews/`, which is owned by `log.save_session_review()`
   per `lessons-learned.md`) covering:
   - Current ir/ schema for forms (what fields exist at form level and control level)
   - Current ir/ schema for reports (sections, GroupLevel, events)
   - Specific gaps confirmed (e.g. RecordSource not first-class, GroupLevel shape)
   - Gaps found to be already closed (enrichers that already handle them)
7. Route session verification.

**Creates:**
- `docs/session-sets/structured-form-report-extractor/audit-findings.md`

**Touches:** (read-only audit; no code changes)

**Ends with:** A concrete, evidence-backed list of the actual gaps in the ir/ output
vs. the YAML coverage baseline. "We assumed X was missing; it's actually present as Y"
or "Gap Z is confirmed; fix in Session 2 is: [approach]." Verification VERIFIED.

**Progress keys:** `session-1/audit-ir-schema`, `session-1/gaps-confirmed`

---

### Session 2 of 3: FormMetadataExtractor + ReportMetadataExtractor

> **Scope locked at end of Session 1.** Per `audit-findings.md` §4, the human
> adjudicated **Option B** (2026-05-14): apply the
> `AccessHarvester.Access/TableMetadataExtractor.cs` pattern to forms and
> reports. The earlier Option A framing (one-line `RecordSource` promotion in
> `SaveAsTextTranslator.BuildPayload`) was retired because the audit found
> the prior spike's premise wrong — the YAML baseline does not promote
> `RecordSource` either, so the real gap is between the current `AccessNode`
> `ir/` JSON and the already-designed `FormMetadata` / `ReportMetadata`
> typed records that have no extractor yet.

**Steps:**
1. Verify prior session committed (`git status` against the session-set folder
   and the prior set's residual closeout flips — bundle if present).
2. Read Session 1's `audit-findings.md` to refresh the gap list, the typed
   record contracts (`FormMetadata`, `ReportMetadata`, `AccessControl`,
   `GroupLevel`, `LabelInfo`, `GeometryInches`), and the
   `TableMetadataExtractor` precedent.
3. Add `AccessHarvester.Access/FormMetadataExtractor.cs`:
   - `public static FormMetadata Extract(JsonElement irRoot)` mirroring
     `TableMetadataExtractor.Extract`.
   - Promote `RecordSource`, `Filter`, `OrderBy` out of the Properties bag
     (remove the keys after lift — exact-value-lift rule per
     `FormMetadata.cs`).
   - Decode `DefaultView` numeric → label (0 → "Single Form",
     1 → "Continuous Forms", 2 → "Datasheet", 3 → "PivotTable",
     4 → "PivotChart", 5 → "Split Form"); leave the raw numeric in
     Properties (decoded-lift rule).
   - Deserialize `Children` recursively into `IReadOnlyList<AccessControl>`,
     promoting per-node `Name` (remove from per-node Properties).
   - Read `NonDefaultProperties` and `GeometryIn` if present (already
     emitted by `PropertyDecoder` / `GeometryEnricher`).
   - Read `Label` if present (emitted by `LabelResolver`). The JSON enricher
     writes lowercase `{ source, text }`; the typed `LabelInfo` record uses
     PascalCase `{ Source, Text }` — extractor maps the case.
   - **Do NOT** put `Procedures` on `FormMetadata` (per `FormMetadata.cs`
     lines 122-129 the typed schema deliberately does not re-model it;
     consumers read it from the underlying `ir/` JSON).
4. Add `AccessHarvester.Access/ReportMetadataExtractor.cs`:
   - Everything `FormMetadataExtractor` does (shared shape for shared fields).
   - Plus: build `IReadOnlyList<GroupLevel>` by inspecting the report's
     `GroupLevel<N>Header` / `GroupLevel<N>Footer` section nodes in `Children`
     (set `HasHeader` / `HasFooter` based on section presence) and the
     report's grouping properties (GroupOn / GroupingMode / GroupInterval /
     SortOrder / KeepTogether — read a real fixture's parser output to
     confirm where each lives in the AccessNode tree).
   - `PageSetup` decoding from `PrtMip` / `PrtDevMode` binary blobs is
     **deferred** per the human's caveat. Stub to `null` and document the
     deferral in a comment + the change-log; a follow-on session can pick
     it up. The typed schema already documents `PageSetup` as
     best-effort / nullable so this is contract-compliant.
5. Extend `tests/AccessHarvester.Tests/SyntheticMetadataExtractorIntegrationTests.cs`:
   - Add `"Forms"` and `"Reports"` to the validated category list (currently
     hard-coded to `Queries` / `Tables` / `Relationships` only).
   - Implement `AssertForms` and `AssertReports` comparators that walk the
     `09-report-grouping-events.expected.json` `Forms[]` / `Reports[]`
     sections and validate against `FormMetadataExtractor.Extract` /
     `ReportMetadataExtractor.Extract` output.
6. Route code review on the two new extractor files + the test extension.
7. `dotnet build && dotnet test` (sequential per `lessons-learned.md`).
8. Route session verification (cross-provider, `task_type="session-verification"`).

**Creates:**
- `AccessHarvester.Access/FormMetadataExtractor.cs`
- `AccessHarvester.Access/ReportMetadataExtractor.cs`
- `docs/session-sets/structured-form-report-extractor/session-reviews/session-002.md`

**Touches:**
- `tests/AccessHarvester.Tests/SyntheticMetadataExtractorIntegrationTests.cs`
  (un-hard-code the category filter; add Forms/Reports comparators)
- `docs/session-sets/structured-form-report-extractor/disposition.json` (update)

**Ends with:** The two new extractors produce typed records matching the
`09-report-grouping-events.expected.json` aspirational fixture's `Forms[]`
and `Reports[]` sections. `dotnet test` green. Verification VERIFIED.

**Progress keys:** `session-2/form-metadata-extractor`,
`session-2/report-metadata-extractor`, `session-2/integration-tests-extended`

---

### Session 3 of 3: Hermetic Tests + Typed-Schema Documentation

> **Scope locked at end of Session 1.** Aligned with Option B from
> `audit-findings.md` §4 — Session 3 documents the **typed
> `FormMetadata` / `ReportMetadata` schema** as the public contract for
> downstream AI consumers (not the intermediate `AccessNode` `ir/` JSON,
> which is a harvester implementation detail).

**Steps:**
1. Verify prior session committed.
2. Write hermetic unit tests for the two new extractors:
   - `FormMetadataExtractorTests`:
     - `RecordSource` / `Filter` / `OrderBy` promotion when present and when
       absent (and that the keys are removed from `Properties` after lift).
     - `DefaultView` decoding for every numeric code (0..5).
     - `Name` promotion on `AccessControl`.
     - `Properties` bag passes-through unknown keys faithfully.
   - `ReportMetadataExtractorTests`:
     - All `FormMetadataExtractor` tests above (shared shape).
     - `GroupLevels` construction: zero levels, single level with header
       only / footer only / both, multi-level with mixed header/footer
       presence.
     - `KeepTogether` decoding.
     - `PageSetup` stub returns `null` (deferred) — explicit test pins the
       deferral so a future session that implements it has a visible
       contract change.
3. Write `docs/ir-schema-forms-reports.md`:
   - The **typed `FormMetadata` / `ReportMetadata` schema is the public
     contract.** Document each typed field: what it represents, where it
     sources from in the underlying `AccessNode` `ir/`, what convention
     applies (only-emit-what-Access-wrote, provenance `{ source, ... }`,
     etc.).
   - Document `AccessControl` (Type, Name, Properties, Children, GeometryIn,
     Label, NonDefaultProperties), `LabelInfo` (Source, Text),
     `GeometryInches` (X / Y / W / H sparse), `GroupLevel` (Level, GroupOn,
     GroupingMode, GroupInterval, SortOrder, HasHeader, HasFooter,
     KeepTogether), `PageSetup` + `Margins` (note: deferred decoding,
     currently always null).
   - Briefly describe the `AccessNode` `ir/` JSON as the harvester's
     intermediate output and point readers at the enrichers
     (`GeometryEnricher`, `LabelResolver`, `MacroActionsEnricher`,
     `VbaSignatureEnricher`, `PropertyDecoder`, `TextCapacityEnricher`)
     for fields not re-modeled on the typed records (e.g., `Procedures`,
     `EventMacros`).
   - Worked example: a small Form and a small Report, showing both the raw
     `ir/` JSON and the resulting typed records side by side.
4. Route documentation review.
5. `dotnet build && dotnet test`.
6. Route session verification.
7. Generate change-log and next-session-set recommendation.

**Creates:**
- `docs/ir-schema-forms-reports.md`
- `tests/AccessHarvester.Tests/FormMetadataExtractorTests.cs`
- `tests/AccessHarvester.Tests/ReportMetadataExtractorTests.cs`
- `docs/session-sets/structured-form-report-extractor/change-log.md`
- `docs/session-sets/structured-form-report-extractor/next-session-set-recommendation.md`
- `docs/session-sets/structured-form-report-extractor/session-reviews/session-003.md`

**Touches:**
- `docs/session-sets/structured-form-report-extractor/disposition.json` (update — close-out status)

**Ends with:** New unit tests pass. `docs/ir-schema-forms-reports.md` is
complete enough that a downstream AI can use the typed
`FormMetadata` / `ReportMetadata` records as the canonical contract for
form/report metadata. Verification VERIFIED.

**Progress keys:** `session-3/unit-tests`, `session-3/typed-schema-docs`

---

## Deliverables

1. Confirmed audit of ir/ output vs. YAML baseline + typed-schema target
   (Session 1 — `audit-findings.md`)
2. `FormMetadataExtractor` + `ReportMetadataExtractor` producing typed
   `FormMetadata` / `ReportMetadata` records, exercised against the
   `09-report-grouping-events.expected.json` aspirational fixture via
   `SyntheticMetadataExtractorIntegrationTests` (Session 2)
3. Hermetic unit tests for both extractors (Session 3)
4. `docs/ir-schema-forms-reports.md` — typed-schema reference for downstream AI (Session 3)
5. `change-log.md` (Session 3)
6. `next-session-set-recommendation.md` (Session 3)

**Explicitly deferred:** `PageSetup` decoding from `PrtMip` / `PrtDevMode`
binary blobs. The typed schema documents this as best-effort / nullable;
Session 2 stubs to `null` and a future session can address it without
breaking the contract.
