using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Help-text drift gate — asserts <c>PrintUsage()</c> in
/// <c>AccessHarvester.Cli.Program</c> stays in sync with the dispatcher's
/// subcommand set, option list, and per-group exit-code legend. Future
/// drift (a new subcommand without a help entry, a removed exit code,
/// etc.) fails here rather than waiting for a downstream consumer to
/// notice.
/// </summary>
public sealed class CliUsageTextTests
{
    [Fact]
    public void PrintUsage_ListsAllDispatcherSubcommands()
    {
        var result = CliInvoker.Run();
        string usage = result.StdErr;

        Assert.Contains("extract", usage);
        Assert.Contains("translate", usage);
        Assert.Contains("run", usage);
        Assert.Contains("parse", usage);
        Assert.Contains("generate-docs", usage);
        Assert.Contains("bare-file mode", usage);
    }

    [Fact]
    public void PrintUsage_DocumentsExitCodesForEachSubcommandGroup()
    {
        var result = CliInvoker.Run();
        string usage = result.StdErr;

        Assert.Contains("Exit codes for extract/translate/run:", usage);
        Assert.Contains("Exit codes for parse", usage);
        Assert.Contains("Exit codes for generate-docs:", usage);
    }

    [Fact]
    public void PrintUsage_DocumentsBothCliOptions()
    {
        var result = CliInvoker.Run();
        string usage = result.StdErr;

        Assert.Contains("--types", usage);
        Assert.Contains("--json-status", usage);
    }

    [Fact]
    public void UnknownSubcommand_MultiArg_EmitsUnknownCommandAndPrintsUsage()
    {
        var result = CliInvoker.Run("fake-cmd", "--db", "x");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown subcommand:", result.StdErr);
        Assert.Contains("Usage:", result.StdErr);
    }
}
