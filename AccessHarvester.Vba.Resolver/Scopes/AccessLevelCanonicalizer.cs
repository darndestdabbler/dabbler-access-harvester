using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.Scopes;

// Maps the parser AST's source-spelled AccessModifier (Unspecified |
// Public | Private | Friend | Global | Dim) onto the resolver's
// canonicalised AccessLevel (Public | Private | Friend), per the rules
// in Declarations/AccessLevel.cs:
//
//   Unspecified | Dim → Private (module-level default).
//   Global            → Public  (legacy synonym, source-spelled).
//   Public / Private / Friend pass through.
//
// The split between ForModuleMember and ForProcedure exists because
// VBA's "default access level when the keyword is omitted" differs by
// declaration kind: variables/types/enums/declares/events default to
// Private at module level, while Sub/Function/Property procedures
// default to Public. The IsDefaulted return value records whether the
// returned level came from the source's own keyword (false) or from
// the resolver's inference (true).
internal static class AccessLevelCanonicalizer
{
    public static (AccessLevel Level, bool IsDefaulted) ForModuleMember(AccessModifier source)
    {
        return source switch
        {
            AccessModifier.Public      => (AccessLevel.Public,  false),
            AccessModifier.Private     => (AccessLevel.Private, false),
            AccessModifier.Friend      => (AccessLevel.Friend,  false),
            AccessModifier.Global      => (AccessLevel.Public,  false),
            AccessModifier.Dim         => (AccessLevel.Private, false),
            AccessModifier.Unspecified => (AccessLevel.Private, true),
            _                          => (AccessLevel.Private, true),
        };
    }

    public static (AccessLevel Level, bool IsDefaulted) ForProcedure(AccessModifier source)
    {
        return source switch
        {
            AccessModifier.Public      => (AccessLevel.Public,  false),
            AccessModifier.Private     => (AccessLevel.Private, false),
            AccessModifier.Friend      => (AccessLevel.Friend,  false),
            AccessModifier.Global      => (AccessLevel.Public,  false),
            AccessModifier.Dim         => (AccessLevel.Private, false),
            AccessModifier.Unspecified => (AccessLevel.Public,  true),
            _                          => (AccessLevel.Public,  true),
        };
    }
}
