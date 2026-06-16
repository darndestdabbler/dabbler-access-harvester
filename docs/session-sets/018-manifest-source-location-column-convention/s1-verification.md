# S1 Cross-Provider Verification (routed gpt-5-4, session-verification)

**Date:** 2026-06-15  **Round:** 1

---

1. On the `+1` applied to both `StartColumn` and `EndColumn` under half-open, exclusive-end semantics:

This part is correct.

If the internal `SourceRange` is half-open `[Start, End)`, then rebasing from 0-based columns to 1-based columns requires adding 1 to both endpoints, not just the start. The width identity is preserved algebraically:

- old width = `End.Column - Start.Column`
- new width = `(End.Column + 1) - (Start.Column + 1)`
- therefore new width = old width

So the stated example is right:

- internal: `[0,11)` for an 11-character token
- projected: `[1,12)` in 1-based coordinates
- width remains `11`

That also means keeping `EndColumn` exclusive is the right choice. Converting to inclusive at the same time would have been a semantic change, not a base normalization, and would have broken width preservation.

As to consumers:

- Any consumer that assumed inclusive end was already semantically wrong relative to the now-documented contract. This change does not introduce that bug; it merely makes the base consistent.
- Any consumer that assumed 0-based manifest columns will now observe a behavior change, but that consumer was depending on a producer defect against the public schema contract. Given the schema already declared `minimum: 1`, fixing the producer is the correct side to move.

So concern 1 comes out clean: `+1` on both ends is the correct normalization for a half-open span.

2. On the golden-delta proof methodology:

For the specific regenerated HHC golden artifact, the methodology is strong and basically sound.

The important parts of the proof are:

- comparison was structured/value-level, not text diff
- all differing leaves were `StartColumn`/`EndColumn`
- counts balanced exactly: 5941 start-column shifts and 5941 end-column shifts
- every shift was exactly `+1`
- no non-column fields changed
- no synthetic top-level `Source.Location` values changed
- the separate unresolved-items artifact remained byte-identical

That is good evidence that, on that corpus, the committed golden changed in exactly the intended way and nothing else moved.

The only limitation is scope: this proves exactness for that artifact, not universal correctness for every possible manifest shape. But that limitation is already covered by the added hermetic line-leading-token test and the StudentICT integration witness. So as a combined validation story, this is solid.

I would not challenge the methodology itself. I would only say it is a proof of “exactly this delta on HHC,” not a proof of “no other possible emitter exists anywhere in the codebase.”

3. On the DW-27 scope decision:

(a) Keeping `unresolved-items.json` out of DW-26 scope was the correct call.

The reason is straightforward: that index is explicitly documented as preserving producer-native column values verbatim, and those producer-native values are mixed-base today. A blanket `+1` in the aggregator/index layer would be wrong because it would double-increment already-1-based producers.

That means a correct index-side fix is not “normalize everything now,” but “normalize selectively using provenance.” That is a different change, with different semantics, and belongs where producer origin is known. Deferring that as DW-27 is appropriate.

So I do not think DW-26 should have normalized the index too. Doing so would likely have been a bad fix.

(b) The re-pin `inline == index + 1` reads as a faithful re-pin, not a force-green weakening.

Why:

- it still asserts an exact arithmetic relationship
- it still asserts exact line equality
- it is tied to a specifically documented asymmetry: manifest inline location now 1-based, index location still raw 0-based for this resolver-sourced path
- the comment explicitly records the reason and the deferred cleanup item

A force-green weakening would have looked like “columns are close,” “columns are non-null,” or removing the correlation entirely. This test still meaningfully constrains behavior.

The only thing it does not do is assert concrete numeric coordinates for that specific fixture. But the old equality-based test did not do that either, so this is not a new weakening.

(c) On whether some other schema-gated manifest surface was missed:

Based on the supplied evidence, I do not see a concrete missed schema-gated `SourceLocation` emitter.

The evidence supporting that is:

- `BuildLocation` was changed centrally
- synthetic top-level location was already hardcoded to 1
- HHC regenerated with every changed leaf being a `StartColumn`/`EndColumn` `+1`
- zero top-level location diffs
- StudentICT now schema-validates, which is the key real-corpus witness for former column-0 leakage
- hermetic line-leading-token tests verify both width preservation and absence of zero columns in a projected tree

So on substance, the scope decision looks right and I do not see a missed public-manifest surface from what you supplied.

4. On possible missed 0-based leaks into a schema-gated `SourceLocation`:

I do not see evidence of an actual remaining leak, but I do see one overclaim in the proof language.

