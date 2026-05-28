#!/usr/bin/env python3
"""One-shot verification script for harvester-schema-v1-adr-followups Session 1."""
import pathlib
import sys
import os

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))

import ai_router

def main():
    qabs = pathlib.Path("AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs").read_text(encoding="utf-8")
    schema_v = pathlib.Path("AccessHarvester.Pipeline/Validation/SchemaValidator.cs").read_text(encoding="utf-8")
    migrate_py = pathlib.Path("tools/migrate_v0_to_v1.py").read_text(encoding="utf-8")[:4000]
    round_trip = pathlib.Path("tests/AccessHarvester.Pipeline.Tests/PredicateNodeRoundTripTests.cs").read_text(encoding="utf-8")
    conformance = pathlib.Path("tests/AccessHarvester.Pipeline.Tests/TestCaseFixtureConformanceTests.cs").read_text(encoding="utf-8")
    adr_tail = pathlib.Path("docs/schema/v1-adr.md").read_text(encoding="utf-8")[-3000:]
    fixture01 = pathlib.Path("docs/test-cases/01-simple-validation.expected.json").read_text(encoding="utf-8")[:1500]

    # PredicateNode section from QueryAbstraction.cs
    start = qabs.find("[JsonPolymorphic")
    end = qabs.find("public sealed record AndPredicate") + 100
    predicate_section = qabs[start:end]

    prompt = f"""Your response MUST begin with a detailed prose analysis for each concern below BEFORE the JSON block. Do not skip the analysis. Both the analysis and the JSON block are required.

Session: harvester-schema-v1-adr-followups, Session 1 of 1
Orchestrator: claude-sonnet-4-6

## Session Goal
Close two "Action required" items in docs/schema/v1-adr.md Known Issues:
1. Add [JsonPolymorphic] / [JsonDerivedType] to PredicateNode so pipeline output validates against v1.0 schema for PredicateNode-containing fields.
2. Migrate 13 docs/test-cases/*.expected.json fixtures from v0 camelCase to v1.0 PascalCase DatabaseManifest envelope via tools/migrate_v0_to_v1.py.

## What Was Done
1. Added [JsonPolymorphic(TypeDiscriminatorPropertyName="NodeKind")] + [JsonDerivedType] for 9 concrete subtypes to PredicateNode in QueryAbstraction.cs.
2. Fixed latent race condition in SchemaValidator (ConcurrentDictionary.GetOrAdd -> Lazy<JsonSchema>).
3. Authored tools/migrate_v0_to_v1.py: key PascalCasing, enum-value migration, DatabaseManifest wrapping, idempotency.
4. Ran migration against all 13 fixtures. 12 pass v1.0 validation; fixture 12 excluded by stop rule (Source: "g_CurrentUserID" -- variable name where enum required; semantic incompatibility).
5. Added PredicateNodeRoundTripTests.cs (4 tests: And/Or/Comparison round-trip + schema validation).
6. Added TestCaseFixtureConformanceTests.cs (12 tests: each passing fixture validates against v1.0 schema).
7. Updated v1-adr.md: Known Issues -> Resolved.
8. dotnet build: 0 errors. dotnet test: 391 Pipeline.Tests passed, 0 failures across full suite.

## Key Files

### PredicateNode polymorphism attributes (QueryAbstraction.cs)
```csharp
{predicate_section}
```

### SchemaValidator.cs (race condition fix)
```csharp
{schema_v}
```

### tools/migrate_v0_to_v1.py (first 4000 chars)
```python
{migrate_py}
```

### PredicateNodeRoundTripTests.cs
```csharp
{round_trip}
```

### TestCaseFixtureConformanceTests.cs
```csharp
{conformance}
```

### docs/test-cases/01-simple-validation.expected.json (migrated, first 1500 chars)
```json
{fixture01}
```

### docs/schema/v1-adr.md (last 3000 chars -- Known Issues -> Resolved)
```
{adr_tail}
```

## Out of scope (not included in this verification)
- The v1.0 JSON schema file itself (docs/test-cases/harvester-schema.v1.0.json) -- pre-existing, not changed this session.
- The 12 other migrated fixture files -- structurally identical migrations of the pattern shown in fixture 01.
- fixture 12 (12-switchboard-navigation.expected.json) -- excluded from conformance gate by stop rule.

## Concerns to verify
1. PredicateNode [JsonPolymorphic] correctness: Are all 9 subtypes covered? Are discriminator strings correct per v1.0 schema enum ("And","Or","Not","Comparison","In","IsNull","Between","Like","Opaque")?
2. Round-trip test coverage: Do the 4 tests adequately cover the spec requirement (single AndPredicate, nested Or/And, leaf comparison, schema validation)?
3. SchemaValidator race fix: Is the Lazy<JsonSchema> approach correct for preventing concurrent registration errors?
4. Migration script correctness: Does the key-PascalCase logic + GLOBAL_VALUE_OVERRIDES + POST_CAP_OVERRIDES look correct? Is idempotency adequate?
5. Fixture conformance test exclusion: Is the stop-rule exclusion for fixture 12 justified and correctly implemented?
6. ADR update accuracy: Do the Resolved sections accurately describe what was done?

After your analysis, output a JSON block in this exact format:
```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```
If verdict is VERIFIED, issues must be an empty array [].
Do not omit this block.
"""

    print(f"Prompt length: {len(prompt)} chars")
    result = ai_router.route(
        content=prompt,
        task_type="session-verification",
        session_set="harvester-schema-v1-adr-followups",
        session_number=1,
    )

    output_path = pathlib.Path("docs/session-sets/harvester-schema-v1-adr-followups/session-reviews/session-001.md")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(result.content, encoding="utf-8")
    print(f"Saved to {output_path}")
    print("--- FIRST 600 CHARS ---")
    print(result.content[:600])
    return result

if __name__ == "__main__":
    main()
