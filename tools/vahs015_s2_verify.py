"""Set 015 Session 2 cross-provider verification (DW-11 Layer B, FINAL session).
Module-level unresolved refs reach unresolved-items.json via a new module-level
feed + a determinism tiebreaker. Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/015-unresolved-items-completeness"
OUT = os.path.join(SS, "s2-verification.md")


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

    projector_modlevel = excerpt(
        "AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs",
        "/// Enumerates every MODULE-LEVEL reference", 1600)
    projector_group = excerpt(
        "AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs",
        "private static Dictionary<(string Name, ProcedureKind Kind), List<ResolvedReference>>\n        GroupReferencesByProcedure",
        1100)
    pipe_wire = excerpt(
        "AccessHarvester.Core/Pipeline.cs",
        "// DW-11 Layer B (015 S2): ALSO feed", 1400, back=60)
    schema_enum = excerpt(
        "docs/test-cases/harvester-schema.v1.1.json",
        '"Source": { "enum": ["Resolver"', 900)
    enum_decl = excerpt(
        "AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs",
        "[JsonConverter(typeof(JsonStringEnumConverter))]", 200)

    prompt = f"""You are an expert C#/.NET reviewer performing an INDEPENDENT cross-provider
verification of one session's work. Your response MUST begin with a detailed prose
analysis addressing each concern below, BEFORE the JSON block. Both the prose and the
JSON block are required. Prefer a false-positive ISSUES_FOUND over a false-negative
VERIFIED. The two highest-value things you can do: (1) try to BREAK the no-double-count
claim in concern A, and (2) decide whether the determinism tiebreaker fix in concern C
is correct and complete.

# Context

Repo: dabbler-access-harvester — a C#/.NET 10 toolchain that MECHANICALLY harvests
Microsoft Access metadata for a downstream AI consumer. Core principles: explicit-
unresolved over silent-drop; DETERMINISTIC byte-identical output (two runs over the
same inputs must be byte-identical); lean by default; only emit fields the producer
actually wrote; NEVER weaken a test to force green.

This is **Session 2 of 2 (the FINAL session)** of set `015-unresolved-items-completeness`,
closing **DW-11 Layer B**. `unresolved-items.json` is the run-wide manifest INDEX a
consumer reads to enumerate every VBA reference the resolver could NOT bind.

## What Session 1 already shipped (Layer A — cross-session evidence, VERIFIED)

