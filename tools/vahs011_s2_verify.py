"""Set 011 Session 2 — cross-provider session verification (Step 6).

Routes a full verification packet to gpt-5-4 (cross-provider vs the Claude/Opus
orchestrator; the calibrated session-verification override). Persist-then-display.
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route  # type: ignore

SET = Path("docs/session-sets/011-resolver-and-navigation-wiring")
src_diff = Path("tools/.vahs011_s2_src.diff").read_text(encoding="utf-8", errors="ignore")
new_test = Path("tests/AccessHarvester.Tests/ManifestEnrichmentWiringTests.cs").read_text(encoding="utf-8")

PROMPT = f"""\
Your response MUST begin with a detailed PROSE analysis of each concern below,
BEFORE the trailing JSON block. Do not skip the prose. Prefer a false-positive
ISSUES_FOUND over a false-negative VERIFIED if you are unsure.

You are the cross-provider verifier for Session 2 of session set
`011-resolver-and-navigation-wiring` (a C# .NET 10 Access-metadata harvester).
The orchestrator was Claude Opus 4.8. Verify the work below.

# Session scope (F-2-2) and a re-grounding-driven scope decision

Session 2's spec asked to (A) wire the built-but-unwired "Phase 6 enrichment
substrate" into the production manifest pipeline, and (B) deliver the R2-1
(switchboard navigation edges) and R2-2 (dialog->query->report chains) consumer
requests via a "Switchboard Items table reader (rows are in the harvested table
data; no new COM surface)".

