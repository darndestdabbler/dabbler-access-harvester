using System.Collections.Generic;
using AccessHarvester.SymbolDb.Models;

namespace AccessHarvester.SymbolDb.Extraction;

// Output of SymbolDbBuilder + Session 3 extractors. Aggregated by table;
// the persistence layer writes each list independently.
public sealed record BuildResult(
    IReadOnlyList<ModuleRow> Modules,
    IReadOnlyList<SymbolRow> Symbols,
    IReadOnlyList<ParameterRow> Parameters,
    IReadOnlyList<ReferenceRow> References,
    IReadOnlyList<MemberAccessRow> MemberAccesses,
    BuildStatistics Statistics,
    IReadOnlyList<FormControlRow>? FormControls = null,
    IReadOnlyList<EventBindingRow>? EventBindings = null,
    IReadOnlyList<MacroRow>? Macros = null,
    IReadOnlyList<MacroActionRow>? MacroActions = null,
    IReadOnlyList<QueryRow>? Queries = null,
    IReadOnlyList<TableRow>? Tables = null);

public sealed record BuildStatistics(
    int ModuleCount,
    int DeferredModuleCount,
    int SymbolCount,
    int DeadSymbolCount,
    int ProcedureCount,
    int FunctionCount,
    int PropertyCount,
    int ModuleVarCount,
    int EnumDeclCount,
    int EnumValueCount);
