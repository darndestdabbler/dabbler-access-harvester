namespace AccessHarvester.SymbolDb.Models;

public sealed record QueryRow(
    string Fqn,
    string SourceSql,
    string? NormalizedSql,
    string? SqlTextHash,
    string? SqlClauseHash,
    bool IsDead,
    string? EquivalenceCanonicalFqn);
