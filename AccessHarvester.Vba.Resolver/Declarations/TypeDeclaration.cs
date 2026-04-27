using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// User-defined Type ... End Type. Distinct from the parser's syntax
// record AccessHarvester.Vba.Parser.Ast.TypeDeclaration; namespace
// separation disambiguates. This is the resolver's semantic view —
// each member is its own TypeMemberDeclaration, owned by an implicit
// "type-member scope" anchored on the UDT (Session 2 elaborates).
public sealed class UserDefinedTypeDeclaration : Declaration
{
    public AccessLevel Access { get; }
    public bool IsDefaultedAccess { get; }
    public IReadOnlyList<TypeMemberDeclaration> Members { get; }

    public UserDefinedTypeDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        IReadOnlyList<TypeMemberDeclaration> members)
        : base(name, provenance)
    {
        Access = access;
        IsDefaultedAccess = isDefaultedAccess;
        Members = members;
        foreach (var member in members)
        {
            member.SetContainingType(this);
        }
    }
}

// One field inside a UDT. Member-access reference resolution (#8)
// looks these up via the UDT scope; treating each as a Declaration
// gives the references-to-this-declaration tracking for free.
public sealed class TypeMemberDeclaration : Declaration
{
    public TypeReference? Type { get; }
    public bool IsArray { get; }
    public IReadOnlyList<ArrayBound> ArrayBounds { get; }

    // Stitched in by the UDT's constructor — TypeMembers are always
    // constructed before their owning UDT, so this back-pointer is
    // populated immediately after the UDT's `: base(...)` runs. Null
    // only during the brief window between member construction and
    // UDT construction; once the UDT exists, it never changes.
    public UserDefinedTypeDeclaration ContainingType { get; private set; } = null!;

    internal void SetContainingType(UserDefinedTypeDeclaration owner)
        => ContainingType = owner;

    public TypeMemberDeclaration(
        string name,
        DeclarationProvenance provenance,
        TypeReference? type,
        bool isArray,
        IReadOnlyList<ArrayBound> arrayBounds)
        : base(name, provenance)
    {
        Type = type;
        IsArray = isArray;
        ArrayBounds = arrayBounds;
    }
}
