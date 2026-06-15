"""Set 015 Session 2 cross-provider verification ROUND 2 (DW-11 Layer B).
R1 (gpt-5-4) ISSUES_FOUND: 1 Major — SortDeterministically not a total order.
This round verifies the CanonicalResidual total-order fix. Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/015-unresolved-items-completeness"
OUT = os.path.join(SS, "s2-verification-r2.md")


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def excerpt(path, anchor, length, *, back=0):
    t = read(path)
    i = t.find(anchor)
    if i < 0:
        return f"(anchor {anchor!r} not found in {path})"
    start = max(0, i - back)
    return t[start:i + length]


def main():
    sort_method = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs",
        "internal static IReadOnlyList<UnresolvedItem> SortDeterministically", 3300)
    residual = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs",
        "private static string CanonicalResidual(UnresolvedItem item)", 1100)
    det_test = excerpt(
        "tests/AccessHarvester.Pipeline.Tests/UnresolvedItemAggregatorTests.cs",
        "public void SortDeterministically_IsInputOrderIndependent_ForSamePrefixItems", 2200)

    prompt = f"""You are an expert C#/.NET reviewer performing ROUND 2 of an INDEPENDENT
cross-provider verification. Round 1 (you, gpt-5-4) returned ISSUES_FOUND with one Major:

> "The updated SortDeterministically comparer still is not a total order over serialized
>  UnresolvedItem values, so non-identical equal-key items can remain nondeterministically
>  ordered." Fix suggested: extend the comparer to include remaining fields (Location.File,
>  ConstructDescription, AdditionalContext, SuggestedNextSteps) or compare a canonical
>  serialized form after the primary key; add regression tests with same-prefix items that
>  differ only in omitted fields.

This round verifies ONLY whether that Major is correctly and completely resolved. Begin
with prose, then the JSON block. Prefer false-positive ISSUES_FOUND over false-negative.

# The fix applied

`SortDeterministically` now precomputes a `CanonicalResidual(item)` string ONCE per item
(covering every field NOT in the semantic comparison: `Location.File`,
`ConstructDescription`, every `SuggestedNextSteps` entry, and every `AdditionalContext`
key=value pair, ordered) and uses it as the FINAL tiebreaker after
(Source, module, procedure, line, column, reason, SurroundingText). Two items that compare
equal under this comparer are byte-identical across ALL fields, so the comparer is a
provably total order and the output is a pure function of the input SET (independent of
incoming order or list size — which is what bit the golden when the +2 module-level items
perturbed the non-stable introsort). The residual uses U+001F (unit separator) between
fields — a control char that cannot occur in these source-derived strings — so the
concatenation is unambiguous.

A direct regression pin was added:
`SortDeterministically_IsInputOrderIndependent_ForSamePrefixItems` — two Resolver
diagnostics tying on (Source, module, procedure, line, column, reason) with
SurroundingText=null but differing in ConstructDescription + AdditionalContext are sorted
identically regardless of input permutation.

# Validation

- Full solution builds; AccessHarvester.Pipeline.Tests **916/916** (the new determinism
  pin included).
- Golden gate `CanonicalGoldenRegressionTests` (HomeHealthCare) PASSES byte-identical
  WITHOUT a re-regen after this comparer change — i.e. no HHC items tie on the 7-key with
  differing residual, so the residual tiebreaker is pure robustness with zero additional
  golden impact beyond the already-explained Layer B deltas (the 2 net-new
  ModuleLevelReference items + the reordering the SurroundingText tiebreaker introduced).

# Questions for this round

1. Is the comparer NOW a provably total order over the whole `UnresolvedItem`? Is any
   field omitted from the primary comparison AND the residual? (Enumerate UnresolvedItem's
   fields: Source, Reason, ConstructDescription, Location{{File,ModuleName,ProcedureName,
   Line,Column}}, SurroundingText, SuggestedNextSteps, AdditionalContext — confirm each is
   covered by either the semantic key or the residual.)
2. Is the U+001F separator argument sound (could two distinct field-tuples produce the same
   residual string)? Consider empty fields and the trailing separators.
3. Is the regression pin adequate, or is a stronger one warranted?
4. Any residual nondeterminism or regression introduced by the change?

# Source under review

=== UnresolvedItemAggregator.SortDeterministically (UPDATED) ===
```csharp
{sort_method}
```

=== UnresolvedItemAggregator.CanonicalResidual (NEW) ===
```csharp
{residual}
```

=== UnresolvedItemAggregatorTests — new determinism pin ===
```csharp
{det_test}
```

After your prose, output:
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
