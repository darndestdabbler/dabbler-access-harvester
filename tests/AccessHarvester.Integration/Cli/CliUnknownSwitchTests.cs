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
    // All twelve subcommands report a usage error (here, an unknown switch) as the
    // harvester contract's exit 1. `identify-cleanup` was the lone exception with
    // an inverted exit-2-on-usage convention (DW-15); the 013 devil's-advocate pass
    // (both GPT and Gemini) flagged that as defeating the set's exit-code-honesty
    // mandate, so it was normalised onto exit 1 with the rest.
    [Theory]
    [InlineData("extract")]
    [InlineData("translate")]
    [InlineData("run")]
    [InlineData("parse")]
    [InlineData("generate-docs")]
    [InlineData("validate")]
    [InlineData("identify-usage")]
    [InlineData("identify-cleanup")]
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

    // DW-15 normalisation (013 devil's-advocate follow-up): identify-cleanup's
    // OTHER usage errors are now exit 1 too (were exit 2 under the inverted
    // convention). These COM-free paths fail before any database work.
    [Fact]
    public void IdentifyCleanup_MissingRequiredOption_IsUsageErrorExit1()
    {
        var result = CliInvoker.Run("identify-cleanup", "--threshold-days", "30");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("required", result.StdErr);
    }

    [Fact]
    public void IdentifyCleanup_BadThresholdDays_IsUsageErrorExit1()
    {
        var result = CliInvoker.Run("identify-cleanup", "--in", "x", "--threshold-days", "notanumber");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("threshold-days", result.StdErr);
    }
}
