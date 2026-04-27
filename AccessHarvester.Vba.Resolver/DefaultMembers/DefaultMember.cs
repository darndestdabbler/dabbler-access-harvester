namespace AccessHarvester.Vba.Resolver.DefaultMembers;

// One row in the curated default-member registry. Mirrors the shape of
// `References.BuiltInFunction`: a small immutable record with the
// minimum the resolver needs at lookup time. Adding a new type to the
// registry is a row add, not a code edit, per the spec's standard
// "the registry is data, not code branching."
//
// Stability: this shape is a public contract per
// docs/session-sets/resolver-default-members-and-inference/synthesis/
// session-1-default-member-registry.md §3. Session 2 (type inference)
// reads `ReturnType` for chaining; Session 4 (resolver wiring) reads
// every field. Future sessions may *extend* the record but should not
// *reshape* without coordinating with consumers.
public sealed record DefaultMember(
    // Canonical type spelling. Matched against the LAST segment of a
    // TypeReference.QualifiedName (so `["DAO","Recordset"]` and
    // `["Recordset"]` both hit the same row — see §5.1 of the synthesis
    // doc for the leaf-only-match rationale).
    string TypeName,
    // The implicit member's name: `.Value`, `.Fields`, `.Item`, `.Form`,
    // `.Report`, etc.
    string MemberName,
    DefaultMemberKind Kind,
    // Qualified-name leaf of the resolved return type, used by
    // Session 2 type inference to chain (`rs("X")` -> `Field` ->
    // `"Variant"`). Two distinct null-vs-string semantics:
    //   null         = unknown / polymorphic. Session 2 treats this as
    //                  "type is genuinely indeterminate at this layer"
    //                  and stops chaining.
    //   "Variant"    = the call returns a VBA Variant explicitly. Still
    //                  a known type — Session 2 may chain through if a
    //                  Variant-default-member entry exists.
    // Don't conflate the two: nulls mean "we don't know"; "Variant"
    // means "we know it's Variant."
    string? ReturnType,
    // True when the canonical call form takes an index argument
    // (`rs("X")`, `coll(i)`); false for nullary defaults like `.Value`,
    // `.Form`, `.Report`. Read this flag — do not infer
    // parameterisation from MemberName, since a hypothetical future
    // `Item` overload could go either way.
    bool IsParameterized);
