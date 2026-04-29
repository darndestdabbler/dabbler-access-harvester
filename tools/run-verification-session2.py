"""Session 2 cross-provider verification script."""
import importlib.util, sys, os, pathlib

def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        'ai_router', 'ai-router/__init__.py',
        submodule_search_locations=['ai-router'])
    mod = importlib.util.module_from_spec(spec)
    sys.modules['ai_router'] = mod
    spec.loader.exec_module(mod)
    return mod

ar = load_ai_router()
from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = 'docs/session-sets/vba-symbol-db'
log = SessionLog(SESSION_SET)

def read(path):
    try:
        return pathlib.Path(path).read_text(encoding='utf-8')
    except Exception as e:
        return f"<could not read: {e}>"

ref_row = read('AccessHarvester.SymbolDb/Models/ReferenceRow.cs')
ma_row = read('AccessHarvester.SymbolDb/Models/MemberAccessRow.cs')
fqn_resolver = read('AccessHarvester.SymbolDb/Extraction/FqnResolver.cs')
refs_extractor = read('AccessHarvester.SymbolDb/Extraction/ReferencesExtractor.cs')
build_result = read('AccessHarvester.SymbolDb/Extraction/BuildResult.cs')
tests = read('tests/AccessHarvester.SymbolDb.Tests/ReferencesExtractorTests.cs')

pass5_snippet = """// [SymbolDbBuilder.cs - Pass 5 addition only]
// Pass 5: extract reference edges and member-access expressions.
var resolvedModules = resolved.Select(r => r.Resolved).ToList();
var (referenceRows, memberAccessRows) = ReferencesExtractor.Extract(resolvedModules);
var statistics = ComputeStatistics(moduleRows, symbolRows, deferredFqns);
return new BuildResult(moduleRows, symbolRows, parameterRows, referenceRows, memberAccessRows, statistics);"""

writer_snippet = """// [SymbolDbWriter.cs - updated Write + new private methods]
public void Write(
    IReadOnlyList<ModuleRow> modules,
    IReadOnlyList<SymbolRow> symbols,
    IReadOnlyList<ParameterRow> parameters,
    IReadOnlyList<ReferenceRow>? references = null,
    IReadOnlyList<MemberAccessRow>? memberAccesses = null)
{
    using var transaction = _connection.BeginTransaction();
    WriteModules(modules, transaction);
    WriteSymbols(symbols, transaction);
    WriteParameters(parameters, transaction);
    if (references is not null) WriteReferences(references, transaction);
    if (memberAccesses is not null) WriteMemberAccesses(memberAccesses, transaction);
    transaction.Commit();
}
// WriteReferences and WriteMemberAccesses: standard parameterized INSERT loops
// matching the existing WriteModules/WriteSymbols/WriteParameters pattern."""

