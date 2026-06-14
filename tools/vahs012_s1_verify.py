"""Set 012 Session 1 cross-provider verification (R2-1 / DW-9).
Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"
OUT = os.path.join(SS, "s1-verification.md")

FILES = [
    "AccessHarvester.Core/Extraction/SwitchboardSignatureDetector.cs",
    "AccessHarvester.Core/Extraction/SwitchboardExporter.cs",
    "AccessHarvester.Pipeline/Enrichment/SwitchboardArtifact.cs",
    "AccessHarvester.Pipeline/Enrichment/SwitchboardStructureBuilder.cs",
    "AccessHarvester.Pipeline/Enrichment/MacroChainBuilder.cs",
    "tests/AccessHarvester.Tests/SwitchboardSignatureDetectorTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/SwitchboardStructureBuilderTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/MacroChainBuilderTests.cs",
]


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def main():
    files_block = "\n\n".join(f"=== {p} ===\n```csharp\n{read(p)}\n```" for p in FILES)

    # The model extension + Pipeline wiring (key excerpts only).
    model_ext = read("AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs")
    # Trim to the switchboard region to keep the prompt bounded.
    start = model_ext.find("public sealed record SwitchboardStructure")
    model_excerpt = model_ext[start:start + 3500]

    pipe = read("AccessHarvester.Core/Pipeline.cs")
    bni = pipe.find("internal static NavigationGraphInputs? BuildNavigationInputs")
    pipe_excerpt = pipe[bni:bni + 2600]
    callsite = pipe.find("// ── 2b. Navigation graph")
    callsite_excerpt = pipe[callsite:callsite + 1300]

    prompt = f"""You are an expert C#/.NET reviewer performing an INDEPENDENT cross-provider
verification of one session's work. Your response MUST begin with a detailed
prose analysis addressing each concern below, BEFORE the JSON block. Both the
prose and the JSON block are required. Prefer a false-positive ISSUES_FOUND over
a false-negative VERIFIED.

# Context

Repo: dabbler-access-harvester — a C#/.NET 10 toolchain that mechanically harvests
Microsoft Access metadata for a downstream AI consumer. Core principle: MECHANICAL
EXTRACTION, PRESERVE RAW, NO INTERPRETATION — interpretive slots stay null; the
downstream AI interprets. Lean by default.

This is **Session 1 of 3** of set `012-switchboard-and-report-chain-edges`,
delivering **R2-1 / DW-9**: materialise the switchboard rows (previously in NO
harvested artifact) and populate the already-modelled NavigationGraphInputs
(Switchboard tree, ordered EntryPoints, Macros), retiring the production
`NavigationGraphBuilder.Build(inputs: null)` call. R2-2 (chain edges) is sessions 2-3,
explicitly OUT of scope.

# Spec Hard Constraints (carry verbatim)

- NEVER regenerate/commit any NorthwindDevEdition artifact. Golden regen is
  HomeHealthCare-only; regenerate once per session after behavioural changes;
  every golden delta must map to R2-1. The new row-read must stay NARROW and
  SHAPE-GATED: detection by column-signature (not table name), acquisition a
  bounded ordered SELECT of only the canonical columns, gated to shape-matched
  tables; NOT a general row-data export surface. Unknown command codes / missing
  targets must never be silently dropped or defaulted.
- Do not weaken assertions/tests to force green.

# Ground-truth re-grounding (this session)

A direct OLE DB probe of HomeHealthCare's `tblSwitchboardItems` (52 rows) DISPROVED
the spec's premise that it is the canonical Access Switchboard Manager table. It is
hand-rolled custom: `SwitchboardID` is an autonumber PK (unique per row, NOT a page
id); `ParentID` is the real page key (0=main menu, 1=Reports Menu, NULL=32 embedded
"Plan N status" audit rows that are NOT navigation); command codes are custom
{{0=label,1=open form,2=go to page,3=open report}} — NOT the canonical 1-8 set.

# Design decisions made this session (DISCLOSED for your scrutiny — challenge them)

