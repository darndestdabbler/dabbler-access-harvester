# Set 017 S1 - devil's-advocate review

### A1 - UPHELD

I grepped the tracked set docs and the deferred-work entry for the specific leak surfaces the prompt called out. The committed surfaces I could inspect contain placeholders, not a live credential or machine path: `s1-reground-evidence.md` uses `PWD=<redacted>` and `<absolute-local-path>` (`docs/session-sets/017-studentict-local-invariant-corpus/s1-reground-evidence.md:31`), and DW-24 does the same (`docs/planning/deferred-work.md:851`). `activity-log.json` also records that the real value/path were redacted before any commit (`docs/session-sets/017-studentict-local-invariant-corpus/activity-log.json:101`).

The current set folder does not contain `change-log.md` or `disposition.json`, so there was nothing else in-tree to inspect for this item. On the current tree I could not reproduce a tracked credential or absolute BE-path leak.

### A2 - UPHELD

I could not make a second independent schema failure class stick from the shipped tree. The one hard public-contract failure the test encodes is still `SourceLocation.StartColumn >= 1`; the rest of the cited validator messages fit the known `oneOf` losing-branch pattern, and the same object-valued shapes validate on Northwind via the same validator path.

I do think the DW-26 write-up overstates the root cause location; see A3. But on the evidence available in-tree, I did not find a second non-`StartColumn` schema defect that would make the known-limitation pin dishonest.

### A3 - CHALLENGED

The set's root-cause story is too strong. The current codebase explicitly says the VBA parser's `SourceLocation` column is 0-based (`AccessHarvester.Vba.Parser/Ast/SourceLocation.cs:3`). `AstBuilder` stores `token.Column` / `ctx.Start.Column` straight into `SourceLocation` and `SourceProvenance` (`AccessHarvester.Vba.Parser/Ast/AstBuilder.cs:1215-1226`), and `ParserOutputProjector.BuildLocation` writes `StartColumn = range.Start.Column` directly into the manifest (`AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs:431`). At the same time, the public schema requires `minimum: 1` for `StartColumn` (`docs/test-cases/harvester-schema.v1.1.json:249`), and the same projector hardcodes the top-level source location to `StartColumn = 1` (`AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs:115`).

So the public contract may still be 1-based, but the tree absolutely does contain a documented 0-based internal column model. That means DW-26's narrative mislocates the bug. The exposed failure is not "StudentICT alone found a special line-leading off-by-one"; it is "an internal 0-based location model is being projected into a 1-based public schema without normalization, and StudentICT is the first corpus that makes that mismatch schema-visible at column 0." I would keep the public-contract test pin, but I would rewrite DW-26 and the test commentary to describe that internal/public convention mismatch and point the eventual fix at manifest projection normalization, unless the project deliberately changes the schema and all expected fixtures to 0-based.

### A4 - UPHELD

The current `run` path really does leave `IsLinked` unusable and makes `Connect + SourceTableName` the only robust linked-table signal. `TableMetadata` still tells consumers to use `IsLinked` (`AccessHarvester.Access/TableMetadata.cs:67-78`), and `TableMetadataExtractor` derives it from `Attributes` (`AccessHarvester.Access/TableMetadataExtractor.cs:67-80`), but `Pipeline.BuildTableExports` does not call that extractor; it just deserializes `ir/Tables/*.ir.json` into `TableMetadata` (`AccessHarvester.Core/Pipeline.cs:1166-1173`). The raw exporter writes top-level `Attributes`, `SourceTableName`, and `Connect` (`AccessHarvester.Core/Extraction/TableExporter.cs:134,138-139`), so `Connect + SourceTableName` is the signal that actually survives on the `run` path today.

I did not find a second run-path population of `IsLinked`, and I did not find evidence in-tree that native local user tables carry non-empty `Connect`/`SourceTableName`. On the shipped tree, DW-25's guidance is sound.

### A5 - UPHELD

I do not see a no-op assertion in the shipped test. The schema pin is three-part: `Assert.False(schemaResult.IsValid)` (`tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs:119`), `Assert.Contains(... StartColumn ... [minimum])` (`tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs:128`), and the structural `HasZeroStartColumn` walk (`tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs:133`, `247-270`). The ribbon checks will fail if the producer drops entry points or resolves them outside `modRibbonCallbacks` (`tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs:220-222`).

`Assert.Empty(manifest.Relationships)` would be too weak by itself, but paired with the linked-table assertion it is specifically checking the split-FE surface, not just "a database that happens to have no relationships." I could not make the false-green hypothesis stick.

### A6 - UPHELD

The test file itself now makes the canary/invariant split honestly. The exact ribbon name/id/module checks are explicitly labeled as corpus-specific canaries, while the structural assertions are presence/shape checks. There are still observational facts in comments (`9 today`, `3000+ today`), but they are commentary, not asserted contract.

I did not find remaining exact-count assertions masquerading as corpus-independent invariants in the shipped test.

### A7 - CHALLENGED

The shipped outcome is not spec-faithful on the current tree. The set spec still requires Session 1 to assert that the StudentICT manifest `schema-validates` (`docs/session-sets/017-studentict-local-invariant-corpus/spec.md:148`) and separately says `Do not weaken assertions or tests to force green` (`docs/session-sets/017-studentict-local-invariant-corpus/spec.md:124`, `196`). The shipped test now asserts the opposite (`tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs:119`) and justifies it in code comments and the routed-verifier note, but the set spec itself was not amended to describe the known-limitation pin.

The linked-table count correction from 13 linked to 9 linked is handled honestly in `s1-reground-evidence.md` (`docs/session-sets/017-studentict-local-invariant-corpus/s1-reground-evidence.md:20`). The schema-validity invariant is the part that drifted. If you want the known-limitation inversion to be the accepted outcome, the spec needs to say so explicitly; otherwise this is a spec/stop-rule miss, not just a documentation preference.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "item": "A3",
      "location": "AccessHarvester.Vba.Parser/Ast/SourceLocation.cs; AccessHarvester.Vba.Parser/Ast/AstBuilder.cs; AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs",
      "summary": "DW-26's write-up/test commentary treats StartColumn==0 as a StudentICT-specific producer off-by-one, but the shipped code explicitly uses an internal 0-based column model and projects it directly into the manifest without normalization.",
      "fix": "Keep the public-contract pin, but rewrite DW-26/test commentary to describe the internal/public convention mismatch and target the eventual fix at normalizing columns at manifest projection time, unless the project deliberately migrates the schema and expected fixtures to 0-based."
    },
    {
      "severity": "Major",
      "item": "A7",
      "location": "docs/session-sets/017-studentict-local-invariant-corpus/spec.md; tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs",
      "summary": "The authored spec still requires the StudentICT manifest to schema-validate and forbids weakening assertions, but the shipped test inverts that invariant to Assert.False(IsValid) without any in-tree spec amendment reconciling the deviation.",
      "fix": "Either update the set spec and closeout artifacts to explicitly authorize the DW-26 known-limitation pin as the accepted Session 1 outcome, or restore a schema-validating invariant and treat the current invalidity as an open blocker instead of silent spec drift."
    }
  ]
}
```

Pass 2 missed one thing on the first pass: the current set folder still has no `change-log.md` or `disposition.json`, so I could not inspect those leak/spec surfaces the prompt names. That is benign for A1 because the live leak surfaces I could inspect are redacted, but it reinforces that A7 is not fully reconciled on the current tree.