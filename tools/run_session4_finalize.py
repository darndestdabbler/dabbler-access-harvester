"""Session 4 finalization: change-log, next-set recommendation, cost report, mark complete."""
import importlib.util, sys, pathlib

def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        'ai_router', 'ai-router/__init__.py',
        submodule_search_locations=['ai-router'])
    mod = importlib.util.module_from_spec(spec)
    sys.modules['ai_router'] = mod
    spec.loader.exec_module(mod)
    return mod

ar = load_ai_router()
route = ar.route
from ai_router.session_log import find_active_session_set, SessionLog
from ai_router import print_cost_report, print_session_set_status, mark_session_complete

SESSION_SET = find_active_session_set('docs/session-sets')
log = SessionLog(SESSION_SET)

# ── Step A: Change log ─────────────────────────────────────────────────
change_log_prompt = """Generate a change log for the parser-infrastructure session set.

## Session set: parser-infrastructure
## Plan phase: Phase 2.1-2.5 of the VBA Parser Migration Plan

### Session 1: Error-Recovery Strategy + Parser Pipeline
- Authored docs/session-sets/parser-infrastructure/error-recovery-strategy.md (in-conversation synthesis by Opus orchestrator)
- Created AccessHarvester.Vba.Parser/ project with ParseMode, ParseOptions, Diagnostic, ParseResult
- DiagnosticCollectingErrorListener, ThrowingErrorListener
- StatementBoundaryErrorStrategy: syncs on NEWLINE/COLON + procedure-header tokens (Sub/Function/Property/End/Private/Public/Friend/Static)
- VbaParserPipeline.Parse() — main entry point returning ParseResult with tree + diagnostics
- Added to AccessHarvester.sln. 8 new parser unit tests.

### Session 2: AST Shape
- Authored docs/session-sets/parser-infrastructure/ast-shape.md (in-conversation synthesis by Opus orchestrator)
- Created AccessHarvester.Vba.Parser/Ast/ with:
  - Provenance (SourceProvenance, RecoveredProvenance)
  - VbaSyntaxNode base record
  - Module, ModuleItem hierarchy (ProcedureDeclaration, PreprocessorDirective, UnknownModuleItem)
  - Sub/Function/Property procedure variants
  - Parameter, TypeReference, Argument
  - Statement: 22 variants + UnknownStatement (IfStatement, ForStatement, ForEachStatement, DoLoopStatement, WhileStatement, WithStatement, SelectStatement, AssignmentStatement, SetStatement, CallStatement, ExitStatement, ReturnStatement, OnErrorStatement, ResumeStatement, GoToStatement, GoSubStatement, ReturnGoSubStatement, ExitStatement variants, RaiseErrorStatement, DeclareStatement, VariableDeclarationStatement, ConstDeclarationStatement, CommentStatement, LabelStatement, StopStatement, EndStatement, UnknownStatement)
  - Expression: ~25 variants + UnknownExpression
  - VbaSyntaxWalker, VbaSyntaxVisitor<T>, AstChildren
  - AstBuilder (parse-tree -> AST) + ParseResultAstExtensions
- 35 new AST tests, total 44 parser tests.

### Session 3: Position Tracking + Preprocessor Pass-Through
- Created SourceLocation.cs (File, Line, Column triple)
- Created SourceRange.cs (Start + End SourceLocation, half-open range)
- Added SourceRange? Range to Provenance (additive, defaults null)
- Updated AstBuilder.MakeProvenance to populate Range from ANTLR token start/stop
- Added PreprocessorStatement:Statement for in-body preprocessor lines (#If/#Else/#End If/#Const)
- Updated VbaSyntaxWalker/Visitor switch + virtual methods + AstChildren
- Authored docs/test-cases/13-preprocessor.bas + 13-preprocessor.expected.json (preprocessor fixture pair)
- 8 new tests (walker/visitor dispatch + preprocessor AST construction), total 52 parser tests.

### Session 4: Cross-Cut + Mechanical Tightening (Final)
- Ran full corpus: 676 tests pass (54 parser, 294 grammar, 328 core), 0 failed.
- Ran schema validation: fixture 13 failed on multiple violations.
- Schema fix: added "preprocessorStatement" to statement.kind enum in harvester-schema.v0.json.
- Fixture fix: corrected 13-preprocessor.expected.json — statement kinds, classification values, resolvedCallee fields, sqlStrings required fields.
- All 13 fixtures now pass schema validation.

## Outputs produced
- AccessHarvester.Vba.Parser/ (project with parser pipeline, error recovery)
- AccessHarvester.Vba.Parser/Ast/ (full AST type hierarchy + visitor + walker + builder)
- docs/test-cases/13-preprocessor.bas + 13-preprocessor.expected.json (preprocessor fixture)
- docs/session-sets/parser-infrastructure/error-recovery-strategy.md
- docs/session-sets/parser-infrastructure/ast-shape.md
- harvester-schema.v0.json updated (preprocessorStatement kind added)

## Verification verdicts
- Session 1: VERIFIED (round 2 after 2 Major + 2 Minor fixes; 1 deferred finding sided with orchestrator by tiebreaker)
- Session 2: VERIFIED (round 2 after 1 Major + 1 Minor fix; gpt-5-4 fell back to gemini-pro per escalation ladder)
- Session 3: VERIFIED (round 3 after 2 Major + 1 Minor fixes)
- Session 4: VERIFIED (round 1)

Write a concise change log in markdown. Focus on what was built and why it matters for downstream consumers.
"""

