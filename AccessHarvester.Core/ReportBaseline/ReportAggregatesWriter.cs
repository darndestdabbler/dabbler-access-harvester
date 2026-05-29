using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Serializes <see cref="ReportAggregates"/> to canonical JSON and writes
/// it to disk. Output is deterministic — same input produces the same
/// bytes — so the file participates in the harvester's determinism gate
/// alongside the other emitted artifacts.
/// </summary>
public static class ReportAggregatesWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Serialize <paramref name="aggregates"/> to a canonical JSON string.
    /// </summary>
    public static string Serialize(ReportAggregates aggregates)
    {
        if (aggregates is null) throw new ArgumentNullException(nameof(aggregates));
        return JsonSerializer.Serialize(aggregates, Options);
    }

    /// <summary>
    /// Serialize <paramref name="aggregates"/> to
    /// <paramref name="filePath"/>, creating intermediate directories as
    /// needed. Overwrites any existing file.
    /// </summary>
    public static void WriteToFile(ReportAggregates aggregates, string filePath)
    {
        if (aggregates is null) throw new ArgumentNullException(nameof(aggregates));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is empty.", nameof(filePath));

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, Serialize(aggregates));
    }
}
