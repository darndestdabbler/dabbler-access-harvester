After reviewing the current ADR text against the two Round 1 findings, I do not think it is fully verified yet.

The version-detection problem does appear to be fixed. The Back-Compatibility Plan now tells consumers to inspect `document.SchemaVersion` for `DatabaseManifest` root documents and to use embedded `ParserOutput.SchemaVersion` at paths such as `Modules[n].ParserOutput.SchemaVersion` and `Forms[n].ParserOutput.SchemaVersion`. That matches the manifest structure described elsewhere in the ADR, and I do not see any remaining reference to `document.Source.SchemaVersion`. On that specific point, the correction is good.

The fixture-migration story, however, is still not internally consistent. The later Back-Compatibility Plan correctly states the intended current status: 12 of 13 fixtures are migrated to v1.0, and fixture 12 remains a v0 stop-rule exclusion. But earlier text still describes the migration as future or not-yet-shipped work. In the Summary, the ADR says v0 remains valid “until a migration script is shipped,” which is stale now that the script is described as already shipped. In the D20 section, the “Impact on test fixtures” paragraph still says all 13 fixtures use camelCase “today” and that “Session 2” will update them, which is also stale and conflicts with the resolved state later in the document.

There is also still a direct contradiction inside the migration-status text itself. In Known Issues, the resolution says the migration “was applied to all 13” fixtures, but the Back-Compatibility Plan says it was applied to 12 of the 13 fixtures and that fixture 12 remains at v0. If the intent is that the script was attempted on all 13 but only 12 were adopted as v1.0 fixtures, the document needs to say that explicitly. As written, the ADR does not yet present a single consistent 12/13-migrated narrative.

So the second Round 1 issue is resolved, but the first one is not fully resolved, and stale text still remains.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/schema/v1-adr.md (Summary; D20 — Impact on test fixtures)",
      "summary": "The ADR still contains stale future-tense migration text, saying the script is not yet shipped and that Session 2 will update all 13 fixtures.",
      "fix": "Rewrite these sections to reflect the current state: the migration script ships in harvester-schema-v1-adr-followups Session 1, 12 of 13 fixtures are migrated to v1.0, and fixture 12 remains a v0 stop-rule exclusion."
    },
    {
      "severity": "Minor",
      "location": "docs/schema/v1-adr.md (Known Issues — Test fixture casing migration)",
      "summary": "The migration count is still inconsistent: this section says the migration was applied to all 13 fixtures, but the Back-Compatibility Plan says only 12 of 13 were migrated and fixture 12 remains v0.",
      "fix": "Make the Known Issues wording match the operative status, e.g. state that 12 of 13 fixtures were migrated to v1.0 and that 12-switchboard-navigation.expected.json was excluded under the stop rule and retained as a v0 reference."
    }
  ]
}
```