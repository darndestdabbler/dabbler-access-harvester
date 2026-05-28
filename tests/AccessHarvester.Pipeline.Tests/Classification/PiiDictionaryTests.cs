using AccessHarvester.Pipeline.Classification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Classification;

public class PiiDictionaryTests
{
    [Theory]
    [InlineData("First*", "FirstName",   true)]
    [InlineData("First*", "firstname",   true)]   // glob is case-insensitive
    [InlineData("First*", "OldFirst",    false)]  // glob is anchored, no leading *
    [InlineData("SSN*",   "SSN",         true)]
    [InlineData("SSN*",   "SSNumber",    true)]
    [InlineData("DOB",    "DOB",         true)]
    [InlineData("DOB",    "DOB2",        false)]
    [InlineData("Addr*",  "Address1",    true)]
    [InlineData("Email*", "EmailAddr",   true)]
    public void CompileGlob_anchors_and_handles_star_wildcard(string glob, string name, bool expected)
    {
        var regex = PiiDictionary.CompileGlob(glob);
        Assert.Equal(expected, regex.IsMatch(name));
    }

    [Fact]
    public void Compiled_value_pattern_ids_all_resolve_to_regexes()
    {
        foreach (var id in PiiDictionary.DefaultPiiValuePatternIds)
            Assert.True(PiiDictionary.DefaultPiiValuePatterns.ContainsKey(id),
                        $"No regex registered for value-pattern id '{id}'");
    }

    [Theory]
    [InlineData("us-ssn",      "123-45-6789", true)]
    [InlineData("us-ssn",      "12345-6789",  false)]
    [InlineData("us-phone",    "(212) 555-1234", true)]
    [InlineData("us-phone",    "212-555-1234",   true)]
    [InlineData("us-phone",    "555-1234",       false)]
    [InlineData("us-zip",      "12345",     true)]
    [InlineData("us-zip",      "12345-6789", true)]
    [InlineData("us-zip",      "ABCDE",     false)]
    [InlineData("email",       "user@example.com",  true)]
    [InlineData("email",       "not-an-email",      false)]
    [InlineData("credit-card", "4111-1111-1111-1111", true)]
    [InlineData("credit-card", "4111111111111111",    true)]
    [InlineData("credit-card", "411111",              false)]
    public void Value_patterns_match_canonical_samples(string id, string value, bool expected)
    {
        var regex = PiiDictionary.DefaultPiiValuePatterns[id];
        Assert.Equal(expected, regex.IsMatch(value));
    }
}
