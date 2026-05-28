"""Round 3 verification for harvester-object-use-identifier Session 2."""
import os, pathlib

os.environ["AI_ROUTER_CONFIG"] = "ai_router/router-config.yaml"
os.environ["AI_ROUTER_METRICS_PATH"] = "ai_router/router-metrics.jsonl"

from ai_router import route
from ai_router.session_log import SessionLog

def read(p):
    return pathlib.Path(p).read_text(encoding="utf-8")

classifier = read("AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs")
test_clf = read("tests/AccessHarvester.Pipeline.Tests/UsageIdentification/UsageClassifierTests.cs")

prompt = f"""Your response MUST begin with a detailed prose analysis for each issue below BEFORE the JSON block. Do not skip the analysis. Both the analysis and the JSON block are required.

You are performing Round 3 cross-provider session verification for Session 2 of harvester-object-use-identifier.

## Round 2 Issue and Fix Applied

**R2 Major (FIXED):** Forms/reports with inline SQL RecordSources were treated as having no RecordSource, misclassifying bound objects.

**Fix applied:**
- `ClassifyForm` now checks `!string.IsNullOrWhiteSpace(form.RecordSource)` for `hasRecordSource` (bound status). `rs` (named source) still comes from `index.RecordSourceByObject`.
- New branch: when `hasRecordSource && rs == null` (bound with inline SQL) → `MainDataEntry` at 0.60 confidence.
- `ClassifyReport` R1 now uses `!string.IsNullOrWhiteSpace(report.RecordSource)` so inline-SQL-bound reports are classified as Reporting.
- Regression test added: `Form_InlineSql_RecordSource_Is_MainDataEntry`.
- All 511 tests pass.

## Verification Question

Does the inline SQL RecordSource fix correctly separate "is this form/report bound?" from "does it reference a named saved object?"  Is the `MainDataEntry` at 0.60 classification for inline-SQL-bound forms appropriate per the strategy?

## Updated Classifier (relevant sections)

```csharp
{classifier[:4000]}
```

## New Test

```csharp
    [Fact]
    public void Form_InlineSql_RecordSource_Is_MainDataEntry()
    {{
        // An inline SQL RecordSource means the form IS bound (not unbound),
        // but the SQL string is not a named table/query object.
        var db = EmptyDb() with
        {{
            Forms = [Form("frmInlineSql", "SELECT * FROM tblClients")],
        }};
        var f = Classify(db, "frmInlineSql");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
    }}
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

print(f"R3 prompt: {len(prompt)} chars, sending...")
pathlib.Path("docs/session-sets/harvester-object-use-identifier/verification-r3-raw.txt").write_text("", encoding="utf-8")

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=55,
    session_set="harvester-object-use-identifier",
    session_number=2,
)

raw = result.content
pathlib.Path("docs/session-sets/harvester-object-use-identifier/verification-r3-raw.txt").write_text(raw, encoding="utf-8")
print("R3 result written.")
print(raw[:400])

log = SessionLog("docs/session-sets/harvester-object-use-identifier", total_sessions=3)
log.log_step(2, 8, "verification-r3", "Cross-provider session verification Round 3", status="ok")
