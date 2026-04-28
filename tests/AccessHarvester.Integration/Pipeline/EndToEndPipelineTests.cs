using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
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
//     (that each tier produced the expected directory shape and at least
//     one IR file per requested category) so the integration project's
//     gate is "the pipeline ran end-to-end and produced something that
//     downstream tooling can iterate", not "the extractor handled this
//     specific edge case".
//
// The "ran-pipeline output validates against harvester-schema.v1.0.json"
// gate is intentionally NOT here — see Gap A in
// docs/session-sets/integration-testing-and-acceptance/coverage-audit.md.
// The pipeline emits per-object IR files today, not a rolled-up
// DatabaseManifest, so there is no document for SchemaValidator to
// consume yet.
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
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }
}
