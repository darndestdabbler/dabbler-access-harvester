using System;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.Scopes;

namespace AccessHarvester.SymbolDb.Extraction;

// Conservative Session 1 entry-point detection. The full version (Session 2-3
// after EventBindings + NavigationGraph land) joins against those tables;
// this one uses naming-shape heuristics that match the canonical Access
// patterns:
//
//   * In a Form / Report module, any procedure whose name contains an
//     underscore is treated as a candidate event handler. Access-generated
//     handlers always follow the '<ControlName>_<EventName>' or
//     'Form_<EventName>' / 'Report_<EventName>' shape.
//   * The special procedure name 'AutoExec' in any module is a database
//     startup hook.
//   * Public procedures in any module kind are conservatively treated as
//     alive: they may be reached via macros (RunCode), DoCmd.OpenForm
//     'OpenArgs', or external host code that the symbol DB does not yet
//     index. Session 2-3 tighten this once macro and navigation tables are
//     populated.
//   * Private procedures with no callers in CrossReferenceIndex AND no
//     entry-point shape are dead.
public static class EntryPointHeuristic
{
    public static bool IsEntryPoint(
        ProcedureDeclaration procedure,
        ModuleScope moduleScope)
    {
        if (string.Equals(procedure.Name, "AutoExec", StringComparison.OrdinalIgnoreCase))
            return true;

        bool isFormOrReport =
            moduleScope.Kind == ModuleKind.Form || moduleScope.Kind == ModuleKind.Report;

        if (isFormOrReport && procedure.Name.Contains('_'))
            return true;

        // Public procedures (or Friend, or unspecified-public-by-default) are
        // possibly callable from outside the resolver's view. Treat them as
        // alive until later sessions surface evidence to the contrary.
        if (procedure.Access == AccessLevel.Public || procedure.Access == AccessLevel.Friend)
            return true;

        return false;
    }
}
