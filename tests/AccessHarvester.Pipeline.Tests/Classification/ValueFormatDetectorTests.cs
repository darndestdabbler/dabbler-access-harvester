using System.Collections.Generic;
using AccessHarvester.Pipeline.Classification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Classification;

public class ValueFormatDetectorTests
{
    [Fact]
    public void Detects_consistent_ssn_shape()
    {
        var samples = new List<string?> { "123-45-6789", "987-65-4321", "001-02-0003" };
        Assert.Equal("###-##-####", ValueFormatDetector.Detect(samples));
    }

    [Fact]
    public void Detects_literal_label_plus_digits()
    {
        var samples = new List<string?> { "MRN-1234567", "MRN-7654321" };
        Assert.Equal("MRN-#######", ValueFormatDetector.Detect(samples));
    }

    [Fact]
    public void Returns_null_when_no_two_samples_agree()
    {
        var samples = new List<string?> { "12345", "ABC-12", "98765" };
        Assert.Null(ValueFormatDetector.Detect(samples));
    }

    [Fact]
    public void Returns_null_when_only_uppercase_letters_observed()
    {
        // No placeholder token — the schema's at-least-one-placeholder rule
        // requires the detector to drop this rather than emit "JOHNSMITH".
        var samples = new List<string?> { "JOHNSMITH" };
        Assert.Null(ValueFormatDetector.Detect(samples));
    }

    [Fact]
    public void Lowercase_letters_collapse_to_any_char_placeholder()
    {
        var samples = new List<string?> { "user1@example.com" };
        // 'user1' → '????#', '@' → '?', 'example.com' → '???????.???'
        // Just assert that the result conforms to the schema regex shape
        // and is non-null.
        var result = ValueFormatDetector.Detect(samples);
        Assert.NotNull(result);
        // schema regex: ^[A-Z#? .,()+:/-]*$ and must contain at least one [#?]
        Assert.Matches(@"^[A-Z#? .,()+:/-]*$", result!);
        Assert.Matches(@"[#?]", result!);
    }

    [Fact]
    public void Empty_input_returns_null()
    {
        Assert.Null(ValueFormatDetector.Detect(new List<string?>()));
    }

    [Fact]
    public void Whitespace_only_entries_are_ignored()
    {
        // null / empty / whitespace skipped; remaining single observation
        // satisfies the at-least-one-placeholder requirement.
        var samples = new List<string?> { null, "", "12345" };
        Assert.Equal("#####", ValueFormatDetector.Detect(samples));
    }
}
