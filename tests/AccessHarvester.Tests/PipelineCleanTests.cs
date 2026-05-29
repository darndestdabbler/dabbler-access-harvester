using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Core;
using AccessHarvester.Core.Abstractions;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Tests;

// Hermetic coverage for the batch-readiness changes that closed
// Findings #4 and #6 of harvester-manifest-index-and-batch-readiness:
//
//   - Enrich(clean: true) deletes the target ir/ tier before re-emitting,
//     so orphan files left behind by source objects removed since the
//     prior run do not linger (Finding #4).
//
//   - Enrich(clean: false) preserves prior ir/ contents, leaving the
//     legacy file-by-file overwrite behaviour intact for callers that
//     opt out via --no-clean.
//
//   - EmitRootIndex scans <outputRoot>/AccessMetaData/*/manifest.json
//     and writes a deterministic, name-sorted roll-up index at
//     <outputRoot>/AccessMetaData/index.json so a batch consumer
//     pointed at a many-DB output root gets a single enumeration
//     without filesystem scanning (Finding #4's optional cross-DB
//     roll-up).
//
// All tests are pure file-system mechanics — no Access COM, no
// SkippableFact gate, runs on every host.
public sealed class PipelineCleanTests
{
    [Fact]
    public void Enrich_CleanTrue_RemovesOrphanIrFile_WhenKbObjectIsGone()
    {
        var outRoot = NewTempDir();
        try
        {
            string kbDir = Path.Combine(outRoot, "kb");
            string irDir = Path.Combine(outRoot, "ir");

            // Round 1 — kb has frmOld + frmNew → ir gets both.
            string kbCategory = Path.Combine(kbDir, "TestCategory");
            Directory.CreateDirectory(kbCategory);
            File.WriteAllText(Path.Combine(kbCategory, "frmOld.json"), "{ }");
            File.WriteAllText(Path.Combine(kbCategory, "frmNew.json"), "{ }");

            var pipeline = NoEnricherPipeline();
            pipeline.Enrich(kbDir, irDir, clean: true);

            string irCategory = Path.Combine(irDir, "TestCategory");
            Assert.True(File.Exists(Path.Combine(irCategory, "frmOld.ir.json")));
            Assert.True(File.Exists(Path.Combine(irCategory, "frmNew.ir.json")));

            // Round 2 — frmOld removed from kb → ir/frmOld.ir.json must
            // be gone. The clean semantics wipe ir/ before re-emission so
            // the orphan can never survive an opt-in run.
            File.Delete(Path.Combine(kbCategory, "frmOld.json"));
            pipeline.Enrich(kbDir, irDir, clean: true);

            Assert.False(File.Exists(Path.Combine(irCategory, "frmOld.ir.json")),
                "clean=true must remove the orphan ir/ file when its kb source is gone.");
            Assert.True(File.Exists(Path.Combine(irCategory, "frmNew.ir.json")),
                "Surviving kb source must still produce an ir/ file after clean.");
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    [Fact]
    public void Enrich_CleanFalse_LeavesOrphanIrFile_WhenKbObjectIsGone()
    {
        var outRoot = NewTempDir();
        try
        {
            string kbDir = Path.Combine(outRoot, "kb");
            string irDir = Path.Combine(outRoot, "ir");

            // Seed an ir/ that mimics a prior run with frmOld + frmNew.
            string irCategory = Path.Combine(irDir, "TestCategory");
            Directory.CreateDirectory(irCategory);
            File.WriteAllText(Path.Combine(irCategory, "frmOld.ir.json"), "{ }");
            File.WriteAllText(Path.Combine(irCategory, "frmNew.ir.json"), "{ }");

            // Kb has only frmNew — frmOld was deleted from source between
            // runs.
            string kbCategory = Path.Combine(kbDir, "TestCategory");
            Directory.CreateDirectory(kbCategory);
            File.WriteAllText(Path.Combine(kbCategory, "frmNew.json"), "{ }");

            var pipeline = NoEnricherPipeline();
            pipeline.Enrich(kbDir, irDir, clean: false);

            // clean=false leaves the orphan exactly where it is, matching
            // the legacy file-by-file overwrite contract documented in
            // --no-clean's help text.
            Assert.True(File.Exists(Path.Combine(irCategory, "frmOld.ir.json")),
                "clean=false must preserve a stale ir/ file that has no kb source.");
            Assert.True(File.Exists(Path.Combine(irCategory, "frmNew.ir.json")));
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    [Fact]
    public void EmitRootIndex_WritesSchemaShapedRollUp_SortedByDatabaseName()
    {
        var outRoot = NewTempDir();
        try
        {
            string accessMetaRoot = Path.Combine(outRoot, "AccessMetaData");
            // Intentional non-alphabetical creation order so the test
            // proves the sort is name-driven, not filesystem-driven.
            SeedManifest(accessMetaRoot, "Northwind",      schemaVersion: "1.1");
            SeedManifest(accessMetaRoot, "HomeHealthCare", schemaVersion: "1.1");
            SeedManifest(accessMetaRoot, "Alpha",          schemaVersion: "1.0");

            CorePipeline.EmitRootIndex(outRoot);

            string indexPath = Path.Combine(accessMetaRoot, "index.json");
            Assert.True(File.Exists(indexPath),
                $"Cross-DB roll-up index missing at {indexPath}.");

            var index = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
            Assert.Equal("AccessMetaDataRootIndex", index["SchemaType"]!.GetValue<string>());
            Assert.Equal("1.0",                     index["SchemaVersion"]!.GetValue<string>());

            var dbs = index["Databases"]!.AsArray();
            Assert.Equal(3, dbs.Count);

            // Sort key is the embedded Database.Name (not the on-disk dir
            // name), so "Alpha.accdb" precedes "HomeHealthCare.accdb"
            // precedes "Northwind.accdb" under Ordinal sort.
            Assert.Equal("Alpha.accdb",          dbs[0]!["Name"]!.GetValue<string>());
            Assert.Equal("HomeHealthCare.accdb", dbs[1]!["Name"]!.GetValue<string>());
            Assert.Equal("Northwind.accdb",      dbs[2]!["Name"]!.GetValue<string>());

            Assert.Equal("Alpha/manifest.json",          dbs[0]!["Manifest"]!.GetValue<string>());
            Assert.Equal("HomeHealthCare/manifest.json", dbs[1]!["Manifest"]!.GetValue<string>());
            Assert.Equal("Northwind/manifest.json",      dbs[2]!["Manifest"]!.GetValue<string>());

            Assert.Equal("1.0", dbs[0]!["SchemaVersion"]!.GetValue<string>());
            Assert.Equal("1.1", dbs[1]!["SchemaVersion"]!.GetValue<string>());
            Assert.Equal("1.1", dbs[2]!["SchemaVersion"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    [Fact]
    public void EmitRootIndex_IsDeterministic_AcrossTwoInvocations()
    {
        var outRoot = NewTempDir();
        try
        {
            string accessMetaRoot = Path.Combine(outRoot, "AccessMetaData");
            SeedManifest(accessMetaRoot, "Bravo", schemaVersion: "1.1");
            SeedManifest(accessMetaRoot, "Alpha", schemaVersion: "1.1");

            CorePipeline.EmitRootIndex(outRoot);
            string first = File.ReadAllText(Path.Combine(accessMetaRoot, "index.json"));

            CorePipeline.EmitRootIndex(outRoot);
            string second = File.ReadAllText(Path.Combine(accessMetaRoot, "index.json"));

            Assert.Equal(first, second);
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    [Fact]
    public void EmitRootIndex_SkipsDbDir_WhenManifestMissingOrUnparseable()
    {
        var outRoot = NewTempDir();
        try
        {
            string accessMetaRoot = Path.Combine(outRoot, "AccessMetaData");
            SeedManifest(accessMetaRoot, "Good", schemaVersion: "1.1");

            // Dir without a manifest.json — must be silently skipped, not
            // crash, not enumerated.
            Directory.CreateDirectory(Path.Combine(accessMetaRoot, "NoManifest"));

            // Dir with an unparseable manifest.json — same treatment. The
            // per-DB Run that emitted the bad file already surfaced the
            // problem through its own diagnostics; the index is not the
            // place to re-raise it.
            string brokenDir = Path.Combine(accessMetaRoot, "Broken");
            Directory.CreateDirectory(brokenDir);
            File.WriteAllText(Path.Combine(brokenDir, "manifest.json"), "this is not json");

            CorePipeline.EmitRootIndex(outRoot);

            var index = JsonNode.Parse(
                File.ReadAllText(Path.Combine(accessMetaRoot, "index.json")))!.AsObject();
            var names = index["Databases"]!.AsArray()
                .Select(d => d!["Name"]!.GetValue<string>())
                .ToArray();
            Assert.Single(names);
            Assert.Equal("Good.accdb", names[0]);
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    private static CorePipeline NoEnricherPipeline()
        => new CorePipeline(
            exporters: System.Array.Empty<IObjectExporter>(),
            translators: System.Array.Empty<IObjectTranslator>(),
            enrichers: System.Array.Empty<IObjectEnricher>());

    private static void SeedManifest(string accessMetaRoot, string dbName, string schemaVersion)
    {
        string dbDir = Path.Combine(accessMetaRoot, dbName);
        Directory.CreateDirectory(dbDir);
        var manifest = new
        {
            SchemaType = "DatabaseManifest",
            SchemaVersion = schemaVersion,
            HarvesterVersion = "0.0.0",
            Database = new { Name = dbName + ".accdb" },
        };
        File.WriteAllText(
            Path.Combine(dbDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string NewTempDir()
    {
        string p = Path.Combine(Path.GetTempPath(), $"ah-pipeclean-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(p);
        return p;
    }
}
