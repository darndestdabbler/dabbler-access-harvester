namespace AccessHarvester.Vba.Parser.Ast;

// One parameter in a procedure / property / event / declare parameter
// list. Per ast-shape.md §4. PassMode is recorded as Unspecified when
// neither ByVal nor ByRef is in the source — VBA's default is ByRef
// per §4.6, but the AST records the spelling so a downstream linter
// can flag "implicit ByRef" without re-parsing.
public sealed record Parameter(
    Provenance Provenance,
    string Name,
    string? TypeSuffix,                                 // from TYPED_IDENTIFIER, else null
    ParameterPassMode PassMode,
    ParameterModifier Modifier,
    TypeReference? Type,                                // null = Variant
    bool IsArray,                                       // true if `()` present after the name
    Expression? DefaultValue)                           // only valid on Optional
    : VbaSyntaxNode(Provenance);

public enum ParameterPassMode { Unspecified, ByRef, ByVal }

public enum ParameterModifier { Required, Optional, ParamArray }
