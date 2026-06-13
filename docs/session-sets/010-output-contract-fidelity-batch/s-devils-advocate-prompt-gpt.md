Reviewed against the Set 010 target commits `7c4b43d` and `ee89e0c`. Current `HEAD` also contains a later prompt-only commit (`17824d8`), but `git show --stat` and `git diff --name-only 7c4b43d^..ee89e0c` confirmed the reviewed code and golden surface is exactly the Set 010 closeout pair. I also ran the narrow regression slice that covers the touched areas; it passed `157/157`.

## Pass 1 — steelman attempts

1. **F-2-1 / F-3-7 (DAO decode + AST QueryType): failed to prove.**
   - `AccessHarvester.Core/Extraction/QueryExporter.cs:68` now decodes DAO `QueryTypeCode` `16` as `Crosstab`, and the classification layer keys off the shared constants in `AccessHarvester.Pipeline/UsageIdentification/DaoQueryTypeCodes.cs:17`.
   - `AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs:278`, `:285`, and `:303` route crosstabs, action queries, and infra queries off that corrected table.
   - `AccessHarvester.Pipeline/Manifest/QuerySqlProjector.cs:47` and `:138` derive `Sql.Parsed.QueryType` from the parsed AST shape, not from the DAO label.
   - The four committed HHC crosstabs now agree everywhere I checked: each `qxt*.json` / `qxt*.ir.json` shows `QueryTypeCode: 16` and `QueryTypeName: "Crosstab"`, and `manifest.json` now shows both top-level `QueryType` and nested `Sql.Parsed.QueryType` as `"Crosstab"`.
   - I did not find a remaining contradiction between DAO provenance and AST-derived `Sql.Parsed.QueryType` on the reviewed corpus.

2. **F-3-5 (PassingMechanism): failed to prove.**
   - `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs:273` emits the schema-declared `PassingMechanism`; explicit `ByVal` and `ByRef` are written at `:282-:283`, and unspecified remains omitted by design.
   - A manifest-wide count over Forms + Reports + Modules returned `ByValCount=103` and `ByRefCount=5`, exactly matching the set closeout claim.
   - Focused diff inspection showed prior `"ByRef": true` entries replaced by `"PassingMechanism": "ByRef"`, with no surviving old boolean in the changed surface.
   - I found no evidence that a path which formerly preserved an explicit pass mode now drops it.

3. **F-2-4 (Q6): failed to prove.**
   - `AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs:271` now consults `QueryRefsNoTables`, and the helper itself is live at `:910`.
   - `AccessHarvester.Pipeline/UsageIdentification/QueryTableReferenceCollector.cs:43` collects raw `FROM` / `JOIN` names before any known-query filter is applied, so an unresolved source does not silently become “all known queries” just because the known subset is small.
   - The new targeted coverage in `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/UsageClassifierTests.cs:468-:500` pins both the positive `QueryReferencesNoTables` case and a guard case.
   - The changed `LauncherSurfaceTests` assertion is a correction, not a weakening: `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/LauncherSurfaceTests.cs:318-:324` now expects the documented `QueryReferencesNoTables` branch instead of the old dead-code fallback.
   - Residual limitation: Q6 is still only as precise as the regex-level SQL target extractor. I did not find a present over-fire on HHC.

4. **F-2-5 (inline-SQL table credit): no proven over-credit on the reviewed corpus.**
   - `AccessHarvester.Pipeline/UsageIdentification/SignalIndex.cs:120-:149` deliberately credits all catalog-resolved inline-SQL `FROM` / `JOIN` targets into the broad `UsedAsRecordSource` set, while keeping the form/report-specific sets query-only.
   - The HHC live case does support the change: `manifest.json` contains an inline SQL record source centered on `tblServiceRateLimits` (`SELECT tblServiceRateLimits.*, tblServices.ServiceName FROM tblServiceRateLimits INNER JOIN tblServices ...`).
   - The usage diff shows exactly one table classification change from this fix: `tblServiceRateLimits` flips `StandaloneUtility -> MainDataEntry` and gains `used-as-RecordSource`. I did not find any additional HHC object churn attributable to the broader credit.
   - Residual risk remains for future corpora: because the broad set credits all catalog targets, a secondary join/subquery table could be over-credited in a more complex inline SQL form. I could not prove a bad current output from that on Set 010.

5. **F-2-7 (numeric / bit validation): failed to prove on actual output.**
   - `AccessHarvester.Pipeline/Migration/SqlValueFormatter.cs` now uses one gate for formatting and invalid-token detection.
   - The added tests cover valid invariant numerics, invalid injected tokens, blank/whitespace handling, and binary-family exemptions: `tests/AccessHarvester.Pipeline.Tests/Migration/CsvTableReaderAndValueTests.cs` and `tests/AccessHarvester.Pipeline.Tests/Migration/SqlServerDataLoadGeneratorTests.cs`.
   - I did not find a committed or probed Access export using `.5`, `1.`, `1D2`, or locale-comma forms, so I could not substantiate the claim that the new regex is already rejecting legitimate produced data.
   - This remains a future-corpus risk, not a proven defect in the reviewed set.

