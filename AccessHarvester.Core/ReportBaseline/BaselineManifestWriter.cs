using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Serializes <see cref="BaselineManifest"/> to canonical JSON and writes
/// it to disk. Output is deterministic — same input produces the same
/// bytes — so the file participates in the harvester's determinism gate
/// alongside <c>aggregates.json</c>. The PDF binaries written next to it
/// are excluded from byte-comparison the way <c>ExportedAt</c> is on the
/// other tiers (per the spec's Session 2 determinism note).
/// </summary>
public static class BaselineManifestWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(BaselineManifest manifest)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        return JsonSerializer.Serialize(manifest, Options);
    }

    public static void WriteToFile(BaselineManifest manifest, string filePath)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is empty.", nameof(filePath));

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, Serialize(manifest));
    }
}
