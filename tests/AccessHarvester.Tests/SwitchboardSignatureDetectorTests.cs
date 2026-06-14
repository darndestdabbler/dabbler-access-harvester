using AccessHarvester.Core.Extraction;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Pins <see cref="SwitchboardSignatureDetector"/> (012 R2-1 / DW-9):
/// detection is by canonical column signature, name- and locale-independent,
/// tolerant of extras, and yields zero false positives on shape-mismatched
/// tables.
/// </summary>
public class SwitchboardSignatureDetectorTests
{
    [Fact]
    public void Detects_canonical_signature()
    {
        var fields = new[] { "SwitchboardID", "ItemNumber", "ItemText", "Command", "Argument" };
        Assert.True(SwitchboardSignatureDetector.IsSwitchboardTable(fields));
    }

    [Fact]
    public void Tolerates_extra_columns_like_ParentID()
    {
        var fields = new[] { "SwitchboardID", "ItemNumber", "ItemText", "Command", "Argument", "ParentID" };
        Assert.True(SwitchboardSignatureDetector.IsSwitchboardTable(fields));
    }

    [Fact]
    public void Is_case_insensitive()
    {
        var fields = new[] { "switchboardid", "itemnumber", "ITEMTEXT", "command", "argument" };
        Assert.True(SwitchboardSignatureDetector.IsSwitchboardTable(fields));
    }

    [Fact]
    public void Rejects_table_missing_a_canonical_column()
    {
        // No "Argument" column.
        var fields = new[] { "SwitchboardID", "ItemNumber", "ItemText", "Command" };
        Assert.False(SwitchboardSignatureDetector.IsSwitchboardTable(fields));
    }

    [Fact]
    public void Rejects_shape_mismatched_business_table()
    {
        var fields = new[] { "ClientID", "FirstName", "LastName", "DOB" };
        Assert.False(SwitchboardSignatureDetector.IsSwitchboardTable(fields));
    }

    [Fact]
    public void Ignores_null_and_whitespace_field_names()
    {
        var fields = new[] { "SwitchboardID", null, "ItemNumber", " ", "ItemText", "Command", "Argument" };
        Assert.True(SwitchboardSignatureDetector.IsSwitchboardTable(fields));
    }

    [Fact]
    public void HasParentIdColumn_detects_presence_case_insensitively()
    {
        Assert.True(SwitchboardSignatureDetector.HasParentIdColumn(new[] { "SwitchboardID", "parentid" }));
        Assert.False(SwitchboardSignatureDetector.HasParentIdColumn(new[] { "SwitchboardID", "ItemNumber" }));
    }
}
