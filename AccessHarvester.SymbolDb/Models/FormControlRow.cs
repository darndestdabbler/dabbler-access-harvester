namespace AccessHarvester.SymbolDb.Models;

public sealed record FormControlRow(
    string FormFqn,
    string ControlName,
    string ControlType,
    string? LabelText,
    string? ControlSource,
    string? RowSource,
    string? BoundTableOrQuery,
    string? BoundColumn);
