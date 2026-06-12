using System;
using System.IO;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Black-box tests for the <c>validate</c> subcommand.
/// Drives the CLI against committed fixture manifests and asserts on
/// exit code, stderr content, and --json-status envelope shape.
/// </summary>
public sealed class CliValidateTests
{
    // ── --manifest option ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_Manifest_ValidDocument_ReturnsZero()
    {
        string manifest = Path.Combine(RepoPaths.Examples(), "homehealthcare-output.json");
        Assert.True(File.Exists(manifest), $"Fixture missing: {manifest}");

        var result = CliInvoker.Run("validate", "--manifest", manifest);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Validation passed", result.StdErr);
    }

    [Fact]
    public void Validate_Manifest_InvalidDocument_ReturnsThree_AndStderrHasErrors()
    {
        string manifest = Path.Combine(RepoPaths.Examples(), "validate-invalid-manifest.json");
        Assert.True(File.Exists(manifest), $"Fixture missing: {manifest}");

        var result = CliInvoker.Run("validate", "--manifest", manifest);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Validation errors", result.StdErr);
    }

    [Fact]
    public void Validate_Manifest_FileNotFound_ReturnsOne()
    {
        var result = CliInvoker.Run("validate", "--manifest", "does-not-exist.json");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.StdErr);
    }

    [Fact]
    public void Validate_Manifest_NotJson_ReturnsOne()
    {
        string tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, "this is not json at all", System.Text.Encoding.UTF8);
            var result = CliInvoker.Run("validate", "--manifest", tmpFile);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Not valid JSON", result.StdErr);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── --in option ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_In_DirectoryContainingManifest_ReturnsZero()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), $"ah-val-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            string manifestJson = """
                {
                  "SchemaType": "DatabaseManifest",
                  "SchemaVersion": "1.1",
                  "HarvesterVersion": "1.0.0",
                  "Database": { "Name": "Test.accdb" }
                }
                """;
            File.WriteAllText(Path.Combine(tmpDir, "manifest.json"), manifestJson, System.Text.Encoding.UTF8);

            var result = CliInvoker.Run("validate", "--in", tmpDir);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Validation passed", result.StdErr);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_In_NestedRunOutputLayout_ReturnsZero()
    {
        // Simulates the actual layout produced by `run`: <outRoot>/AccessMetaData/<dbName>/manifest.json
        string tmpDir = Path.Combine(Path.GetTempPath(), $"ah-val-nested-{Guid.NewGuid():N}");
        try
        {
            string manifestDir = Path.Combine(tmpDir, "AccessMetaData", "TestDb.accdb");
            Directory.CreateDirectory(manifestDir);
            string manifestJson = """
                {
                  "SchemaType": "DatabaseManifest",
                  "SchemaVersion": "1.1",
                  "HarvesterVersion": "1.0.0",
                  "Database": { "Name": "TestDb.accdb" }
                }
                """;
            File.WriteAllText(Path.Combine(manifestDir, "manifest.json"), manifestJson, System.Text.Encoding.UTF8);

            var result = CliInvoker.Run("validate", "--in", tmpDir);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Validation passed", result.StdErr);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    // ── per-document schema dispatch (backward-compat v1.0 + v1.1) ──────────────

    // v1.1 additive bump's backward-compat policy: a manifest declaring
    // SchemaVersion="1.0" must still validate against the current
    // `validate` subcommand because the v1.0 schema is bundled
    // alongside v1.1 and dispatch happens per-document.
    [Fact]
    public void Validate_V1_0_Manifest_DispatchesToV1_0_Schema_AndReturnsZero()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), $"ah-val-v10-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            string manifestJson = """
                {
                  "SchemaType": "DatabaseManifest",
                  "SchemaVersion": "1.0",
                  "HarvesterVersion": "1.0.0",
                  "Database": { "Name": "Legacy.accdb" }
                }
                """;
            File.WriteAllText(Path.Combine(tmpDir, "manifest.json"), manifestJson, System.Text.Encoding.UTF8);

            var result = CliInvoker.Run("validate", "--in", tmpDir);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Validation passed", result.StdErr);
            // The "checked against schema vX" line should mention the
            // dispatched version, not the CLI's current literal.
            Assert.Contains("v1.0", result.StdErr);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    // Mixed-version input set: a v1.0 manifest and a v1.1 manifest in
    // the same --in tree should both pass via per-document dispatch
    // and the "checked against schema vX, vY" line should mention both.
    [Fact]
    public void Validate_Mixed_V1_0_And_V1_1_Manifests_BothDispatchedAndPass()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), $"ah-val-mixed-{Guid.NewGuid():N}");
        try
        {
            string v10Dir = Path.Combine(tmpDir, "AccessMetaData", "LegacyDb.accdb");
            string v11Dir = Path.Combine(tmpDir, "AccessMetaData", "CurrentDb.accdb");
            Directory.CreateDirectory(v10Dir);
            Directory.CreateDirectory(v11Dir);

            File.WriteAllText(Path.Combine(v10Dir, "manifest.json"), """
                {
                  "SchemaType": "DatabaseManifest",
                  "SchemaVersion": "1.0",
                  "HarvesterVersion": "1.0.0",
                  "Database": { "Name": "LegacyDb.accdb" }
                }
                """, System.Text.Encoding.UTF8);
            File.WriteAllText(Path.Combine(v11Dir, "manifest.json"), """
                {
                  "SchemaType": "DatabaseManifest",
                  "SchemaVersion": "1.1",
                  "HarvesterVersion": "1.0.0",
                  "Database": { "Name": "CurrentDb.accdb" }
                }
                """, System.Text.Encoding.UTF8);

            var result = CliInvoker.Run("validate", "--in", tmpDir);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Validation passed", result.StdErr);
            Assert.Contains("v1.0", result.StdErr);
            Assert.Contains("v1.1", result.StdErr);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_In_NoManifestFound_ReturnsOne()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), $"ah-val-empty-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            var result = CliInvoker.Run("validate", "--in", tmpDir);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No manifest.json found", result.StdErr);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_In_DirectoryNotFound_ReturnsOne()
    {
        var result = CliInvoker.Run("validate", "--in", "path-that-does-not-exist-xyz");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.StdErr);
    }

    // ── usage errors ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NoOptions_ReturnsOne_UsageError()
    {
        var result = CliInvoker.Run("validate");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage error", result.StdErr);
    }

    [Fact]
    public void Validate_BothInAndManifest_ReturnsOne_UsageError()
    {
        string manifest = Path.Combine(RepoPaths.Examples(), "homehealthcare-output.json");
        var result = CliInvoker.Run("validate", "--in", "somedir", "--manifest", manifest);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage error", result.StdErr);
    }

    // ── --json-status envelope ──────────────────────────────────────────────────

    [Fact]
    public void Validate_JsonStatus_ValidDocument_EmitsEnvelopeWithExitZero()
    {
        string manifest = Path.Combine(RepoPaths.Examples(), "homehealthcare-output.json");
        Assert.True(File.Exists(manifest), $"Fixture missing: {manifest}");

        var result = CliInvoker.Run("validate", "--manifest", manifest, "--json-status");

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal("validate", doc.RootElement.GetProperty("Command").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("ExitCode").GetInt32());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("Errors").ValueKind);
        Assert.Equal(0, doc.RootElement.GetProperty("Errors").GetArrayLength());
    }

    [Fact]
    public void Validate_JsonStatus_InvalidDocument_EmitsEnvelopeWithExitThree()
    {
        string manifest = Path.Combine(RepoPaths.Examples(), "validate-invalid-manifest.json");
        Assert.True(File.Exists(manifest), $"Fixture missing: {manifest}");

        var result = CliInvoker.Run("validate", "--manifest", manifest, "--json-status");

        Assert.Equal(3, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal("validate", doc.RootElement.GetProperty("Command").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("ExitCode").GetInt32());
        Assert.True(doc.RootElement.GetProperty("Errors").GetArrayLength() > 0,
            "Expected at least one error entry in --json-status envelope.");
    }
}
