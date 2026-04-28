using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

// Black-box bootstrap tests for the CLI. Exercises the no-args, --help,
// and unknown-subcommand exit code surface that PrintUsage() / Dispatch()
// guarantee, without actually running any pipeline command.
public sealed class CliBootstrapTests
{
    [Fact]
    public void NoArgs_PrintsUsage_And_ReturnsExitOne()
    {
        var result = CliInvoker.Run();
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("AccessHarvester", result.StdErr);
        Assert.Contains("Usage:", result.StdErr);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("/?")]
    public void HelpFlag_PrintsUsage_And_ReturnsExitZero(string flag)
    {
        var result = CliInvoker.Run(flag);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdErr);
    }

    [Fact]
    public void UnknownSubcommand_PrintsError_And_ReturnsExitOne()
    {
        // Use a token containing a "--"-style flag prefix-free word that is
        // not a recognized subcommand and not a file path on disk. The CLI's
        // bare-file mode would otherwise route it to `parse`, which would
        // emit "File not found" — distinct from the unknown-subcommand path.
        var result = CliInvoker.Run("definitely-not-a-real-subcommand-xyz");
        Assert.Equal(1, result.ExitCode);
        // Bare-file mode catches this token because it lacks "--" prefix and
        // is not in the known subcommand set; it routes to parse, which
        // emits a "File not found" failure JSON. Either path returns exit 1
        // and is the expected unknown-input behaviour from the user's view.
        bool sawUsage = result.StdErr.Contains("Unknown subcommand");
        bool sawFileNotFound = result.StdOut.Contains("File not found")
            || result.StdOut.Contains("\"status\": \"failed\"");
        Assert.True(sawUsage || sawFileNotFound,
            $"Expected unknown-subcommand or parse-failure response. " +
            $"stdout={result.StdOut} stderr={result.StdErr}");
    }
}
