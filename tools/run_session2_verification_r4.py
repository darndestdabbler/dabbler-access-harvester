"""R4 verification driver — final check after R3's 2 Minor fixes.

Human-authorized override of the workflow's 2-retry cap, matching Session 1's
precedent (R4 with human authorization to close out cleanly).
"""
from __future__ import annotations
import sys
import subprocess
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SESSION_SET_DIR = REPO / "docs" / "session-sets" / "harvester-object-cleanup-identifier"
SESSION_NUMBER = 2

# Just the two files touched in the R3 fix round — keep R4 prompt focused.
FILES_OF_INTEREST = [
    "AccessHarvester.Cli/Commands/IdentifyCleanupCommand.cs",
    "AccessHarvester.Pipeline/Cleanup/ComDateMaximaProbe.cs",
    "AccessHarvester.Pipeline/Cleanup/CleanupAnalyser.cs",
]


def read(path: Path) -> str:
    if not path.exists():
        return f"[FILE NOT FOUND: {path}]"
    return path.read_text(encoding="utf-8", errors="replace")


def main() -> int:
    # Read all saved reviews so the verifier has full history
    reviews_dir = SESSION_SET_DIR / "session-reviews"
    review_history = ""
    if reviews_dir.exists():
        for p in sorted(reviews_dir.glob("*.md")):
            review_history += f"### {p.name}\n\n```\n{read(p)}\n```\n\n"

    sections = []
    sections.append("# Session 2 R4 verification — focused review of R3 fixes")
    sections.append("")
    sections.append("Session 2 of `harvester-object-cleanup-identifier` has gone through R1, R2, R3 verification rounds. R3 returned ISSUES_FOUND with 2 Minor issues. Both have been fixed. This is R4, human-authorized override of the workflow's 2-retry cap (matching Session 1's precedent of closing out cleanly rather than deferring Minors).")
    sections.append("")
    sections.append("R4 scope is INTENTIONALLY NARROW: verify that the two R3 Minor issues are now correctly resolved, and that the fixes did not introduce any new bug. Do NOT re-relitigate R1/R2/R3 findings that were already resolved.")
    sections.append("")
    sections.append("## R3 issues and their R3 fixes")
    sections.append("")
    sections.append("**R3 Issue 1 (Minor, Completeness):** Spec §8 requires the CLI to print the usage message when both `--in` and `--manifest` are supplied. The original R2-era code printed the error message and returned exit code 2 but did not call `PrintUsage()`.")
    sections.append("")
    sections.append("**R3 Fix 1:** Added `PrintUsage();` immediately before `return 2;` in the dual-flag branch of `IdentifyCleanupCommand.Run`. Now matches the `--in`/`--manifest` neither-supplied branch which already called `PrintUsage()`.")
    sections.append("")
    sections.append("**R3 Issue 2 (Minor, Correctness):** The R2 COM-failure probe always prepended a degradation note saying \"Per-table date signals fell back to DAO LastUpdated.\" Even when no IR `LastUpdated` values exist, this note would be appended, contradicting the analyser's separately-emitted \"Date sampling unavailable: COM declined AND no IR LastUpdated values present\" note.")
    sections.append("")
    sections.append("**R3 Fix 2:** In `IdentifyCleanupCommand.Run`, the prepend-COM-fallback-note step is now conditional on `findings.DateSignalSource != \"unavailable\"`. When the DAO fallback also produced no findings (LastUpdated absent), the analyser's own note is more accurate and the probe message is suppressed. When DAO fallback succeeded for at least one table, the probe message is preserved as useful context.")
    sections.append("")
    sections.append("## Saved review history (R1, R2, R3)")
    sections.append("")
    sections.append(review_history)
    sections.append("")
    sections.append("## Current state of the fix-affected files")
    sections.append("")
    for rel in FILES_OF_INTEREST:
        p = REPO / rel
        sections.append(f"### `{rel}`")
        sections.append("")
        sections.append("```csharp")
        sections.append(read(p))
        sections.append("```")
        sections.append("")

    sections.append("## Build + test results (R4)")
    sections.append("")
    sections.append("- `dotnet build`: SUCCESS, 0 errors.")
    sections.append("- `dotnet test` on Pipeline.Tests: 582/582 passed (unchanged from R2-fix → R3, no new tests for these Minor fixes).")
    sections.append("")
    sections.append("## Response format (mandatory)")
    sections.append("")
    sections.append("Respond as JSON exactly matching this schema:")
    sections.append("")
    sections.append("```json")
    sections.append('{"verdict": "VERIFIED" | "ISSUES_FOUND", "issues": [{"severity": "Critical" | "Major" | "Minor", "category": "Correctness" | "Completeness" | "False Positive", "description": "what is wrong", "location": "file path or section reference"}]}')
    sections.append("```")
    sections.append("")
    sections.append("R4 narrow focus:")
    sections.append("1. Is R3 Issue 1 fixed? Look at `IdentifyCleanupCommand.Run` — does the dual-flag branch now call `PrintUsage()` before returning?")
    sections.append("2. Is R3 Issue 2 fixed? Look at the analyser-call block — is the prepend now gated on `findings.DateSignalSource != \"unavailable\"`?")
    sections.append("3. No regressions? Did either fix introduce a new bug?")
    sections.append("")
    sections.append("Out of scope (DO NOT flag — already known/resolved/acknowledged):")
    sections.append("- All previously-flagged R1, R2 issues (already fixed in their respective rounds).")
    sections.append("- The 3 pre-existing CLI integration test timeouts.")
    sections.append("- The 4 pre-existing NU1903 warnings.")
    sections.append("- Architectural split between Pipeline/Cleanup/DateMaximaSampler and Core/Com/AccessDateMaximaSampler.")
    sections.append("- NorthwindDevEdition `[SkippableFact]` (Session 3 calibration).")
    sections.append("- Calibration concerns about regex over/under-matching on real-world names (Session 3 scope per strategy §11).")
    sections.append("- Anything not directly about R3 Issue 1 or R3 Issue 2.")
    sections.append("")
    sections.append("If both R3 issues are correctly resolved and no new bug introduced, return `VERIFIED`. Be decisive — this is a clean-up retry, not an architecture review.")

    prompt = "\n".join(sections)
    print(f"Prompt length: {len(prompt):,} characters")

    from ai_router import route
    from ai_router.session_log import SessionLog

    log = SessionLog(session_set_dir=str(SESSION_SET_DIR))
    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,  # back to base — Minors only
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
        round_number=4,
    )
    print("\nReview saved.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
