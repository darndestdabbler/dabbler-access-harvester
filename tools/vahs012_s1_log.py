from ai_router.session_log import SessionLog
log = SessionLog("docs/session-sets/012-switchboard-and-report-chain-edges", total_sessions=3)


def call(model, tt, i, o, c):
    return {"model": model, "taskType": tt, "inputTokens": i, "outputTokens": o, "costUsd": c}


steps = [
 (1, "session-001/re-ground", "Re-grounded R2-1 against the tree: confirmed Build(inputs:null) call site, NavigationGraphInputs/Switchboard shapes; OLE DB probe of the 52 tblSwitchboardItems rows DISPROVED the canonical-Switchboard-Manager premise (custom ParentID tree, command codes 0-3, 32 embedded audit rows).", []),
 (2, "session-001/ai-assignment", "Routed per-session orchestrator recommendation for ai-assignment.md.",
    [call("gemini-pro", "analysis", 762, 168, 0.0026)]),
 (3, "session-001/human-decision", "Surfaced the spec-vs-fixture conflict; human (former Access dev) chose generic faithful-mechanical design + StartUpForm read, and directed it must document BOTH custom and generic switchboards.", []),
 (4, "session-001/detector-and-model", "Pure SwitchboardSignatureDetector (column-signature); extended NavigationGraph model (SwitchboardItem.Provenance, SwitchboardItemProvenance, SwitchboardStructure.UnlinkedItemCount + page Table); additive on additionalProperties-tolerant schema (no envelope bump).", []),
 (5, "session-001/producers", "Pure generic SwitchboardStructureBuilder (page key = ParentID-if-present else SwitchboardID; raw Command preserved, no decode; null-parent rows counted not dropped) + MacroChainBuilder (ir/Macros -> MacroChain[]).", []),
 (6, "session-001/exporter-and-wiring", "DAO row-read SwitchboardExporter (Category Navigation; EscapeIdentifier discipline; StartUpForm membership-probe) + JsonPassthroughTranslator.Navigation; wired NavigationGraphInputs into NavigationGraphBuilder.Build (retired inputs:null).", []),
 (7, "session-001/hermetic-tests", "Hermetic pins: detector 7/7; SwitchboardStructureBuilder (canonical + custom + null-parent + multi-table + null-preservation) + MacroChainBuilder; full Pipeline.Tests 846/846 green; ribbon decision recorded (null on HHC, no producer, deferred).", []),
 (8, "session-001/golden-and-docs", "Coordinated HHC golden regen (COM, TRX): NavigationGraph now populated (EntryPoints DefaultForm frmSwitchboard + 2 roots; Switchboard 2 pages/10 items, UnlinkedItemCount=32; 4 Macros) + new kb/ir Navigation files; every delta maps to R2-1. CanonicalGoldenRegression + ManifestSchemaConformance 6/6. Interpretation-guide 5.7 updated.", []),
 (9, "session-001/verification-r1", "Cross-provider session-verification (gpt-5-4): ISSUES_FOUND R1 (2 Major: null->0 coercion, multi-table page identity; 1 Minor: StartUpForm silent failure).",
    [call("gpt-5-4", "session-verification", 11766, 10030, 0.1799)]),
 (10, "session-001/verification-r2", "Fixed all three (nullable Command/ids; page Table; StartUpForm membership-probe + failed outcome) + 2 regression pins; golden re-regen (page Table added); gate 6/6. R2 (gpt-5-4): both Majors + Minor VERIFIED; 1 new Minor (NULL ItemText->'' caption) accepted-by-design (no value collision; raw dump preserves it).",
    [call("gpt-5-4", "session-verification", 6411, 8041, 0.1366)]),
]
for n, key, desc, calls in steps:
    log.log_step(1, n, key, desc, "complete", calls)
print("logged", len(steps), "session-1 steps")
