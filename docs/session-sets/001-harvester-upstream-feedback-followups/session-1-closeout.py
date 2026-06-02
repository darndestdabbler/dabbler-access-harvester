"""Session 1 closeout helper: persist the cumulative issue log and print the
routed-cost summary for ai-assignment.md actuals + disposition.json.
"""
from __future__ import annotations

import sys
from pathlib import Path

from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "001-harvester-upstream-feedback-followups"

ISSUES = [
    {
        "round": 1, "severity": "Major",
        "location": "AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs",
        "summary": "OutOfBandEntryPossible could fire for a query referenced only by another query's SQL — HasHarvestedOpener ignored inbound query-SQL use.",
        "resolution": "Fixed: HasHarvestedOpener now also returns true when index.UsedInQuerySql contains the object; added Query_Consumed_By_Another_Query regression.",
        "status": "resolved",
    },
    {
        "round": 1, "severity": "Major",
        "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs (BuildSignals)",
        "summary": "Form LauncherSurface signal fired for any startup-opened/launcher-named form regardless of binding (disagreed with the classifier's category), and launcher-chrome evidence was never emitted.",
        "resolution": "Fixed: form LauncherSurface evidence gated on no-RecordSource (mirrors F5b); StartupEntry kept as a standalone positive signal for all types; launcher-chrome evidence emitted via a threaded launcherChrome flag; added boundary tests.",
        "status": "resolved",
    },
    {
        "round": 1, "severity": "Minor",
        "location": "tests/AccessHarvester.Pipeline.Tests/UsageIdentification",
        "summary": "Missing boundary tests (bound launcher-named form off F5b; query-SQL inbound suppressing out-of-band; startup-opened bound form signal alignment).",
        "resolution": "Fixed: added 4 boundary regression tests.",
        "status": "resolved",
    },
    {
        "round": 2, "severity": "Minor",
        "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs (ClassifyMacro)",
        "summary": "A name-only launcher macro (no parsed nav edges) used the edge-backed summary, overstating evidence by claiming it 'contains navigation actions'.",
        "resolution": "Fixed: macro summary worded by evidence source — name-only uses cautious '...named like a switchboard/menu launcher helper (no navigation actions parsed)'; added regression test.",
        "status": "resolved",
    },
    {
        "round": 3, "severity": "Minor",
        "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs (BuildSummary, ClassifyMacro)",
        "summary": "Launcher-specific summaries applied to ALL NavigationShell forms / edge-backed macros, overstating switchboard/menu/launcher traits for generic navigation objects; no positive Query out-of-band regression.",
        "resolution": "Fixed: launcher wording gated on launcher evidence (forms: name/startup; macros: launcher-named AND edge-backed); generic nav objects keep neutral wording; added a positive Query out-of-band test and neutral-summary regressions.",
        "status": "resolved",
    },
    {
        "round": 4, "severity": "Minor",
        "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs (BuildSummary, BuildSignals table)",
        "summary": "Launcher table summary/signal were re-derived from the table name alone, so a positively-used table like 'tblMenuItems' could get launcher output even when an earlier rule decided its category.",
        "resolution": "Fixed (post-R4, human-authorized close without R5): threaded a launcherDataTable flag from the T5b decision into BuildFinding/BuildSummary/BuildSignals; table launcher output now gated on T5b, not the name; added Positively_Used_Launcher_Named_Table regression.",
        "status": "resolved",
    },
]


def main() -> int:
    log = SessionLog(SESSION_SET)
    log.save_issue_log(session_number=1, issues=ISSUES)
    print("issue log saved:", len(ISSUES), "findings (all resolved)")
    try:
        print("cost summary:", log.get_cost_summary())
    except Exception as exc:  # noqa: BLE001
        print("cost summary unavailable:", exc)
    return 0


if __name__ == "__main__":
    sys.exit(main())
