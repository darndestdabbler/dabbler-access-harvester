1. **PredicateNode `[JsonPolymorphic]` correctness**

The `PredicateNode` base type is annotated with one `JsonPolymorphic` attribute and nine `JsonDerivedType` entries, and the nine discriminator strings shown are exactly the v1.0 schema enum values you called out: `"And"`, `"Or"`, `"Not"`, `"Comparison"`, `"In"`, `"IsNull"`, `"Between"`, `"Like"`, and `"Opaque"`. Based on the snippet, this closes the specific gap that would have caused `PredicateNode`-containing fields to serialize without a schema-recognized discriminator.

The discriminator property name is also correct: `TypeDiscriminatorPropertyName = "NodeKind"` matches the schema contract described in the tests and ADR. The round-trip tests provide practical confirmation that `NodeKind` is emitted on root nodes and nested operands, and the schema-validation test confirms that a manifest containing a `PredicateNode`-valued `Where` field now validates against v1.0. On this concern, the implementation looks correct.

2. **Round-trip test coverage**

The four tests are well aligned with the stated acceptance criteria. You asked for coverage of: a single `AndPredicate`, a nested `OrPredicate`/`AndPredicate` tree, a leaf `ComparisonPredicate`, and schema validation of a manifest containing a `PredicateNode`. Those are exactly the four tests present.

The tests also do the right kind of verification: they assert the discriminator value, they deserialize back through the abstract base type, and they re-serialize to confirm stable polymorphic round-trip behavior. The nested test additionally checks that child operands also carry `NodeKind`, which is important because the schema problem would often surface below the root. This is not exhaustive coverage of all nine concrete predicate kinds, but it is adequate for the spec you described.

3. **SchemaValidator race fix**

The `Lazy<JsonSchema>` change is the correct fix for the specific `ConcurrentDictionary.GetOrAdd` race you described. A plain `GetOrAdd(path, factory)` can invoke the factory multiple times under contention even though only one value is retained, which is a real problem when `JsonSchema.FromText` has global registration side effects keyed by `$id`.

By storing `Lazy<JsonSchema>` instead of `JsonSchema`, you move the expensive and side-effecting work into `Lazy.Value`. Multiple `Lazy<JsonSchema>` instances may still be constructed transiently during a race, but only the winning instance stored in the dictionary has its `Value` evaluated by callers. With the default `Lazy<T>` behavior, the schema load/registration then runs once for that cached entry. For the same absolute schema path, this correctly eliminates the duplicate-registration race.

There is still a theoretical edge case if the same physical schema file is reachable via different absolute paths, but that is outside the defect you were fixing and is not introduced by this change. For the stated concern, the fix is sound.

4. **Migration script correctness**

For the stated migration scope, the script design is coherent. The three required transforms are all represented in the snippet and in the observed results: PascalCase object keys, PascalCase enum values where needed, and wrapping bare parser-output JSON in a `DatabaseManifest` envelope. The stated idempotency rule—no-op when `SchemaType == "DatabaseManifest"`—is also appropriate for this use case.

The strongest evidence here is outcome-based: you ran the migration over the full fixture set, and 12 fixtures validate against the v1.0 schema afterward, with tests added to keep them conformant. That strongly suggests the current key/value mappings are sufficient for the existing corpus.

The only caveat I would call out is that the comments say `GLOBAL_VALUE_OVERRIDES` are applied to **all** string values before per-key handling. That is broader than ideal, because in principle it could rewrite a non-enum leaf string that happens to equal one of those tokens. I do not see evidence that this caused a current fixture problem, and the reported validation results support the current implementation, so I would not treat this as a blocker here. But if you want the tool to be safer as a general-purpose migrator, constraining those overrides to known enum-bearing contexts would be better.

5. **Fixture conformance test exclusion**

The stop-rule exclusion for `12-switchboard-navigation.expected.json` is justified. A value like `Source: "g_CurrentUserID"` is not a casing problem; it is a semantic mismatch between a free-form variable name in the v0 fixture and a closed enum in the v1.0 schema. A mechanical migration cannot resolve that without making an interpretive choice.

The implementation is also correct. The exclusion is explicit, filename-based, documented in both code and ADR text, and applied at test discovery time so the conformance suite accurately reflects the intended gate: 12 migrated fixtures must validate; fixture 12 is intentionally outside that gate pending semantic adjudication. That is a reasonable and transparent stop rule.

6. **ADR update accuracy**

The implementation summary is mostly accurate, but the ADR excerpt is **not fully internally consistent**.

There are two visible documentation problems:

- The fixture migration status is described inconsistently. One section says the migration “was applied to all 13” fixtures, while the Back-Compatibility Plan says it was “applied to 12 of the 13” fixtures, and another line still says v0 remains operative until a future Session 2 migrates those fixtures. Those statements cannot all be true at once given the reported Session 1 completion state.
- The version-detection guidance says consumers inspect `document.Source.SchemaVersion` for embedded `ParserOutput` nodes. That does not match the shown document shape. In the migrated fixture sample, embedded parser output lives under entity properties like `Modules[].ParserOutput`, not under `document.Source`.

So the code/test work looks good, but the ADR text still needs cleanup to accurately describe the final state.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/schema/v1-adr.md",
      "summary": "The ADR is internally inconsistent about fixture migration status and still contains stale wording that says v0 remains operative until a future Session 2 migration.",
      "fix": "Reconcile the document to one factual statement of record: explicitly state whether fixture 12 was migrated or intentionally left at v0, make the fixture count consistent everywhere, and remove the stale 'until Session 2 migrates those fixtures' text."
    },
    {
      "severity": "Minor",
      "location": "docs/schema/v1-adr.md",
      "summary": "The version-detection guidance references document.Source.SchemaVersion for embedded ParserOutput nodes, which does not match the shown DatabaseManifest structure.",
      "fix": "Update the guidance to reference the actual nested ParserOutput locations (for example Modules[].ParserOutput.SchemaVersion, Forms[].ParserOutput.SchemaVersion, etc.) or describe it generically as the embedded ParserOutput.SchemaVersion field."
    }
  ]
}
```