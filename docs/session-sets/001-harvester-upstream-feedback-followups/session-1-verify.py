"""Session 1 verification: cross-provider session verification.

Routes via task_type="session-verification" (cross-provider second opinion for a
Claude/Opus orchestrator). The verifier receives:
  - The Session 1 spec excerpt
  - The git diff of the usage-side code changes
  - The new + amended test files
  - The strategy-doc contract diff
  - The before/after identify-usage pilot evidence (committed HHC golden IR)
  - Build/test results
  - The required trailing JSON verdict block
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "001-harvester-upstream-feedback-followups"

CODE_FILES = [
    "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs",
    "AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs",
    "AccessHarvester.Pipeline/UsageIdentification/SignalIndex.cs",
    "AccessHarvester.Pipeline/UsageIdentification/UsageFinding.cs",
    "AccessHarvester.Pipeline/UsageIdentification/object-use-schema.json",
    "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/QuestionGeneratorTests.cs",
    "docs/planning/object-use-identification-strategy.md",
]
NEW_FILES = [
    "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/LauncherSurfaceTests.cs",
]

SPEC_EXCERPT = """### Session 1 of 3: Launcher Surfaces + Out-Of-Band Entry Uncertainty

In scope: usage-classification improvements so switchboards, menu forms, startup
forms, and related launcher helpers (helper macros, switchboard tables) are
recognized as navigation surfaces even with no record source and no harvested
opener; and explicit out-of-band entry uncertainty (startup, direct navigation,
ribbon) in usage output and checklist questions. The core rule: lack of an opener
is an uncertainty to explain, not a cleanup hint.

Out of scope (later sessions / other sets):
- Parameterized-query and parameter-dialog promotion to first-class signals
  (set 001 Session 2). frmReportParam_* forms must stay Unknown this session.
- Reopening cleanup semantics (already narrowed to positive stale-like signals).
- Formal regeneration of the committed usage golden + checklist-casing
  verification against a fresh run (set 001 Session 3).

