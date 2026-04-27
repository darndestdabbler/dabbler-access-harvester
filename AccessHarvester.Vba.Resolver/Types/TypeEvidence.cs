using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Types;

// One piece of evidence the inference pass collected for a candidate
// declaration. Per session-2-type-inference.md §3.3, evidence is
// exposed so consumers (debug views, AI translation prompts) can pin
// the inference back to source without re-walking the AST.
//
// Stability: the three-property shape is a public contract. Future
// sessions may add fields but should not rename or reshape existing
// ones — Session 4 wiring and any downstream enrichment will read
// every field.
public sealed record TypeEvidence(
    TypeEvidenceKind Kind,
    // Canonical leaf type spelling. Compared via Declaration.NameComparer
    // (ordinal, case-insensitive) when grouping evidence for the
    // confidence vote.
    string TypeName,
    // Source range of the evidence site:
    //   TypeSuffix          -> the declaration's provenance range.
    //   Literal             -> the RHS literal's range.
    //   NewExpression       -> the New expression's range.
    //   *DeclarationType    -> the RHS reference site's range.
    //   *ReturnType         -> the call's range.
    SourceRange Location);
