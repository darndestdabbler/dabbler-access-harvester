using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core;
using AccessHarvester.Integration.TestSupport;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Integration.Pipeline;

// Full Extract -> Translate -> Enrich pipeline against a committed
// sample .accdb. SkippableFact-gated for hosts without Microsoft Access.
//
// This complements (does not duplicate) AccessHarvester.Tests'
// SyntheticDbIntegrationTests:
//   - SyntheticDbIntegrationTests asserts specific extractor behaviours
//     (geometry-in-inches, signature start lines, etc.) against
//     test-builder-built synthetic DBs.
//   - This class asserts only the contract structure of the run output
//     (each tier produced the expected directory shape, at least one IR
//     file per requested category, and the Session-2 manifest +
//     unresolved-items.json emission landed alongside the tier
//     directories) so the integration project's gate is "the pipeline
//     ran end-to-end and produced something downstream tooling can
//     iterate".
//
// The schema-validation gate ("produced manifest validates against
// harvester-schema.v1.0.json") lives in ManifestSchemaConformanceTests
// — kept separate so a schema regression and a tier-shape regression
// surface as distinct failures.
[SupportedOSPlatform("windows")]
public sealed class EndToEndPipelineTests
{
    [SkippableFact]
    public void Pipeline_RunAgainstMiniComplex_EmitsRawKbIrTreesForFormsCategory()
    {
        AccessAvailability.SkipIfNoAccess();

        string db = Path.Combine(RepoPaths.Samples(), "MiniComplex.accdb");
        Skip.IfNot(File.Exists(db), $"Sample DB not present at {db}.");

        string outRoot = Path.Combine(Path.GetTempPath(), $"ah-int-pipe-{Guid.NewGuid():N}");
        try
        {
            var pipeline = CorePipeline.Default().WithCategories(new[] { "Forms" });
            var result = pipeline.Run(db, outRoot);

            // Pipeline did not surface per-object failures.
            Assert.All(result.Extract,
                c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
            Assert.All(result.Translate,
                c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
            Assert.All(result.Enrich,
                c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

            // Tier directories exist under the conventional layout.
            Assert.True(Directory.Exists(result.RawDir), $"raw dir missing: {result.RawDir}");
            Assert.True(Directory.Exists(result.KbDir),  $"kb dir missing: {result.KbDir}");
            Assert.True(Directory.Exists(result.IrDir),  $"ir dir missing: {result.IrDir}");

            // Forms tier emitted at least one ir file. The exact form list
            // depends on MiniComplex.accdb's contents and is asserted only
            // as "non-empty" — the regression-snapshot suite (Session 2)
            // is where exact expected outputs get pinned.
            //
            // Both checks are mandatory, not conditional: if the pipeline
            // regresses to the point of dropping the Forms category
            // directory entirely, this test must fail rather than silently
            // pass on a missing-directory branch.
            string formsIrDir = Path.Combine(result.IrDir, "Forms");
            Assert.True(
                Directory.Exists(formsIrDir),
                $"Expected ir/Forms directory at {formsIrDir}. " +
                $"Pipeline reported no extract failures but the requested " +
                $"category directory is absent — contract regression.");
            var irFiles = Directory.GetFiles(formsIrDir, "*.ir.json", SearchOption.TopDirectoryOnly);
            Assert.True(
                irFiles.Any(),
                $"Expected at least one Forms/*.ir.json under {formsIrDir}. " +
                $"Pipeline reported no extract failures, so empty output indicates a contract change.");

            // Session 2 emission gates: manifest.json and unresolved-items.json
            // land alongside raw/, kb/, ir/ at <baseDir>. The contract is
            // verified at the file-existence + minimal-shape level here; full
            // schema validation lives in ManifestSchemaConformanceTests.
            string manifestPath = Path.Combine(result.BaseDir, "manifest.json");
            string unresolvedPath = Path.Combine(result.BaseDir, "unresolved-items.json");

            Assert.True(File.Exists(manifestPath),
                $"Expected manifest at {manifestPath}. Pipeline.Run() must emit the rolled-up " +
                $"DatabaseManifest after Enrich() — contract regression.");
            Assert.True(File.Exists(unresolvedPath),
                $"Expected unresolved-items at {unresolvedPath}. Pipeline.Run() must emit the " +
                $"sibling unresolved-items array (per GAP #5) — contract regression.");

            using (var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath)))
            {
                var root = manifestDoc.RootElement;
                Assert.Equal("DatabaseManifest", root.GetProperty("SchemaType").GetString());
                Assert.Equal("1.0", root.GetProperty("SchemaVersion").GetString());
                Assert.False(string.IsNullOrEmpty(root.GetProperty("HarvesterVersion").GetString()));
                Assert.Equal(
                    "MiniComplex.accdb",
                    root.GetProperty("Database").GetProperty("Name").GetString());
            }

            using (var unresolvedDoc = JsonDocument.Parse(File.ReadAllText(unresolvedPath)))
            {
                // V1 wiring emits an empty array; downstream tooling reads
                // this file as IReadOnlyList<UnresolvedItem>. Assert array
                // shape regardless of cardinality so a future regression
                // that flips the file to an object surfaces here.
                Assert.Equal(JsonValueKind.Array, unresolvedDoc.RootElement.ValueKind);
            }
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }
}
