I see two actionable defects in the landed Session 2 diff; the rest is mostly correct and the stated HomeHealthCare golden deltas are explained.

1. **F-2-3 BOM removal**
   This fix looks correct. `UsageChecklistWriter` and `CleanupChecklistWriter` now use `new UTF8Encoding(false)` for both JSON outputs each, which covers the four affected files. That matches RFC 8259’s no-BOM requirement for JSON. The added read-back tests are directionally right: they explicitly check for absence of `EF BB BF`, check the first byte is a JSON opener, and the cleanup test also parses the bytes successfully. I do not see a hidden determinism or contract issue here.

2. **F-2-4 Q6 dead code**
   The implementation matches the stated decision and the strategy-doc rule: `targets.Count > 0 && targets.All(KnownQueryNames.Contains)` is the right minimal predicate for “references only other queries.” Wiring the `"references-no-tables"` evidence into the `QueryTableReference` signal also fixes the previous unreachable question path, and suppressing the generic OutOfBand question when Q6 fires is consistent with the existing PossiblyUnused suppression pattern.

   The updated `LauncherSurfaceTests.Query_Consumed_By_Another_Query` is a **legitimate correction**, not a weakening. The old test was asserting the broken behavior created by the dead helper. The new assertion is stricter in the relevant way: it now pins `Unknown` plus `QueryReferencesNoTables`, and still forbids the unrelated OutOfBand question. That is exactly what a dead-code fix should do.

   I do not see evidence of an unexplained HomeHealthCare delta from F-2-4; the supplied delta list saying it is golden-neutral is plausible.

3. **F-2-5 inline-SQL RecordSource table credit**
   This fix is also correct. The new split is sensible:
   - query-only credit stays in `usedAsFormRecordSource` / `usedAsReportRecordSource`
   - broad referenced-ness credit now includes all catalog-resolved `FROM`/`JOIN` targets from inline SQL via `objectCatalog`

   That matches the finding precisely. It also stays conservative by only crediting names that resolve to known catalog objects, so aliases and arbitrary tokens do not gain false credit. The declared HomeHealthCare delta for `tblServiceRateLimits` is fully explained by this change, and the supplied “only one extra table reclassification beyond the crosstab fixes” story is coherent.

4. **F-2-7 numeric/bit injection**
   The security fix itself is correct in spirit: bare numeric/bit interpolation is no longer trusted, invalid tokens become `NULL`, and degradations are deduped once per column. The regex is exactly the one described in the session context.

   **However, there is a real bug in the implementation**: `SqlValueFormatter.Format(...)` almost certainly treats null/blank/whitespace cells as clean `NULL`, but `IsInvalidDataToken(...)` only exempts `null`, not empty or whitespace strings. So for a CSV row like `,` or `"   "` in a nullable numeric/bit column:
   - `Format(...)` will emit `NULL`
   - `IsInvalidDataToken(...)` will return `true`
   - the generator will record a spurious `InvalidDataToken` degradation

   That means normal blank numeric/bit cells can be misreported as invalid data. The SQL output remains safe, but the degradation surface becomes wrong and noisier than intended. This is a real hidden regression because blank nullable numeric fields are common. The tests added here do not cover `""` or whitespace.

5. **F-1-4 missing diagnostic**
   The intended fix is good, but the implementation is incomplete.

   `BuildFormOrReportExports` still has the early:
   `if (!Directory.Exists(irCategoryDir)) return new List<FormExport>();`

   That means if `parserOutputs` is non-empty but the entire `ir/<category>` directory is absent, the method still exits silently and emits **no** `FORM_SPEC_MISSING` diagnostics. But that case is exactly `parserOutputs.Keys minus the ir-file names`, where the ir-file set is empty. So the contract remains broken for the “all IR specs missing” case.

   In other words: the fix works when the IR directory exists but some files are missing; it does **not** work when the IR directory itself is missing. That is a real correctness gap against the stated finding, even if HomeHealthCare does not hit it.

