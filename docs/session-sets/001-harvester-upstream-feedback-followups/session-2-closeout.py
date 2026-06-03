"""Session 2 closeout helper: persist the cumulative issue log and print the
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
        "round": 1, "severity": "Minor",
        "location": "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/ParameterDialogTests.cs",
        "summary": "No regression covered the [Reports]![...] report-control-reference query-parameter path, though BuildQueryParameterEvidence supports report-reference evidence.",
        "resolution": "Fixed (test-only): added Report_Control_Reference_Query_Fires_ParameterizedInput asserting ParameterizedInput fires with report-control-reference evidence + the ParameterizedQuery question.",
        "status": "resolved",
    },
    {
        "round": 1, "severity": "Minor",
        "location": "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/ParameterDialogTests.cs",
        "summary": "No regression covered the ODBC '?' marker parameter-evidence path.",
        "resolution": "Fixed (test-only): added Odbc_Marker_Query_Fires_ParameterizedInput asserting ParameterizedInput fires with odbc-parameter-marker evidence + the ParameterizedQuery question (Q5 category behavior unchanged).",
        "status": "resolved",
    },
]


def main() -> int:
    log = SessionLog(SESSION_SET)
    log.save_issue_log(session_number=2, issues=ISSUES)
    print("issue log saved:", len(ISSUES), "findings (all resolved)")
    try:
        print("cost summary:", log.get_cost_summary())
    except Exception as exc:  # noqa: BLE001
        print("cost summary unavailable:", exc)
    return 0


if __name__ == "__main__":
    sys.exit(main())
