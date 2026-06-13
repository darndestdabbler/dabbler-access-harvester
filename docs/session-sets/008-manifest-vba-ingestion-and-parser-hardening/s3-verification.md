**D1 — Schema versioning scope.** Keeping the manifest envelope at `1.1` while bumping only the embedded `ParserOutput.SchemaVersion` from `1.0` to `1.1` is defensible on the facts presented. The new field is additive, optional, and placed on a definition that already tolerated extra properties, so this is not an envelope-shape break. The envelope schema file already accepts the new documents, and the producer is using the sub-document version to signal a ParserOutput-level enhancement rather than an envelope contract change. That part is reasonable.

The only caveat is process/historical immutability: editing a versioned schema file (`harvester-schema.v1.1.json`) in place is acceptable **only if** this project treats that file as the living schema for envelope v1.1, not as an immutable released artifact. Given your own description, this change is effectively a documentation-and-clarification update for a shape the schema already admitted, not a validator-breaking contract expansion. So I would not force a v1.2 envelope bump here. In short: sub-version bump good; envelope file staying at `1.1` acceptable; in-place documentation of an optional property acceptable in this repo model.

**D2 — `ResolutionReason` as a free string.** This is the right call. If the source of truth is an append-only unresolved-reason set that may grow over time, a schema enum would create avoidable forward-compatibility failures for older schema snapshots. A free string, paired with strong documentation of known current values and append-only semantics, preserves compatibility while still surfacing useful semantics to consumers. The description you added does that. I do not see a problem with leaving it non-enumerated.

**D3 — F-3-9 choosing Option B.** Option B is honest if the repo does not actually enforce fixture-by-fixture conformance against every `*.expected.json`. Claiming a stronger executable contract than the test suite really enforces would be worse than restating the true one. Reframing the enforced contract as schema-shape validation, targeted behavioral assertions, and the HomeHealthCare byte golden is therefore the more accurate outcome. Reclassifying the `expected.json` files as design-intent/onboarding artifacts is fine so long as the docs clearly say they are illustrative, not binding. On what you described, this does **not** weaken a real guarantee; it corrects overstated contract language to match reality.

**D4 — Expected golden delta.** Treating this as expected is correct. The combination of (a) every ParserOutput sub-document moving to `1.1`, (b) thousands of newly surfaced `ResolutionReason` values on already-unresolved references, and (c) unchanged `kb/` and `ir/` trees is exactly what I would expect from F-3-6 in the disclosed intermediate “Unresolved wall” state. The important distinction is whether new unresolved references were introduced versus existing unresolved references merely gaining reasons. Your description points to the latter. So I do not see this golden delta as a regression.

**Additional closeout concern — F-3-6 appears only partially evidenced.** This is the blocker for me. The session scope did not stop at `ResolutionReason` and query SQL parse failure diagnostics; it also called for folding **per-module VBA parse diagnostics (counts + `ModeUsed`) into top-level manifest `Diagnostics`**. In the evidence provided here, I only see:
- `ResolutionReason` projection and schema documentation,
- `QUERY_SQL_PARSE_FAILURE` replacing the silent query catch,
- tests for those two behaviors.

I do **not** see corresponding code, schema/doc updates, or a test pin proving that per-module parse degradation/failure diagnostics now expose the required counts and `ModeUsed` at the manifest level. Because this is the final closeout session and you asked for a false-positive bias, I cannot treat that scoped deliverable as verified from the supplied material. If it exists elsewhere, it needs to be surfaced explicitly; if it does not, the session is incomplete on F-3-6.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Pipeline.cs + tests/AccessHarvester.Tests/ManifestVbaIngestionTests.cs",
      "summary": "The scoped F-3-6 requirement to surface per-module VBA parse diagnostic counts and ModeUsed in top-level manifest Diagnostics is not shown or test-pinned, so that deliverable remains unverified.",
      "fix": "Implement the manifest-diagnostics fold if it is still missing, or provide the missing code/doc/schema diff and add an explicit ingestion test proving degraded/failure VBA modules emit manifest-level diagnostics that include counts and ModeUsed."
    }
  ]
}
```