using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Black-box tests for the `run` subcommand.
/// <para>
/// Invokes the CLI executable out-of-process to drive the full
/// Extract -> Translate -> Enrich pipeline against a sample database.
/// </para>
/// <para>
/// Asserts on the process exit code, the shape of the `--json-status`
/// envelope, the existence of the expected tier directories (raw, kb, ir),
/// and the presence of the session-level `manifest.json` and
/// `unresolved-items.json` files.
/// </para>
/// <para>
/// Does NOT duplicate detailed per-object assertions from in-process pipeline
/// tests; its focus is the CLI's public contract surface for the `run` command.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CliRunTests
{
    [SkippableFact]
    public void Run_AgainstHomeHealthCare_Succeeds_And_EmitsFullOutputTree()
    {
        AccessAvailability.SkipIfNoAccess();

        string db = Path.Combine(RepoPaths.Samples(), "HomeHealthCare.accdb");
        Skip.IfNot(File.Exists(db), $"Sample DB not present at {db}.");

        string outRoot = Path.Combine(Path.GetTempPath(), $"ah-cli-run-{Guid.NewGuid():N}");
        try
        {
            var result = CliInvoker.Run("run", "--db", db, "--out", outRoot, "--json-status");

            Assert.Equal(0, result.ExitCode);

            using var doc = JsonDocument.Parse(result.StdOut);
            var envelope = doc.RootElement;

            Assert.Equal("run", envelope.GetProperty("Command").GetString());
            Assert.Equal(0, envelope.GetProperty("ExitCode").GetInt32());

            string baseDir = envelope.GetProperty("BaseDir").GetString()!;
            string rawDir = envelope.GetProperty("RawDir").GetString()!;
            string kbDir = envelope.GetProperty("KbDir").GetString()!;
            string irDir = envelope.GetProperty("IrDir").GetString()!;
            Assert.False(string.IsNullOrEmpty(baseDir));
            Assert.False(string.IsNullOrEmpty(rawDir));
            Assert.False(string.IsNullOrEmpty(kbDir));
            Assert.False(string.IsNullOrEmpty(irDir));
            Assert.False(string.IsNullOrEmpty(envelope.GetProperty("Database").GetString()));
            Assert.False(string.IsNullOrEmpty(envelope.GetProperty("OutputRoot").GetString()));

            var extract = envelope.GetProperty("Extract");
            Assert.True(extract.GetProperty("Ok").GetInt32() > 0);
            Assert.Equal(0, extract.GetProperty("Failed").GetInt32());

            var translate = envelope.GetProperty("Translate");
            Assert.True(translate.GetProperty("Ok").GetInt32() > 0);
            Assert.Equal(0, translate.GetProperty("Failed").GetInt32());

            var enrich = envelope.GetProperty("Enrich");
            Assert.True(enrich.GetProperty("Ok").GetInt32() > 0);
            Assert.Equal(0, enrich.GetProperty("Failed").GetInt32());

            Assert.True(Directory.Exists(rawDir), $"Expected raw dir to exist at {rawDir}");
            Assert.True(Directory.Exists(kbDir), $"Expected kb dir to exist at {kbDir}");
            Assert.True(Directory.Exists(irDir), $"Expected ir dir to exist at {irDir}");

            string manifestPath = Path.Combine(baseDir, "manifest.json");
            string unresolvedPath = Path.Combine(baseDir, "unresolved-items.json");

            Assert.True(File.Exists(manifestPath), $"Expected manifest.json at {manifestPath}");
            Assert.True(File.Exists(unresolvedPath), $"Expected unresolved-items.json at {unresolvedPath}");

            using (var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath)))
            {
                var root = manifestDoc.RootElement;
                Assert.Equal("DatabaseManifest", root.GetProperty("SchemaType").GetString());
                Assert.Equal("1.1", root.GetProperty("SchemaVersion").GetString());
            }

            using (var unresolvedDoc = JsonDocument.Parse(File.ReadAllText(unresolvedPath)))
            {
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