Stop rules: do not treat missing inbound references as deletability evidence; do
not fold duplicate-table-shape or obsolete-module-header heuristics into this set.
"""

PILOT = """Before/after of `identify-usage` run against the committed HomeHealthCare
golden IR (`samples/GoldenOutput/HomeHealthCare/ir`, run into a temp dir — the
committed golden was deliberately NOT regenerated; that is Session 3's scope):

  object                          before            -> after
  frmSwitchboard                  Unknown 0.35      -> NavigationShell 0.85   (0 questions)
  frmReportMenu                   Unknown 0.35      -> NavigationShell 0.85   (0 questions)
  mcrSwitchboardActions           NavigationShell   -> NavigationShell 0.75   (unchanged)
  tblSwitchboardItems             unexplained       -> StandaloneUtility 0.65 (launcher-tied, no 'unreferenced' question)
  frmReportParam_Client           Unknown 0.35      -> Unknown 0.35           (correctly left for Session 2)
  qryProviderPlansByClient_Param  Reporting 0.65    -> Reporting 0.65 + OutOfBandEntryPossible question

frmSwitchboard usageSummary: "Navigation/launcher form 'frmSwitchboard' — a
switchboard/menu shell that routes to other forms or reports (directly or via a
navigation macro)". LauncherSurface signal fires. Checklist top-level keys remain
canonical PascalCase (ProjectName/Review/FunctionalArea/...).
"""

BUILD_TEST = """Full-solution `dotnet build`: 0 errors (only pre-existing CA1416 / NU1903 /
xUnit analyzer warnings, unchanged from baseline).
`dotnet test` AccessHarvester.Pipeline.Tests (hermetic): 807 passed, 0 failed
(790 baseline + 17 net new). The full integration suite has pre-existing
HomeHealthCare COM-CLI timeouts unrelated to this hermetic, Pipeline-only change
(documented in the cleanup/use sibling sets' actuals)."""

R3_FIX = """ROUND 4 (human-authorized override of the 2-retry cap, to confirm a clean
VERIFIED). Round 3 (gpt-5-4) returned ISSUES_FOUND with two Minor findings, now fixed.

R3 Issue 1 (Minor, UsageClassifier.cs BuildSummary/ClassifyMacro) — launcher-aware
summary text was applied to ALL NavigationShell forms and ALL edge-backed macros,
overstating switchboard/menu/launcher traits for generic navigation objects.
  FIX: launcher wording is now conditional on actual launcher evidence.
    - Forms: NavigationShell uses the "Navigation/launcher ... switchboard/menu
      shell" wording only when IsLauncherFormName(name) || IsStartupOpened(name)
      (F5b); a generic F5 nav form (edges only) keeps the neutral
      "Navigation/shell form '<name>' that opens other forms or reports".
    - Macros: "...for a launcher surface" only when launcher-named AND edge-backed;
      a generic edge-backed macro keeps neutral "containing navigation actions that
      open forms or reports"; name-only stays "...named like a switchboard/menu
      launcher helper (no navigation actions parsed)".

R3 Issue 2 (Minor, tests) — no positive Query out-of-band regression.
  FIX: added Query_With_No_Harvested_Opener_Gets_OutOfBand_Question (a parameterized
  Reporting query, not PossiblyUnused, no opener -> OutOfBandEntryPossible present,
  NoNavigationEntry absent). Also added neutral-summary regressions for a generic
  edge-backed nav form and a generic edge-backed macro."""

R2_FIX = """ROUND 3 update. Round 2 (gpt-5-4) confirmed the two R1 Major fixes and the added
boundary tests, and returned ISSUES_FOUND with a single new Minor finding, now fixed.

R2 Issue (Minor, UsageClassifier.cs / ClassifyMacro + tests) — the name-only
launcher-macro fallback (a switchboard/menu-named macro with no parsed navigation
edges) used the same summary as an edge-backed macro, overstating evidence by
claiming it "contains navigation actions".
  FIX: ClassifyMacro now words the summary by evidence source —
    - parsed nav edges -> "...containing navigation actions that open forms or
      reports for a launcher surface" (unchanged),
    - name-only        -> "...named like a switchboard/menu launcher helper
      (no navigation actions parsed)" (cautious, does not overstate),
    - neither          -> "...providing automated actions".
  Added a regression test (mcrSwitchboardHelper, empty Actions) asserting
  NavigationShell, LauncherSurface fired, and the cautious summary wording; and
  strengthened the edge-backed macro test to assert the "containing navigation
  actions" wording so the two paths stay distinct."""

R1_FIXES = """This is verification ROUND 2. Round 1 (gpt-5-4) returned ISSUES_FOUND with
two Major and one Minor finding; all three are now fixed in the diff above.

R1 Issue 1 (Major, QuestionGenerator.cs) — HasHarvestedOpener ignored inbound
query-SQL references, so an intermediate query consumed only by another query's
SQL was wrongly told "no harvested opener was found".
  FIX: HasHarvestedOpener now also returns true when index.UsedInQuerySql contains
  the object. A query referenced by another query's FROM/JOIN is an in-graph
  dependency and no longer gets OutOfBandEntryPossible.

R1 Issue 2 (Major, UsageClassifier.cs / BuildSignals) — the form LauncherSurface
signal fired for any startup-opened / launcher-named form regardless of binding,
so a bound form could classify MainDataEntry/Reporting while emitting
LauncherSurface=true; and launcher-chrome evidence was never emitted.
  FIX: the form LauncherSurface evidence is now gated on no-RecordSource (mirrors
  F5b exactly), so it agrees with the category. StartupEntry remains a standalone
  positive signal for all object types (a bound startup-opened form still reports
  StartupEntry=true, which is honest). When launcher chrome corroborates a primary
  name/startup signal, "launcher-chrome" is now emitted in the LauncherSurface
  evidence. Threaded via a trailing optional `launcherChrome` param on
  BuildFinding/BuildSignals (default false; only the form path passes it).

R1 Issue 3 (Minor, tests) — missing boundary coverage.
  FIX: added four tests — (a) a bound launcher-named form
  (frmClientMenu + RecordSource) stays off F5b and fires no LauncherSurface;
  (b) a startup-opened BOUND form reports StartupEntry but not LauncherSurface
  and is not NavigationShell; (c) launcher-chrome is asserted present in the
  LauncherSurface evidence; (d) a query consumed by another query's SQL gets no
  OutOfBandEntryPossible, while a truly-unreferenced query gets the reframed
  UnreferencedObject instead.

Note on the R1 prose remark that the pilot's 0.85 confidence did not match the
F5b base of 0.80: this is not a defect. AdjustConfidence adds +0.05 for a
corroborating VBA signal on top of the F5b base (0.80 name+chrome -> 0.85), which
is the same confidence-adjustment step applied to every rule. No change made; the
test pins the launcher-chrome case at >= 0.8.
"""


def git_diff(paths: list[str]) -> str:
    return subprocess.run(
        ["git", "diff", "--", *paths],
        cwd=REPO_ROOT, capture_output=True, text=True, encoding="utf-8",
    ).stdout


def main() -> int:
    log = SessionLog(SESSION_SET)

    diff = git_diff(CODE_FILES)
    new_files = "\n\n".join(
        f"# NEW FILE: {p}\n\n```csharp\n{(REPO_ROOT / p).read_text(encoding='utf-8')}\n```"
        for p in NEW_FILES
    )

    prompt = f"""You are verifying Session 1 of the `001-harvester-upstream-feedback-followups`
session set for the Dabbler Access Harvester (a C#/.NET metadata harvester whose
downstream consumer is an AI, not a human). This session tightens the usage-side
explanation layer; it does not touch COM extraction or the cleanup pipeline.

# Session 1 spec excerpt

{SPEC_EXCERPT}

# What changed (git diff of usage-side code, schema, and the strategy contract)

```diff
{diff}
```

# New test file

{new_files}

# Before/after pilot evidence

{PILOT}

# Build and test results

{BUILD_TEST}

# What changed across the verification rounds

{R1_FIXES}

{R2_FIX}

{R3_FIX}

# What to verify

1. **Launcher recognition is correct and bounded.** A no-RecordSource form that
   matches a switchboard/menu/startup name, or is opened by an AutoExec startup
   macro, is classified NavigationShell even with no navigation edges of its own
   (the F5b rule). Confirm launcher chrome only *corroborates* (it never
   classifies a form on its own), so a stripped-chrome parameter dialog
   (frmReportParam_*) is NOT misclassified as a launcher — that belongs to
   Session 2. Flag any false-positive risk (e.g., a data form whose name contains
   "Menu" but which has a RecordSource — confirm the RecordSource branch wins).

2. **Out-of-band reframing is honest and not a staleness signal.** OutOfBandEntryPossible
   and the reworded UnreferencedObject must frame a missing opener as out-of-band
   uncertainty (startup/direct/ribbon), never imply deletability. Confirm the
   firing scope (reports/queries always; forms only when bound) does not double up
   with NoRecordSource on unbound forms, and does not fire for NavigationShell or
   PossiblyUnused-vs-OutOfBand overlaps. Confirm NoNavigationEntry is retired but
   retained in the schema enum for backward compatibility (mirrors the
   CrossReferenceAbsence precedent).

3. **Helper artifacts are tied to the launcher.** The switchboard macro is
   NavigationShell; the switchboard-data table is StandaloneUtility with a
   LauncherSurface signal and a launcher-aware summary, and gets NO
   UnreferencedObject question. Confirm tblSwitchboardItems can no longer read as
   PossiblyUnused solely for lacking inbound references.

4. **Signal/schema consistency.** The new signal types (LauncherSurface,
   StartupEntry) and uncertainty signal (OutOfBandEntryPossible) are in the JSON
   schema enums, and BuildSignals emits LauncherSurface/StartupEntry whose Fired
   state agrees with the classifier's category decision. Flag any case where the
   signal and the category could disagree.

5. **Scope discipline.** No parameter-dialog/parameterized-query promotion (Session
   2); no cleanup-semantics changes; the committed usage golden is intentionally
   NOT regenerated (Session 3). Confirm nothing crosses those lines.

6. **Test adequacy.** The regression tests cover: switchboard form delegating to a
   macro, the helper macro, the helper table (not PossiblyUnused), a modal report
   menu, a startup-opened form, the chrome-only negative guard, and the
   out-of-band question cases. Flag any uncovered claim from concerns 1–4.

# Response format

Provide a detailed prose analysis for each of the 6 concerns BEFORE the JSON
block. Then output a JSON block in exactly this format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or section>",
      "summary": "<one sentence>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit the block.
"""

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    log.save_session_review(session_number=1, review_text=result.content, round_number=4)
    log.log_step(
        session_number=1,
        step_number=7,
        step_key="session-1/verify-round-4",
        description="Cross-provider session verification round 4 (human-authorized cap override, after R3 fixes)",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )

    print(f"verifier={result.model_name} in={result.input_tokens} "
          f"out={result.output_tokens} cost=${result.cost_usd:.4f}")
    print("----- review head -----")
    print(result.content[-1600:])
    return 0


if __name__ == "__main__":
    sys.exit(main())
