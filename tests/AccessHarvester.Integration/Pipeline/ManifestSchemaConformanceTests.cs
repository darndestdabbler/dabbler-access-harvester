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
// integration-testing-and-acceptance set: until the integration set
// wired Pipeline.Run() to emit a rolled-up DatabaseManifest, there
// was no document for SchemaValidator to consume against
// harvester-schema.v1.1.json. This class validates both the
// aggregator-produced shape (hermetic) and the production-pipeline-
// produced manifest.json on disk (SkippableFact integration check).
//
// Distinct from SchemaConformanceTests in this same directory: that
// class validates the committed example fixtures under examples/.
// This one validates manifests the harvester itself produces.
//
// Serialized with the other HomeHealthCare COM tests: its SkippableFact
// integration checks drive in-process COM against
// samples/HomeHealthCare.accdb, so it shares the ACE file lock with the
// Cli command tests (project-guidance.md -> Testing Conventions). The
// hermetic aggregator tests in this class are unaffected by the collection.
[Collection("HomeHealthCareCom")]
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

    /// <summary>
    /// Full pipeline run against HomeHealthCare validates the complete
    /// post-Session-1 manifest (Modules + Forms + Reports + Queries +
    /// Tables + Relationships) against the v1.1 schema. Closes the
    /// Session-1-handoff: Session 2 absorbed the three drift fixes
    /// (ParserOutput.Items branch selection via ItemKind discriminator,
    /// RelationshipDefinition Parent/Child rename, TableIndex
    /// IsPrimary/IsUnique/IsRequired + Fields-as-objects) into the
    /// additive v1.0 → v1.1 bump, and added the navigation index
    /// (Documentation + FileMap) per Finding #2.
    /// </summary>
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void HomeHealthCare_FullRun_ManifestValidatesAgainstSchema()
    {
        AccessAvailability.SkipIfNoAccess();

        string db = Path.Combine(RepoPaths.Samples(), "HomeHealthCare.accdb");
        Skip.IfNot(File.Exists(db), $"Canonical fixture not present at {db}.");

        string outRoot = Path.Combine(Path.GetTempPath(), $"ah-int-hhc-fullrun-{Guid.NewGuid():N}");
        try
        {
            var runResult = CorePipeline.Default().Run(db, outRoot);

            string manifestPath = Path.Combine(runResult.BaseDir, "manifest.json");
            Assert.True(File.Exists(manifestPath), $"Manifest not produced at {manifestPath}.");

            string json = File.ReadAllText(manifestPath);
            var result = ValidatorAgainstHarvesterSchema().Validate(json);

            Assert.True(
                result.IsValid,
                $"HomeHealthCare full-run manifest {manifestPath} failed schema validation:\n  - " +
                string.Join("\n  - ", result.Errors));
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }

    // v1.1 (Session 2 backward-compat): a v1.0-shaped document (no
    // Documentation, no FileMap, all per-category arrays empty) must
    // continue to validate against v1.1, modulo the SchemaVersion
    // const bump. examples/northwind-output.json is the smoke fixture
    // the CLI's `validate` subcommand exercises; if it stops
    // validating, the additive-policy guarantee is broken.
    [Fact]
    public void V1_0_Shaped_SmokeFixture_ValidatesAgainstSchema()
    {
        string fixturePath = Path.Combine(RepoPaths.Examples(), "northwind-output.json");
        Assert.True(File.Exists(fixturePath),
            $"Smoke fixture missing: {fixturePath}");

        string json = File.ReadAllText(fixturePath);
        var result = ValidatorAgainstHarvesterSchema().Validate(json);

        Assert.True(
            result.IsValid,
            "examples/northwind-output.json (v1.0-shape, no Documentation/FileMap) "
                + "must validate against v1.1:\n  - "
                + string.Join("\n  - ", result.Errors));
    }

    // v1.1 (Session 2 — Finding #2): Pipeline.Run() must emit a
    // READ-ME-FIRST.md alongside manifest.json so a binary-only
    // consumer who opens the output folder cold finds the entry
    // point. Content is static — same bytes across runs — so the
    // determinism gate stays green. This test only runs when Access
    // is present (it drives the real pipeline rather than building a
    // synthetic shape).
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void PipelineRun_EmitsReadMeFirstAlongsideManifest()
    {
        AccessAvailability.SkipIfNoAccess();

        string db = Path.Combine(RepoPaths.Samples(), "HomeHealthCare.accdb");
        Skip.IfNot(File.Exists(db), $"Sample DB not present at {db}.");

        string outRoot = Path.Combine(Path.GetTempPath(), $"ah-int-readme-{Guid.NewGuid():N}");
        try
        {
            var pipeline = CorePipeline.Default().WithCategories(new[] { "Forms" });
            var runResult = pipeline.Run(db, outRoot);

            string manifestPath = Path.Combine(runResult.BaseDir, "manifest.json");
            string readMePath = Path.Combine(runResult.BaseDir, "READ-ME-FIRST.md");
            Assert.True(File.Exists(manifestPath), $"Manifest not produced at {manifestPath}.");
            Assert.True(File.Exists(readMePath), $"READ-ME-FIRST.md not produced at {readMePath}.");

            string content = File.ReadAllText(readMePath);
            Assert.Contains("manifest.json", content);
            Assert.Contains("harvester-schema", content);
            Assert.Contains("interpretation-guide", content);
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
