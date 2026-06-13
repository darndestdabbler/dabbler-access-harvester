using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.DefaultMembers;

// Resolution target for a bang-access default-member invocation
// (`rs!Notes` ≡ `rs.Fields("Notes")`). It carries the curated
// <see cref="DefaultMembers.DefaultMember"/> the bang invokes — e.g.
// Recordset's `Fields` — and the <see cref="AccessedName"/>, the bang key
// (`"Notes"`).
//
// Deliberately does NOT model `Notes` as a known field/member: without
// the recordset's source schema the only honest static claim is "this
// access binds through the type's default member, returning a Field." The
// resolver therefore resolves the *indirection*, not a fabricated symbol
// named after the bang key (project-guidance → "Unresolvable constructs
// are explicit, not silent": we resolve only what we can prove).
//
// Emitted via <see cref="References.ResolutionOutcome.ResolvedToObjectModel"/>
// so the deliberately-closed ResolutionOutcome set and the existing
// consumer machinery (projector, cross-reference index back-population)
// are reused with no breaking change. Added 011 Session 1 (F-3-4) after
// cross-provider design consensus (GPT-5.4 + Gemini Pro both chose this
// "synthetic declaration via the existing ObjectModel variant" option).
public sealed class SyntheticDefaultMemberDeclaration : Declaration
{
    // The curated default member the bang invokes (e.g. Recordset.Fields).
    public DefaultMember DefaultMember { get; }

    // The bang key as written in source (e.g. "Notes" in `rs!Notes`).
    // Carried for provenance only — never treated as a resolved symbol.
    public string AccessedName { get; }

    public SyntheticDefaultMemberDeclaration(DefaultMember defaultMember, string accessedName)
        : base(
            // The declaration's Name is the default member's name
            // (`Fields`), not the bang key — the binding target is the
            // default member, per the design consensus.
            name: defaultMember.MemberName,
            // Reuse the established synthetic provenance (Range is null —
            // a synthetic decl has no source location). The provenance is
            // internal only: it is never emitted to output and the
            // cross-reference index keys synthetic decls by identity.
            provenance: new SyntheticAccessDeclarationProvenance(defaultMember.TypeName))
    {
        DefaultMember = defaultMember;
        AccessedName = accessedName;
    }
}
