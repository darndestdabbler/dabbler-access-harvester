# Verification Round 1

```json
{
  "verdict": "pass_with_issues",
  "summary": "Implementation matches the Phase 3.7 shape: CrossReferenceIndex performs a single pass over ResolvedModule.References, keys declarations by reference identity, mirrors ResolvedModule.TryGetDeclaration outcome handling, and exposes GetReferencesTo() with immutable empty-list fallback. One important test does not actually exercise the with-flattening branch it claims to verify.",
  "issues": [
    {
      "severity": "medium",
      "issue": "The with-flattening test does not create a ResolvedViaWithFlattening outcome, so the recursive unwrap path in CrossReferenceIndex.DeclarationFromOutcome is not truly verified.",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/CrossReference/CrossReferenceIndexTests.cs :: Build_WithFlattening_IndexesUnderInnerDeclaration",
      "fix": "Replace the current parser-driven example with either: (a) a hand-built ResolvedModule containing a ResolvedReference whose Outcome is ResolutionOutcome.ResolvedViaWithFlattening(new ..., new ResolutionOutcome.Resolved(targetDecl)), then assert GetReferencesTo(targetDecl) returns that reference; or (b) a real implicit-With source that produces ResolvedViaWithFlattening and assert at least one returned reference has Outcome of that variant."
    }
  ],
  "confidence": "high"
}
```

---

# Verification Round 2

- **Issue** → Round-1 finding: `Build_WithFlattening_IndexesUnderInnerDeclaration` did not actually produce a `ResolvedViaWithFlattening` outcome.  
  **Location** → `CrossReferenceIndexTests.Build_WithFlattening_IndexesUnderInnerDeclaration`  
  **Fix** → **Resolved.** The test now explicitly constructs a `ResolvedReference` with `Outcome = new ResolutionOutcome.ResolvedViaWithFlattening(..., InnerOutcome = new ResolutionOutcome.Resolved(targetDecl))`, builds a `ResolvedModule` from that synthetic reference, and asserts:
  - `CrossReferenceIndex.Build(rm)` indexes the reference under `targetDecl`
  - the exact synthetic reference instance is returned
  - the returned reference still has the outer `ResolvedViaWithFlattening` outcome variant

This directly exercises the unwrap/indexing path that the prior version failed to cover.
