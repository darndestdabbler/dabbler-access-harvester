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

    // ── 006 F-1-6: bracket-aware PARAMETERS-clause tokenisation ──────────────────
    // A bracketed prompt name may legally contain ',' and ';' (DAO accepts
    // [Enter start date, please] and [Pick one; carefully] as single parameters).
    // The prior parser split the clause body on a bare ',' (shredding one
    // declared parameter into two garbage entries) and terminated the clause at
    // the first ';' (truncating a name-internal ';'). Both are now bracket-aware.

    [Fact]
    public void ParseParameters_BracketedNameWithComma_IsOneParameter()
    {
        var parameters = QueryMetadataExtractor.ParseParameters(
            "PARAMETERS [Enter start date, please] DateTime;SELECT 1;");

        var declared = parameters!.Where(p => p.Source == "declared").ToArray();
        Assert.Single(declared);
        Assert.Equal("Enter start date, please", declared[0].Name);
        Assert.Equal("DateTime", declared[0].DataType);
    }

    [Fact]
    public void ParseParameters_BracketedNameWithSemicolon_IsNotTruncated()
    {
        var parameters = QueryMetadataExtractor.ParseParameters(
            "PARAMETERS [Pick one; carefully] Text;SELECT 1;");

        var declared = parameters!.Where(p => p.Source == "declared").ToArray();
        Assert.Single(declared);
        Assert.Equal("Pick one; carefully", declared[0].Name);
        Assert.Equal("Text", declared[0].DataType);
    }

    [Fact]
    public void ParseParameters_TwoBracketedNamesEachWithComma_AreTwoParameters()
    {
        var parameters = QueryMetadataExtractor.ParseParameters(
            "PARAMETERS [Start, please] DateTime, [End, also] DateTime;SELECT 1;");

        var declared = parameters!.Where(p => p.Source == "declared").ToArray();
        Assert.Equal(2, declared.Length);
        Assert.Equal("Start, please", declared[0].Name);
        Assert.Equal("End, also", declared[1].Name);
    }

    [Fact]
    public void ParseParameters_OrdinaryTwoParamClause_IsUnchanged()
    {
        // Regression guard: the HHC-shaped clause (no name-internal punctuation)
        // must still yield exactly two declared parameters — the bracket-aware
        // change is behaviour-preserving for ordinary clauses (golden-neutral).
        var parameters = QueryMetadataExtractor.ParseParameters(
            "PARAMETERS [Enter Provider ID:] Long, [Enter Fiscal Year:] Long;SELECT 1;");

        var declared = parameters!.Where(p => p.Source == "declared").ToArray();
        Assert.Equal(2, declared.Length);
        Assert.Equal("Enter Provider ID:", declared[0].Name);
        Assert.Equal("Long", declared[0].DataType);
        Assert.Equal("Enter Fiscal Year:", declared[1].Name);
        Assert.Equal("Long", declared[1].DataType);
    }

    // 006 F-1-6 follow-up (devil's-advocate, both providers): a doubled ']]' is a
    // Jet/Access escaped literal ']' inside a bracketed name, not the terminator.
    // The first-pass fix closed the name at the first ']' (and the depth scanners
    // zeroed depth on it), so an escaped-']' name was misparsed.

    [Fact]
    public void ParseParameters_EscapedBracketInName_UnescapesAndKeepsType()
    {
        // GPT reproducer: was Name="a", DataType="]b] Text".
        var parameters = QueryMetadataExtractor.ParseParameters(
            "PARAMETERS [a]]b] Text;SELECT 1;");

        var declared = parameters!.Where(p => p.Source == "declared").ToArray();
        Assert.Single(declared);
        Assert.Equal("a]b", declared[0].Name);
        Assert.Equal("Text", declared[0].DataType);
    }

    [Fact]
    public void ParseParameters_CommaAfterEscapedBracketInName_DoesNotSplit()
    {
        // Gemini's sharper case: a ',' AFTER the escaped ']]' is still inside the
        // bracketed name, so the depth scanner must not split on it.
        var parameters = QueryMetadataExtractor.ParseParameters(
            "PARAMETERS [Has]]Bracket, And Comma] Long, [p2] Text;SELECT 1;");

        var declared = parameters!.Where(p => p.Source == "declared").ToArray();
        Assert.Equal(2, declared.Length);
        Assert.Equal("Has]Bracket, And Comma", declared[0].Name);
        Assert.Equal("Long", declared[0].DataType);
        Assert.Equal("p2", declared[1].Name);
        Assert.Equal("Text", declared[1].DataType);
    }
}
