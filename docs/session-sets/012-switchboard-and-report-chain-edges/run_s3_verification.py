"""Routed cross-provider session-verification for set 012 Session 3.

Closeout-grade: prose-first lead + trailing JSON block; embeds earlier-session
(S1/S2) evidence because the routed verifier has no file access and no memory of
prior sessions. Persists output to disk BEFORE any display side effect.
"""
import os
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"


def read(path, lo=None, hi=None):
    with open(path, encoding="utf-8") as f:
        lines = f.readlines()
    if lo is not None:
        lines = lines[lo - 1:hi]
    return "".join(lines)


codebehind = read("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.CodeBehind.cs")
# Build overload + TempVar-supply matching (the S3 entry points in the main file).
nav_build = read("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs", 78, 170)
model = read("AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs", 703, 863)
tests_codebehind = read("tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs", 470, 744)
design = read(f"{SS}/s3-regrounding-and-design.md")

prompt = f"""Your response MUST begin with a detailed prose analysis for each \
numbered concern below BEFORE the JSON block. Do not skip the analysis. Both the \
analysis and the JSON block are required.

You are a cross-provider closeout verifier for Session 3 (final) of session set \
`012-switchboard-and-report-chain-edges` in a C#/.NET 10 Microsoft Access \
metadata harvester. The harvester is MECHANICAL extraction only — no AI \
interpretation, deterministic byte-identical output is a hard requirement.

## What Session 3 delivered (R2-2 part 2, DW-10)

A new **code-behind** producer extends `NavigationChainBuilder` to derive the \
cross-object navigation CHAIN edges that need the resolved VBA AST (the S2 \
producers were pure over the IR surfaces and touched no VBA). Over each \
form/report code-behind module it emits, with provenance `vba-call`:
- `DoCmd.OpenForm`/`OpenReport` literal-target dispatches as \
`OpensDialog`/`OpensForm`/`OpensReport` (an `acDialog` WindowMode → `OpensDialog`);
- `Forms!dlg!ctl` / `Reports!rpt!ctl` control READS as `SuppliesParameter` \
(dialog control → owning object);
- `TempVars!x = …` SETTERS as `SuppliesParameter` `tempvar-flow` (the supplier \
side that pairs to the S2 record-source `tempvar-flow` consumer references);
- non-literal open targets and unmatched TempVars as explicit \
`UnresolvedChainTarget`s.

The HomeHealthCare golden's `NavigationGraph.ChainEdges` went 37 → 63 (+24 \
`vba-call` = 13 deduped opens + 11 control reads; +2 `tempvar-flow` setters); \
`UnresolvedChainTargets` unchanged at 3 (both new unresolved reasons are \
golden-neutral on HHC and pinned synthetically). `kb/` and `ir/` are \
byte-identical (chain edges live only in the manifest). The full hermetic suite \
is 886 + 38 green; `CanonicalGoldenRegressionTests` + \
`ManifestSchemaConformanceTests` (COM) are 6/6 green and the golden reproduces \
byte-identically from a fresh run.

## Design + deviations (S3 re-grounding doc)

{design}

## S3 code — NavigationChainBuilder.CodeBehind.cs (the code-behind producer)

```csharp
{codebehind}
```

## S3 code — Build overload + TempVar supply-matching (NavigationChainBuilder.cs)

```csharp
{nav_build}
```

## Chain-edge model (NavigationGraph.cs)

```csharp
{model}
```

## S3 hermetic pins (NavigationChainBuilderTests.cs — code-behind region)

```csharp
{tests_codebehind}
```

## EARLIER-SESSION EVIDENCE (the verifier has no memory of S1/S2)

- **S2 (mechanical chain producers, same file, already shipped & 4-round \
VERIFIED):** the `embedded-macro` / `record-source` / `query-form-reference` / \
`tempvar-flow`(consumer) producers + the token-aware `ExtractTempVars` scanner \
live in `NavigationChainBuilder.cs`. S3 reuses the same closed `ChainEdgeKind` / \
provenance / `Reason` sets and the same determinism (single sort of the combined \
edge list). The S2 consumer-side `tempvar-flow` edge is `From={{TempVar,x}} → \
To={{owner}}`; the S3 supplier-side edge is `From={{owner}} → To={{TempVar,x}}` — \
together they form the walkable report→TempVar→sub-report chain.
- **S1 (R2-1, DW-9 closed):** a shape-gated switchboard row-read + \
`BuildNavigationInputs` populate `NavigationGraph.EntryPoints`/`.Switchboard`/\
`.Macros`; `Ribbon` deliberately null (DW-13).

## Concerns to verify (prose for each, by number, before the JSON)

1. **acDialog detection** — is positional slot 5 (OpenForm WindowMode) plus the \
`WindowMode:=` named-arg path correct and complete? Is making `OpensDialog` \
form-only (OpenReport always `OpensReport`) a defensible model decision or a gap?
2. **Syntactic Forms!/Reports! recognition** (deviation from the spec's \
resolver-based wording) — does the two-bang `IsFormsControlRef` shape miss \
realistic read shapes (`Forms!frm!ctl.Value`, `Forms("x")!ctl`, `With` blocks) \
or fabricate/keep wrong-direction edges? Is the `ReferenceEqualityComparer` \
write-target exclusion sound?
3. **module→owner mapping by name** — collision risk if a standard module shares \
a form's name, or a form and report share a name (forms-first then reports in the \
dictionary). Real defect or HHC-only non-issue?
4. **tempvar-no-supplier** — the supplied universe is `CollectAssignedTempVars` \
(all modules' `TempVars!x =` bang setters). False negative for `TempVars.Add`/\
`TempVars("x")=` setter forms? Reason-set closure?
5. **duplicate-edge collapse** — exact-duplicate code-behind edges collapse to \
one; the `ChainEdge` model carries no WhereCondition/OpenArgs. Lossy merge a \
consumer would be misled by, or acceptable structural abstraction (detail still \
in `Edges`)?
6. **determinism / golden** — is the combined (S2+S3) edge list sorted to a \
total, byte-stable order? Any nondeterminism from dictionary/HashSet iteration \
reaching output?
7. **scope discipline** — anything in S3 that exceeds R2-2 (DW-10) or weakens a \
test/assertion to force green?

Prefer false-positive ISSUES_FOUND over false-negative VERIFIED. After the prose \
analysis, output a JSON block in this exact format:

```json
{{
  "verdict": "VERIFIED | ISSUES_FOUND",
  "issues": [
    {{"severity": "Critical|Major|Minor", "location": "<file/concern>", "summary": "<one sentence>", "fix": "<recommended fix>"}}
  ]
}}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit the block."""

result = route(prompt, task_type="session-verification", complexity_hint=72)
out_path = f"{SS}/s3-verification.md"
with open(out_path, "w", encoding="utf-8") as f:
    f.write(f"# Set 012 S3 — routed cross-provider session-verification\n\n")
    f.write(f"Model: {getattr(result, 'model', '?')}  |  ")
    f.write(f"cost: ${getattr(result, 'cost', 0):.4f}  |  ")
    f.write(f"tokens(out): {getattr(result, 'output_tokens', '?')}  |  ")
    f.write(f"truncated: {getattr(result, 'truncated', '?')}\n\n---\n\n")
    f.write(result.content)
print("WROTE", out_path)
print("model:", getattr(result, "model", "?"), "truncated:", getattr(result, "truncated", "?"))
