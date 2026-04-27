using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.DefaultMembers;

// One logical property paired across its (up to) three procedure
// declarations. VBA splits a property into independent
// `Property Get` / `Property Let` / `Property Set` procedures; the
// parser-AST and the `Declaration` model preserve them as separate
// `PropertyDeclaration`s ("Pairing is deferred to #9" — see
// DeclarationModelTests.PropertyDeclaration_DistinguishesGetLetSet).
// This class is the deferred pairing.
//
// A class (not a record) because each group is a unique entity in the
// resolver graph — equality is reference equality. Two groups with
// the same `Name` in different scopes must not coalesce.
//
// `Get`, `Let`, and `Set` are mutually optional: VBA permits read-only
// (Get only), write-only-value (Let only), and write-only-object (Set
// only) properties. Same-name `Let` AND `Set` is invalid VBA but the
// parser does not synthesise a diagnostic; the group records both
// rather than dropping data, leaving diagnostic emission to a later
// layer.
public sealed class PropertyGroup
{
    // The property's case-preserved name. The first declaration in
    // source order whose name matched (case-insensitively) supplies
    // the spelling.
    public string Name { get; }

    // The scope this group belongs to — the same scope as each of the
    // member procedures' Scope back-pointer. ModuleScope is the typical
    // case (per the spec's Standards: "Property triplet pairing lands
    // here too — the registry maps a triplet name to its three
    // procedure declarations").
    public IDeclarationScope Scope { get; }

    public PropertyDeclaration? Get { get; }
    public PropertyDeclaration? Let { get; }
    public PropertyDeclaration? Set { get; }

    public bool IsReadable => Get is not null;
    public bool IsWritableValue => Let is not null;
    public bool IsWritableObject => Set is not null;

    public PropertyGroup(
        string name,
        IDeclarationScope scope,
        PropertyDeclaration? get,
        PropertyDeclaration? let,
        PropertyDeclaration? set)
    {
        Name = name;
        Scope = scope;
        Get = get;
        Let = let;
        Set = set;
    }
}