RE-GROUNDING FINDING (the spec required re-grounding against the tree): the spec's
R2-1 premise is FALSE. `tblSwitchboardItems` ships `(no data file)`; its rows are
absent from every harvested artifact (the set-006 review's own probe says "no
artifact carries row content"). Reading them needs a NEW OleDb/DAO row-read =
new COM surface, which this set's Hard Constraints forbid; and set-006 already
ranked R2-1/R2-2 as a separate 3-session "P4" set whose scope explicitly includes
"narrow OleDb row-read". DECISION (Option A): deliver only the F-2-2 core wiring
(no new COM); defer R2-1/R2-2 to P4 via DW-9/DW-10. This was chosen by
cross-provider consensus (gpt-5-4 + gemini-pro both Option A) AND approved by the
human maintainer. You are NOT asked to re-litigate the scope decision; verify the
DELIVERED work is correct, complete for Option A, and honest about what was deferred.

# What was implemented

1. NavigationGraph wired into manifest.json off the existing resolvedModules +
   crossRefs seam (NavigationGraphBuilder.Build(resolvedModules, crossRefs),
   inputs=null). Surfaces VBA code-behind DoCmd.OpenForm/OpenReport/Close/BrowseTo
   edges. inputs=null because the switchboard/ribbon/macro inputs are the deferred
   DW-9 work (not harvested).
2. unresolved-items.json populated via UnresolvedItemAggregator.Build(
   navigationGraph, functionClassifications) — exactly the aggregation the proven
   hermetic EndToEndFixtureTests exercise. Resolver diagnostics deliberately NOT
   fed separately (they would double-emit; the same unresolved refs already
   surface via FunctionClassification.UnresolvedReferences).
3. Enrichment classification content: EnrichmentPipelineRunner run per form/report
   (EventHandlerClassifier + ValidationRuleExtractor + RowSourceSpecBuilder),
   populating the per-reference Classification slots AND the run-wide manifest
   arrays; plus FunctionClassifications passed through; plus table-field validation
   via ValidationRuleExtractor.ExtractFromTable.
4. Removed the 010 interim "unresolved-items always empty" note from
   interpretation-guide section 6 (replaced with the now-wired status).
5. DW-9 (switchboard/macro inputs) + DW-10 (R2-2 chains) added to deferred-work.md.
6. A PRE-EXISTING F-3-4 (Session 1) bug surfaced when this session ran the full
   ManifestSchemaConformanceTests gate (S1 had only run the golden-compare gate):
   ParserOutputProjector.BuildDeclarationTarget wrote `"Kind": null` for ObjectModel
   (Me.<control>) targets whose declaration type DeclarationKindString does not
   enumerate (325 occurrences, ALL identical in the HEAD/S1 golden, proving it is
   pre-existing and orthogonal to F-2-2). null-on-disk is schema-invalid
   (ResolvedTarget.Kind is typed string) and violates the omit-null convention.
   FIX: omit Kind when null. This is an F-3-4 conformance correction — within this
   set's Stop-Rule golden-delta envelope ("every delta explained by F-3-4 OR F-2-2").

# Build & test results (all GREEN unless noted)

- `dotnet build`: succeeded, 0 errors.
- Hermetic: AccessHarvester.Tests 646 pass / 2 skip; AccessHarvester.Pipeline.Tests
  835 pass; AccessHarvester.Vba.Resolver.Tests 410 pass; Vba.Parser.Tests 92;
  SymbolDb.Tests 71. New F-2-2 pins (5) + F-3-4 omit-null pin (1) all pass.
- COM golden regen (foreground, TRX-confirmed): only manifest.json +
  unresolved-items.json changed in the golden (kb/ir/migration/READ-ME byte-identical;
  samples/*.accdb timestamp churn swept).
- Full Integration COM suite: 58 pass / 3 skip (2 regen guards + 1 quarantined
  sanitize test, intentionally skipped) / 0 fail — incl. CanonicalGoldenRegressionTests
  AND ManifestSchemaConformanceTests (the latter was red before the Kind fix).

# Golden delta (every delta mapped)

- manifest.json gains the `NavigationGraph` key: 22 Edges (13 OpenForm + 1 OpenReport
  + 8 Close), 22 UnresolvedTargets, EntryPoints/Switchboard/Ribbon/Macros empty
  (DW-9 deferred). [F-2-2]
- ValidationRules 0->76, RowSourceSpecs 0->19, FunctionClassifications 0->233,
  EventHandlerClassifications 0->81. [F-2-2]
- UnresolvedItems 0->1414. FAITHFULNESS CHECK: 1392 are FunctionClassification-sourced
  and EXACTLY match (reason-by-reason) the count of per-reference ResolutionStatus=
  Unresolved across all ParserOutputs (default-member-not-yet-resolved 680,
  member-access-on-unresolved-target 216, not-in-scope 494, with-block-flattening
  -deferred 2) — no double-count, no loss; +22 NavigationTarget non-literal-target.
  unresolved-items.json is byte-identical to manifest.UnresolvedItems. [F-2-2]
- ResolvedTarget Kind keys 3561->3236 (the 325 null-Kind entries drop the key). [F-3-4 fix]
- Object counts unchanged (Forms 19, Reports 12, Queries 22, Modules 4, Tables 13,
  Relationships 12, Diagnostics 1); SchemaVersion 1.1; no new VBA_*_FAILURE diagnostics.

# Concerns to verify (address each in prose)

C1. Is the NavigationGraph/UnresolvedItems/enrichment wiring correct and
    deterministic (the manifest aggregator preserves caller order; each producer
    is internally sorted)? Any double-emission or lost-signal risk?
C2. Is omitting R2-1/R2-2 (DW-9/DW-10) honestly represented (no silent "done"),
    and is inputs=null the right call given the rows are not harvested?
C3. Is the F-3-4 Kind omit-null fix correct and does it lose needed signal?
    (Name is always retained; Kind was null only for control-type declarations
    DeclarationKindString does not enumerate.)
C4. Is the unresolved-items count of 1414 a faithful aggregation rather than noise,
    given the faithfulness check above and that the per-reference ResolutionReason
    remains the authoritative per-reference signal?
C5. Were any assertions/tests weakened to force green? (The 2 pre-existing
    skipped tests are the documented regen guards + quarantined sanitize test.)
C6. Anything in scope for Option A that is missing or under-tested?

# Source diff (Pipeline.cs + ParserOutputProjector.cs)

```diff
{src_diff}
```

# New hermetic production-wiring test file (ManifestEnrichmentWiringTests.cs)

```csharp
{new_test}
```

After your prose analysis, output a JSON block in EXACTLY this format:
```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{ "severity": "Critical" | "Major" | "Minor",
       "location": "<file or test name>",
       "summary": "<one-sentence description>",
       "fix": "<recommended fix>" }}
  ]
}}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit this block.
"""

out = SET / "s2-verification.md"
r = route(content=PROMPT, task_type="session-verification", complexity_hint=75,
          session_set=str(SET), session_number=2)
out.write_text(r.content, encoding="utf-8")
print(f"[ok] verifier={r.model_name} {r.input_tokens} in / {r.output_tokens} out / ${r.cost_usd:.4f} -> {out}")
print("="*60)
print(r.content)
