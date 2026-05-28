"""Round 2 verification for harvester-object-use-identifier Session 2."""
import os, pathlib, sys

os.environ["AI_ROUTER_CONFIG"] = "ai_router/router-config.yaml"
os.environ["AI_ROUTER_METRICS_PATH"] = "ai_router/router-metrics.jsonl"

from ai_router import route
from ai_router.session_log import SessionLog

def read(p):
    return pathlib.Path(p).read_text(encoding="utf-8")

r1_raw = read("docs/session-sets/harvester-object-use-identifier/verification-r1-raw.txt")
classifier = read("AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs")
signalindex = read("AccessHarvester.Pipeline/UsageIdentification/SignalIndex.cs")
sqldetection = read("AccessHarvester.Pipeline/UsageIdentification/SqlDetection.cs")
recordsource = read("AccessHarvester.Pipeline/UsageIdentification/RecordSourceCollector.cs")
vbaref = read("AccessHarvester.Pipeline/UsageIdentification/VbaReferenceCollector.cs")
test_clf = read("tests/AccessHarvester.Pipeline.Tests/UsageIdentification/UsageClassifierTests.cs")

prompt = f"""Your response MUST begin with a detailed prose analysis for each issue below BEFORE the JSON block. Do not skip the analysis. Both the analysis and the JSON block are required.

You are performing Round 2 cross-provider session verification for Session 2 of harvester-object-use-identifier.

## Round 1 Issues and Fixes Applied

Round 1 returned ISSUES_FOUND with these findings:

**Major 1 (FIXED):** Form F4 branch applied to any table without transactional/lookup signals, misclassifying plain tables as Reporting. Fixed: the classifier now checks `index.KnownQueryNames.Contains(rs)` before entering the F4 path. If the RecordSource is a known query -> Reporting (F4). If it is an unknown plain table -> MainDataEntry at 0.50 confidence (table-without-signals branch). A regression test was added: `Form_PlainTable_RecordSource_No_FK_Is_MainDataEntry` and `Form_Query_RecordSource_Is_Reporting`.

**Major 2 (FIXED):** No findings for Module or Macro objects. Fixed: `ClassifyModule` and `ClassifyMacro` methods added; `Classify` now iterates `db.Modules` and `db.Macros`. Tests added: `Module_With_VbaCode_Is_StandaloneUtility`, `Macro_WithNavEdges_Is_NavigationShell`, `Macro_NoNavEdges_Is_StandaloneUtility`.

**Major 3 (FIXED):** Inline SQL detection only covered SELECT. Fixed: a `SqlDetection` helper was added covering SELECT, PARAMETERS, TRANSFORM, WITH, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER. All collectors and the SignalIndex now use this helper instead of `StartsWith("SELECT")`.

**Minor 1 (Accepted, not changed):** `IsCompletelyUnreferenced` not including NavigationSources. Reviewed: the strategy defines "standalone" as "not referenced by any form, report, query, VBA, or macro." An object that opens other objects is the *source* of navigation, not a reference target; it is still unreferenced (nothing references *it*). The predicate is intentionally asymmetric. No change needed.

**Minor 2 (FIXED):** Missing Q10/Q11/F4-boundary tests. Fixed: `Query_Q10_Union_UsedAsFormRecordSource_Is_MainDataEntry`, `Query_Q10_Union_Unreferenced_Is_PossiblyUnused`, `Form_PlainTable_RecordSource_No_FK_Is_MainDataEntry` added.

## Verification Questions for R2

1. Does the F4 fix correctly distinguish query RecordSources from table RecordSources? Is the `Form_PlainTable_RecordSource_No_FK_Is_MainDataEntry` test meaningful and does it cover the failure mode?
2. Are the Module/Macro classification paths reasonable given the strategy only defines taxonomy rules for Forms/Reports/Queries/Tables? Is there any schema compliance risk with objectType values "Module" and "Macro"?
3. Does `SqlDetection.IsInlineSql` correctly handle the word-boundary requirement (e.g., "Selected" should not be treated as SELECT SQL)?
4. Are there any remaining material classification rule gaps that would affect the Session 3 pilot?

## Updated Files

### UsageClassifier.cs (F4 fix + Module/Macro classifiers)
```csharp
{classifier[:5500]}
```

### SignalIndex.cs (KnownQueryNames + SqlDetection usage)
```csharp
{signalindex}
```

### SqlDetection.cs (new shared helper)
```csharp
{sqldetection}
```

### RecordSourceCollector.cs (SqlDetection usage)
```csharp
{recordsource}
```

### VbaReferenceCollector.cs (SqlDetection usage)
```csharp
{vbaref}
```

### UsageClassifierTests.cs (new tests)
```csharp
{test_clf[-3000:]}
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

print(f"R2 prompt length: {len(prompt)} chars, sending...")
pathlib.Path("docs/session-sets/harvester-object-use-identifier/verification-r2-raw.txt").write_text("", encoding="utf-8")

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=65,
    session_set="harvester-object-use-identifier",
    session_number=2,
)

raw = result.content
pathlib.Path("docs/session-sets/harvester-object-use-identifier/verification-r2-raw.txt").write_text(raw, encoding="utf-8")
print("R2 result written.")
print(raw[:500])

log = SessionLog("docs/session-sets/harvester-object-use-identifier", total_sessions=3)
log.log_step(2, 7, "verification-r2", "Cross-provider session verification Round 2", status="ok")
