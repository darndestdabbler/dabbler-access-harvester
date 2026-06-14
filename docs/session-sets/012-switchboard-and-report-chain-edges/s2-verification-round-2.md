1. **Major 1 — object-level embedded macros / raw `*EmMacro` silent drop**

   **Verified resolved.**  
   The fix is present end-to-end:

   - `IrFormOrReport.EventMacros` exists as an object-level surface.
   - `IrLoader` now populates it from the root node.
   - `CollectEmbeddedMacroEdges` scans both object-level and control-level macros.
   - `FlagUnparsedEmbeddedMacros` now turns uncovered raw `<Event>EmMacro` properties into explicit `UnresolvedChainTarget` entries.

   The object-level path is implemented correctly: object-level edges use `From = { Kind = Form|Report, Name = obj.Name, Member = null }`, which matches the intended model. The fallback unresolved path also correctly suppresses itself when the structured `EventMacros` already cover that event.

   On your specific question: **using `Kind = OpensForm` as a representative kind for `embedded-macro-unparsed` is acceptable in the current model**, because the real signal is carried by `Reason = embedded-macro-unparsed` plus `Provenance = embedded-macro`. It is not semantically exact, but with no neutral “unknown-open-kind” enum member available, this is a reasonable placeholder rather than a defect. If the schema ever grows a neutral kind, that would be preferable.

2. **Major 2 — TempVars regex not token-aware**

   **Not fully resolved.**  
   The fix does address the two cases explicitly called out in R1:

   - the left boundary prevents substring matches like `XTempVars!y`;
   - blanking string literals prevents `"TempVars!x"` or `'TempVars!x'` from fabricating edges.

   However, the extractor is still **not fully token-aware**. It will still match inside **bracketed identifiers**, e.g. SQL such as:

   ```sql
   SELECT [TempVars!fake] FROM tblClients;
   ```

   or

   ```sql
   SELECT 1 AS [TempVars!fake] FROM tblClients;
   ```

   In those cases the regex sees `TempVars!fake` preceded by `[` (which passes the negative lookbehind) and will incorrectly emit a `tempvar-flow` edge even though this is just an identifier, not a TempVars reference. That means the original class of false-positive dependency is still possible.

3. **Major 3 — recovered-but-empty inline SQL parse silently lost record-source edges**

   **Verified resolved.**  
   The previous flaw was: “no exception” was treated as success even when the recovered AST yielded no usable source tables. That silent loss is now gone. The builder emits `record-source-unparsed` whenever inline SQL yields zero recovered `FROM`/`JOIN` sources, regardless of whether parsing threw.

   Mechanically, that is exactly what the code now does:

   - parse/build/walk;
   - emit any recovered `TableSources`;
   - if `collector.TableSources.Count == 0`, emit unresolved.

   For this project’s “explicit-unresolved over silent-drop” rule, I consider **zero recovered table sources** an acceptable success criterion. It is stricter than parser diagnostics and closes the original silent-loss hole. It does conflate “parser failed” with “inline SQL had no recoverable base source”, but that is a modeling trade-off, not a regression in the stated chain-building goal.

4. **Minor 1 — sort comparators not total**

   **Verified resolved.**  
   `CompareEndpoint` now does:

   - `OrdinalIgnoreCase` on `Name`,
   - then `Ordinal` on `Name`,
   - then the same pair on `Member`.

   That makes the ordering total even for case-only differences, which removes the instability risk from `List.Sort`. `SortEdges` and `SortUnresolved` therefore now have deterministic tiebreaks.

5. **Minor 2 — `ParseFormRefPath` fabricated `"?"` endpoints for blank/malformed paths**

   **Not fully resolved.**  
   The fix removes the explicit `"?"` injection, and the newly added unresolved path is correct for some malformed inputs such as `[Forms]`. But the implementation still has a positional bug:

   ```csharp
   var segments = path.Split('!')
       .Select(...)
       .Where(s => s.Length > 0)
       .ToList();
   ```

   Because empty segments are discarded **before** validation, malformed paths can collapse into apparently valid ones. For example:

   - `[Forms]!![txtId]`
   - `![frmX]![txtId]`

   can shift segment positions and still return a non-null object/member pair, producing a fabricated supplier edge instead of `unresolvable-reference`.

   So the original problem class—fabricating endpoints from malformed paths—is reduced, but not eliminated.

**Net:** Findings 1, 3, and 4 are genuinely resolved. Findings 2 and 5 are only partially resolved. I did not see a separate additional regression beyond those two remaining issues.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
      "summary": "The TempVars extractor still matches inside bracketed identifiers such as `[TempVars!fake]`, so the token-awareness fix is incomplete and can still fabricate `tempvar-flow` edges.",
      "fix": "Scrub bracketed identifiers (and ideally comments) before regex scanning, or replace the regex with a small lexer that only recognizes standalone `TempVars!name` bang expressions in expression context."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
      "summary": "ParseFormRefPath removes empty `!` segments before validation, so malformed paths like `[Forms]!![txtId]` can still collapse into a fabricated supplier endpoint instead of `unresolvable-reference`.",
      "fix": "Preserve positional segments, validate the root/object/member slots explicitly, and return `(null, null)` unless segment 0 is `Forms` or `Reports` and segment 1 is a non-empty object name."
    }
  ]
}
```