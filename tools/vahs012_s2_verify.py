"""Set 012 Session 2 cross-provider verification (R2-2 part 1 / DW-10).
Chain-edge model + mechanical producers. Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"
OUT = os.path.join(SS, "s2-verification.md")

FULL_FILES = [
    "AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
    "tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs",
]


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def excerpt(path, anchor, length):
    t = read(path)
    i = t.find(anchor)
    return t[i:i + length] if i >= 0 else f"(anchor {anchor!r} not found)"


def main():
    full_block = "\n\n".join(
        f"=== {p} ===\n```csharp\n{read(p)}\n```" for p in FULL_FILES)

    chain_model = excerpt(
        "AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs",
        "// ── Chain edges (R2-2 / DW-10)", 6500)
    ng_props = excerpt(
        "AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs",
        "public IReadOnlyList<ChainEdge> ChainEdges", 900)
    ir_model = excerpt(
        "AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs",
        "public IReadOnlyList<IrControlEventMacro>? EventMacros", 1500)
    ir_loader = excerpt(
        "AccessHarvester.Pipeline/UsageIdentification/IrLoader.cs",
        "private static IReadOnlyList<IrControlEventMacro>? ReadEventMacros", 1700)
    pipe_wire = excerpt(
        "AccessHarvester.Core/Pipeline.cs",
        "// 012 S2 (DW-10 / R2-2): derive the cross-object", 900)
    schema = excerpt(
        "docs/test-cases/harvester-schema.v1.1.json",
        '"ChainEdge": {', 1500)
    irloader_test = excerpt(
        "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/IrLoaderTests.cs",
        "Control_EventMacros_Block_Is_Captured", 1700)

    prompt = f"""You are an expert C#/.NET reviewer performing an INDEPENDENT cross-provider
verification of one session's work. Your response MUST begin with a detailed prose
analysis addressing each concern below, BEFORE the JSON block. Both the prose and the
JSON block are required. Prefer a false-positive ISSUES_FOUND over a false-negative
VERIFIED.

# Context

Repo: dabbler-access-harvester — a C#/.NET 10 toolchain that MECHANICALLY harvests
Microsoft Access metadata for a downstream AI consumer. Core principle: mechanical
extraction, preserve raw, NO interpretation (interpretive slots stay null); explicit-
unresolved over silent-drop; deterministic output; lean by default.

This is **Session 2 of 3** of set `012-switchboard-and-report-chain-edges`, delivering
**R2-2 part 1 / DW-10**: the chain-edge MODEL plus the MECHANICAL producers that derive
the launcher -> report -> dialog -> query -> parameter chain over the IR surfaces
(embedded macros, record sources, query parameters). Session 1 (R2-1) already landed the
switchboard tree / EntryPoints / Macros. **Session 3 (out of scope here)** adds the
VBA code-behind hops (`acDialog` -> OpensDialog, `Forms!dlg!ctl` reads, `TempVars!x =`
assignments) — this session touches NO VBA.

# Spec — Session 2 steps (verbatim summary)

- Edge model: extend the NavigationEdge family with a CLOSED chain-edge kind set
  (OpensDialog, OpensReport, OpensForm, FeedsRecordSource, SuppliesParameter) carrying
  {{From, To, Kind, Provenance}} with the closed provenance source set
  (vba-call | embedded-macro | macro-action | record-source | query-form-reference |
  query-declared-parameter | tempvar-flow). Append to the set; do not rename.
- Record-source producer: named-query RecordSource -> FeedsRecordSource(query -> owner);
  inline-SQL record sources go through the jet-sql parser and emit edges for FROM/JOIN
  targets plus SuppliesParameter for TempVars!x / [Forms]!… tokens in the parsed tree.
- Embedded-macro producer: parse OnClickEmMacro/OnOpenEmMacro block text ->
  OpensReport/OpensForm edges from the owning form/control.
