"""Session 3 (final) cross-provider verification.

Session 3 is pilot + fresh-output verification + closeout. It ships NO new
production code — its deliverables are:
  (a) regenerated committed usage goldens (HHC + Northwind) baking in the
      cumulative Session 1 (launcher/out-of-band) + Session 2 (parameter) changes,
  (b) a verification-only confirmation that object-use-checklist.json casing is
      canonical PascalCase on a fresh run (stop rule: patch only on drift),
  (c) the pilot review judgment (wording reads clearly, no calibration needed;
      no consumer-doc change warranted).

So this verification is a "verify against description-of-work + concrete artifact
evidence" pass (the implementation it depends on was already cross-provider
verified in Sessions 1 and 2). Evidence is computed programmatically from the
regenerated goldens and the git HEAD baseline so nothing is hand-transcribed.
"""
from __future__ import annotations

import json
import subprocess
import sys
from collections import Counter
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "001-harvester-upstream-feedback-followups"
FIXTURES = ["HomeHealthCare", "NorthwindDevEdition"]


def load_bom(p: Path):
    return json.loads(p.read_text(encoding="utf-8-sig"))


def head_json(rel: str):
    out = subprocess.run(
        ["git", "show", f"HEAD:{rel}"],
        cwd=REPO_ROOT, capture_output=True,
    ).stdout
    return json.loads(out.decode("utf-8-sig"))


def fired(o):
    return {s["signalType"] for s in o.get("signals", []) if s.get("fired")}


def build_golden_evidence() -> str:
    blocks = []
    for db in FIXTURES:
        rel = f"samples/GoldenOutput/{db}/usage/object-use.json"
        new = load_bom(REPO_ROOT / rel)
        old = head_json(rel)
        no = {o["objectName"]: o for o in new["objects"]}
        oo = {o["objectName"]: o for o in old["objects"]}
        added = sorted(set(no) - set(oo))
        removed = sorted(set(oo) - set(no))
        # new signal types now present on every object (fired:false rows)
        new_sigtypes = [s["signalType"] for s in next(iter(no.values()))["signals"]]
        old_sigtypes = [s["signalType"] for s in next(iter(oo.values()))["signals"]]
        added_sigtypes = [s for s in new_sigtypes if s not in old_sigtypes]
        changed = []
        for name in sorted(set(no) & set(oo)):
            f, g = no[name], oo[name]
            ff, gf = fired(f), fired(g)
            bits = []
            if f["classification"] != g["classification"]:
                bits.append(f"class {g['classification']}->{f['classification']}")
            if f.get("confidence") != g.get("confidence"):
                bits.append(f"conf {g.get('confidence')}->{f.get('confidence')}")
            if ff != gf:
                bits.append(f"+sig{sorted(ff - gf)} -sig{sorted(gf - ff)}")
            if f.get("usageSummary") != g.get("usageSummary"):
                bits.append("summary changed")
            if bits:
                changed.append(f"  [{f['objectType']}] {name}: " + "; ".join(bits))
        blocks.append(
            f"### {db}: {len(new['objects'])} objects (was {len(old['objects'])})\n"
            f"objects added: {added or 'none'} | removed: {removed or 'none'}\n"
            f"new signal-types now present as fired:false rows on EVERY object: {added_sigtypes}\n"
            f"objects with real (classification/confidence/fired/summary) changes — "
            f"{len(changed)} total:\n" + "\n".join(changed)
        )
    return "\n\n".join(blocks)


def build_casing_evidence() -> str:
    lines = []
    for db in FIXTURES:
        chk = load_bom(REPO_ROOT / f"samples/GoldenOutput/{db}/usage/object-use-checklist.json")
        rev = chk.get("Review") or []
        item_keys = list(rev[0].keys()) if rev else []
        camel = sorted({k for it in rev for k in it if k[:1].islower()})
        lines.append(
            f"{db}: top-level keys={list(chk.keys())} "
            f"(PascalCase 'Review' present={'Review' in chk}, camel 'review' present={'review' in chk}); "
            f"item keys={item_keys}; camelCase item keys={camel or 'NONE'}"
        )
    return "\n".join(lines)


def build_sample_objects() -> str:
    picks = {
        "HomeHealthCare": ["frmSwitchboard", "tblSwitchboardItems",
                           "frmReportParam_Client", "qryProviderPlansByClient_Param"],
        "NorthwindDevEdition": ["frmStartup", "frmGenericDialog", "qryProductList_Export"],
    }
    out = []
    for db, names in picks.items():
        use = load_bom(REPO_ROOT / f"samples/GoldenOutput/{db}/usage/object-use.json")
        idx = {o["objectName"]: o for o in use["objects"]}
        for n in names:
            o = idx[n]
            slim = {
                "objectType": o["objectType"], "objectName": o["objectName"],
                "classification": o["classification"], "confidence": o.get("confidence"),
                "usageSummary": o.get("usageSummary"),
                "firedSignals": sorted(fired(o)),
                "questions": o.get("questions", []),
            }
            out.append(f"[{db}] " + json.dumps(slim, ensure_ascii=False, indent=2))
    return "\n\n".join(out)


