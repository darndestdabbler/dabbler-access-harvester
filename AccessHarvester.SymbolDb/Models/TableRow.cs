namespace AccessHarvester.SymbolDb.Models;

public sealed record TableRow(
    string Fqn,
    string? PrimaryKeyColumn,
    string? CandidateLabelColumn,
    int? IsLookupTableScore,
    string? IsLookupTableReason);
