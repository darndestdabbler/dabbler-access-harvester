Concern 1 — NO WEAKENED ASSERTIONS

Most of the re-based SymbolDb assertions remain substantive and are not the kind of vacuous “it ran” guards the policy forbids. `HhcFixtureSmokeTests` pins exact module count, exact module kinds/encodings, exact procedure signatures, exact enum members, and exact locals. `CrossModuleResolutionTests` still asserts specific `ToFqn` targets and exact unresolved behavior. `ReferencesExtractorTests` keeps concrete semantic checks for caller kind, local-variable `to_fqn` shape, `control_access`, `enum_value_use`, and DB round-trip counts. `PersistenceAndDedupTests` still contains exact synthetic dedup assertions and exact signature formatting. So the core SymbolDb re-base did not broadly collapse into smoke-only testing.

There are, however, two real weakening points.

First, the macro path in `Session3ExtractorTests` is weaker than the original Northwind-backed action-count assertion. Because the committed HHC macro files produce zero `MacroActions`, both `MacroExtractor_PositionsAreDenseAndContiguous` and `Session3_FullPipeline_WritesExpectedTableCounts` now allow an empty action set. That means the extractor-level “macro file -> action rows emitted” behavior is no longer exercised live by this test file. The justification that `MacroXmlParserTests` covers the parser is only partial: parser coverage is not the same as extractor integration coverage.

Second, `CrossModuleResolutionTests.Single(...)` is named as though it enforces uniqueness, but it only asserts `matches.Count > 0` and returns the first match. That preserves existence, but it no longer proves there is exactly one reference row for the identifier. If duplicate rows were emitted, these tests could still pass. That is a smaller issue than the macro one, but it is still a weakening relative to a true single-row assertion.

So for concern 1: mostly good, but not fully clean. The macro action path is the substantive exception, and the cross-module helper is a minor assertion-strength leak.

Concern 2 — RE-BASE FIDELITY

On the main SymbolDb semantics, the re-base is strong and faithful.

`CrossModuleResolutionTests` is the best example. The session note correctly identifies that HHC forms use qualified cross-module calls, which do not exercise the original bare-name binding behavior. The replacement synthetic two-module fixture explicitly reproduces the same semantic claims: bare-name cross-module procedure binding (`Greet`, `Farewell`), public enum-member spill (`clrRed`), same-module binding (`LocalHelper`), unresolved out-of-scope identifier (`UnknownThing`), and persistence of those resolved references through SQLite. That is a high-fidelity re-base, not a semantic drift.

`ReferencesExtractorTests` also preserves the listed claims well. It still verifies:
- references and member-access extraction happen,
- expected identifiers appear in the probe target,
- caller kinds distinguish UI event handlers from standard-module procedures,
- persistence round-trips reference/member-access row counts,
- local-variable `to_fqn` includes the procedure segment,
- `control_access` rows exist and are shaped correctly,
- `enum_value_use` rows resolve to the expected enum declaration.

`PersistenceAndDedupTests` preserves the important semantics too. Signature formatting is pinned exactly for the ParamArray probe. Dedup still has an exact synthetic twin-procedure test that proves canonical selection and sibling tagging deterministically. The HHC-backed dedup smoke is looser, but the exact algorithmic claim is preserved elsewhere in the same file.

`Session3ExtractorTests` is faithful for form controls, event bindings, query extraction, dead-query marking, lookup-table scoring, row-source score tiering, and full-pipeline persistence. The exact `cboServiceID` assertions are especially good: they pin form FQN, control type, control source, row source, bound table/query, and bound column.

The fidelity gap is the macro extractor action path. The file-level macro contract is still asserted, but the prior semantic claim that real macro action rows are extracted is no longer preserved by an equivalent live test. If HHC lacks the construct, the stated policy was to add a committed synthetic fixture rather than weaken; that does not appear to have been done here.

So for concern 2: fidelity is strong for cross-module resolution, references/member access, caller kinds, local FQN, persistence, dedup, signature formatting, and Session-3 form/query/table extraction; the one notable fidelity loss is macro action extraction.

Concern 3 — NO SILENT COVERAGE DROP

The deletes and arm trims look justified from the evidence provided.

Deleting `AccessHarvester.TranslationProbe.Tests` does not appear to drop harvester or SymbolDb engine coverage. The decision record frames it as a leaf test project for a spent C# translation probe of proprietary form logic, and nothing in the provided files contradicts that. Likewise, deleting `GenerateArtifacts.cs` appears safe: a dev regenerator whose only meaningful assertion was process exit is not unique engine coverage, and the persistence/builder round-trip remains covered elsewhere.

