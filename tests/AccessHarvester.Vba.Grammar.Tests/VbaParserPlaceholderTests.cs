using Xunit;

namespace AccessHarvester.Vba.Grammar.Tests;

public class VbaParserPlaceholderTests
{
    [Fact]
    public void Placeholder_Always_Passes()
    {
        // Placeholder test for the ANTLR4 grammar project.
        // Real parser tests will land in vba-lexical-and-expression-grammar child session set (#3).
        Assert.True(true);
    }
}
