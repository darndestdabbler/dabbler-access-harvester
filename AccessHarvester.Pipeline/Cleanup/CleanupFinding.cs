using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>Top-level output for object-cleanup.json. Field names mirror the JSON schema.</summary>
public sealed record CleanupFindings
{
    public string Version { get; init; } = "1.0";
    public required string Database { get; init; }
    public required string GeneratedAt { get; init; }
    public required string HarvestDate { get; init; }
    public required string DateSignalSource { get; init; }
    public required int ThresholdDays { get; init; }
    public required IReadOnlyList<string> DegradationNotes { get; init; }
    public required IReadOnlyList<CleanupFinding> Findings { get; init; }
}

/// <summary>One flagged object with its fired signals and a confidence tier.</summary>
public sealed record CleanupFinding
{
    public required string ObjectType { get; init; }
    public required string ObjectName { get; init; }
    public required string ConfidenceTier { get; init; }
    public required string Explanation { get; init; }
    public required IReadOnlyList<CleanupSignal> FiredSignals { get; init; }
}

/// <summary>Discriminated base for the three signal variants in object-cleanup.json.</summary>
public abstract record CleanupSignal
{
    public abstract string SignalType { get; }
    public required string Summary { get; init; }
}

/// <summary>NamePatternSignal — see strategy doc §3.1.</summary>
public sealed record NamePatternSignal : CleanupSignal
{
    public override string SignalType => "NamePattern";
    public required string MatchedSubstring { get; init; }
    public required string Category { get; init; }
}

/// <summary>DateSignal — see strategy doc §3.2.</summary>
public sealed record DateSignal : CleanupSignal
{
    public override string SignalType => "DateSignal";
    public required string Source { get; init; }
    public required DateTimeOffset MaxValue { get; init; }
    public required int DaysSinceMax { get; init; }
    public required int ThresholdDays { get; init; }
}

/// <summary>CrossReferenceAbsenceSignal — see strategy doc §3.3.</summary>
public sealed record CrossReferenceAbsenceSignal : CleanupSignal
{
    public override string SignalType => "CrossReferenceAbsence";
    public required IReadOnlyList<string> CheckedReferenceTypes { get; init; }
}

/// <summary>Tier rank for stable sort order. High=3, Medium=2, Low=1.</summary>
public static class ConfidenceTiers
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public static int Rank(string tier) => tier switch
    {
        High => 3,
        Medium => 2,
        Low => 1,
        _ => 0,
    };
}