print("Generating change log...")
cl_result = route(
    content=change_log_prompt,
    task_type='summarization',
    max_tier=1,
    session_set=str(SESSION_SET),
    session_number=4,
)

cl_path = pathlib.Path('docs/session-sets/parser-infrastructure/change-log.md')
cl_path.write_text(cl_result.content, encoding='utf-8')
print(f"Change log written ({cl_result.output_tokens} tokens, ${cl_result.cost_usd:.4f})")
log.log_step(4, 4, 'session-4/change-log-generation',
    f'Generated change-log.md via summarization (model={cl_result.model_name}, cost=${cl_result.cost_usd:.4f})',
    'complete', [{
        'model': cl_result.model_name,
        'taskType': 'summarization',
        'inputTokens': cl_result.input_tokens,
        'outputTokens': cl_result.output_tokens,
        'costUsd': cl_result.cost_usd
    }])

# ── Step B: Next-session-set recommendation ───────────────────────────
tree_row_6 = "parser-infrastructure (#6): Phase 2.1-2.5 complete. Unblocks: #7 resolver-declarations-and-scopes (Phase 3.1-3.2) and #12 jet-sql-grammar-and-parser (Phase 5.1-5.2). Both have #6 as their only unblocked prereq (assuming #5 is already complete, which it is)."

next_set_prompt = """Given the completion of parser-infrastructure (#6), recommend the next session set(s) to execute.

## Completed set: parser-infrastructure (#6)
Delivered: AccessHarvester.Vba.Parser/ (pipeline, error recovery, AST, position tracking, preprocessor pass-through).
Synthesis sessions: 2 (error recovery + AST shape — both Opus-class work requiring in-conversation synthesis).
Verification rounds: 1+2+3+1 across 4 sessions. Cost-efficient sessions 3-4 (Sonnet @ low effort).

## Sets now unblocked by #6 completing

### #7: resolver-declarations-and-scopes
- Plan phase: 3.1-3.2 (declaration model + scope tree)
- Synthesis count: 2 (declaration model, scope tree visitor) — Opus-class sessions expected
- Outputs: AccessHarvester.Vba.Resolver/Declarations/, AccessHarvester.Vba.Resolver/Scopes/
- Sessions: 3-4

### #12: jet-sql-grammar-and-parser
- Plan phase: 5.1-5.2 (JET SQL grammar + parser infrastructure)
- Synthesis count: 2 (JET SQL grammar, error recovery + AST for SQL) — Opus-class sessions expected
- Outputs: AccessHarvester.Sql.Grammar/, AccessHarvester.Sql.Parser/
- Sessions: 4-5

Both #7 and #12 have independent prereq chains (no shared prereqs beyond #6) so they CAN run in parallel if worktrees are used.

## This session's actuals (for calibration)
- Session 4 (final, tightening): Sonnet 4.6 @ effort=unknown, total routed cost ~$0.08
- Synthesis sessions (1-2) needed Opus orchestrator
- Mechanical sessions (3-4) worked well with Sonnet

Recommend the next session set(s) and the best orchestrator for each. If both #7 and #12 are viable starts, note that.
Format per the workflow schema:
**Next session-set recommendation (after parser-infrastructure):**
...
"""

print("Generating next-session-set recommendation...")
nss_result = route(
    content=next_set_prompt,
    task_type='analysis',
    complexity_hint=40,
    session_set=str(SESSION_SET),
    session_number=4,
)

nss_path = pathlib.Path('docs/session-sets/parser-infrastructure/next-set-recommendation.md')
nss_path.write_text(nss_result.content, encoding='utf-8')
print(f"Next-set recommendation written (${nss_result.cost_usd:.4f})")
log.log_step(4, 5, 'session-4/next-session-set-recommendation',
    f'Routed analysis for next-session-set recommendation (model={nss_result.model_name}, cost=${nss_result.cost_usd:.4f})',
    'complete', [{
        'model': nss_result.model_name,
        'taskType': 'analysis',
        'inputTokens': nss_result.input_tokens,
        'outputTokens': nss_result.output_tokens,
        'costUsd': nss_result.cost_usd
    }])

print()
print("=== NEXT-SET RECOMMENDATION ===")
try:
    print(nss_result.content)
except UnicodeEncodeError:
    print("[saved to next-set-recommendation.md]")

# ── Cost report + status ──────────────────────────────────────────────
print()
print("=== COST REPORT ===")
print_cost_report(SESSION_SET)
print()
print("=== SESSION SET STATUS ===")
print_session_set_status()

# ── Mark session complete ─────────────────────────────────────────────
mark_session_complete(
    session_set=SESSION_SET,
    verification_verdict="VERIFIED",
)
print("session-state.json marked complete.")
