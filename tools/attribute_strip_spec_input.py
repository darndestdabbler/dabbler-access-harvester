"""Cross-engine input on the `vba-attribute-header-strip` session-set scope.

Two open spec-authoring questions surfaced when scoping the new session set:
  (Q1) Audit scope of call sites: every VBA-parsing call site (resolver,
       classifier, navigation graph, dynamic-SQL analyzer, parse-success-
       report generator), or just the high-value consumers, leaving the
       rest for organic adoption?
  (Q2) What does "strip" mean structurally: drop the VERSION/BEGIN/END/
       Attribute VB_* block entirely, or preserve it as parallel
       `moduleHeader` metadata on the parsed AST so downstream consumers
       that need VB_Name etc. can still reach it?

Routes the SAME framing to gpt-5-4 (OpenAI) and gemini-pro (Google) and
saves each response before any display side effect.
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import query

OUT_DIR = Path("docs/strategy-input")
OUT_DIR.mkdir(parents=True, exist_ok=True)

framing = """\
You are advising on the scope of a new session set in the Dabbler Access
Harvester project: `vba-attribute-header-strip`. The harvester's purpose is
to extract Microsoft Access metadata at sufficient structural fidelity
that a downstream AI consumer can interpret and document the original
application. The downstream consumer is an AI agent, not a human reviewer
— outputs are JSON; precision, completeness, and machine-readability
matter; layouts and styling do not. The project's stated posture is
"refine to do it right" rather than "ship the quick heuristic"
(confirmed in a prior cross-engine consult on a related issue where both
engines independently recommended grammar refinement over a token-sniff
heuristic).

# The problem this set fixes

When Access exports a form or report's code-behind module via SaveAsText
(producing the YAML mirror), the module body begins with a header block:

```
VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "Form_frmClientEdit"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
```

Standalone `.bas` modules typically don't carry this header. The VBA
grammar (`Vba.g4`) does not currently recognise the `VERSION`/`BEGIN`/
`END`/`Attribute VB_*` token sequence, so every code-behind module's
parse aborts. Concrete impact in the just-shipped Phase 4 parse-success
report:

* HomeHealthCare.accdb: 29 of 32 VBA modules parseFailed (~91% failure)
* NorthwindDevEdition.accdb: 52 of 66 VBA modules parseFailed (~79% failure)

The downstream-AI consumer can't read most of the VBA in the canonical
fixtures right now. The grammar gap is the sole blocker for those modules.

The human's decision: remove the header from the inputs the harvester
parses. The session set's placement choice was the **shared module-body
loader** approach — a new helper (e.g., `VbaModuleBodyLoader`) that all
VBA-parsing consumers route through, so the strip is a load-time concern
and the YAML mirror itself is preserved as-is for source-of-truth fidelity.

# Current parser entry point

```csharp
// AccessHarvester.Vba.Parser/VbaParserPipeline.cs
public static class VbaParserPipeline
{
    public static ParseResult Parse(string source, ParseOptions? options = null)
    {
        ...
    }
}
```

Call sites pass source strings directly. There is no module-body loader
or pre-processing layer today — every consumer reads its own source and
hands the string to `VbaParserPipeline.Parse`.

# Known consumers of VBA-parsed module bodies (varying fidelity needs)

| Consumer | What it needs from the parsed body |
|---|---|
| `AccessHarvester.Vba.Resolver` (declarations, scopes, references, type inference) | The full AST — procedures, declarations, statements |
| `AccessHarvester.Pipeline.Enrichment.FunctionClassifier` | Procedure-level body content for classification heuristics |
| `AccessHarvester.Pipeline.Enrichment.NavigationGraphBuilder` | DoCmd dispatch sites in procedure bodies |
| `AccessHarvester.Sql.Parser.DynamicSql.DynamicSqlAnalyzer` | String-concat builders for SQL execution sites |
| `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs` (the new harness) | Per-module parseOk/parseFailed counts |
| `AccessHarvester.Cli/Program.cs` (CLI) | Run-time parsing for the `parse` subcommand |
| Grammar / parser test corpus (`AccessHarvester.Vba.Grammar.Tests`) | Test fixtures often hand-built without headers — likely unaffected |

# Open spec-authoring questions

## Q1 — Audit scope of call sites

Two reasonable scopes for "route call sites through the new loader":

* **(Q1.a) Audit every VBA-parsing call site and route them all through
  the new loader.** Most consistent — all consumers see headerless module
  bodies. Larger blast radius — every call site touched in one session set;
  some call sites (e.g., test fixtures) may not need the strip at all.
* **(Q1.b) Strip only at known high-value consumer entry points** (parser
  pipeline tests + parse-success-report generator + resolver + classifier
  + navigation graph + dynamic-SQL analyzer when invoked from
  pipeline-level callers), leaving raw `Parse(source)` callers untouched
  for organic adoption. Smaller blast radius — leaves a "two paths"
  invariant where callers explicitly want raw can still get raw.

