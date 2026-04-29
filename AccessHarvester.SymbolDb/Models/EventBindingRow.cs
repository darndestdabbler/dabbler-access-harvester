namespace AccessHarvester.SymbolDb.Models;

public sealed record EventBindingRow(
    string FormFqn,
    string? ControlName,
    string EventName,
    string HandlerKind,      // 'vba', 'macro', 'expression'
    string? HandlerFqn,
    string? HandlerExpression);
