using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// Enum declaration (Enum Foo ... End Enum). Distinct from the parser's
// Parser.Ast.EnumDeclaration syntax record. Each member is its own
// declaration so reference resolution (#8) can attach use sites to
// individual enum constants.
public sealed class EnumDeclaration : Declaration
{
    public AccessLevel Access { get; }
    public bool IsDefaultedAccess { get; }
    public IReadOnlyList<EnumMemberDeclaration> Members { get; }

    public EnumDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        IReadOnlyList<EnumMemberDeclaration> members)
        : base(name, provenance)
    {
        Access = access;
        IsDefaultedAccess = isDefaultedAccess;
        Members = members;
        foreach (var member in members)
        {
            member.SetContainingEnum(this);
        }
    }
}

public sealed class EnumMemberDeclaration : Declaration
{
    // Null when the source did not give an explicit value (the
    // member's value is then auto-incremented from the previous
    // member, or 0 if it is the first). #9 evaluates the chain.
    public Expression? Value { get; }

    public EnumDeclaration ContainingEnum { get; private set; } = null!;

    internal void SetContainingEnum(EnumDeclaration owner)
        => ContainingEnum = owner;

    public EnumMemberDeclaration(
        string name,
        DeclarationProvenance provenance,
        Expression? value)
        : base(name, provenance)
    {
        Value = value;
    }
}
