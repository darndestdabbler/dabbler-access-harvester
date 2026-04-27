using System.Collections.Generic;

namespace AccessHarvester.Vba.Resolver.Types;

// One inferred-type record for a candidate declaration. Produced by
// TypeInferencePass and stored in InferredTypeMap.
//
// Stability: this record's three-property shape is a public contract
// per session-2-type-inference.md §11. Session 4 (resolver wiring)
// reads every field; downstream enrichment may read the Evidence list
// for translation hints.
public sealed record InferredType(
    // Canonical leaf type spelling. Casing is preserved from whichever
    // evidence first contributed the dominant type; comparison should
    // use Declaration.NameComparer per VBA's ordinal-case-insensitive
    // semantics.
    string TypeName,
    InferenceConfidence Confidence,
    // Source-ordered list of every evidence row collected for the
    // declaration. Includes evidence whose TypeName differed from the
    // dominant choice — preserved verbatim so a consumer can see the
    // disagreement (especially relevant for Medium/Low confidence
    // results, and for the "TypeSuffix vs. assignment" conflict the
    // language otherwise leaves silent).
    IReadOnlyList<TypeEvidence> Evidence);
