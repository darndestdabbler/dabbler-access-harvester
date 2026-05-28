1. **ParameterOrigin mapping**

`FormReference -> FormControl` is the easy part: that is semantically sound.

`ReportReference -> FormControl` is acceptable only if the manifest schema is intentionally collapsing both form-bound and report-bound parameters into one broader “UI-bound control” bucket. It is lossy, but not misleading in the way `UserPrompt` would be. If downstream consumers care about exact provenance, this should eventually become a distinct value such as `ReportControl` or a more neutral `BoundControl`.

`Unknown -> UserPrompt` is the risky mapping. “Unknown” means the harvester could not classify the source confidently. “UserPrompt” means the runtime will ask the user for a value. Those are not equivalent. If you silently promote unknowns to `UserPrompt`, consumers will infer interactivity that may not exist, and you hide parser/normalization gaps. Unless the upstream abstraction explicitly uses `Unknown` as “prompt-like but unclassified,” I would not do that. Better options are: emit no origin, emit an explicit `Unknown` if the schema allows it, or fail/diagnose.

My recommendation: keep `FormReference -> FormControl`; allow `ReportReference -> FormControl` only as a documented normalization; change `Unknown -> UserPrompt` unless there is a hard upstream semantic guarantee.

2. **QueryType unknown values**

This should fail loudly, not emit verbatim, unless the schema is intentionally open-ended and consumers are documented to accept arbitrary strings.

In a projection layer like this, `QueryType` is usually a closed vocabulary. If an unexpected value gets through, that means one of three things happened: a new upstream enum value was introduced, a cast/default enum bug occurred, or the projector is missing support. Emitting it verbatim turns a projector bug into a downstream data-contract problem. It also makes failures less local and harder to diagnose.

The best behavior is a guarded switch with a throwing/default diagnostic path that includes query name and raw type. If you want forward compatibility, preserve the raw source type in a separate extension/debug field, but keep the schema-facing `queryType` restricted to known values.

My recommendation: fail loudly on unsupported/unknown `QueryType` values.

3. **SELECT * fallback when `Parsed.Select == null` and `ReturnShape == null`**

If both are null, the safe fallback is to omit the projected-column list, not invent one.

A null `Parsed.Select` may mean at least two different things: explicit `SELECT *`, or “projection not available from parse output.” If `ReturnShape` is also null, you do not have enough information to materialize a trustworthy explicit column list. Emitting an empty array would be wrong, because that means “returns nothing.” Emitting inferred columns would also be wrong, because there are none to infer. So from a truthfulness perspective, null/omission is the correct fallback.

The caveat is that this is lossy for explicit wildcard projection. Ideally the model should distinguish:
- explicit wildcard projection,
- explicit named projection,
- unknown/unavailable projection.

If the schema can represent wildcard directly, that is better. If not, omission is still safer than fabrication.

My recommendation: current null/omit fallback is correct from a safety standpoint, but the model should eventually gain an explicit wildcard representation if you need fidelity.

4. **`PredicateNode` serialization via `JsonSerializer.SerializeToNode(abstraction.Where)`**

This is only correct if two conditions are true:

- the JSON contract of `PredicateNode` exactly matches the manifest schema contract, and
- you are using the same serializer options/type metadata as the rest of manifest emission.

The biggest practical risk here is that `JsonNode` is materialized before final manifest serialization. That means any naming policy, ignore-default behavior, converters, or polymorphic configuration used later will not “fix up” the node afterward. If you call bare `SerializeToNode` with default options, you can easily end up with wrong property casing, wrong enum text, extra null/default fields, or incomplete derived-type serialization.

The second risk is polymorphism. If `abstraction.Where` is typed as a base `PredicateNode` but actually holds derived nodes, default serialization can drop subtype data unless polymorphism is explicitly configured.

So: this call is fine only if `PredicateNode` was explicitly designed to be serialized directly into manifest JSON and you pass the manifest serializer options/context. Otherwise it is fragile. Of the five concerns, this is the one I would inspect first even though tests pass.

My recommendation: do not rely on bare `SerializeToNode(abstraction.Where)`. Use shared manifest serializer options/type info at minimum, and prefer an explicit projector if the manifest contract differs at all from the CLR model.

5. **`IsSubquery` on joins emitted even when false**

If `false` is the default and the schema does not require the property, omitting it is usually better.

A default-valued boolean like `isSubquery: false` adds noise without adding information. In JSON manifests, absence naturally reads as “not special,” and most consumers deserializing into a bool property will get `false` anyway. Omitting default booleans also makes diffs smaller and aligns better with schemas where such flags are optional.

The only reasons to keep explicit `false` are:
- the property is required by schema,
- consumers rely on stable property presence,
- the manifest style intentionally favors explicitness over compactness.

One subtle point: if these join objects are being built as `JsonNode`s, any global “ignore default values” serializer setting will not remove `false` later. You must omit it at node construction time if that is the desired style.

My recommendation: omit `isSubquery` when false unless the schema or consumer contract requires explicit presence.

```json
{
  "session": "harvester-body-and-query-sql Session 2 D14",
  "overallVerdict": "Implementation looks solid, but three semantic areas should be tightened.",
  "concerns": [
    {
      "id": 1,
      "topic": "ParameterOrigin mapping",
      "verdict": "partial",
      "recommendation": {
        "FormReference": "FormControl",
        "ReportReference": "FormControl",
        "Unknown": "do_not_map_to_UserPrompt"
      },
      "priority": "high",
      "notes": [
        "FormReference -> FormControl is correct.",
        "ReportReference -> FormControl is acceptable only as documented normalization.",
        "Unknown should remain unknown/omitted/diagnosed unless upstream guarantees it means runtime prompt."
      ]
    },
    {
      "id": 2,
      "topic": "QueryType unknown values",
      "verdict": "fail_loudly",
      "recommendation": "Throw or emit a diagnostic on unsupported values rather than emitting verbatim.",
      "priority": "high",
      "notes": [
        "Projection layer should enforce schema vocabulary.",
        "If forward compatibility is needed, preserve raw type separately, not in the schema-facing field."
      ]
    },
    {
      "id": 3,
      "topic": "SELECT * fallback",
      "verdict": "safe_but_lossy",
      "recommendation": "If Parsed.Select and ReturnShape are both null, omit the select list rather than fabricating columns.",
      "priority": "medium",
      "notes": [
        "Null/omission is safer than an empty array.",
        "Longer term, add explicit wildcard representation if fidelity matters."
      ]
    },
    {
      "id": 4,
      "topic": "PredicateNode serialization",
      "verdict": "fragile_as_written",
      "recommendation": "Use manifest serializer options/type metadata explicitly, or project PredicateNode manually.",
      "priority": "highest",
      "notes": [
        "Bare SerializeToNode can lock in wrong casing/default handling.",
        "Polymorphic predicate nodes may lose subtype data without explicit configuration."
      ]
    },
    {
      "id": 5,
      "topic": "IsSubquery false booleans",
      "verdict": "prefer_omit_when_false",
      "recommendation": "Do not emit isSubquery when false unless schema/consumers require explicit presence.",
      "priority": "low",
      "notes": [
        "Absence naturally implies false for optional flags.",
        "If using JsonNode, omission must happen during node construction."
      ]
    }
  ],
  "priorityActions": [
    "Fix PredicateNode serialization to use the shared manifest JSON contract.",
    "Change QueryType handling to fail loudly on unsupported values.",
    "Stop mapping Unknown ParameterOrigin to UserPrompt unless that meaning is guaranteed upstream."
  ]
}
```