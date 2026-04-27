using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.AccessIntegration;

// Provenance for declarations synthesised from Access design metadata
// (forms, reports, controls). Carries the artifact name (e.g. the form
// name a control belongs to) so downstream consumers can attribute the
// synthetic decl to its source artifact per the spec's
// Feature 1 → Standards: "synthetic declarations carry provenance —
// { source: 'synthetic-from-access', artifact: '<form-name>' }".
//
// Range is always null — synthetic decls have no source location in
// any VBA module.
public sealed record SyntheticAccessDeclarationProvenance(string Artifact)
    : DeclarationProvenance
{
    public override SourceRange? Range => null;

    // Stable provenance discriminator string callers can serialize
    // without reflecting on the type name.
    public string Source => "synthetic-from-access";
}
