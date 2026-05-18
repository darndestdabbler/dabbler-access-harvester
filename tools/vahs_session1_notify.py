"""Mark session 1 complete in session-state.json and send Pushover
notification. Per the workflow, this MUST run BEFORE the Step 9
reorganization review so the human is not blocking the completion
signal."""
from __future__ import annotations
import sys
from pathlib import Path

from ai_router import mark_session_complete, send_session_complete_notification

SESSION_SET = Path("docs/session-sets/vba-attribute-header-strip")


def main() -> None:
    mark_session_complete(
        session_set=str(SESSION_SET),
        verification_verdict="VERIFIED",
    )

    summary = (
        "Session 1 of 1 (one-session set; F1.5) COMPLETE.\n"
        "VbaModuleBodyLoader ships; SymbolDbBuilder + ParseSuccessReportGenerator "
        "routed; parse-success-report goldens regenerated.\n"
        "HHC parseOk: 3 -> 30/32 (94%); Northwind: 14 -> 61/66 (92%).\n"
        "1869 tests passed, 2 skipped. Verification VERIFIED at R3 against gpt-5-4.\n"
        "Next session set: F2 (harvester-finish-canonical-golden-output). "
        "Recommended orchestrator: Claude Opus 4.7 @ effort=high for Session 1 "
        "spec-elaboration. Two deviation candidates surfaced: insert a small "
        "`vba-astbuilder-recovery-guards` set before F2 to fix the AstBuilder NRE, "
        "and consider a grammar-extension set for the 7 still-parseFailed modules. "
        "See next-session-set-recommendation.md."
    )

    try:
        send_session_complete_notification(
            session_set=str(SESSION_SET),
            session_number=1,
            total_sessions=1,
            verification_verdict="VERIFIED",
            summary=summary,
        )
        sys.stdout.write("Notification sent.\n")
    except Exception as exc:
        sys.stdout.write(f"Notification failed: {exc}\n")


if __name__ == "__main__":
    main()
