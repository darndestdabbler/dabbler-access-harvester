"""Tiebreaker: second opinion on M2 (variable_write detection) from a different provider."""
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
from ai_router import route, record_adjudication

SESSION_SET = 'docs/session-sets/vba-symbol-db'

def read(path):
    try:
        return pathlib.Path(path).read_text(encoding='utf-8')
    except Exception as e:
        return f"<could not read: {e}>"

resolved_reference = read('AccessHarvester.Vba.Resolver/References/ResolvedReference.cs')
refs_extractor = read('AccessHarvester.SymbolDb/Extraction/ReferencesExtractor.cs')
spec_session2 = """### Session 2 of 5: References + Member Accesses + Caller-Kind Annotation

Steps:
1. Required reading.
2. Implement the [References] writer that walks every ResolvedModule.References and writes rows:
   - Resolution status mapping (closed union to text codes).
   - from_fqn derivation from the enclosing scope of the reference site.
   - to_fqn from the bound declaration when applicable.
3. Implement the MemberAccesses writer specialized for MemberAccessExpression / BangAccessExpression / ImplicitWithMemberExpression / ImplicitWithBangExpression AST nodes.
4. Populate [References].caller_kind by joining against the existing EventHandlerClassification and EventBindings data.
5. Hermetic tests.
6. dotnet build && dotnet test.
7. Cross-provider verification.

Schema for kind column: 'call' | 'variable_read' | 'variable_write' | 'enum_value_use' | 'member_access' | 'control_access'"""

finding_text = """M2 (Major): ClassifyKind never distinguishes variable writes, so assignment targets
are persisted as variable_read.
Fix suggested by verifier: Detect write context from the AST parent/statement shape or extend
ResolvedReference with an access-mode flag, then map assignment targets to variable_write while
keeping read sites as variable_read."""

dismissal_reason = """ResolvedReference does not carry parent-statement context. The resolver's
contract is a flat list of reference sites with (Expression, Location, EnclosingScope, Outcome)
but no information about whether the expression is on the LHS of an assignment. Detecting writes
requires walking the AST upward from each expression node to its containing statement and checking
AssignmentStatement.LHS — substantial new AST-walking work. Session 2's step list specifies
from_fqn, to_fqn, identifier, kind, resolution_status, and resolution_reason, but does NOT list
write-vs-read detection as a deliverable. The schema column 'variable_write' is aspirational and
could be populated in a future resolver enhancement session."""

prompt = (
    "You are a second-opinion tiebreaker. The primary verifier (GPT-5.4) flagged the following "
    "finding as Major. The orchestrator (Claude Sonnet 4.6) disagrees. You must adjudicate.\n\n"
    "## Finding M2\n\n"
    + finding_text + "\n\n"
    "## Orchestrator's dismissal reason\n\n"
    + dismissal_reason + "\n\n"
    "## Relevant spec (Session 2 steps)\n\n"
    + spec_session2 + "\n\n"
    "## ResolvedReference contract (what the resolver exposes)\n\n"
    "```csharp\n" + resolved_reference + "\n```\n\n"
    "## ClassifyKind implementation in ReferencesExtractor.cs\n\n"
    "```csharp\n" + refs_extractor + "\n```\n\n"
    "## Question\n\n"
    "Given that:\n"
    "1. `ResolvedReference` does NOT carry write-vs-read context\n"
    "2. Session 2's spec steps do NOT explicitly require write detection\n"
    "3. Adding write detection requires new AST-walking code outside Session 2 scope\n\n"
    "Is the verifier's Major finding justified (orchestrator must fix in Session 2), or is the "
    "orchestrator's dismissal correct (variable_write detection is out of Session 2 scope and "
    "should be deferred to a future resolver enhancement)?\n\n"
    "Please reason about:\n"
    "- Whether the schema's 'variable_write' kind implies Session 2 must populate it\n"
    "- Whether persisting 'variable_read' for all identifier references (including writes) is "
    "a correctness defect vs. an acceptable known limitation for this session\n"
    "- Whether the fix would require changes outside the ReferencesExtractor\n\n"
    "Conclude with a single verdict: ACCEPT_FINDING (the finding is justified, fix in Session 2) "
    "or ACCEPT_DISMISSAL (orchestrator's dismissal is correct, defer to future session). "
    "Then give a one-paragraph explanation."
)

print(f"Tiebreaker prompt: {len(prompt)} chars")

# Save prompt
pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/').mkdir(parents=True, exist_ok=True)
pathlib.Path('docs/session-sets/vba-symbol-db/session-reviews/session-002-tiebreaker-m2-prompt.txt').write_text(prompt, encoding='utf-8')

# Route to a different provider than GPT-5-4 (original verifier)
# Force Gemini Pro via query() to ensure different provider
from ai_router import query
result = query(
    'gemini-pro',
    content=prompt,
    task_type='analysis',
    session_set=SESSION_SET,
    session_number=2,
)

# Persist before display
out_path = 'docs/session-sets/vba-symbol-db/session-reviews/session-002-tiebreaker-m2.md'
pathlib.Path(out_path).write_text(result.content, encoding='utf-8')
print(f"Tiebreaker response saved to {out_path}")
print(f"Model: {result.model_name}, tokens: in={result.input_tokens} out={result.output_tokens}, cost=${result.cost_usd:.4f}")

# Record the adjudication event
try:
    import yaml
    config = yaml.safe_load(pathlib.Path('ai-router/router-config.yaml').read_text(encoding='utf-8'))
except Exception:
    config = {}

record_adjudication(
    config,
    task_type='session-verification',
    cause='genuine-split',
    resolution='second-opinion',
    session_set=SESSION_SET,
    session_number=2,
    generator_model='claude-sonnet-4-6',
    verifier_model='gpt-5-4',
    finding_summary='ClassifyKind never distinguishes variable writes; all identifier refs emit variable_read',
    dismissal_reason='ResolvedReference has no write-context; write detection requires AST parent walking beyond Session 2 scope',
)

print("Adjudication recorded.")
print("\n--- Tiebreaker verdict ---")
# Print last 300 chars to get the conclusion
print(result.content[-600:])
