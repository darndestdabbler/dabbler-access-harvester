using System.IO;
using System.Text.Json;
using AccessHarvester.Integration.TestSupport;
using Xunit;

namespace AccessHarvester.Integration.Cli;

// Black-box tests for the `parse` subcommand. Drives the CLI's full
// argument-parsing -> file-read -> ANTLR -> JSON-emit path against
// committed VBA fixtures from docs/test-cases/. Hermetic — no Access
// COM required.
public sealed class CliParseTests
{
    [Fact]
    public void Parse_FlagForm_AgainstSimpleValidationFixture_Succeeds()
    {
        string fixture = Path.Combine(RepoPaths.TestCases(), "01-simple-validation.bas");
        Assert.True(File.Exists(fixture), $"Fixture missing: {fixture}");

        var result = CliInvoker.Run("parse", "--in", fixture);

        Assert.Equal(0, result.ExitCode);
        AssertParsedJsonShape(result.StdOut);
    }

    [Fact]
    public void Parse_BareFileMode_AgainstClassWithEvents_Succeeds()
    {
        string fixture = Path.Combine(RepoPaths.TestCases(), "10-class-with-events.cls");
        Assert.True(File.Exists(fixture), $"Fixture missing: {fixture}");

        var result = CliInvoker.Run(fixture);

        Assert.Equal(0, result.ExitCode);
        AssertParsedJsonShape(result.StdOut);
    }

    [Fact]
    public void Parse_FileNotFound_ReturnsExitOne_AndEmitsFailedStatus()
    {
        var result = CliInvoker.Run("parse", "--in", "Z:\\does\\not\\exist.bas");

        Assert.Equal(1, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal("failed", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.GetProperty("errors").GetArrayLength() > 0);
    }

    private static void AssertParsedJsonShape(string stdout)
    {
        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        string status = root.GetProperty("status").GetString()!;
        Assert.Equal("parsed", status);
        Assert.True(root.TryGetProperty("errors", out _));
        var stats = root.GetProperty("tree_stats");
        Assert.True(stats.GetProperty("node_count").GetInt32() > 0);
        Assert.True(stats.TryGetProperty("error_token_count", out _));
    }
}
