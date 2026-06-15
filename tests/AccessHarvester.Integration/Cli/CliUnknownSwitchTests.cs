using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Black-box pins that every subcommand rejects an unknown <c>--switch</c> as a
/// usage error (006 O-1-4). Before this set only <c>generate-migration</c>
/// validated option names; the other eleven subcommands stored an unrecognised
/// <c>--key value</c> pair and then never read it, so a typo (e.g. <c>--datta</c>
/// for <c>--data</c>) silently degraded the run instead of failing. The shared
/// <see cref="AccessHarvester.Cli.OptionParser"/> now rejects unknown switches
/// uniformly. The unknown-switch check runs in option parsing, before any COM /
/// database work, so these are COM-free usage-error paths (no Access instance).
/// </summary>
public sealed class CliUnknownSwitchTests
{
    // The eleven subcommands whose usage-error exit code is the harvester
    // contract's 1. `identify-cleanup` is pinned separately: it uses exit 2 for
    // every usage error by its own pre-existing (inverted) convention — see
    // IdentifyCleanupExit2OnUnknownSwitch below and DW-15.
    [Theory]
    [InlineData("extract")]
    [InlineData("translate")]
    [InlineData("run")]
    [InlineData("parse")]
    [InlineData("generate-docs")]
    [InlineData("validate")]
    [InlineData("identify-usage")]
    [InlineData("classify")]
    [InlineData("sanitize")]
    [InlineData("report-baseline")]
    [InlineData("generate-migration")]
    public void UnknownSwitch_IsUsageErrorExit1(string subcommand)
    {
        var result = CliInvoker.Run(subcommand, "--bogus", "x");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown option: --bogus", result.StdErr);
    }

    // identify-cleanup returns exit 2 for ALL usage errors (its Run summary
    // documents "0 on success, 2 on usage error, 1 on other failure" — inverted
    // vs the rest of the CLI). O-1-4 only adds rejection; it deliberately does NOT
    // renumber this command's exit code (out of P5 scope; recorded as DW-15).
    // Before this set `identify-cleanup --bogus x` already exited 2 (via the
    // "--in required" path), so the exit code is unchanged — only the message is
    // now the precise "Unknown option" diagnostic.
    [Fact]
    public void IdentifyCleanup_UnknownSwitch_IsRejectedExit2()
    {
        var result = CliInvoker.Run("identify-cleanup", "--bogus", "x");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown option: --bogus", result.StdErr);
    }
}
