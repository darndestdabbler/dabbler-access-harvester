using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Serializes <see cref="ObjectUseFindings"/> to disk as:
/// - <c>object-use.json</c> — the intermediate structured findings.
/// - <c>object-use-checklist.json</c> — the uat-checklist-editor-compatible checklist.
/// </summary>
public static class UsageChecklistWriter
{
    // object-use.json is the schema-aligned camelCase findings document.
    private static readonly JsonSerializerOptions _camelOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // object-use-checklist.json must be PascalCase so it loads in the
    // uat-checklist-editor (its schema requires {"ProjectName","Review":[...]}).
    // Mirrors CleanupChecklistWriter's _pascalOpts for object-cleanup-checklist.json.
    private static readonly JsonSerializerOptions _pascalOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // BOM-less UTF-8 (RFC 8259 §8.1 forbids a BOM on JSON). Mirrors the
    // sanitization writers' encoding so every output surface is BOM-free (F-2-3).
    private static readonly UTF8Encoding _utf8NoBom = new(false);

    /// <summary>Writes both output files adjacent to <paramref name="outputDir"/>.</summary>
    public static void Write(ObjectUseFindings findings, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        WriteObjectUse(findings, Path.Combine(outputDir, "object-use.json"));
        WriteChecklist(findings, Path.Combine(outputDir, "object-use-checklist.json"));
    }

    // ── object-use.json ────────────────────────────────────────────────────────

    private static void WriteObjectUse(ObjectUseFindings findings, string path)
    {
        var doc = new ObjectUseJsonDocument(
            findings.Version,
            findings.Database,
            findings.GeneratedAt,
            findings.ExcludedObjects,
            findings.Objects.Select(ToJsonFinding).ToArray());

        string json = JsonSerializer.Serialize(doc, _camelOpts);
        File.WriteAllText(path, json, _utf8NoBom);
    }

    private static ObjectUseFindingJson ToJsonFinding(UsageFinding f)
        => new(f.ObjectType, f.ObjectName, f.Classification, f.Confidence, f.UsageSummary,
               f.Signals.Select(s => new SignalJson(s.SignalType, s.Fired, s.Evidence.ToArray())).ToArray(),
               f.Questions.Select(q => new QuestionJson(q.QuestionText, q.UncertaintySignal)).ToArray());

    // ── object-use-checklist.json ──────────────────────────────────────────────

    internal static ChecklistDocument BuildChecklist(ObjectUseFindings findings)
    {
        var reviews = new List<ChecklistReview>();
        foreach (var f in findings.Objects)
        {
            var steps = f.Questions.Select((q, i) => new ChecklistStep(
                StepNumber: i + 1,
                Action: q.QuestionText,
                Verifications: []
            )).ToArray();

            reviews.Add(new ChecklistReview(
                FunctionalArea: f.ObjectType,
                Subarea: f.ObjectName,
                ItemLabel: f.UsageSummary,
                Steps: steps,
                IsJudgmentItem: true,
                Passes: false,
                Feedback: ""
            ));
        }

        return new ChecklistDocument(
            ProjectName: $"{findings.Database} — Object Use Checklist",
            Review: reviews.ToArray());
    }

    private static void WriteChecklist(ObjectUseFindings findings, string path)
    {
        var doc = BuildChecklist(findings);
        string json = JsonSerializer.Serialize(doc, _pascalOpts);
        File.WriteAllText(path, json, _utf8NoBom);
    }

    // ── JSON shapes ────────────────────────────────────────────────────────────

    private sealed record ObjectUseJsonDocument(
        string Version,
        string Database,
        string GeneratedAt,
        IReadOnlyList<string> ExcludedObjects,
        ObjectUseFindingJson[] Objects);

    private sealed record ObjectUseFindingJson(
        string ObjectType,
        string ObjectName,
        string Classification,
        double Confidence,
        string UsageSummary,
        SignalJson[] Signals,
        QuestionJson[] Questions);

    private sealed record SignalJson(
        string SignalType,
        bool Fired,
        string[] Evidence);

    private sealed record QuestionJson(
        string QuestionText,
        string UncertaintySignal);

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
