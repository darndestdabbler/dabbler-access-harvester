# Verification Round 1

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs (VisitForEachStatement)",
      "summary": "The resolver never visits the For Each enumerable/collection expression, so references in the `In ...` clause are silently skipped.",
      "fix": "Visit the collection expression before pushing the `ForEachScope`, then walk the loop body inside the pushed scope."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs (DecideMemberOutcome)",
      "summary": "Member-access dispatch assumes the last emitted reference is the target node itself, which fails for non-emitting wrapper targets such as `IndexOrCallExpression` and misclassifies chains.",
      "fix": "Track outcomes by expression subtree (or return them from visit helpers) and resolve member-access targets from that map instead of relying on `_collector[^1]` plus `ReferenceEquals`."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Vba.Resolver/References/ResolutionOutcome.cs, AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs, tests/AccessHarvester.Vba.Resolver.Tests/References/ReferenceResolverTests.cs",
      "summary": "Access object-model hits are collapsed into `ResolutionOutcome.Resolved(Declaration)` instead of the documented object-model-specific outcome, which diverges from the Session-1 synthesis contract and hides registry resolutions from downstream consumers.",
      "fix": "Add the documented object-model outcome variant and emit it for registry-backed resolutions, or explicitly update the synthesis/stability contract and tests before later sessions build on the current shape."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Vba.Resolver/References/ResolutionOutcome.cs",
      "summary": "The discriminated union is not actually closed inside the assembly because `ResolutionOutcome` uses a `private protected` constructor, allowing new same-assembly variants despite comments claiming a closed set.",
      "fix": "Make the base constructor `private` and keep all valid variants nested inside `ResolutionOutcome` so no other type in the assembly can subclass it."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/References/ReferenceResolverTests.cs",
      "summary": "Test coverage misses the top-level Access-registry fallback and the For Each collection-expression path, so the documented scope→registry→built-in ordering and the skipped For Each branch are not guarded.",
      "fix": "Add tests for a bare identifier resolved via `TryResolveTopLevelMember`, for registry precedence over built-ins, and for `For Each x In someCollection` asserting `someCollection` is emitted and resolved."
    }
  ]
}
```

---

# Verification Round 2

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs (DecideMemberOutcome and non-emitting wrapper expression handling)",
      "summary": "_outcomesByExpression is only populated for emitted reference-site nodes, so member access on transparent wrappers such as a parenthesized target loses the wrapped target outcome and can miss both object-model resolution and the correct unresolved reason.",
      "fix": "When deciding a member outcome, recursively unwrap transparent expression wrappers (at minimum parenthesized nodes) or record the wrapped target's outcome onto those wrapper nodes after visit so dispatch sees the underlying target outcome."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/References/ReferenceResolverTests.cs",
      "summary": "The Round-1 coverage gap is only partially closed because the new top-level registry test does not exercise the documented registry-before-built-in ordering on a colliding name.",
      "fix": "Add a test where the stub registry returns a declaration for a known built-in name such as Format and assert that ResolveIdentifier returns ResolvedToObjectModel rather than ResolvedToBuiltIn."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/resolver-references-and-with-blocks/synthesis/session-1-resolution-algorithm.md (§9 Test plan)",
      "summary": "The synthesis doc still mentions Unresolved(\"for-loop-variable-implicit-deferred\"), which no longer exists in UnresolvedReasons and contradicts the current loop-variable behavior described earlier in the same document.",
      "fix": "Update the stale test-plan bullet to match the implemented Session-1 behavior (not-in-scope) or remove that obsolete reason entirely."
    }
  ]
}
```

---

# Verification Round 3

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
