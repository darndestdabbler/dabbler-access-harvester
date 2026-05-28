Based on the fixes and the evidence provided, I do not see a remaining defect in the three areas you asked about. The `IsSubquery` change is correct if the intended contract is omission when false; the `ParameterOrigin.Unknown -> UserPrompt` mapping is semantically lossy but acceptable as a documented schema-constrained fallback; and `PredicateNode` serialization appears safe for the current implementation given both round-trip coverage and explicit schema-validation coverage.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```