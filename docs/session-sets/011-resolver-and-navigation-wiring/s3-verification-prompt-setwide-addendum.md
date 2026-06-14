# Set 011 — set-wide devil's-advocate addendum (Sessions 1 & 2 soft spots)

> **Why this exists:** the main `s3-verification-prompt.md` is closeout-grade but
> S3-centric. This addendum points the same devil's-advocate pair at the three
> **Session 1 / Session 2** claims that most deserve a harder look — each was
> either **closed by reasoning rather than a code change**, **discharged by a
> single narrow pin**, or **validated empirically on one corpus rather than
> proven structurally**. The earlier verifications (S1: gpt-5-4 R1→R2; S2:
> gpt-5-4 R1 clean) were single-provider and accepted these; a second,
> adversarial, *path-aware* pair is the right instrument to re-test them.
>
> **How to run (operator):** same as the main prompt — paste into GitHub Copilot
> twice (Gemini Pro, then GPT-5.4) in the `dabbler-access-harvester` workspace;
> the reviewers can open every file named below. Save raw answers to
> `…/s3-setwide-gemini.md` and `…/s3-setwide-gpt-5-4.md`. Hand both back.

---

> **Bias cautions:** This prompt was authored by the session orchestrator
> (Claude Opus 4.8), which has an opinion. Its framing may steer you toward
> in-scope refinements when the right answer is to question the scope, and the
> work may be presented as further along than it is. Before answering as posed,
> check whether this is the right question; if a sharper one exists, answer that
> too.

**You are a devil's-advocate verifier. Null hypothesis: each claim below is
wrong.** These three findings were *raised* by the original verifier and then
*retired* — your job is to test whether they were retired correctly, using the
actual code (not the prose rebuttals). Prefer a concrete counterexample
(a specific AST, name-divergence, or unresolved-reference shape) over a verdict.
**Begin with prose analysis of C7–C9 by number before the JSON block; both are
required.**

Background: Set 011 wired the resolver (S1, F-3-4) and the navigation/enrichment
substrate (S2, F-2-2) into production. The shipped HHC golden
(`samples/GoldenOutput/HomeHealthCare/`) is confirmed to reproduce
byte-identically from a fresh COM run. The three concerns are about whether the
wiring is *correct beyond the HHC corpus*, not whether HHC happens to pass.

---

**C7 — S1: does a degraded module contaminate sibling resolution, or resolve to
an effectively-absent target?**

S1 replaced per-module throwaway `ProjectScope`s with **one shared
`ProjectScope`** across the parse loop (`AccessHarvester.Core/Pipeline.cs`,
two-pass: build all module scopes, then resolve). The original verifier raised
this as a **Major** (`s1-verification.md`, finding A) with two leakage cases,
then **retired it in `s1-verification-round-2.md`** on two reasoning claims:
(A1) "no parser-producible AST can make `BuildModuleScope` throw *after* it has
attached a partial scope, because the post-attach guards sit on statement
visitors that are unreachable from a module root"; (A2) "a module that fails
*resolution* still has valid public declarations, so siblings binding to them is
semantically fine." A1 was explicitly closed on **absence of a counterexample**,
not proof.

*Contrarian hypotheses to make true (pick the strongest you can substantiate):*
- **(a)** Read `BuildModuleScope` and the scope-building visitor in
  `AccessHarvester.Vba.Resolver` directly. Find *any* code path — not only
  statement visitors — that mutates/attaches into the shared `project` and can
  then throw (e.g., duplicate-symbol insertion, a malformed-but-parseable
  declaration, a name collision across modules). If one exists, the
  "transactional registration" fix the original Major asked for is still owed.
- **(b)** The A2 rebuttal proves the symbols are *real* but not that the target
  is *present in the output a consumer reads*. A module with
  `VBA_PARSE_FAILURE` ships as a **stub** (`ParserOutput=null`); a
  `VBA_RESOLVE_FAILURE` module ships degraded. Can a sibling's cross-module
  reference resolve to a public member of such a module, producing a
  `"Resolved"` reference in the manifest whose target's own body is null/
  degraded — a state no diagnostic flags and the interpretation guide does not
  warn about? If so, is that a correctness or a documentation defect?

Files: `AccessHarvester.Core/Pipeline.cs`; `AccessHarvester.Vba.Resolver/`
(scope builder + `ReferenceResolver`); `s1-verification.md`;
`s1-verification-round-2.md`; the manifest `Diagnostics` contract in
`docs/harvester/interpretation-guide.md` §4.

