using System;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="RecordSourceResolver"/>. Covers the
/// SELECT-prefix heuristic, the object-name → bracketed-SELECT wrapper,
/// and the rejected-identifier safety check.
/// </summary>
public class RecordSourceResolverTests
{
    [Theory]
    [InlineData("Orders")]
    [InlineData("qryInvoiceDetails")]
    [InlineData("Order Details")]
    public void Resolve_TreatsObjectName_AsTableOrQuery_AndWrapsInSelectStar(string name)
    {
        var resolved = RecordSourceResolver.Resolve(name);

        Assert.Equal(RecordSourceKind.TableOrQueryName, resolved.Kind);
        Assert.Equal(name, resolved.Original);
        Assert.Equal($"SELECT * FROM [{name}]", resolved.SqlToExecute);
    }

    [Fact]
    public void Resolve_UsesSavedQuerySql_WhenProvided_ForQueryBackedRecordSource()
    {
        const string savedQuerySql = "SELECT OrderID, CustomerID FROM Orders";

        var resolved = RecordSourceResolver.Resolve("qryOrders", savedQuerySql);

        Assert.Equal(RecordSourceKind.TableOrQueryName, resolved.Kind);
        Assert.Equal("qryOrders", resolved.Original);
        Assert.Equal(savedQuerySql, resolved.SqlToExecute);
    }

    [Theory]
    [InlineData("SELECT * FROM Orders")]
    [InlineData("select OrderID, CustomerID from Orders where ShipCountry = 'USA'")]
    [InlineData("  SELECT 1")]
    [InlineData("PARAMETERS [MinOrderId] LONG; SELECT * FROM Orders WHERE OrderID >= [MinOrderId]")]
    [InlineData("  parameters [d] datetime ; select * from Orders where OrderDate >= [d]")]
    [InlineData("TRANSFORM Count(*) SELECT Country FROM Customers GROUP BY Country PIVOT ContactTitle")]
    [InlineData("transform Sum([Total]) select OrderYear from Sales group by OrderYear pivot Region")]
    public void Resolve_TreatsInlineSqlPrefixes_AsInlineSql_Verbatim(string sql)
    {
        var resolved = RecordSourceResolver.Resolve(sql);

        Assert.Equal(RecordSourceKind.InlineSql, resolved.Kind);
        Assert.Equal(sql, resolved.Original);
        Assert.Equal(sql, resolved.SqlToExecute);
    }

    [Fact]
    public void Resolve_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => RecordSourceResolver.Resolve(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Resolve_RejectsWhitespaceOnly(string value)
    {
        Assert.Throws<ArgumentException>(() => RecordSourceResolver.Resolve(value));
    }

    [Theory]
    [InlineData("bad]name")]
    [InlineData("with\rcr")]
    [InlineData("with\nlf")]
    public void Resolve_RejectsObjectName_WithBracketUnsafeCharacters(string name)
    {
        Assert.Throws<ArgumentException>(() => RecordSourceResolver.Resolve(name));
    }
}
