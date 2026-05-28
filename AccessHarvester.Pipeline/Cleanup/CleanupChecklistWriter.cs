using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>Writes object-cleanup.json (camelCase, schema-aligned) and object-cleanup-checklist.json (PascalCase, uat-checklist-editor).</summary>
public static class CleanupChecklistWriter
{
    private static readonly JsonSerializerOptions _camelOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions _pascalOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes both output files into the given directory.</summary>
    public static void Write(CleanupFindings findings, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        WriteCleanup(findings, Path.Combine(outputDir, "object-cleanup.json"));
        WriteChecklist(findings, Path.Combine(outputDir, "object-cleanup-checklist.json"));
    }

    // ── object-cleanup.json ────────────────────────────────────────────────────

    private static void WriteCleanup(CleanupFindings findings, string path)
    {
        var doc = new CleanupJsonDocument(
            findings.Version,
            findings.Database,
            findings.GeneratedAt,
            findings.HarvestDate,
            findings.DateSignalSource,
            findings.ThresholdDays,
            findings.DegradationNotes.ToArray(),
            findings.Findings.Select(ToJsonFinding).ToArray());
        File.WriteAllText(path, JsonSerializer.Serialize(doc, _camelOpts), System.Text.Encoding.UTF8);
    }

    private static CleanupFindingJson ToJsonFinding(CleanupFinding f)
        => new(f.ObjectType, f.ObjectName, f.ConfidenceTier, f.Explanation,
               f.FiredSignals.Select(ToJsonSignal).ToArray());

    private static SignalJson ToJsonSignal(CleanupSignal s) => s switch
    {
        NamePatternSignal n => new SignalJson(
            SignalType: "NamePattern",
            Summary: n.Summary,
            MatchedSubstring: n.MatchedSubstring,
            Category: n.Category,
            Source: null,
            MaxValue: null,
            DaysSinceMax: null,
            ThresholdDays: null,
            CheckedReferenceTypes: null),
        DateSignal d => new SignalJson(
            SignalType: "DateSignal",
            Summary: d.Summary,
            MatchedSubstring: null,
            Category: null,
            Source: d.Source,
            MaxValue: d.MaxValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture)
                .Replace("+00:00", "Z"),
            DaysSinceMax: d.DaysSinceMax,
            ThresholdDays: d.ThresholdDays,
            CheckedReferenceTypes: null),
        CrossReferenceAbsenceSignal c => new SignalJson(
            SignalType: "CrossReferenceAbsence",
            Summary: c.Summary,
            MatchedSubstring: null,
            Category: null,
            Source: null,
            MaxValue: null,
            DaysSinceMax: null,
            ThresholdDays: null,
            CheckedReferenceTypes: c.CheckedReferenceTypes.ToArray()),
        _ => throw new InvalidOperationException($"Unknown signal type: {s.GetType().Name}"),
    };

    // ── object-cleanup-checklist.json ───────────────────────────────────────────

    internal static ChecklistDocument BuildChecklist(CleanupFindings findings)
    {
        var reviews = findings.Findings.Select(f => new ChecklistReview(
            FunctionalArea: f.ObjectType,
            Subarea: f.ObjectName,
            ItemLabel: $"[{f.ConfidenceTier}] {f.Explanation}",
            Steps:
            [
                new ChecklistStep(
                    StepNumber: 1,
                    Action: "Review with user: is this object still needed, or can it be excluded from downstream analysis?",
                    Verifications: [])
            ],
            IsJudgmentItem: true,
            Passes: false,
            Feedback: "")
        ).ToArray();

        return new ChecklistDocument(
            ProjectName: $"{findings.Database} — Object Cleanup Checklist",
            Review: reviews);
    }

    private static void WriteChecklist(CleanupFindings findings, string path)
    {
        var doc = BuildChecklist(findings);
        File.WriteAllText(path, JsonSerializer.Serialize(doc, _pascalOpts), System.Text.Encoding.UTF8);
    }

    // ── JSON shapes ────────────────────────────────────────────────────────────

    private sealed record CleanupJsonDocument(
        string Version,
        string Database,
        string GeneratedAt,
        string HarvestDate,
        string DateSignalSource,
        int ThresholdDays,
        string[] DegradationNotes,
        CleanupFindingJson[] Findings);

    private sealed record CleanupFindingJson(
        string ObjectType,
        string ObjectName,
        string ConfidenceTier,
        string Explanation,
        SignalJson[] FiredSignals);

    private sealed record SignalJson(
        string SignalType,
        string Summary,
        string? MatchedSubstring,
        string? Category,
        string? Source,
        string? MaxValue,
        int? DaysSinceMax,
        int? ThresholdDays,
        string[]? CheckedReferenceTypes);

    internal sealed record ChecklistDocument(
        string ProjectName,
        ChecklistReview[] Review);

    internal sealed record ChecklistReview(
        string FunctionalArea,
        string Subarea,
        string ItemLabel,
        ChecklistStep[] Steps,
        bool IsJudgmentItem,
        bool Passes,
        string Feedback);

    internal sealed record ChecklistStep(
        int StepNumber,
        string Action,
        string[] Verifications);
}
