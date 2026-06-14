using System.IO;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Black-box exit-code pins for <c>--types</c> validation (006 F-1-3). Before this
/// fix a misspelled <c>--types</c> value was passed straight into the pure category
/// set filter, matched nothing, and the run reported "Ok: 0, Failed: 0" and exited
/// 0 — a silent empty success the exit-code-keying consumer reads as "all objects
/// processed." These drive the COM-free <c>translate</c> path (the same path the
/// 006 probe used), so no Access instance is involved.
/// </summary>
public sealed class CliTypesValidationTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ah-types-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void UnknownTypesValue_IsUsageErrorExit1WithValidList()
    {
        string raw = NewTempDir();
        string kb = NewTempDir();
        try
        {
            var result = CliInvoker.Run("translate", "--in", raw, "--out", kb, "--types", "Formz");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Unknown --types value(s): Formz", result.StdErr);
            // The error lists the valid set so the operator can correct the typo.
            Assert.Contains("Forms", result.StdErr);
            Assert.Contains("Navigation", result.StdErr);
        }
        finally
        {
            Directory.Delete(raw, recursive: true);
            Directory.Delete(kb, recursive: true);
        }
    }

    [Fact]
    public void EmptyTypesSelection_IsUsageErrorExit1()
    {
        string raw = NewTempDir();
        string kb = NewTempDir();
        try
        {
            // ",," splits to no categories at all — the defensive all-filtered-out guard.
            var result = CliInvoker.Run("translate", "--in", raw, "--out", kb, "--types", ",,");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("selected no categories", result.StdErr);
        }
        finally
        {
            Directory.Delete(raw, recursive: true);
            Directory.Delete(kb, recursive: true);
        }
    }

    [Fact]
    public void ValidTypesValue_DoesNotChangeExitCode()
    {
        string raw = NewTempDir();
        string kb = NewTempDir();
        try
        {
            // A valid category against an empty raw dir is an honest no-op success
            // (exit 0) — the fix must not change the exit code of a valid run.
            var result = CliInvoker.Run("translate", "--in", raw, "--out", kb, "--types", "Forms");

            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Unknown --types", result.StdErr);
        }
        finally
        {
            Directory.Delete(raw, recursive: true);
            Directory.Delete(kb, recursive: true);
        }
    }
}
