using AccessHarvester.Core.Extraction;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Pins <see cref="RibbonSignatureDetector"/> (016 / DW-13): detection is by
/// the (<c>RibbonName</c>, <c>RibbonXML</c>) column signature — name- and
/// locale-independent, tolerant of extras (the <c>ID</c> autonumber), and
/// yields zero false positives on shape-mismatched tables.
/// </summary>
public class RibbonSignatureDetectorTests
{
    [Fact]
    public void Detects_canonical_USysRibbons_signature()
    {
        var fields = new[] { "ID", "RibbonName", "RibbonXML" };
        Assert.True(RibbonSignatureDetector.IsRibbonTable(fields));
    }

    [Fact]
    public void Is_case_insensitive()
    {
        var fields = new[] { "ribbonname", "RIBBONXML" };
        Assert.True(RibbonSignatureDetector.IsRibbonTable(fields));
    }

    [Fact]
    public void Tolerates_extra_columns()
    {
        var fields = new[] { "ID", "RibbonName", "RibbonXML", "Notes", "ModifiedOn" };
        Assert.True(RibbonSignatureDetector.IsRibbonTable(fields));
    }

    [Theory]
    [InlineData("RibbonName")]                      // missing RibbonXML
    [InlineData("RibbonXML")]                       // missing RibbonName
    [InlineData("SwitchboardID", "ItemText")]       // unrelated table
    public void Rejects_shape_mismatched_tables(params string[] fields)
    {
        Assert.False(RibbonSignatureDetector.IsRibbonTable(fields));
    }

    [Fact]
    public void Ignores_null_and_whitespace_field_names()
    {
        var fields = new[] { null, "  ", "RibbonName", "RibbonXML" };
        Assert.True(RibbonSignatureDetector.IsRibbonTable(fields));
    }
}
