using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

public class QueryTableReferenceCollectorTests
{
    private static IrDatabase DbWithQuery(string name, string? sql, int typeCode = 0) =>
        new()
        {
            Name = "Test",
            Forms = [], Reports = [], Tables = [], Relationships = [], Macros = [], Modules = [],
            Queries = [new IrQuery { Name = name, QueryTypeCode = typeCode, Sql = sql }],
        };

    [Fact]
    public void Collect_Returns_Empty_When_No_Queries()
    {
        var db = new IrDatabase
        {
            Name = "Test",
            Forms = [], Reports = [], Queries = [], Tables = [],
            Relationships = [], Macros = [], Modules = [],
        };
        var result = QueryTableReferenceCollector.Collect(db);
        Assert.Empty(result);
    }

    [Fact]
    public void Collect_Simple_From_Clause_Extracts_Table()
    {
        var db = DbWithQuery("qryClients", "SELECT * FROM tblClients");
        var refs = QueryTableReferenceCollector.Collect(db);
        Assert.Single(refs);
        Assert.Equal("qryClients", refs[0].QueryName);
        Assert.Equal("tblClients", refs[0].ReferencedObject);
    }

    [Fact]
    public void Collect_Join_Clause_Extracts_Table()
    {
        var sql = "SELECT * FROM tblClients INNER JOIN tblOrders ON tblClients.ID = tblOrders.ClientID";
        var db = DbWithQuery("qryJoined", sql);
        var refs = QueryTableReferenceCollector.Collect(db);
        var names = refs.Select(r => r.ReferencedObject).ToList();
        Assert.Contains("tblClients", names);
        Assert.Contains("tblOrders", names);
    }

    [Fact]
    public void Collect_From_With_Alias_Strips_Alias()
    {
        var db = DbWithQuery("qryAliased", "SELECT c.Name FROM tblClients AS c");
        var refs = QueryTableReferenceCollector.Collect(db);
        Assert.Single(refs);
        Assert.Equal("tblClients", refs[0].ReferencedObject);
    }

    [Fact]
    public void Collect_Null_Sql_Returns_No_References()
    {
        var db = DbWithQuery("qryEmpty", null);
        var refs = QueryTableReferenceCollector.Collect(db);
        Assert.Empty(refs);
    }

    [Fact]
    public void Collect_Deduplicated_Per_Query()
    {
        // Same table appears in both FROM and JOIN
        var sql = "SELECT * FROM tblClients, tblClients AS c2";
        var db = DbWithQuery("qryDup", sql);
        var refs = QueryTableReferenceCollector.Collect(db);
        // Should appear only once
        Assert.Single(refs.Where(r => r.ReferencedObject == "tblClients"));
    }

    [Fact]
    public void ExtractTableNames_HandlesSimpleSelect()
    {
        var names = QueryTableReferenceCollector.ExtractTableNames("SELECT * FROM tblA");
        Assert.Contains("tblA", names);
    }

    [Fact]
    public void ExtractTableNames_HandlesMultipleJoins()
    {
        var sql = "SELECT * FROM tblA LEFT JOIN tblB ON tblA.ID = tblB.AID LEFT JOIN tblC ON tblA.ID = tblC.AID";
        var names = QueryTableReferenceCollector.ExtractTableNames(sql);
        Assert.Contains("tblA", names);
        Assert.Contains("tblB", names);
        Assert.Contains("tblC", names);
    }
}
