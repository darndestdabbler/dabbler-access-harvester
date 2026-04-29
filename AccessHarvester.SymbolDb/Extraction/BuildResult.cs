using System.Collections.Generic;
using AccessHarvester.SymbolDb.Models;

namespace AccessHarvester.SymbolDb.Extraction;

// Output of SymbolDbBuilder. Aggregated by table; the persistence layer
// writes each list independently. MD5-equivalence canonicalisation is a
// post-write SQL pass and is therefore not represented here.
public sealed record BuildResult(
    IReadOnlyList<ModuleRow> Modules,
    IReadOnlyList<SymbolRow> Symbols,
    IReadOnlyList<ParameterRow> Parameters,
    IReadOnlyList<ReferenceRow> References,
    IReadOnlyList<MemberAccessRow> MemberAccesses,
    BuildStatistics Statistics);

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