Layer A (prior session, gpt-5-4 VERIFIED) closed the procedure-HEADER undercount: an
unresolved ref in a procedure header (an Optional parameter's default expression) was
absent from the index because `FunctionClassificationBuilder` walks `procedureAst.Body`
ONLY. The fix added a procedure-scoped feed (`EnumerateProcedureScopedReferences` +
`FromProcedureScopedReferences`) deduped against the body-only feed on the key
`(module, procedure, line, column, reason, memberName)`. Both feeds and their dedup are
in the FULL `UnresolvedItemAggregator.cs` below (the `procedureScopedReferences` block in
`Build`, `FromProcedureScopedReferences`, `DedupKey`) and pinned by the `LayerA_*` tests
in the FULL test file below — review them as the foundation Layer B builds on.

## Layer B (THIS session)

A **module-level** unresolved reference — a `Const`/`Dim` initializer, an
`Implements <unresolved>`, a module-scope `As New <unresolved>` — lives on
`resolvedModule.References` with NO enclosing `ProcedureScope`. The projector's
`GroupReferencesByProcedure` drops it (no procedure `Body` to surface in), so before
this session it reached NEITHER `ParserOutput.Body.References` NOR `unresolved-items.json`
— no manifest signal at all.

- Proven reproducer: `Public Const DemoConst = MissingThing` (+ a trivial Sub) yields
  ONE reference on `resolvedModule.References` (confirmed empirically:
  `enclosingScope=ModuleScope`, `outcome=Unresolved`), dropped from both surfaces.

# The fix

1. **New projector enumerator** `EnumerateModuleLevelReferences(ResolvedModule)` returns
   exactly the references `GroupReferencesByProcedure` drops (those whose
   `FindEnclosingProcedureScope` is null).
2. **New aggregator feed** `FromModuleLevelReferences` emits `UnresolvedItem` with
   `Source = UnresolvedSource.ModuleLevelReference` (NEW enum value, appended last) and
   `ProcedureName = null`. Field mapping mirrors `FromProcedureScopedReferences`.
3. **New record** `ModuleLevelReference(ModuleName, Reference)`.
4. **Schema:** the `Source` enum in `harvester-schema.v1.1.json` is widened to add
   `"ModuleLevelReference"`. NO manifest-envelope version bump: the `UnresolvedItem`
   `$def` has no `additionalProperties:false`, the change adds no property (widens an
   existing enum), and the repo treats `harvester-schema.v1.1.json` as the LIVING schema
   for the v1.1 envelope. The enum serialises as a STRING
   (`[JsonConverter(typeof(JsonStringEnumConverter))]`), so appending is safe.
5. **Determinism tiebreaker (see concern C):** `SortDeterministically` gained
   `SurroundingText` as a final tiebreaker.

# Golden outcome — Layer B IS golden-affecting (DW-11's "zero cases" was WRONG)

The golden gate caught that HomeHealthCare DOES contain module-level unresolved refs:
`basDataOperations` lines 4-5 are
`Public Const ERR_SAME_FY As Long = vbObjectError + 1001` and
`Public Const ERR_CANCELED As Long = vbObjectError + 1002`. `vbObjectError` is a VBA
intrinsic outside the resolver's built-in set, so each Const initializer is a
module-level `not-in-scope` reference. The golden was regenerated (HHC only).

**Proven delta (multiset diff of the committed golden HEAD vs. the regenerated file):**
- HEAD 1414 items, NEW 1416 items.
- ADDED (in new, not head): EXACTLY 2 — both `Source=ModuleLevelReference`,
  `module=basDataOperations`, `ProcedureName=null`, lines 4 & 5, member `vbObjectError`.
- REMOVED (in head, not new): 0.
So the item MULTISET is identical except for the 2 net-new module-level items. Every
other line in the textual diff is pure REORDERING (see concern C). `manifest.json`
`UnresolvedItemCount` 1414 -> 1416 is the mechanical consequence.

# Concerns to address explicitly

A. **No double-count (CHALLENGE THIS).** The claim: a module-level reference has NO
   enclosing `ProcedureScope`, so the module-level feed (references where
   `FindEnclosingProcedureScope` is null) is DISJOINT from BOTH procedure-scoped feeds
   (which only carry references WITH a `ProcedureScope`), so no dedup is needed. Try to
   break it: is there a construct where the SAME `ResolvedReference` could be enumerated
   by both `EnumerateModuleLevelReferences` and `EnumerateProcedureScopedReferences`?
   Consider: a `Const`/`Dim` initializer that CALLS a function; a module-level statement
   nested in some scope that is neither clearly module nor procedure; a reference whose
   `EnclosingScope` chain is malformed. The multiset proof (0 removed) is empirical
   evidence for HHC — is the disjointness argument airtight in general?
B. **Enum-widening contract-correctness.** Is "widen the `Source` enum, no envelope
   bump" correct given the living-schema treatment, the string serialisation, and the
   additive-field lesson? Or does adding an enum value to a closed set warrant an
   envelope version bump? Argue and commit.
C. **Determinism tiebreaker (CHALLENGE THIS).** The first regen showed, besides the +2
   items, a cluster of same-key item REORDERINGS. Diagnosis: `SortDeterministically`'s
   key `(Source, module, procedure, line, column, reason)` is NOT a total order — a
   chained member access emits several Unresolved links sharing position+reason and
   differing only by member name (e.g. `.Fields` vs `.Value`). `List.Sort` is a
   non-stable introsort, so adding the +2 items perturbed the partition and reshuffled
   equal-key pairs. Fix: added `SurroundingText` (member name) as the final tiebreaker.
   Questions: (1) Is this the correct root cause and fix? (2) Is the key NOW a total
   order, or can two distinct items still tie (same Source/module/procedure/line/column/
   reason/SurroundingText)? If they can still tie, is the residual nondeterminism real
   or benign (identical items)? (3) Is changing the committed golden's order (vs.
   preserving the old arbitrary order) the right call?
