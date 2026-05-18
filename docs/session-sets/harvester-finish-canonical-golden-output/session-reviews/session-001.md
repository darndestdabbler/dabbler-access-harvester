# Verification Round 1

1. **Spec elaboration correctness**

The Session 1 spec work is mostly strong: it locks the four decisions the stub deferred, and Sessions 2–4 now have a coherent sequence built on those decisions. In particular:

- the aggregator is defined as a pure in-memory assembly function,
- the manifest envelope types are named and scoped,
- the golden subset is narrowed to `kb/ + ir/ + manifest.json + unresolved-items.json`,
- the normalization API is explicitly postponed to Session 4 with fixed entry points.

That said, the spec is **not fully normalized after those decisions**. There are still earlier stub-era passages that contradict the locked design. The most important examples are in the “This set” / “In scope” sections, which still say or imply:

- the aggregator “reads `ir/<Category>/*.ir.json`” and emits files itself,
- the location could be “`AccessHarvester.Pipeline/` (or `Core/`, whichever fits — Session 1 decides)”,
- the shape could be one combined file “or one combined file — Session 1 picks the shape”.

Those are no longer open questions after the Decision 1 / Session 2 text. Session 1’s job was to elaborate the stub into a full spec, so leaving contradictory pre-decision text behind is a real documentation defect. Later sessions could follow the wrong section.

2. **Aggregator contract**

`DatabaseManifestAggregator.Build(...)` does satisfy the Appendix A contract in the important ways:

- **Required arg validation:** `database` is null-checked; `harvesterVersion` rejects `null` and `""`.
- **Null collection handling:** every enumerable input goes through `Materialize`, so `null` becomes `Array.Empty<T>()`.
- **No re-sorting:** inputs are materialized in source enumeration order only.
- **Summary mirrors arrays:** counts are computed from the materialized collections.
- **No I/O:** the class is pure assembly logic only.

A couple of smaller notes:

- It allows whitespace-only `harvesterVersion`; Appendix A only promised null/empty rejection, so this is not a contract violation.
- The XML exception docs say `ArgumentNullException` is thrown for null/empty `harvesterVersion`, but the implementation throws `ArgumentException`. That is a doc mismatch, not a behavioral defect.

So on the functional contract, this is good.

3. **Schema-vs-code alignment**

For the specific question you asked: yes, the four required top-level fields are guaranteed when callers use the aggregator:

- `SchemaType` is always `"DatabaseManifest"`,
- `SchemaVersion` is always `"1.0"`,
- `HarvesterVersion` is required by the builder,
- `Database` is required by the builder.

The constants are spelled correctly in the landed code.

I would not mark Session 1 down for not proving full schema conformance yet, because the spec explicitly defers the schema-validation gate to Session 2. But I do want to note that the spec text itself still contains naming uncertainty in places, so the documentation side of schema alignment is not fully clean even though the required-field guarantee is fine.

4. **Test coverage**

The 13 tests cover the core Appendix A behaviors reasonably well:

- required-argument validation,
- schema constants,
- empty/default collections,
- summary counts,
- order preservation,
- one-shot materialization of lazy inputs,
- passthrough for `Database` and `NavigationGraph`,
- basic determinism across repeated calls.

The main gap is that the session step list explicitly called for **per-category arrays passthrough unchanged**, but the tests only sample a subset of properties. They do **not** directly assert that every collection parameter is wired into the correct manifest slot. Since the aggregator has a long manual assignment block, one copy/paste miswire could slip through without a failing test.

I would add one comprehensive “all categories map correctly” test with unique sentinel values for:

- reports,
- queries,
- modules,
- relationships,
- validation rules,
- row source specs,
- function classifications,
- event-handler classifications,
- diagnostics,
- unresolved items.

That is a coverage gap, but not evidence of a current code bug.

5. **Determinism**

The aggregator itself is deterministic.

- It performs no I/O.
- It does not inspect time, environment, filesystem, or COM.
- It materializes each enumerable once.
- It preserves caller order.

So the determinism story is correct **provided callers supply deterministic ordering**, which the spec documents clearly. I do not see a hidden nondeterminism source in the implementation.

6. **Lean-by-default conformance**

The implementation is lean and appropriately scoped.

Good choices:

- static class instead of introducing an interface,
- no projection layer for every existing type,
- no serializer/persistence logic in the aggregator,
- no speculative normalization code in Session 1.

