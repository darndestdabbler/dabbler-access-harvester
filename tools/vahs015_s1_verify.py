"""Set 015 Session 1 cross-provider verification (DW-11 Layer A).
Procedure-header unresolved refs reach unresolved-items.json via a
dedup-additive feed. Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/015-unresolved-items-completeness"
OUT = os.path.join(SS, "s1-verification.md")


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
    aggregator = read("AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs")
    tests = read("tests/AccessHarvester.Pipeline.Tests/UnresolvedItemsCompletenessTests.cs")

    projector_helper = excerpt(
        "AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs",
        "/// Enumerates every procedure-scoped reference", 1700)
    projector_group = excerpt(
        "AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs",
        "private static Dictionary<(string Name, ProcedureKind Kind), List<ResolvedReference>>\n        GroupReferencesByProcedure",
        1500)
    projector_key = excerpt(
        "AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs",
        "private static (string Name, ProcedureKind Kind) ProcedureScopeKey", 700)

    pipe_wire = excerpt(
        "AccessHarvester.Core/Pipeline.cs",
        "// DW-11 Layer A (015 S1): ALSO feed", 1500, back=120)

    # The body-only feed the new feed must EXACTLY mirror for the dedup to collapse
    # every body ref. These are the ground-truth derivations the verifier should
    # check the new feed's key against.
    fcb_visit = excerpt(
        "AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs",
        "public void Visit(ParserProcedure procedureAst)", 700)
    fcb_dispatch = excerpt(
        "AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs",
        "private void DispatchOutcome(ResolutionOutcome outcome, Expression expr)", 1000)
    fcb_handle = excerpt(
        "AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs",
        "private void HandleUnresolved(string reason, Expression expr)", 1100)
    fcb_build_name = excerpt(
        "AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs",
        "ModuleName = module.ModuleScope.ModuleName,", 200)

    prompt = f"""You are an expert C#/.NET reviewer performing an INDEPENDENT cross-provider
verification of one session's work. Your response MUST begin with a detailed prose
analysis addressing each concern below, BEFORE the JSON block. Both the prose and the
JSON block are required. Prefer a false-positive ISSUES_FOUND over a false-negative
VERIFIED. The single highest-value thing you can do is try to BREAK the dedup argument
in concern B.

# Context

Repo: dabbler-access-harvester — a C#/.NET 10 toolchain that MECHANICALLY harvests
Microsoft Access metadata for a downstream AI consumer. Core principles: explicit-
unresolved over silent-drop; deterministic byte-identical output; lean by default;
only emit fields the producer actually wrote.

