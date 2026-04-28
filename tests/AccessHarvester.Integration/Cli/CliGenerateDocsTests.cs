using System;
using System.IO;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

// Black-box tests for the `generate-docs` subcommand. Hermetic — drives
// the CLI against committed examples/*.json fixtures and asserts that the
// expected schema-summary.md lands at <out>/<dbSlug>/schema-summary.md.
public sealed class CliGenerateDocsTests
{
    [Fact]
    public void GenerateDocs_AgainstMinimalFormManifest_WritesSchemaSummary()
    {
        string manifest = Path.Combine(RepoPaths.Examples(), "minimal-form.json");
        Assert.True(File.Exists(manifest), $"Example manifest missing: {manifest}");

        string outDir = Path.Combine(Path.GetTempPath(), $"ah-int-{Guid.NewGuid():N}");
        try
        {
            var result = CliInvoker.Run("generate-docs", "--in", manifest, "--out", outDir);
            Assert.Equal(0, result.ExitCode);

            // The CLI derives the slug from the Database.Name property of
            // the manifest. minimal-form.json sets it to "Minimal.accdb"
            // -> slug "minimal".
            string expected = Path.Combine(outDir, "minimal", "schema-summary.md");
            Assert.True(File.Exists(expected),
                $"Expected schema summary not written. stderr={result.StdErr}");

            string md = File.ReadAllText(expected);
            // SchemaDocGenerator emits a Database overview, form catalog,
            // and subform-dependency section. Spot-check the heading shape
            // without binding to exact prose.
            Assert.Contains("Minimal.accdb", md);
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }
}
