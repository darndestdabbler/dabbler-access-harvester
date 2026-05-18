using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Non-blocking harvester-level warning or informational message
/// recorded in <see cref="DatabaseManifest.Diagnostics"/>. Distinct
/// from <see cref="Enrichment.UnresolvedItem"/>: a diagnostic is
/// about the harvester itself (a COM call returned an unexpected
/// status, a fixture file was unreadable), not about an unresolved
/// VBA / SQL construct.
/// </summary>
public sealed record HarvesterDiagnostic
{
    public required HarvesterDiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }
}

/// <summary>
/// Closed severity set for <see cref="HarvesterDiagnostic.Severity"/>.
/// Matches the v1.0 schema's <c>HarvesterDiagnostic.Severity</c> enum.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HarvesterDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}
