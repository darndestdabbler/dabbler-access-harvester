using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.SymbolDb.Extraction;

// Maps the AST AccessModifier enum to the four-valued string column on
// Symbols.access. Unspecified is represented as NULL so consumers can
// distinguish "no modifier on the source" from explicit Public / Private.
public static class AccessLevelMapper
{
    public static string? ToColumnValue(AccessModifier modifier) => modifier switch
    {
        AccessModifier.Public => "Public",
        AccessModifier.Private => "Private",
        AccessModifier.Friend => "Friend",
        AccessModifier.Global => "Public",  // legacy: Global ≡ module-level Public
        AccessModifier.Dim => "Private",    // module-level Dim is Private per VBA spec
        _ => null,                           // Unspecified
    };
}