- Declared-parameter producer: project IrQuery.Parameters
  (declared/form-reference/report-reference) as SuppliesParameter edges, or explicit
  unresolved when the supplier is unknowable (prompt-style runtime parameters).
- Hermetic pins per producer; single coordinated HHC golden regen; every delta maps to R2-2.

# Spec Hard Constraints (carry verbatim)

- Golden regeneration is HomeHealthCare-only; never regenerate any NorthwindDevEdition
  artifact. Every golden delta must be explained by R2-2.
- Do not weaken assertions/tests to force green. Reconcile any hermetic test that
  exercised unwired models with the wired path; do not delete coverage.
- Scope is R2-2 only; stop at the boundary and record a DW entry rather than pulling in
  DW-8 dynamic-SQL wiring, grammar-completeness items, etc.

# Ground-truth re-grounding (this session — DISCLOSED; challenge these)

The HHC fixture realises the chain: frmReportMenu (4 OnClick embedded macros ->
OpenReport) ; reports' Report_Open VBA opens frmReportParam_* via acDialog (S3) ;
report/sub-report RecordSources are named queries/tables OR inline SQL ; two sub-reports'
inline record sources reference TempVars!repCID/!repFY ; two queries declare prompt-style
PARAMETERS ([Enter Provider ID:] etc.). Re-grounding surfaced TWO deviations from the
spec's literal wording — please scrutinise both:

1. **Embedded-macro producer consumes the enricher's already-structured `EventMacros`
   block (named args, derived from the canonical `_AXL` XML), NOT a re-parse of the raw
   `OnClickEmMacro` block-format property string.** MacroActionsEnricher already parses
   the AXL into `EventMacros: {{ OnClick: {{ Actions: [{{Action, Arguments}}] }} }}` in the
   form IR. IrLoader did not previously surface it; this session extends IrControl to
   carry it (additive optional field). Consuming the structured form is DRY and more
   faithful (named args) than re-parsing legacy block text. Is this defensible, or does
   it risk missing edges when a control has an `*EmMacro` property but the enricher
   produced no `EventMacros` entry?
2. **TempVars!x is recovered by a TEXTUAL regex scan, not from the parsed tree.** The JET
   SQL grammar models bang-access (`!`) ONLY for Forms/Reports roots (the `formReference`
   rule); there is no generic postfix-bang in `primaryExpression`, so `TempVars!repCID`
   is not a parseable node. `[Forms]!`/`[Reports]!` ARE taken from the parsed AST
   (FormReference). FROM/JOIN targets come from the parsed AST (SqlSyntaxWalker subclass).
   Is the textual TempVars scan an acceptable reconciliation of "tokens in the parsed
   tree", and is the regex safe?

# Design decisions (challenge them)

- `ChainEdgeKind` is a PascalCase enum (matches spec); `Provenance` is a CLOSED kebab-case
  STRING set (mirrors UnresolvedNavigationTarget.Reason's raw-string precedent so on-disk
  values match the spec verbatim). `vba-call` is produced by the S3 code-behind pass;
  `macro-action` is RESERVED (no producer in this set) — documented as such. Is a reserved-
  but-documented closed-set value acceptable (the spec fixed the full set up front and
  forbids renaming)?
- Endpoints are `ChainEndpoint {{ Kind, Name, Member? }}` (Member = the launching control /
  referenced control / token). FROM/JOIN target object kind is resolved against the
  IrDatabase query/table NAME sets -> Query | Table | Unknown (named-but-not-found is
  Unknown, never dropped).
- Direction: launcher -> target for the open kinds; supplier -> consumer for
  FeedsRecordSource (source -> owner) and SuppliesParameter (supplier -> consumer).
- Attached to the existing NavigationGraph via `with {{ ChainEdges, UnresolvedChainTargets }}`
  so the VBA-side Edges are untouched; both new arrays default to empty and are always
  emitted. Schema: additive optional properties on the `additionalProperties`-tolerant
  NavigationGraph `$def` -> NO envelope version bump.