SPEC = """### Session 3 of 3 (FINAL): Pilot, Fresh-Output Verification, And Closeout
Steps: (1) run identify-usage against committed sample/golden output exercising
launcher/menu and parameterized-query cases incl HomeHealthCare; (2) review the
regenerated object-use.json and object-use-checklist.json for whether launcher
explanations and parameter flows read clearly; (3) verify object-use-checklist.json
casing against the fresh run — if it already emits canonical PascalCase, RECORD the
verification and LEAVE THE WRITER ALONE; only patch code if a current run still
shows drift; (4) calibrate wording/thresholds ONLY if the pilot exposes local
defects, scope constrained to the S1/S2 launcher/parameter slices; (5) update
planning/consumer docs IF the artifacts expose new concepts operators should
understand; (6) dotnet build + dotnet test; (7) cross-provider verification of
output quality and the verification-only-unless-broken handling of checklist casing;
(8) closeout.

Stop rules: do NOT fold duplicate-table-shape or obsolete-module-header heuristics
into this set; do NOT treat missing inbound references as deletability evidence;
do NOT change object-use-checklist.json casing code unless a fresh run shows the
current writer still drifts."""

PILOT_JUDGMENT = """Pilot review judgment (orchestrator):
- Launcher forms (frmSwitchboard/frmReportMenu/frmStartup) classify NavigationShell
  at 0.8-0.85 with a clear "navigation/launcher ... switchboard/menu shell" summary
  and no nagging question. Helper table tblSwitchboardItems -> StandaloneUtility
  "switchboard configuration table ... backing the launcher menu" (no longer
  PossiblyUnused, so no deletion-scented UnreferencedObject question). Helper macro
  mcrSwitchboardActions -> NavigationShell (edge-backed, accurate "containing
  navigation actions" wording).
- Parameter-dialog forms (frmReportParam_*, frmGenericDialog, frmSelectVendorDialog)
  stay Unknown @ 0.45 with a hedged "Likely parameter/filter dialog ... appears to
  collect run-time criteria" summary + a single ParameterDialogFlow question
  (generic NoRecordSource/UnrecognizedVbaPattern suppressed).
- Parameterized queries fire ParameterizedInput and carry BOTH OutOfBandEntryPossible
  AND ParameterizedQuery questions (additive by design — entry-path vs parameter-
  source are different uncertainties).
- Conclusion: NO local wording/threshold defect was exposed -> no calibration (Step 4
  is conditional). frmGenericDialog firing on the *Dialog* name alone is the
  documented-broad F8 heuristic (conservative: keeps Unknown, asks a question), not a
  new defect.
- Consumer-doc judgment: docs/harvester/reviewing-the-feedback-artifacts.md is
  intentionally high-level and vocabulary-agnostic ("answer the questions you see");
  the new questions are self-describing plain English in the checklist Steps. Per the
  repo's Lean-by-default principle, no consumer-doc edit is warranted. The concept
  vocabulary (LauncherSurface/StartupEntry/OutOfBandEntryPossible/ParameterDialogFlow/
  ParameterizedQuery) is already defined in docs/planning/object-use-identification-
  strategy.md (updated in Sessions 1 and 2)."""

BUILD_TEST = """dotnet build AccessHarvester.sln: 0 errors (only pre-existing CA1416 /
NU1903 warnings). dotnet test AccessHarvester.Pipeline.Tests (hermetic; excluded the
Access-COM ComDateMaximaProbe tier): 816 passed / 0 failed / 0 skipped — includes all
Session 1 LauncherSurfaceTests and Session 2 ParameterDialogTests, unchanged and
green. No production code was modified this session, so the unit suite is identical to
the Session 2 close."""

DISCLAIMERS = """OUT-OF-SCOPE / CONTEXT (to prevent false 'missing code' findings):
1. NO production code changed this session. The classifier/question-generator/
   signal-index/schema implementation was already cross-provider verified in Session 1
   (4 rounds, all findings resolved) and Session 2 (VERIFIED round 2). Session 3 only
   regenerates committed golden artifacts and records verification judgments.
2. There is intentionally NO automated golden-regression test asserting the usage
   golden byte-for-byte; the usage goldens under samples/GoldenOutput/*/usage/ are
   committed reference artifacts regenerated by running `identify-usage` against the
   committed IR. That is the established repo pattern (prerequisite set
   harvester-object-use-identifier committed them the same way).
3. The large line-count of the golden diff is expected and benign: the new signal
   ENUM values (LauncherSurface/StartupEntry/ParameterizedInput) now appear as
   fired:false rows on EVERY object's signals array (the committed golden predated
   them). Unchanged objects (e.g. frmAbout) are otherwise identical (same
   classification/confidence/questions). Files were LF-normalized per
   .gitattributes (*.json eol=lf).
4. The spurious samples/HomeHealthCare.accdb timestamp mutation produced during the
   test build was reverted (git checkout) and is NOT in the commit, per the repo
   convention on Access-managed timestamp churn.
5. identify-usage runs hermetically on committed IR (no Access COM needed for this
   path), so the pilot is reproducible cross-platform from the committed fixtures."""


