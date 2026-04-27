using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// One parameter on a Sub / Function / Property / Event / Declare. The
// owning procedure's ProcedureScope is the parameter's Scope back-pointer
// (assigned by the scope-construction visitor in Session 2). Parameters
// are case-insensitively matched against named arguments at call sites
// during reference resolution (#8).
public sealed class ParameterDeclaration : Declaration
{
    // Reuses the parser-AST enums verbatim — the resolver's notion of
    // pass-mode and parameter-modifier is the source spelling, not a
    // canonicalisation. (VBA's default is ByRef, but the resolver
    // preserves Unspecified so a downstream linter can flag implicit
    // ByRef without re-parsing.)
    public ParameterPassMode PassMode { get; }
    public ParameterModifier Modifier { get; }

    public TypeReference? Type { get; }

    // Type-suffix character from a TYPED_IDENTIFIER (e.g. '$', '%') if
    // the source used one; null otherwise.
    public string? TypeSuffix { get; }

    public bool IsArray { get; }

    // Only valid when Modifier == Optional.
    public Expression? DefaultValue { get; }

    public ParameterDeclaration(
        string name,
        DeclarationProvenance provenance,
        ParameterPassMode passMode,
        ParameterModifier modifier,
        TypeReference? type,
        string? typeSuffix,
        bool isArray,
        Expression? defaultValue)
        : base(name, provenance)
    {
        PassMode = passMode;
        Modifier = modifier;
        Type = type;
        TypeSuffix = typeSuffix;
        IsArray = isArray;
        DefaultValue = defaultValue;
    }
}
