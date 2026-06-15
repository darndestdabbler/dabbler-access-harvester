"""Set 015 S2 cross-provider verification ROUND 3 (DW-11 Layer B).
R2 (gpt-5-4): Major — residual not provably injective (lossy primary folds +
key=value ambiguity); Minor — pin coverage. This round verifies the injective
full-serialization residual + expanded pins. Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/015-unresolved-items-completeness"
OUT = os.path.join(SS, "s2-verification-r3.md")


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def excerpt(path, anchor, length, *, back=0):
    t = read(path)
    i = t.find(anchor)
    if i < 0:
        return f"(anchor {anchor!r} not found in {path})"
    return t[max(0, i - back):i + length]


def main():
    sort_method = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs",
        "internal static IReadOnlyList<UnresolvedItem> SortDeterministically", 2700)
    residual = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs",
        "// Options for the canonical-residual serialization", 1700)
    item_model = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs",
        "public sealed record UnresolvedItem", 2200)
    tests = excerpt(
        "tests/AccessHarvester.Pipeline.Tests/UnresolvedItemAggregatorTests.cs",
        "public void SortDeterministically_IsInputOrderIndependent_ForSamePrefixItems", 3200)

    prompt = f"""You are an expert C#/.NET reviewer performing ROUND 3 of an INDEPENDENT
cross-provider verification. This round verifies ONLY the resolution of the Round-2
findings. Begin with prose, then the JSON block.

# Round-2 findings being verified

- **Major:** "the comparer still is not provably a total order ... several fields are
  compared/serialized with lossy normalization (OrdinalIgnoreCase module/procedure;
  null-vs-empty; null-vs-0) and AdditionalContext uses an ambiguous key=value encoding."
- **Minor:** the regression pin did not exercise the remaining compare-equal collision
  classes (case-only module/procedure, null-vs-empty, null-vs-0, AdditionalContext values
  containing '=').

# Fix applied

The final tiebreaker is now an INJECTIVE canonical serialization of the WHOLE item:
`CanonicalResidual(item) = System.Text.Json.JsonSerializer.Serialize(item, opts)`.
- It serializes EVERY field (including the exact-case ModuleName/ProcedureName, null vs 0
  Line/Column as `null` vs `0`, null vs "" SurroundingText, and AdditionalContext as a
  proper JSON object with each key and value separately quoted/escaped — so there is no
  `key=value` delimiter ambiguity).
- Determinism: the only collection member, AdditionalContext, is a `SortedDictionary`
  (stable key order); `UnresolvedSource` serializes as a string via its type-level
  `[JsonStringEnumConverter]`. So the residual is a deterministic, injective-up-to-value
  function: two items with identical JSON are byte-identical in the emitted output (order
  immaterial); any difference yields a distinct, stable residual. The semantic
  comparisons are retained ONLY for human-readable primary ordering.

Regression pins expanded (a `[Theory]` with `FoldedCollisionPairs`):
(a) module name differing ONLY by case; (b) Line null vs 0; (c) AdditionalContext value
containing '='. Each pair ties on every primary comparison and is separated only by the
residual; the test asserts identical output across input permutations and that both
distinct items survive.

# Validation
- Full solution builds; AccessHarvester.Pipeline.Tests **919/919** (incl. the expanded
  determinism pins).
- Golden gate `CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests` +
  `SchemaConformanceTests`: **7/7** integration, byte-identical, NO re-regen needed after
  switching the residual to full serialization (no HHC items tie on the primary key, so
  the residual is never decisive for the corpus — it is pure robustness).

# Questions
1. Is the comparer NOW a provably total order (injective up to byte-identical output)?
   Name any UnresolvedItem field still not covered by either the semantic key or the
   serialized residual.
2. Is the determinism argument for the serialization sound (SortedDictionary key order,
   string enum, no other ordering-sensitive members)?
3. Are the expanded pins adequate?
4. Any regression introduced?

# Source

=== UnresolvedItem model (fields the residual must cover) ===
```csharp
{item_model}
```

=== SortDeterministically (UPDATED) ===
```csharp
{sort_method}
```

=== CanonicalResidual + options (UPDATED — injective serialization) ===
```csharp
{residual}
```

=== Determinism pins (UPDATED) ===
```csharp
{tests}
```

After prose, output:
```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [ {{ "severity": "Critical"|"Major"|"Minor", "location": "<file>", "summary": "<one sentence>", "fix": "<fix>" }} ]
}}
```
If VERIFIED, `issues` must be []. Do not omit the block.
"""

    r = route(content=prompt, task_type="session-verification", complexity_hint=85,
              session_set=SS, session_number=2)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
