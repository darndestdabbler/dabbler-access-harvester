namespace AccessHarvester.SymbolDb.Models;

public sealed record MacroRow(
    string Fqn,
    string SourceXml,
    bool IsDead);