Trimming the Northwind arm from `SyntheticDbIntegrationTests`, `CanonicalGoldenRegressionTests`, and `GoldenCorpusRegenerator` also does not look like a silent coverage drop. Per the supplied ground truth, those Northwind paths were already gitignored/skipped on fresh clones or in CI, while the HHC arms remain live. Removing dead/skipped proprietary arms is consistent with the session goal and does not weaken live coverage.

The coverage-drop concern that remains is again the macro-action path. In the provided files, I do not see another extractor-level test that proves `MacroExtractor.Extract(...)` emits nonzero `MacroActions` when given action-bearing input. `MacroXmlParserTests` may cover parsing, but not necessarily the extractor/file-wiring behavior. Since the original fixture-backed test apparently did exercise action extraction, replacing it with zero-action HHC fixture checks does create a plausible silent gap.

So for concern 3: the deletes and Northwind-arm removals appear safe; the only substantive coverage-drop risk I can substantiate from the provided material is macro action extraction.

Concern 4 — REDACTION COMPLETENESS + CORRECTNESS

Within the provided re-based code files, I do not see lingering proprietary structural identifiers of the `frm*`/`rpt*`/`qry*`/`mod*` NorthwindDevEdition kind. The test class/file renames are cleaned up (`HhcFixtureSmokeTests` instead of `NorthwindFixtureSmokeTests`), the live fixture pointers now target committed HHC assets, and the synthetic cross-module test uses neutral synthetic names. So the shown code-side re-base looks clean.

I cannot fully verify the five “redact-literals” files for internal rename consistency because their contents are not included here. On the evidence supplied, I can only say the described strategy is sound: paired input/expected renames preserving object-kind prefixes would preserve behavior if applied consistently.

One concrete residue problem is present in the provided decision record itself: `docs/session-sets/005-northwind-embedded-residue-remediation/test-triage.md` still spells out multiple proprietary structural identifiers such as `frmCompanyDetail`, `rptInvoice`, `qryInvoice`, `modStrings`, and others. That is in a tracked file at HEAD, and it directly contradicts the file’s own “no proprietary content” note. If the remediation goal is removal from the tracked tree, not just executable tests, this doc reintroduces the very residue the session set is supposed to eliminate.

So for concern 4: the shown re-based test code looks clean, but the supplied triage doc is not fully redacted and should be sanitized if the scope truly is “tracked tree at HEAD.”

Concern 5 — SCOPE DISCIPLINE

The provided changes are disciplined with respect to runtime/product scope. Everything shown is test code, test harness, or documentation. `FixtureLoader.cs` changes only the fixture root from the local proprietary KB to the committed HHC KB. The re-based test files consume either HHC committed fixtures or inline synthetic fixtures. I see no evidence here of changes to harvester or SymbolDb engine behavior.

I also see nothing suggesting git-history rewrite activity; the material is all HEAD-state file content and a decision record. And the local-only proprietary fixture appears untouched: the tests no longer depend on it, but there is no sign of editing or deleting it.

So for concern 5: satisfied on the provided evidence.

```json
{"verdict":"ISSUES_FOUND","issues":[{"severity":"Major","location":"tests/AccessHarvester.SymbolDb.Tests/Session3ExtractorTests.cs","summary":"The re-base drops live extractor-level coverage that macro files produce MacroActions rows, because the HHC fixture yields zero actions and the remaining macro-action assertions pass vacuously.","fix":"Add a committed synthetic macro fixture or inline temp JSON in this test file that exercises MacroExtractor on action-bearing input, then assert MacroActions.Count > 0 plus exact action/FQN/position expectations."},{"severity":"Major","location":"docs/session-sets/005-northwind-embedded-residue-remediation/test-triage.md","summary":"The decision record still contains multiple proprietary structural identifiers despite claiming 'no proprietary content', leaving tracked-tree residue at HEAD.","fix":"Rewrite the document to use neutral placeholders or category-level descriptions only, avoiding original frm/rpt/qry/mod identifiers entirely."},{"severity":"Minor","location":"tests/AccessHarvester.SymbolDb.Tests/CrossModuleResolutionTests.cs","summary":"The helper named Single only checks that at least one matching reference exists and returns the first row, so duplicate reference rows would not fail these tests.","fix":"Replace the helper body with Assert.Single(matches) and return that single row, or assert the exact expected match count explicitly."}]}
```