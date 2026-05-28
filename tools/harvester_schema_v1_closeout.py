#!/usr/bin/env python3
"""Generate closeout artifacts for harvester-schema-v1-adr-followups Session 1."""
import pathlib, sys, json
sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import ai_router

SLUG = "harvester-schema-v1-adr-followups"
SESSION_DIR = pathlib.Path(f"docs/session-sets/{SLUG}")

# ---- 1. Change log -------------------------------------------------------
summary_prompt = """
Generate a concise change log for the following session work.
Format as a markdown document with sections:
## Summary
## Changes
## Testing
## Notes (if any)

Session: harvester-schema-v1-adr-followups, Session 1 of 1
Verification result: VERIFIED (Round 3, gpt-5-4)

What was done:
1. Added [JsonPolymorphic(TypeDiscriminatorPropertyName="NodeKind")] and 9 [JsonDerivedType] attributes to PredicateNode in AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs. Covers all 9 concrete subtypes: And, Or, Not, Comparison, In, IsNull, Between, Like, Opaque.
2. Fixed a latent race condition in AccessHarvester.Pipeline/Validation/SchemaValidator.cs: changed ConcurrentDictionary<string, JsonSchema> to ConcurrentDictionary<string, Lazy<JsonSchema>> so GetOrAdd factory executes exactly once per schema path under concurrency.
3. Authored tools/migrate_v0_to_v1.py: mechanically migrates v0 camelCase parser-output JSON to v1.0 DatabaseManifest format (key PascalCasing, enum-value migration, DatabaseManifest envelope wrapping, idempotency guard).
4. Ran migration against all 13 docs/test-cases/*.expected.json fixtures. All 13 are now in DatabaseManifest v1.0 envelope format with PascalCase keys.
5. Added PredicateNodeRoundTripTests.cs: 4 tests verifying [JsonPolymorphic] produces NodeKind discriminator and round-trips cleanly.
6. Added TestCaseFixtureConformanceTests.cs: 12 tests (one per passing fixture) that validate each against harvester-schema.v1.0.json as a standing regression gate. Fixture 12 (12-switchboard-navigation.expected.json) excluded per stop rule: Source: "g_CurrentUserID" is a VBA variable name where the v1.0 ParserQueryParameter.Source enum is required -- a semantic incompatibility not resolvable by mechanical renaming.
7. Updated docs/schema/v1-adr.md: both Known Issues moved to Resolved; Back-Compatibility Plan updated to reflect completed migration state.
8. Build: 0 errors. Tests: 391 Pipeline.Tests + all other suites, 0 failures.
"""

print("Routing summary/change-log...")
cl_result = ai_router.route(
    content=summary_prompt,
    task_type="summarization",
    max_tier=1,
    session_set=SLUG,
    session_number=1,
)
change_log_path = SESSION_DIR / "change-log.md"
change_log_path.write_text(cl_result.content, encoding="utf-8")
print(f"Saved change-log.md")

# ---- 2. Next session set recommendation ----------------------------------
next_set_prompt = """
Given the completed session work below, write a brief next-session-set recommendation.
Format as markdown with sections:
## Recommended Next Session Set
## Why
## Prerequisites

Completed work: harvester-schema-v1-adr-followups Session 1
- PredicateNode polymorphism fixed ([JsonPolymorphic] attributes added)
- 13 test fixtures migrated to v1.0 DatabaseManifest format
- 12 of 13 fixtures pass v1.0 schema validation (fixture 12 excluded by stop rule)
- Conformance regression test added

The two remaining new session sets in this repo are:
1. harvester-cli-validate-subcommand: Add AccessHarvester validate subcommand that validates a run output against embedded v1.0 schema. Soft-prefers this ADR-followups set to complete first (now done).
2. harvester-cli-distribution: Set up GitHub Releases distribution channel for the AccessHarvester.exe binary. Independent of the validate subcommand.

The downstream consumer Dabbler.NorthwindToBlazor Set 001 Session 2 needs to "smoke-validate the IR JSON" and currently has no first-class CLI tool for that.
"""

print("Routing next-set recommendation...")
ns_result = ai_router.route(
    content=next_set_prompt,
    task_type="summarization",
    max_tier=1,
    session_set=SLUG,
    session_number=1,
)
next_set_path = SESSION_DIR / "next-session-set-recommendation.md"
next_set_path.write_text(ns_result.content, encoding="utf-8")
print(f"Saved next-session-set-recommendation.md")

# ---- 3. Disposition.json -------------------------------------------------
disposition = {
    "status": "complete",
    "summary": "Closed both v1-adr.md Known Issues: PredicateNode polymorphism fix via [JsonPolymorphic] attributes; v0->v1 fixture migration via tools/migrate_v0_to_v1.py. 12/13 fixtures pass v1.0 schema validation; conformance regression test added.",
    "session_set": SLUG,
    "session_number": 1,
    "verification_verdict": "VERIFIED",
    "verification_rounds": 3,
    "verifier_model": "gpt-5-4",
    "files_changed": [
        "AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs",
        "AccessHarvester.Pipeline/Validation/SchemaValidator.cs",
        "tools/migrate_v0_to_v1.py",
        "tests/AccessHarvester.Pipeline.Tests/PredicateNodeRoundTripTests.cs",
        "tests/AccessHarvester.Pipeline.Tests/TestCaseFixtureConformanceTests.cs",
        "docs/test-cases/01-simple-validation.expected.json",
        "docs/test-cases/02-cascading-combo-subform.expected.json",
        "docs/test-cases/03-with-block-shadowing.expected.json",
        "docs/test-cases/04-saved-query-form-params.expected.json",
        "docs/test-cases/05-dynamic-sql-construction.expected.json",
        "docs/test-cases/06-calculated-expressions.expected.json",
        "docs/test-cases/07-legacy-patterns.expected.json",
        "docs/test-cases/08-unresolvable.expected.json",
        "docs/test-cases/09-report-grouping-events.expected.json",
        "docs/test-cases/10-class-with-events.expected.json",
        "docs/test-cases/11-autoexec-startup-chain.expected.json",
        "docs/test-cases/12-switchboard-navigation.expected.json",
        "docs/test-cases/13-preprocessor.expected.json",
        "docs/schema/v1-adr.md",
        "docs/session-sets/harvester-schema-v1-adr-followups/session-reviews/session-001.md",
        "docs/session-sets/harvester-schema-v1-adr-followups/session-reviews/session-001-r2.md",
        "docs/session-sets/harvester-schema-v1-adr-followups/session-reviews/session-001-r3.md",
        "docs/session-sets/harvester-schema-v1-adr-followups/change-log.md",
        "docs/session-sets/harvester-schema-v1-adr-followups/next-session-set-recommendation.md",
        "docs/session-sets/harvester-schema-v1-adr-followups/disposition.json"
    ],
    "next_session_set": {
        "slug": "harvester-cli-validate-subcommand",
        "summary": "Add AccessHarvester validate subcommand; soft-prerequisite (this set) is now complete."
    },
    "blockers": [],
    "stop_rule_notes": "Fixture 12 (12-switchboard-navigation.expected.json) excluded from v1.0 conformance gate: Source field contains a VBA variable name ('g_CurrentUserID') where the v1.0 schema requires a ParserQueryParameter.Source enum value. Semantic incompatibility; fixture migrated in place but excluded from TestCaseFixtureConformanceTests via StopRuleExclusions."
}

disp_path = SESSION_DIR / "disposition.json"
disp_path.write_text(json.dumps(disposition, indent=2), encoding="utf-8")
print(f"Saved disposition.json")
print("Closeout artifacts done.")