- Inline record source that fails to parse -> explicit `UnresolvedChainTarget`
  (reason `record-source-unparsed`); prompt-style declared parameter -> explicit
  `UnresolvedChainTarget` (reason `prompt-parameter`).

# Validation results

- Build: succeeds, 0 errors.
- Hermetic: AccessHarvester.Pipeline.Tests 858/858 pass (incl. new
  NavigationChainBuilderTests 13 + IrLoader EventMacros pin); the 329 IrDatabase-consumer
  tests (classifier/usage/query-abstraction) unaffected by the additive IrControl field.
- COM golden regen (HomeHealthCare): GoldenCorpusRegenerator passed; CanonicalGolden-
  RegressionTests + ManifestSchemaConformanceTests 6/6 pass (TRX-confirmed). Golden delta:
  manifest.json NavigationGraph gains ONLY `ChainEdges` (37) + `UnresolvedChainTargets` (3);
  no other manifest change; no kb/ir tree change (chain edges live only in the manifest).
  Edge breakdown: 4 OpensReport (embedded-macro), 29 FeedsRecordSource (record-source),
  4 SuppliesParameter (tempvar-flow). Unresolved: 3 prompt-parameter (1 from
  qryClientServicePlan_ByClient, 2 from qryProviderPlansByClient_Param). Every delta maps
  to R2-2. The form-reference / report-reference SuppliesParameter branch and the
  record-source-unparsed branch are golden-neutral on HHC (zero such cases) and are covered
  by SYNTHETIC hermetic pins.

# Concerns to address explicitly

A. Correctness of each producer (embedded-macro OpenReport/OpenForm; record-source named +
   inline FROM/JOIN; declared/form-reference/report-reference parameters; prompt unresolved).
B. The two re-grounding deviations (consume EventMacros vs re-parse block text; TempVars
   textual scan) — are they correct and faithful? Push back if you disagree.
C. Closed-set discipline: enum Kind + string Provenance; reserved vba-call/macro-action.
D. Determinism (the SortEdges / SortUnresolved comparators — are they total/stable?).
E. SQL parse resilience: Recovering mode + try/catch -> record-source-unparsed; FROM/JOIN
   collection completeness via the SqlSyntaxWalker subclass (nested parens / subqueries);
   the `parsed` flag semantics (does catching mask a partial-but-empty parse?).
F. Endpoint classification + From/To direction correctness; Member null-omission.
G. Schema additive-only correctness (no envelope bump); always-emitted arrays.
H. Scope discipline: no VBA touched (S3 owns vba-call); no test weakened.
I. Any Lean-By-Default over-reach or any silent-drop / default-injection.

# Source under review

=== NavigationGraph.cs — new NavigationGraph properties ===
```csharp
{ng_props}
```

=== NavigationGraph.cs — chain-edge model (records + enums) ===
```csharp
{chain_model}
```

=== IrDatabase.cs — IrControl.EventMacros + IrControlEventMacro / IrEventMacroAction ===
```csharp
{ir_model}
```

=== IrLoader.cs — ReadEventMacros (the two control-creation call sites pass EventMacros = ReadEventMacros(child)) ===
```csharp
{ir_loader}
```

=== Pipeline.cs — chain-builder wiring (after NavigationGraphBuilder.Build) ===
```csharp
{pipe_wire}
```

=== harvester-schema.v1.1.json — new $defs (ChainEdge / ChainEndpoint / UnresolvedChainTarget) + NavigationGraph gains optional ChainEdges + UnresolvedChainTargets ===
```json
{schema}
```

=== IrLoaderTests.cs — EventMacros parsing pin ===
```csharp
{irloader_test}
```

{full_block}

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

    r = route(content=prompt, task_type="session-verification", complexity_hint=75,
              session_set=SS, session_number=2)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
