using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Sql.Parser;
using AccessHarvester.Sql.Parser.Ast;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Manifest;

// Hermetic unit tests for QuerySqlProjector.
// Each test parses a minimal SQL snippet or creates a minimal
// QueryAbstraction and asserts on the projected QueryItem JSON shape.
public sealed class QuerySqlProjectorTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static QueryAbstraction Build(string name, string sql)
    {
        var result = SqlParserPipeline.Parse(sql);
        var ast = result.BuildAst(name);
        return QueryAbstractionBuilder.Build(name, ast);
    }

    private static JsonNode Project(
        string name, string typeName, string sql,
        QueryAbstraction? abstraction = null)
    {
        abstraction ??= Build(name, sql);
        return QuerySqlProjector.Project(name, typeName, sql, abstraction);
    }

    // ── Tests: basic shape ────────────────────────────────────────────

    [Fact]
    public void ProjectedQueryItem_HasRequiredFields()
    {
        var node = Project("qryTest", "Select", "SELECT a FROM b");

        Assert.Equal("Query", node["ItemKind"]!.GetValue<string>());
        Assert.Equal("qryTest", node["Name"]!.GetValue<string>());
        Assert.Equal("Select", node["QueryType"]!.GetValue<string>());
        Assert.NotNull(node["Sql"]);
        Assert.Equal("SELECT a FROM b", node["Sql"]!["OriginalText"]!.GetValue<string>());
    }

    [Fact]
    public void ParsedSql_AlwaysHasRaw()
    {
        var node = Project("qryTest", "Select", "SELECT a FROM b");
        var parsed = node["Sql"]!["Parsed"]!;

        Assert.Equal("SELECT a FROM b", parsed["Raw"]!.GetValue<string>());
        Assert.Equal("Select", parsed["QueryType"]!.GetValue<string>());
    }

    // ── Tests: QueryType mapping ──────────────────────────────────────

    [Theory]
    [InlineData("Select",         "Select")]
    [InlineData("MakeTable",      "MakeTable")]
    [InlineData("Append",         "Insert")]
    [InlineData("Update",         "Update")]
    [InlineData("Delete",         "Delete")]
    [InlineData("Union",          "Union")]
    [InlineData("CrossTab",       "Crosstab")]
    [InlineData("DataDefinition", "DataDefinition")]
    [InlineData("SqlPassthrough", "PassThrough")]
    public void QueryType_MapsRawNameToSchemaEnum(string rawName, string expected)
    {
        var node = QuerySqlProjector.Project("q", rawName, "SELECT 1", abstraction: null);
        Assert.Equal(expected, node["QueryType"]!.GetValue<string>());
    }

    [Fact]
    public void QueryType_UnknownRawName_EmittedVerbatim()
    {
        var node = QuerySqlProjector.Project("q", "Xbase", "SELECT 1", abstraction: null);
        Assert.Equal("Xbase", node["QueryType"]!.GetValue<string>());
    }

    // ── Tests: Parsed.Select ──────────────────────────────────────────

    [Fact]
    public void ParsedSelect_ProjectsSelectListItems()
    {
        const string sql = "SELECT a, b FROM tbl";
        var node = Project("q", "Select", sql);
        var parsed = node["Sql"]!["Parsed"]!;
        var select = parsed["Select"]?.AsArray();

        Assert.NotNull(select);
        Assert.True(select!.Count >= 2, "Expected at least 2 SELECT items");
        Assert.NotNull(select[0]!["Name"]);
        Assert.NotNull(select[0]!["Expression"]);
        Assert.False(select[0]!["IsAggregate"]!.GetValue<bool>());
    }

    [Fact]
    public void ParsedSelect_AggregateDetected()
    {
        const string sql = "SELECT COUNT(*) AS cnt FROM tbl";
        var node = Project("q", "Select", sql);
        var select = node["Sql"]!["Parsed"]!["Select"]?.AsArray();

        // COUNT(*) should be flagged as aggregate
        Assert.NotNull(select);
        var cntItem = select!.FirstOrDefault(n => n!["IsAggregate"]?.GetValue<bool>() == true);
        Assert.NotNull(cntItem);
    }

    [Fact]
    public void ParsedSelect_StarSelectList_IsNull()
    {
        const string sql = "SELECT * FROM tbl";
        var abstraction = Build("q", sql);
        // SELECT * → ReturnShape is null
        var node = QuerySqlProjector.Project("q", "Select", sql, abstraction);
        var select = node["Sql"]!["Parsed"]!["Select"];

        // SELECT * produces null ReturnShape → null Select
        Assert.Null(select);
    }

    // ── Tests: Parsed.From ────────────────────────────────────────────

    [Fact]
    public void ParsedFrom_ProjectsPrimaryTable()
    {
        const string sql = "SELECT a FROM tblClients AS c";
        var node = Project("q", "Select", sql);
        var from = node["Sql"]!["Parsed"]!["From"]?.AsArray();

        Assert.NotNull(from);
        Assert.Single(from!);
        Assert.Equal("tblClients", from![0]!["TableName"]?.GetValue<string>());
        Assert.Equal("c", from![0]!["Alias"]?.GetValue<string>());
    }

    // ── Tests: PARAMETERS → Sql.Parameters ───────────────────────────

    [Fact]
    public void Parameters_NamedParameter_ProjectsCorrectSource()
    {
        const string sql = "PARAMETERS [Enter ID] Long; SELECT a FROM b WHERE id = [Enter ID]";
        var abstraction = Build("q", sql);
        var node = QuerySqlProjector.Project("q", "Select", sql, abstraction);
        var parameters = node["Sql"]!["Parameters"]?.AsArray();

        Assert.NotNull(parameters);
        Assert.True(parameters!.Count >= 1);
        var first = parameters![0]!;
        Assert.Equal("NamedParameter", first["Source"]!.GetValue<string>());
        Assert.NotNull(first["Name"]);
    }

    // ── Tests: Failed-parse fallback ──────────────────────────────────

    [Fact]
    public void FailedParse_NullAbstraction_EmitsRawOnly()
    {
        const string badSql = "THIS IS NOT VALID SQL AT ALL @@@@";
        var node = QuerySqlProjector.Project("q", "Select", badSql, abstraction: null);
        var parsed = node["Sql"]!["Parsed"]!;

        Assert.Equal(badSql, parsed["Raw"]!.GetValue<string>());
        Assert.Null(parsed["Select"]);
        Assert.Null(parsed["From"]);
        Assert.Null(parsed["Where"]);
    }

    // ── Tests: Abstraction section ────────────────────────────────────

    [Fact]
    public void Abstraction_HasSuggestedMethodName()
    {
        const string sql = "SELECT a FROM tbl";
        var abstraction = Build("qryGetClients", sql);
        var node = QuerySqlProjector.Project("qryGetClients", "Select", sql, abstraction);
        var abstr = node["Abstraction"];

        Assert.NotNull(abstr);
        Assert.NotNull(abstr!["SuggestedMethodName"]);
    }

    // ── Tests: optional DAO fields ────────────────────────────────────

    [Fact]
    public void OptionalDaoFields_EmittedWhenProvided()
    {
        var node = QuerySqlProjector.Project(
            "q", "Select", "SELECT 1",
            abstraction: null,
            connect: null,
            returnsRecords: true,
            odbcTimeout: 30,
            queryTypeCode: 0);

        Assert.Equal(true, node["ReturnsRecords"]!.GetValue<bool>());
        Assert.Equal(30, node["OdbcTimeout"]!.GetValue<int>());
        Assert.Equal(0, node["QueryTypeCode"]!.GetValue<int>());
        Assert.Null(node["Connect"]);
    }
}
