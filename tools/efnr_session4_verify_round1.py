"""Session 4 verification — round 1 cross-provider via gemini-pro."""
import importlib.util
import sys


def load():
    spec = importlib.util.spec_from_file_location(
        'ai_router', 'ai-router/__init__.py',
        submodule_search_locations=['ai-router'])
    mod = importlib.util.module_from_spec(spec)
    sys.modules['ai_router'] = mod
    spec.loader.exec_module(mod)
    return mod


ar = load()
from ai_router import query
from ai_router.session_log import SessionLog

log = SessionLog('docs/session-sets/enrichment-functions-navigation-review')

with open('tests/AccessHarvester.Pipeline.Tests/EndToEndFixtureTests.cs', 'r', encoding='utf-8') as f:
    test_code = f.read()
with open('docs/test-cases/harvester-schema.v0.json', 'r', encoding='utf-8') as f:
    schema = f.read()
with open('docs/session-sets/enrichment-functions-navigation-review/spec.md', 'r', encoding='utf-8') as f:
    spec_md = f.read()
with open('docs/session-sets/enrichment-functions-navigation-review/issue-logs/session-004.json', 'r', encoding='utf-8') as f:
    issue_log = f.read()

out_of_scope_note = """=== OUT OF SCOPE for this session (referenced but unchanged) ===
The following files are referenced by the new EndToEndFixtureTests.cs but were
authored in earlier sessions and are NOT being changed in Session 4:

- AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs (Session 1)
- AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs (Session 1; exposes BuildAll convenience method used by RunFixture)
- AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs (Session 2)
- AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs (Session 2)
- AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs (Session 3)
- AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs (Session 3)
- AccessHarvester.Vba.Parser, AccessHarvester.Vba.Resolver: pre-existing infra
- docs/schema/v0-vs-tiers.md: schema-delta doc (parser-foundations Session 2) that
  pins v0-G5 (procedureKind missing Declare; reserves EventHandler) as a known
  gap deferred to schema-finalization-and-docs (#17)
- docs/test-cases/{07,08,10,11,12}-*.{bas,cls}: anchor fixtures unchanged

Files inside docs/test-cases (the .bas / .cls fixtures) are read at test time
via LocateTestCasesRoot() walking up from AppContext.BaseDirectory; the test
project does NOT copy them into bin/, it walks the worktree.

This session set runs in a per-set git worktree; the parallel sibling
enrichment-events-and-validation set introduced an EnrichmentPipelineRunner /
EnrichmentResult on main that this branch has not yet merged back (the merge
happens at Step 8 close-out per the worktree convention). Session 4's E2E
test does NOT use the sibling's runner — it builds its own end-to-end fixture
chain to avoid cross-session-set merge conflicts during execution.
"""

