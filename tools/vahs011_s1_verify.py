"""Step 6 cross-provider session-verification for 011 Session 1 (F-3-4).

Routes to a different provider than the Claude orchestrator (gpt-5-4).
Persists raw output to disk BEFORE printing (persist-before-display lesson).
"""
import os
import sys

from ai_router import route

SESSION_SET = "docs/session-sets/011-resolver-and-navigation-wiring"
OUT = os.path.join(SESSION_SET, "s1-verification.md")
ROUND = sys.argv[1] if len(sys.argv) > 1 else "1"
if ROUND != "1":
    OUT = os.path.join(SESSION_SET, f"s1-verification-round-{ROUND}.md")

SRC_DIFF = open(r"C:/temp/vahs011_s1_src.diff", encoding="utf-8").read()
NEWDECL = open(
    "AccessHarvester.Vba.Resolver/DefaultMembers/SyntheticDefaultMemberDeclaration.cs",
    encoding="utf-8").read()

PROMPT = r"""
You are the cross-provider verifier for one session of an AI-led workflow. The
orchestrator is Claude; you are a DIFFERENT provider, giving an independent
review. Be skeptical; prefer a false-positive ISSUES_FOUND over a
false-negative VERIFIED. Your response MUST begin with a detailed prose analysis
of each concern below BEFORE the JSON block. Both are required.

# Session: 011 Session 1 — Resolver Wiring (006 review finding F-3-4)

## What F-3-4 is
The harvester's manifest parse loop built a FRESH per-module `ProjectScope` and a
registry-less, owningClass-less VBA `ReferenceResolver` INSIDE the loop, so three
differentiating, already-hermetically-tested resolver capabilities could never
fire in production output: (1) cross-module Public lookup, (2) `Me`/bare-control
binding in form/report modules, (3) default-member resolution. `SqlStrings` was a
hardcoded empty array.

## What this session implemented (4 items)
1. **Shared ProjectScope (two-pass).** `Pipeline.EmitManifestArtifacts` now builds
   ONE `ProjectScope`; pass 1 scope-builds every module into it, pass 2 resolves
   each — so cross-module Public lookup (which walks `project.Children`) fires.
   Mirrors the in-repo `SymbolDbBuilder` pattern.
2. **AccessObjectModelRegistry + owningClass.** Built once from the enriched ir/
   form/report metadata (`FormMetadataExtractor`/`ReportMetadataExtractor` +
   `TypeLibraryRegistry`); a Form/Report `owningClass` (ClassDeclaration) is passed
   to `BuildModuleScope`; modules resolve with `new ReferenceResolver(registry)`.
   So `Me`, `Me.<control>`, and bare-control bind.
3. **DefaultMemberRegistry consultation.** `DecideMemberOutcome` now consults the
   curated `DefaultMemberRegistry` for a BANG access on a resolved source-typed
   target whose declared type has a PARAMETERIZED default member (`rs!Notes` ≡
   `rs.Fields("Notes")`). Design chosen by cross-provider consensus (GPT-5.4 +
   Gemini Pro both picked it): a `SyntheticDefaultMemberDeclaration` wraps the
   default member and is emitted via the EXISTING `ResolvedToObjectModel` variant
   (no breaking change to the closed `ResolutionOutcome`). It resolves the
   INDIRECTION (the default member, e.g. Recordset.Fields) ONLY — never a
   fabricated symbol for the bang key. Gated on isBang; dot-access unaffected.
4. **SqlStrings decision.** OMIT the key rather than emit the forbidden always-
   empty array (schema marks Body.SqlStrings optional). DynamicSqlAnalyzer wiring
   deferred as register entry DW-8.

## Source diff (the three changed production files)
```diff
%s
```

## New file: SyntheticDefaultMemberDeclaration.cs
```csharp
%s
```

## Hermetic pins added/updated (all green)
- `ManifestResolverWiringTests` (new, pipeline-level, 4 pins): cross-module Public
  call resolves to Procedure; `Me`→Form; `Me.txtName`→ObjectModel control;
  bare-control→ObjectModel control.
- `BangDefaultMemberResolutionTests` (new, resolver-level, 5 pins): bang-on-
  Recordset resolves via Fields default member; an ARBITRARY/typo'd bang key does
  NOT create a symbol named after the key (over-claim guard); scalar-default type
  (TextBox.Value) stays deferred (IsParameterized gate); unregistered type stays
  deferred; DOT access does NOT apply the default member.
- Updated `ResolverFixtureAnnotationTests.Fixture08/Fixture11_BangAccessOnRs_*`:
  these previously asserted `rs!field` = default-member-not-yet-resolved; now
  correctly assert it resolves via the Recordset.Fields default member. (Behavior
  change disclosed; no assertion weakened.)
- `ParserOutputProjectorTests.SqlStrings_OmittedNotEmittedAsEmptyArray` (updated).
- Hermetic suites green: Vba.Resolver.Tests 410, Pipeline.Tests 835,
  SymbolDb.Tests 71, AccessHarvester.Tests F-3-4 pins + ingestion 10. Solution
  builds. Tests run sequentially (no parallel dotnet).

## Coordinated golden regen (HomeHealthCare, COM, TRX-confirmed; gate GREEN)
Only `manifest.json` changed (kb/ir trees unchanged). Structural invariants hold:
object counts (Forms 19 / Reports 12 / Queries 22 / Modules 4 / Tables 13 /
Relationships 12) unchanged; Diagnostics unchanged (still the single known
VBA_PARSE_DEGRADED — basResetFixture, DW-2 — i.e. the two-pass restructure
introduced NO new resolution failures); FileMap/SchemaVersion unchanged;
unresolved-items.json unchanged (still empty — F-2-2 is Session 2).

Reference-level transitions (total refs 5049 → 5049, SAME sites, zero added/
dropped); EVERY transition maps to F-3-4:
- me-in-standard-module → Resolved:Form (541) + Resolved:Report (18) = 559  [item 2 owningClass]
- member-access-on-unresolved-target → Resolved (293)                      [item 2 registry]
- member-access-on-unresolved-target → default-member-not-yet-resolved (400)
      [item 2 cascade: Me.control now resolves, exposing the deeper control-
       property/type-inference gap (Phase 3.6, out of scope) as the honest
       default-member reason — the spec explicitly predicted this surfacing]
- default-member-not-yet-resolved → Resolved:DefaultMember (37)            [item 3 rs!field]
- default-member-not-yet-resolved → Resolved (14)                         [item 2/3 registry member]
- not-in-scope → Resolved:Procedure (7) + Resolved (4) = 11               [item 1 cross-module]
Net: Resolved 3182 → 4103 (+921); Unresolved 2306 → 1392.

Example DefaultMember target (honest — no fabricated field symbol):
`rs!City` → ResolutionStatus "Resolved", ReferenceKind "ObjectModel",
ResolvedTarget { Kind:"DefaultMember", Name:"Fields", Container:"Recordset",
FullyQualifiedName:"Recordset.Fields(\"City\")", Type:"Field" }.

## Hard constraints honored
NorthwindDevEdition never touched; golden regen HomeHealthCare-only via the
established AH_REGEN_GOLDEN path; COM run foreground/sequential/TRX-as-truth;
spurious samples/*.accdb touch swept (git checkout) before staging; no assertion
weakened (behavior changes disclosed); quarantined sanitize test untouched.

## Concerns to verify (analyze each in prose first)
A. **Two-pass correctness.** Is deferring resolution to pass 2 (after all module
   scopes are registered in the shared ProjectScope) correct, and does the new
   `VBA_RESOLVE_FAILURE` per-module catch preserve the prior per-module
   degradation contract (kind-correct stub for ModulesAndClasses, no whole-
   manifest abort)?
B. **owningClass name alignment.** owningClass.Name = module name = kb file name =
   FormMetadata.Name; the registry's TryResolveControlOnForm falls back to its
   by-Name index. Is this alignment sound for forms whose ModuleName
   (`Form_<name>`) differs from Name? Any case it silently misses?
C. **Default-member design (item 3).** Is emitting via ResolvedToObjectModel with a
   per-site (non-cached) SyntheticDefaultMemberDeclaration safe for CrossReference
   back-population? Is the isBang + IsParameterized gating correct (no false
   resolutions)? Does the projection over-claim anything?
D. **SqlStrings omission.** Is omitting (vs empty array) the right call under the
   "only emit what the producer wrote / no synthesized defaults" convention, given
   the schema marks Body.SqlStrings optional?
E. **Golden deltas.** Are all transitions genuinely explained by F-3-4, or is any
   transition (especially the 400 member-access→default-member) evidence of a
   regression rather than an honest deeper-gap surfacing?

After your analysis, output a JSON block in this exact format:
```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    { "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>" }
  ]
}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit this block.
""" % (SRC_DIFF, NEWDECL)


def main() -> None:
    result = route(content=PROMPT, task_type="session-verification",
                   complexity_hint=70, session_set=SESSION_SET, session_number=1)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(result.content)
    print(f"Wrote {OUT} | model={result.model_name} | cost=${result.cost_usd:.4f}")


if __name__ == "__main__":
    main()
