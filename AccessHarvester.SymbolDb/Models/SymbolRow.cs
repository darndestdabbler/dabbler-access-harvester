namespace AccessHarvester.SymbolDb.Models;

// Row in the Symbols table. AI-interpretation columns are written by
// Session 4; only the structural and dead-code fields are populated by
// Session 1. EquivalenceCanonicalFqn is populated by the MD5 dedup pass.
public sealed record SymbolRow(
    string Fqn,
    string ModuleFqn,
    string Name,
    string Kind,
    string? Access,
    string? DataType,
    string? DeclaredInFqn,
    int? SourceLineStart,
    int? SourceLineEnd,
    string? SourceBody,
    string? SignatureString,
    string? BodyHashMd5,
    bool IsDead,
    string? DeadCodeReason,
    string? EquivalenceCanonicalFqn);
