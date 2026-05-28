"""One-shot Session 2 verification driver for harvester-object-cleanup-identifier.

Loads spec excerpt + all created/edited files + build/test results, builds a
single verification prompt, routes it through the AI router with
task_type='session-verification', and saves the review.

Usage:
  .venv/Scripts/python.exe tools/run_session2_verification.py
"""
from __future__ import annotations
import os
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SESSION_SET_DIR = REPO / "docs" / "session-sets" / "harvester-object-cleanup-identifier"
SESSION_NUMBER = 2

CREATED_FILES = [
    "AccessHarvester.Pipeline/Cleanup/CleanupFinding.cs",
    "AccessHarvester.Pipeline/Cleanup/NamePatternDetector.cs",
    "AccessHarvester.Pipeline/Cleanup/CrossReferenceDetector.cs",
    "AccessHarvester.Pipeline/Cleanup/DateMaximaSampler.cs",
    "AccessHarvester.Pipeline/Cleanup/CleanupChecklistWriter.cs",
    "AccessHarvester.Pipeline/Cleanup/CleanupAnalyser.cs",
    "AccessHarvester.Core/Com/AccessDateMaximaSampler.cs",
    "AccessHarvester.Cli/Commands/IdentifyCleanupCommand.cs",
    "tests/AccessHarvester.Pipeline.Tests/Cleanup/NamePatternDetectorTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/Cleanup/CrossReferenceDetectorTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/Cleanup/DateMaximaSamplerTests.cs",
]

EDITED_FILES = [
    "AccessHarvester.Cli/Program.cs",
    "AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs",
    "AccessHarvester.Pipeline/UsageIdentification/IrLoader.cs",
    "tests/AccessHarvester.Pipeline.Tests/AccessHarvester.Pipeline.Tests.csproj",
]


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def git_diff(path: str) -> str:
    out = subprocess.run(
        ["git", "diff", "--no-color", path],
        cwd=REPO, check=False, capture_output=True, text=True
    )
    return out.stdout


