"""Send Pushover notification for Session 1 completion."""
from __future__ import annotations
import importlib.util, sys, pathlib

REPO = pathlib.Path(__file__).resolve().parents[2]


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        str(REPO / "ai-router" / "__init__.py"),
        submodule_search_locations=[str(REPO / "ai-router")],
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


SESSION_SET = REPO / "docs" / "session-sets" / "vba-lexical-and-expression-grammar"


def main() -> None:
    ar = load_ai_router()
    summary = (
        "Lexical section of Vba.g4 + 42 lexer tests landed. "
        "Switched grammar project to Antlr4BuildTasks 12.8.0 + "
        "Antlr4.Runtime.Standard 4.13.1 (parser-foundations deferred "
        "codegen item). 137 tests pass. 3 verification rounds; final "
        "Major fixed in-place at retry cap. Routed cost ~$0.68. "
        "Next session orchestrator: Opus 4.7 @ effort=high."
    )
    try:
        ar.send_session_complete_notification(
            session_set=SESSION_SET,
            session_number=1,
            total_sessions=4,
            verification_verdict="VERIFIED-WITH-INPLACE-FIX",
            summary=summary,
        )
        print("Pushover notification sent.")
    except Exception as exc:
        print(f"Session committed but Pushover notification failed: {exc}")


if __name__ == "__main__":
    main()
