"""Set 012 Session 2 closeout helpers: route the next-orchestrator
recommendation (do-not-self-opine) and log the session's activity steps."""
import os
from pathlib import Path
from ai_router import route
from ai_router.session_log import SessionLog

SS = Path("docs/session-sets/012-switchboard-and-report-chain-edges")


def route_next_orchestrator():
    prompt = """Recommend the orchestrator (engine + model + effort) for Session 3 of 3 of the
session set `012-switchboard-and-report-chain-edges` (a C#/.NET mechanical Access-metadata
harvester). Do NOT self-opine on cost; give a routed, reasoned recommendation.

Session 2 (just completed) landed the MECHANICAL R2-2 chain edges: a NavigationChainBuilder
deriving OpensReport/OpensForm (embedded macros), FeedsRecordSource (record sources incl.
inline-SQL FROM/JOIN), SuppliesParameter (query [Forms]!/[Reports]! refs + record-source
TempVars!), and explicit UnresolvedChainTargets (prompt parameters). Verified VERIFIED
(gpt-5-4, after a 4-round convergent fix arc).

Session 3 of 3 (per spec) must: (1) add the VBA code-behind chain edges (the resolver's
Access-integration layer) — `DoCmd.OpenForm ... acDialog` -> OpensDialog, `Forms!dlg!ctl`
member reads -> SuppliesParameter(dialog -> report), `TempVars!x =` assignments paired with
record-source TempVars references -> tempvar-flow (provenance `vba-call`); (2) explicit
unresolved for variable targets; (3) interpretation-guide updates for the full chain;
(4) final hermetic validation + coordinated golden regen; (5) AUTHOR THE NEXT SESSION SET
(last-session-authors-next-set: the remaining unowned 006 sets are P5 cli-input-robustness
and P6 cleanup-zero-date-guard); (6) closeout-grade cross-provider verification (prose-first
+ trailing JSON, embed earlier-session evidence).

Output 2-4 sentences: recommended orchestrator + the single highest-leverage reason."""
    r = route(content=prompt, task_type="analysis", complexity_hint=45,
              session_set=str(SS), session_number=2)
    (SS / "ai-assignment-analysis-s2.md").write_text(
        f"# Routed next-orchestrator analysis (S2 -> S3)\n\nModel: {r.model_name} | "
        f"cost ${r.cost_usd:.4f}\n\n{r.content}\n", encoding="utf-8")
    print(f"NEXT-ORCH ({r.model_name}, ${r.cost_usd:.4f}):\n{r.content}\n")


def log_steps():
    log = SessionLog(session_set_dir=str(SS), total_sessions=3)
    steps = [
        ("session-2/reground", "Re-grounded R2-2 surfaces against the tree: embedded EventMacros already enriched; TempVars not a JET AST node; report->dialog is VBA (S3). Wrote s2-regrounding-and-design.md.", []),
        ("session-2/chain-model", "Added closed ChainEdge family on NavigationGraph (ChainEdges/UnresolvedChainTargets; ChainEdgeKind enum; ChainEndpoint/ChainNodeKind; closed kebab-case provenance + reason sets).", []),
        ("session-2/ir-eventmacros", "Extended IrControl + IrFormOrReport with EventMacros; IrLoader captures control- and object-level EventMacros from the form/report IR.", []),
        ("session-2/producers", "NavigationChainBuilder: embedded-macro, record-source (named + inline-SQL FROM/JOIN via SqlSyntaxWalker; [Forms]!/[Reports]! refs; token-aware TempVars scan), and declared-parameter producers; explicit unresolved channel.", []),
        ("session-2/wiring-schema", "Wired NavigationChainBuilder into Pipeline.cs (attached via `with`); added ChainEdge/ChainEndpoint/UnresolvedChainTarget $defs + optional NavigationGraph properties to harvester-schema.v1.1 (no envelope bump).", []),
        ("session-2/tests-golden", "Hermetic pins (NavigationChainBuilderTests + IrLoader EventMacros); Pipeline.Tests 865/865; coordinated HHC golden regen -> NavigationGraph gains 37 ChainEdges + 3 UnresolvedChainTargets only; CanonicalGoldenRegression + ManifestSchemaConformance 6/6 (TRX).", []),
        ("session-2/docs", "interpretation-guide §5.7 chain-surface section + summary blocks; deferred-work DW-10 S2 progress note.", []),
    ]
    for idx, (key, desc, calls) in enumerate(steps, start=1):
        log.log_step(session_number=2, step_number=idx, step_key=key,
                     description=desc, status="complete", api_calls=calls)
    # verification rounds
    log.log_step(session_number=2, step_number=8, step_key="session-2/verification",
                 description="Cross-provider session-verification (gpt-5-4), 4 rounds: R1 3 Major + 2 Minor (all fixed) -> R2 2 follow-ons (fixed) -> R3 2 narrower variants (structural token-aware-scanner + strict-path rewrite) -> R4 VERIFIED. Golden byte-identical throughout.",
                 status="complete",
                 api_calls=[
                     {"model": "gpt-5-4", "taskType": "session-verification", "round": 1, "costUsd": 0.2122},
                     {"model": "gpt-5-4", "taskType": "session-verification", "round": 2, "costUsd": 0.1513},
                     {"model": "gpt-5-4", "taskType": "session-verification", "round": 3, "costUsd": 0.1379},
                     {"model": "gpt-5-4", "taskType": "session-verification", "round": 4, "costUsd": 0.1121},
                 ])
    print("Logged 8 activity steps for session 2.")


if __name__ == "__main__":
    route_next_orchestrator()
    log_steps()
