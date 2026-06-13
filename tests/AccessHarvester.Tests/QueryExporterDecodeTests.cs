using AccessHarvester.Core.Extraction;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic pin for the DAO <c>QueryDefTypeEnum</c> decode in
/// <see cref="QueryExporter"/> (006 F-2-1). The prior table was shifted by one
/// slot, so crosstabs (16) decoded as "MakeTable" and downstream classified as
/// possibly-unused action queries. This asserts each documented DAO value decodes
/// to its correct label — the test that would have caught the shift.
/// </summary>
public class QueryExporterDecodeTests
{
    [Theory]
    [InlineData(0, "Select")]            // dbQSelect
    [InlineData(16, "Crosstab")]         // dbQCrosstab
    [InlineData(32, "Delete")]           // dbQDelete
    [InlineData(48, "Update")]           // dbQUpdate
    [InlineData(64, "Append")]           // dbQAppend
    [InlineData(80, "MakeTable")]        // dbQMakeTable
    [InlineData(96, "DataDefinition")]   // dbQDDL
    [InlineData(112, "SqlPassthrough")]  // dbQSQLPassThrough
    [InlineData(128, "Union")]           // dbQSetOperation
    [InlineData(144, "SqlPassthroughBulk")]  // dbQSPTBulk
    [InlineData(160, "Compound")]        // dbQCompound
    [InlineData(224, "Procedure")]       // dbQProcedure
    public void QueryTypeName_DecodesDaoQueryDefTypeEnum(int code, string expected)
    {
        Assert.Equal(expected, QueryExporter.QueryTypeName(code));
    }

    [Fact]
    public void QueryTypeName_UnknownCode_IsLabeledUnknown()
    {
        Assert.Equal("Unknown(999)", QueryExporter.QueryTypeName(999));
    }
}
