using System.Collections.Generic;

namespace AccessHarvester.Vba.Parser;

public sealed record ParseDiagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    int Line,
    int Column,
    int StartIndex,
    int StopIndex,
    string? OffendingTokenText = null,
    IReadOnlyList<string>? ExpectedTokens = null);
