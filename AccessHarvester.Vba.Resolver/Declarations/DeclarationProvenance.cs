using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// How a Declaration came into existence. Mirrors the explicit/implicit
// distinction the spec calls out under Feature 1 → Standards
// ("implicit declarations are represented explicitly, never silent").
//
// Source   — declared in source by an explicit construct (Sub, Function,
//            Dim, Const, Type, Enum, etc.). Range is the declarator's
//            source range.
// Implicit — synthesised at first use of an undeclared name in a module
//            that is not Option Explicit. Range is the source range of
//            that first use site.
public abstract record DeclarationProvenance
{
    public abstract SourceRange? Range { get; }
}

public sealed record SourceDeclarationProvenance(SourceRange Range) : DeclarationProvenance
{
    public override SourceRange? Range { get; } = Range;
}

public sealed record ImplicitDeclarationProvenance(SourceRange OriginatingUse) : DeclarationProvenance
{
    public override SourceRange? Range => OriginatingUse;
}