6. **F-1-7 system-object filter drift**
   This looks good. A shared `SystemObjectNames.IsSystemObjectName` using explicit `StringComparison` is the right cleanup. It aligns the exporters with the schema reader, removes culture-sensitive `StartsWith` calls, and the test coverage is appropriate. No determinism concern here.

7. **F-2-6 / O-1-1 / O-1-2 docs**
   No code-side issue visible from the supplied diff. Reconciling the label-source doc to the emitter is the cheaper and safer direction, and the interim notes about `.accdb` name forms and unresolved-items being empty are documentation-only and do not imply contract drift in the code shown.

8. **Encoding regression fix / SaveAsTextDecoder**
   This is the highest-risk part, and my take is:

   - **Static initialization ordering:** correct. In C#, static field initializers run in textual order, so `_providerRegistered = RegisterCodePagesProvider()` will execute before `Encoding.GetEncoding(1252)`. That part is fine.
   - **`ReadLines` vs `File.ReadAllLines` semantics:** effectively fine. `StringReader.ReadLine()` gives the expected CR/LF/CRLF handling and does not invent a trailing empty line for terminal newlines. I do not see a spurious-line bug there.
   - **Heuristic soundness:** better than the fixed-cp1252 regression, but not mathematically sound for all no-BOM inputs.

   Specifically, `BOM -> strict UTF-8 -> cp1252 fallback` will correctly fix both observed corpus failures:
   - cp1252 `0x97` now falls back and becomes `—`
   - UTF-8 `C2 A7` without BOM now stays `§` instead of mojibaking to `Â§`

   But a genuinely cp1252 file can still be mis-decoded if its raw bytes happen to form valid UTF-8. Example: bytes `C3 A9` mean `Ã©` in cp1252, but `é` in UTF-8. Your current decoder will choose UTF-8 there. So the heuristic is not universally “sound”; it is an unavoidable best-effort unless the source/export step can give you encoding metadata. I would **not** block Session 2 on that ambiguity alone, because the source bytes are inherently ambiguous without a BOM or side-channel. But I would tone down the code comment’s certainty: it does *not* “never decode any file worse” than fixed cp1252.

On the golden deltas: I do **not** see an unexplained delta in the set you listed. The crosstab flips, `PassingMechanism` changes, em-dash repairs, `tblServiceRateLimits` reclassification, and BOM removals all map cleanly to named fixes. `basResetFixture` staying unchanged is exactly what the new decoder should preserve.

On determinism / lean-by-default: the new code is still deterministic overall. Sorting in the new diagnostics path is good. The JSON BOM change is output-stable. The two regressions I called out are not nondeterminism issues; they are correctness gaps.

Bottom line: most of Session 2 is solid, but I cannot mark it VERIFIED because of the incomplete F-1-4 fix and the false-positive `InvalidDataToken` behavior.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Migration/SqlValueFormatter.cs",
      "summary": "IsInvalidDataToken flags empty or whitespace numeric/bit cells as invalid even though Format treats those cells as clean NULLs, causing false InvalidDataToken degradations on ordinary nullable columns.",
      "fix": "Make IsInvalidDataToken return false for string.IsNullOrWhiteSpace(raw), and add unit/end-to-end tests covering blank and whitespace numeric/bit CSV cells so generator degradations stay aligned with Format."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Core/Pipeline.cs",
      "summary": "BuildFormOrReportExports still emits no FORM_SPEC_MISSING diagnostics when parserOutputs is non-empty but the entire ir/<category> directory is absent, because it returns before diffing against an empty IR set.",
      "fix": "Do not early-return before diagnostic generation; treat a missing IR directory as zero IR files and emit sorted FORM_SPEC_MISSING warnings for all parserOutputs keys, with a test for the missing-directory/nonempty-parserOutputs case."
    }
  ]
}
```