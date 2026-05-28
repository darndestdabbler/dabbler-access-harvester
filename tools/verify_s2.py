"""Prepare and send session-2 verification prompt for harvester-object-use-identifier."""
import os, pathlib, sys

os.environ["AI_ROUTER_CONFIG"] = "ai_router/router-config.yaml"
os.environ["AI_ROUTER_METRICS_PATH"] = "ai_router/router-metrics.jsonl"

from ai_router import route
from ai_router.session_log import SessionLog

def read(p):
    return pathlib.Path(p).read_text(encoding="utf-8")

strategy = read("docs/planning/object-use-identification-strategy.md")
schema   = read("AccessHarvester.Pipeline/UsageIdentification/object-use-schema.json")
irdatabase   = read("AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs")
usagefinding = read("AccessHarvester.Pipeline/UsageIdentification/UsageFinding.cs")
signalindex  = read("AccessHarvester.Pipeline/UsageIdentification/SignalIndex.cs")
classifier   = read("AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs")
questiongen  = read("AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs")
checklistwriter = read("AccessHarvester.Pipeline/UsageIdentification/UsageChecklistWriter.cs")
navEdge      = read("AccessHarvester.Pipeline/UsageIdentification/NavigationEdgeCollector.cs")
recordsource = read("AccessHarvester.Pipeline/UsageIdentification/RecordSourceCollector.cs")
vbaref       = read("AccessHarvester.Pipeline/UsageIdentification/VbaReferenceCollector.cs")
command      = read("AccessHarvester.Cli/Commands/IdentifyUsageCommand.cs")
test_clf     = read("tests/AccessHarvester.Pipeline.Tests/UsageIdentification/UsageClassifierTests.cs")
test_qg      = read("tests/AccessHarvester.Pipeline.Tests/UsageIdentification/QuestionGeneratorTests.cs")
test_cw      = read("tests/AccessHarvester.Pipeline.Tests/UsageIdentification/UsageChecklistWriterTests.cs")

prompt = f"""Your response MUST begin with a detailed prose analysis for each concern below BEFORE the JSON block. Do not skip the analysis. Both the analysis and the JSON block are required.

You are performing cross-provider session verification for Session 2 of the harvester-object-use-identifier session set.

## Session Summary

Session 2 of 3 (CLI Analysis Code) implemented the `AccessHarvester identify-usage` subcommand. This session adds:
- Data model: IrDatabase (in-memory IR representation), IrLoader (disk loader), UsageFinding (JSON output model)
- 6 signal collectors: RecordSourceCollector, RowSourceCollector, NavigationEdgeCollector, VbaReferenceCollector, QueryTableReferenceCollector, RelationshipCollector
- SignalIndex (cross-reference index), UsageClassifier (classification rules), QuestionGenerator (uncertainty question templates)
- UsageChecklistWriter (writes object-use.json and object-use-checklist.json)
- IdentifyUsageCommand wired into Program.cs as the `identify-usage` subcommand
- 76 unit tests covering each component, all pass

## Verification Questions

1. **Classification rule coverage**: Does UsageClassifier implement the rules from the strategy doc (Forms F1-F8, Reports R1-R3, Queries Q1-Q11, Tables T1-T7)?  Are any rules missing or materially divergent?
2. **Signal collector correctness**: Do the 6 collectors correctly extract signals from the IrDatabase model? Are any signal extraction patterns obviously wrong?
3. **Output schema compliance**: Does UsageChecklistWriter produce JSON matching the object-use-schema.json structure? Are the camelCase field names correct?
4. **Test coverage**: Do the unit tests adequately cover the classification rules, especially the query sub-rules (Q8 vs Q9, Q10 Union handling, Q11)?
5. **Design note on RowSource**: The form IR JSON files only contain embedded macro action Children nodes, NOT the actual form control tree (ComboBox/ListBox with RowSource properties). RowSourceCollector falls back to scanning VBA code for static `.RowSource = "tableName"` assignments. This is a documented limitation — out of scope for Session 2 per spec stop rules.

## Strategy (authoritative spec)

{strategy[:2500]}

## JSON Schema

{schema}

## UsageClassifier.cs (core classification logic)

{classifier[:5000]}

## QuestionGenerator.cs

{questiongen}

## UsageFinding.cs (output model)

{usagefinding}

## SignalIndex.cs

{signalindex}

## UsageChecklistWriter.cs

{checklistwriter}

## NavigationEdgeCollector.cs

{navEdge}

## RecordSourceCollector.cs

{recordsource}

## VbaReferenceCollector.cs

{vbaref}

## UsageClassifierTests.cs (key tests)

{test_clf[:3500]}

## UsageChecklistWriterTests.cs (output format tests)

{test_cw[:2000]}

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

print(f"Prompt length: {len(prompt)} chars, sending to verifier...")
pathlib.Path("docs/session-sets/harvester-object-use-identifier/verification-s2-r1-prompt.txt").write_text(prompt, encoding="utf-8")

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=70,
    session_set="harvester-object-use-identifier",
    session_number=2,
)

raw = result.content
pathlib.Path("docs/session-sets/harvester-object-use-identifier/verification-r1-raw.txt").write_text(raw, encoding="utf-8")
print("Verification result written.")
print(raw[:500])

log = SessionLog("docs/session-sets/harvester-object-use-identifier", total_sessions=3)
log.log_step(2, 6, "session-verification", "Cross-provider session verification Round 1", status="ok")