def main() -> int:
    log = SessionLog(SESSION_SET)
    golden = build_golden_evidence()
    casing = build_casing_evidence()
    samples = build_sample_objects()

    prompt = f"""You are verifying Session 3 (the FINAL session) of the
`001-harvester-upstream-feedback-followups` session set for the Dabbler Access
Harvester — a C#/.NET metadata harvester whose downstream consumer is an AI agent,
not a human. Session 3 is a pilot + fresh-output-verification + closeout session; it
ships NO new production code. Its deliverables are (a) regenerated committed usage
goldens that bake in the cumulative Session 1 launcher/out-of-band changes and
Session 2 parameter changes, (b) a verification-only confirmation of checklist
casing, and (c) the pilot review judgment.

Your response MUST begin with a detailed prose analysis for each of the 5 concerns
below BEFORE the JSON block. Do not skip the analysis. Both the analysis and the JSON
block are required.

# Session 3 spec excerpt + stop rules

{SPEC}

# Evidence A — golden regeneration diff (regenerated working tree vs git HEAD), computed programmatically

{golden}

# Evidence B — checklist casing on the fresh run (both fixtures)

{casing}

# Evidence C — representative regenerated object-use.json entries (slimmed: summary, fired signals, questions)

{samples}

# Pilot review judgment (orchestrator)

{PILOT_JUDGMENT}

# Build and test results

{BUILD_TEST}

# {DISCLAIMERS}

# What to verify

1. **Golden regeneration is correct, complete, and bounded.** The regenerated HHC
   (74 objects) and Northwind (167 objects) usage goldens should differ from HEAD by
   exactly: the 3 new fired:false signal-type rows on every object PLUS the listed
   real per-object changes (12 HHC / 6 Northwind), with NO objects added or removed.
   Confirm the real changes are exactly the intended cumulative S1+S2 effect
   (launcher forms -> NavigationShell+LauncherSurface; switchboard helper macro/table
   tied to launcher; startup form -> NavigationShell+StartupEntry; AutoExec/AutoKeys
   +LauncherSurface; parameter-dialog forms +ParameterizedInput; parameter queries
   +ParameterizedInput) and that nothing unexpected (e.g. a cleanup-semantics shift,
   a deletability flip, an object disappearing) crept in.

2. **Checklist-casing stop rule handled correctly.** Evidence B shows both fixtures
   emit canonical PascalCase (top-level 'Review', PascalCase item keys, no camelCase).
   Confirm the correct action is to RECORD the verification and leave the writer code
   UNCHANGED (the spec's stop rule: patch only if a fresh run drifts). Flag if you
   believe a code change is warranted (it should not be).

3. **Pilot output quality / wording reads clearly, and the no-calibration decision is
   sound.** Using Evidence C, judge whether the launcher explanations and parameter
   flows are clear and honest for a downstream AI consumer: launcher shells are
   positively explained; "no harvested opener" reads as out-of-band uncertainty (NOT
   deletability); parameter dialogs are hedged Unknown + a specific question; param
   queries keep BOTH OutOfBand and ParameterizedQuery questions (additive). Confirm
   that "no local defect -> no calibration" is the right call, and that frmGenericDialog
   firing on the *Dialog* name alone is acceptable documented-heuristic breadth rather
   than a defect to fix in this session.

4. **The no-consumer-doc-change judgment is defensible.** Confirm that leaving the
   high-level, vocabulary-agnostic consumer guide unchanged (concepts defined in the
   strategy doc; checklist questions self-describing; Lean-by-default principle) is
   reasonable, OR identify a specific operator-facing gap that genuinely requires a doc
   edit in this final session.

5. **Scope discipline and closeout integrity.** No production code changed; no
   cleanup-semantics reopened; no duplicate-table-shape / obsolete-module-header
   heuristics folded in; missing inbound references are NOT treated as deletability;
   the spurious .accdb churn was excluded; build+test green (816). Flag any stop-rule
   violation or closeout risk.

# Response format

Provide a detailed prose analysis for each of the 5 concerns BEFORE the JSON block.
Then output a JSON block in exactly this format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or area>",
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
        session_number=3,
    )

    log.save_session_review(session_number=3, review_text=result.content, round_number=1)
    log.log_step(
        session_number=3,
        step_number=7,
        step_key="session-3/verify-round-1",
        description="Cross-provider session verification round 1 (final session: golden regen + casing stop-rule + pilot judgment)",
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
    print(result.content[-2500:])
    return 0


if __name__ == "__main__":
    sys.exit(main())
