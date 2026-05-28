#!/usr/bin/env python3
"""Round 2 verification - checks ADR fixes for the 2 Minor issues from Round 1."""
import pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import ai_router

adr = pathlib.Path("docs/schema/v1-adr.md").read_text(encoding="utf-8")

prompt = f"""Your response MUST begin with a detailed prose analysis BEFORE the JSON block.

Session: harvester-schema-v1-adr-followups, Session 1 -- Round 2 verification.

Round 1 found 2 Minor issues in docs/schema/v1-adr.md:
1. ADR was internally inconsistent about fixture migration status (said "all 13" in one place, "12 of 13" in another; stale text "until Session 2 migrates those fixtures").
2. Version-detection guidance referenced document.Source.SchemaVersion, which does not match the actual DatabaseManifest structure.

Both issues have been fixed. Please verify the ADR changes resolve both issues.

## docs/schema/v1-adr.md (full current text)
```
{adr}
```

## Concerns to verify (Round 2)
1. Is the fixture migration status now stated consistently? (all mentions should agree that 12/13 are migrated to v1.0; fixture 12 excluded per stop rule)
2. Is the version-detection guidance now correct? (should reference the actual embedded ParserOutput paths, not document.Source.SchemaVersion)
3. Does any other stale text remain (e.g., references to "Session 2" still being needed for work that is now complete)?

After your analysis, output a JSON block in this exact format:
```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```
If verdict is VERIFIED, issues must be an empty array [].
Do not omit this block.
"""

print(f"R2 prompt length: {len(prompt)} chars")
result = ai_router.route(
    content=prompt,
    task_type="session-verification",
    session_set="harvester-schema-v1-adr-followups",
    session_number=1,
)
output_path = pathlib.Path("docs/session-sets/harvester-schema-v1-adr-followups/session-reviews/session-001-r2.md")
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(result.content, encoding="utf-8")
print(f"Saved to {output_path}")
print("--- LAST 800 CHARS ---")
print(result.content[-800:])
