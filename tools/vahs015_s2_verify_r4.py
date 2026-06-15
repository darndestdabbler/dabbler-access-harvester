"""015 S2 verification ROUND 4 (confirmation). R3 cleared all Major/Critical;
sole R3 Minor = add FoldedCollisionPairs cases for ProcedureName case-only,
SurroundingText null-vs-empty, Column null-vs-0. This confirms they were added."""
import os
from ai_router import route
SS = "docs/session-sets/015-unresolved-items-completeness"
OUT = os.path.join(SS, "s2-verification-r4.md")
def read(p):
    with open(p, encoding="utf-8") as f: return f.read()
def excerpt(path, anchor, length):
    t=read(path); i=t.find(anchor)
    return t[i:i+length] if i>=0 else f"(anchor {anchor!r} not found)"
tests = excerpt("tests/AccessHarvester.Pipeline.Tests/UnresolvedItemAggregatorTests.cs",
                "public static IEnumerable<object[]> FoldedCollisionPairs", 2200)
prompt = f"""ROUND 4 confirmation of an independent cross-provider verification. R3 returned
ISSUES_FOUND with exactly ONE Minor (no Major/Critical): the determinism regression pins
did not cover ProcedureName case-only, SurroundingText null-vs-empty, and Column null-vs-0
folded-collision classes. The injective full-serialization residual fix itself was
ACCEPTED at R3. This round confirms ONLY that the requested pin cases were added and are
correct. Begin with brief prose, then the JSON block.

The FoldedCollisionPairs data source now contains cases (a) module case-only, (b) Line
null-vs-0, (c) AdditionalContext value with '=', (d) ProcedureName case-only, (e)
SurroundingText null-vs-empty, (f) Column null-vs-0. Each pair ties on every primary
comparison (module/procedure OrdinalIgnoreCase; null Line/Column folded to 0; null
SurroundingText folded to "") and is separated only by the injective serialized residual.
The [Theory] asserts identical output across input permutations and that both distinct
items survive. Full suite: AccessHarvester.Pipeline.Tests 922/922; golden gate + schema
conformance 7/7 byte-identical.

Confirm: do the cases cover the three R3-named classes (ProcedureName case-only,
SurroundingText null-vs-empty, Column null-vs-0)? Any case mis-constructed (e.g. a pair
that is actually NOT a primary-key tie, or that the residual would wrongly collapse)?

=== FoldedCollisionPairs + ResolverItem helper ===
```csharp
{tests}
```

After prose, output:
```json
{{ "verdict": "VERIFIED" | "ISSUES_FOUND", "issues": [ {{ "severity": "Critical"|"Major"|"Minor", "location": "<f>", "summary": "<s>", "fix": "<f>" }} ] }}
```
If VERIFIED, issues must be []."""
r = route(content=prompt, task_type="session-verification", complexity_hint=60, session_set=SS, session_number=2)
with open(OUT,"w",encoding="utf-8") as f: f.write(r.content)
print(f"Wrote {OUT} | model={r.model_name} | cost=${r.cost_usd:.4f}")