**C8 — S1: does `Me` / bare-control binding silently miss when object names
diverge?**

S1 builds `AccessObjectModelRegistry` and passes an `owningClass` for
Form/Report modules so `Me`/control references bind. The `owningClass` is
**synthesized from the parse-loop / file name**, not from authoritative
extracted metadata, and binding relies on a registry **by-`Name` fallback**. The
original verifier raised this as a **Minor** (`s1-verification.md`, finding B)
wanting coverage of *three* divergences: `FormMetadata.Name != ModuleName`,
`filename-stem != metadata Name`, and sanitization mismatches. It was retired in
R2 on the strength of **one** new hermetic pin (`ModuleName != Name`).

*Contrarian hypothesis:* the binding is brittle on real databases where the
three names diverge. Verify: does the new pin (find it in
`tests/AccessHarvester.*`) actually exercise all three divergence classes, or
only `ModuleName != Name`? Is the "alignment on `Name` is structural" claim
*proven* anywhere, or assumed because `owningClass` is built from the file/loop
name? Construct a name-divergence case (metadata `Name` ≠ file stem, or a
sanitized control name) and argue whether `Me`/bare-control binding would
silently fall through to unresolved.

Files: `AccessHarvester.Core/Pipeline.cs` (registry build + `owningClass`
synthesis); the by-`Name`/by-`ModuleName` lookup in
`AccessHarvester.Vba.Resolver`; the divergence pin under `tests/`;
`s1-verification.md` finding B.

**C9 — S2: is `unresolved-items.json` actually complete, or only complete on
HHC?**

In production, `UnresolvedItems` is built from **exactly two** sources —
`UnresolvedItemAggregator.Build(navigationGraph, functionClassifications)`
(`AccessHarvester.Core/Pipeline.cs:612-614`); the resolver-diagnostics,
SQL-form-reference, and dynamic-SQL inputs are passed `null` (DW-8). The S2
disposition reconciles the total `1414 = 1392` (from
`FunctionClassification.UnresolvedReferences`) `+ 22` (navigation), claiming the
1392 matches the per-reference `ResolutionStatus="Unresolved"` count across all
parser outputs **reason-by-reason** — but that match was measured **on HHC**,
and a repo search finds **no hermetic pin** asserting the superset relationship.

*Contrarian hypothesis to make true:* on a database other than HHC,
`unresolved-items.json` **under-counts** — a reference is `Unresolved` inline on
`ParserOutput.Body.References[]` but absent from the run-wide index. Steelman it:
`FunctionClassification` is **procedure-scoped**. Identify unresolved references
that may live *outside* a classified procedure — module-level declarations
(`Private x As New <unresolved>`, `Implements <unresolved>`, `Const` initializers
referencing unresolved names), or references in procedures for which no
`FunctionClassification` is emitted. Read how `FunctionClassification.Unresolved\
References` is populated (`FunctionClassificationBuilder` /
`AccessHarvester.Pipeline/Enrichment/`) and decide: is it a **structural
superset** of `Body.References[Unresolved]`, or does it simply *happen* to be one
on HHC because HHC has no module-level/unclassified unresolveds? Separately:
does interpretation-guide §6 / §5.7 warn consumers that the run-wide
`unresolved-items.json` is function-classification-scoped (it names the DW-8/DW-9
producer gaps but not this scoping), so a consumer does not read an empty/short
list as "everything resolved"?

Files: `AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs`;
`AccessHarvester.Core/Pipeline.cs:612-614`; the `FunctionClassification`
builder under `AccessHarvester.Pipeline/`; `docs/harvester/interpretation-guide.md`
§6 and the new §5.7; `s2-verification.md`.

---

After the prose, output exactly:

```json
{
  "verdict": "VERIFIED",
  "issues": [
    { "severity": "Critical", "location": "<file or concern id>",
      "summary": "<one sentence>", "fix": "<recommended fix>" }
  ]
}
```

`issues` is `[]` iff you found no Critical/Major issue. `verdict` is `"VERIFIED"`
only when `issues` is empty; otherwise `"ISSUES_FOUND"`. Distinguish a **code**
defect (the wiring is wrong beyond HHC) from a **documentation** defect (the
behavior is acceptable but the interpretation guide should warn the consumer) in
each finding's `summary`.
