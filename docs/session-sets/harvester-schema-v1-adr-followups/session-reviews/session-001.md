1. **PredicateNode `[JsonPolymorphic]` correctness**

The polymorphism wiring shown for `PredicateNode` is correct for the stated v1.0 requirement. The base type is annotated with `TypeDiscriminatorPropertyName = "NodeKind"`, which matches the schema’s expected discriminator property name exactly, including case. The nine `JsonDerivedType` registrations shown are:

- `AndPredicate` → `"And"`
- `OrPredicate` → `"Or"`
- `NotPredicate` → `"Not"`
- `ComparisonPredicate` → `"Comparison"`
- `InPredicate` → `"In"`
- `IsNullPredicate` → `"IsNull"`
- `BetweenPredicate` → `"Between"`
- `LikePredicate` → `"Like"`
- `OpaquePredicate` → `"Opaque"`

That is the full nine-value set you asked to verify, and the discriminator strings exactly match the v1.0 enum values you listed. For `System.Text.Json`, this is the right mechanism to make fields typed as `PredicateNode` serialize with a `NodeKind` discriminator and deserialize back to the correct concrete subtype. The nested `IReadOnlyList<PredicateNode>` case in `AndPredicate`/`OrPredicate` is also covered by this same base-type annotation, so child predicates should carry discriminators as well. On the material provided, this concern is resolved correctly.

2. **Round-trip test coverage**

The four tests are adequate for the specific spec requirement you named.

- `AndPredicate_RoundTrips_WithNodeKindDiscriminator` covers the single `AndPredicate` case.
- `OrPredicateWithNestedAnd_RoundTrips_WithNodeKindDiscriminator` covers the nested `Or`/`And` tree case and explicitly checks that nested operands also emit `NodeKind`.
- `ComparisonPredicate_RoundTrips_WithNodeKindDiscriminator` covers the leaf `ComparisonPredicate` case.
- `PredicateNodeInManifest_ValidatesAgainstV1Schema` covers the critical end-to-end proof: a real `DatabaseManifest` containing a `PredicateNode` validates against the v1.0 schema.

That set directly maps to the requirement described in the test comments and in your verification prompt. It is not exhaustive across all nine predicate subtypes, but that is a scope question, not a correctness defect. For proving that the new polymorphic serialization shape is emitted, survives deserialize/serialize round-trip, and is accepted by the schema, the coverage is sufficient.

3. **SchemaValidator race fix**

The `Lazy<JsonSchema>` change is the correct fix for the class of race you described.

The important point is that `JsonSchema.FromText(...)` is the side effect that must happen exactly once per unique schema identity, because the underlying library uses a global registry keyed by `$id`. Storing `Lazy<JsonSchema>` inside the `ConcurrentDictionary` means the dictionary arbitrates which `Lazy` wins for a given key, and then `Lazy` arbitrates single execution of `JsonSchema.FromText(...)` for that winning entry. That is the standard pattern for eliminating duplicate expensive or side-effecting initialization under concurrency.

The path cache uses `Lazy<string>` with `OrdinalIgnoreCase`, which is reasonable for Windows path semantics. The schema cache uses `Ordinal` for schema IDs / URI strings, which is also reasonable. Using `$id` when present, and a content hash otherwise, is a sound way to deduplicate path-based and text-based loads of the same schema.

So, for the stated problem—preventing concurrent duplicate registration failures—this implementation is correct. The only caveat is theoretical: if two different schema texts were ever loaded with the same `$id`, the first one cached would win. But that would already represent invalid schema identity hygiene, and nothing in your session suggests that scenario exists here. For the actual defect being fixed, this is a proper solution.

4. **Migration script correctness**

Based on the script excerpt, the migrated fixture example, and the reported validation results, the migration logic looks correct for the intended mechanical transformation.

- **Key migration:** “capitalize first char” is the right rule for the v0→v1 property-name shape you described (`schemaVersion`→`SchemaVersion`, `itemKind`→`ItemKind`, etc.). The migrated fixture sample is consistent with that rule.
- **Enum-value migration:** The `ENUM_VALUED_KEYS` table is targeted and sensible. Including both camelCase and PascalCase spellings is the right choice for mixed-state inputs. The explicit `GLOBAL_VALUE_OVERRIDES` handle the known cases where first-letter capitalization is insufficient, such as `gosub`→`GoSub` and the overloaded `Source` enum values.
- **Envelope wrapping:** Wrapping a bare v0 parser-output document into a `DatabaseManifest` root is exactly what the ADR follow-up required.
- **Idempotency:** Short-circuiting on `SchemaType == "DatabaseManifest"` is adequate idempotency for the intended workflow. Re-running the tool on already migrated fixtures should be a no-op, which is the important property here.

Most importantly, the empirical result strongly supports correctness: 12 migrated fixtures validate against the v1.0 schema, and the remaining fixture is excluded for a documented semantic incompatibility rather than a mechanical migration failure. That is the expected outcome for a migration script that is intentionally mechanical, not semantic.

5. **Fixture conformance test exclusion**

The stop-rule exclusion for `12-switchboard-navigation.expected.json` is justified and correctly implemented.

The documented failure mode is not a casing or envelope issue; it is a semantic mismatch where `ParserQueryParameter.Source` contains `g_CurrentUserID`, a VBA variable identifier, while the v1.0 schema requires one of a constrained enum set. That is exactly the kind of thing a mechanical migration should not silently “guess” at. The ADR’s suggested interpretation (`ConcatenatedVariable`) is plausible, but it is still a semantic decision, so excluding the fixture until that decision is explicitly made is the right approach.

Implementation-wise, the exclusion is narrow and clear:

- it is filename-specific,
- case-insensitive,
- documented in both code comments and the ADR,
- and applied only when building the theory data set.

That means the v1.0 conformance suite still enforces all other fixtures while intentionally carving out the one known semantic exception. This is a correct stop-rule implementation.

6. **ADR update accuracy**

The ADR update accurately reflects the work described in the session summary.

The “Resolved” narrative matches the implementation evidence:

- `PredicateNode` polymorphism was added with `JsonPolymorphic`/`JsonDerivedType`.
- The migration tool was created and run.
- 12 of 13 fixtures validate against v1.0.
- Fixture 12 is migrated but excluded from the conformance gate due to a remaining semantic incompatibility.
- A regression suite now enforces both the `PredicateNode` behavior and the migrated-fixture conformance set.

The back-compat section is also internally consistent with the migrated fixture sample you provided: the root document is v1.0 `DatabaseManifest`, while embedded `ParserOutput.SchemaVersion` can still remain `0.1`, which is appropriate because the inner parser-output payload is historical content wrapped by a v1 envelope rather than fully reauthored into a different internal schema dialect.

Bottom line: the ADR’s resolved-state description matches what was actually done in this session.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```