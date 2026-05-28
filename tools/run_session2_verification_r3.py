"""R3 verification driver — re-verify after R2 fixes (4 issues addressed)."""
from __future__ import annotations
import sys
import subprocess
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
    "AccessHarvester.Pipeline/Cleanup/ComDateMaximaProbe.cs",
    "AccessHarvester.Core/Com/AccessDateMaximaSampler.cs",
    "AccessHarvester.Cli/Commands/IdentifyCleanupCommand.cs",
    "tests/AccessHarvester.Pipeline.Tests/Cleanup/NamePatternDetectorTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/Cleanup/CrossReferenceDetectorTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/Cleanup/DateMaximaSamplerTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/Cleanup/ComDateMaximaProbeTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/IrLoaderTests.cs",
]

EDITED_FILES = [
    "AccessHarvester.Cli/Program.cs",
    "AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs",
    "AccessHarvester.Pipeline/UsageIdentification/IrLoader.cs",
    "tests/AccessHarvester.Pipeline.Tests/AccessHarvester.Pipeline.Tests.csproj",
]


def read(path: Path) -> str:
    if not path.exists():
        return f"[FILE NOT FOUND: {path}]"
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

    # Read all saved reviews to give the R3 verifier full history
    reviews_dir = SESSION_SET_DIR / "session-reviews"
    review_history = ""
    if reviews_dir.exists():
        for p in sorted(reviews_dir.glob("*.md")):
            review_history += f"### {p.name}\n\n```\n{read(p)}\n```\n\n"

    sections = []
    sections.append("# Session 2 of `harvester-object-cleanup-identifier` — R3 verification (post-R2 fixes)")
    sections.append("")
    sections.append("R1 found 5 issues; all fixed in R1 fix round. R2 found 4 NEW issues (2 Major including 1 regression in IrLoader, 2 Minor). All 4 R2 issues are now fixed. This is R3 — re-verify that the R2 fixes are correct, no new issues introduced, and the implementation is ready to ship.")
    sections.append("")
    sections.append("## Saved review history (R1 + R2)")
    sections.append("")
    sections.append(review_history)
    sections.append("")
    sections.append("## Summary of R2 fixes applied")
    sections.append("")
    sections.append("- **R2 Issue 2 (Major, REGRESSION) — IrLoader WalkChildren branch ordering:** Reordered branches so `type == \"Begin\"` (macro-action) is checked BEFORE the generic-properties catch-all. Added 2 new hermetic tests in `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/IrLoaderTests.cs` covering embedded-form Begin-action capture and generic-control walking.")
    sections.append("- **R2 Issue 1 (Major) — graceful COM-failure degradation:** Extracted a new `ComDateMaximaProbe` helper in `AccessHarvester.Pipeline/Cleanup/`. It accepts a sampler factory (Func) + IrDatabase, runs them inside try/catch, and returns either `{Primary, Note=null}` on success or `{Primary=null, Note=\"<error message>\"}` on failure. CLI consumes the result: on failure, prints a stderr warning and prepends the degradation note to `findings.DegradationNotes` via record-`with`. 5 new hermetic tests in `ComDateMaximaProbeTests.cs` cover: success, factory-throws, Sample-throws, disposable cleanup runs even on failure, null-args validation.")
    sections.append("- **R2 Issue 4 (Minor) — narrow module/class VBA ref to spec contexts:** New `ScanModuleContextPresence` method uses three regexes per module name: `\\bCall\\s+<name>\\b`, `\\bSet\\b[^\\r\\n]*\\bNew\\s+<name>\\b`, `(?<![A-Za-z0-9_])<name>\\.` (qualifier dot-access). Form/report bare-token scan kept as-is per §3.3.3 matrix. 4 new tests cover Call, Set...New, qualifier, and a negative (bare token in comment/string is NOT a reference).")
    sections.append("- **R2 Issue 3 (Minor) — RowSource tests added:** 2 new tests in `CrossReferenceDetectorTests.cs`: control with literal table name in RowSource, control with inline-SQL `SELECT col FROM tblTarget` in RowSource.")
    sections.append("")
    sections.append("## Build + test results (R3)")
    sections.append("")
    sections.append("- `dotnet build`: SUCCESS, 0 errors.")
    sections.append("- `dotnet test` on Pipeline.Tests: **582/582 passed** (up from 569 — +13 new tests across IrLoader, ComDateMaximaProbe, module-context VBA, RowSource).")
    sections.append("- Smoke test: `identify-cleanup --in samples/GoldenOutput/HomeHealthCare --no-com --json-status` → 30 objects flagged, schema-valid, `Subarea` byte-identical to `objectName` invariant holds.")
    sections.append("- COM-failure-degradation: covered at the unit-test level (5 hermetic `ComDateMaximaProbeTests`). End-to-end smoke could not provoke a real COM open failure without uninstalling Office; the CLI is a thin delegator to the tested helper so behaviour is equivalent.")
    sections.append("")
    sections.append("## Strategy document (authoritative spec)")
    sections.append("")
    sections.append("```markdown")
    sections.append(strategy)
    sections.append("```")
    sections.append("")
    sections.append("## JSON schema")
    sections.append("")
    sections.append("```json")
    sections.append(schema)
    sections.append("```")
    sections.append("")
    sections.append("## Spec (session step list)")
    sections.append("")
    sections.append("```markdown")
    sections.append(spec)
    sections.append("```")
    sections.append("")

    sections.append("## Current file contents (created)")
    sections.append("")
    for rel in CREATED_FILES:
        p = REPO / rel
        sections.append(f"### `{rel}`")
        sections.append("")
        sections.append("```csharp")
        sections.append(read(p))
        sections.append("```")
        sections.append("")

    sections.append("## Current file diffs (edited)")
    sections.append("")
    for rel in EDITED_FILES:
        sections.append(f"### `{rel}` (diff vs HEAD)")
        sections.append("")
        sections.append("```diff")
        sections.append(git_diff(rel))
        sections.append("```")
        sections.append("")

    sections.append("## Response format (mandatory)")
    sections.append("")
    sections.append("Respond as JSON exactly matching this schema:")
    sections.append("")
    sections.append("```json")
    sections.append('{"verdict": "VERIFIED" | "ISSUES_FOUND", "issues": [{"severity": "Critical" | "Major" | "Minor", "category": "Correctness" | "Completeness" | "False Positive", "description": "what is wrong", "location": "file path or section reference"}]}')
    sections.append("```")
    sections.append("")
    sections.append("R3 verification focus:")
    sections.append("1. **Are R2 Issues 1–4 actually fixed?** Specifically:")
    sections.append("   - R2 Issue 1: does the CLI gracefully degrade on COM failure (not exit with error; degradation note appears)?")
    sections.append("   - R2 Issue 2: does `IrLoader.WalkChildren` correctly capture embedded form `Begin` macro-action nodes into `IrFormOrReport.MacroActions`?")
    sections.append("   - R2 Issue 3: are there hermetic tests for RowSource literal-name AND inline-SQL paths?")
    sections.append("   - R2 Issue 4: does `ScanModuleContextPresence` correctly restrict to Call / Set...New / qualifier-dot contexts (not bare-token)?")
    sections.append("2. **No new issues** — did the R2 fix round introduce any new bugs, regressions, or spec deviations?")
    sections.append("3. **Architectural soundness** — is `ComDateMaximaProbe` cleanly placed in Pipeline/Cleanup (no Pipeline→Core reference required)?")
    sections.append("")
    sections.append("Out of scope (DO NOT flag — already known/acknowledged):")
    sections.append("- The 3 pre-existing CLI integration test timeouts.")
    sections.append("- The 4 pre-existing NU1903 warnings.")
    sections.append("- Architectural split between `Pipeline/Cleanup/DateMaximaSampler` and `Core/Com/AccessDateMaximaSampler`.")
    sections.append("- NorthwindDevEdition COM `[SkippableFact]` (Session 3 calibration).")
    sections.append("- Calibration concerns about regex over/under-matching on specific real-world names (Session 3).")
    sections.append("- Whether `LastUpdated` is a perfect signal (degradation note covers it).")
    sections.append("- The R1 fixes that were previously verified (mixed-source design, DoCmd.Open* parsing, event-property narrowing, matchedSubstring boundary).")
    sections.append("")
    sections.append("If R2 Issues 1–4 are correctly resolved AND no new spec violation introduced, return `VERIFIED`. If you find a previously-overlooked issue that should clearly have been caught in R1/R2, flag it but note that it was missed previously.")

    prompt = "\n".join(sections)
    print(f"Prompt length: {len(prompt):,} characters")

    from ai_router import route
    from ai_router.session_log import SessionLog

    log = SessionLog(session_set_dir=str(SESSION_SET_DIR))
    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,
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
        round_number=3,
    )
    print(f"\nReview saved.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
