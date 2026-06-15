# Devil's-Advocate Review - Set 017 - Gemini 3.1 Pro (Preview)

This is an adversarial evaluation of Set 017 (StudentICT local-invariant corpus onboarding), evaluating the contrarian hypotheses presented.

## A1 — Credential / machine-path leak into git
**UPHELD.** The contrarian hypothesis failed. An exhaustive search of the committed diff, `s1-reground-evidence.md`, `deferred-work.md`, `interpretation-guide.md`, and the test file confirms that every occurrence of the back-end path and password is appropriately redacted (using placeholders like `PWD=<redacted>` and `<absolute-local-path>`). The test deliberately asserts only `BackEndFileName` and never the full connection string. No real credentials or absolute machine paths were leaked into tracked files.

## A2 — The DW-26 known-limitation pin is dishonest or wrong
**UPHELD.** The contrarian hypothesis failed. The other thousands of errors reported by the schema validator (`ResolvedTarget should be "null"`, `ItemKind Expected "Query"/"Expression"`, etc.) are indeed losing `oneOf` branch noise. This is standard `NJsonSchema` behavior when an object inside a `oneOf` branch fails validation (in this case, due to `StartColumn`); it attempts the `null` branch, fails there too, and emits validation errors for both. Since NorthwindDevEdition validates perfectly with the exact same object-valued properties, the `StartColumn: 0` violation is genuinely the sole independent schema failure. The DW-26 known-limitation pin accurately captures this reality.

## A3 — `StartColumn==0` is correct and the SCHEMA is the bug
**CHALLENGED.** The contrarian hypothesis is correct. The orchestrator's claim that the harvester's column contract is 1-based is demonstrably false at the parser boundary. Inside `AccessHarvester.Vba.Parser/Ast/SourceLocation.cs`, the doc-comment explicitly states: `// File + line + column triple. Column is 0-based (ANTLR convention)`. The parser is legitimately 0-based. The only reason the HomeHealthCare golden has `StartColumn` minimum of 1 is because its codebase is indented (its line-leading tokens happen to start at column 1 or later). DW-26 frames this as a "0-based leak" requiring a "fix" in the VBA reference/call emission path, but the parser's internal columns are accurately following the 0-based ANTLR convention. It is the downstream JSON schema's `minimum: 1` requirement (or the manifest pipeline's failure to translate 0-based AST columns to 1-based schema columns) that is the defect.

## A4 — The linked-table detection heuristic is wrong (DW-25)
**UPHELD.** The contrarian hypothesis failed. In Microsoft Access, native local tables legitimately carry an empty string (`""`) for `Connect` and no `SourceTableName`. System tables (like `MSysObjects`) also have an empty `Connect` string. Additionally, the orchestrator accurately states that the `run` pipeline (via `Pipeline.BuildTableExports`) bypasses `TableMetadataExtractor.Extract` entirely, deserializing `ir.json` files straight into `TableMetadata`. Because the raw DAO Attributes bitmask is a top-level property and `IsLinked` is merely a `{ get; init; }` with no `[JsonExtensionData]` hook, it is dropped on deserialization. Exploring all assignments to `IsLinked` confirms it is never populated outside `TableMetadataExtractor`. Therefore, checking `Connect` + `SourceTableName` is indeed the robust and only reliable signal for linked tables in the manifest output.

## A5 — The test passes for the wrong reason (false green)
**UPHELD.** The contrarian hypothesis failed. The `HasZeroStartColumn` structural assertion correctly searches for exactly `StartColumn: 0`. Since ANTLR columns are 0-based, any `StartColumn: 0` is indeed a line-leading token and faithfully represents the DW-26 trigger. The capability-level ribbon checks use `Assert.Single` and `Assert.Equal("modRibbonCallbacks", ...)`, which would absolutely hard-fail if the ribbon was missing or mapped to a different module. Regarding `Assert.Empty(manifest.Relationships)`, while it is a negative assertion that would pass if the relationships simply vanished pipeline-wide, such a regression would trigger red in the `NorthwindLocalInvariantTests` (which strictly asserts 5 relationships). Therefore, the test securely tests its segment of the pipeline without leaving uncaught false greens.

## A6 — Corpus-independence labeling and staleness
**CHALLENGED.** The contrarian hypothesis holds; the test conflates a corpus-specific canary with a capability-level invariant. Specifically, around line 177, the test does:
```csharp
// CAPABILITY-LEVEL invariants (corpus-independent): the root onLoad and
// the button onActions are surfaced AND resolve...
var onLoad = Assert.Single(ribbonEntryPoints!, ep => ep.Kind == RibbonCallbackKind.OnLoad);
```
Asserting `Assert.Single` directly pins the exact count of `onLoad` callbacks (exactly 1). While a ribbon legitimately resolving its callbacks is a capability invariant, the hardcoded fact that this particular ribbon defines *exactly one* `onLoad` callback in its XML is a corpus-structure canary. This is mislabeled. A benign FE edit adding a second ribbon with an `onLoad`, or removing the current one, would fail this test despite the producer working perfectly.

## A7 — Scope / spec-fidelity
**UPHELD.** The contrarian hypothesis failed. The session strictly adhered to its Stop Rules and spec. By modifying the test to assert `Assert.False(schemaResult.IsValid)` and strictly pinpoint the exact error `Errors.Contains("StartColumn") && e.Contains("[minimum]")`, the orchestrator built a mechanical backstop that will intentionally fail (flip to red) once the defect is fixed. This adheres to the rule: "a failing real-corpus invariant is a finding, not an assertion to soften." Softening would entail bypassing the assertion or hiding the error, but this approach accurately captures the defect in amber without muddying the scope. The table discrepancy count (13 linked vs 9 linked) was also openly updated and transparently documented.

---

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "item": "A3",
      "location": "docs/planning/deferred-work.md (DW-26 definition)",
      "summary": "DW-26 wrongly blames the parser for a '0-based leak', but AccessHarvester.Vba.Parser/Ast/SourceLocation.cs proves the AST natively and intentionally uses 0-based columns (ANTLR convention). The downstream schema's 'minimum: 1' or lack of a projection offset is the real defect.",
      "fix": "Reframe DW-26: The fix is either to relax the JSON schema to minimum 0 or have the AST-to-manifest projector automatically add 1 to StartColumn/EndColumn."
    },
    {
      "severity": "Minor",
      "item": "A6",
      "location": "tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs",
      "summary": "A corpus-specific assertion enforcing exactly 1 onLoad callback (Assert.Single) is mislabeled under the CAPABILITY-LEVEL invariants block.",
      "fix": "Move the exact-count 'Assert.Single' assertion into the CORPUS-SPECIFIC CANARIES section, and restrict the capability-level block to generic resolution success checks."
    }
  ]
}
```

**One thing pass 1 missed (Pass 2 Finding):**
In evaluating the DW-18 invariant check in the test (`Invariant 7`), Pass 1 missed that the assertion `Assert.Contains("\"FunctionClassification\"", unresolvedJson)` uses a brute-force substring check against the raw JSON string instead of deserializing the file and validating properties cleanly. While benign and functional for ensuring the classification exists in the output, it is highly brittle to JSON serialization formatting (such as spacing or quotes).
