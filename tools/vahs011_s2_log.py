from ai_router.session_log import SessionLog
log = SessionLog("docs/session-sets/011-resolver-and-navigation-wiring", total_sessions=3)
def call(model, tt, i, o, c):
    return {"model": model, "taskType": tt, "inputTokens": i, "outputTokens": o, "costUsd": c}
steps = [
 (1, "session-002/re-ground-f-2-2", "Re-grounded F-2-2 call sites: substrate has zero production call sites; switchboard rows NOT harvested (tblSwitchboardItems '(no data file)').", []),
 (2, "session-002/scope-consensus", "Scope decision (R2-1 premise disproven): routed cross-provider consensus -> both Option A (core wiring; defer R2-1/R2-2 to P4); human approved.",
    [call("gpt-5-4","analysis",1143,1696,0.0283), call("gemini-pro","analysis",1211,228,0.0038)]),
 (3, "session-002/navgraph-unresolved-wired", "Wired NavigationGraphBuilder -> manifest.NavigationGraph and UnresolvedItemAggregator -> unresolved-items.json off the resolvedModules+crossRefs seam.", []),
 (4, "session-002/enrichment-wired", "Wired EnrichmentPipelineRunner per form/report + ExtractFromTable + FunctionClassifications pass-through into manifest top-level arrays + per-reference Classification slots.", []),
 (5, "session-002/f-3-4-kind-omit-null-fix", "Fixed pre-existing S1 F-3-4 bug surfaced by full schema gate: ParserOutputProjector omits ResolvedTarget.Kind when null (was schema-invalid '\"Kind\": null', 325x).", []),
 (6, "session-002/docs-and-pins", "Removed 010 interim unresolved-items note (interp guide 6); added DW-9/DW-10; added 7 hermetic production-wiring pins; reconciled EndToEndFixtureTests note.", []),
 (7, "session-002/build-test-golden", "Build + hermetic suites green; HHC golden regen (COM, TRX); full Integration COM 58 pass/3 skip/0 fail incl. golden + schema gates; every golden delta maps to F-2-2 or the F-3-4 fix.", []),
 (8, "session-002/cross-provider-verification", "Cross-provider session verification routed to gpt-5-4: VERIFIED R1, empty issues; non-blocking RowSourceSpecs-pin note closed post-verdict.",
    [call("gpt-5-4","session-verification",7543,6799,0.1208)]),
 (9, "session-002/s3-orchestrator-recommendation", "Routed S3 orchestrator recommendation (scope adjusted: R2-1/R2-2 deferred -> S3 validates delivered surfaces + docs + authors P4 set).",
    [call("gemini-pro","analysis",520,90,0.0014)]),
]
for n, key, desc, calls in steps:
    log.log_step(2, n, key, desc, "complete", calls)
print("logged", len(steps), "session-2 steps")
