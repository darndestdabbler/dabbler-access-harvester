using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Serializes <see cref="SpotCheckManifest"/> to JSON and writes it to disk.
/// Uses the same serializer options as <see cref="BaselineManifestWriter"/> for
/// consistency, but this manifest lives in the non-gated
/// <c>reports-baseline-spot-check/</c> root and is NOT part of the determinism
/// gate's byte comparison — it indexes the optional human-visual PDFs.
/// </summary>
public static class SpotCheckManifestWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(SpotCheckManifest manifest)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        return JsonSerializer.Serialize(manifest, Options);
    }

    public static void WriteToFile(SpotCheckManifest manifest, string filePath)
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