The only mild overreach is documentary, not architectural: `DatabaseManifest`’s XML remarks claim callers cannot bypass aggregator-enforced summary consistency, but the type is still publicly constructible with public `init` setters, so that guarantee is not actually enforced by the type system. That is not a major design problem for this session, but the wording is stronger than the implementation.

7. **Session-scoping discipline**

Session scoping is good.

This change set does **not** drift into:

- pipeline wiring,
- schema validation,
- committing golden output,
- regression harness construction.

It stays within Session 1’s lane: spec elaboration, AI assignment, manifest envelope types, aggregator, and unit tests. Nothing here blocks later sessions in a problematic way; Session 2 can wire and validate, Session 3 can generate golden data, and Session 4 can build the normalizer/regression gate.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md",
      "summary": "The spec still contains pre-decision stub language that contradicts Session 1's locked decisions about the aggregator's behavior, location, and output shape.",
      "fix": "Normalize the earlier 'This set' and 'In scope' sections so they consistently state that the aggregator is a pure in-memory builder in AccessHarvester.Pipeline/Manifest, does not read IR files or emit files itself, and that Session 2 writes separate manifest.json and unresolved-items.json files."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Pipeline.Tests/Manifest/DatabaseManifestAggregatorTests.cs",
      "summary": "The tests cover the general contract but do not directly verify that every collection parameter is passed through into the correct manifest property, despite the session scope calling for per-category passthrough coverage.",
      "fix": "Add a comprehensive mapping test that supplies distinct sentinel values for each collection input (reports, queries, modules, relationships, validation rules, row source specs, function classifications, event handler classifications, unresolved items, diagnostics) and asserts each lands in the matching manifest slot with preserved order."
    }
  ]
}
```

---

# Verification Round 2

Both original findings are resolved.

- **Original major spec finding:** fixed. The rewritten **Project Overview** and **In scope** sections now consistently reflect the locked Session 1 decisions: the aggregator is a **pure in-memory builder with no I/O**, **Session 2** owns `Pipeline.Run()` wiring and file emission, and the committed golden subset is explicitly **`kb/ + ir/ + manifest.json + unresolved-items.json` under `samples/GoldenOutput/<fixture-name>/`**.
- **Original minor test finding:** fixed. The new test `Every_collection_input_lands_in_its_corresponding_manifest_slot` does what was asked: it supplies a distinct sentinel for every collection input and asserts each lands in the correct manifest slot.

I do see two **new minor documentation consistency issues** in the spec:

1. The **Purpose** section still says `UnresolvedItems.json`, while the rest of the spec consistently uses `unresolved-items.json`.
2. The **Project Overview** mentions `TableSchemas[]`, while the locked API/test language elsewhere uses `Tables[]`.

These are doc-cleanup issues, not regressions of the original findings, and I do **not** see any new issue in the updated test file.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md (Purpose)",
      "summary": "The spec uses `UnresolvedItems.json` in the Purpose section but `unresolved-items.json` everywhere else, leaving the output filename inconsistent.",
      "fix": "Standardize the filename in the Purpose section to `unresolved-items.json` so all sections use the same locked output path/name."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md (Project Overview)",
      "summary": "The Project Overview refers to `TableSchemas[]` while the rest of the locked API/spec/tests use `Tables[]`, creating a contract-name inconsistency.",
      "fix": "Align the Project Overview terminology with the locked manifest contract by using the same property name consistently everywhere; if the schema truly uses a different name, call that out explicitly and reconcile the rest of the spec/tests."
    }
  ]
}
```

---

# Verification Round 3

Yes — both round 2 findings are resolved in the provided `spec.md`.

Confirmed fixes:
- The filename is now consistently `unresolved-items.json`; I do not see any remaining `UnresolvedItems.json` references.
- The manifest property narration now uses `Tables[]`; I do not see any remaining `TableSchemas` references.

New issues surfaced in this final consistency sweep:

1. **Session sequencing is internally inconsistent.**  
   Session 3 says to apply `GoldenNormalizer.NormalizeManifest(...)` and possibly extend `NormalizePerObjectIr(...)`, but Session 4 is where `GoldenNormalizer.cs` is actually introduced. As written, Session 3 depends on a helper that does not exist yet.

