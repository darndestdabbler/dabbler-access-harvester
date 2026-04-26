using System.Linq;
using AccessHarvester.Access;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="QueryMetadataExtractor"/>. Exercises the
/// raw-JSON -> typed-record transform and the regex-based parameter detection
/// against the SQL surface forms documented in Plan Phase 4.3.
/// </summary>
public class QueryMetadataExtractorTests
{
    [Fact]
    public void Extract_LiftsTopLevelExactValueFields()
    {
        const string raw = """
        {
          "Name": "qrySelectCustomers",
          "Type": "Query",
          "QueryTypeCode": 0,
          "QueryTypeName": "Select",
          "Sql": "SELECT * FROM Customers;",
          "Connect": null,
          "ReturnsRecords": true,
          "OdbcTimeout": 60
        }
        """;

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.Equal("qrySelectCustomers", meta.Name);
        Assert.Equal("Select", meta.QueryType);
        Assert.Equal("SELECT * FROM Customers;", meta.Sql);
        Assert.Null(meta.Connect);
        Assert.True(meta.ReturnsRecords);
        Assert.Equal(60, meta.OdbcTimeout);
    }

    [Fact]
    public void Extract_RemovesPromotedExactValueKeysFromPropertiesBag()
    {
        const string raw = """
        {
          "Name": "qry",
          "Type": "Query",
          "QueryTypeCode": 0,
          "QueryTypeName": "Select",
          "Sql": "SELECT 1;",
          "Connect": "",
          "ReturnsRecords": true,
          "OdbcTimeout": 60,
          "ExtraProp": "kept"
        }
        """;

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.NotNull(meta.Properties);
        Assert.False(meta.Properties!.ContainsKey("Name"));
        Assert.False(meta.Properties.ContainsKey("Type"));
        Assert.False(meta.Properties.ContainsKey("Sql"));
        Assert.False(meta.Properties.ContainsKey("Connect"));
        Assert.False(meta.Properties.ContainsKey("ReturnsRecords"));
        Assert.False(meta.Properties.ContainsKey("OdbcTimeout"));
        Assert.False(meta.Properties.ContainsKey("QueryTypeName"));
        Assert.True(meta.Properties.ContainsKey("ExtraProp"));
        Assert.Equal("kept", meta.Properties["ExtraProp"].GetString());
    }

    [Fact]
    public void Extract_PreservesRawQueryTypeCodeInProperties()
    {
        // Decoded-lift: QueryType (decoded label) is promoted typed; raw
        // QueryTypeCode remains in Properties for round-trip fidelity.
        const string raw = """
        {
          "Name": "qry",
          "Type": "Query",
          "QueryTypeCode": 112,
          "QueryTypeName": "CrossTab",
          "Sql": "TRANSFORM Sum(x) SELECT 1;"
        }
        """;

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.Equal("CrossTab", meta.QueryType);
        Assert.NotNull(meta.Properties);
        Assert.True(meta.Properties!.ContainsKey("QueryTypeCode"));
        Assert.Equal(112, meta.Properties["QueryTypeCode"].GetInt32());
    }

    [Fact]
    public void Extract_NullSqlYieldsNullParameters()
    {
        const string raw = """
        { "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": null }
        """;
        var meta = QueryMetadataExtractor.Extract(raw);
        Assert.Null(meta.Parameters);
    }

    [Fact]
    public void Extract_DeclaredParametersClause_ParsesNamesAndTypes()
    {
        const string sql = "PARAMETERS [Enter Customer ID] Long, [StartDate] DateTime; SELECT * FROM Orders;";
        var raw = $$"""{ "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": "{{sql.Replace("\"", "\\\"")}}" }""";

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.NotNull(meta.Parameters);
        var declared = meta.Parameters!.Where(p => p.Source == "declared").ToList();
        Assert.Equal(2, declared.Count);
        Assert.Equal("Enter Customer ID", declared[0].Name);
        Assert.Equal("Long", declared[0].DataType);
        Assert.Equal("StartDate", declared[1].Name);
        Assert.Equal("DateTime", declared[1].DataType);
    }

    [Fact]
    public void Extract_DeclaredParameterWithoutBracketsAndWithoutType_LiftsName()
    {
        const string sql = "PARAMETERS pBare; SELECT 1;";
        var raw = $$"""{ "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": "{{sql}}" }""";

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.NotNull(meta.Parameters);
        var p = meta.Parameters!.Single();
        Assert.Equal("declared", p.Source);
        Assert.Equal("pBare", p.Name);
        Assert.Null(p.DataType);
    }

    [Fact]
    public void Extract_FormReferenceParameter_LiftsLeafAndPath()
    {
        const string sql = "SELECT * FROM Orders WHERE CustomerId = [Forms]![frmCustomers]![txtId];";
        var raw = $$"""{ "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": "{{sql}}" }""";

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.NotNull(meta.Parameters);
        var p = meta.Parameters!.Single();
        Assert.Equal("form-reference", p.Source);
        Assert.Equal("txtId", p.Name);
        Assert.Equal("[Forms]![frmCustomers]![txtId]", p.Path);
    }

    [Fact]
    public void Extract_BareFormReference_UsesFormNameAsLeaf()
    {
        const string sql = "SELECT * FROM Orders WHERE Active = [Forms]![frmCustomers];";
        var raw = $$"""{ "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": "{{sql}}" }""";

        var meta = QueryMetadataExtractor.Extract(raw);

        var p = meta.Parameters!.Single();
        Assert.Equal("form-reference", p.Source);
        Assert.Equal("frmCustomers", p.Name);
        Assert.Equal("[Forms]![frmCustomers]", p.Path);
    }

    [Fact]
    public void Extract_ReportReferenceParameter_DistinguishedFromForm()
    {
        const string sql = "SELECT * FROM T WHERE X = [Reports]![rptDaily]![txtCount];";
        var raw = $$"""{ "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": "{{sql}}" }""";

        var meta = QueryMetadataExtractor.Extract(raw);

        var p = meta.Parameters!.Single();
        Assert.Equal("report-reference", p.Source);
        Assert.Equal("txtCount", p.Name);
    }

    [Fact]
    public void Extract_DuplicateFormReferences_DeduplicatedByPath()
    {
        const string sql =
            "SELECT * FROM Orders WHERE CustomerId = [Forms]![frmCust]![txtId] " +
            "OR ParentId = [Forms]![frmCust]![txtId];";
        var raw = $$"""{ "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": "{{sql}}" }""";

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.Single(meta.Parameters!);
    }

    [Fact]
    public void Extract_DeclaredAndImplicitTogether_BothSurfaced()
    {
        const string sql =
            "PARAMETERS [pStart] DateTime; SELECT * FROM Orders WHERE CustomerId = [Forms]![frm]![txt];";
        var raw = $$"""{ "Name": "qry", "Type": "Query", "QueryTypeName": "Select", "Sql": "{{sql}}" }""";

        var meta = QueryMetadataExtractor.Extract(raw);

        Assert.Equal(2, meta.Parameters!.Count);
        Assert.Contains(meta.Parameters, p => p.Source == "declared" && p.Name == "pStart");
        Assert.Contains(meta.Parameters, p => p.Source == "form-reference" && p.Path == "[Forms]![frm]![txt]");
    }

    [Fact]
    public void Extract_MissingName_Throws()
    {
        const string raw = """{ "Type": "Query", "QueryTypeName": "Select" }""";
        Assert.Throws<System.ArgumentException>(() => QueryMetadataExtractor.Extract(raw));
    }

    [Fact]
    public void Extract_RootArray_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => QueryMetadataExtractor.Extract("[]"));
    }
}