What would you recommend? If (a), how would you handle test-fixture call
sites that don't need the strip? If (b), where do you draw the line, and
how do you ensure downstream consumers don't accidentally use the raw
path?

## Q2 — What does "strip" mean structurally

Two reasonable shapes for what happens to the stripped header:

* **(Q2.a) Drop the header entirely.** The parser sees a clean module body;
  the `VERSION`/`BEGIN`/`END`/`Attribute VB_*` content is gone from the
  pipeline. Simplest. Lossy at the parser layer — a downstream consumer
  that wants `VB_Name` or `VB_GlobalNameSpace` cannot reach it without
  re-reading the YAML mirror.
* **(Q2.b) Preserve the header as parallel `moduleHeader` metadata** on
  the parsed `Module` AST node. The parser still sees a clean module body,
  but the AST carries a `Module.Header: ModuleHeaderMetadata?` slot with
  parsed fields (`VbName`, `VbGlobalNameSpace`, `VbCreatable`, `VbExposed`,
  `VbPredeclaredId`, `Multiuse`, the `VERSION` number, etc.). Non-lossy
  but a larger AST surface change.

The harvester's downstream consumer is an AI migration agent. Is
`VB_Name` / `VB_GlobalNameSpace` / `VB_Creatable` / `VB_Exposed` /
`VB_PredeclaredId` / `MultiUse` semantically valuable for that
consumer's migration task, or is it housekeeping noise the migration
shouldn't carry? Examples of how the consumer might use each:

* `VB_Name` — names the module (e.g., "Form_frmClientEdit"). But the
  YAML-mirror file path already encodes this.
* `VB_GlobalNameSpace` — whether the class is exposed globally. Migration
  targets (Blazor / React / etc.) typically don't have this concept.
* `VB_Creatable` — whether the class can be instantiated with `New`.
  Forms/reports are not user-instantiable in Access; this is always
  False for code-behind. Possibly meaningful for migration of standalone
  class modules (`.cls`).
* `VB_PredeclaredId` — whether the class has a "default instance"
  (`Form_frmFoo.Visible = True` works because of the predeclared
  instance). Migration-relevant for understanding why the original
  code uses the unqualified form name.
* `VB_Exposed` — whether external code can reference the class.
  Migration-relevant for understanding visibility.

What would you recommend? Drop entirely, or preserve as metadata? If
preserve, what shape — a flat map, a typed record, both? Are there fields
in the list above the downstream consumer genuinely needs vs. fields
that are housekeeping noise?

# Constraints + posture

* Lean-by-default — don't add fields, abstractions, or safety nets beyond
  what the task in front of us requires. But: "the downstream consumer
  is an AI agent that needs structural fidelity" overrides lean-by-default
  when the alternative is silent information loss.
* Determinism is non-negotiable for parser output.
* The session set is sized 1–2 sessions in the current Phase 4 plan; a
  larger surface change pushes it to 2-3 sessions.

# What I'd like from you

For Q1 and Q2 each:

1. Concrete recommendation with reasoning.
2. What you'd specifically include in the session-set spec to make the
   recommendation testable / verifiable.
3. Any edge case the spec author should pin explicitly so a future
   orchestrator doesn't have to re-litigate it.

Keep your response under ~700 words total across Q1 and Q2 combined.
Concrete + opinionated beats exhaustive.
"""

print("=" * 72)
print("Routing to gpt-5-4 (OpenAI)...")
gpt_result = query(
    model="gpt-5-4",
    content=framing,
    task_type="analysis",
    session_set="post-close-strategy-input",
    session_number=0,
)
gpt_path = OUT_DIR / "attribute-strip-spec-gpt-5-4.md"
gpt_path.write_text(gpt_result.content, encoding="utf-8")
print(f"gpt-5-4: ${gpt_result.cost_usd:.4f}, "
      f"in/out tokens: {gpt_result.input_tokens}/{gpt_result.output_tokens}")
print(f"Saved to {gpt_path}")
print()

print("=" * 72)
print("Routing to gemini-pro (Google)...")
gemini_result = query(
    model="gemini-pro",
    content=framing,
    task_type="analysis",
    session_set="post-close-strategy-input",
    session_number=0,
)
gemini_path = OUT_DIR / "attribute-strip-spec-gemini-pro.md"
gemini_path.write_text(gemini_result.content, encoding="utf-8")
print(f"gemini-pro: ${gemini_result.cost_usd:.4f}, "
      f"in/out tokens: {gemini_result.input_tokens}/{gemini_result.output_tokens}")
print(f"Saved to {gemini_path}")
print()

print("=" * 72)
total = gpt_result.cost_usd + gemini_result.cost_usd
print(f"Total routed cost: ${total:.4f}")
