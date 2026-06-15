Resolution 1 / A3: This is now materially correct. The rewritten DW-26 framing locates the defect at the right seam: AST/source locations are intentionally 0-based internally, the projector passes them through raw, while the public schema currently enforces `StartColumn >= 1` and the projector also emits at least one synthetic location as `1`. That is a convention mismatch at projection/public contract, not a generic “line-leading off-by-one.” The current pin is still honest: `IsValid == false` + a `StartColumn` `[minimum]` error + structural evidence of `StartColumn == 0` accurately captures the present contract failure and will flip regardless of whether the eventual fix is “normalize projection to 1-based” or “change the public contract/schema to allow 0-based.” So A3 itself is resolved.

Resolution 2 / A7: As described, this adequately reconciles the spec/test mismatch. The important part was not merely commenting the test, but making the exception explicit in-tree in `spec.md`. An execution amendment that specifically authorizes the DW-26 pin as the accepted Session 1 outcome, and ties it back to the Stop Rule, removes the “silent drift” problem. The inline annotation on the Step-2 invariant line is also the right belt-and-suspenders touch. On that basis, A7 is resolved.

Resolution 3 / A6: The capability-vs-canary split is now mostly correct. Moving `Assert.Single(onLoads)` out of the capability block and leaving capability at “at least one of each kind, all resolved, resolved entries have non-empty module” is the right change. That makes the capability assertion robust and keeps exact corpus shape in the canary section. So the original A6 concern is resolved.

New issue introduced: there is now a small prose/assertion mismatch in the corpus-specific canary block. The comment says the buttons “all share one dispatch callback (`OnActionButton`),” but the code does not assert that at all; it asserts only `Assert.Single(onLoads)` and that every resolved module is `modRibbonCallbacks`. So the canary section currently overstates what it pins. This is minor, but real: either add the callback-name assertion or trim the prose to match the actual checks.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "StudentIctLocalInvariantTests.cs - CORPUS-SPECIFIC CANARIES block under Invariant 6",
      "summary": "The canary comment claims all button callbacks share the dispatch callback \"OnActionButton\", but the test does not assert that; it only asserts a single onLoad and module name \"modRibbonCallbacks\".",
      "fix": "Either add an explicit canary assertion for the onAction callback name(s), e.g. asserting all onActions have CallbackName == \"OnActionButton\" (or equivalent resolved procedure name), or revise the comment so it no longer claims that callback-name pin exists."
    }
  ]
}
```