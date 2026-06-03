"""Session 2 verification: cross-provider session verification.

Routes via task_type="session-verification" (cross-provider second opinion for a
Claude/Opus orchestrator). The verifier receives:
  - The Session 2 spec excerpt (in/out of scope + calibration note)
  - The git diff of the usage-side code + schema + strategy-doc contract
  - The new ParameterDialogTests file
  - The before/after identify-usage pilot evidence (committed HHC golden IR)
  - Build/test results
  - Out-of-scope disclaimers (reused/unchanged code, golden not regenerated, etc.)
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
    "AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs",
    "AccessHarvester.Pipeline/UsageIdentification/IrLoader.cs",
    "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs",
    "AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs",
    "AccessHarvester.Pipeline/UsageIdentification/SignalIndex.cs",
    "AccessHarvester.Pipeline/UsageIdentification/UsageFinding.cs",
    "AccessHarvester.Pipeline/UsageIdentification/object-use-schema.json",
    "docs/planning/object-use-identification-strategy.md",
]
NEW_FILES = [
    "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/ParameterDialogTests.cs",
]

SPEC_EXCERPT = """### Session 2 of 3: Parameterized Queries + Parameter-Dialog Flows

In scope: widen the usage-side IR so saved-query parameter metadata survives load
into the classifier; add explicit signals for prompt-style/bracketed parameters,
declared PARAMETERS blocks, and other evidence that a query/report expects runtime
input; add heuristics for likely parameter-collection forms and report-open/
query-open flows when the evidence is present; teach the question generator and
checklist writer to express parameter uncertainty distinctly from generic unknown
intent; add regression tests.

Done looks like: parameterized queries emit explicit parameter-related signals,
and likely parameter-dialog flows are distinguishable from generic unknown forms.
frmReportParam_* forms were intentionally left Unknown after Session 1 and are
Session 2's primary target.

Out of scope (later session / other sets): reopening cleanup semantics; the
committed usage golden is NOT regenerated this session (Session 3); duplicate-
table-shape and obsolete-module-header heuristics.