6. **F-1-4 / F-1-7: failed to prove.**
   - `AccessHarvester.Core/Pipeline.cs:770` and `:782` now emit deterministic `FORM_SPEC_MISSING` diagnostics; the new tests at `tests/AccessHarvester.Tests/MissingSpecDiagnosticsTests.cs:21`, `:39`, and `:54` cover both partial and whole-directory-missing cases.
   - `AccessHarvester.Core/Extraction/SystemObjectNames.cs:15` and `:18` centralize `MSys` / `~` skipping, and `tests/AccessHarvester.Tests/SystemObjectNamesTests.cs:21` and `:29` pin the predicate.
   - I did not find a false positive or false negative in the reviewed output.

7. **Encoding fix (highest-risk hypothesis): the steelman fails on the reviewed corpus, but one deferred divergence remains.**
   - The shared decoder really is shared by the two run-pipeline read sites: `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs:69` and `:78`, `AccessHarvester.Core/Translation/SaveAsTextDecoder.cs:50` and `:68`, and `AccessHarvester.Core/Pipeline.cs:339`.
   - I verified the actual mixed-encoding case the set claims. A raw probe of `tmp/golden-staging-s2/AccessMetaData/HomeHealthCare/raw/ModulesAndClasses/basResetFixture.txt` found the bytes `C2 A7` at offset `451`; strict UTF-8 decodes the snippet as `docs/uat-seed-design.md §0).`, while cp1252 decodes the same bytes as `docs/uat-seed-design.md Â§0).`.
   - The new tests pin both observed failure modes: `tests/AccessHarvester.Tests/SaveAsTextDecoderTests.cs:21` covers UTF-8-without-BOM `§`, `:33` covers cp1252 `0x97 -> —`, and `:53` covers UTF-8 BOM.
   - I also scanned the committed `samples/GoldenOutput/HomeHealthCare` outputs for the prompt’s concrete ambiguity example (`C3 A9`) and found none, so I could not prove “common ANSI path now silently corrupts real data.”
   - **Residual gap, not a set-blocking defect:** `AccessHarvester.Cli/Program.cs:552` and `:594-:600` still leave `parse` on the older BOM-or-cp1252 path rather than `SaveAsTextDecoder`. That is a real deferred divergence. I am not counting it as a Set 010 defect because I did not prove a bad consumer-visible result from that dev-only CLI path.

8. **Golden regen integrity: failed to prove unexplained churn.**
   - `git diff --name-only 7c4b43d^..ee89e0c -- samples/GoldenOutput/HomeHealthCare` lists exactly the changed surface the set claims: 4 query kb files, 4 query ir files, 2 module kb files, 2 module ir files, `manifest.json`, 2 usage files, and 2 cleanup files.
   - The cleanup diff is BOM removal only: the leading `∩╗┐{` becomes `{` and nothing else changes.
   - The usage diff is fully attributable: four crosstabs move `PossiblyUnused 0.65 -> Reporting 0.70` and gain the crosstab/out-of-band questions, and `tblServiceRateLimits` flips `StandaloneUtility -> MainDataEntry` with `used-as-RecordSource` evidence.
   - `git diff --name-only` over Forms / Reports / Tables / `migration/` returned no output.
   - Module JSON spot-checks show the claimed glyph repairs: `basResetFixture.ir.json` contains `\u00A7`, while `basUtilities.ir.json` / `basDataOperations.json` contain `\u2014`; I did not see `\uFFFD` or `Â§` in those reviewed outputs.

9. **Decision-consensus soundness: failed to prove the defer side should have won.**
   - Q6 was a live doc/code divergence with golden-neutral implementation and targeted tests.
   - F-2-5 was a live HHC output defect, and fixing it inside the coordinated regen kept every resulting delta attributable within the same set.
   - I do not see hidden churn or scope creep that would have made deferral the stronger call.

## Pass 2 — completeness critic

- I found no weakened or deleted test. The one assertion edit the prompt called out in `LauncherSurfaceTests` is a direct correction from the old dead-Q6 behavior to the documented `QueryReferencesNoTables` branch.
- The set still leaves one explicit decode divergence in place: `AccessHarvester.Cli parse` is not on `SaveAsTextDecoder` yet (`AccessHarvester.Cli/Program.cs:552`, `:594-:600`). That is real follow-on debt, but I could not prove it corrupts any shipped `run` output.
- The highest-risk decoder ambiguity case remains ungrounded by corpus evidence. The tests pin the real `C2 A7` UTF-8-no-BOM case and the `0x97` cp1252 case, but neither the tests nor the committed HHC golden contain a real cp1252-valid-UTF8 byte pair such as `C3 A9`. `SaveAsTextDecoder.cs` explicitly acknowledges that residual ambiguity in its comment.
- The numeric-token regex is well-pinned for the intended invariant forms, but this set did not re-ground it against locale-variant or edge-format Access exports. That is a future-fixture gap, not a present regression.
- The COM golden regression still does not exercise usage / cleanup / migration end-to-end, but the preserved volatile fields plus the exact 17-file diff surface make the manual regen explanation credible here.

Overall judgment: the contrarian hypotheses did not produce a proven defect in the reviewed Set 010 output-contract work. The closest remaining concern is the intentionally deferred `parse`-path decode divergence, and that reads as follow-on debt rather than a reason to reopen this set.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