D. **Superset / completeness boundary.** Module-level refs now reach the INDEX but NOT
   the per-module `ParserOutput.Body.References` (recorded as known limitation DW-17).
   Is index-only the right boundary for DW-11, or must the inline per-module surface
   also carry them this session?
E. **Source tagging.** Module-level refs are tagged `ModuleLevelReference` (distinct from
   the procedure-scoped `FunctionClassification`). Defensible, or should they reuse an
   existing source?
F. **Scope discipline / no weakened tests.** Confirm nothing out of scope was pulled in
   and no assertion/test was weakened to force green.
G. **Lean / no-silent-drop / determinism.** Any over-reach, silent drop, default
   injection, or residual nondeterminism?

# Validation results (already run)

- Build: full solution succeeds, 0 errors.
- Hermetic: AccessHarvester.Pipeline.Tests **915/915 pass** (912 prior + 3 new Layer B
  pins in `UnresolvedItemsCompletenessTests`):
  * `LayerB_ModuleLevelUnresolvedRef_DroppedByProcedureScopedSurfaces_RecoveredByModuleLevelFeed`
    — the ref IS on `resolvedModule.References`, ABSENT from inline Body.References AND
    the post-S1 index, PRESENT (once, source=ModuleLevelReference, reason=not-in-scope,
    module set, ProcedureName=null) after the module-level feed.
  * `LayerB_ModuleLevelFeed_DoesNotDoubleCount_ProcedureScopedRefs` — a module carrying
    BOTH a module-level ref and a body ref: module ref once under ModuleLevelReference,
    body ref once under FunctionClassification.
  * `LayerB_ModuleFreeOfModuleLevelRefs_IsInert` — a module with no module-level ref:
    the feed adds nothing (count unchanged, no ModuleLevelReference items).
- COM golden gate (HomeHealthCare): `CanonicalGoldenRegressionTests` PASSED (the
  regenerated corpus reproduces byte-identical); `ManifestSchemaConformanceTests` +
  `SchemaConformanceTests` PASSED (7/7 integration) — the widened enum is schema-valid
  for both produced and example documents. Multiset delta proven as above.

# Source under review

=== AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs (FULL — Layer A feed+dedup AND Layer B FromModuleLevelReferences, ModuleLevelReference record, SortDeterministically tiebreaker) ===
```csharp
{aggregator}
```

=== AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs — EnumerateModuleLevelReferences (NEW) ===
```csharp
{projector_modlevel}
```

=== ParserOutputProjector.cs — GroupReferencesByProcedure + FindEnclosingProcedureScope (the procedure grouping; module-level feed is its complement) ===
```csharp
{projector_group}
```

=== AccessHarvester.Core/Pipeline.cs — Layer B production wiring (single call site) ===
```csharp
{pipe_wire}
```

=== docs/test-cases/harvester-schema.v1.1.json — widened Source enum ===
```json
{schema_enum}
```

=== AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs — UnresolvedSource enum declaration (string serialisation) ===
```csharp
{enum_decl}
```

=== tests/AccessHarvester.Pipeline.Tests/UnresolvedItemsCompletenessTests.cs (FULL — Layer A pins [cross-session evidence] + 3 new Layer B pins) ===
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

    r = route(content=prompt, task_type="session-verification", complexity_hint=78,
              session_set=SS, session_number=2)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
