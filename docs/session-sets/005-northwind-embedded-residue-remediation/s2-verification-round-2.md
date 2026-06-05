Issue 1 — macro action extraction coverage: this is resolved.

The new `MacroExtractor_ActionBearingMacro_EmitsActionsWithDensePositions` test does restore non-vacuous extractor-level coverage for the path that was previously missing. It does not merely test the XML parser in isolation; it writes a synthetic macro JSON file to disk, places an `_AXL:` payload in `Properties.Comment`, invokes `MacroExtractor.Extract`, and then asserts on the emitted `MacroActions`. That is the right integration seam for the specific gap Round 1 identified: “macro file -> action rows emitted.”

The assertions are also materially non-vacuous. The test requires:

- exactly one extracted macro row,
- non-empty `SourceXml`,
- exactly 2 `MacroActions`,
- all action rows tied back to the extracted macro’s FQN,
- dense positions `{0,1}`,
- ordered action names `Beep`, then `OpenForm`,
- presence of the `FormName` argument value `frmHome`.

That is enough to prove that an action-bearing macro file produces action rows and that the rows retain ordering and arguments. It complements, rather than duplicates, the parser-only coverage in `MacroXmlParserTests`. So on the narrow question asked here, yes: extractor-level coverage for action emission has been restored with a non-vacuous assertion.

Issue 2 — proprietary structural identifiers in `test-triage.md`: this is not fully resolved.

Most of the document has been sanitized appropriately, and the remaining uses of `NorthwindDevEdition` as a brand token are within the stated allowance. The synthetic HHC names such as `frmProviderSearch`, `basListHelpers`, `frmClientServiceNeedDetail`, and `tlkpServiceCategories` are also within the stated allowance.

However, the document still contains proprietary structural identifiers in the delete-project section:

- `tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbe.cs`
- `tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbeTests.cs`

Those are not just the allowed bare brand token, and they are not synthetic HHC names. They preserve a specific `frm*`-derived structural name inside the prose artifact being audited. Since the confirmation requested is whether the triage doc is now free of proprietary structural identifiers, I cannot confirm that as written. This remaining residue should be redacted to an object-kind/category form the same way the other structural identifiers were handled.

Issue 3 — `Single` helper renamed to `FirstRef`: this is resolved.

The naming/behavior mismatch is fixed cleanly. The helper is now named `FirstRef`, and the comment explicitly states its contract: it asserts existence and returns the first match, while each test is responsible for asserting the actual per-identifier resolution claim. That matches the implementation:

- it gathers matches for the identifier,
- asserts `Count > 0`,
- returns `matches[0]`.

This does not weaken the tests relative to what they were actually doing before, because the old helper never enforced uniqueness despite being named `Single`. The rename makes the test code honest and easier to read without reducing the substantive assertions in the call sites.

Regression check — no new code-level regression is evident from the two changed test files, but the documentation sanitization fix is incomplete as noted above.

On weakened assertions: I do not see a new weakened assertion introduced by the C# changes. The new macro test strengthens the previously vacuous area. The helper rename does not downgrade any claim; it only aligns the name and comment with the already-existing behavior.

On compile risk: I do not see a meaningful new compile risk in either changed test file. The added test uses existing APIs and patterns already present in the suite: temp-file creation, raw string literal JSON, `try/finally` cleanup, and `MacroExtractor.Extract`. The `FirstRef` rename is internally consistent in the provided file.

On scope creep: I do not see scope creep in the C# fixes. The macro test is narrowly targeted at the missing extractor behavior. The helper rename is a local clarity fix. The only scope problem remaining is in the doc, where two proprietary form-derived filenames are still spelled out.

On the specific single-quoted XML question: that approach is sound for the described parser pipeline. XML permits both single-quoted and double-quoted attribute values, so:

```xml
<Action Name='Beep' />
```

is valid XML. If `MacroXmlParser` unescapes SaveAsText-style text and then hands the result to `XDocument.Parse`, `XDocument.Parse` should accept those attributes without issue. Using single quotes inside the JSON string is a reasonable way to avoid extra escaping noise while still exercising the intended extraction path.

{"verdict":"ISSUES_FOUND","issues":[{"severity":"Major","location":"docs\\session-sets\\005-northwind-embedded-residue-remediation\\test-triage.md","summary":"The triage document still names proprietary structural identifiers via `FrmCompanyDetailTranslationProbe.cs` and `FrmCompanyDetailTranslationProbeTests.cs` in the delete-project section.","fix":"Redact those remaining form-derived filenames to object-kind/category placeholders (for example `frm*-translation-probe source` and `frm*-translation-probe tests`) so the document contains no proprietary structural identifiers beyond the allowed brand token and synthetic HHC names."}]}