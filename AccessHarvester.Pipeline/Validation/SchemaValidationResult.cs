using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Validation;

/// <summary>Result of validating a document against the harvester schema.</summary>
public sealed class SchemaValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    private SchemaValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public static SchemaValidationResult Valid() =>
        new(true, Array.Empty<string>());

    public static SchemaValidationResult Invalid(IReadOnlyList<string> errors) =>
        new(false, errors);
}
