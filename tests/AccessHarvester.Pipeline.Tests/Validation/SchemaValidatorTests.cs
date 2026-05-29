using System;
using System.IO;
using AccessHarvester.Pipeline.Validation;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Validation;

public sealed class SchemaValidatorTests
{
    private static string LocateSchemaPath()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases", "harvester-schema.v1.1.json");
            if (File.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException(
            $"Could not locate harvester-schema.v1.1.json walking up from '{AppContext.BaseDirectory}'.");
    }

    private static readonly string SchemaPath = LocateSchemaPath();

    private static SchemaValidator CreateValidator() => new(SchemaPath);

    // (a) A conformant minimal DatabaseManifest returns no errors.
    [Fact]
    public void Validate_ConformantDocument_ReturnsValid()
    {
        const string json = """
            {
              "SchemaType": "DatabaseManifest",
              "SchemaVersion": "1.1",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "Test.accdb" }
            }
            """;

        var result = CreateValidator().Validate(json);

        Assert.True(result.IsValid, $"Expected VALID but got errors: {string.Join("; ", result.Errors)}");
        Assert.Empty(result.Errors);
    }

    // (b) A document missing a required field (SchemaType) returns errors.
    [Fact]
    public void Validate_MissingRequiredField_ReturnsInvalid()
    {
        const string json = """
            {
              "SchemaVersion": "1.1",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "Test.accdb" }
            }
            """;

        var result = CreateValidator().Validate(json);

        Assert.False(result.IsValid, "Expected INVALID (missing SchemaType) but got VALID.");
        Assert.NotEmpty(result.Errors);
    }

    // (c) A document with an unknown extra field is accepted (additionalProperties not forbidden at v1).
    [Fact]
    public void Validate_UnknownField_IsAccepted()
    {
        const string json = """
            {
              "SchemaType": "DatabaseManifest",
              "SchemaVersion": "1.1",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "Test.accdb" },
              "UnknownFutureField": "some-value"
            }
            """;

        var result = CreateValidator().Validate(json);

        Assert.True(result.IsValid, $"Expected VALID (unknown fields allowed) but got errors: {string.Join("; ", result.Errors)}");
    }

    // ── SchemaValidator.FromText factory ────────────────────────────────────────

    [Fact]
    public void FromText_ConformantDocument_ReturnsValid()
    {
        string schemaJson = File.ReadAllText(SchemaPath, System.Text.Encoding.UTF8);
        var validator = SchemaValidator.FromText(schemaJson);

        const string json = """
            {
              "SchemaType": "DatabaseManifest",
              "SchemaVersion": "1.1",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "Test.accdb" }
            }
            """;

        var result = validator.Validate(json);

        Assert.True(result.IsValid, $"Expected VALID but got errors: {string.Join("; ", result.Errors)}");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void FromText_InvalidDocument_ReturnsErrors()
    {
        string schemaJson = File.ReadAllText(SchemaPath, System.Text.Encoding.UTF8);
        var validator = SchemaValidator.FromText(schemaJson);

        const string json = """
            {
              "SchemaVersion": "1.1",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "Test.accdb" }
            }
            """;

        var result = validator.Validate(json);

        Assert.False(result.IsValid, "Expected INVALID (missing SchemaType) but got VALID.");
        Assert.NotEmpty(result.Errors);
    }
}
