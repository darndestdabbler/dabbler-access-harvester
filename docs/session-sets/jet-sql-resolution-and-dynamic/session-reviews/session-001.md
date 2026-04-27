# Verification Round 1

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Sql.Parser/FormReferences/FormReferenceResolver.cs",
      "summary": "The resolver ignores `Bang` vs `Dot` operator semantics, so `!` control references can be misresolved as type-library members and direct dot-member failures at segment 1 are incorrectly reported as `control-not-on-form`/`control-not-on-report`.",
      "fix": "Make resolution operator-sensitive: keep segment 0 as collection member lookup, but when resolving later segments use `node.Operators[i]` so `Bang` on a form/report goes through `TryResolveControlOnForm`/`TryResolveControlOnReport`, `Dot` goes through `TryResolveMemberOf`, and segment-1 failures reached via `Dot` emit `member-not-found-at-segment-1` instead of the control-missing reasons."
    }
  ]
}
```

---

# Verification Round 2

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
