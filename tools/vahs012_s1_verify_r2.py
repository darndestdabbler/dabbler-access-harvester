"""Set 012 Session 1 cross-provider verification — Round 2 (post-fix).
Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"
OUT = os.path.join(SS, "s1-verification-round-2.md")


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def main():
    builder = read("AccessHarvester.Pipeline/Enrichment/SwitchboardStructureBuilder.cs")
    tests = read("tests/AccessHarvester.Pipeline.Tests/SwitchboardStructureBuilderTests.cs")
    exporter = read("AccessHarvester.Core/Extraction/SwitchboardExporter.cs")
    model = read("AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs")
    s = model.find("public sealed record SwitchboardPage")
    model_excerpt = model[s:s + 3400]

    prompt = f"""You are performing Round 2 of an INDEPENDENT cross-provider verification.
Round 1 (your prior pass) returned ISSUES_FOUND with three findings; all three were
addressed. Your job: confirm the fixes are correct and complete and look for any
NEW defect they introduce. Begin with prose analysis per finding, THEN the JSON block.
Prefer false-positive ISSUES_FOUND over false-negative VERIFIED.

Context unchanged: set 012 S1, R2-1/DW-9, mechanical-no-interpretation harvester.
Validation after the fixes: solution builds (0 errors); AccessHarvester.Pipeline.Tests
52/52 on the affected classes (incl. 2 new regression pins below); the HomeHealthCare
COM golden was regenerated and CanonicalGoldenRegressionTests +
ManifestSchemaConformanceTests pass 6/6 (TRX-confirmed). The golden manifest delta
this round is the new page-level `Table` field; HHC command/id values are all
non-null so no value changed from coercion removal.

# Round 1 findings and how each was addressed

**R1-Major-1** — "structured projection silently coerces nullable raw numerics to 0
(NULL Command / missing provenance id indistinguishable from a real 0)."
FIX: `SwitchboardItem.Command`, `SwitchboardItemProvenance.SwitchboardId` and
`.ItemNumber` are now `int?`. The builder passes the raw nullable values straight
through (no `?? 0`). The manifest serializer omits null (WhenWritingNull), so a NULL
Command is omitted, never a synthetic 0. New pin:
`Null_command_and_ids_are_preserved_not_coerced_to_zero`.

**R1-Major-2** — "multi-table switchboards appended without page-level source
identity; SwitchboardId overloaded, page identity ambiguous across tables."
FIX: added `SwitchboardPage.Table` (page-level source identity). Grouping is already
per-table and deterministic (DetectedTables sorted ordinally in the exporter, pages
ordered by page key within each table). New pin:
`Multiple_tables_keep_distinct_page_source_identity`.

**R1-Minor-3** — "ReadStartUpForm catches all and Export still records success, so a
genuine DAO read failure collapses silently to null."
FIX: `ReadStartUpForm` now PROBES `db.Properties` for membership — absent → null with
no throw (the legitimate "no startup form" case); a present-but-unreadable property
lets the exception propagate to a new try/catch in `Export` that records
`ExportOutcome("startup-options", false, msg)`, so a real failure is visible while the
expected-absent case stays clean.

# Updated source under review

=== SwitchboardStructureBuilder.cs (full) ===
```csharp
{builder}
```

=== NavigationGraph.cs (SwitchboardPage / SwitchboardItem / SwitchboardItemProvenance excerpt) ===
```csharp
{model_excerpt}
```

=== SwitchboardExporter.cs (full) ===
```csharp
{exporter}
```

=== SwitchboardStructureBuilderTests.cs (full, incl. new pins) ===
```csharp
{tests}
```

Concerns: (A) are all three fixes correct and complete; (B) does making Command/ids
nullable introduce any NEW issue (ordering uses `?? int.MaxValue`; page Name uses the
lowest-ordered row); (C) is the StartUpForm membership-probe correct (does it cleanly
separate absent from read-error); (D) any remaining silent-drop/default or
multi-table ambiguity.

After your prose analysis, output a JSON block in this exact format:
```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{ "severity": "Critical" | "Major" | "Minor", "location": "<file>", "summary": "<one sentence>", "fix": "<recommended fix>" }}
  ]
}}
```
If verdict is VERIFIED, `issues` must be an empty array []. Do not omit this block.
"""

    r = route(content=prompt, task_type="session-verification", complexity_hint=72,
              session_set=SS, session_number=1)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