def main() -> int:
    spec = read(SESSION_SET_DIR / "spec.md")
    strategy = read(REPO / "docs" / "planning" / "object-cleanup-strategy.md")
    schema = read(REPO / "AccessHarvester.Pipeline" / "Cleanup" / "object-cleanup-schema.json")

    sections = []
    sections.append("# Session 2 of `harvester-object-cleanup-identifier` — verification request")
    sections.append("")
    sections.append("## Session set spec (excerpt: Session 2 step list and creates)")
    sections.append("")
    sections.append("```markdown")
    sections.append(spec)
    sections.append("```")
    sections.append("")
    sections.append("## Session 1 strategy document (the design spec — this session implements it)")
    sections.append("")
    sections.append("```markdown")
    sections.append(strategy)
    sections.append("```")
    sections.append("")
    sections.append("## Session 1 JSON schema (the output shape)")
    sections.append("")
    sections.append("```json")
    sections.append(schema)
    sections.append("```")
    sections.append("")

    sections.append("## Files created in Session 2")
    sections.append("")
    for rel in CREATED_FILES:
        p = REPO / rel
        sections.append(f"### `{rel}`")
        sections.append("")
        sections.append("```csharp")
        sections.append(read(p))
        sections.append("```")
        sections.append("")

    sections.append("## Files edited in Session 2 (diffs)")
    sections.append("")
    for rel in EDITED_FILES:
        sections.append(f"### `{rel}` (diff)")
        sections.append("")
        sections.append("```diff")
        sections.append(git_diff(rel))
        sections.append("```")
        sections.append("")

    sections.append("## Build + test results")
    sections.append("")
    sections.append("- `dotnet build`: SUCCESS, 0 errors, 4 pre-existing NU1903 warnings (unrelated).")
    sections.append("- `dotnet test --filter Cleanup`: 45/45 passed including a SkippableFact COM test that ran against samples/HomeHealthCare.accdb.")
    sections.append("- `dotnet test --filter UsageIdentification`: 87/87 passed (no regressions from the additive IrTable/IrFormOrReport/IrControl Property edits).")
    sections.append("- Full Pipeline.Tests: 559/559 passed.")
    sections.append("- Full solution test run: 2095 passed, 2 skipped, 3 failed.")
    sections.append("  - The 3 failures are pre-existing CLI integration timeouts (CliExtractTests, CliTranslateTests, CliRunTests on the HomeHealthCare.accdb integration path) — these timed out at 60s before Session 2 work began and have nothing to do with this session's `identify-cleanup` subcommand. Documented in Session 1 actuals (`ai-assignment.md`): '42/45 integration tests pass'. Same baseline.")
    sections.append("- Smoke test: `AccessHarvester.exe identify-cleanup --in samples/GoldenOutput/HomeHealthCare --no-com --json-status` produced 40 flagged objects (all Low tier — expected since HHC names lack staleness vocabulary and LastUpdated is 15 days old < 90 day threshold). `object-cleanup.json` validated cleanly against the JSON schema (0 errors). Checklist `Subarea` byte-identical to `objectName` per §10 invariant.")
    sections.append("")

    sections.append("## Architectural deviation documented")
    sections.append("")
    sections.append("Strategy doc lists `DateMaximaSampler.cs` under `AccessHarvester.Pipeline/Cleanup/`. The Pipeline project cannot reference `AccessHarvester.Core` because Core already references Pipeline (would create a cycle). Resolution: `Pipeline/Cleanup/DateMaximaSampler.cs` holds the `IDateMaximaSampler` interface + `IrLastUpdatedSampler` fallback impl; the COM-using `AccessDateMaximaSampler` lives in `AccessHarvester.Core/Com/`. CLI wires the right impl in. The strategy doc's design (signal families, tier rules, output shape) is unchanged.")
    sections.append("")

    sections.append("## Subagent deviations on test inputs")
    sections.append("")
    sections.append("The implementing subagent kept regex patterns verbatim from §3.1 but adjusted two test-case inputs after running the regexes mentally:")
    sections.append("1. `frmReports2024` was originally proposed as a year-match test case, but the year regex `(?:^|[^0-9A-Za-z])((?:19|20)\\d{2})(?:$|[^0-9A-Za-z])` requires a non-alphanumeric *before* the year; `s2024` has `s` (alphanumeric) before the year. So `frmReports2024` does NOT match. The subagent used `Reports_2024` instead (underscore boundary).")
    sections.append("2. `frmHolders` was proposed as a vocabulary non-match for `Hold`. The subagent flagged this would actually match via CamelCase boundary (m→H), but on closer inspection the *trailing* boundary fails (d→e is lowercase→lowercase, not a CamelCase transition; e is alphanumeric, not non-letter; not end-of-string). So `frmHolders` correctly does NOT match `Hold`. The subagent dropped the test entry rather than verify this manually — Session 3 calibration should explicitly cover this edge case.")
    sections.append("")
    sections.append("Please flag if either of these (or anything else) suggests a regex correctness issue worth fixing before close.")
    sections.append("")

    sections.append("## Response format (mandatory)")
    sections.append("")
    sections.append("Respond as JSON exactly matching this schema:")
    sections.append("")
    sections.append("```json")
    sections.append('{"verdict": "VERIFIED" | "ISSUES_FOUND", "issues": [{"severity": "Critical" | "Major" | "Minor", "category": "Correctness" | "Completeness" | "False Positive", "description": "what is wrong", "location": "file path or section reference"}]}')
    sections.append("```")
    sections.append("")
    sections.append("Focus criteria:")
    sections.append("1. **Spec fidelity** — Do the detectors implement strategy doc §3 (name patterns, date signals, cross-reference absence) faithfully? Are the regex patterns byte-identical to §3.1?")
    sections.append("2. **Tier assignment** — Does CleanupAnalyser implement §4 correctly? (High = Name AND Date; Medium = Name XOR Date; Low = Cross-only and Cross does not upgrade Name/Date findings.)")
    sections.append("3. **Schema mapping** — Does `object-cleanup.json` output match the JSON schema's field names, types, and tagged-union shape?")
    sections.append("4. **Checklist invariants** — Does `object-cleanup-checklist.json` honour §6 invariants? Specifically: `Subarea` byte-identical to `objectName` (no transformation), `ItemLabel` includes `[Tier]` prefix, `IsJudgmentItem: true`, `Passes: false` initial.")
    sections.append("5. **Exclusions** — §7 system objects (`MSys*`, `USys*`, `~sq_*`, `MSysNavPane*`) AND §7.3 code-behind modules (`Form_*` / `Report_*`) globally excluded from the detector pipeline.")
    sections.append("6. **Self-reference exclusion** — §3.3's note that a query SQL self-referencing its own name doesn't count.")
    sections.append("7. **Inline-SQL parsing** — §3.3.1 RecordSource/RowSource inline SELECT parsing fires the FROM/JOIN names as references; value-list literals correctly skipped.")
    sections.append("8. **Event-property bindings** — §3.3.2: properties starting with `On`/`Before`/`After` (case-sensitive prefix) whose value is a bare-identifier macro name count as NavigationEdge references.")
    sections.append("9. **CLI argument shape** — §8: exactly-one-of `--in`/`--manifest`, `--harvest-date` accepts YYYY-MM-DD, `--threshold-days >= 1`, `--no-com` flag. Usage-error exit code 2 per spec.")
    sections.append("10. **Test coverage** — Do the new tests cover §3.1 vocabulary/year/year-month/quarter/numeric-duplicate, §3.3 reference types, exclusions, and self-reference correctly?")
    sections.append("")
    sections.append("Out of scope for this verification (do not flag):")
    sections.append("- The 3 pre-existing CLI integration test timeouts (HomeHealthCare extract/translate/run) — they were failing before this session and are unrelated.")
    sections.append("- The 4 pre-existing NU1903 warnings.")
    sections.append("- Whether `LastUpdated` from DAO metadata represents true 'last data update' (a known soft signal — degradation note covers it).")
    sections.append("- Calibration concerns about regex over/under-matching specific names (Session 3 calibration scope per strategy doc §11).")

    prompt = "\n".join(sections)

    print(f"Prompt length: {len(prompt):,} characters")

    from ai_router import route
    from ai_router.session_log import SessionLog

    log = SessionLog(session_set_dir=str(SESSION_SET_DIR))
    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET_DIR),
        session_number=SESSION_NUMBER,
    )

    print(f"Model: {result.model_name}")
    print(f"Input tokens: {result.input_tokens}, Output tokens: {result.output_tokens}")
    print(f"Cost USD: ${result.cost_usd:.4f}")
    print()
    print("=" * 70)
    print(result.content)
    print("=" * 70)

    log.save_session_review(
        session_number=SESSION_NUMBER,
        review_text=result.content,
        round_number=1,
    )
    print(f"\nReview saved to session-reviews/")
    return 0


if __name__ == "__main__":
    sys.exit(main())
