using System.Collections.Generic;

namespace AccessHarvester.Vba.Resolver.Declarations;

// Event declaration at module level (`Event Name(parameters)`). The
// parser-AST EventDeclaration is the syntax record; this is the
// resolver's semantic view, with reference tracking. RaiseEvent and
// WithEvents-handler resolution (#8) attach to instances of this.
//
// VBA disallows `Private Event` — Public is the only legal access
// level. The resolver still records IsDefaultedAccess for parity with
// the other declaration kinds (true when the source omitted Public).
public sealed class EventDeclaration : Declaration
{
    public AccessLevel Access { get; }
    public bool IsDefaultedAccess { get; }
    public IReadOnlyList<ParameterDeclaration> Parameters { get; }

    public EventDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        IReadOnlyList<ParameterDeclaration> parameters)
        : base(name, provenance)
    {
        Access = access;
        IsDefaultedAccess = isDefaultedAccess;
        Parameters = parameters;
    }
}