prompt = (
    "You are performing the mandatory cross-provider end-of-session verification for "
    "Session 4 of the enrichment-functions-navigation-review session set.\n\n"
    "CONTEXT\n"
    "- This is the LAST regular session of a 4-5 session set (Session 5 is a buffer reserved for cross-provider verifier findings).\n"
    "- Session 4's spec line items (from spec.md):\n"
    "  1. Run full pipeline (#1 through this set) against `07`, `08`, `10`, `11`, `12`. Confirm classification + navigation + unresolved aggregation in run output.\n"
    "  2. Route `schema-validation` for output vs. `harvester-schema.v0.json`.\n"
    "  3. Land tightening commits.\n"
    "- Sessions 1-3 of this set produced FunctionClassification (Phase 6.6), NavigationGraph (Phase 6.7), and UnresolvedItem + UnresolvedItemAggregator (Phase 6.8).\n\n"
    "WHAT SESSION 4 ACTUALLY DID\n"
    "1. Authored tests/AccessHarvester.Pipeline.Tests/EndToEndFixtureTests.cs:\n"
    "   - Drives parse -> scope -> resolve -> CrossReferenceIndex -> FunctionClassificationBuilder.BuildAll -> NavigationGraphBuilder.Build -> UnresolvedItemAggregator.Build for each of the 5 anchor fixtures (07/08/10/11/12).\n"
    "   - Per-fixture facts asserting key behaviours (Fixture07 mixed classification, Fixture08 unresolved navigation, Fixture10 PropertyGet/Let kinds, Fixture11 >=5 OpenForm edges, Fixture12 specific Click handler edges).\n"
    "   - Cross-fixture envelope theory tests asserting harvester-schema.v0.json envelope shape (schemaVersion, source, items[], unresolvedItems[]) and determinism across repeated runs.\n"
    "   - MapSchemaReason / MapPurity / MapTranslatability / MapProcedureKind helpers translating internal Pipeline shapes into the schema's closed enums.\n"
    "2. Routed a schema-validation analysis to Sonnet (query, $0.1384, 5202 tokens) which surfaced 1 Critical (Declare->schema gap), 1 Major (0-based ANTLR Column emitting startColumn:0), 1 Minor (test assertion masked Critical).\n"
    "3. Applied all 3 fixes:\n"
    "   - MapProcedureKind helper: ProcedureKind.Declare -> \"Function\" placeholder, documented v0-G5 (deferred to #17).\n"
    "   - Math.Max(1, ...) coercion on startLine/startColumn so 0-based ANTLR positions emit as 1.\n"
    "   - SchemaProcedureKinds (closed schema enum) replaces the Pipeline-enum assertion set; new AnchorFixtures_DoNotProduceDeclareProcedureKind theory guards the fallback.\n"
    "4. Build green; full test suite: 1371 passed, 4 skipped (Windows-only Access COM), 0 failed. Pipeline.Tests grew from 192 -> 212 (+20: 15 fixture tests + 5 Declare guards).\n\n"
    "YOUR TASK\n"
    "Independently verify Session 4's deliverables. Specifically check:\n"
    "- The end-to-end test correctly exercises the full chain per the spec (parse -> resolve -> enrichment -> aggregation -> run output assertions).\n"
    "- The 5 anchor fixtures all surface their expected facets (mixed classification, unresolved navigation targets, property kinds, navigation edges).\n"
    "- The MapSchemaReason switch covers all aggregator reason codes deterministically.\n"
    "- The 3 schema-validation fixes are correct in shape and posture (especially the v0-G5 deferral - verify the comment clearly points readers at docs/schema/v0-vs-tiers.md and the AnchorFixtures_DoNotProduceDeclareProcedureKind guard).\n"
    "- The envelope output is genuinely deterministic (repeated runs produce byte-identical JSON).\n"
    "- Determinism: per-procedure ordering and per-unresolved-item sort order should be stable across runs (UnresolvedItemAggregator already sorts; FunctionClassificationBuilder.BuildAll preserves source order).\n\n"
    "Your response MUST begin with detailed prose analysis (one paragraph per concern above) BEFORE the JSON block; the JSON block alone is not sufficient.\n\n"
    "After the prose, output a JSON block in this exact format:\n"
    "```json\n"
    "{\n"
    '  "verdict": "VERIFIED" | "ISSUES_FOUND",\n'
    '  "issues": [\n'
    "    {\n"
    '      "severity": "Critical" | "Major" | "Minor",\n'
    '      "location": "<file or test name>",\n'
    '      "summary": "<one-sentence description>",\n'
    '      "fix": "<recommended fix>"\n'
    "    }\n"
    "  ]\n"
    "}\n"
    "```\n"
    'If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit this block.\n\n'
    f"{out_of_scope_note}\n\n"
    f"=== spec.md (this session set) ===\n{spec_md}\n\n"
    f"=== EndToEndFixtureTests.cs ===\n{test_code}\n\n"
    f"=== harvester-schema.v0.json ===\n{schema}\n\n"
    f"=== issue-logs/session-004.json (issues from schema-validation routed call, all Fixed) ===\n{issue_log}\n"
)

print(f"Prompt size: {len(prompt)} chars")

result = query(
    model="gemini-pro",
    content=prompt,
    task_type="session-verification",
    session_set="docs/session-sets/enrichment-functions-navigation-review",
    session_number=4,
)

log.save_session_review(session_number=4, review_text=result.content, round_number=1)
print(f"model={result.model_name} cost=${result.cost_usd:.4f} input={result.input_tokens} output={result.output_tokens}")
print("--- VERDICT BLOCK (last 1500 chars) ---")
print(result.content[-1500:])

log.log_step(
    4, 8, 'session-4/verify-round-1',
    f'Round 1 cross-provider verification (gemini-pro, ${result.cost_usd:.4f}, '
    f'{result.output_tokens} output tokens) per session-1+2+3 calibration '
    f'(gpt-5-4 still throttled). Saved to session-reviews/session-004.md.',
    'complete',
    [{'model': result.model_name, 'taskType': 'session-verification',
      'inputTokens': result.input_tokens, 'outputTokens': result.output_tokens,
      'costUsd': result.cost_usd}],
)
print('logged step 8')
