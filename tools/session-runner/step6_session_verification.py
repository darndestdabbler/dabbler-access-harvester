"""Step 6: Route session verification for vba-grammar-validation Session 1."""
import importlib.util, sys, os
from pathlib import Path

def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router", "ai-router/__init__.py",
        submodule_search_locations=["ai-router"])
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod

ar = load_ai_router()
route = ar.route
from ai_router.session_log import SessionLog

SESSION_SET = "docs/session-sets/vba-grammar-validation"
log = SessionLog(SESSION_SET)

# Read all edge fixture VBA sources and expected JSONs
edge_dir = Path("docs/test-cases/edge")
fixture_content = []
for bas_file in sorted(edge_dir.glob("*.bas")) + sorted(edge_dir.glob("*.cls")):
    json_file = bas_file.with_suffix("").with_suffix(".expected.json")
    if not json_file.suffix:
        json_file = bas_file.with_name(bas_file.stem + ".expected.json")
    else:
        json_file = bas_file.parent / (bas_file.stem + ".expected.json")
    vba_src = bas_file.read_text(encoding="utf-8")
    json_src = json_file.read_text(encoding="utf-8") if json_file.exists() else "(missing)"
    fixture_content.append(f"### {bas_file.name}\n\nVBA Source:\n```vba\n{vba_src}\n```\n\nExpected JSON (schema-validated PASS):\n```json\n{json_src[:600]}...\n```")

fixtures_text = "\n\n".join(fixture_content)

# Read the hazard list excerpt (key non-covered hazard IDs)
hazard_excerpt = """
The 5 hazards explicitly requiring synthetic fixtures per docs/spec-coverage/hazard-list.md:

- H-D05: Resume / Resume Next / Resume <label> (§5.4.12) -- no corpus witness
- H-D12: Named arguments at call sites (:=) (§5.5) -- no corpus witness
- H-E07: ParamArray parameter (§4.6) -- no corpus witness
- H-A07 (REM sub-case): REM-form comments (§3.3.1) -- not in corpus
- H-D04 (On Error GoTo 0 sub-case): Deactivate error handler (§5.4.11) -- not in corpus
"""

spec_excerpt = """
## Session 1 spec excerpt (from docs/session-sets/vba-grammar-validation/spec.md)

Session 1: Edge-Case Fixture Authoring
- Read hazard-list.md and 12 fixture coverage to identify non-covered hazards
- Route test-generation for each non-covered hazard producing a fixture pair
- Land fixtures under docs/test-cases/edge/

Success criteria: All hazard-listed uncovered constructs have a paired fixture; build and tests pass; verified by cross-provider.

Standards:
- Fixtures follow the shape of existing 12 test cases (input .bas + pinned .expected.json)
- Each fixture's expected JSON validates against harvester-schema.v0.json
- Fixtures are minimal: one fixture exercises one hazard
"""

verification_prompt = f"""
# Session Verification Request: vba-grammar-validation Session 1 of 4

You are a cross-provider verifier reviewing the work completed in Session 1 of the `vba-grammar-validation` session set for the Dabbler Access Harvester project. This is NOT the orchestrator reviewing its own work; it is an independent verification step. Be skeptical and thorough.

## Session Scope

{spec_excerpt}

## Hazards Requiring Synthetic Fixtures

{hazard_excerpt}

## What Was Produced

Session 1 authored 5 edge-case fixture pairs (one per uncovered hazard) under `docs/test-cases/edge/`. All 5 expected JSON files passed schema validation against `harvester-schema.v0.json`. The full test suite ran: 278 grammar tests + 328 core tests passed (2 skipped = COM integration requiring Windows Access install).

## Fixture Files

{fixtures_text}

## Files Outside Scope (Present in Repo but Not Changed This Session)

- `docs/test-cases/01-12-*.bas/cls/sql/txt` and their `*.expected.json` -- the 12 existing corpus fixtures (not modified)
- `AccessHarvester.Vba.Grammar/Vba.g4` -- the grammar itself (not modified; grammar changes are Session 2's scope)
- `docs/spec-coverage/hazard-list.md` -- the hazard list (not modified; read only)
- `docs/spec-coverage/parse-failure-log.md` -- parse failure log (not modified yet; Session 2 and 3 will populate it)

## Verification Criteria

Please verify:
1. **Hazard coverage**: Does each fixture correctly target its stated hazard? Is the VBA source syntactically valid for the hazard it exercises?
2. **Fixture minimalism**: Are fixtures minimal (one hazard per fixture) or do they contain unnecessary complexity?
3. **Expected JSON correctness**: Given the schema, are the expected JSON files structurally reasonable? Do the statement kinds, procedure kinds, visibility, and parameter shapes match the VBA source?
4. **No over-engineering**: Were any unnecessary files created? Is the scope contained?
5. **Build and test**: Build succeeded with 0 errors; tests 278+328 passed. Is there any concern about these results?
6. **Missing coverage**: Are there any hazards listed in the hazard-list.md that should have been covered but were not? Specifically note H-A07 (REM) and H-D04 (GoTo 0) which are sub-case gaps, not full hazard gaps.

## Output Format

VERDICT: [VERIFIED | ISSUES_FOUND]

If ISSUES_FOUND, list each issue:
- Severity: [Critical | Major | Minor | Info]
- Finding: <specific description>
- Location: <file/section>
- Suggested fix: <concrete action>

If VERIFIED, write a brief confirmation of what was checked and that the session's deliverable is correct.
"""

print("Routing session verification...")
result = route(
    content=verification_prompt,
    task_type="session-verification",
    complexity_hint=70,
    session_set=SESSION_SET,
    session_number=1,
)

review_path = "docs/session-sets/vba-grammar-validation/session-reviews/session-001.md"
Path("docs/session-sets/vba-grammar-validation/session-reviews").mkdir(parents=True, exist_ok=True)
with open(review_path, "w", encoding="utf-8") as f:
    f.write(result.content)
print(f"Session review saved to {review_path}")
print(f"Model: {result.model_name}, Cost: ${result.cost_usd:.4f}")

# Log step
log.log_step(1, 8, "session-1/session-verification",
             f"Cross-provider session verification via route(session-verification). Model: {result.model_name}",
             "complete",
             [{
                 "model": result.model_name,
                 "taskType": "session-verification",
                 "inputTokens": result.input_tokens,
                 "outputTokens": result.output_tokens,
                 "costUsd": result.cost_usd,
             }])
print("Verification step logged")
