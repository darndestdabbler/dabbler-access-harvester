using System;
using System.IO;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

// Black-box tests for the `generate-docs` subcommand. Hermetic — drives
// the CLI against committed examples/*.json fixtures and asserts that the
// expected schema-summary.md lands at <out>/<dbSlug>/schema-summary.md.
//
// Fixture choice: northwind-output.json is the manifest-shaped example
// (SchemaType == "DatabaseManifest", Database.Name set). The other
// examples (minimal-form.json, complex-form.json) are per-form IR
// records, not manifests, so they are not valid inputs to generate-docs.
public sealed class CliGenerateDocsTests
{
    [Fact]
    public void GenerateDocs_AgainstCommittedManifest_WritesSchemaSummary()
    {
        string manifest = Path.Combine(RepoPaths.Examples(), "northwind-output.json");
        Assert.True(File.Exists(manifest), $"Example manifest missing: {manifest}");

        string outDir = Path.Combine(Path.GetTempPath(), $"ah-int-{Guid.NewGuid():N}");
        try
        {
            var result = CliInvoker.Run("generate-docs", "--in", manifest, "--out", outDir);
            Assert.Equal(0, result.ExitCode);

            // The CLI derives the slug from the Database.Name property of
            // the manifest. northwind-output.json sets it to
            // "NorthwindDevEdition.accdb" -> slug "northwinddevedition"
            // (regex strips non-word characters, then ToLowerInvariant).
            string expected = Path.Combine(outDir, "northwinddevedition", "schema-summary.md");
            Assert.True(File.Exists(expected),
                $"Expected schema summary not written. stderr={result.StdErr}");

            string md = File.ReadAllText(expected);
            // SchemaDocGenerator emits a Database overview, form catalog,
            // and subform-dependency section. Spot-check the heading shape
            // without binding to exact prose.
            Assert.Contains("NorthwindDevEdition.accdb", md);
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }
}
