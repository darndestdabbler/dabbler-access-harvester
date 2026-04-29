namespace AccessHarvester.SymbolDb.Models;

public sealed record MacroActionRow(
    string MacroFqn,
    int Position,
    string ActionName,
    string? ArgumentsJson);
