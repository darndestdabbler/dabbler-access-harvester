using System;
using AccessHarvester.Cli;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for the shared <see cref="OptionParser"/> (006 O-1-4).
/// The seven copy-pasted <c>ParseOptions</c> bodies (Program.cs + six command
/// classes) now delegate to this one implementation, so unknown-switch
/// rejection is uniform across every subcommand. These pin the core tokeniser
/// behaviour the per-command black-box tests then exercise end-to-end.
/// </summary>
public class OptionParserTests
{
    private static readonly string[] ValueOpts = ["in", "out"];
    private static readonly string[] FlagOpts = ["json-status"];

    [Fact]
    public void Parse_CapturesValueOptions()
    {
        var opts = OptionParser.Parse(["--in", "a.txt", "--out", "b"], ValueOpts, FlagOpts);
        Assert.Equal("a.txt", opts["in"]);
        Assert.Equal("b", opts["out"]);
    }

    [Fact]
    public void Parse_FlagOption_MapsToTrue()
    {
        var opts = OptionParser.Parse(["--in", "a.txt", "--json-status"], ValueOpts, FlagOpts);
        Assert.Equal("true", opts["json-status"]);
        Assert.True(opts.ContainsKey("json-status"));
    }

    [Fact]
    public void Parse_IsCaseInsensitiveOnKeys()
    {
        var opts = OptionParser.Parse(["--IN", "a.txt", "--Json-Status"], ValueOpts, FlagOpts);
        Assert.Equal("a.txt", opts["in"]);
        Assert.True(opts.ContainsKey("json-status"));
    }

    [Fact]
    public void Parse_UnknownOption_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => OptionParser.Parse(["--in", "a.txt", "--bogus", "x"], ValueOpts, FlagOpts));
        Assert.Contains("Unknown option: --bogus", ex.Message);
    }

    [Fact]
    public void Parse_PositionalArgument_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => OptionParser.Parse(["positional", "--in", "a.txt"], ValueOpts, FlagOpts));
        Assert.Contains("Unexpected positional argument: positional", ex.Message);
    }

    [Fact]
    public void Parse_ValueOptionWithoutValue_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => OptionParser.Parse(["--in"], ValueOpts, FlagOpts));
        Assert.Contains("Option --in requires a value.", ex.Message);
    }

    [Fact]
    public void Parse_ValueOptionFollowedByAnotherOption_Throws()
    {
        // "--in" expects a value; the next token is itself an option, so the
        // value is missing rather than silently consuming "--out".
        var ex = Assert.Throws<ArgumentException>(
            () => OptionParser.Parse(["--in", "--out", "b"], ValueOpts, FlagOpts));
        Assert.Contains("Option --in requires a value.", ex.Message);
    }

    [Fact]
    public void Parse_EmptyArgs_ReturnsEmptyMap()
    {
        var opts = OptionParser.Parse([], ValueOpts, FlagOpts);
        Assert.Empty(opts);
    }

    [Fact]
    public void Parse_RepeatedValueOption_Throws()
    {
        // A repeated option is a malformed invocation, not a merge — "last one
        // wins" would silently accept a wrong run (006 P5 honest-error contract).
        var ex = Assert.Throws<ArgumentException>(
            () => OptionParser.Parse(["--out", "a", "--out", "b"], ValueOpts, FlagOpts));
        Assert.Contains("Option --out specified more than once.", ex.Message);
    }

    [Fact]
    public void Parse_RepeatedFlagOption_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => OptionParser.Parse(["--json-status", "--json-status"], ValueOpts, FlagOpts));
        Assert.Contains("Option --json-status specified more than once.", ex.Message);
    }

    [Fact]
    public void Parse_RepeatedOptionDifferingOnlyInCase_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => OptionParser.Parse(["--out", "a", "--OUT", "b"], ValueOpts, FlagOpts));
        Assert.Contains("specified more than once.", ex.Message);
    }
}