1. **Human-approved generic faithful-mechanical design** (the user, a former Access
   developer, explicitly chose this and added "we should be able to document both
   custom and generic switchboards — it shouldn't matter"): detection by column
   signature; lossless raw row dump of all canonical columns + ParentID when present;
   `SwitchboardStructure` page key = ParentID-if-the-column-exists else SwitchboardID;
   page Name = lowest-ItemNumber row's text; raw `Command` int preserved with NO
   canonical decode; null-page-key rows preserved in the raw dump but excluded from
   pages and counted in `UnlinkedItemCount`.
2. **DAO recordset row-read, NOT a second OLE DB connection.** The spec sketch said
   "through the existing OleDbValueSampler discipline." I honoured the DISCIPLINE
   (EscapeIdentifier-guarded identifiers, bounded ordered SELECT, degradation via
   ExportOutcome) but read via DAO on the already-open COM session (the extractor
   tier's native path — TableExporter/QueryExporter all use DAO), avoiding a second
   OLE DB connection against the COM-held file. Is this defensible?
3. **No `UnresolvedNavigationTarget` emitted for switchboard rows.** That record is
   VBA-dispatch-shaped (SourceModule/Procedure/Line/Action). Determining "unknown
   command" or "missing target" mechanically would require assuming a command set,
   which the no-interpretation principle and the custom-table reality forbid. Raw
   preservation (raw Command int + nullable Argument, all rows in the lossless dump)
   IS the "never silently dropped/defaulted" guarantee. Is this an acceptable reading
   of the Hard Constraint, or a real gap?
4. **StartUpForm DAO database-property read** added for DefaultFormName (a second
   small metadata read beyond the row-read) — human-approved; it's metadata not
   row-data, serves EntryPoints ordering. On HHC it yields `frmSwitchboard`.
5. **Model extension is additive** on `additionalProperties`-tolerant schema defs
   (no envelope version bump): `SwitchboardItem.Provenance`, `SwitchboardItemProvenance`,
   `SwitchboardStructure.UnlinkedItemCount`. Macro `Arguments` use documented names
   for known actions, deterministic `Argument{{n}}` fallback for unknown ones.
6. **Ribbon stays null** on HHC (switchboard-driven, no USysRibbons producer) — a
   recorded decision, deferred.

# Validation results

- Build: succeeds, 0 errors.
- Hermetic tests: AccessHarvester.Pipeline.Tests 846/846 pass (incl. new
  SwitchboardStructureBuilderTests + MacroChainBuilderTests); SwitchboardSignatureDetectorTests 7/7.
- COM golden regen (HomeHealthCare): CanonicalGoldenRegressionTests +
  ManifestSchemaConformanceTests 6/6 pass (TRX-confirmed). Golden deltas: manifest.json
  NavigationGraph now populated (EntryPoints: DefaultForm frmSwitchboard + 2 SwitchboardRoots;
  Switchboard: 2 pages [ParentID 0,1] of 10 items each, UnlinkedItemCount=32; Macros: 4);
  new kb/Navigation/ + ir/Navigation/ (switchboard-items.json with all 52 rows +
  startup-options.json). Every delta maps to R2-1.

# Concerns to address explicitly

A. Correctness of `SwitchboardStructureBuilder` for BOTH canonical and custom tables
   (page grouping, name selection, ordering determinism, null-parent handling).
B. Whether the design honours the Hard Constraints (narrow/shape-gated read; no
   silent drop/default) given decisions #2 and #3 — push back if you disagree.
C. `SwitchboardExporter` correctness/safety (EscapeIdentifier use, DAO recordset
   close in finally, DBNull handling, deterministic ordering, StartUpForm guard).
D. `MacroChainBuilder` (positional arg mapping, scalar-vs-array, RunCode parens strip,
   comment-only Begin nodes skipped, determinism).
E. Any way the new row-read drifts toward a "general row export surface."
F. Schema/model additive-only correctness (no envelope bump needed).

# Source under review

{callsite_excerpt}

=== Pipeline.cs BuildNavigationInputs helper ===
```csharp
{pipe_excerpt}
```

=== NavigationGraph.cs (switchboard model excerpt) ===
```csharp
{model_excerpt}
```

{files_block}

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