Session 1 calibration note carried into Session 2: any new parameter signal/summary
MUST be gated on the rule that actually fired and threaded as an explicit flag,
never recomputed from the object name in BuildSignals/BuildSummary.
"""

PILOT = """Before/after of `identify-usage` run against the committed HomeHealthCare golden
IR (`samples/GoldenOutput/HomeHealthCare/ir`, run into a temp dir — the committed
golden was deliberately NOT regenerated; that is Session 3's scope). 74 objects
classified, exit 0.

  object                          before (traced)              -> after
  frmReportParam_Client (x5 forms) Unknown 0.30 (F6),          -> Unknown 0.45, ParameterizedInput ON
    incl _ClientFY/_FY/_Provider/   NoRecordSource +               [param-dialog-name-pattern, dialog-chrome,
    _ProviderFY                     UnrecognizedVbaPattern Qs        opened-as-dialog], question=ParameterDialogFlow
                                                                   (generic NoRecordSource/UnrecognizedVba suppressed)
  qryProviderPlansByClient_Param  Reporting 0.65, no param sig -> Reporting 0.65, ParameterizedInput ON
                                                                   [declared-parameter:Enter Provider ID:,
                                                                    declared-parameter:Enter Fiscal Year:],
                                                                   questions = OutOfBandEntryPossible + ParameterizedQuery
  qryClientServicePlan_ByClient   Reporting 0.65, no param sig -> Reporting 0.65, ParameterizedInput ON
                                                                   [declared-parameter:Enter Client ID:], same questions
  frmReportMenu (launcher)        NavigationShell 0.85         -> NavigationShell 0.85, ParameterizedInput off (launcher
                                                                   wins over F8 — unchanged from Session 1)

The frmReportParam_* forms are opened as dialogs by reports via OnOpen embedded
macros (e.g. rptClientProfile -> frmReportParam_Client), which is why they are
navigation targets and carry the `opened-as-dialog` corroborating evidence.
"""

BUILD_TEST = """`dotnet build AccessHarvester.sln`: 0 errors (only pre-existing CA1416 / NU1903
warnings, unchanged from baseline).
`dotnet test AccessHarvester.Pipeline.Tests` hermetic (excluding the Access-COM
ComDateMaximaProbe tier): 814 passed, 0 failed, 0 skipped (was 814 before adding
the 12 new ParameterDialogTests + refactor; net new tests included). The
UsageIdentification slice specifically: 118/118 passing, including all Session 1
LauncherSurfaceTests (unchanged) and the 12 new ParameterDialogTests."""

DISCLAIMERS = """OUT-OF-SCOPE / CONTEXT NOT IN THE DIFF (to prevent false 'missing code' findings):

1. `QueryMetadataExtractor.ParseParameters` lives in `AccessHarvester.Access`
   (project already referenced by AccessHarvester.Pipeline) and is REUSED UNCHANGED
   — it is the canonical, tested regex scan for a leading `PARAMETERS <name> <type>;`
   clause and `[Forms]![X]![Y]` / `[Reports]![X]![Y]` control references. This
   session does NOT re-implement parameter parsing; IrLoader.ExtractQueryParameters
   maps its result onto the new IrQuery.Parameters field. The extractor is not in
   the diff because it was not changed.

2. IrQuery.Parameters is populated at LOAD (IrLoader.ExtractQueryParameters), so
   saved-query parameter metadata survives into the classifier rather than being
   re-parsed there. In-memory unit-test fixtures populate IrQuery.Parameters via the
   same public helper so they match the real load path.

3. The committed HomeHealthCare usage golden under
   `samples/GoldenOutput/HomeHealthCare/usage/` is INTENTIONALLY NOT regenerated this
   session — golden regeneration + checklist-casing verification is Session 3's
   scope. The pilot above was run into a temp dir; no golden was overwritten.

4. Reports are intentionally NOT given a first-class ParameterizedInput signal this
   session (scope discipline). The report-open parameter flow is captured from the
   FORM side: a parameter-dialog form that is a harvested navigation target carries
   `opened-as-dialog` evidence. A report still classifies as Reporting as before.

5. The legacy Q5 parameter-marker CATEGORY path (UsageClassifier.ContainsParamBracket
   driving Reporting 0.65) is UNCHANGED — no query category regression. The new
   ParameterizedInput signal is additive and independent of the category branch.

6. ParameterizedQuery is deliberately additive and does NOT suppress the Session 1
   OutOfBandEntryPossible question on a parameterized query: the two probe different
   things (parameter source vs. entry path), and suppression would have regressed the
   Session 1 test `Query_With_No_Harvested_Opener_Gets_OutOfBand_Question`.
"""


R1_FIXES = """This is verification ROUND 2. Round 1 (gpt-5-4) returned ISSUES_FOUND with two
Minor test-coverage findings (no behavioral defects — concerns 1-5 were confirmed
correct). Both are now fixed in the test file above:

R1 Issue 1 (Minor, tests) — no regression for the `[Reports]!` report-control-
reference parameter path.
  FIX: added Report_Control_Reference_Query_Fires_ParameterizedInput — a query whose
  SQL references [Reports]![rptFiscal]![txtYear]; asserts ParameterizedInput fires
  with report-control-reference evidence and the ParameterizedQuery question.

R1 Issue 2 (Minor, tests) — no regression for the ODBC `?` marker path.
  FIX: added Odbc_Marker_Query_Fires_ParameterizedInput — a query with a `?` marker;
  asserts ParameterizedInput fires with odbc-parameter-marker evidence and the
  ParameterizedQuery question (Q5 category behavior unchanged).

No production code changed between R1 and R2; the two additions are test-only.
ParameterDialogTests now 13/13; full hermetic Pipeline.Tests still green.
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

    prompt = f"""You are verifying Session 2 of the `001-harvester-upstream-feedback-followups`
session set for the Dabbler Access Harvester (a C#/.NET metadata harvester whose
downstream consumer is an AI, not a human). This session adds parameterized-query
and parameter-dialog recognition to the usage-identification layer; it does not
touch COM extraction or the cleanup pipeline.

Your response MUST begin with a detailed prose analysis for each of the 6 concerns
below BEFORE the JSON block. Do not skip the analysis. Both the analysis and the
JSON block are required.

# Session 2 spec excerpt

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

# Changes since Round 1

{R1_FIXES}

# {DISCLAIMERS}

# What to verify

1. **Parameterized-query signal: correctness and independence from category.**
   ParameterizedInput must fire for declared PARAMETERS, `[Forms]!`/`[Reports]!`
   control references, inline bracketed prompts, and ODBC `?` markers, computed
   independently of the Q2–Q7 category branch so a parameterized query used as a
   report RecordSource (Q3) STILL surfaces it (the core upstream-feedback gap).
   Confirm the declared/reference evidence is carried on IrQuery.Parameters at load
   (reusing the Access extractor, not re-implemented). Flag false-positive risk
   (e.g. a bracketed field name with a space like `[Order Date]`).

2. **Parameter-dialog form recognition (F8) is evidence-gated and bounded.** An
   unbound form whose name matches `*Param*`/`*Filter*`/`*Criteria*`/`*Dialog*` is
   recognized (rule F8). Confirm the RecordSource branches and the launcher rules
   (F5/F5b) win FIRST (a bound `*Param*` form stays data entry; a launcher/menu-named
   form is NavigationShell, not a param dialog), and that the ParameterizedInput
   signal + parameter-dialog summary are threaded via an explicit flag (paramEvidence)
   rather than recomputed from the name in BuildSignals/BuildSummary — the Session 1
   discipline. Flag any name-recomputation or boundary leak.

3. **Category discipline (no over-reach).** F8 keeps the Unknown category per the
   strategy-doc F8 contract ("mark question; it does not assign a positive
   category"). "Distinguishable from a generic unknown form" is achieved via the
   fired ParameterizedInput signal + the parameter-dialog summary + the specific
   ParameterDialogFlow question — NOT a new taxonomy category. Confirm no 8th
   classification enum value was introduced and that this is the right call.

4. **Question generation and suppression.** For a recognized parameter dialog,
   ParameterDialogFlow must REPLACE (suppress) the now-redundant NoRecordSource and
   UnrecognizedVbaPattern questions; a GENERIC unbound VBA-only form (no param name)
   must still get those generic questions (suppression is specific). For a query,
   ParameterizedQuery is ADDITIVE and deliberately does not suppress
   OutOfBandEntryPossible. Flag any redundant, missing, or contradictory question.

5. **Signal / schema / summary consistency.** ParameterizedInput is in the schema
   signal enum; ParameterizedQuery and ParameterDialogFlow are in the uncertainty
   enum; emitted values match the schema (the CLI ran clean). The parameter-dialog
   summary is gated on the threaded flag so it agrees with the F8 decision (mirrors
   the Session 1 lesson: signal/summary must agree with the decision that fired, not
   be re-derived from the name). Flag any case where the signal/summary could
   disagree with the actual rule.

6. **Scope discipline and test adequacy.** No cleanup-pipeline changes, no COM
   changes, golden NOT regenerated (Session 3), no duplicate-table/obsolete-module
   heuristics. The tests should cover: a declared-PARAMETERS query, a parameterized
   query used as a report RecordSource (independence), a form-control-reference
   query, a plain non-parameterized query (negative), an inline-bracket query that
   keeps OutOfBand, parameter-dialog recognition with suppression, opened-as-dialog
   corroboration, a filter-named form without chrome, a bound `*Param*` negative
   guard, launcher-wins-over-F8, and a generic unknown form still getting generic
   questions. Flag any uncovered claim from concerns 1–5.

# Response format

Provide a detailed prose analysis for each of the 6 concerns BEFORE the JSON block.
Then output a JSON block in exactly this format:

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
        session_number=2,
    )

    log.save_session_review(session_number=2, review_text=result.content, round_number=2)
    log.log_step(
        session_number=2,
        step_number=8,
        step_key="session-2/verify-round-2",
        description="Cross-provider session verification round 2 (after R1 test-coverage fixes: report-ref + ODBC marker tests)",
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
    print("----- review tail -----")
    print(result.content[-2000:])
    return 0


if __name__ == "__main__":
    sys.exit(main())
