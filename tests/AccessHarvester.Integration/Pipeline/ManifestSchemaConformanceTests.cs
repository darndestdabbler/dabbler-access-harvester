using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccessHarvester.Core;
using AccessHarvester.Integration.TestSupport;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Pipeline.Validation;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Integration.Pipeline;

// Manifest-side schema-conformance gate. Closes Gap A from the prior
// integration-testing-and-acceptance set: until Session 2 wired
// Pipeline.Run() to emit a rolled-up DatabaseManifest, there was no
// document for SchemaValidator to consume against
// harvester-schema.v1.0.json. This class validates both the
// aggregator-produced shape (hermetic) and the production-pipeline-
// produced manifest.json on disk (SkippableFact integration check).
//
// Distinct from SchemaConformanceTests in this same directory: that
// class validates the committed example fixtures under examples/.
// This one validates manifests the harvester itself produces.
public sealed class ManifestSchemaConformanceTests
{
    [Fact]
    public void AggregatorMinimalManifest_ValidatesAgainstSchema()
    {
        // Exercises the contract that the smallest valid input
        // (Database + HarvesterVersion only) round-trips through
        // serialization and passes SchemaValidator. If any field
        // emitted by the envelope conflicts with the schema's
        // required/property declarations, this gate catches it.
        var database = new DatabaseInfo
        {
            Name = "Synthetic.accdb",
            FilePath = @"C:\fixtures\Synthetic.accdb",
            ExportedAt = "2026-05-18T12:00:00.0000000+00:00",
        };

        var manifest = DatabaseManifestAggregator.Build(
            database: database,
            harvesterVersion: "1.0.0-test");

        string json = JsonSerializer.Serialize(manifest, ManifestSerializerOptions);

        var result = ValidatorAgainstHarvesterSchema().Validate(json);

        Assert.True(
            result.IsValid,
            "Aggregator-produced minimal manifest failed schema validation:\n  - " +
            string.Join("\n  - ", result.Errors));
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void PipelineRunProducedManifest_ValidatesAgainstSchema()
    {
        AccessAvailability.SkipIfNoAccess();

        // HomeHealthCare is the primary fixture per project-guidance.md.
        // MiniComplex is excluded from automated test coverage pending an
        // error-audit pass (Label.Source enum drift in that DB's ir/ data).
        string db = Path.Combine(RepoPaths.Samples(), "HomeHealthCare.accdb");
        Skip.IfNot(File.Exists(db), $"Sample DB not present at {db}.");

        string outRoot = Path.Combine(Path.GetTempPath(), $"ah-int-manifest-{Guid.NewGuid():N}");
        try
        {
            var pipeline = CorePipeline.Default().WithCategories(new[] { "Forms" });
            var runResult = pipeline.Run(db, outRoot);

            string manifestPath = Path.Combine(runResult.BaseDir, "manifest.json");
            Assert.True(File.Exists(manifestPath), $"Manifest not produced at {manifestPath}.");

            string json = File.ReadAllText(manifestPath);
            var result = ValidatorAgainstHarvesterSchema().Validate(json);

            Assert.True(
                result.IsValid,
                $"Pipeline-produced manifest {manifestPath} failed schema validation:\n  - " +
                string.Join("\n  - ", result.Errors));
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }

    // Shared with Pipeline.Run() — keeping the options identical
    // ensures the validator sees the same JSON the production path
    // emits.
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static SchemaValidator ValidatorAgainstHarvesterSchema()
        => new(RepoPaths.SchemaFile());
}