This is **Session 1 of 2** of set `015-unresolved-items-completeness`, closing
**DW-11 Layer A**. `unresolved-items.json` is the manifest index a consumer reads to
enumerate every VBA reference the resolver could NOT bind. DW-11 (proven by a GPT-5.4
devil's-advocate probe in set 011) showed it UNDERCOUNTS in two layers; this session
fixes **Layer A only**:

- **Layer A (THIS session):** the VBA half of `unresolved-items.json` is built from
  `FunctionClassification.UnresolvedReferences`. `FunctionClassificationBuilder` walks
  only procedure **bodies** (`procedureAst.Body`), so an unresolved reference in a
  procedure **HEADER** (an Optional parameter's default expression) was ABSENT from the
  index — even though it IS present inline on `ParserOutput.Body.References[]` (the
  projector groups references by enclosing procedure *scope*, header included). So the
  index was not a structural superset of `Body.References[ResolutionStatus="Unresolved"]`.
  - Proven reproducer: `Public Sub Demo(Optional ByVal x As Variant = MissingThing)`
    (trivial body) projects ONE unresolved reference (`MissingThing`, `not-in-scope`)
    into `Body.References`, while the body-only feed and the aggregator returned ZERO.
- **Layer B (Session 2 — OUT OF SCOPE here, do NOT flag it as missing):** module-level
  unresolved references (`Const`/`Dim` initializers, `Implements <unresolved>`) are
  dropped at projection because the v1.0 schema has no module-level item type. The
  projector STILL drops them this session; Session 2 adds the manifest home. Anything
  about MODULE-LEVEL references is correctly deferred.

# The fix (the design decided at re-grounding)

**Dedup-additive feed.** The body-only `FunctionClassification.UnresolvedReferences`
feed is kept UNCHANGED (so body refs stay byte-identical). The aggregator is ADDITIONALLY
fed the **full procedure-scoped reference set** (body + header) from
`resolvedModule.References` — the exact set `ParserOutputProjector` groups into
`Body.References` — filtered to (post-with-flattening) Unresolved, and **deduplicated**
against the body-only set by a stable key `(module, procedure, line, column, reason)`.
Net-new items are therefore EXACTLY the header refs. The header items are tagged
`UnresolvedSource.FunctionClassification` (same source value as body refs) so a header
ref is indistinguishable in the index from a body ref — both are procedure-scoped VBA
references — and the existing body-ref Source value the golden carries is unchanged.

The new feed reuses `ParserOutputProjector.GroupReferencesByProcedure` (via the new
public `EnumerateProcedureScopedReferences`), so the index is a structural superset of
`Body.References` BY CONSTRUCTION (same source set). The dedup key is computed from
`reference.Expression.Provenance.Line/Column` and the (with-flattening) Unresolved
reason — chosen to MIRROR EXACTLY what the body-only feed derives, so every body ref
the body-only feed already emitted collapses and does not double-count.

# Why the dedup is claimed correct (CHALLENGE THIS — concern B)

For the dedup to collapse a body ref carried by BOTH feeds, the two feeds must produce
an identical `(module, procedure, line, column, reason)` key for the SAME reference:
- **module**: body-only feed uses `fc.ModuleName = module.ModuleScope.ModuleName`; new
  feed uses `module.ModuleScope.ModuleName` (same source object). Identical.
- **procedure**: body-only feed uses `fc.ProcedureName = procedureAst.Name`; new feed
  uses the enclosing `ProcedureScope`'s `ps.Procedure.Name`. Claimed identical.
- **line/column**: body-only feed uses `expr.Provenance.Line/Column` (see
  `HandleUnresolved`); new feed uses `reference.Expression.Provenance.Line/Column` —
  and `reference.Expression` IS the same AST node instance the body walk visits.
  Identical.
- **reason**: body-only feed's `DispatchOutcome` recurses into
  `ResolvedViaWithFlattening.InnerOutcome` then takes `Unresolved.Reason`; new feed's
  `UnresolvedReasonOf` does the same recursion. Identical.

The contrarian hypothesis to test: **could the dedup key suppress a LEGITIMATELY DISTINCT
header ref that happens to share a body ref's (module, procedure, line, column, reason)?**
A header ref (a parameter default expression) and a body ref live at different source
positions, so line+column differ — but argue whether any construction makes them collide.

# Concerns to address explicitly

A. **Dedup-key parity** — does the new feed's `(module, procedure, line, column, reason)`
   EXACTLY mirror the body-only feed's for the SAME reference (the four bullets above)?
   If any component can diverge for a real body ref, the body ref would DOUBLE-COUNT.
B. **Over-suppression (false-negative)** — can the dedup drop a real header ref that
   shares a body ref's key? Construct a counterexample if you can.
C. **Net-new body items (golden risk)** — could the procedure-scoped feed emit a body
   item the body-only feed MISSED (i.e., a reference the resolver records but the
   `ClassificationVisitor` body walk does not visit), which would survive dedup as
   net-new and CHANGE the golden? Note the body walk overrides Visit for
   Identifier/MemberAccess/BangAccess/ImplicitWithMember/ImplicitWithBang/Me only.
   (Disclosed: the HHC COM golden gate passed byte-identical — see Validation — so on the
   32-module corpus there are zero net-new items. Is that a sufficient guarantee, or is
   there a construct not in HHC that would diverge?)
D. **Source tagging** — header refs are tagged `UnresolvedSource.FunctionClassification`.
   Is tagging a header ref with the same source value as a body ref defensible (both are
   procedure-scoped VBA references; it keeps body-ref Source unchanged), or is a distinct
   source value warranted?
E. **Superset property** — is the index now a structural superset of
   `Body.References[Unresolved]` for procedure-scoped refs, given the feed reuses the
   projector's own grouping?
F. **Determinism** — `Build` still ends in `SortDeterministically`; the dedup uses a
   HashSet seeded from the FunctionClassification items then `bodyKeys.Add` as the
   skip/keep test. Is output order still input-order-independent and byte-stable?
G. **Scope discipline** — module-level (Layer B) refs are STILL dropped (Session 2 owns
   them); no schema change this session; no test weakened. Confirm nothing out of scope
   was pulled in.
H. **Lean/no-silent-drop** — any over-reach, silent drop, or default injection?

# Validation results (already run)

- Build: full solution succeeds, 0 errors.
- Hermetic: AccessHarvester.Pipeline.Tests **911/911 pass** (907 prior + 4 new pins in
  `UnresolvedItemsCompletenessTests`):
  * `LayerA_HeaderUnresolvedRef_DroppedByBodyOnlyFeed_RecoveredByProcedureScopedFeed`
    — proves the header ref IS inline on Body.References, ABSENT from the body-only feed
    (the gap), and PRESENT (exactly once, correct source/reason/module/procedure) after
    the additive feed.
  * `LayerA_BodyUnresolvedRef_EmittedExactlyOnce_NoDoubleCount` — a body unresolved ref
    appears exactly once after the additive feed, and `bodyOnly.Count == withHeaders.Count`
    on a header-free module (the inertness that makes HHC golden-neutral).
  * `LayerA_Index_IsStructuralSuperset_OfInlineUnresolvedBodyReferences` — every inline
    Body.References[Unresolved] entry has a matching index entry; body ref once.
  * `LayerA_HeaderRef_IndexPositionMatchesInlineReference` — the header ref's index
    Line/Column align with the inline Body.References Location (Provenance == Location.Start
    here).
- COM golden gate (HomeHealthCare): `CanonicalGoldenRegressionTests.HomeHealthCare_
  ReproducesGoldenOutput` **PASSED** (TRX-confirmed) — `unresolved-items.json`, manifest,
  kb, ir, and reports-baseline are BYTE-IDENTICAL to the committed golden. So Layer A is
  proven golden-neutral on HHC at the byte level (NO regen). HHC has zero header/module-
  level unresolved cases (per DW-11), so net-new = 0.

# Source under review

=== AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs (FULL — Build dedup, FromProcedureScopedReferences, UnresolvedReasonOf, ExtractReferenceName, DedupKey, ProcedureScopedReference) ===
```csharp
{aggregator}
```

=== AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs — EnumerateProcedureScopedReferences (NEW public method) ===
```csharp
{projector_helper}
```

=== ParserOutputProjector.cs — GroupReferencesByProcedure + FindEnclosingProcedureScope (the grouping the new method reuses; same set that builds Body.References) ===
```csharp
{projector_group}
```

=== ParserOutputProjector.cs — ProcedureScopeKey (procedure name + kind from the enclosing scope) ===
```csharp
{projector_key}
```

=== AccessHarvester.Core/Pipeline.cs — the production wiring (single call site) ===
```csharp
{pipe_wire}
```

=== GROUND TRUTH (NOT changed) FunctionClassificationBuilder — body-only walk the dedup must mirror ===
--- Visit (walks procedureAst.Body ONLY — the root cause) ---
```csharp
{fcb_visit}
```
--- DispatchOutcome (recurses ResolvedViaWithFlattening.InnerOutcome; Unresolved -> HandleUnresolved) ---
```csharp
{fcb_dispatch}
```
--- HandleUnresolved (Line = expr.Provenance.Line, Column = expr.Provenance.Column, MemberName = ExtractName(expr)) ---
```csharp
{fcb_handle}
```
--- FunctionClassification.ModuleName / ProcedureName derivation ---
```csharp
{fcb_build_name}
```

=== tests/AccessHarvester.Pipeline.Tests/UnresolvedItemsCompletenessTests.cs (FULL — the 4 new pins) ===
```csharp
{tests}
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

    r = route(content=prompt, task_type="session-verification", complexity_hint=72,
              session_set=SS, session_number=1)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
