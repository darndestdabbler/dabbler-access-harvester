namespace AccessHarvester.SymbolDb.Models;

// Row in the Parameters table. One per parameter in a procedure / property /
// declare statement signature.
public sealed record ParameterRow(
    string ProcedureFqn,
    int Position,
    string Name,
    string? DataType,
    string? Passing,
    bool IsOptional,
    bool IsParamArray,
    bool IsArray,
    string? DefaultValue);
