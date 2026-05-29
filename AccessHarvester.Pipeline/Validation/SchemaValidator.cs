using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Json.Schema;

namespace AccessHarvester.Pipeline.Validation;

/// <summary>
/// Validates harvester output documents against harvester-schema.v1.1.json.
/// Hard-fails the caller on schema non-conformance; conformant documents pass silently.
/// </summary>
public sealed class SchemaValidator
{
    // Two-level cache strategy to prevent duplicate-registration errors in the
    // JsonSchema.Net global registry, which forbids re-registering the same $id.
    //
    // _pathCache  : file path → Lazy<string> (avoids re-reading the same file;
    //               OrdinalIgnoreCase is correct for Windows case-insensitive paths)
    // _schemaCache: schema $id URI (or content hash) → Lazy<JsonSchema> (ensures
    //               JsonSchema.FromText is called exactly once per unique schema;
    //               Ordinal because URI comparison is case-sensitive per RFC 3986)
    private static readonly ConcurrentDictionary<string, Lazy<string>> _pathCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> _schemaCache =
        new(StringComparer.Ordinal);

    private readonly JsonSchema _schema;

    public SchemaValidator(string schemaJsonPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaJsonPath);
        string fullPath = Path.GetFullPath(schemaJsonPath);
        string text = _pathCache.GetOrAdd(fullPath, p => new Lazy<string>(() =>
            File.ReadAllText(p, System.Text.Encoding.UTF8))).Value;
        _schema = GetOrCreate(text);
    }

    private SchemaValidator(JsonSchema schema) { _schema = schema; }

    /// <summary>
    /// Creates a <see cref="SchemaValidator"/> from an in-memory JSON Schema string.
    /// Use this when the schema is embedded as an assembly resource rather than on disk.
    /// </summary>
    public static SchemaValidator FromText(string schemaJson)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaJson);
        return new SchemaValidator(GetOrCreate(schemaJson));
    }

    // Ensures the schema is in _schemaCache and returns the shared JsonSchema instance.
    // Uses the schema's $id as the cache key so path-based and text-based loads of the
    // same schema content share one entry and JsonSchema.FromText is called exactly once.
    private static JsonSchema GetOrCreate(string schemaJson)
    {
        string key = ExtractId(schemaJson)
            ?? ("[hash]:" + Convert.ToHexString(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(schemaJson))));
        return _schemaCache.GetOrAdd(key, _ => new Lazy<JsonSchema>(() =>
            JsonSchema.FromText(schemaJson))).Value;
    }

    private static string? ExtractId(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            return doc.RootElement.TryGetProperty("$id", out var el) ? el.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Validates a JSON document string against the loaded schema.
    /// Returns a result containing validity and any error messages.
    /// </summary>
    public SchemaValidationResult Validate(string documentJson)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentJson);

        using var document = JsonDocument.Parse(documentJson);
        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        EvaluationResults results = _schema.Evaluate(document.RootElement, options);

        if (results.IsValid)
            return SchemaValidationResult.Valid();

        var errors = new List<string>();
        foreach (var detail in results.Details ?? [])
        {
            if (!detail.IsValid && detail.Errors is not null)
            {
                foreach (var (keyword, message) in detail.Errors)
                    errors.Add($"{detail.InstanceLocation}: [{keyword}] {message}");
            }
        }

        return SchemaValidationResult.Invalid(errors);
    }
}
