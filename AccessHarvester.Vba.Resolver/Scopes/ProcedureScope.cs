using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Scopes;

// One per Sub / Function / Property procedure body. The ProcedureScope
// is always a direct child of the procedure's owning ModuleScope;
// nested With and ForEach scopes appear as descendants. Parameters are
// declared in the ProcedureScope (Declarations list); locals from
// Dim / Static / Const inside the body are declared here too — VBA
// has no block-scoped locals, so a Dim inside a With or a ForEach
// still belongs to the enclosing ProcedureScope.
//
// Declares and Events have parameters but no body. They produce no
// ProcedureScope; their parameters are declared on the owning
// ModuleScope's Declarations list (and the parameters' Scope back-
// pointer points at the ModuleScope). This keeps "scope owns a body"
// invariant — there is nowhere inside a Declare or Event for a
// reference to resolve to.
public sealed class ProcedureScope : Scope
{
    // The semantic procedure declaration this scope hosts. Matches one
    // of: SubDeclaration, FunctionDeclaration, PropertyDeclaration.
    // DeclareDeclaration does not appear here — see the type comment
    // above.
    public Declarations.ProcedureDeclaration Procedure { get; }

    public ProcedureScope(
        ModuleScope parent,
        Declarations.ProcedureDeclaration procedure,
        SourceRange? range)
        : base(parent, range)
    {
        Procedure = procedure;
    }
}
