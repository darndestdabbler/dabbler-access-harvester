using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.Scopes;

namespace AccessHarvester.SymbolDb.Extraction;

// Derives Symbols-table FQNs from resolver scope and declaration objects.
// The FQN scheme mirrors what SymbolDbBuilder wrote in Session 1:
//   - procedures:   <ModuleName>.<ProcName>  (plus #get/#let/#set for properties)
//   - module decls: <ModuleName>.<Name>
//   - local decls:  <ModuleName>.<ProcName>.<LocalName>
internal static class FqnResolver
{
    // Returns the FQN of the innermost containing procedure (or module when
    // the reference is at module level). Walks from the given scope up through
    // ForEachScope / WithBlockScope parents until a ProcedureScope or
    // ModuleScope is reached.
    internal static string ContainingFqn(Scope scope)
    {
        Scope? current = scope;
        while (current is not null)
        {
            if (current is ProcedureScope ps)
                return ProcedureFqn(ps);
            if (current is ModuleScope ms)
                return ms.ModuleName;
            current = current.Parent;
        }
        // Defensive — scope chain always bottoms out at ModuleScope above ProjectScope.
        return "<unknown>";
    }

    // Returns the FQN of a Declaration as stored in the Symbols table, or null
    // when the declaration cannot be mapped (no Scope back-pointer, synthetic
    // object-model declarations that don't appear in Symbols, etc.).
    internal static string? DeclarationFqn(Declaration? declaration)
    {
        if (declaration is null) return null;

        var scope = declaration.Scope;

        // Local variable or parameter — declared directly in a ProcedureScope.
        // LocalVariableExtractor emits these as <ModuleName>.<ProcFqn>.<LocalName>
        // (i.e., three-part FQN), so we must include the procedure segment.
        if (scope is ProcedureScope ps)
            return $"{ProcedureFqn(ps)}.{declaration.Name}";

        // Module-level declaration (procedure, variable, constant, enum, etc.).
        string? moduleName = ModuleNameFromScope(scope);
        if (moduleName is null) return null;

        return declaration switch
        {
            PropertyDeclaration pd =>
                $"{moduleName}.{pd.Name}#{pd.Kind.ToString().ToLowerInvariant()}",
            _ => $"{moduleName}.{declaration.Name}",
        };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers

    private static string ProcedureFqn(ProcedureScope ps)
    {
        // ProcedureScope.Parent is always ModuleScope.
        string moduleName = ps.Parent is ModuleScope ms ? ms.ModuleName : "<unknown>";
        return ps.Procedure switch
        {
            PropertyDeclaration pd =>
                $"{moduleName}.{pd.Name}#{pd.Kind.ToString().ToLowerInvariant()}",
            _ => $"{moduleName}.{ps.Procedure.Name}",
        };
    }

    private static string? ModuleNameFromScope(IDeclarationScope? scope)
    {
        if (scope is ModuleScope ms) return ms.ModuleName;
        if (scope is ProcedureScope ps)
            return ps.Parent is ModuleScope pms ? pms.ModuleName : null;
        return null;
    }
}
