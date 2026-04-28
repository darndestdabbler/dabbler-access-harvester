# Verification Round 1

The end-to-end test fixture, `EndToEndFixtureTests.cs`, correctly exercises the full processing pipeline as required by the specification. The `RunFixture` helper method chains the necessary components in the correct sequence: parse, scope building, reference resolution, cross-reference indexing, function classification, navigation graph building, and unresolved item aggregation. This comprehensive chain ensures that the interactions between components are tested, validating the integrated system rather than just individual units.

The per-fixture `[Fact]` tests are well-designed and precisely verify the expected behaviors for each of the five anchor fixtures. The assertions for Fixture 07 (mixed classification), Fixture 08 (unresolved dynamic navigation), Fixture 10 (property kinds), Fixture 11 (multiple `OpenForm` edges), and Fixture 12 (specific handler-to-target edges) are all present and correctly align with the stated purpose of each fixture in the project's test-case documentation. These tests confirm that the enrichment and aggregation phases are producing the correct high-level outputs for key patterns.

The `MapSchemaReason` helper method provides comprehensive and deterministic mapping from the richer internal `UnresolvedItem` reason codes to the closed enum defined in `harvester-schema.v0.json`. By using nested switch expressions with default fallbacks, it guarantees that every possible input is handled. The mapping is stateless and purely functional, ensuring that identical inputs always produce identical outputs, which is critical for the overall determinism of the pipeline's output.

The three fixes applied in response to the schema-validation analysis are correct in both implementation and posture. The `MapProcedureKind` helper's handling of `ProcedureKind.Declare` correctly mitigates the v0-G5 schema gap, and the associated comment and `AnchorFixtures_DoNotProduceDeclareProcedureKind` guard test provide robust documentation and regression prevention. The `Math.Max(1, ...)` coercion for source locations correctly handles the 0-based ANTLR position-to-schema mapping. Finally, updating the envelope validation test to assert against the schema's canonical list of procedure kinds, rather than the internal pipeline enum, correctly tightens the test's adherence to the public contract.

The pipeline output is genuinely deterministic, as confirmed by the `RunOutputEnvelope_IsDeterministicAcrossRepeatedRuns` test, which performs a byte-identical comparison of two separate runs. This determinism is structurally sound: `FunctionClassificationBuilder` preserves source order for procedures, and `UnresolvedItemAggregator` pre-sorts its output. These behaviors, combined with stable JSON property serialization, ensure that the final JSON output is stable across runs, a key requirement for reliable consumption and diffing by downstream tools.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
