using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Json.Schema;

namespace AccessHarvester.Pipeline.Validation;

/// <summary>
/// Validates harvester output documents against harvester-schema.v1.0.json.
/// Hard-fails the caller on schema non-conformance; conformant documents pass silently.
/// </summary>
public sealed class SchemaValidator
{
    // JsonSchema.FromText registers the schema globally by its $id on first load.
    // Cache by absolute path to avoid duplicate-registration errors when multiple
    // instances are created with the same schema file.
    //
    // Lazy<T> prevents the race in ConcurrentDictionary.GetOrAdd where the factory
    // can be called multiple times concurrently: only the first Lazy<T> wins the
    // slot, and its Value factory runs exactly once even under concurrent access.
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly JsonSchema _schema;

    public SchemaValidator(string schemaJsonPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaJsonPath);
        string fullPath = Path.GetFullPath(schemaJsonPath);
        _schema = _cache.GetOrAdd(fullPath, path => new Lazy<JsonSchema>(() =>
        {
            var schemaText = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonSchema.FromText(schemaText);
        })).Value;
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
        foreach (var detail in results.Details)
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