2. **`unresolved-items.json` normalization contract is unclear/inconsistent.**  
   The spec describes `unresolved-items.json` as a separate serialization of the `UnresolvedItems[]` data, but Session 3/4 instruct using `NormalizeManifest(...)` on it. That only makes sense if the file is manifest-shaped, which the spec does not say. Either the file shape needs to be locked more explicitly, or it needs its own/no-op normalizer.

3. **Session 4 artifact inventory is incomplete.**  
   Step 6 says Session 4 produces `next-session-set-recommendation.md`, but that file is missing from the Session 4 **Creates** list.

4. **Session 4 step numbering has a dangling reference.**  
   “Step 9 reorganization review AFTER notify” appears in a session that only has seven listed steps. That looks like a stale templating/reference artifact.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md (Decision 4; Session 3 step 2; Session 4 step 1)",
      "summary": "Session 3 requires GoldenNormalizer usage and possible extension before Session 4 introduces GoldenNormalizer.cs.",
      "fix": "Either move GoldenNormalizer creation into Session 3, or rewrite Session 3 to perform explicitly documented manual normalization and reserve helper codification for Session 4."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md (Session 2 step 1; Session 3 step 2; Session 4 step 2)",
      "summary": "The spec treats unresolved-items.json as a standalone UnresolvedItems payload but instructs sessions to run NormalizeManifest on it, leaving the file shape/normalization contract ambiguous.",
      "fix": "Lock the unresolved-items.json shape explicitly and align the normalization API: either state it is copied verbatim, add a dedicated NormalizeUnresolvedItems method, or define the file as a manifest-like envelope if that is the intent."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md (Session 4 Creates list)",
      "summary": "Session 4 step 6 requires next-session-set-recommendation.md, but that file is omitted from the Creates list.",
      "fix": "Add docs/session-sets/harvester-finish-canonical-golden-output/next-session-set-recommendation.md to the Session 4 Creates section."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md (Session 4 step 7)",
      "summary": "Session 4 says 'Step 9 reorganization review AFTER notify' even though the session only lists seven steps.",
      "fix": "Replace the phrase with an unnumbered instruction or correct the referenced step number to match the actual session step sequence."
    }
  ]
}
```

---

# Verification Round 4

Three of the four round-3 findings are fully resolved, and the original major timing defect is substantively fixed — but there is still one stale sentence left in the spec, so I can’t mark this fully clean yet.

Summary by prior finding:

1. **Cross-session timing** — **mostly resolved, but one stale contradiction remains.**  
   The actual session plan is now correct:
   - Decision 4 says **Session 3** lands `GoldenNormalizer`
   - Session 3 step 2 creates it
   - Session 4 consumes it

   That fixes the real sequencing problem from round 3. However, **In scope #4** still says:  
   > “a `GoldenNormalizer` static class lands in Session 4”  
   which now contradicts the decision section and Session 3 steps.

2. **`unresolved-items.json` shape vs `NormalizeManifest`** — **resolved.**  
   The new `NormalizeUnresolvedItems(string json) -> string` entry point cleanly closes this.

3. **`next-session-set-recommendation.md` missing from Creates** — **resolved.**  
   It is now present in Session 4’s Creates list.

4. **“Step 9 reorganization review” wording ambiguity** — **resolved.**  
   The wording now clearly points to the workflow doc’s Step 9.

I did not find any additional new issues from these edits beyond the stale Session-4 reference above.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-golden-output/spec.md",
      "summary": "In scope item 4 still says GoldenNormalizer lands in Session 4, which contradicts Decision 4 and Session 3 step 2 where the normalizer is now created.",
      "fix": "Update In scope item 4 to say the GoldenNormalizer lands in Session 3 (or remove the session reference there) so the scope section matches Decision 4 and the Session 3/4 step lists."
    }
  ]
}
```

---

# Verification Round 5

I checked the specific three-way linkage you called out, and it now agrees cleanly:

- **Decision 4** says `GoldenNormalizer` **lands in Session 3**.
- **In scope item 4** now says `GoldenNormalizer` **lands in Session 3** and explains why.
- **Session 3 step 2** is where `GoldenNormalizer` is actually created.
- **Session 4 step 1** correctly treats it as the **Session-3** normalizer that the regression test reuses.

So the lifecycle is now internally consistent: **define in Session 3, use to produce committed corpus in Session 3, reuse in Session 4 regression**.

I do **not** see any remaining contradictory residue from the round-4 issue.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