prompt = (
    "Your response MUST begin with a detailed prose analysis for each concern below BEFORE the JSON block.\n\n"
    "## Session 2 of 5 - vba-symbol-db: References + MemberAccesses + Caller-Kind Annotation\n\n"
    "Cross-provider verifier for a C# .NET 10 project. Session 2 persists VBA reference edges "
    "and member-access expressions into a SQLite symbol database.\n\n"
    "### Spec requirements:\n"
    "1. `[References]` writer walks `ResolvedModule.References`: from_fqn from enclosing scope, "
    "to_fqn from bound declaration, resolution status mapping, caller_kind annotation.\n"
    "2. `MemberAccesses` writer for MemberAccessExpression/BangAccessExpression/"
    "ImplicitWithMemberExpression/ImplicitWithBangExpression.\n"
    "3. caller_kind: 'ui-event-handler' if Form/Report module + '_' in proc name; 'non-ui' otherwise.\n"
    "4. Tests: probe target (cboCompanyTypeID_BeforeUpdate) refs include CompanyIsActive, GetString, "
    "Me, strDlgMsg; caller_kind='ui-event-handler'; CompanyIsActive has a caller.\n"
    "5. dotnet test: 1575 passed / 3 skipped / 0 failed\n\n"
    "### Files NOT in scope (unchanged from Session 1):\n"
    "SchemaMigrator.cs (already has [References] and MemberAccesses DDL), "
    "PersistenceAndDedupTests.cs, NorthwindFixtureSmokeTests.cs.\n\n"
    "### New/Modified files:\n\n"
    "#### ReferenceRow.cs (new)\n```csharp\n" + ref_row + "\n```\n\n"
    "#### MemberAccessRow.cs (new)\n```csharp\n" + ma_row + "\n```\n\n"
    "#### FqnResolver.cs (new)\n```csharp\n" + fqn_resolver + "\n```\n\n"
    "#### ReferencesExtractor.cs (new)\n```csharp\n" + refs_extractor + "\n```\n\n"
    "#### BuildResult.cs (modified)\n```csharp\n" + build_result + "\n```\n\n"
    "#### SymbolDbBuilder.cs Pass 5 addition\n```csharp\n" + pass5_snippet + "\n```\n\n"
    "#### SymbolDbWriter.cs updated\n```csharp\n" + writer_snippet + "\n```\n\n"
    "#### ReferencesExtractorTests.cs (new)\n```csharp\n" + tests + "\n```\n\n"
    "### Build/test results: dotnet build: 0 errors. dotnet test: 1575 passed / 3 skipped / 0 failed\n\n"
    "### Concerns to analyze:\n"
    "1. FqnResolver correctness: scope chain walk, PropertyDeclaration #get/#let/#set suffix.\n"
    "2. ClassifyKind dispatch: VariableDeclaration -> 'variable_read' (write not distinguishable).\n"
    "3. caller_kind heuristic vs spec's 'join EventBindings' requirement; acceptable for Session 2?\n"
    "4. ImplicitWithMemberExpression with non-ResolvedViaWithFlattening outcome (unresolved fallback).\n"
    "5. to_fqn=null for ResolvedToObjectModel: correct since synthetics not in Symbols table.\n"
    "6. FK constraint: from_fqn matches Symbols.fqn (enclosing procedure FQN, not local's FQN).\n"
    "7. Test coverage gaps: enum values, built-in references, bang-access kind rows.\n"
    "8. Backward-compat of optional Write() params: existing tests pass null-equivalent.\n\n"
    "After your analysis, output a JSON block in this exact format:\n"
    "```json\n"
    '{\n'
    '  "verdict": "VERIFIED" | "ISSUES_FOUND",\n'
    '  "issues": [\n'
    '    {\n'
    '      "severity": "Critical" | "Major" | "Minor",\n'
    '      "location": "<file or test name>",\n'
    '      "summary": "<one-sentence description>",\n'
    '      "fix": "<recommended fix>"\n'
    '    }\n'
    '  ]\n'
    '}\n'
    "```\n"
    "If verdict is VERIFIED, `issues` must be an empty array `[]`.\n"
    "Do not omit this block.\n"
)

print(f"Verification prompt length: {len(prompt)} chars")

pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/').mkdir(parents=True, exist_ok=True)
pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/session-002-prompt.txt').write_text(prompt, encoding='utf-8')
print("Prompt saved.")

result = route(
    content=prompt,
    task_type='session-verification',
    complexity_hint=70,
    session_set=SESSION_SET,
    session_number=2,
)

review_path = 'docs/session-sets/vba-symbol-db/session-reviews/session-002.md'
pathlib.Path(review_path).write_text(result.content, encoding='utf-8')
print(f"Review saved to {review_path}")
print(f"Model: {result.model_name}, tokens: in={result.input_tokens} out={result.output_tokens}, cost=${result.cost_usd:.4f}")

log.log_step(2, 7, 'session-2/verification-round-1', f'Cross-provider session-verification round 1 via {result.model_name}', 'complete', [{
    'model': result.model_name,
    'taskType': 'session-verification',
    'inputTokens': result.input_tokens,
    'outputTokens': result.output_tokens,
    'costUsd': result.cost_usd,
}])

print("Logged.")
