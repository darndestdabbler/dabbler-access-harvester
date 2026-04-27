namespace AccessHarvester.Vba.Resolver.Declarations;

// Canonicalised access level for module-level declarations. The
// parser AST exposes the source spelling (Parser.Ast.AccessModifier
// includes Unspecified / Dim / Global) so the source can be
// round-tripped. The resolver collapses synonyms:
//   Unspecified | Dim   → Private (VBA's module-level default).
//   Global              → Public  (legacy synonym).
//   Public / Private / Friend pass through.
// Each module-level declaration also carries IsDefaultedAccess so
// downstream consumers can tell whether the source spelled the
// modifier or whether the resolver inferred Private.
public enum AccessLevel
{
    Public,
    Private,
    Friend,
}
