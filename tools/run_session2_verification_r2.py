"""R2 verification driver — re-verify after R1 fixes."""
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

    # Find the R1 review file to include as context for R2
    reviews_dir = SESSION_SET_DIR / "session-reviews"
    r1_text = ""
    if reviews_dir.exists():
        candidates = sorted(reviews_dir.glob("*.md"))
        if candidates:
            r1_text = read(candidates[-1])

    sections = []
    sections.append("# Session 2 of `harvester-object-cleanup-identifier` — R2 verification (post-fix)")
    sections.append("")
    sections.append("R1 verification (saved below) returned ISSUES_FOUND with 5 issues. All 5 have been fixed. This is R2 — re-verify that the fixes are correct and complete, and that no new issues were introduced.")
    sections.append("")
    sections.append("## R1 review (the issues to verify are fixed)")
    sections.append("")
    sections.append("```")
    sections.append(r1_text)
    sections.append("```")
    sections.append("")
    sections.append("## Summary of fixes applied")
    sections.append("")
    sections.append("- **Issue 1 (Major):** `CleanupAnalyser.Run` now takes an optional `primaryDateSampler` (COM) plus the required `fallback` (IR `LastUpdated`). Per-table selection: COM sample if the table has DateTime columns AND COM produced a sample; otherwise DAO `LastUpdated`. CLI wires both samplers when COM-enabled.")
    sections.append("- **Issue 2 (Major):** `CrossReferenceDetector` now regex-parses `DoCmd.OpenForm`/`OpenReport`/`OpenQuery`/`OpenMacro`/`RunMacro` calls in ALL VBA code blobs (modules + form/report code-behind) as NavigationEdge. `DCount`/`OpenRecordset` targets filtered to known table/query names. Bare-token presence of form/report/module names in OTHER VBA blobs counts as VbaReference. Self-reference exclusion respected (owner-name guard).")
    sections.append("- **Issue 3 (Minor):** `CollectEventPropertyBindings` narrowed to macro names only — bare-identifier values matching forms/reports/queries are NOT NavigationEdges.")
    sections.append("- **Issue 4 (Minor):** `NamePatternDetector` returns `matchedSubstring` with leading boundary character preserved (e.g., `_OLD`, `_2023`) and only trailing boundary trimmed. Medium-tier explanation uses `MatchedSubstring` instead of `Category`.")
    sections.append("- **Issue 5 (Minor):** Added 9 hermetic tests covering per-table mixed-source sampling, DoCmd.OpenForm/OpenReport/OpenQuery/RunMacro NavigationEdge, DCount VbaReference, bare-token form references across modules, self-reference exclusion, and negative tests confirming event-property bindings to forms/queries do NOT count.")
    sections.append("")
    sections.append("## Strategy document (the authoritative spec — reproduced for context)")
    sections.append("")
    sections.append("```markdown")
    sections.append(strategy)
    sections.append("```")
    sections.append("")
    sections.append("## JSON schema (output shape)")
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

    sections.append("## Build + test results (R2)")
    sections.append("")
    sections.append("- `dotnet build`: SUCCESS, 0 errors, 4 pre-existing NU1903 warnings (unrelated).")
    sections.append("- `dotnet test` on full Pipeline.Tests project: 569/569 passed (up from 559 — 9 new hermetic tests + 1 net-positive after the per-table sampler refactor).")
    sections.append("- Smoke test: `identify-cleanup --in samples/GoldenOutput/HomeHealthCare --no-com --json-status` produced 30 flagged objects (down from 40 in R1 — VBA navigation edges and bare-token cross-references now suppress false-positive Low-tier findings). JSON schema validation: 0 errors. `Subarea` byte-identical to `objectName` invariant: holds.")
    sections.append("")

    sections.append("## Response format (mandatory)")
    sections.append("")
    sections.append("Respond as JSON exactly matching this schema:")
    sections.append("")
    sections.append("```json")
    sections.append('{"verdict": "VERIFIED" | "ISSUES_FOUND", "issues": [{"severity": "Critical" | "Major" | "Minor", "category": "Correctness" | "Completeness" | "False Positive", "description": "what is wrong", "location": "file path or section reference"}]}')
    sections.append("```")
    sections.append("")
    sections.append("Verification focus for R2 (in this order):")
    sections.append("1. Are R1 Issues 1–5 actually fixed? Specifically check:")
    sections.append("   - Issue 1: does `CleanupAnalyser` produce per-table mixed `com` + `dao-metadata` sources when the primary sampler is supplied? Does the CLI wire it correctly?")
    sections.append("   - Issue 2: does `CrossReferenceDetector` recognise `DoCmd.OpenForm/OpenReport/OpenQuery/OpenMacro/RunMacro` calls in VBA + bare-token form/report/module references across modules + does it respect self-reference exclusion?")
    sections.append("   - Issue 3: is the event-property binding now macro-only?")
    sections.append("   - Issue 4: do regex matches preserve the leading boundary char in `matchedSubstring`? Does Medium explanation use the matched substring?")
    sections.append("   - Issue 5: are the new tests asserting the right behaviour?")
    sections.append("2. Did the fixes introduce any new spec deviations (over-correction)? Especially: does the VBA regex over-match (e.g., catching DoCmd.OpenForm inside a string literal)? Does the bare-token form-name scan have enough specificity to avoid false positives on common English words?")
    sections.append("3. Schema fidelity unchanged?")
    sections.append("")
    sections.append("Out of scope for this verification (DO NOT flag — already known):")
    sections.append("- The 3 pre-existing CLI integration test timeouts on HomeHealthCare extract/translate/run.")
    sections.append("- The 4 pre-existing NU1903 warnings.")
    sections.append("- The architectural split between Pipeline/Cleanup/DateMaximaSampler (interface + fallback) and Core/Com/AccessDateMaximaSampler (COM impl).")
    sections.append("- Whether the strategy doc's `{pattern}` placeholder in §5 Medium template means the category or the matched-substring (the R1 verifier read it as matched-substring; R1 fix applied matched-substring; treat as resolved).")
    sections.append("- NorthwindDevEdition `[SkippableFact]` (Session 3 calibration covers it).")
    sections.append("- Calibration concerns about regex over/under-matching specific names (Session 3 calibration scope per strategy §11).")
    sections.append("")
    sections.append("If R1 Issues 1–5 are correctly resolved AND no new spec-violation introduced, return `VERIFIED`.")

    prompt = "\n".join(sections)
    print(f"Prompt length: {len(prompt):,} characters")

    from ai_router import route
    from ai_router.session_log import SessionLog

    log = SessionLog(session_set_dir=str(SESSION_SET_DIR))
    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,  # major-issue retry per Step 7
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
        round_number=2,
    )
    print(f"\nReview saved.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
