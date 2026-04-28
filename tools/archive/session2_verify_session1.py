#!/usr/bin/env python3
"""Backfill cross-provider verification for Session 1 of vba-symbol-resolution-and-enrichment.

Session 1 was committed without the mandatory Step 6 verification (Rule #2 violation).
This script runs the verification now and saves the review file so Session 2 can proceed.
"""
import importlib.util
import json
import sys
from pathlib import Path


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router", "ai-router/__init__.py",
        submodule_search_locations=["ai-router"])
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


def read_text(p):
    return Path(p).read_text(encoding="utf-8")


def main():
    ar = load_ai_router()
    route = ar.route
    from ai_router.session_log import SessionLog

    SESSION_SET = Path("docs/session-sets/vba-symbol-resolution-and-enrichment")
    log = SessionLog(SESSION_SET)

    spec_excerpt = read_text(SESSION_SET / "spec.md")
    activity_log = read_text(SESSION_SET / "activity-log.json")
    session_summary = read_text(SESSION_SET / "SESSION-1-SUMMARY.md")

    sym_extractor = read_text("tools/symbol-resolver/symbol_extractor.py")
    ref_resolver = read_text("tools/symbol-resolver/reference_resolver.py")
    runner = read_text("tools/symbol-resolver/runner.py")

    poc_report = read_text("tmp/symbol-resolution-poc/POC-REPORT.md")
    cross_module = read_text("main/tmp/symbol-resolution-poc/cross-module-dependencies.json")
    schema_proposal = read_text("main/tmp/symbol-resolution-poc/enriched-ir-schema-proposal.json")
    modstrings_symbols = read_text("main/tmp/symbol-resolution-poc/modStrings-symbols.json")

    parts = [
        "## Session 1 Verification — Backfill",
        "",
        "**Context for the verifier:** Session 1 of the session set "
        "`vba-symbol-resolution-and-enrichment` was committed (commit aabf8d7) without "
        "running the mandatory cross-provider verification step (workflow Rule #2). "
        "This is a backfilled verification of the on-disk state.",
        "",
        "**Out-of-scope for this verification:**",
        "- The session-state.json file currently records `VERIFIED` — that record was "
        "  written without verification actually happening; you are the verification.",
        "- The Session 1 PoC outputs were written to `main/tmp/symbol-resolution-poc/` "
        "  (a doubled-`main` path that indicates a wrong CWD during the run). They were "
        "  NOT committed to git — only `tools/symbol-resolver/` itself and the "
        "  session-set metadata files were committed.",
        "- `tools/symbol-resolver/test_poc.py` is not included below (it is a smoke "
        "  test runner, not a feature deliverable).",
        "",
        "## Spec",
        "",
        "```markdown",
        spec_excerpt,
        "```",
        "",
        "## Session 1 Activity Log",
        "",
        "```json",
        activity_log,
        "```",
        "",
        "## Session 1 Summary (orchestrator's own writeup)",
        "",
        "```markdown",
        session_summary,
        "```",
        "",
        "## Deliverable: tools/symbol-resolver/symbol_extractor.py",
        "",
        "```python",
        sym_extractor,
        "```",
        "",
        "## Deliverable: tools/symbol-resolver/reference_resolver.py",
        "",
        "```python",
        ref_resolver,
        "```",
        "",
        "## Deliverable: tools/symbol-resolver/runner.py",
        "",
        "```python",
        runner,
        "```",
        "",
        "## Deliverable: PoC Report (uncommitted, on disk only)",
        "",
        "```markdown",
        poc_report,
        "```",
        "",
        "## Deliverable: cross-module-dependencies.json (uncommitted)",
        "",
        "```json",
        cross_module,
        "```",
        "",
        "## Deliverable: enriched-ir-schema-proposal.json (uncommitted)",
        "",
        "```json",
        schema_proposal,
        "```",
        "",
        "## Sample symbol-table output: modStrings",
        "",
        "```json",
        modstrings_symbols,
        "```",
        "",
        "## What to evaluate",
        "",
        "Per the spec's Session 1 step list:",
        "1. Was the `architecture` design call for the symbol resolver algorithm actually "
        "   routed (the spec required this)? The activity log shows zero routed API calls.",
        "2. Does the regex-based extractor in `symbol_extractor.py` correctly handle the "
        "   target VBA constructs (Public/Private Enum, Function/Sub signatures with "
        "   ParamArray and complex types, module-level Dim/Const)?",
        "3. Does the reference resolver correctly classify cross-module calls, VBA "
        "   built-ins, and enum values? Note the s-prefix convention on enum values "
        "   (sCompanyDelete_CompanyTypeChange, etc.) and the heuristic in "
        "   `_resolve_identifier`.",
        "4. Does the PoC report's claim of '95%+ identifier coverage' and '100% "
        "   cross-module resolution' hold up given what was actually built? The activity "
        "   log says 'Read target VBA from IR files' but the spec said to read raw .txt "
        "   files; the form's raw .txt file is UTF-16 encoded, which the extractor's "
        "   regexes would not handle correctly.",
        "5. Is the enriched IR schema proposal coherent and well-grounded?",
        "6. Are the SESSION-1-SUMMARY claims consistent with the on-disk state? "
        "   (Summary says symbol_extractor.py is 515 lines; on disk it is 293 lines.)",
        "",
        "Please return your verdict per the standard verification schema.",
    ]

    verification_prompt = "\n".join(parts)

    Path("tmp").mkdir(exist_ok=True)
    Path("tmp/session1-verification-prompt.md").write_text(
        verification_prompt, encoding="utf-8"
    )
    print(f"Verification prompt: {len(verification_prompt)} chars")
    print(f"Wrote tmp/session1-verification-prompt.md")

    print("Routing session-verification...")
    result = route(
        content=verification_prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    Path("tmp/session1-verification-response.md").write_text(
        result.content, encoding="utf-8"
    )
    print(f"Wrote tmp/session1-verification-response.md ({len(result.content)} chars)")
    print(f"Model: {result.model_name}, cost: ${result.cost_usd:.4f}")

    log.save_session_review(
        session_number=1,
        review_text=result.content,
        round_number=1,
    )
    print("Saved session-reviews/session-001.md")

    log.log_step(
        session_number=1,
        step_number=6,
        step_key="session-1/cross-provider-verification-backfill",
        description=(
            "Backfilled mandatory cross-provider session-verification call for Session 1, "
            "which was committed (aabf8d7) without it. Sent spec + activity log + Session 1 "
            "summary + symbol resolver source + uncommitted PoC outputs to verifier. "
            f"Verifier model: {result.model_name}."
        ),
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print("Logged backfill step to activity-log.json")


if __name__ == "__main__":
    main()
