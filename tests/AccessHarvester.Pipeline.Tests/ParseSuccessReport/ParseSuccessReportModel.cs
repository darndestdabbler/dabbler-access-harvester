using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Serialisation POCO records for the per-fixture parse-success report
// at samples/parse-success-reports/<fixture>.parse-success-report.json.
//
// Property order is fixed via [JsonPropertyOrder] so the generated JSON
// is byte-stable across runs. Field semantics + lineage:
//   * `summary.*` — per-source coverage counts produced by walking the
//     YAML mirror and invoking the existing parser entry points (no
//     parser changes; counts only).
//   * `unresolvedItems` — for the Session 1 first-pass, populated with
//     zero items and a note. The full UnresolvedItemAggregator output
//     requires the live enrichment pipeline (resolver + dynamic-SQL
//     analyzer + navigation graph + function classifier) which is
//     downstream of this set; `harvester-finish-canonical-golden-output`
//     wires that in.
public sealed record ParseSuccessReport
{
    [JsonPropertyOrder(0)] public required string Fixture { get; init; }
    [JsonPropertyOrder(1)] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyOrder(2)] public required string Generator { get; init; }
    [JsonPropertyOrder(3)] public required string YamlMirror { get; init; }
    [JsonPropertyOrder(4)] public required ParseSuccessSummary Summary { get; init; }
    [JsonPropertyOrder(5)] public required UnresolvedItemsSection UnresolvedItems { get; init; }
}

public sealed record ParseSuccessSummary
{
    [JsonPropertyOrder(0)] public required VbaModuleCounts VbaModules { get; init; }
    [JsonPropertyOrder(1)] public required JetSqlQueryCounts JetSqlQueries { get; init; }
    [JsonPropertyOrder(2)] public required MacroCounts Macros { get; init; }
    [JsonPropertyOrder(3)] public required PropertySheetCounts PropertySheet { get; init; }
}

public sealed record VbaModuleCounts
{
    [JsonPropertyOrder(0)] public int TotalModules { get; init; }
    [JsonPropertyOrder(1)] public int TotalProcedures { get; init; }
    [JsonPropertyOrder(2)] public int ParseOk { get; init; }
    [JsonPropertyOrder(3)] public int ParseFailed { get; init; }
    [JsonPropertyOrder(4)] public int ErrorTokenCount { get; init; }
}

public sealed record JetSqlQueryCounts
{
    [JsonPropertyOrder(0)] public int TotalQueries { get; init; }
    [JsonPropertyOrder(1)] public int ParseOk { get; init; }
    [JsonPropertyOrder(2)] public int ParseFailed { get; init; }
}

public sealed record MacroCounts
{
    [JsonPropertyOrder(0)] public int TotalMacroObjects { get; init; }
    [JsonPropertyOrder(1)] public int TotalMacroEntries { get; init; }

    // Structural count of `Action ="..."` lines in the SaveAsText
    // macro definition body. NOT a parser-coverage measurement — the
    // harvester's macro action enricher runs as a live-pipeline
    // enrichment step that requires the full extraction pass, and is
    // wired into the report by `harvester-finish-canonical-golden-output`.
    [JsonPropertyOrder(2)] public int ActionLines { get; init; }
}

public sealed record PropertySheetCounts
{
    [JsonPropertyOrder(0)] public required PropertyBucketCounts ValidationRules { get; init; }
    [JsonPropertyOrder(1)] public required PropertyBucketCounts DefaultValues { get; init; }
    [JsonPropertyOrder(2)] public required PropertyBucketCounts FormFilters { get; init; }
    [JsonPropertyOrder(3)] public required PropertyBucketCounts FormOrderBys { get; init; }
}

public sealed record PropertyBucketCounts
{
    [JsonPropertyOrder(0)] public int Total { get; init; }
    [JsonPropertyOrder(1)] public int ParsedOk { get; init; }
    [JsonPropertyOrder(2)] public int ParseFailed { get; init; }
}

public sealed record UnresolvedItemsSection
{
    // "not-measured" in the Session 1 first-pass; flipped to
    // "measured" by the set that wires the live aggregator output.
    // A literal status string is unambiguous in the JSON; a null
    // count would still risk a downstream consumer treating absence
    // as zero.
    [JsonPropertyOrder(0)] public required string Status { get; init; }

    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; init; }

    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<UnresolvedReasonGroup>? BySourceAndReason { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }
}

public sealed record UnresolvedReasonGroup
{
    [JsonPropertyOrder(0)] public required string Source { get; init; }
    [JsonPropertyOrder(1)] public required string Reason { get; init; }
    [JsonPropertyOrder(2)] public required int Count { get; init; }

    [JsonPropertyOrder(3)]
    public required IReadOnlyList<UnresolvedExemplarLocation> ExemplaryLocations { get; init; }
}

public sealed record UnresolvedExemplarLocation
{
    [JsonPropertyOrder(0)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModuleName { get; init; }

    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcedureName { get; init; }

    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Column { get; init; }

    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SurroundingText { get; init; }
}
