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
    IReadOnlyList<TableRow>? Tables = null,
    // FQNs of non-deferred modules whose Pass-2 parse/scope-build threw and
    // were degraded (kept their Modules row, contributed no symbols) rather
    // than failing the whole build. Empty on a clean build. Makes the
    // per-module catch's loss explicit, not silent (006 review F-3-2).
    IReadOnlyList<string>? ParseFailedModuleFqns = null);

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
