namespace AccessHarvester.SymbolDb.Models;

// Row in the Modules table. One per VBA module / class module / form
// code-behind / report code-behind in the harvested database.
public sealed record ModuleRow(
    string Fqn,
    string Kind,
    string SourcePath,
    string Encoding,
    bool IsDeferred,
    string? DeferredReason,
    string? DeferredCompanionFqn);
