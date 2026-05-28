#!/usr/bin/env python3
"""Round 3 verification - ADR consistency check after R2 fixes."""
import pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import ai_router

adr = pathlib.Path("docs/schema/v1-adr.md").read_text(encoding="utf-8")

prompt = f"""Your response MUST begin with prose analysis BEFORE the JSON block.

Session: harvester-schema-v1-adr-followups, Session 1 -- Round 3 (ADR consistency only).

Round 2 found 2 Minor issues in docs/schema/v1-adr.md:
1. Stale future-tense text: "Session 2 will update all 13 fixtures."
2. Inconsistent fixture count: one section said "all 13 migrated", another said "12 of 13".

Both have been fixed. Please verify the ADR is now internally consistent about:
- The migration is COMPLETE (no future-tense "Session 2 will..." text remaining)
- 12 of 13 fixtures were migrated to v1.0; fixture 12 excluded by stop rule -- all mentions agree
- Version-detection guidance refers to correct paths (Modules[n].ParserOutput.SchemaVersion, not document.Source.SchemaVersion)

## docs/schema/v1-adr.md (full current text)
```
{adr}
```

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

print(f"R3 prompt: {len(prompt)} chars")
result = ai_router.route(
    content=prompt,
    task_type="session-verification",
    session_set="harvester-schema-v1-adr-followups",
    session_number=1,
)
output = pathlib.Path("docs/session-sets/harvester-schema-v1-adr-followups/session-reviews/session-001-r3.md")
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(result.content, encoding="utf-8")
print(f"Saved to {output}")
print("--- LAST 500 CHARS ---")
print(result.content[-500:])
