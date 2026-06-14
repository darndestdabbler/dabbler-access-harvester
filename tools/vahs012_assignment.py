"""Route the ai-assignment.md orchestrator analysis for set 012
(switchboard-and-report-chain-edges). Persist-before-display."""
import os
from ai_router import route

SESSION_SET = "docs/session-sets/012-switchboard-and-report-chain-edges"
OUT = os.path.join(SESSION_SET, "ai-assignment-analysis.md")

CONTENT = r"""
Recommend the cheapest CAPABLE orchestrator (engine, model, effort) for each of
the three sessions of session set 012-switchboard-and-report-chain-edges, each with
a one-sentence rationale, plus an estimated routed-cost band (low/moderate/high).
Output a short markdown section per session. This is 006's P4, successor to 011 (P3).

Set context: 3-session Full-tier set delivering two deferred consumer requests over
the navigation substrate 011 wired. No UI (quality gate = build + hermetic tests +
COM golden regen + mandatory cross-provider session-verification). Cross-provider
verification is mandatory regardless of orchestrator.

Session 1 (IN PROGRESS now, R2-1 / DW-9 — switchboard row materialization + entry
points): orchestrated by Claude Code Opus 4.8 @ effort=high. Work turned out
synthesis-heavy: re-grounding disproved the spec's premise (HHC's tblSwitchboardItems
is a hand-rolled CUSTOM nav table — autonumber PK, ParentID tree, command codes 0-3,
embedded audit rows — NOT the canonical Access Switchboard Manager table), which
forced a human decision and a generic (canonical + custom) faithful-mechanical design:
a pure column-signature detector, a generic SwitchboardStructure builder (page key =
ParentID-if-present else SwitchboardID; raw Command int preserved, no canonical
decode), a new DAO switchboard row-read exporter (Category Navigation) writing
raw/Navigation/switchboard-items.json, a StartUpForm DAO read, a MacroChain producer
from the macro IR, model extension (SwitchboardItem provenance), wiring the populated
NavigationGraphInputs into the production NavigationGraphBuilder.Build (retiring
inputs:null), hermetic tests, and a coordinated COM golden regen.

Session 2 (NEXT, R2-2 part 1 / DW-10 — chain-edge model + mechanical producers):
extend the NavigationEdge family with a closed chain-edge kind + provenance set;
build record-source (named-query + inline-SQL via the jet-sql parser), embedded-macro
(OnClickEmMacro/OnOpenEmMacro block text), and declared-parameter (IrQuery.Parameters)
edge producers; hermetic pins; coordinated golden regen. Substantial new derivation
logic over existing structured models; moderate-to-high design surface (closed edge
kind/provenance set design).

Session 3 (FINAL, R2-2 part 2 / DW-10 — code-behind chain edges + closeout): derive
chain edges needing the resolver's Access-integration layer (acDialog OpensDialog,
Forms!dlg!control SuppliesParameter, TempVars flows); explicit unresolved for dynamic
targets; interpretation-guide updates; author the next session set; closeout-grade
cross-provider verification (prose-first + set-wide devil's-advocate addendum mining
earlier-session weak-evidence closures). Synthesis + closeout heavy.

Consider: synthesis/judgment depth per session, that the human runs
Claude/Codex/Gemini orchestrators interchangeably, and that mechanical-correspondence
routed sub-tasks calibrate to Sonnet while synthesis earns Opus.
"""


def main() -> None:
    r = route(content=CONTENT, task_type="analysis", session_set=SESSION_SET,
              session_number=1,
              context="Author per-session orchestrator recommendations for ai-assignment.md "
                      "per docs/ai-led-session-workflow.md Step 3.5.")
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
