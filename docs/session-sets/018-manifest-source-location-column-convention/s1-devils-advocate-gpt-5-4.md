# Set 018 S1 — devil's-advocate review

I reviewed the shipped tree directly. I do not see a remaining schema-gated `SourceLocation` leak in the manifest path: `ParserOutputProjector.BuildLocation` and the synthetic top-level root are the only manifest `StartColumn`/`EndColumn` writers I could find, the `+1` on both ends preserves half-open width, and the embedded-schema wiring still points straight at `docs/test-cases/harvester-schema.v1.1.json`.

### Major finding — DW-27 / consumer guidance overstates which index producers are already 1-based

The new DW-27 guidance says `unresolved-items.json` mixes 1-based navigation-graph / function-classification facts with 0-based resolver / SQL-form-reference facts (`docs/planning/deferred-work.md:1004-1028`, `docs/harvester/interpretation-guide.md:1523-1532`, `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs:228-236`). The shipped code does not support that claim.

`FunctionClassificationBuilder` still records unresolved-reference columns as raw `expr.Provenance.Column` (`AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs:385-389`), and `NavigationGraphBuilder.LocationOf` still returns `range.Start.Column` / `p.Column` verbatim (`AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs:642-650`). Those provenance values come straight from ANTLR `token.Column` / `ctx.Start.Column` (`AccessHarvester.Vba.Parser/Ast/AstBuilder.cs:1215-1226`), and the parser's own location type documents columns as 0-based (`AccessHarvester.Vba.Parser/Ast/SourceLocation.cs:3`). The typed-record comments also still claim 1-based for these same fields (`AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs:408-409`, `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs:267-269`, `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs:389-391`).

That means the current consumer guidance understates the mixed-base surface. At minimum, function-classification unresolved items in `unresolved-items.json` are also 0-based today, so a consumer following the new guide will apply the wrong offset. The good news is that I do not see a dedup regression here: the body-only `FunctionClassification` feed and the additive procedure-scoped feed both key off the same raw provenance, exactly as `UnresolvedItemAggregator` says (`AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs:144-160`), so this is a contract/documentation bug, not an index-collapse bug.

Fix: either (a) correct the docs/comments to say these provenance-backed index producers are still raw/0-based today, or (b) if the intended post-018 contract is truly 1-based for them, do that as a real DW-27 producer change and re-audit dedup plus `unresolved-items.json` golden churn. What should not stand is the current half-state where the code is raw but the guide says otherwise.

### Everything else I checked holds

- I did not find another schema-gated manifest `SourceLocation` emitter beyond `BuildLocation` and the synthetic module root. `BuildLocation` is still the central path for inline references, calls, and resolved-callee locations (`AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs:397-449`).
- The `+1` on both `StartColumn` and exclusive `EndColumn` is the correct width-preserving rebase for the AST's half-open `[Start, End)` spans (`AccessHarvester.Vba.Parser/Ast/SourceRange.cs:3-5`, `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs:430-449`).
- The schema `description` edits are non-validating annotations only, and I do not see a separate committed schema copy to drift: the CLI embeds `..\docs\test-cases\harvester-schema.v1.1.json` directly (`AccessHarvester.Cli/AccessHarvester.Cli.csproj:52-53`), and `install.md` describes the same build-time bundling (`docs/harvester/install.md:349-351`).

## Overall verdict

ISSUES_FOUND.

The manifest `SourceLocation` rebasing itself looks sound. The one thing I would not sign off on as written is the new DW-27 / interpretation-guide story about which unresolved-item producers are already 1-based.

```json
{
  "issues": [
    {
      "severity": "Major",
      "location": "docs/harvester/interpretation-guide.md; docs/planning/deferred-work.md; AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs; AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs; AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs",
      "summary": "DW-27's new guidance says navigation-graph and function-classification unresolved columns are already 1-based, but the shipped producers still pass raw ANTLR/Provenance columns through unchanged, so the consumer-facing mixed-base contract is misstated.",
      "fix": "Either update the docs/comments to say these producers are still raw/0-based today, or normalize those producers as an actual DW-27 change and re-audit dedup plus unresolved-items golden churn. Do not leave the current docs/code mismatch in place."
    }
  ]
}
```