The test `Projection_HasNoZeroColumn_EvenWithLineLeadingToken` says:

> “Proves BuildLocation is the only column-emitting path and that it is normalised.”

That statement is stronger than the test actually proves.

What the test really proves is:

- for this projected fixture,
- no `StartColumn` or `EndColumn` value anywhere in the JSON tree is zero,
- and the synthetic root still starts at column 1.

That is useful, but it does not by itself prove there is no other emitter on an unexercised manifest surface. The broader confidence comes from the HHC value-diff and the StudentICT integration result, not from this single hermetic test alone.

So the implementation appears correct, and I do not see a concrete leak. But the “only column-emitting path” claim is not fully established by that test in isolation.

5. On the schema `description`-only edit:

This cannot change validation behavior, assuming normal JSON Schema semantics.

`description` is an annotation keyword, not an assertion keyword. Adding or changing it does not affect instance validity. Since the actual validation-bearing constraints (`type`, `required`, `minimum`) were not changed, the schema’s acceptance/rejection behavior remains the same.

So concern 5 is clean.

6. On test integrity:

The StudentICT pin flip looks genuine.

It now does two real things:

- asserts schema validity for the whole manifest
- asserts a structural witness that no `StartColumn == 0` remains

That is exactly what a known-limitation pin should become once the defect is fixed. It is stronger than before, not weaker.

The LayerA header-ref re-pin also looks genuine.

It did not merely relax the test; it updated the asserted relation to the new documented semantics:

- lines still must match exactly
- columns now must differ by exactly 1 in the expected direction

That is a real invariant, not a vague one.

My only criticism in this area is the same as under concern 4: one test comment slightly overstates what the hermetic scan proves globally. That is a proof-quality/documentation issue, not a sign that the tests were weakened to pass.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {"severity": "Minor", "location": "tests/AccessHarvester.Pipeline.Tests/Manifest/ParserOutputProjectorTests.cs", "summary": "The `Projection_HasNoZeroColumn_EvenWithLineLeadingToken` comment overstates the proof: that single fixture does not by itself prove `BuildLocation` is the only column-emitting path across all schema-gated manifest surfaces.", "fix": "Reword the comment to match the actual scope of the test, or add a broader repo-wide/emitter-audit test or multi-surface corpus assertion if you want to prove globally that no other schema-gated column emitter exists."}
  ]
}
```
---

## Round 2 — in-IDE devil's-advocate pair (PRIMARY per spec), 2026-06-16

**Reviewers:** gpt-5-4 + gemini-3.1-pro, full repo access (files:
`s1-devils-advocate-gpt-5-4.md`, `s1-devils-advocate-reply-gemini.md`).

**Both UPHELD the core DW-26 work:** no missed schema-gated `SourceLocation` leak
(BuildLocation + the synthetic root are the only column emitters); `+1` on the half-open
`[Start,End)` span is width-preserving for all token sizes (incl. zero-width); the DW-27
boundary is structurally necessary and the aggregator dedup is not broken; the schema
`description` edits are non-validating with no copy-drift.

**One Major finding (both providers), now FIXED — documentation accuracy.** The DW-27
entry, interpretation-guide note, schema `$def` description, and several pre-existing
record comments (`UnresolvedLocation.Column`, `FunctionClassification.Column`,
`NavigationTarget.SourceColumn` ×2) claimed `unresolved-items.json` is *mixed-base*
(nav-graph + function-classification "1-based", resolver/SQL "0-based"). The shipped
producers all pass **raw 0-based** ANTLR/provenance columns, so the index is **uniformly
0-based**. Independently re-verified against `FunctionClassificationBuilder.cs:388-389`,
`NavigationGraphBuilder.cs:648-650`, `AstBuilder.cs:1215`, and the HHC golden
(`Column: 4` on a 4-space-indented line = 0-based). Corrected all docs/comments to
"uniformly 0-based" + a uniform `+1` manifest-vs-index offset; restated DW-27 (the
index normalisation is a uniform `+1` but golden-affecting, deferred). **Code unchanged**
— no producer was 1-based to begin with; the `inline == index + 1` pin is unchanged and
green. Rebuilt Pipeline (0 errors); 28 affected hermetic tests pass.

**Final verdict: VERIFIED.** Both verification rounds' findings were documentation-only;
the column-base normalisation itself was independently upheld by the routed verifier and
both devil's-advocate providers. This was an Evidence-Over-Inference miss (trusted a
pre-existing comment instead of the producer code) → Step 9 lesson candidate.
