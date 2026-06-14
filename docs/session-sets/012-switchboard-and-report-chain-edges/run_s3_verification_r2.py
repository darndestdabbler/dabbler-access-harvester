"""Routed cross-provider session-verification R2 for set 012 S3.

Confirms the R1 fixes and the disposition of the two deferred (DW-14) coverage
gaps. Persists output to disk before display.
"""
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"


def read(path, lo=None, hi=None):
    with open(path, encoding="utf-8") as f:
        lines = f.readlines()
    return "".join(lines[lo - 1:hi] if lo else lines)


codebehind = read("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.CodeBehind.cs")
new_tests = read("tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs", 487, 560)

prompt = f"""Your response MUST begin with a prose analysis of each item below \
BEFORE the JSON block. Both are required.

This is **Round 2** of the closeout `session-verification` for Session 3 of set \
`012-switchboard-and-report-chain-edges` (a C#/.NET 10 mechanical Access \
metadata harvester; deterministic byte-identical output is required). In R1 you \
(gpt-5-4) returned ISSUES_FOUND with 3 Major + 1 Minor. Confirm the fixes and \
judge the two deferrals.

## R1 findings and their disposition

1. **Major (R1 concern 2b) — member/index-wrapped LHS write false-positive.**
   `Forms!frm!ctl.Value = 42` was not suppressed and could emit a wrong-direction
   read edge. **FIXED:** `HandleAssignmentTarget` now peels `.Member`/`(index)`
   postfix layers to the controlling bang (`UnwrapToControllingBang`) and marks
   it as a write-target; a member-wrapped RHS read still emits. Pinned by
   `CodeBehind_MemberWrappedFormsControlWrite_DoesNotEmitReadEdge` and
   `CodeBehind_MemberWrappedFormsControlRead_EmitsReadEdge`.

2. **Major (R1 concern 4) — non-bang TempVars setter forms.** `TempVars.Add` and
   `TempVars("x") = ` setters were not in the supplied universe, risking false
   `tempvar-no-supplier`. **FIXED:** `TempVarSetterCollector` now recognises the
   bang, indexer, and `.Add` (bare + parenthesised) forms. Pinned by
   `TempVarSupply_NonBangSetterForm_SuppressesUnmatched` (theory, both forms).

3. **Minor (R1 concern 4 doc) — stale closed `Reason` set.** **FIXED:**
   `UnresolvedChainTarget.Reason` XML-doc now lists `non-literal-target` and
   `tempvar-no-supplier`.

4. **Major (R1 concern 1) — named-argument open targets** + **Major (R1 concern
   2a) — `Forms("frm")!ctl` indexer / `With` control reads.** **DEFERRED to
   DW-14.** Rationale: both are golden-neutral on HomeHealthCare (every HHC
   `DoCmd` open uses a positional string-literal target; every dialog read is a
   bare two-bang). The named-argument gap is **shared with the already-shipped
   VBA-edge layer** (`NavigationGraphBuilder`, set 011) — which records a
   named-arg open as a `missing-target` unresolved — so fixing it only in the
   chain producer would make the two layers disagree on the same call; a correct
   fix spans both layers (011's navigation-extraction surface), beyond this set's
   R2-2 / DW-10 Stop Rule. A consumer coverage-boundary note was added to
   interpretation-guide §5.7 directing consumers to cross-read `Edges` for
   per-call-site dispatches.

Post-fix: full hermetic suite green (chain pins now 42), and the HomeHealthCare
golden reproduces **byte-identically** from a fresh COM run (all four fixes are
golden-neutral).

## Updated code — NavigationChainBuilder.CodeBehind.cs

```csharp
{codebehind}
```

## New / updated hermetic pins

```csharp
{new_tests}
```

## Verify

- **(A)** Are fixes 1–3 correct and complete as implemented (no new
  false-positive/negative; `UnwrapToControllingBang` and the broadened
  `TempVarSetterCollector` sound; determinism preserved)?
- **(B)** Is the DW-14 deferral of the named-argument and indexer/`With`
  coverage gaps a legitimate scope call for a closeout session given they are
  golden-neutral and the named-arg gap is shared with the out-of-scope VBA-edge
  layer — or does either rise to a must-fix-now correctness defect?

Prefer false-positive ISSUES_FOUND over false-negative VERIFIED. After the prose, \
output a JSON block exactly:

```json
{{
  "verdict": "VERIFIED | ISSUES_FOUND",
  "issues": [
    {{"severity": "Critical|Major|Minor", "location": "<file/concern>", "summary": "<one sentence>", "fix": "<recommended fix>"}}
  ]
}}
```
If verdict is VERIFIED, `issues` must be `[]`. Do not omit the block."""

result = route(prompt, task_type="session-verification", complexity_hint=60)
out_path = f"{SS}/s3-verification-round-2.md"
with open(out_path, "w", encoding="utf-8") as f:
    f.write("# Set 012 S3 — routed cross-provider session-verification (Round 2)\n\n")
    f.write(f"cost: ${getattr(result,'cost',0):.4f} | tokens(out): {getattr(result,'output_tokens','?')} | truncated: {getattr(result,'truncated','?')}\n\n---\n\n")
    f.write(result.content)
print("WROTE", out_path, "truncated:", getattr(result, "truncated", "?"))
