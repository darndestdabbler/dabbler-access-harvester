"""Set 015 Session 1 cross-provider verification — ROUND 2.
Confirms the R1 Major (intra-feed dedup could drop distinct chained header
refs) is resolved by the strengthened key + non-mutating dedup.
Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/015-unresolved-items-completeness"
OUT = os.path.join(SS, "s1-verification-r2.md")


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
    build_block = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs",
        "if (procedureScopedReferences is not null)", 2200, back=80)
    feed = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs",
        "public static IEnumerable<UnresolvedItem> FromProcedureScopedReferences", 1400)
    chain_pin = excerpt(
        "tests/AccessHarvester.Pipeline.Tests/UnresolvedItemsCompletenessTests.cs",
        "public void LayerA_ChainedHeaderUnresolvedRefs_AllReachIndex_NoIntraFeedDrop", 2100)

    prompt = f"""You are an expert C#/.NET reviewer performing ROUND 2 of an INDEPENDENT
cross-provider verification. Your response MUST begin with prose analysis BEFORE the
JSON block. Prefer a false-positive ISSUES_FOUND over a false-negative VERIFIED.

# What Round 1 found (Major — now addressed)

This is `015-unresolved-items-completeness` Session 1 (DW-11 Layer A): procedure-HEADER
unresolved references (an Optional parameter's default expression) must reach
`unresolved-items.json`, which is built from the procedure-BODY-only
`FunctionClassification.UnresolvedReferences` feed. The fix ADDITIONALLY feeds the full
procedure-scoped resolver reference set (body + header) and suppresses the body overlap.

Round 1 (you, gpt-5-4) returned ISSUES_FOUND with ONE Major:

> The `procedureScopedReferences` dedup loop used `bodyKeys.Add(DedupKey(item))`, which
> deduplicates WITHIN the new procedure-scoped feed as well as against the body-only feed.
> A chained unresolved member access in a header — `Foo.Bar.Baz` → links `Foo`,
> `Foo.Bar`, `Foo.Bar.Baz` — emits distinct Unresolved refs that (because member-access
> provenance anchors at the chain start) share `(module, procedure, line, column, reason)`.
> The `Add`-based loop dropped the later link, recreating an undercount inside Layer A.

**This was CONFIRMED empirically:** a new pin
`LayerA_ChainedHeaderUnresolvedRefs_AllReachIndex_NoIntraFeedDrop` with exactly that
reproducer FAILED against the Round-1 code (the `Foo.Bar` / `Foo.Bar.Baz` collision is
real on this parser — member-access provenance does anchor at the chain start).

# The fix (verify it fully closes the Major and introduces nothing new)

Two changes, both in `UnresolvedItemAggregator`:

1. **Non-mutating dedup.** The loop now seeds `bodyKeys` once from the FunctionClassification
   items and tests membership with `!bodyKeys.Contains(DedupKey(item))` — it NEVER adds
   procedure-scoped keys back into the set. So the feed is dedup'd ONLY against the
   body-only set (the stated design intent), never against itself. Distinct procedure-scoped
   refs that share a key both survive.
2. **Stronger key.** `DedupKey` now includes `SurroundingText` (the member/identifier name)
   as a sixth component. The body-only feed and the procedure-scoped feed BOTH derive this
   name from the SAME AST node via the identical `ExtractName` switch
   (`Foo.Bar` → "Bar", `Foo.Bar.Baz` → "Baz"), so:
   - the same physical body ref still produces an IDENTICAL key in both feeds → still
     suppressed (no double-count regression);
   - the chain links now differ by name → distinct keys → both survive.

# Questions for Round 2

1. Does the `Contains`-based loop fully resolve the intra-feed over-suppression? Is there
   any remaining path where a distinct procedure-scoped unresolved ref is dropped?
2. Does adding `SurroundingText` to the key risk FAILING to suppress a real body ref
   (→ double-count)? Argue from the fact that both feeds compute the name from the same
   node via the same `ExtractName` logic. Is there any node type where the two would derive
   a DIFFERENT name for the same reference?
3. Any new determinism or ordering issue introduced (the final `SortDeterministically`
   is unchanged; the dedup no longer depends on enumeration order)?
4. Any residual correctness concern on the dedup seam, the superset property, or scope?

# Re-validation results (already run after the fix)

- AccessHarvester.Pipeline.Tests: **912/912 pass** (was 911; +1 the new chained-header pin).
  The chained-header pin was RED before the fix and is GREEN after.
- COM golden gate `CanonicalGoldenRegressionTests.HomeHealthCare_ReproducesGoldenOutput`:
  **PASSED again** (TRX-confirmed) — `unresolved-items.json` + manifest + kb + ir +
  reports-baseline remain BYTE-IDENTICAL to the committed golden, so the strengthened key
  is still golden-neutral on HHC.

# Source under review (post-fix)

=== UnresolvedItemAggregator.Build — the dedup block (Contains, not Add) + DedupKey (now 6-tuple incl SurroundingText) ===
```csharp
{build_block}
```

=== UnresolvedItemAggregator.FromProcedureScopedReferences (unchanged from R1 — shown for the name/SurroundingText derivation) ===
```csharp
{feed}
```

=== The new pin proving the chain links all reach the index (RED before fix, GREEN after) ===
```csharp
{chain_pin}
```

After your prose analysis, output a JSON block in this exact format:
```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{ "severity": "Critical" | "Major" | "Minor", "location": "<file>", "summary": "<one sentence>", "fix": "<recommended fix>" }}
  ]
}}
```
If verdict is VERIFIED, `issues` must be an empty array []. Do not omit this block.
"""

    r = route(content=prompt, task_type="session-verification", complexity_hint=68,
              session_set=SS, session_number=1)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
