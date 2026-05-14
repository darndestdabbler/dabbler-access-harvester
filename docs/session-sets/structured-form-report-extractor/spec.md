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

### Session 2 of 3: Close the Confirmed Gaps

**Steps:**
1. Verify prior session committed.
2. Read Session 1's findings (`audit-findings.md`) to get the confirmed
   gap list and proposed fix approaches.
3. Implement RecordSource promotion — likely a one-line addition in
   `SaveAsTextTranslator.BuildPayload` that extracts `RecordSource` from the parsed
   node's Properties and adds it as a named field on the output payload, OR a new
   micro-enricher that runs first in the chain.
4. Implement any confirmed GroupLevel / report section gap fix.
5. Implement any confirmed embedded macro correlation gap fix — verify that
   `MacroActionsEnricher` is applied to embedded macros on form events, and that
   the output correlates the action list with the event name.
6. Route code review.
7. `dotnet build && dotnet test`.
8. Route session verification.

**Creates:**
- `docs/session-sets/structured-form-report-extractor/session-reviews/session-002.md`

**Touches:**
- `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs` (RecordSource promotion)
- `AccessHarvester.Core/Enrichment/*.cs` (if GroupLevel or macro correlation fixes needed)

**Ends with:** All gaps from Session 1's findings list are either fixed or explicitly
deferred with rationale. `dotnet test` green. Verification VERIFIED.

**Progress keys:** `session-2/record-source-promotion`, `session-2/grouplevel-fix`,
`session-2/macro-correlation-fix`

---

### Session 3 of 3: Tests and ir/ Schema Documentation

**Steps:**
1. Verify prior session committed.
2. Write hermetic unit tests for: RecordSource extraction from a form fixture;
   GroupLevel field extraction from a report fixture (if applicable);
   MacroActions correlation with an event that has an embedded macro.
3. Write `docs/ir-schema-forms-reports.md`:
   - Top-level structure for a form ir/ JSON (Type, Properties, Children)
   - Control nodes: how to find them (in Children of Section nodes), what Type
     values map to what control types, what properties are standard vs. optional
   - Event handler fields: where they live in Properties, how to detect
     "[Event Procedure]" vs "[Embedded Macro]" vs embedded macro name, where
     `MacroActions` live in the tree
   - Report-specific: Section nodes (Detail, PageHeader, GroupHeader_N etc.),
     GroupLevel properties
   - RecordSource: the first-class field added in Session 2
   - VBA signatures: where `Procedures` array lives (from `VbaSignatureEnricher`)
   - Geometry: where `GeometryIn` lives (from `GeometryEnricher`)
4. Route documentation review.
5. `dotnet build && dotnet test`.
6. Route session verification.
7. Generate change-log and next-session-set recommendation.

**Creates:**
- `docs/ir-schema-forms-reports.md`
- `docs/session-sets/structured-form-report-extractor/change-log.md`
- `docs/session-sets/structured-form-report-extractor/next-session-set-recommendation.md`
- `docs/session-sets/structured-form-report-extractor/session-reviews/session-003.md`

**Touches:**
- `tests/AccessHarvester.Tests/` (new tests)

**Ends with:** New unit tests pass. `docs/ir-schema-forms-reports.md` is complete
enough that a downstream AI can use it as a reference for interpreting form/report
ir/ JSON. Verification VERIFIED.

**Progress keys:** `session-3/unit-tests`, `session-3/ir-schema-docs`

---

## Deliverables

1. Confirmed audit of ir/ output vs. YAML baseline (Session 1 findings)
2. RecordSource promotion fix + any GroupLevel / macro-correlation fixes
3. Hermetic unit tests for all fixes
4. `docs/ir-schema-forms-reports.md` — ir/ schema reference for downstream AI
5. `change-log.md`
6. `next-session-set-recommendation.md`
