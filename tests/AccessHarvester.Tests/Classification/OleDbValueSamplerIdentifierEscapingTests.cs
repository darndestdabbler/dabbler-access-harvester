using System;
using AccessHarvester.Core.Com;
using Xunit;

namespace AccessHarvester.Tests.Classification;

/// <summary>
/// Strict-unit tests for <see cref="OleDbValueSampler.EscapeIdentifier"/>.
/// The escaper is the single defense against malformed Access identifiers
/// poisoning the generated SQL; it has to be exact about which inputs it
/// accepts and which it rejects.
/// </summary>
public class OleDbValueSamplerIdentifierEscapingTests
{
    [Theory]
    [InlineData("Person")]
    [InlineData("Last Name")]   // spaces are common in Access names
    [InlineData("DateOfBirth")]
    [InlineData("Person_123")]
    [InlineData("Person.Old")]  // dots are legal inside [...]
    [InlineData("[Open")]       // a stray open bracket is fine inside [...]
    public void Pass_through_normal_identifiers(string name)
    {
        Assert.Equal(name, OleDbValueSampler.EscapeIdentifier(name));
    }

    [Theory]
    [InlineData("Bad]Name")]
    [InlineData("Has\0Null")]
    [InlineData("Has\rCR")]
    [InlineData("Has\nLF")]
    public void Reject_unsafe_identifiers(string name)
    {
        Assert.Throws<ArgumentException>(() => OleDbValueSampler.EscapeIdentifier(name));
    }

    [Fact]
    public void Null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => OleDbValueSampler.EscapeIdentifier(null!));
    }
}
