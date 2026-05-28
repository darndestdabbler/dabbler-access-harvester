using System.Collections.Generic;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>Top-level output container — serialized to object-use.json.</summary>
public sealed record ObjectUseFindings
{
    public string Version { get; init; } = "1.0";
    public required string Database { get; init; }
    public required string GeneratedAt { get; init; }
    public required IReadOnlyList<string> ExcludedObjects { get; init; }
    public required IReadOnlyList<UsageFinding> Objects { get; init; }
}

/// <summary>
/// Per-object usage classification finding. Field names match the
/// object-use-schema.json property names (camelCase for JSON serialization).
/// </summary>
public sealed record UsageFinding
{
    public required string ObjectType { get; init; }
    public required string ObjectName { get; init; }
    public required string Classification { get; init; }
    public required double Confidence { get; init; }
    public required string UsageSummary { get; init; }
    public required IReadOnlyList<SignalEntry> Signals { get; init; }
    public required IReadOnlyList<ClarifyingQuestion> Questions { get; init; }
}

/// <summary>One signal family's result for a given object.</summary>
public sealed record SignalEntry
{
    public required string SignalType { get; init; }
    public required bool Fired { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
}

/// <summary>One clarifying question generated for an uncertain object.</summary>
public sealed record ClarifyingQuestion
{
    public required string QuestionText { get; init; }
    public required string UncertaintySignal { get; init; }
}

/// <summary>
/// Usage category names matching the schema enum values.
/// </summary>
public static class UsageCategory
{
    public const string MainDataEntry    = "MainDataEntry";
    public const string LookupReference  = "LookupReference";
    public const string Reporting        = "Reporting";
    public const string NavigationShell  = "NavigationShell";
    public const string StandaloneUtility = "StandaloneUtility";
    public const string PossiblyUnused   = "PossiblyUnused";
    public const string Unknown          = "Unknown";
}

/// <summary>
/// Signal type names matching the schema enum values.
/// </summary>
public static class UsageSignalType
{
    public const string RecordSource         = "RecordSource";
    public const string RowSource            = "RowSource";
    public const string NavigationEdge       = "NavigationEdge";
    public const string VbaReference         = "VbaReference";
    public const string QueryTableReference  = "QueryTableReference";
    public const string FkRelationship       = "FkRelationship";
    public const string QueryType            = "QueryType";
    public const string ObjectNameConvention = "ObjectNameConvention";
}

/// <summary>
/// Uncertainty signal names matching the schema enum values.
/// </summary>
public static class UncertaintySignal
{
    public const string NoNavigationEntry      = "NoNavigationEntry";
    public const string NoRecordSource         = "NoRecordSource";
    public const string QueryReferencesNoTables = "QueryReferencesNoTables";
    public const string UnreferencedObject     = "UnreferencedObject";
    public const string LookupTablePattern     = "LookupTablePattern";
    public const string CrosstabQuery          = "CrosstabQuery";
    public const string UnrecognizedVbaPattern = "UnrecognizedVbaPattern";
    public const string CrypticObjectName      = "CrypticObjectName";
}
