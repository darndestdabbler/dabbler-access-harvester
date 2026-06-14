"""Session 3 (011-resolver-and-navigation-wiring) activity-log appender.

Mirrors tools/vahs011_s2_log.py. Run with the workspace venv interpreter.
Each invocation passes the steps it knows about; SessionLog.log_step appends.
"""
import sys
from ai_router.session_log import SessionLog

log = SessionLog("docs/session-sets/011-resolver-and-navigation-wiring", total_sessions=3)


def call(model, tt, i, o, c):
    return {"model": model, "taskType": tt, "inputTokens": i, "outputTokens": o, "costUsd": c}


# Steps completed so far this session. Re-running is harmless (append-only ledger);
# pass only the batch you want to add via the BATCH argv selector.
BATCHES = {
    "a": [
        (1, "session-003/reground-fresh", "Grounded delivered surfaces against current output (committed golden = S2's COM regen, no intervening behavioral change => equals a fresh run). Confirmed NavigationGraph 22 edges (13 OpenForm/8 Close/1 OpenReport) + 22 UnresolvedTargets (all non-literal-target); EntryPoints/Macros empty, Switchboard/Ribbon omitted (DW-9); enrichment arrays ValidationRules 76, RowSourceSpecs 19, FunctionClassifications 233, EventHandlerClassifications 81; UnresolvedItems 1414 byte-identical to side-file. R2-1/R2-2 confirmed NOT present (deferred to P4, as expected).", []),
        (2, "session-003/navgraph-documented", "Documented the navigation-graph consumer surface in interpretation-guide: new section 5.7 (edges/unresolved-targets shape + join keys + omit-null-vs-empty-array split + R2-1/R2-2 deferral + determinism); added NavigationGraph to the section-4 envelope key list + empty-run example; added a section-8 known-limitation; fixed stale SchemaVersion 1.0->1.1 in the envelope narrative + example; added a reading-order navigation step in 3.2.", []),
        (3, "session-003/residual-gap-triage", "Residual enrichment-gap triage: no new gap surfaced. All known gaps are already DW-tracked - DW-8 (DynamicSqlAnalyzer/SqlStrings unwired), DW-9 (switchboard/ribbon/macro NavigationGraphInputs unwired), DW-10 (R2-2 chain derivation). Close/Me.name self-close non-literal targets are within DW-10's derivation space, not a new gap.", []),
        (4, "session-003/ai-assignment", "Appended Session 3 block to ai-assignment.md (recommended Sonnet per S2 routed analysis; actual Opus 4.8 - human's orchestrator choice; verification via in-IDE devil's-advocate Gemini+GPT pair, cross-provider preserved).", []),
    ],
    "b": [
        (5, "session-003/author-next-set", "Authored P4 next set 012-switchboard-and-report-chain-edges (spec.md + not-started v4 state stub): R2-1 switchboard row materialization (shape-gated detection + narrow OleDb row-read + NavigationGraphInputs.Switchboard/Macros/EntryPoints population) over 3 sessions, R2-2 chain-edge derivation; Hard Constraints carried verbatim (finding refs updated to R2-1/R2-2) + set-specific narrow-row-read constraint. Updated DW-9/DW-10 owning-set to the concrete 012 slug.", []),
        (6, "session-003/final-validation", "Final validation: no C# source or golden changed (S3 is docs/artifacts-only). dotnet build green (0 errors). COM grounding gates green via fresh extraction (TRX-confirmed 6/6: CanonicalGoldenRegressionTests proves committed golden == fresh run byte-for-byte; ManifestSchemaConformanceTests proves manifest schema-valid). No golden regen required (spec conditional: only if step 2/3 changed output). samples/*.accdb timestamp churn swept.", []),
    ],
    "c": [
        (7, "session-003/devils-advocate-verification", "Closeout-grade cross-provider verification via in-IDE devil's-advocate pair (GitHub Copilot -> Gemini Pro + GPT-5.4), two prompts (S3 closeout + set-wide S1/S2 addendum) x two providers = 4 raw reviews saved verbatim. Round verdict ISSUES_FOUND. Confirmed defects: C1 (Major, GPT) §5.7/§4 join-key doc wrong (Forms/Reports are {Spec,...}, name=Spec.SourceFormName not Name); C9 (Major GPT / Critical Gemini) unresolved-items.json procedure-body-scoped undercount (GPT-proven reproducer). Latent: C7b (Gemini Major / GPT not-triggerable) Resolved-ref-to-VBA_RESOLVE_FAILURE-stub. Disproved by both: C7a, C8. Synthesis in s3-verification-synthesis.md; structured findings in s3-issues.json.",
            [{"model": "gemini-pro (in-IDE Copilot, subscription)", "taskType": "session-verification", "costUsd": 0.0},
             {"model": "gpt-5-4 (in-IDE Copilot, subscription)", "taskType": "session-verification", "costUsd": 0.0}]),
        (8, "session-003/finding-dispositions", "Step 7 dispositions: C1 FIXED this session (interp-guide §5.7 join sentence -> Spec.SourceFormName + no-top-level-Name warning; §4 stub bullet rewritten per-category accurately). C7b FIXED via doc (interp-guide §4 VBA_RESOLVE_FAILURE bullet: null-check ParserOutput regardless of Resolved status) + latent code asymmetry DEFERRED -> DW-12. C9 DEFERRED -> DW-11 (operator-adjudicated: design decision re double-emit + golden-neutral => focused session) + documented scoping caveat in §6 (inline ResolutionReason is authoritative complete signal). DW-11/DW-12 added; 012 non-goals updated. No code/golden change from any disposition.", []),
    ],
    "d": [
        (9, "session-003/reverification-round-2", "R2 re-verify of the closeout fixes (in-IDE Gemini + GPT-5.4). Gemini VERIFIED; GPT-5.4 ISSUES_FOUND with 2 new Major findings, both about the accuracy of the R1 doc fixes (proven by probe, orchestrator-confirmed): R2-1 §4 said Forms={Spec}/Reports={Spec,ParserOutput} but ParserOutput is optional on BOTH (golden 18/19 forms + 10/12 reports carry it) -> FIXED. R2-2 §6 overclaimed completeness: module-level unresolveds are dropped at projection (ParserOutputProjector.cs:66, no module-level item type) so they're in NEITHER Body.References[] NOR the index -> FIXED (§6 split into Layer A header / Layer B module-level; DW-11 broadened to both layers + a schema-home note + both reproducers). No code defect; R1 dispositions held. Both R2 fixes ground-truthed. s3-issues-round-2.json + synthesis updated.",
            [{"model": "gemini-pro (in-IDE Copilot, subscription)", "taskType": "session-verification", "costUsd": 0.0},
             {"model": "gpt-5-4 (in-IDE Copilot, subscription)", "taskType": "session-verification", "costUsd": 0.0}]),
    ],
}


def main():
    batch = sys.argv[1] if len(sys.argv) > 1 else "a"
    steps = BATCHES[batch]
    for n, key, desc, calls in steps:
        log.log_step(3, n, key, desc, "complete", calls)
    print(f"logged {len(steps)} session-3 step(s) (batch {batch})")


if __name__ == "__main__":
    main()
